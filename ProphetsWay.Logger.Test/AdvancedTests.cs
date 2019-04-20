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
		[Fact]
		public void ShouldTriggerProperlyWhenError()
		{
			//setup
			SetupTriggers();

			//act
			Utilities.Logger.Error(new Exception("Hello World"));

			//assert
			AssertTriggers(true, true, true, true, true, false, false, false, false);

			//cleanup
			CleanupTriggers();
		}

		[Fact]
		public void ShouldTriggerProperlyWhenWarn()
		{
			//setup
			SetupTriggers();

			//act
			Utilities.Logger.Warn("Hello World!", new Exception("Goodbye"));

			//assert
			AssertTriggers(false, true, true, true, true, true, false, false, false);

			//cleanup
			CleanupTriggers();
		}

		[Fact]
		public void ShouldTriggerProperlyWhenSecurity()
		{
			//setup
			SetupTriggers();

			//act
			Utilities.Logger.Security("Hello World!");

			//assert
			AssertTriggers(false, false, true, true, true, false, true, false, false);

			//cleanup
			CleanupTriggers();
		}

		[Fact]
		public void ShouldTriggerProperlyWhenInfo()
		{
			//setup
			SetupTriggers();

			//act
			Utilities.Logger.Info("Hello World!");

			//assert
			AssertTriggers(false, false, false, true, true, false, false, true, false);

			//cleanup
			CleanupTriggers();
		}

		[Fact]
		public void ShouldTriggerProperlyWhenDebug()
		{
			//setup
			SetupTriggers();

			//act
			Utilities.Logger.Debug("Hello World!");

			//assert
			AssertTriggers(false, false, false, false, true, false, false, false, true);

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

        private void AssertTriggers(bool err, bool warn, bool sec, bool info, bool debug, bool oWarn, bool oSec, bool oInfo, bool oDebug)
        {
            AssertTrigger(err, LogLevels.Error);
            AssertTrigger(warn, LogLevels.Warning);
            AssertTrigger(sec, LogLevels.Security);
            AssertTrigger(info, LogLevels.Information);
            AssertTrigger(debug, LogLevels.Debug);
            AssertTrigger(oWarn, LogLevels.WarningOnly);
            AssertTrigger(oSec, LogLevels.SecurityOnly);
            AssertTrigger(oInfo, LogLevels.InformationOnly);
            AssertTrigger(oDebug, LogLevels.DebugOnly);
        }

        private void AssertTrigger(bool expected, LogLevels checkLevel)
        {
            var because = $"{checkLevel} should have reported as '{expected}'";
            _triggers[checkLevel].Should().Be(expected, because);
        }

    }
}