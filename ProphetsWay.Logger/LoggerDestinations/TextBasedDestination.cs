using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>
	/// A base class to handle logging destinations that are inherintly text based.
	/// It will prepend a timestamp and the LogLevel to the message before passing the message out.
	/// </summary>
	public abstract class TextBasedDestination : BaseLoggingDestination
	{
		/// <summary>Prints a composed text entry only when every message bit is accepted.</summary>
		/// <param name="level">A nonzero combination of known severity bits, from 1 through 63.</param>
		/// <param name="message">Optional raw context, including null, empty or whitespace.</param>
		/// <param name="ex">Optional exception; null is valid at every accepted mask.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is zero, negative or contains an unknown bit.</exception>
		/// <remarks>Validity and eligibility precede massage, text composition and PrintLogEntry. A valid mismatch returns without recipient work. Accepted content uses the existing local timestamp, severity and massaged-text layout.</remarks>
		public override void Log(LogLevels level, string message = null, Exception ex = null)
		{
			if (level == 0 || (level & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(level));
			if (!ValidateMessageLevel(level))
				return;

			var massagedMessage = MassageLogStatement(level, message, ex);
			var msg = $"{DateTime.Now} :: {level.ToString().PadLeft(12)}:  {massagedMessage}";
			PrintLogEntry(msg);
		}

		protected TextBasedDestination(LogLevels reportingLevel) : base(reportingLevel) { }
		protected TextBasedDestination(string strReportingLevel) : base(strReportingLevel) { }
		protected TextBasedDestination(int intReportingLevel) : base(intReportingLevel) { }

		protected abstract void PrintLogEntry(string message);
	}
}