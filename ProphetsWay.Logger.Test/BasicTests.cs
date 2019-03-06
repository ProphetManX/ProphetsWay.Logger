using System;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class BasicTests
	{
		[Fact]
		public void ShouldContainMessage()
		{
			//setup
			const string msg = "Hello World!";
			var d = new EventDestination(LogLevels.Debug);
			d.LoggingEvent += (sender, args) => Assert.Contains(msg, args.Message);

			//act
			Utilities.Logger.Log(LogLevels.Debug, msg, new Exception("Custom Exception"));

			//assert 
			//it's inside the logging event handler
		}

		[Fact]
		public void ShouldContainException()
		{
			//setup
			var e = new Exception("Hello World!");
			var d = new EventDestination(LogLevels.Error);
			d.LoggingEvent += (sender, args) => Assert.Equal(e.Message, args.Message);

			//act
			Utilities.Logger.Log(LogLevels.Error, "Goodbye Everyone", e);

			//assert
			//it's inside the logging event handler
		}
	}
}
