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
		public EventDestination(LogLevels reportingLevel) : base(reportingLevel)
		{

		}

		protected override void WriteLogEntry(string message, LogLevels level, string raw, Exception ex)
		{
			var evt = new LoggerEventArgs(message, level, raw, ex);
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
