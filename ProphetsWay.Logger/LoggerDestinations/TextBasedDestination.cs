using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>
	/// A base class to handle logging destinations that are inherintly text based.
	/// It will prepend a timestamp and the LogLevel to the message before passing the message out.
	/// </summary>
	public abstract class TextBasedDestination : BaseLoggingDestination
	{
		/// <inheritdoc />
		/// <remarks>B11/B12: the supplied guard has completed. Preserve existing massage and output
		/// semantics. Context is the selected completed capture. Text delivery massages
		/// once, composes the existing local-time/severity/text layout, then calls PrintLogEntry once.
		/// No implicit scope/property serialization, new timestamp capture or framing rule is added.</remarks>
		protected override void LogCore(LogContext context, LogLevels level, string message, Exception ex)
		{
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