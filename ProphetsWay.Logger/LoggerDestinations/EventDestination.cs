using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>
	/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
	/// </summary>
	public class EventDestination : BaseLoggingDestination
	{
		/// <summary>
		/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
		/// </summary>
		public EventDestination(LogLevels reportingLevel) : base(reportingLevel) { }

		/// <summary>
		/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
		/// </summary>
		public EventDestination(string strReportingLevel) : base(strReportingLevel) { }

		/// <summary>
		/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
		/// </summary>
		public EventDestination(int intReportingLevel) : base(intReportingLevel) { }

		/// <inheritdoc />
		/// <remarks>B11/B12: the supplied guard has completed. Preserve existing massage and output
		/// semantics. Event delivery massages once, constructs one argument and invokes LoggingEvent
		/// once if non-null, with this destination as sender. Normal multicast behavior remains: a
		/// handler throw stops that invocation and is an output failure, not individually contained
		/// DispatchFailed reporting. Context is the selected completed capture.
		/// No implicit scope/property serialization, new timestamp capture or framing rule is added.</remarks>
		protected override void LogCore(LogContext context, LogLevels level, string message, Exception ex)
		{
			var evt = new LoggerEventArgs(MassageLogStatement(level, message,ex), level, message, ex, context);
			LoggingEvent?.Invoke(this, evt);
		}

		/// <summary>
		/// The event you must subscribe to, to receive the relevant log events.
		/// </summary>
		public EventHandler<LoggerEventArgs> LoggingEvent;

		public class LoggerEventArgs : EventArgs
		{
			public LoggerEventArgs(string message, LogLevels level, string raw, Exception ex)
			{
				Message = message;
				LogLevel = level;
				Timestamp = DateTime.Now;
				RawMessage = raw;
				Exception = ex;
			}

			/// <summary>Constructs the existing event payload with its selected complete native context.</summary>
			/// <param name="message">Same unchanged message argument as the existing public constructor.</param>
			/// <param name="level">Same full mask as the existing public constructor.</param>
			/// <param name="ex">Same optional original exception reference.</param>
			/// <param name="context">Required completed capture for this delivery.</param>
			/// <exception cref="ArgumentNullException">context is null; ParamName is "context".</exception>
			/// <remarks>B11: preserve existing field assignments and local timestamp behavior; retain
			/// context without recapture, policy evaluation, output or mutation of supplied values.</remarks>
			/// <param name="raw">Original optional raw text.</param>
			internal LoggerEventArgs(string message, LogLevels level, string raw, Exception ex, LogContext context)
				: this(message, level, raw, ex)
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
			public string RawMessage { get; }
			public Exception Exception { get; }
			public LogLevels LogLevel { get; }
			public DateTime Timestamp { get; }
		}
	}
}
