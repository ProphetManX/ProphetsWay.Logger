using Xunit;
using Moq;
using ProphetsWay.Utilities;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class ILoggingDestinationTests
	{

		[Fact]
		public void ShouldTriggerLogMethodOnIDestination()
		{
			//setup
			const string msg = "Hello World!";
			var mDest = new Mock<ILoggingDestination>();
			mDest.Setup(dest => dest.Log(LogLevels.Debug, msg, null)).Verifiable();
			Utilities.Logger.AddDestination(mDest.Object);

			//act
			Utilities.Logger.Debug(msg);

			//assert
			mDest.VerifyAll();

			//cleanup
			Utilities.Logger.RemoveDestination(mDest.Object);

		}
	}
}
