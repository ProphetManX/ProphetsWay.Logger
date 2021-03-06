﻿using System;
using System.Collections.Generic;
using ProphetsWay.Utilities.LoggerDestinations;

namespace ProphetsWay.Utilities
{
	public static partial class Logger
	{
		//private static readonly IList<ILoggingDestination> Destinations = new List<ILoggingDestination>();
		private static readonly Type _nonGenericTypDestination = typeof(ILoggingDestination);
		/// <summary>
		/// Will add a new LoggingDestination to the pool of targets.  
		/// </summary>
		/// <param name="newDest">Either an existing or a custom Destination that implements the ILoggingDestination interface.</param>
		public static void AddDestination(ILoggingDestination newDest)
		{
			if (!Destinations.ContainsKey(_nonGenericTypDestination))
				Destinations.Add(_nonGenericTypDestination, new List<IDestination>());

			Destinations[_nonGenericTypDestination].Add(newDest);
		}

		/// <summary>
		/// If you retain a reference to your LoggingDestination, you can remove it from the pool of targets.
		/// </summary>
		/// <param name="destToRemove">Either an existing or a custom Destination that implements the ILoggingDestination interface; must have already been added to the pool via "AddDestination".</param>
		public static void RemoveDestination(ILoggingDestination destToRemove)
		{
			if (Destinations.ContainsKey(_nonGenericTypDestination))
				Destinations[_nonGenericTypDestination].Remove(destToRemove);
		}

		/// <summary>
		/// Resets the pool of targets, removes any/all Destinations that have been added.
		/// </summary>
		public static void ClearDestinations()
		{
			if (Destinations.ContainsKey(_nonGenericTypDestination))
				Destinations[_nonGenericTypDestination].Clear();
		}

		/// <summary>
		/// Now hidden, you shouldn't need to use this method directly, only use the shortcut methods below
		/// </summary>
		/// <param name="level">The severity level of the log statement.</param>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		/// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
		private static void Log(LogLevels level, string message, Exception ex = null)
		{
			if (!Destinations.ContainsKey(_nonGenericTypDestination))
				Destinations.Add(_nonGenericTypDestination, new List<IDestination>());

			if (Destinations[_nonGenericTypDestination].Count == 0)
				AddDestination(new FileDestination($"Default Log {DateTime.Now:yyyy-MM-dd hh-mm}.log"));

			foreach (ILoggingDestination dest in Destinations[_nonGenericTypDestination])
				if (dest.ValidateMessageLevel(level))
					dest.Log(level, message, ex);
		}

		/// <summary>
		/// Shortcut method to Log a message with a LogLevel of 'Debug'
		/// </summary>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		public static void Debug(string message)
		{
			Log(LogLevels.DebugOnly, message);
		}

		/// <summary>
		/// Shortcut method to Log a message with a LogLevel of 'Information'
		/// </summary>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		public static void Info(string message)
		{
			Log(LogLevels.InformationOnly, message);
		}

		/// <summary>
		/// Shortcut method to Log a message with a LogLevel of 'Security'
		/// </summary>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		public static void Security(string message)
		{
			Log(LogLevels.SecurityOnly, message);
		}

		/// <summary>
		/// Shortcut method to Log a message with a LogLevel of 'Warning'
		/// </summary>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		/// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
		public static void Warn(string message, Exception ex = null)
		{
			Log(LogLevels.WarningOnly, message, ex);
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
