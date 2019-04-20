using System;

namespace ProphetsWay.Utilities.Generics
{
    public abstract class BaseLoggingDestination<T> : LoggingDestinationCore, ILoggingDestination<T>
    {
        public BaseLoggingDestination(LogLevels reportingLevel) : base(reportingLevel) { }

        /// <summary>
		/// This is the required method from the ILoggingDestinations interface, this is the entry point for the destination from the Logger static class.
		/// </summary>
        public void Log(LogLevels level, T metadata, string message = null, Exception ex = null)
        {
            if (ValidateMessageLevel(level))
                WriteLogEntry(metadata, level, message, ex);
        }

        /// <summary>
		/// This is the method you must implement in your custom LoggingDestination.  
		/// It is where you will actually write the given message wherever you want.
		/// This is called from inside the Destination whenever there is a valid message to write to the log.
		/// </summary>
		/// <param name="metadata">The metadata object you are passing for reference to the log.</param>
		/// <param name="level">The LogLevel of the message being passed.</param>
		/// <param name="message">The message text that was logged.</param,>
		/// <param name="thrownException">The raw original exception that was logged.</param>
        public abstract void WriteLogEntry(T metadata, LogLevels level, string message = null, Exception thrownException = null);
    }
}
