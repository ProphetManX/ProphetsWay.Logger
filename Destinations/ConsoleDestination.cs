using System;

namespace ProphetsWay.Logger.Destinations{
    public class ConsoleDestination : TextBasedDestination
    {
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