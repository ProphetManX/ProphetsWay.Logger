using ProphetsWay.Utilities.Generics;
using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>
	/// A generic destination meant for use in situations that require events to be invoked (ie: UI applications).
	/// The generic version allows for passing a custom object thru to your logging destination to be
	/// handled specially.  (ie: database logging parameters)
	/// </summary>
	public class GenericEventDestination<T> : BaseLoggingDestination<T>
	{
		/// <summary>
		/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
		/// </summary>
		public GenericEventDestination(LogLevels reportingLevel) : base(reportingLevel) { }

		/// <summary>
		/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
		/// </summary>
		public GenericEventDestination(string strReportingLevel) : base(strReportingLevel) { }

		/// <summary>
		/// A basic destination meant for use in situations that require events to be invoked (ie: UI applications).
		/// </summary>
		public GenericEventDestination(int intReportingLevel) : base(intReportingLevel) { }

		/// <summary>Raises a typed log callback only when every message bit is accepted.</summary>
		/// <param name="level">A nonzero combination of known severity bits, from 1 through 63.</param>
		/// <param name="metadata">The original metadata value or reference, including null or default(T).</param>
		/// <param name="message">Optional raw context, including null, empty or whitespace.</param>
		/// <param name="ex">Optional exception; null is valid at every accepted mask.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is zero, negative or contains an unknown bit.</exception>
		/// <remarks>Validity and eligibility precede massage and callbacks, even without subscribers. A valid mismatch returns without recipient work. Accepted callbacks retain the full mask, raw context, exception and metadata without transformation, with massaged text in Message.</remarks>
		public override void Log(LogLevels level, T metadata, string message = null, Exception ex = null)
		{
			if (level == 0 || (level & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(level));
			if (!ValidateMessageLevel(level))
				return;

			var evt = new LoggerEventArgs(message, level, ex, metadata, MassageLogStatement(level, message, ex));
			LoggingEvent?.Invoke(this, evt);
		}

		/// <summary>
		/// The event you must subscribe to, to receive the relevant log events.
		/// </summary>
		public EventHandler<LoggerEventArgs> LoggingEvent;

		public class LoggerEventArgs : EventArgs
		{
			public LoggerEventArgs(string message, LogLevels level, Exception ex, T metadata, string massagedMessage)
			{
				RawMessage = message;
				LogLevel = level;
				Metadata = metadata;
				Timestamp = DateTime.Now;
				Exception = ex;
				Message = massagedMessage;
			}

			public string Message { get; }
			public T Metadata { get; }
			public Exception Exception { get; }
			public LogLevels LogLevel { get; }
			public DateTime Timestamp { get; }
			public string RawMessage { get; }
		}
	}
}
