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
			var transfer = _currentMicrosoftTransfer;
			if (transfer != null && ReferenceEquals(transfer.Dispatch, _currentMicrosoftDispatch)
				&& transfer.Matches(level, eventId, state, exception, formatter))
			{
				var dispatch = transfer.Dispatch;
				var route = typeof(LogBridgeEntry);
				if (dispatch.HasVisitedRoute(route))
					return;

				var oldPrefix = dispatch.ActivePrefix;
				var oldPayload = dispatch.CurrentPayload;
				var oldCaptureFailed = dispatch.BridgeCaptureFailed;
				var oldDepth = dispatch.ContinuationDepth;
				try
				{
					dispatch.ContinuationDepth++;
					DispatchMicrosoftInboundCore(categoryName, transfer.Payload.Entry.OriginalLevel, eventId, state, exception,
						formatter, scopeProvider, labelMapper, transfer);
				}
				finally
				{
					dispatch.ActivePrefix = oldPrefix;
					dispatch.CurrentPayload = oldPayload;
					dispatch.BridgeCaptureFailed = oldCaptureFailed;
					dispatch.ContinuationDepth = oldDepth;
				}
				return;
			}

			var previous = _currentMicrosoftDispatch;
			_currentMicrosoftDispatch = new MicrosoftDispatchContext();
			try
			{
				DispatchMicrosoftInboundCore(categoryName, level, eventId, state, exception,
					formatter, scopeProvider, labelMapper, null);
			}
			finally
			{
				_currentMicrosoftDispatch = previous;
			}
		}

		private static void DispatchMicrosoftInboundCore<TState>(string categoryName, LogLevels level,
			EventId eventId, TState state, Exception exception,
			Func<TState, Exception, string> formatter,
			IExternalScopeProvider scopeProvider, IMicrosoftLogLabelMapper labelMapper,
			MicrosoftOutboundTransfer transfer)
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
			var hasEnabledRegistration = false;

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

				hasEnabledRegistration = Array.Exists(destinations,
					registration => registration.Settings.Enabled);
				ReserveCurrentRouteBlock(typeof(LogBridgeEntry),
					destinations.Length + (hasEnabledRegistration ? 0 : 1), true);

				nativeContext = LogScopeHandle.Capture(null, DateTimeOffset.UtcNow);
				if (transfer == null)
				{
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
				else
				{
					properties = transfer.Payload.Entry.Properties;
					var addedExternalScopes = new List<LogBridgeScope>();
					scopeProvider.ForEachScope((scopeState, capturedScopes) =>
					{
						if (transfer.IsExportedScope(scopeState))
							return;

						var scopeProperties = CaptureMicrosoftProperties(scopeState);
						var annotations = labelMapper == null
							? null
							: labelMapper.MapScope(scopeState, scopeProperties);
						capturedScopes.Add(new LogBridgeScope(scopeState, scopeProperties, annotations));
					}, addedExternalScopes);

					scopes = new List<LogBridgeScope>(transfer.Payload.ExternalScopes);
				scopes.AddRange(addedExternalScopes);
					entryAnnotations = transfer.Payload.Entry.Annotations;
					effectiveLabels = ComposeEffectiveLabels(transfer.Payload, nativeContext, addedExternalScopes);
					_currentMicrosoftDispatch.CurrentPayload = ComposeControlledPayload(
						transfer.Payload, nativeContext, addedExternalScopes, effectiveLabels);
					_currentMicrosoftDispatch.BridgeCaptureFailed = false;
				}
			}
			catch (Exception)
			{
				coreCaptureFailures = 1;
				CompleteDispatchFailure(null, 0, coreCaptureFailures, false);
				return;
			}

			if (!hasEnabledRegistration)
			{
				if (transfer == null)
				{
					DeliverMicrosoftAutomatic(categoryName, level, destinations.Length + 1, eventId, state, exception,
						formatter, properties, scopes, entryAnnotations, nativeContext);
				}
				else
				{
					DispatchAutomatic(destinations.Length + 1,
						print => new AutomaticTypedDestination<LogBridgeEntry>(print)
							.LogCaptured(nativeContext, level, transfer.Payload.Entry,
								transfer.Payload.Entry.FormattedMessage, transfer.Payload.Entry.Exception));
				}
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

			LogBridgeEntry entry;
			string formattedMessage;
			if (transfer != null)
			{
				entry = transfer.Payload.Entry;
				formattedMessage = entry.FormattedMessage;
			}
			else
			{
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

				_currentMicrosoftDispatch.CurrentPayload = CreateBridgePayload(entry,
					new[] { nativeContext }, scopes, effectiveLabels);
				_currentMicrosoftDispatch.BridgeCaptureFailed = false;
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

		[ThreadStatic]
		private static MicrosoftDispatchContext _currentMicrosoftDispatch;
		[ThreadStatic]
		private static MicrosoftOutboundTransfer _currentMicrosoftTransfer;
		private static readonly object OrdinaryMicrosoftRoute = new object();

		private sealed class MicrosoftDispatchContext
		{
			private readonly List<LogFailureDescriptor> _failures = new List<LogFailureDescriptor>(RetainedFailureLimit);
			private readonly List<object> _visitedRoutes = new List<object>();
			private int _overflowCount;
			private int _coreCaptureFailureCount;
			private int _totalSlots;
			private bool _mustThrow;
			private bool _initialFailure;

			internal int ActivePrefix;
			internal int ContinuationDepth;
			internal MicrosoftBridgePayload CurrentPayload;
			internal bool BridgeCaptureFailed;
			internal List<LogFailureDescriptor> Failures { get { return _failures; } }
			internal int OverflowCount { get { return _overflowCount; } }
			internal int CoreCaptureFailureCount { get { return _coreCaptureFailureCount; } }
			internal bool MustThrow { get { return _mustThrow; } }
			internal bool InitialFailure { get { return _initialFailure; } }

			internal bool HasVisitedRoute(object route)
			{
				for (var index = 0; index < _visitedRoutes.Count; index++)
					if (ReferenceEquals(_visitedRoutes[index], route))
						return true;
				return false;
			}

			internal void MarkVisitedRoute(object route)
			{
				if (!HasVisitedRoute(route))
					_visitedRoutes.Add(route);
			}

			internal int ReserveBlock(int count)
			{
				var prefix = _totalSlots;
				_totalSlots = checked(_totalSlots + count);
				ActivePrefix = prefix;
				return prefix;
			}

			internal void RecordFailure(int localRegistrationId, LogFailureStage stage)
			{
				if (_failures.Count < RetainedFailureLimit)
					_failures.Add(new LogFailureDescriptor(checked(ActivePrefix + localRegistrationId), stage));
				else
					_overflowCount++;
			}

			internal void Merge(List<LogFailureDescriptor> failures, int overflowCount,
				int coreCaptureFailureCount, bool mustThrow, bool initialFailure)
			{
				if (failures != null)
					foreach (var failure in failures)
						RecordFailure(failure.RegistrationId, failure.Stage);
				_overflowCount += overflowCount;
				if (coreCaptureFailureCount > 0)
					_coreCaptureFailureCount = 1;
				_mustThrow |= mustThrow;
				_initialFailure |= initialFailure;
			}
		}

		private sealed class MicrosoftBridgePayload
		{
			internal readonly LogBridgeEntry Entry;
			internal readonly ReadOnlyCollection<LogContext> NativeContexts;
			internal readonly ReadOnlyCollection<LogBridgeScope> ExternalScopes;
			internal readonly List<SensitivityLabel> EffectiveLabels;

			internal MicrosoftBridgePayload(LogBridgeEntry entry, IEnumerable<LogContext> nativeContexts,
				IEnumerable<LogBridgeScope> externalScopes, IEnumerable<SensitivityLabel> effectiveLabels)
			{
				Entry = entry;
				NativeContexts = LogContext.CopyMembership(nativeContexts);
				ExternalScopes = LogContext.CopyMembership(externalScopes);
				EffectiveLabels = new List<SensitivityLabel>(effectiveLabels);
			}
		}

		private sealed class MicrosoftOutboundTransfer
		{
			private readonly object _state;
			private readonly LogLevel _level;
			private readonly EventId _eventId;
			private readonly Exception _exception;
			private readonly object _formatter;
			private readonly List<object> _scopeStates = new List<object>();

			internal readonly MicrosoftDispatchContext Dispatch;
			internal readonly MicrosoftBridgePayload Payload;

			internal MicrosoftOutboundTransfer(MicrosoftDispatchContext dispatch, MicrosoftBridgePayload payload,
				object state, LogLevel level, EventId eventId, Exception exception, object formatter)
			{
				Dispatch = dispatch;
				Payload = payload;
				_state = state;
				_level = level;
				_eventId = eventId;
				_exception = exception;
				_formatter = formatter;
			}

			internal bool Matches<TState>(LogLevels level, EventId eventId, TState state,
				Exception exception, Func<TState, Exception, string> formatter)
			{
				if (MapMicrosoftLogLevel(_level) != level || !_eventId.Equals(eventId)
					|| !ReferenceEquals(_state, (object)state)
					|| !ReferenceEquals(_exception, exception) || !ReferenceEquals(_formatter, formatter))
					return false;

				return true;
			}

			internal void AddScopeState(object state)
			{
				_scopeStates.Add(state);
			}

			internal bool IsExportedScope(object state)
			{
				for (var index = 0; index < _scopeStates.Count; index++)
					if (ReferenceEquals(_scopeStates[index], state))
						return true;
				return false;
			}
		}

		private static int ReserveCurrentRouteBlock(object route, int slotCount, bool markVisited)
		{
			var dispatch = _currentMicrosoftDispatch;
			if (dispatch == null)
				return 0;
			if (markVisited)
				dispatch.MarkVisitedRoute(route);
			return dispatch.ReserveBlock(slotCount);
		}

		internal static void ExportMicrosoft<TState>(ILoggerFactory factory, ILogger fixedLogger,
			string defaultCategoryName, LogContext context, LogLevels level, TState state,
			string rawMessage, string formattedMessage, Exception exception)
		{
			var dispatch = _currentMicrosoftDispatch;
			if (dispatch == null)
				throw new InvalidOperationException("Microsoft output requires a guarded Logger dispatch.");
			if (dispatch.BridgeCaptureFailed)
				return;

			var payload = dispatch.CurrentPayload;
			if (payload == null)
			{
				try
				{
					payload = CreateRawBridgePayload(context, level, state, rawMessage, formattedMessage, exception);
					dispatch.CurrentPayload = payload;
				}
				catch (Exception)
				{
					dispatch.BridgeCaptureFailed = true;
					dispatch.Merge(null, 0, 1, false, false);
					return;
				}
			}

			var entry = payload.Entry;
			var microsoftLevel = GetMicrosoftLevel(HighestPriorityBit(entry.OriginalLevel));
			var selectedLogger = fixedLogger;
			if (selectedLogger == null)
			{
				var category = entry.CategoryName ?? defaultCategoryName;
				selectedLogger = factory.CreateLogger(category);
				if (selectedLogger == null)
					throw new InvalidOperationException("The Microsoft logger factory returned null.");
			}

			if (!selectedLogger.IsEnabled(microsoftLevel))
				return;

			var eventState = new MicrosoftLogTransport(new[]
			{
				new KeyValuePair<string, object>("ProphetsWay.Entry", entry),
				new KeyValuePair<string, object>("ProphetsWay.NativeContexts", payload.NativeContexts),
				new KeyValuePair<string, object>("ProphetsWay.ExternalScopes", payload.ExternalScopes)
			});
			var eventId = entry.EventId ?? default(EventId);
			Func<MicrosoftLogTransport, Exception, string> transportFormatter =
				(transport, ignoredException) => entry.FormattedMessage;
			var transfer = new MicrosoftOutboundTransfer(dispatch, payload, eventState,
				microsoftLevel, eventId, entry.Exception, transportFormatter);
			var handles = new List<IDisposable>();
			Exception failure = null;

			try
			{
				var replayedNativeFrames = new List<LogScopeFrame>();
				foreach (var nativeContext in payload.NativeContexts)
				{
					foreach (var frame in nativeContext.Scopes)
					{
						if (ContainsReference(replayedNativeFrames, frame))
							continue;
						replayedNativeFrames.Add(frame);
						var scopeState = new MicrosoftLogTransport(new[]
						{
							new KeyValuePair<string, object>("ProphetsWay.NativeScope", frame)
						});
						transfer.AddScopeState(scopeState);
						var handle = selectedLogger.BeginScope(scopeState);
						if (handle != null)
							handles.Add(handle);
					}
				}

				foreach (var externalScope in payload.ExternalScopes)
				{
					var scopeState = new MicrosoftLogTransport(new[]
					{
						new KeyValuePair<string, object>("ProphetsWay.ExternalScope", externalScope)
					});
					transfer.AddScopeState(scopeState);
					var handle = selectedLogger.BeginScope(scopeState);
					if (handle != null)
						handles.Add(handle);
				}

				var previousTransfer = _currentMicrosoftTransfer;
				_currentMicrosoftTransfer = transfer;
				try
				{
					selectedLogger.Log(microsoftLevel, eventId, eventState, entry.Exception, transportFormatter);
				}
				finally
				{
					_currentMicrosoftTransfer = previousTransfer;
				}
			}
			catch (Exception ex)
			{
				failure = ex;
			}
			finally
			{
				for (var index = handles.Count - 1; index >= 0; index--)
				{
					try
					{
						handles[index].Dispose();
					}
					catch (Exception ex)
					{
						if (failure == null)
							failure = ex;
					}
				}
			}

			if (failure != null)
				throw failure;
		}

		private static MicrosoftBridgePayload CreateRawBridgePayload<TState>(LogContext context,
			LogLevels level, TState state, string rawMessage, string formattedMessage, Exception exception)
		{
			var properties = (object)state as IEnumerable<KeyValuePair<string, object>>;
			ReadOnlyCollection<KeyValuePair<string, object>> capturedProperties = properties == null
				? LogContext.CopyMembership(new KeyValuePair<string, object>[0])
				: LogContext.CopyMembership(properties);
			var entry = new LogBridgeEntry<TState>(state, null, null, null, level,
				rawMessage, formattedMessage, exception, capturedProperties,
				new LogBridgeScope[0], null, context);
			return CreateBridgePayload(entry, new[] { context }, new LogBridgeScope[0],
				context.Labels.EffectiveLabels);
		}

		private static MicrosoftBridgePayload CreateBridgePayload(LogBridgeEntry entry,
			IEnumerable<LogContext> nativeContexts, IEnumerable<LogBridgeScope> externalScopes,
			IEnumerable<SensitivityLabel> effectiveLabels)
		{
			return new MicrosoftBridgePayload(entry, nativeContexts, externalScopes, effectiveLabels);
		}

		private static MicrosoftBridgePayload ComposeControlledPayload(MicrosoftBridgePayload source,
			LogContext currentContext, IList<LogBridgeScope> addedExternalScopes,
			IEnumerable<SensitivityLabel> effectiveLabels)
		{
			var contexts = new List<LogContext>(source.NativeContexts);
			var currentAlreadyCarried = false;
			for (var index = 0; index < contexts.Count; index++)
				if (ReferenceEquals(contexts[index], currentContext))
					currentAlreadyCarried = true;

			if (!currentAlreadyCarried)
				contexts.Add(currentContext);

			var externalScopes = new List<LogBridgeScope>(source.ExternalScopes);
			externalScopes.AddRange(addedExternalScopes);
			return CreateBridgePayload(source.Entry, contexts, externalScopes, effectiveLabels);
		}

		private static List<SensitivityLabel> ComposeEffectiveLabels(MicrosoftBridgePayload source,
			LogContext currentContext, IEnumerable<LogBridgeScope> addedExternalScopes)
		{
			var result = new List<SensitivityLabel>();
			var seen = new HashSet<SensitivityLabel>();
			AddLabels(source.EffectiveLabels, seen, result);
			AddLabels(currentContext.Labels.EffectiveLabels, seen, result);
			foreach (var scope in addedExternalScopes)
				AddLabels(scope.Annotations, seen, result);
			return result;
		}

		private static bool ContainsReference<T>(IList<T> values, T value) where T : class
		{
			for (var index = 0; index < values.Count; index++)
				if (ReferenceEquals(values[index], value))
					return true;
			return false;
		}

		private static LogLevels HighestPriorityBit(LogLevels level)
		{
			if ((level & LogLevels.Critical) != 0) return LogLevels.Critical;
			if ((level & LogLevels.ErrorOnly) != 0) return LogLevels.ErrorOnly;
			if ((level & LogLevels.WarningOnly) != 0) return LogLevels.WarningOnly;
			if ((level & LogLevels.InformationOnly) != 0) return LogLevels.InformationOnly;
			if ((level & LogLevels.DebugOnly) != 0) return LogLevels.DebugOnly;
			if ((level & LogLevels.TraceOnly) != 0) return LogLevels.TraceOnly;
			throw new ArgumentOutOfRangeException(nameof(level));
		}

	}
}
