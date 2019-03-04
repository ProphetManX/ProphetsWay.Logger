using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	public class LoggerTests
	{
		[Fact]
		public void LogShouldAddDestination()
		{
			//setup
			var triggered = false;
			var dest = new EventDestination(LogLevels.Debug);
			dest.LoggingEvent += (sender, args) => triggered = true;

			//act
			Utilities.Logger.AddDestination(dest);
			Utilities.Logger.Debug("Hello World!");

			//assert
			Assert.True(triggered);
		}

		[Fact]
		public void LogShouldRemoveDestination()
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
			Assert.False(triggered);
		}

		[Fact]
		public void LogShouldClearAllDestinations()
		{
			//setup
			var triggered = false;
			var dest1 = new EventDestination(LogLevels.Debug);
			dest1.LoggingEvent += (sender, args) => triggered = true;
			var dest2 = new EventDestination(LogLevels.Debug);
			dest2.LoggingEvent += (sender, args) => triggered = true;
			var dest3 = new EventDestination(LogLevels.Debug);
			dest3.LoggingEvent += (sender, args) => triggered = true;

			//act
			Utilities.Logger.AddDestination(dest1);
			Utilities.Logger.AddDestination(dest2);
			Utilities.Logger.AddDestination(dest3);
			Utilities.Logger.ClearDestinations();
			Utilities.Logger.Debug("Hello World!");

			//assert
			Assert.False(triggered);
		}




	}
}
