using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using ProphetsWay.Utilities.Generics;

namespace ProphetsWay.Utilities
{
	public static partial class Logger
	{
		internal static LogLevels MapMicrosoftLogLevel(LogLevel logLevel)
		{
			switch (logLevel)
			{
				case LogLevel.Trace: return LogLevels.TraceOnly;
				case LogLevel.Debug: return LogLevels.DebugOnly;
				case LogLevel.Information: return LogLevels.InformationOnly;
				case LogLevel.Warning: return LogLevels.WarningOnly;
				case LogLevel.Error: return LogLevels.ErrorOnly;
				case LogLevel.Critical: return LogLevels.Critical;
				case LogLevel.None: return 0;
				default: throw new ArgumentOutOfRangeException(nameof(logLevel));
			}
		}

		internal static bool IsMicrosoftLogEnabled(LogLevel logLevel)
		{
			var level = MapMicrosoftLogLevel(logLevel);
			if (level == 0)
				return false;

			TypedRegistration[] destinations;
			lock (DestinationLock)
			{
				destinations = Destinations.TryGetValue(typeof(LogBridgeEntry), out var route)
					? route.ToArray()
					: Array.Empty<TypedRegistration>();
			}

			if (!Array.Exists(destinations, registration => registration.Settings.Enabled))
				return true;

			foreach (var registration in destinations)
			{
				var settings = registration.Settings;
				if (settings.Enabled && (settings.ReportingLevel & level) == level)
					return true;
			}

			return false;
		}

		internal static void DispatchMicrosoftInbound<TState>(string categoryName, LogLevels level,
			EventId eventId, TState state, Exception exception,
			Func<TState, Exception, string> formatter,
			IExternalScopeProvider scopeProvider, IMicrosoftLogLabelMapper labelMapper)
		{
			TypedRegistration[] destinations;
			DestinationLabelPolicy[] intrinsicPolicies;
			LogContext nativeContext;
			ReadOnlyCollection<KeyValuePair<string, object>> properties;
			List<LogBridgeScope> scopes;
			LogAnnotations entryAnnotations;
			List<SensitivityLabel> effectiveLabels;
			var failures = (List<LogFailureDescriptor>)null;
			var overflowCount = 0;
			var coreCaptureFailures = 0;
			var mustThrow = false;

			try
			{
				lock (DestinationLock)
				{
					destinations = Destinations.TryGetValue(typeof(LogBridgeEntry), out var route)
						? route.ToArray()
						: Array.Empty<TypedRegistration>();
				}

				intrinsicPolicies = new DestinationLabelPolicy[destinations.Length];
				for (var index = 0; index < destinations.Length; index++)
				{
					var supplied = destinations[index].Destination as LoggingDestinationCore;
					if (supplied != null)
						intrinsicPolicies[index] = supplied.LabelPolicy;
				}

				nativeContext = LogScopeHandle.Capture(null, DateTimeOffset.UtcNow);
				properties = CaptureMicrosoftProperties(state);
				scopes = new List<LogBridgeScope>();
				scopeProvider.ForEachScope((scopeState, capturedScopes) =>
				{
					var scopeProperties = CaptureMicrosoftProperties(scopeState);
					var annotations = labelMapper == null
						? null
						: labelMapper.MapScope(scopeState, scopeProperties);
					capturedScopes.Add(new LogBridgeScope(scopeState, scopeProperties, annotations));
				}, scopes);

				entryAnnotations = labelMapper == null
					? null
					: labelMapper.MapEntry(categoryName, GetMicrosoftLevel(level), eventId,
						state, exception, properties);

				effectiveLabels = new List<SensitivityLabel>();
				var seenLabels = new HashSet<SensitivityLabel>();
				AddLabels(nativeContext.Labels.EffectiveLabels, seenLabels, effectiveLabels);
				AddLabels(entryAnnotations, seenLabels, effectiveLabels);
				foreach (var scope in scopes)
					AddLabels(scope.Annotations, seenLabels, effectiveLabels);
			}
			catch (Exception)
			{
				coreCaptureFailures = 1;
				CompleteDispatchFailure(null, 0, coreCaptureFailures, false);
				return;
			}

			var hasEnabledRegistration = Array.Exists(destinations,
				registration => registration.Settings.Enabled);
			if (!hasEnabledRegistration)
			{
				DeliverMicrosoftAutomatic(categoryName, level, destinations.Length + 1, eventId, state, exception,
					formatter, properties, scopes, entryAnnotations, nativeContext);
				return;
			}

			var candidates = new List<TypedRegistration>();
			var candidateIds = new List<int>();
			for (var index = 0; index < destinations.Length; index++)
			{
				var registration = destinations[index];
				var settings = registration.Settings;
				if (!settings.Enabled || (settings.ReportingLevel & level) != level)
					continue;

				try
				{
					if (settings.LabelPolicy.Mode != LabelFilterMode.NoFilter
						&& !settings.LabelPolicy.Allows(effectiveLabels))
						continue;

					var intrinsicPolicy = intrinsicPolicies[index];
					if (intrinsicPolicy != null && intrinsicPolicy.Mode != LabelFilterMode.NoFilter
						&& !intrinsicPolicy.Allows(effectiveLabels))
						continue;
				}
				catch (Exception)
				{
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.LabelCheck);
					continue;
				}

				try
				{
					var destination = (ILoggingDestination<LogBridgeEntry>)registration.Destination;
					if (!destination.ValidateMessageLevel(level))
						continue;
				}
				catch (Exception)
				{
					mustThrow = true;
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.Eligibility);
					continue;
				}

				candidates.Add(registration);
				candidateIds.Add(index + 1);
			}

			if (candidates.Count == 0)
			{
				CompleteDispatchFailure(failures, overflowCount, coreCaptureFailures, mustThrow);
				return;
			}

			string formattedMessage;
			try
			{
				formattedMessage = formatter(state, exception);
			}
			catch (Exception)
			{
				coreCaptureFailures = 1;
				CompleteDispatchFailure(failures, overflowCount, coreCaptureFailures, mustThrow);
				return;
			}

			LogBridgeEntry<TState> entry;
			try
			{
				entry = new LogBridgeEntry<TState>(state, formatter, categoryName, eventId, level,
					null, formattedMessage, exception, properties, scopes, entryAnnotations, nativeContext);
			}
			catch (Exception)
			{
				coreCaptureFailures = 1;
				CompleteDispatchFailure(failures, overflowCount, coreCaptureFailures, mustThrow);
				return;
			}

			for (var index = 0; index < candidates.Count; index++)
			{
				var registration = candidates[index];
				var destination = (ILoggingDestination<LogBridgeEntry>)registration.Destination;
				try
				{
					var supplied = destination as Generics.BaseLoggingDestination<LogBridgeEntry>;
					var contextual = destination as Generics.IContextLoggingDestination<LogBridgeEntry>;
					if (supplied != null)
						supplied.LogCaptured(nativeContext, level, entry, formattedMessage, exception);
					else if (contextual != null)
						contextual.LogWithContext(nativeContext, level, entry, formattedMessage, exception);
					else
						destination.Log(level, entry, formattedMessage, exception);
				}
				catch (Exception)
				{
					mustThrow = true;
					RecordFailure(ref failures, ref overflowCount, candidateIds[index], LogFailureStage.Output);
				}
			}

			CompleteDispatchFailure(failures, overflowCount, coreCaptureFailures, mustThrow);
		}

		private static void DeliverMicrosoftAutomatic<TState>(string categoryName, LogLevels level, int registrationId,
			EventId eventId, TState state, Exception exception,
			Func<TState, Exception, string> formatter,
			ReadOnlyCollection<KeyValuePair<string, object>> properties,
			List<LogBridgeScope> scopes, LogAnnotations annotations, LogContext nativeContext)
		{
			string formattedMessage;
			try
			{
				formattedMessage = formatter(state, exception);
			}
			catch (Exception)
			{
				CompleteDispatchFailure(null, 0, 1, false);
				return;
			}

			LogBridgeEntry<TState> entry;
			try
			{
				entry = new LogBridgeEntry<TState>(state, formatter, categoryName, eventId, level,
					null, formattedMessage, exception, properties, scopes, annotations, nativeContext);
			}
			catch (Exception)
			{
				CompleteDispatchFailure(null, 0, 1, false);
				return;
			}

			DispatchAutomatic(registrationId,
				print => new AutomaticTypedDestination<LogBridgeEntry>(print)
					.LogCaptured(nativeContext, level, entry, formattedMessage, exception));
		}

		private static ReadOnlyCollection<KeyValuePair<string, object>> CaptureMicrosoftProperties<TState>(TState state)
		{
			var properties = (object)state as IEnumerable<KeyValuePair<string, object>>;
			return properties == null
				? LogContext.CopyMembership(new KeyValuePair<string, object>[0])
				: LogContext.CopyMembership(properties);
		}

		private static ReadOnlyCollection<KeyValuePair<string, object>> CaptureMicrosoftProperties(object state)
		{
			var properties = state as IEnumerable<KeyValuePair<string, object>>;
			return properties == null
				? LogContext.CopyMembership(new KeyValuePair<string, object>[0])
				: LogContext.CopyMembership(properties);
		}

		private static void AddLabels(IEnumerable<SensitivityLabel> labels,
			HashSet<SensitivityLabel> seen, List<SensitivityLabel> result)
		{
			foreach (var label in labels)
				if (seen.Add(label))
					result.Add(label);
		}

		private static void AddLabels(LogAnnotations annotations,
			HashSet<SensitivityLabel> seen, List<SensitivityLabel> result)
		{
			if (annotations != null)
				AddLabels(annotations.LabelOccurrences, seen, result);
		}

		private static LogLevel GetMicrosoftLevel(LogLevels level)
		{
			switch (level)
			{
				case LogLevels.TraceOnly: return LogLevel.Trace;
				case LogLevels.DebugOnly: return LogLevel.Debug;
				case LogLevels.InformationOnly: return LogLevel.Information;
				case LogLevels.WarningOnly: return LogLevel.Warning;
				case LogLevels.ErrorOnly: return LogLevel.Error;
				case LogLevels.Critical: return LogLevel.Critical;
				default: throw new ArgumentOutOfRangeException(nameof(level));
			}
		}
	}
}
