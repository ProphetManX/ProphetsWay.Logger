using ProphetsWay.Utilities.Generics;
using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>
	/// A generic destination meant for use in situations that require events to be invoked (ie: UI applications).
	/// The generic version allows for passing a custom object thru to your logging destination to be
	/// handled specially.  (ie: database logging parameters)
	/// </summary>
	public class GenericEventDestination<T> : BaseLoggingDestination<T>
	{
		/// <summary>
		/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
		/// </summary>
		public GenericEventDestination(LogLevels reportingLevel) : base(reportingLevel) { }

		/// <summary>
		/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
		/// </summary>
		public GenericEventDestination(string strReportingLevel) : base(strReportingLevel) { }

		/// <summary>
		/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
		/// </summary>
		public GenericEventDestination(int intReportingLevel) : base(intReportingLevel) { }

		/// <inheritdoc />
		/// <remarks>B11/B12: the supplied guard has completed. Preserve existing massage and output
		/// semantics. Event delivery massages once, constructs one argument and invokes LoggingEvent
		/// once if non-null, with this destination as sender. Normal multicast behavior remains: a
		/// handler throw stops that invocation and is an output failure, not individually contained
		/// DispatchFailed reporting. Context is the selected completed capture.
		/// No implicit scope/property serialization, new timestamp capture or framing rule is added.</remarks>
		protected override void LogCore(LogContext context, LogLevels level, T metadata, string message, Exception ex)
		{
			var evt = new LoggerEventArgs(message, level, ex, metadata, MassageLogStatement(level, message, ex), context);
			LoggingEvent?.Invoke(this, evt);
		}

		/// <summary>
		/// The event you must subscribe to, to receive the relevant log events.
		/// </summary>
		public EventHandler<LoggerEventArgs> LoggingEvent;

		public class LoggerEventArgs : EventArgs
		{
			public LoggerEventArgs(string message, LogLevels level, Exception ex, T metadata, string massagedMessage)
			{
				RawMessage = message;
				LogLevel = level;
				Metadata = metadata;
				Timestamp = DateTime.Now;
				Exception = ex;
				Message = massagedMessage;
			}

			/// <summary>Constructs the existing event payload with its selected complete native context.</summary>
			/// <param name="message">Same unchanged message argument as the existing public constructor.</param>
			/// <param name="level">Same full mask as the existing public constructor.</param>
			/// <param name="ex">Same optional original exception reference.</param>
			/// <param name="context">Required completed capture for this delivery.</param>
			/// <exception cref="ArgumentNullException">context is null; ParamName is "context".</exception>
			/// <remarks>B11: preserve existing field assignments and local timestamp behavior; retain
			/// context without recapture, policy evaluation, output or mutation of supplied values.</remarks>
			/// <param name="metadata">Original T, including null or default(T).</param>
			/// <param name="massagedMessage">The already massaged message.</param>
			internal LoggerEventArgs(string message, LogLevels level, Exception ex, T metadata, string massagedMessage, LogContext context)
				: this(message, level, ex, metadata, massagedMessage)
			{
				if (context == null)
					throw new ArgumentNullException(nameof(context));

				Context = context;
			}

			/// <summary>Gets the full native context supplied with this delivered entry.</summary>
			/// <value>The completed LogContext for library delivery; null when the existing public constructor was used.</value>
			/// <remarks>B11: immutable captured membership with original nested property references,
			/// not a deep freeze, permission, scope handle or diagnostic-safe graph. Existing event
			/// fields retain their meaning. Reading or retaining this value performs no new capture.</remarks>
			public LogContext Context { get; }

			public string Message { get; }
			public T Metadata { get; }
			public Exception Exception { get; }
			public LogLevels LogLevel { get; }
			public DateTime Timestamp { get; }
			public string RawMessage { get; }
		}
	}
}
