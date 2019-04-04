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

        public override void WriteLogEntry(T metadata, LogLevels level, string message = null, Exception thrownException = null)
        {
            var evt = new LoggerEventArgs(message, level, thrownException, metadata);
            LoggingEvent?.Invoke(this, evt);
        }

        /// <summary>
        /// The event you must subscribe to, to receive the relevant log events.
        /// </summary>
        public EventHandler<LoggerEventArgs> LoggingEvent;

        public class LoggerEventArgs : EventArgs
        {
            public LoggerEventArgs(string message, LogLevels level, Exception ex, T metadata)
            {
                Message = message;
                LogLevel = level;
                Metadata = metadata;
                Timestamp = DateTime.Now;
                Exception = ex;
            }

            public string Message { get; }
            public T Metadata { get; }
            public Exception Exception { get; }
            public LogLevels LogLevel { get; }
            public DateTime Timestamp { get; }
        }
    }
}
