using System;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

namespace ProphetsWay.Example
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");

			Logger.AddDestination(new ConsoleDestination());
			Logger.Debug("Hello World!");

			var ex = new Exception("This is a specific Exception Message and will contain a stack trace.");
			Logger.Error(ex, "This is a generic message about what happened.");


			var ex2 = new Exception("This exception has an inner exception. (likely details to hide from a UI)", ex);
			Logger.Error(ex2, "Another generic message about an error occuring. (friendly message to show a UI maybe?)");


			var dest = new FileDestination("test.log", LogLevels.Warning, false, FileDestination.EncodingOptions.UTF8);

			var evtDest = new EventDestination(LogLevels.Debug);
			evtDest.LoggingEvent += (sender, eventArgs) => { /* whatever you want to do with the message here */ };
		}
	}
}
