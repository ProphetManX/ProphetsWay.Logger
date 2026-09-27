using System;
using ProphetsWay.Utilities.Generics;
using System.Collections.Generic;

namespace ProphetsWay.Utilities
{
	public static partial class Logger
	{
		private static readonly IDictionary<Type, List<TypedRegistration>> Destinations = new Dictionary<Type, List<TypedRegistration>>();

		/// <summary>Registers a borrowed recipient on exactly T with no added restrictions.</summary>
		/// <typeparam name="T">Unconstrained declared metadata route type.</typeparam>
		/// <param name="newDest">Required non-null destination compatible with exactly T.</param>
		/// <exception cref="ArgumentNullException">newDest is null; ParamName is "newDest".</exception>
		/// <exception cref="ArgumentException">The same reference is registered on this exact route;
		/// ParamName is "newDest".</exception>
		/// <remarks>A21/A23-A25/A29: equivalent settings are enabled true, mask Trace (63),
		/// NoFilter with empty labels. Preserve recipient severity behavior. Append by reference
		/// identity, never user equality/hashing; disabled duplicates also reject unchanged.
		/// Ordinary and other T routes remain independent, including compatible reuse of one
		/// object. Publication returns without callbacks, disposal or draining old captures.</remarks>
		public static void AddDestination<T>(ILoggingDestination<T> newDest)
		{
			AddDestination(newDest, DefaultSettings);
		}

		/// <summary>Registers a borrowed exact-T recipient with complete immutable settings.</summary>
		/// <typeparam name="T">Unconstrained declared metadata route type.</typeparam>
		/// <param name="newDest">Required non-null destination compatible with exactly T.</param>
		/// <param name="settings">Required immutable enablement, mask and policy.</param>
		/// <exception cref="ArgumentNullException">newDest or settings is null; ParamName names that argument.</exception>
		/// <exception cref="ArgumentException">The same reference is registered on this exact route;
		/// ParamName is "newDest".</exception>
		/// <remarks>A21/A23-A26/A29/A38: append membership/settings atomically; rejection
		/// changes nothing and competing-invalid priority is unspecified. Disabled entries
		/// retain position and duplicate identity. Settings sharing never couples later route
		/// replacement. No destination getter, equality, callback, disposal or old-call drain
		/// occurs. Runtime metadata type and assignability never select this registration.</remarks>
		public static void AddDestination<T>(ILoggingDestination<T> newDest, DestinationRegistrationSettings settings)
		{
			if (newDest == null)
				throw new ArgumentNullException(nameof(newDest));
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			lock (DestinationLock)
			{
				if (!Destinations.TryGetValue(typeof(T), out var destinations))
				{
					destinations = new List<TypedRegistration>();
					Destinations.Add(typeof(T), destinations);
				}

				if (destinations.Exists(registration => ReferenceEquals(registration.Destination, newDest)))
					throw new ArgumentException("The destination is already registered on this route.", nameof(newDest));

				destinations.Add(new TypedRegistration(newDest, settings));
			}
		}

		/// <summary>Replaces all settings of an existing exact-T registration atomically.</summary>
		/// <typeparam name="T">Unconstrained declared metadata route type.</typeparam>
		/// <param name="destination">Required exact registered destination reference.</param>
		/// <param name="settings">Required immutable replacement settings.</param>
		/// <exception cref="ArgumentNullException">destination or settings is null; ParamName names that argument.</exception>
		/// <exception cref="ArgumentException">destination is absent from exactly this route;
		/// ParamName is "destination".</exception>
		/// <remarks>A21/A23/A27/A29/A34: no upsert. Check presence and publish together;
		/// preserve position, recipient reference and every other route. Failure changes
		/// nothing; competing-invalid priority is unspecified. Repeating equivalent values
		/// preserves effective settings. Old calls retain captured settings; later captures
		/// see the replacement after return. No recipient code, disposal or draining occurs.
		/// Concurrent remove/re-add is ordered by reference-based publication, not a generation
		/// token or implicit transfer. Reused settings objects do not connect distinct routes.</remarks>
		public static void SetDestinationSettings<T>(ILoggingDestination<T> destination, DestinationRegistrationSettings settings)
		{
			if (destination == null)
				throw new ArgumentNullException(nameof(destination));
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			lock (DestinationLock)
			{
				if (!Destinations.TryGetValue(typeof(T), out var destinations))
					throw new ArgumentException("The destination is not registered on this route.", nameof(destination));

				var index = destinations.FindIndex(registration => ReferenceEquals(registration.Destination, destination));
				if (index < 0)
					throw new ArgumentException("The destination is not registered on this route.", nameof(destination));

				destinations[index] = new TypedRegistration(destination, settings);
			}
		}

		/// <summary>Removes a borrowed recipient and its settings from exactly T.</summary>
		/// <typeparam name="T">Unconstrained declared metadata route type.</typeparam>
		/// <param name="destToRemove">Exact reference; null or absent is a no-op.</param>
		/// <remarks>A17/A21/A24/A28/A34: preserve survivor order and other routes; never
		/// invoke/dispose a recipient. Return after publication, not old-call completion.
		/// Repeated removal is a no-op; host quiescence precedes borrowed-resource disposal.</remarks>
		public static void RemoveDestination<T>(ILoggingDestination<T> destToRemove)
		{
			lock (DestinationLock)
			{
				if (Destinations.TryGetValue(typeof(T), out var destinations))
					destinations.RemoveAll(registration => ReferenceEquals(registration.Destination, destToRemove));
			}
		}

		/// <summary>Clears explicit registrations and settings for exactly T.</summary>
		/// <typeparam name="T">Unconstrained declared metadata route type.</typeparam>
		/// <remarks>A17/A21/A28/A29/A34: empty/unconfigured is a no-op. Leave ordinary,
		/// other T routes and scopes untouched. Publish without callbacks/disposal/draining;
		/// old captures may finish. Later fallback need is route-local and does not reset
		/// established or remembered-failed file/session state.</remarks>
		public static void ClearDestinations<T>()
		{
			lock (DestinationLock)
			{
				if (Destinations.TryGetValue(typeof(T), out var destinations))
					destinations.Clear();
			}
		}

		/// <summary>Logs an exact-T raw entry with separate explicit optional annotations.</summary>
		/// <typeparam name="T">Unconstrained declared metadata route type, not runtime subtype.</typeparam>
		/// <param name="annotations">Null for no entry attachment; otherwise immutable occurrences.</param>
		/// <param name="level">A nonzero combination of known severity bits, from 1 through 63.</param>
		/// <param name="metadata">Original T value/reference, including null or default(T).</param>
		/// <param name="message">Original optional text; null, empty and whitespace are permitted.</param>
		/// <param name="ex">Original optional exception; null remains absent.</param>
		/// <exception cref="ArgumentOutOfRangeException">level is zero, negative or has unknown bits;
		/// ParamName is "level".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A01/A18/A19/A21-A22/A31-A40: capture the same ambient scope model used
		/// by ordinary logging. T is never a label source, even when T is LogAnnotations,
		/// SensitivityLabel or a destination contract. No marker constraint or runtime routing.
		/// Use Logger's complete-context, captured-settings, whole-entry withholding and
		/// independent synchronous attempts. No raw values are fabricated or mutated. A normal
		/// return does not prove delivery and follows the distinct capture/check failure rule.
		/// Explicit generic invocation selects T when overload inference is not the intent.</remarks>
		public static void LogAnnotated<T>(LogAnnotations annotations, LogLevels level, T metadata, string message = null, Exception ex = null)
		{
			if (level == 0 || (level & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(level));

			DispatchTyped(annotations, level, metadata, message, ex);
		}

		private static void Log<T>(LogLevels level, T metadata, string message, Exception ex = null)
		{
			DispatchTyped(null, level, metadata, message, ex);
		}

		private static void DispatchTyped<T>(LogAnnotations annotations, LogLevels level, T metadata, string message, Exception ex)
		{
			TypedRegistration[] destinations;
			LogContext context;
			DestinationLabelPolicy[] intrinsicPolicies;
			try
			{
				lock (DestinationLock)
				{
					destinations = Destinations.TryGetValue(typeof(T), out var route)
						? route.ToArray()
						: Array.Empty<TypedRegistration>();
				}
				context = LogScopeHandle.Capture(annotations);
				intrinsicPolicies = new DestinationLabelPolicy[destinations.Length];
				for (var index = 0; index < destinations.Length; index++)
				{
					var supplied = destinations[index].Destination as LoggingDestinationCore;
					if (supplied != null)
						intrinsicPolicies[index] = supplied.LabelPolicy;
				}
			}
			catch (Exception)
			{
				CompleteDispatchFailure(null, 0, 1, false);
				return;
			}

			if (!Array.Exists(destinations, registration => registration.Settings.Enabled))
			{
				DispatchOrdinary(annotations, level, message, ex);
				return;
			}

			List<LogFailureDescriptor> failures = null;
			var overflowCount = 0;
			var mustThrow = false;
			for (var index = 0; index < destinations.Length; index++)
			{
				var registration = destinations[index];
				var settings = registration.Settings;
				if (!settings.Enabled || (settings.ReportingLevel & level) != level)
					continue;

				try
				{
					if (settings.LabelPolicy.Mode != LabelFilterMode.NoFilter && !settings.LabelPolicy.Allows(context.Labels.EffectiveLabels))
						continue;

					var intrinsicPolicy = intrinsicPolicies[index];
					if (intrinsicPolicy != null && intrinsicPolicy.Mode != LabelFilterMode.NoFilter && !intrinsicPolicy.Allows(context.Labels.EffectiveLabels))
						continue;
				}
				catch (Exception)
				{
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.LabelCheck);
					continue;
				}

				var destination = (ILoggingDestination<T>)registration.Destination;
				try
				{
					var supplied = destination as LoggingDestinationCore;
					if (supplied != null && !supplied.ValidateMessageLevel(level))
						continue;
					if (!destination.ValidateMessageLevel(level))
						continue;
				}
				catch (Exception)
				{
					mustThrow = true;
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.Eligibility);
					continue;
				}

				try
				{
					var supplied = destination as Generics.BaseLoggingDestination<T>;
					var contextual = destination as Generics.IContextLoggingDestination<T>;
					if (supplied != null)
						supplied.LogCaptured(context, level, metadata, message, ex);
					else if (contextual != null)
						contextual.LogWithContext(context, level, metadata, message, ex);
					else
						destination.Log(level, metadata, message, ex);
				}
				catch (Exception)
				{
					mustThrow = true;
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.Output);
				}
			}

			CompleteDispatchFailure(failures, overflowCount, 0, mustThrow);
		}

		private sealed class TypedRegistration
		{
			internal readonly IDestination Destination;
			internal readonly DestinationRegistrationSettings Settings;

			internal TypedRegistration(IDestination destination, DestinationRegistrationSettings settings)
			{
				Destination = destination;
				Settings = settings;
			}
		}

		/// <summary>Logs typed context with the exact TraceOnly bit.</summary>
		/// <typeparam name="T">Unconstrained declared route type.</typeparam>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <param name="metadata">Original T value/reference, including null or default(T).</param>
		/// <exception cref="ArgumentNullException">message is null before dispatch; ParamName is "message".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A19-A22/A31-A40: capture scopes with absent entry annotations; preserve
		/// unconstrained exact-T routing and Logger's capture/withholding/failure contract.</remarks>
		public static void Trace<T>(string message, T metadata)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.TraceOnly, metadata, message);
		}

		/// <summary>Logs typed context with the exact DebugOnly bit.</summary>
		/// <typeparam name="T">Unconstrained declared route type.</typeparam>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <param name="metadata">Original T value/reference, including null or default(T).</param>
		/// <exception cref="ArgumentNullException">message is null before dispatch; ParamName is "message".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A19-A22/A31-A40: capture scopes with absent entry annotations; preserve
		/// unconstrained exact-T routing and Logger's capture/withholding/failure contract.</remarks>
		public static void Debug<T>(string message, T metadata)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.DebugOnly, metadata, message);
		}

		/// <summary>Logs typed context with the exact InformationOnly bit.</summary>
		/// <typeparam name="T">Unconstrained declared route type.</typeparam>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <param name="metadata">Original T value/reference, including null or default(T).</param>
		/// <exception cref="ArgumentNullException">message is null before dispatch; ParamName is "message".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A19-A22/A31-A40: capture scopes with absent entry annotations; preserve
		/// unconstrained exact-T routing and Logger's capture/withholding/failure contract.</remarks>
		public static void Info<T>(string message, T metadata)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.InformationOnly, metadata, message);
		}

		/// <summary>Logs typed context with the exact WarningOnly bit.</summary>
		/// <typeparam name="T">Unconstrained declared route type.</typeparam>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <param name="metadata">Original T value/reference, including null or default(T).</param>
		/// <param name="ex">Optional original exception; null is valid.</param>
		/// <exception cref="ArgumentNullException">message is null before dispatch; ParamName is "message".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A19-A22/A31-A40: capture scopes with absent entry annotations. Preserve
		/// references and Logger's exact-T capture, withholding, attempts and failure rules.</remarks>
		public static void Warn<T>(string message, T metadata, Exception ex = null)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.WarningOnly, metadata, message, ex);
		}

		/// <summary>Logs a typed exception with the exact ErrorOnly bit.</summary>
		/// <typeparam name="T">Unconstrained declared route type.</typeparam>
		/// <param name="ex">Required original non-null exception.</param>
		/// <param name="metadata">Original T value/reference, including null or default(T).</param>
		/// <param name="message">Optional original text, including null, empty or whitespace.</param>
		/// <exception cref="ArgumentNullException">ex is null before dispatch; ParamName is "ex".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A19-A22/A31-A40: absent text stays absent; capture scopes with no entry
		/// attachment and preserve Logger's exact-T capture/withholding/failure contract.</remarks>
		public static void Error<T>(Exception ex, T metadata, string message = null)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));

			Log(LogLevels.ErrorOnly, metadata, message, ex);
		}

		/// <summary>Logs a typed exception and context with the exact Critical bit.</summary>
		/// <typeparam name="T">Unconstrained declared route type.</typeparam>
		/// <param name="ex">Required original non-null exception.</param>
		/// <param name="metadata">Original T value/reference, including null or default(T).</param>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">ex or message is null before dispatch;
		/// ParamName names that argument, without a competing-error priority.</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A19-A22/A31-A40: capture scopes with absent entry annotations; preserve
		/// unconstrained exact-T routing and Logger's capture/withholding/failure contract.</remarks>
		public static void Critical<T>(Exception ex, T metadata, string message)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.Critical, metadata, message, ex);
		}
	}
}