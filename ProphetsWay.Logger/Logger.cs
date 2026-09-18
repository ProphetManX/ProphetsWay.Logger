using System;
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
		/// <exception cref="ArgumentNullException"><paramref name="newDest"/> is null.</exception>
		/// <remarks>Null is rejected before registration effects. A reject-all destination remains an active registration.</remarks>
		public static void AddDestination(ILoggingDestination newDest)
		{
			if (newDest == null)
				throw new ArgumentNullException(nameof(newDest));

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

		/// <summary>Logs a message with the exact TraceOnly severity bit.</summary>
		/// <param name="message">The required, non-null message.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only messages are preserved.</remarks>
		public static void Trace(string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.TraceOnly, message);
		}

		/// <summary>Logs a message with the exact DebugOnly severity bit.</summary>
		/// <param name="message">The required, non-null message.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only messages are preserved.</remarks>
		public static void Debug(string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.DebugOnly, message);
		}

		/// <summary>Logs a message with the exact InformationOnly severity bit.</summary>
		/// <param name="message">The required, non-null message.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only messages are preserved.</remarks>
		public static void Info(string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.InformationOnly, message);
		}

		/// <summary>Logs a message with the exact WarningOnly severity bit.</summary>
		/// <param name="message">The required, non-null message.</param>
		/// <param name="ex">Optional exception; omitted or null is valid.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only messages and the supplied exception reference are preserved.</remarks>
		public static void Warn(string message, Exception ex = null)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.WarningOnly, message, ex);
		}

		/// <summary>Logs an exception with the exact ErrorOnly severity bit.</summary>
		/// <param name="ex">The required, non-null exception.</param>
		/// <param name="message">Optional context; omitted or null is valid.</param>
		/// <exception cref="ArgumentNullException"><paramref name="ex"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Raw context stays null when absent; supplied text and the exception reference are preserved.</remarks>
		public static void Error(Exception ex, string message = null)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));

			Log(LogLevels.ErrorOnly, message, ex);
		}

		/// <summary>Logs an exception and context with the exact Critical severity bit.</summary>
		/// <param name="ex">The required, non-null exception.</param>
		/// <param name="message">The required, non-null context.</param>
		/// <exception cref="ArgumentNullException"><paramref name="ex"/> or <paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only context and the exception reference are preserved. No priority is promised when both arguments are invalid.</remarks>
		public static void Critical(Exception ex, string message)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.Critical, message, ex);
		}
	}
}
