using System;
using ProphetsWay.Utilities.Generics;
using System.Collections.Generic;

namespace ProphetsWay.Utilities
{
	public static partial class Logger
	{
		private static readonly IDictionary<Type, List<IDestination>> Destinations = new Dictionary<Type, List<IDestination>>();

		/// <summary>
		/// Registers a borrowed destination at the end of the explicit route for exactly T.
		/// </summary>
		/// <typeparam name="T">The unconstrained declared metadata type identifying this route.</typeparam>
		/// <param name="newDest">The non-null compatible destination instance to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="newDest"/> is null.</exception>
		/// <exception cref="ArgumentException">The same reference is already registered on this exact route; ParamName is "newDest".</exception>
		/// <remarks>
		/// Null and duplicate rejection leave membership unchanged. Identity never uses user equality or hashing.
		/// Ordinary and other declared-type routes are independent; compatible cross-route reuse is allowed.
		/// Publication completes before return without draining captured calls or invoking recipient code.
		/// A reject-all destination remains an active registration; custom recipient state is not frozen.
		/// </remarks>
		public static void AddDestination<T>(ILoggingDestination<T> newDest)
		{
			if (newDest == null)
				throw new ArgumentNullException(nameof(newDest));

			lock (DestinationLock)
			{
				if (!Destinations.TryGetValue(typeof(T), out var destinations))
				{
					destinations = new List<IDestination>();
					Destinations.Add(typeof(T), destinations);
				}

				if (destinations.Exists(destination => ReferenceEquals(destination, newDest)))
					throw new ArgumentException("The destination is already registered on this route.", nameof(newDest));

				destinations.Add(newDest);
			}
		}

		/// <summary>
		/// Removes a borrowed destination reference from the explicit route for exactly T.
		/// </summary>
		/// <typeparam name="T">The unconstrained declared metadata type identifying this route.</typeparam>
		/// <param name="destToRemove">The exact instance to remove. Null or an absent instance is a no-op.</param>
		/// <remarks>
		/// Uses reference identity, preserves survivor order and leaves every other route unchanged.
		/// Never invokes or disposes the recipient. Returns after publication, not draining older captures.
		/// Hosts must stop producers and await synchronous calls before disposing borrowed recipients.
		/// </remarks>
		public static void RemoveDestination<T>(ILoggingDestination<T> destToRemove)
		{
			lock (DestinationLock)
			{
				if (Destinations.TryGetValue(typeof(T), out var destinations))
					destinations.RemoveAll(destination => ReferenceEquals(destination, destToRemove));
			}
		}

		/// <summary>
		/// Clears explicit membership for exactly T without changing any other route.
		/// </summary>
		/// <typeparam name="T">The unconstrained declared metadata type identifying this route.</typeparam>
		/// <remarks>
		/// An unconfigured or empty route is a no-op. Never invokes or disposes borrowed recipients.
		/// Older captures retain their ordered references and may finish after publication returns.
		/// This is not a drain or a guarantee about subsequent unconfigured-route logging.
		/// </remarks>
		public static void ClearDestinations<T>()
		{
			lock (DestinationLock)
			{
				if (Destinations.TryGetValue(typeof(T), out var destinations))
					destinations.Clear();
			}
		}

		/// <summary>
		/// Now hidden, you shouldn't need to use this method directly, only use the shortcut methods below
		/// </summary>
		/// <param name="level">The severity level of the log statement.</param>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		/// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
		/// <remarks>
		/// Captures complete ordered membership for declared T before eligibility, never by runtime metadata type.
		/// User code runs outside registry coordination; mutations affect later captures and recursion captures anew.
		/// Payloads and custom recipient state are not cloned. Existing unconfigured fallback remains unqualified.
		/// </remarks>
		private static void Log<T>(LogLevels level, T metadata, string message, Exception ex = null)
		{
			IDestination[] destinations;
			lock (DestinationLock)
			{
				destinations = Destinations.TryGetValue(typeof(T), out var route)
					? route.ToArray()
					: Array.Empty<IDestination>();
			}

			if (destinations.Length == 0)
			{
				Log(level, message, ex);
				return;
			}

			foreach (ILoggingDestination<T> dest in destinations)
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