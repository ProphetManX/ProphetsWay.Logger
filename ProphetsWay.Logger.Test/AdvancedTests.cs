using System;
using System.Collections.Generic;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class AdvancedTests
	{
		public AdvancedTests()
		{
			InitAdvanceTests();
		}

		[Theory]
		[InlineData(LogLevels.NoLogging, true, true, true, true, true, true)]
		[InlineData(LogLevels.Error, false, true, true, true, true, true)]
		[InlineData(LogLevels.Warning, false, false, true, true, true, true)]
		[InlineData(LogLevels.Security, false, false, false, true, true, true)]
		[InlineData(LogLevels.Information, false, false, false, false, true, true)]
		[InlineData(LogLevels.Debug, false, false, false, false, false, true)]
		public void ShouldTriggerSevereLevelThruLowerLevels(LogLevels level, bool no, bool err, bool warn, bool sec, bool info, bool debug)
		{
			//setup
			ResetTriggers();

			//act
			Utilities.Logger.Log(level, "Hello World", new Exception("Custom Exception"));

			//assert
			AssertTriggers(no, err, warn, sec, info, debug);
		}


		private readonly Dictionary<LogLevels, bool> _triggers = new Dictionary<LogLevels, bool>();

		private void InitAdvanceTests()
		{
			foreach (LogLevels num in Enum.GetValues(typeof(LogLevels)))
			{
				_triggers.Add(num, false);
				var ev = new EventDestination(num);
				ev.LoggingEvent += (sender, args) => _triggers[num] = true;
				Utilities.Logger.AddDestination(ev);
			}
		}

		private void ResetTriggers()
		{
			foreach (LogLevels num in Enum.GetValues(typeof(LogLevels)))
				_triggers[num] = false;
		}

		private void AssertTriggers(bool no, bool err, bool warn, bool sec, bool info, bool debug)
		{
			Assert.Equal(no, _triggers[LogLevels.NoLogging]);
			Assert.Equal(err, _triggers[LogLevels.Error]);
			Assert.Equal(warn, _triggers[LogLevels.Warning]);
			Assert.Equal(sec, _triggers[LogLevels.Security]);
			Assert.Equal(info, _triggers[LogLevels.Information]);
			Assert.Equal(debug, _triggers[LogLevels.Debug]);
		}

	}
}
