using System;
using ProphetsWay.Utilities.Generics;
using System.Collections.Generic;

namespace ProphetsWay.Utilities
{
	public static partial class Logger
	{
		private static readonly IDictionary<Type, IList<IDestination>> Destinations = new Dictionary<Type, IList<IDestination>>();

		/// <summary>
		/// Will add a new LoggingDestination to the pool of targets.  
		/// </summary>
		/// <typeparam name="T">The unconstrained metadata type.</typeparam>
		/// <param name="newDest">Either an existing or a custom Destination that implements the ILoggingDestination interface.</param>
		/// <exception cref="ArgumentNullException"><paramref name="newDest"/> is null.</exception>
		/// <remarks>Null is rejected before registration effects. A reject-all destination remains an active registration.</remarks>
		public static void AddDestination<T>(ILoggingDestination<T> newDest)
		{
			if (newDest == null)
				throw new ArgumentNullException(nameof(newDest));

			if (!Destinations.ContainsKey(typeof(T)))
				Destinations.Add(typeof(T), new List<IDestination>());

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
		/// Now hidden, you shouldn't need to use this method directly, only use the shortcut methods below
		/// </summary>
		/// <param name="level">The severity level of the log statement.</param>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		/// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
		private static void Log<T>(LogLevels level, T metadata, string message, Exception ex = null)
		{
			if (!Destinations.ContainsKey(typeof(T)) || Destinations[typeof(T)].Count == 0)
			{
				Log(level, message, ex);
				return;
			}

			foreach (ILoggingDestination<T> dest in Destinations[typeof(T)])
				if (dest.ValidateMessageLevel(level))
					dest.Log(level, metadata, message, ex);
		}

		/// <summary>Logs a typed message with the exact TraceOnly severity bit.</summary>
		/// <typeparam name="T">The unconstrained metadata type.</typeparam>
		/// <param name="message">The required, non-null message.</param>
		/// <param name="metadata">The original metadata value or reference, including null or default(T).</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only messages are preserved; metadata is not validated or transformed.</remarks>
		public static void Trace<T>(string message, T metadata)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.TraceOnly, metadata, message);
		}

		/// <summary>Logs a typed message with the exact DebugOnly severity bit.</summary>
		/// <typeparam name="T">The unconstrained metadata type.</typeparam>
		/// <param name="message">The required, non-null message.</param>
		/// <param name="metadata">The original metadata value or reference, including null or default(T).</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only messages are preserved; metadata is not validated or transformed.</remarks>
		public static void Debug<T>(string message, T metadata)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.DebugOnly, metadata, message);
		}

		/// <summary>Logs a typed message with the exact InformationOnly severity bit.</summary>
		/// <typeparam name="T">The unconstrained metadata type.</typeparam>
		/// <param name="message">The required, non-null message.</param>
		/// <param name="metadata">The original metadata value or reference, including null or default(T).</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only messages are preserved; metadata is not validated or transformed.</remarks>
		public static void Info<T>(string message, T metadata)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.InformationOnly, metadata, message);
		}

		/// <summary>Logs a typed message with the exact WarningOnly severity bit.</summary>
		/// <typeparam name="T">The unconstrained metadata type.</typeparam>
		/// <param name="message">The required, non-null message.</param>
		/// <param name="metadata">The original metadata value or reference, including null or default(T).</param>
		/// <param name="ex">Optional exception; omitted or null is valid.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only messages and supplied references are preserved; metadata is not validated or transformed.</remarks>
		public static void Warn<T>(string message, T metadata, Exception ex = null)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.WarningOnly, metadata, message, ex);
		}

		/// <summary>Logs a typed exception with the exact ErrorOnly severity bit.</summary>
		/// <typeparam name="T">The unconstrained metadata type.</typeparam>
		/// <param name="ex">The required, non-null exception.</param>
		/// <param name="metadata">The original metadata value or reference, including null or default(T).</param>
		/// <param name="message">Optional context; omitted or null is valid.</param>
		/// <exception cref="ArgumentNullException"><paramref name="ex"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Absent raw context stays null; supplied text and references are preserved without metadata validation or transformation.</remarks>
		public static void Error<T>(Exception ex, T metadata, string message = null)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));

			Log(LogLevels.ErrorOnly, metadata, message, ex);
		}

		/// <summary>Logs a typed exception and context with the exact Critical severity bit.</summary>
		/// <typeparam name="T">The unconstrained metadata type.</typeparam>
		/// <param name="ex">The required, non-null exception.</param>
		/// <param name="metadata">The original metadata value or reference, including null or default(T).</param>
		/// <param name="message">The required, non-null context.</param>
		/// <exception cref="ArgumentNullException"><paramref name="ex"/> or <paramref name="message"/> is null.</exception>
		/// <remarks>Validates before dispatch or fallback effects. Empty and whitespace-only context and supplied references are preserved without metadata validation or transformation. No priority is promised when both required values are null.</remarks>
		public static void Critical<T>(Exception ex, T metadata, string message)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.Critical, metadata, message, ex);
		}
	}
}