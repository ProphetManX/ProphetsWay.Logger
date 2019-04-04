using System;
using ProphetsWay.Utilities.Generics;
using System.Collections.Generic;

namespace ProphetsWay.Utilities
{
    public static partial class Logger
    {
        private static readonly IDictionary<Type, IList<ILoggingDestination>> Destinations = new Dictionary<Type, IList<ILoggingDestination>>();

        /// <summary>
        /// Will add a new LoggingDestination to the pool of targets.  
        /// </summary>
        /// <param name="newDest">Either an existing or a custom Destination that implements the ILoggingDestination interface.</param>
        public static void AddDestination<T>(ILoggingDestination<T> newDest)
        {
            if (!Destinations.ContainsKey(typeof(T)))
                Destinations.Add(typeof(T), new List<ILoggingDestination>());

            Destinations[typeof(T)].Add(newDest);
        }

        /// <summary>
        /// If you retain a reference to your LoggingDestination, you can remove it from the pool of targets.
        /// </summary>
        /// <param name="destToRemove">Either an existing or a custom Destination that implements the ILoggingDestination interface; must have already been added to the pool via "AddDestination".</param>
        public static void RemoveDestination<T>(ILoggingDestination<T> destToRemove)
        {
            if (Destinations.ContainsKey(typeof(T)))
                Destinations[typeof(T)].Remove(destToRemove);
        }

        /// <summary>
        /// Resets the pool of targets, removes any/all Destinations that have been added.
        /// </summary>
        public static void ClearDestinations<T>()
        {
            if (Destinations.ContainsKey(typeof(T)))
                Destinations[typeof(T)].Clear();
        }

        /// <summary>
        /// Use this method if you want to log with a grouped set of LogLevels
        /// Ex:  Warning | Security | Information
        /// Will write to Destinations with any of those three LogLevels enabled.
        /// </summary>
        /// <param name="level">The severity level of the log statement.</param>
        /// <param name="message">The message you wish to convey in the log entry.</param>
        /// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
        public static void Log<T>(LogLevels level, T metadata, string message, Exception ex = null)
        {
            if (!Destinations.ContainsKey(typeof(T)) || Destinations[typeof(T)].Count == 0)
            {
                Utilities.Logger.Log(level, message, ex);
                return;
            }

            foreach (ILoggingDestination<T> dest in Destinations[typeof(T)])
                dest.Log(level, metadata, message, ex);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Debug'
        /// </summary>
        /// <param name="message">The message you wish to convey in the log entry.</param>
        public static void Debug<T>(string message, T metadata)
        {
            Log(LogLevels.Debug, metadata, message);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Information'
        /// </summary>
        /// <param name="message">The message you wish to convey in the log entry.</param>
        public static void Info<T>(string message, T metadata)
        {
            Log(LogLevels.Information, metadata, message);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Security'
        /// </summary>
        /// <param name="message">The message you wish to convey in the log entry.</param>
        public static void Security<T>(string message, T metadata)
        {
            Log(LogLevels.Security, metadata, message);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Warning'
        /// </summary>
        /// <param name="message">The message you wish to convey in the log entry.</param>
        /// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
        public static void Warn<T>(string message, T metadata, Exception ex = null)
        {
            Log(LogLevels.Warning, metadata, message, ex);
        }

        /// <summary>
        /// Shortcut method to Log a message with a LogLevel of 'Error'
        /// </summary>
        /// <param name="ex">Required, pass the exception you want to convey in the log entry.</param>
        /// <param name="message">Optional, if no message is passed, the Exception message will still be written to the log.  
        /// If you want to add more context to the error, you can enter it in the message.</param>
        public static void Error<T>(Exception ex, T metadata, string message = null)
        {
            Log(LogLevels.Error, metadata, message, ex);
        }
    }
}
