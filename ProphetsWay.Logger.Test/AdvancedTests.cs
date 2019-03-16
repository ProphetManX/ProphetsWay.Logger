using System;
using System.Collections.Generic;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class AdvancedTests
	{
		[Theory]
		[InlineData(LogLevels.NoLogging, false, false, false, false, false, false)]
		[InlineData(LogLevels.Error, false, true, true, true, true, true)]
		[InlineData(LogLevels.Warning, false, false, true, true, true, true)]
		[InlineData(LogLevels.Security, false, false, false, true, true, true)]
		[InlineData(LogLevels.Information, false, false, false, false, true, true)]
		[InlineData(LogLevels.Debug, false, false, false, false, false, true)]
		public void ShouldTriggerSevereLevelThruLowerLevels(LogLevels level, bool no, bool err, bool warn, bool sec, bool info, bool debug)
		{
			//setup
			SetupTriggers();

			//act
			Utilities.Logger.Log(level, "Hello World", new Exception("Custom Exception"));

			//assert
			AssertTriggers(no, err, warn, sec, info, debug);

			//cleanup
			CleanupTriggers();
		}


		private readonly Dictionary<LogLevels, bool> _triggers = new Dictionary<LogLevels, bool>();
		private readonly Dictionary<LogLevels, BaseLoggingDestination> _destinations = new Dictionary<LogLevels, BaseLoggingDestination>();

		private void SetupTriggers()
		{
			foreach (LogLevels num in Enum.GetValues(typeof(LogLevels)))
			{
				_triggers.Add(num, false);
				var ev = new EventDestination(num);
				ev.LoggingEvent += (sender, args) => _triggers[num] = true;
				Utilities.Logger.AddDestination(ev);
				_destinations.Add(num, ev);
			}
		}

		private void CleanupTriggers()
		{
			_triggers.Clear();

			foreach (var dest in _destinations.Values)
				Utilities.Logger.RemoveDestination(dest);

			_destinations.Clear();
		}

		private void AssertTriggers(bool no, bool err, bool warn, bool sec, bool info, bool debug)
		{
			AssertTrigger(no, LogLevels.NoLogging);
			AssertTrigger(err, LogLevels.Error);
			AssertTrigger(warn, LogLevels.Warning);
			AssertTrigger(sec, LogLevels.Security);
			AssertTrigger(info, LogLevels.Information);
			AssertTrigger(debug, LogLevels.Debug);
		}

		private void AssertTrigger(bool expected, LogLevels checkLevel)
		{
			var because = $"{checkLevel} should have reported as '{expected}'";
			_triggers[checkLevel].Should().Be(expected, because);
		}

	}
}
