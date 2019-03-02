using System;

namespace ProphetsWay.Utilities.LoggerDestinations{
	/// <summary>
	/// A basic destination that simply prints the log statements out to the Console.
	/// </summary>
	public class ConsoleDestination : TextBasedDestination
	{
		/// <summary>
		/// A basic destination that simply prints the log statements out to the Console.
		/// </summary>
		public ConsoleDestination(LogLevels reportingLevel = LogLevels.Debug) : base(reportingLevel)
		{

		}

		protected override void PrintLogEntry(string message)
		{
			lock(LoggerLock)
				Console.WriteLine(message);
		}
	}
}