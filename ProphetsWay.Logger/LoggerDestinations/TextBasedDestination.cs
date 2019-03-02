using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	
	/// <summary>
	/// A base class to handle logging destinations that are inherintly text based.
	/// It will prepend a timestamp and the LogLevel to the message before passing the message out.
	/// </summary>
	public abstract class TextBasedDestination : BaseLoggingDestination
	{
		protected TextBasedDestination(LogLevels reportingLevel) : base(reportingLevel){}

		protected override void WriteLogEntry(string message, LogLevels level){
			var msg = $"{DateTime.Now} :: {level.ToString().PadLeft(12)}:  {message}";
			PrintLogEntry(msg);
		}

		protected abstract void PrintLogEntry(string message);
	}
}