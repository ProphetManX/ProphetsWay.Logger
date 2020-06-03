using Xunit;
using Moq;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.Generics;

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
			mDest.Setup(dest => dest.Log(LogLevels.DebugOnly, msg, null)).Verifiable();
			mDest.Setup(dest => dest.ValidateMessageLevel(LogLevels.DebugOnly)).Returns(true).Verifiable();
			Utilities.Logger.AddDestination(mDest.Object);

			//act
			Utilities.Logger.Debug(msg);

			//assert
			mDest.VerifyAll();

			//cleanup
			Utilities.Logger.RemoveDestination(mDest.Object);
		}

		[Fact]
		public void ShouldTriggerLogMethodOnGenericIDestination()
		{
			//setup
			const string msg = "Hello World!";
			var mDest = new Mock<ILoggingDestination<bool>>();
			mDest.Setup(dest => dest.Log(LogLevels.DebugOnly, true, msg, null)).Verifiable();
			mDest.Setup(dest => dest.ValidateMessageLevel(LogLevels.DebugOnly)).Returns(true).Verifiable();
			Utilities.Logger.AddDestination(mDest.Object);

			//act
			Utilities.Logger.Debug(msg, true);

			//assert
			mDest.VerifyAll();

			//cleanup
			Utilities.Logger.RemoveDestination(mDest.Object);
		}
	}
}
