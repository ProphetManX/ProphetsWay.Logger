using System;

namespace ProphetsWay.Logger.Destinations{
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