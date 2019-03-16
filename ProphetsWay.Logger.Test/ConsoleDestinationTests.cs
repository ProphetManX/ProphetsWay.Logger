using FluentAssertions;
using Moq;
using Xunit;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class ConsoleDestinationTests
	{
		[Fact]
		public void ShouldTriggerWriteLineWhenLoggingMessage()
		{
			//setup
			const string msg = "Hello World!";
			var mWrapper = new Mock<ConsoleDestination.ConsoleWrapper>();
			mWrapper.Setup(w => w.WriteLine(It.IsAny<string>())).Verifiable();
			var dest = new ConsoleDestination(LogLevels.Debug, mWrapper.Object);
			Utilities.Logger.AddDestination(dest);

			//act
			Utilities.Logger.Debug(msg);

			//assert
			mWrapper.VerifyAll();

			//cleanup
			Utilities.Logger.RemoveDestination(dest);

		}
	}
}
