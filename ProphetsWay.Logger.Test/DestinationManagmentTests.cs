using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class DestinationManagementTests
	{
		[Fact]
		public void ShouldAddDestination()
		{
			//setup
			var triggered = false;
			var dest = new EventDestination(LogLevels.Debug);
			dest.LoggingEvent += (sender, args) => triggered = true;

			//act
			Utilities.Logger.AddDestination(dest);
			Utilities.Logger.Debug("Hello World!");

			//assert
			triggered.Should().BeTrue();

			//cleanup
			Utilities.Logger.RemoveDestination(dest);
		}

		[Fact]
		public void ShouldRemoveDestination()
		{
			//setup
			var triggered = false;
			var dest = new EventDestination(LogLevels.Debug);
			dest.LoggingEvent += (sender, args) => triggered = true;

			//act
			Utilities.Logger.AddDestination(dest);
			Utilities.Logger.RemoveDestination(dest);
			Utilities.Logger.Debug("Hello World!");

			//assert
			triggered.Should().BeFalse();
		}

		[Fact]
		public void ShouldClearAllDestinations()
		{
			//setup
			var triggered = false;
			var dest1 = new EventDestination(LogLevels.Debug);
			dest1.LoggingEvent += (sender, args) => triggered = true;
			var dest2 = new EventDestination(LogLevels.Debug);
			dest2.LoggingEvent += (sender, args) => triggered = true;
			var dest3 = new EventDestination(LogLevels.Debug);
			dest3.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.ClearDestinations();

			//act
			Utilities.Logger.AddDestination(dest1);
			Utilities.Logger.AddDestination(dest2);
			Utilities.Logger.AddDestination(dest3);
			Utilities.Logger.ClearDestinations();
			Utilities.Logger.Debug("Hello World!");

			//assert
			triggered.Should().BeFalse();
		}

		[Fact]
		public void ShouldRemoveDestinationEvenIfNotInList()
		{
			//setup
			var dest = new EventDestination(LogLevels.Debug);

			//act
			Utilities.Logger.RemoveDestination(dest);

			//assert that no exception was thrown
		}
	}
}
