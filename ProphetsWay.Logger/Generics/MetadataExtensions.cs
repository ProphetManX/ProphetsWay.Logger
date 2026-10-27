using System;

namespace ProphetsWay.Utilities.Generics
{
    public static class MetadataExtensions
    {
        /// <summary>Forwards a message with the exact TraceOnly severity bit.</summary>
        /// <typeparam name="T">Any type implementing the metadata marker, including a value type.</typeparam>
        /// <param name="metadata">The original metadata value or reference; null and default(T) are valid.</param>
        /// <param name="message">The required, non-null message.</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
        /// <remarks>The typed helper validates before dispatch or fallback effects. Empty and whitespace-only messages are preserved; the receiver is not validated or transformed.</remarks>
        public static void Trace<T>(this T metadata, string message) where T : ILoggerMetadata
        {
            Logger.Trace(message, metadata);
        }

        /// <summary>Forwards a message with the exact DebugOnly severity bit.</summary>
        /// <typeparam name="T">Any type implementing the metadata marker, including a value type.</typeparam>
        /// <param name="metadata">The original metadata value or reference; null and default(T) are valid.</param>
        /// <param name="message">The required, non-null message.</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
        /// <remarks>The typed helper validates before dispatch or fallback effects. Empty and whitespace-only messages are preserved; the receiver is not validated or transformed.</remarks>
        public static void Debug<T>(this T metadata, string message) where T : ILoggerMetadata
        {
            Logger.Debug(message, metadata);
        }

        /// <summary>Forwards a message with the exact InformationOnly severity bit.</summary>
        /// <typeparam name="T">Any type implementing the metadata marker, including a value type.</typeparam>
        /// <param name="metadata">The original metadata value or reference; null and default(T) are valid.</param>
        /// <param name="message">The required, non-null message.</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
        /// <remarks>The typed helper validates before dispatch or fallback effects. Empty and whitespace-only messages are preserved; the receiver is not validated or transformed.</remarks>
        public static void Info<T>(this T metadata, string message) where T : ILoggerMetadata
        {
            Logger.Info(message, metadata);
        }

        /// <summary>Forwards a message with the exact WarningOnly severity bit.</summary>
        /// <typeparam name="T">Any type implementing the metadata marker, including a value type.</typeparam>
        /// <param name="metadata">The original metadata value or reference; null and default(T) are valid.</param>
        /// <param name="message">The required, non-null message.</param>
        /// <param name="ex">Optional exception; omitted or null is valid.</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
        /// <remarks>The typed helper validates before dispatch or fallback effects. Empty and whitespace-only messages and supplied references are preserved; the receiver is not validated or transformed.</remarks>
        public static void Warn<T>(this T metadata, string message, Exception ex = null) where T : ILoggerMetadata
        {
            Logger.Warn(message, metadata, ex);
        }

        /// <summary>Forwards an exception with the exact ErrorOnly severity bit.</summary>
        /// <typeparam name="T">Any type implementing the metadata marker, including a value type.</typeparam>
        /// <param name="metadata">The original metadata value or reference; null and default(T) are valid.</param>
        /// <param name="ex">The required, non-null exception.</param>
        /// <param name="message">Optional context; omitted or null is valid.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ex"/> is null.</exception>
        /// <remarks>The typed helper validates before dispatch or fallback effects. Absent raw context stays null; supplied text and references are preserved without receiver validation or transformation.</remarks>
        public static void Error<T>(this T metadata, Exception ex, string message = null) where T : ILoggerMetadata
        {
            Logger.Error(ex, metadata, message);
        }

        /// <summary>Forwards an exception and context with the exact Critical severity bit.</summary>
        /// <typeparam name="T">Any type implementing the metadata marker, including a value type.</typeparam>
        /// <param name="metadata">The original metadata value or reference; null and default(T) are valid.</param>
        /// <param name="ex">The required, non-null exception.</param>
        /// <param name="message">The required, non-null context.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ex"/> or <paramref name="message"/> is null.</exception>
        /// <remarks>The typed helper validates before dispatch or fallback effects. Empty and whitespace-only context and supplied references are preserved without receiver validation or transformation. No priority is promised when both required values are null.</remarks>
        public static void Critical<T>(this T metadata, Exception ex, string message) where T : ILoggerMetadata
        {
            Logger.Critical(ex, metadata, message);
        }
    }
}