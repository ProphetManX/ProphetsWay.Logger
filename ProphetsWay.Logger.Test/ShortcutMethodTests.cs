using System;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class ShortcutMethodTests
	{
		[Fact]
		public void ShouldTriggerDebugOnDebug()
		{
			var triggered = false;
			var dest = new EventDestination(LogLevels.Debug);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);

			//act
			Utilities.Logger.Debug("Hello World!");

			//assert
			Assert.True(triggered);
		}

		[Fact]
		public void ShouldTriggerInfoOnInfo()
		{
			var triggered = false;
			var dest = new EventDestination(LogLevels.Information);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);

			//act
			Utilities.Logger.Info("Hello World!");

			//assert
			Assert.True(triggered);
		}

		[Fact]
		public void ShouldTriggerSecurityOnSecurity()
		{
			var triggered = false;
			var dest = new EventDestination(LogLevels.Security);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);

			//act
			Utilities.Logger.Security("Hello World!");

			//assert
			Assert.True(triggered);
		}

		[Fact]
		public void ShouldTriggerWarnOnWarn()
		{
			var triggered = false;
			var dest = new EventDestination(LogLevels.Warning);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);

			//act
			Utilities.Logger.Warn("Hello World!");

			//assert
			Assert.True(triggered);
		}

		[Fact]
		public void ShouldTriggerErrorOnError()
		{
			var triggered = false;
			var dest = new EventDestination(LogLevels.Error);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);

			//act
			Utilities.Logger.Error(new Exception("Hello World!"));

			//assert
			Assert.True(triggered);
		}
	}
}
