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

		/// <summary>Raises a log callback only when every message bit is accepted.</summary>
		/// <param name="level">A nonzero combination of known severity bits, from 1 through 63.</param>
		/// <param name="message">Optional raw context, including null, empty or whitespace.</param>
		/// <param name="ex">Optional exception; null is valid at every accepted mask.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is zero, negative or contains an unknown bit.</exception>
		/// <remarks>Validity and eligibility precede massage and callbacks, even without subscribers. A valid mismatch returns without recipient work. Accepted callbacks retain the full mask, original raw context and exception reference, with massaged text in Message.</remarks>
		public override void Log(LogLevels level, string message = null, Exception ex = null)
		{
			if (level == 0 || (level & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(level));
			if (!ValidateMessageLevel(level))
				return;

			var evt = new LoggerEventArgs(MassageLogStatement(level, message,ex), level, message, ex);
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

			public string Message { get; }
			public string RawMessage { get; }
			public Exception Exception { get; }
			public LogLevels LogLevel { get; }
			public DateTime Timestamp { get; }
		}
	}
}
