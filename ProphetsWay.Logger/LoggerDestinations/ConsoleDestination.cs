using System;

namespace ProphetsWay.Utilities.LoggerDestinations{
	/// <summary>
	/// A basic destination that simply prints the log statements out to the Console.
	/// </summary>
	public class ConsoleDestination : TextBasedDestination
	{
		private readonly ConsoleWrapper _wrapper;

		/// <summary>
		/// A basic destination that simply prints the log statements out to the Console.
		/// </summary>
		/// <param name="reportingLevel">The severity level that you wish to have messages logged to the console.</param>
		/// <param name="wrapper">Wrapper for the Console.WriteLine method, allow for customization and Unit Testing. (not required)</param>
		public ConsoleDestination(LogLevels reportingLevel = LogLevels.Debug, ConsoleWrapper wrapper = null) : base(reportingLevel)
		{
			_wrapper = UseOrCreateWrapper(wrapper);
		}

		/// <summary>
		/// A basic destination that simply prints the log statements out to the Console.
		/// </summary>
		/// <param name="strReportingLevel">The string representation of the severity level that you wish to have messages logged to the console.</param>
		/// <param name="wrapper">Wrapper for the Console.WriteLine method, allow for customization and Unit Testing. (not required)</param>
		public ConsoleDestination(string strReportingLevel, ConsoleWrapper wrapper = null) : base(strReportingLevel)
		{
			_wrapper = UseOrCreateWrapper(wrapper);
		}

		/// <summary>
		/// A basic destination that simply prints the log statements out to the Console.
		/// </summary>
		/// <param name="intReportingLevel">The integer representation of the severity level that you wish to have messages logged to the console.</param>
		/// <param name="wrapper">Wrapper for the Console.WriteLine method, allow for customization and Unit Testing. (not required)</param>
		public ConsoleDestination(int intReportingLevel, ConsoleWrapper wrapper = null) : base(intReportingLevel) 
		{
			_wrapper = UseOrCreateWrapper(wrapper);
		}

		private ConsoleWrapper UseOrCreateWrapper(ConsoleWrapper wrapper)
		{
			return wrapper ?? new ConsoleWrapper();
		}

		protected override void PrintLogEntry(string message)
		{
			lock(LoggerLock)
				_wrapper.WriteLine(message);
		}

		public class ConsoleWrapper
		{
			public virtual void WriteLine(string input)
			{
				Console.WriteLine(input);
			}

		}
	}
}