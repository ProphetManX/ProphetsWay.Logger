using System;
using System.Collections.Generic;
using ProphetsWay.Utilities.LoggerDestinations;

namespace ProphetsWay.Utilities
{
	public static partial class Logger
	{
		private static readonly object DestinationLock = new object();
		private static readonly List<ILoggingDestination> OrdinaryDestinations = new List<ILoggingDestination>();

		/// <summary>
		/// Registers a borrowed destination at the end of the ordinary explicit route.
		/// </summary>
		/// <param name="newDest">The non-null destination instance to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="newDest"/> is null.</exception>
		/// <exception cref="ArgumentException">The same reference is already registered on the ordinary route; ParamName is "newDest".</exception>
		/// <remarks>
		/// Null and duplicate rejection leave membership unchanged. Identity never uses user equality or hashing.
		/// Typed routes are independent, and the same instance may register on other compatible routes.
		/// Publication completes before return without draining captured calls or invoking recipient code.
		/// A reject-all destination remains an active registration; custom recipient state is not frozen.
		/// </remarks>
		public static void AddDestination(ILoggingDestination newDest)
		{
			if (newDest == null)
				throw new ArgumentNullException(nameof(newDest));

			lock (DestinationLock)
			{
				if (OrdinaryDestinations.Exists(destination => ReferenceEquals(destination, newDest)))
					throw new ArgumentException("The destination is already registered on this route.", nameof(newDest));

				OrdinaryDestinations.Add(newDest);
			}
		}

		/// <summary>
		/// Removes a borrowed destination reference from the ordinary explicit route only.
		/// </summary>
		/// <param name="destToRemove">The exact instance to remove. Null or an absent instance is a no-op.</param>
		/// <remarks>
		/// Uses reference identity and preserves survivor order. Never invokes or disposes the recipient.
		/// Returns after publication, not draining; older captures may still call the removed recipient.
		/// Hosts must stop producers and await synchronous calls before disposing borrowed recipients.
		/// </remarks>
		public static void RemoveDestination(ILoggingDestination destToRemove)
		{
			lock (DestinationLock)
			{
				OrdinaryDestinations.RemoveAll(destination => ReferenceEquals(destination, destToRemove));
			}
		}

		/// <summary>
		/// Clears ordinary explicit membership without changing any typed route.
		/// </summary>
		/// <remarks>
		/// An empty route is a no-op. Publishes empty membership without invoking or disposing recipients.
		/// Older captures retain their ordered references and may finish after this method returns.
		/// This is not a drain or a guarantee about subsequent unconfigured-route logging.
		/// </remarks>
		public static void ClearDestinations()
		{
			lock (DestinationLock)
			{
				OrdinaryDestinations.Clear();
			}
		}

		/// <summary>
		/// Now hidden, you shouldn't need to use this method directly, only use the shortcut methods below
		/// </summary>
		/// <param name="level">The severity level of the log statement.</param>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		/// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
		/// <remarks>
		/// Captures complete ordered membership before eligibility. User code runs outside registry coordination.
		/// Mutations affect later captures only; recursive logging captures anew. Payloads and custom state are not cloned.
		/// </remarks>
		private static void Log(LogLevels level, string message, Exception ex = null)
		{
			ILoggingDestination[] destinations;
			lock (DestinationLock)
			{
				destinations = OrdinaryDestinations.ToArray();
			}

			if (destinations.Length == 0)
			{
				AddDestination(new FileDestination($"Default Log {DateTime.Now:yyyy-MM-dd hh-mm}.log"));
				lock (DestinationLock)
				{
					destinations = OrdinaryDestinations.ToArray();
				}
			}

			foreach (ILoggingDestination dest in destinations)
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
