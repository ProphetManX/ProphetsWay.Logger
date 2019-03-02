using System;
using System.Collections.Generic;
using ProphetsWay.Utilities.LoggerDestinations;

namespace ProphetsWay.Utilities
{
	public static class Logger
	{
		private static readonly IList<ILoggingDestination> Destinations = new List<ILoggingDestination>();

		/// <summary>
		/// Will add a new LoggingDestination to the pool of targets.  
		/// </summary>
		/// <param name="newDest">Either an existing or a custom Destination that implements the ILoggingDestination interface.</param>
		public static void AddDestination(ILoggingDestination newDest)
		{
			Destinations.Add(newDest);
		}

		/// <summary>
		/// If you retain a reference to your LoggingDestination, you can remove it from the pool of targets.
		/// </summary>
		/// <param name="destToRemove">Either an existing or a custom Destination that implements the ILoggingDestination interface; must have already been added to the pool via "AddDestination".</param>
		public static void RemoveDestination(ILoggingDestination destToRemove)
		{
			Destinations.Remove(destToRemove);
		}

		/// <summary>
		/// Resets the pool of targets, removes any/all Destinations that have been added.
		/// </summary>
		public static void ClearDestinations()
		{
			Destinations.Clear();
		}

		/// <summary>
		/// Use this method if you want to log with a grouped set of LogLevels
		/// Ex:  Warning | Security | Information
		/// Will write to Destinations with any of those three LogLevels enabled.
		/// </summary>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		/// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
		public static void Log(LogLevels level, string message = null, Exception ex = null)
		{
			if (Destinations.Count == 0)
				AddDestination(new FileDestination($"Default Log {DateTime.Now:yyyy-MM-dd hh-mm}.log"));

			foreach (var dest in Destinations)
				dest.Log(level, message, ex);
		}

		/// <summary>
		/// Shortcut method to Log a message with a LogLevel of 'Debug'
		/// </summary>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		public static void Debug(string message)
		{
			Log(LogLevels.Debug, message);
		}

		/// <summary>
		/// Shortcut method to Log a message with a LogLevel of 'Information'
		/// </summary>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		public static void Info(string message)
		{
			Log(LogLevels.Information, message);
		}

		/// <summary>
		/// Shortcut method to Log a message with a LogLevel of 'Security'
		/// </summary>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		public static void Security(string message)
		{
			Log(LogLevels.Security, message);
		}

		/// <summary>
		/// Shortcut method to Log a message with a LogLevel of 'Warning'
		/// </summary>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		/// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
		public static void Warn(string message, Exception ex = null)
		{
			Log(LogLevels.Warning, message, ex);
		}

		/// <summary>
		/// Shortcut method to Log a message with a LogLevel of 'Error'
		/// </summary>
		/// <param name="ex">Required, pass the exception you want to convey in the log entry.</param>
		/// <param name="message">Optional, if no message is passed, the Exception message will still be written to the log.  
		/// If you want to add more context to the error, you can enter it in the message.</param>
		public static void Error(Exception ex, string message = null)
		{
			Log(LogLevels.Error, message, ex);
		}
	}
}
