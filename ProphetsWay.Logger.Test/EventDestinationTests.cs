using FluentAssertions;
using Xunit;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class EventDestinationTests
	{
		[Fact]
		public  void ShouldTriggerEventWhenLoggingMessage()
		{
			//setup
			var triggered = false;
			var dest = new EventDestination(LogLevels.Debug);
			dest.LoggingEvent += (sender, args) => { triggered = true; };
			Utilities.Logger.AddDestination(dest);

			//act
			Utilities.Logger.Debug("Hello World!");

			//assert
			triggered.Should().BeTrue();

			//cleanup
			Utilities.Logger.RemoveDestination(dest);

		}

	}
}
