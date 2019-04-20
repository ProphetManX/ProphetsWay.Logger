using System;

namespace ProphetsWay.Utilities
{
    /// <summary>
    /// Core functionality that all Logging destinations will need to use, 
    /// manages log level comparison in a single location,
    /// includes a LoggerLock for use if needed in any inheriting destination.
    /// </summary>
    public abstract class LoggingDestinationCore : IDestination
    {
        public LoggingDestinationCore(LogLevels reportingLevel)
        {
            _reportingLevel = reportingLevel;
        }

        /// <summary>
        /// A lock object for use in making threadsafe destinations
        /// </summary>
        protected readonly object LoggerLock = new object();

        private readonly LogLevels _reportingLevel;

        /// <summary>
		/// This method is meant to massage the statement to be logged, before it goes to the WriteLogEntry method.
		/// It will extract the details from an exception and add them to the end of the message.
		/// It is Virtual and can be overridden if you wish to customize how you handle writing the raw Log method call.
		/// This method calls WriteLogEntry.
		/// </summary>
		protected virtual string MassageLogStatement(LogLevels level, string message = null, Exception ex = null)
        {
            string exMessage, exStackTrace, massagedMessage = null;

            switch (level)
            {
                case LogLevels.Error:
                    ExceptionDetailer(ex, out exMessage, out exStackTrace);
                    massagedMessage = $"{message}{Environment.NewLine}{exMessage}{Environment.NewLine}{exStackTrace}";
                    break;

                case LogLevels.Warning:
                    if (ex != null)
                    {
                        ExceptionDetailer(ex, out exMessage, out exStackTrace);
                        massagedMessage = $"{message}{Environment.NewLine}{exMessage}{Environment.NewLine}{exStackTrace}";
                    }
                    break;

                default:
                    massagedMessage = message;
                    break;
            }

            return massagedMessage;
        }

        /// <summary>
        /// Recursive function to peer deep into an Exception and its Inner Exceptions to capture the messages and stack traces within.
        /// </summary>
        private static void ExceptionDetailer(Exception ex, out string message, out string stack)
        {
            var imessage = string.Empty;
            var istack = string.Empty;

            if (ex.InnerException != null)
                ExceptionDetailer(ex.InnerException, out imessage, out istack);

            message = string.IsNullOrEmpty(imessage)
                ? ex.Message
                : string.Format("{0}{1}{1}Inner Exception Message:{1}{2}", ex.Message, Environment.NewLine, imessage);

            stack = string.IsNullOrEmpty(istack)
                ? ex.StackTrace
                : string.Format("{0}{1}{1}Inner Exception Stack Trace:{1}{2}", ex.StackTrace, Environment.NewLine, istack);
        }


        /// <summary>
        /// Returns true if the messageLevel matches the level specified when the Destination was created.
        /// </summary>
        public bool ValidateMessageLevel(LogLevels messageLevel)
        {
            //if the level of the message being passed is lower than the ReportingLevel set on the Destination, then return and don't log the message
            return (messageLevel & _reportingLevel) == messageLevel;
        }
    }
}
