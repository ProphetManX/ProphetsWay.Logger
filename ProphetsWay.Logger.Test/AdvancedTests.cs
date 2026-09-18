using System;
using System.Collections.Generic;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class AdvancedTests
	{
		[Fact]
		public void ShouldTriggerProperlyWhenError()
		{
			AssertRecipients(2, () => Utilities.Logger.Error(new Exception("Hello World")));
		}

		[Fact]
		public void ShouldTriggerProperlyWhenWarn()
		{
			AssertRecipients(4, () => Utilities.Logger.Warn("Hello World!", new Exception("Goodbye")));
		}

		[Fact]
		public void ShouldTriggerProperlyWhenCritical()
		{
			AssertRecipients(1, () => LoggerTests.Emit("Critical", "Hello World!", new Exception("synthetic detail")));
		}

		[Fact]
		public void ShouldTriggerProperlyWhenTrace()
		{
			AssertRecipients(32, () => LoggerTests.Emit("Trace", "Hello World!", null));
		}

		[Fact]
		public void ShouldTriggerProperlyWhenInfo()
		{
			AssertRecipients(8, () => Utilities.Logger.Info("Hello World!"));
		}

		[Fact]
		public void ShouldTriggerProperlyWhenDebug()
		{
			AssertRecipients(16, () => Utilities.Logger.Debug("Hello World!"));
		}

		private static void AssertRecipients(int messageBit, Action emit)
		{
			var masks = new[] { 0, 1, 2, 4, 8, 16, 32, 3, 7, 15, 31, 63, 9 };
			var received = new Dictionary<int, List<LogLevels>>();
			var destinations = new List<EventDestination>();
			try
			{
				foreach (var mask in masks)
				{
					received.Add(mask, new List<LogLevels>());
					var destination = new EventDestination((LogLevels)mask);
					destination.LoggingEvent += (sender, args) => received[mask].Add(args.LogLevel);
					Utilities.Logger.AddDestination(destination);
					destinations.Add(destination);
				}
				emit();
				foreach (var mask in masks)
					received[mask].ShouldBe((mask & messageBit) == messageBit ? new[] { (LogLevels)messageBit } : new LogLevels[0]);
			}
			finally
			{
				foreach (var destination in destinations) Utilities.Logger.RemoveDestination(destination);
			}
		}
	}
}