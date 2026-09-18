using System;

namespace ProphetsWay.Utilities
{
	/// <summary>
	/// Core functionality that all Logging destinations will need to use, 
	/// manages log level comparison in a single location,
	/// includes a LoggerLock for use if needed in any inheriting destination.
	/// </summary>
	public abstract class LoggingDestinationCore : IDestination
	{
		/// <summary>Creates a destination with the supplied severity mask.</summary>
		/// <param name="reportingLevel">Any combination of known severity bits, from 0 through 63.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="reportingLevel"/> contains an unknown bit or is negative.</exception>
		/// <remarks>Zero is an active reject-all mask. Unnamed known-bit combinations are valid.</remarks>
		public LoggingDestinationCore(LogLevels reportingLevel)
		{
			if ((reportingLevel & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(reportingLevel));

			_reportingLevel = reportingLevel;
		}

		/// <summary>Creates a destination by parsing a case-sensitive severity mask.</summary>
		/// <param name="strReportingLevel">A decimal integer or comma-separated recognized severity names.</param>
		/// <exception cref="ArgumentNullException"><paramref name="strReportingLevel"/> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="strReportingLevel"/> cannot be parsed or represents a mask outside 0 through 63.</exception>
		/// <remarks>Uses the case-sensitive Enum.TryParse grammar, including its whitespace handling. Zero rejects all messages; unnamed known-bit combinations are valid.</remarks>
		public LoggingDestinationCore(string strReportingLevel)
		{
			_reportingLevel = ParseReportingLevel(strReportingLevel);
		}

		/// <summary>Creates a destination with the supplied integer severity mask.</summary>
		/// <param name="intReportingLevel">Any combination of known severity bits, from 0 through 63.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="intReportingLevel"/> contains an unknown bit or is negative.</exception>
		/// <remarks>Zero is an active reject-all mask. No named enum constant is required.</remarks>
		public LoggingDestinationCore(int intReportingLevel)
		{
			if ((intReportingLevel & ~(int)LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(intReportingLevel));

			_reportingLevel = (LogLevels)intReportingLevel;
		}

		private LogLevels ParseReportingLevel(string strReportingLevel)
		{
			if (strReportingLevel == null)
				throw new ArgumentNullException(nameof(strReportingLevel));

			if (!Enum.TryParse(strReportingLevel, out LogLevels reportingLevel) || (reportingLevel & ~LogLevels.Trace) != 0)
				throw new ArgumentException("The reporting level must be a mask of known severity bits.", nameof(strReportingLevel));

			return reportingLevel;
		}

		/// <summary>
		/// A lock object for use in making threadsafe destinations
		/// </summary>
		protected readonly object LoggerLock = new object();

		private readonly LogLevels _reportingLevel;

		/// <summary>
		/// Combines the supplied context with exception messages and available stack traces.
		/// </summary>
		/// <param name="level">The validated, eligible message mask.</param>
		/// <param name="message">Optional context, preserved without trimming or substitution.</param>
		/// <param name="ex">Optional exception whose nested messages and available stacks are included.</param>
		/// <returns>The original message, including null, when no exception is supplied; otherwise context and exception detail.</returns>
		/// <remarks>Applies to every valid mask. This hook neither dispatches nor prints and does not add an independent mask guard.</remarks>
		protected virtual string MassageLogStatement(LogLevels level, string message = null, Exception ex = null)
		{
			if (ex == null)
				return message;

			ExceptionDetailer(ex, out string exceptionMessage, out string exceptionStackTrace);
			return $"{message}{Environment.NewLine}{exceptionMessage}{Environment.NewLine}{exceptionStackTrace}";
		}

		/// <summary>
		/// Recursive function to peer deep into an Exception and its Inner Exceptions to capture the messages and stack traces within.
		/// </summary>
		private static void ExceptionDetailer(Exception ex, out string message, out string stack)
		{
			var imessage = string.Empty;
			var istack = string.Empty;

			if (ex.InnerException != null)
				ExceptionDetailer(ex.InnerException, out imessage, out istack);

			message = string.IsNullOrEmpty(imessage)
				? ex.Message
				: string.Format("{0}{1}{1}Inner Exception Message:{1}{2}", ex.Message, Environment.NewLine, imessage);

			stack = string.IsNullOrEmpty(istack)
				? ex.StackTrace
				: string.Format("{0}{1}{1}Inner Exception Stack Trace:{1}{2}", ex.StackTrace, Environment.NewLine, istack);
		}


		/// <summary>
		/// Tests whether the destination includes every requested message bit.
		/// </summary>
		/// <param name="messageLevel">A nonzero combination of known severity bits, from 1 through 63.</param>
		/// <returns>True when all message bits are accepted; false for a valid mismatch, including any message against destination mask zero.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="messageLevel"/> is zero, negative or contains an unknown bit.</exception>
		/// <remarks>This query does not dispatch or change the captured mask or registration.</remarks>
		public bool ValidateMessageLevel(LogLevels messageLevel)
		{
			if (messageLevel == 0 || (messageLevel & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(messageLevel));

			return (messageLevel & _reportingLevel) == messageLevel;
		}
	}
}
