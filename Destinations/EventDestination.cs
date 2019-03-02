using System;

namespace ProphetsWay.Logger.Destinations
{
	public class EventDestination : BaseLoggingDestination
	{
		public EventDestination(LogLevels reportingLevel) : base(reportingLevel)
		{

		}

		protected override void WriteLogEntry(string message, LogLevels level)
		{
			LoggingEvent?.Invoke(this, new LoggerEventArgs(message, level));
		}

		public EventHandler<LoggerEventArgs> LoggingEvent;

		public class LoggerEventArgs : EventArgs
		{
			public LoggerEventArgs(string message, LogLevels level)
			{
				Message = message;
				LogLevel = level;
			}

			public string Message { get; }
			public LogLevels LogLevel { get; }
		}
	}
}
