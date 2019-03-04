using System;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	public class DestinationTests
	{
		public DestinationTests()
		{
			InitLogger();
		}

		[Theory]
		[InlineData(LogLevels.NoLogging, 1, 1, 1, 1, 1, 1)]
		[InlineData(LogLevels.Error, 0, 1, 1, 1, 1, 1)]
		[InlineData(LogLevels.Warning, 0, 0, 1, 1, 1, 1)]
		[InlineData(LogLevels.Security, 0, 0, 0, 1, 1, 1)]
		[InlineData(LogLevels.Information, 0, 0, 0, 0, 1, 1)]
		[InlineData(LogLevels.Debug, 0, 0, 0, 0, 0, 1)]
		public void LogShouldHaveFallThruCounts(LogLevels level, int no, int err, int warn, int sec, int info, int debug)
		{
			//setup
			InitTest();

			//act
			Utilities.Logger.Log(level, "Hello World", new Exception("Custom Exception"));

			//assert
			AssertCounts(no, err, warn, sec, info, debug);
		}

		[Theory]
		[InlineData(LogLevels.NoLogging)]
		[InlineData(LogLevels.Error)]
		[InlineData(LogLevels.Warning)]
		[InlineData(LogLevels.Security)]
		[InlineData(LogLevels.Information)]
		[InlineData(LogLevels.Debug)]
		public void LogShouldHaveMessage(LogLevels targetLevel)
		{
			//setup
			const string msg = "Hello World!";
			InitTest();

			//act
			var d = new EventDestination(targetLevel);
			d.LoggingEvent += (sender, args) => Assert.Contains(msg, args.Message);
			Utilities.Logger.Log(targetLevel, "Hello World", new Exception("Custom Exception"));

			//assert 
			//it's inside the logging event handler
		}




		public void DestinationsShouldAddAndRemoveAccordingly()
		{
			InitTest();
			Utilities.Logger.Log(LogLevels.NoLogging, "Hello World", new Exception("Custom Exception"));
			AssertCounts(1, 1, 1, 1, 1, 1);


			Utilities.Logger.Log(LogLevels.NoLogging, "Hello World", new Exception("Custom Exception"));
			Utilities.Logger.ClearDestinations();
			AssertCounts(1, 1, 1, 1, 1, 1);


			var dest = new EventDestination(LogLevels.NoLogging);
			dest.LoggingEvent += (sender, args) => _nCount++;


			Utilities.Logger.Log(LogLevels.NoLogging, "Hello World", new Exception("Custom Exception"));
			AssertCounts(1, 1, 1, 1, 1, 1);
		}



		private void InitLogger()
		{
			var nd = new EventDestination(LogLevels.NoLogging);
			nd.LoggingEvent += (sender, args) => _nCount++;

			var ed = new EventDestination(LogLevels.Error);
			ed.LoggingEvent += (sender, args) => _eCount++;

			var wd = new EventDestination(LogLevels.Warning);
			wd.LoggingEvent += (sender, args) => _wCount++;

			var sd = new EventDestination(LogLevels.Security);
			sd.LoggingEvent += (sender, args) => _sCount++;

			var id = new EventDestination(LogLevels.Information);
			id.LoggingEvent += (sender, args) => _iCount++;

			var dd = new EventDestination(LogLevels.Debug);
			dd.LoggingEvent += (sender, args) => _dCount++;
			
			Utilities.Logger.AddDestination(nd);
			Utilities.Logger.AddDestination(ed);
			Utilities.Logger.AddDestination(wd);
			Utilities.Logger.AddDestination(sd);
			Utilities.Logger.AddDestination(id);
			Utilities.Logger.AddDestination(dd);
		}

		private int _nCount = 0;
		private int _eCount = 0;
		private int _wCount = 0;
		private int _sCount = 0;
		private int _iCount = 0;
		private int _dCount = 0;

		private void InitTest()
		{
			_nCount = 0;
			_eCount = 0;
			_wCount = 0;
			_sCount = 0;
			_iCount = 0;
			_dCount = 0;
		}

		private void AssertCounts(int no, int err, int warn, int sec, int info, int debug)
		{
			Assert.Equal(no, _nCount);
			Assert.Equal(err, _eCount);
			Assert.Equal(warn, _wCount);
			Assert.Equal(sec, _sCount);
			Assert.Equal(info, _iCount);
			Assert.Equal(debug, _dCount);
		}
	}
}
