using Microsoft.Extensions.Logging;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

namespace ProphetsWay.Example
{
	// Compile-only references; Program.Main intentionally does not invoke this method.
	internal static class MicrosoftBridgeExamples
	{
		internal static MicrosoftLoggerProvider Register(ILoggerFactory loggerFactory, ILogger ordinaryLogger)
		{
			var provider = loggerFactory.AddProphetsWay();

			Logger.AddDestination(new MicrosoftLoggerDestination(LogLevels.Trace, ordinaryLogger));
			Logger.AddDestination<LogBridgeEntry>(
				new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, loggerFactory));
			return provider;
		}
	}
}
