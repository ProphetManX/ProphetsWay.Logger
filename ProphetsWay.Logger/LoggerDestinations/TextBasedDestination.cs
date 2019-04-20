using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>
	/// A base class to handle logging destinations that are inherintly text based.
	/// It will prepend a timestamp and the LogLevel to the message before passing the message out.
	/// </summary>
	public abstract class TextBasedDestination : BaseLoggingDestination
	{
        public override void Log(LogLevels level, string message = null, Exception ex = null)
        {
            var massagedMessage = MassageLogStatement(level, message, ex);
            var msg = $"{DateTime.Now} :: {level.ToString().PadLeft(12)}:  {massagedMessage}";
            PrintLogEntry(msg);
        }

        protected TextBasedDestination(LogLevels reportingLevel) : base(reportingLevel){}

		protected abstract void PrintLogEntry(string message);
	}
}