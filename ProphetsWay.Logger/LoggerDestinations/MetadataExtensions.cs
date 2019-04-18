using ProphetsWay.Utilities.Generics;
using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
    public static class MetadataExtensions
    {
        public static void Log<T>(this T metadata, LogLevels level, string message = null, Exception ex = null) where T : ILoggerMetadata
        {
            Logger.Log(level, metadata, message, ex);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Debug'
        /// </summary>
        /// <param name="message">The message you wish to convey in the log entry.</param>
        public static void Debug<T>(this T metadata, string message = null) where T : ILoggerMetadata
        {
            Logger.Debug(message, metadata);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Information'
        /// </summary>
        /// <param name="message">The message you wish to convey in the log entry.</param>
        public static void Info<T>(this T metadata, string message = null) where T : ILoggerMetadata
        {
            Logger.Info(message, metadata);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Security'
        /// </summary>
        /// <param name="message">The message you wish to convey in the log entry.</param>
        public static void Security<T>(this T metadata, string message = null) where T : ILoggerMetadata
        {
            Logger.Security(message, metadata);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Warning'
        /// </summary>
        /// <param name="message">The message you wish to convey in the log entry.</param>
        /// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
        public static void Warn<T>(this T metadata, string message = null, Exception ex = null) where T : ILoggerMetadata
        {
            Logger.Warn(message, metadata, ex);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Error'
        /// </summary>
        /// <param name="ex">Required, pass the exception you want to convey in the log entry.</param>
        /// <param name="message">Optional, if no message is passed, the Exception message will still be written to the log.  
        /// If you want to add more context to the error, you can enter it in the message.</param>
        public static void Error<T>(this T metadata, Exception ex = null, string message = null) where T : ILoggerMetadata
        {
            Logger.Error(ex, metadata, message);
        }
    }
}