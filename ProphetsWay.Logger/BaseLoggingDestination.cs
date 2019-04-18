using System;

namespace ProphetsWay.Utilities
{

	/// <summary>
	/// A Base class for creating new ILoggingDestinations, it is thread safe and will check the level of a message before logging.
	/// </summary>
	public abstract class BaseLoggingDestination : LoggingDestinationCore, ILoggingDestination
	{
        protected BaseLoggingDestination(LogLevels reportingLevel) : base(reportingLevel) { }

		/// <summary>
		/// This is the required method from the ILoggingDestinations interface, this is the entry point for the destination from the Logger static class.
		/// </summary>
		public void Log(LogLevels level, string message = null, Exception ex = null)
		{
			if(IgnoreLog(level))
				return;

			var massagedMessage = MassageLogStatement(level, message, ex);
            WriteLogEntry(massagedMessage, level, message, ex);
		}

		/// <summary>
		/// This is the method you must implement in your custom LoggingDestination.  
		/// It is where you will actually write the given message wherever you want.
		/// This is called from inside the Destination whenever there is a valid message to write to the log.
		/// </summary>
		/// <param name="masssagedMessage">The massaged statement text being logged.</param>
		/// <param name="level">The LogLevel of the message being passed.</param>
		/// <param name="rawMessage">The raw original message text that was logged.</param,>
		/// <param name="thrownException">The raw original exception that was logged.</param>
		protected abstract void WriteLogEntry(string masssagedMessage, LogLevels level, string rawMessage, Exception thrownException);
	}
}