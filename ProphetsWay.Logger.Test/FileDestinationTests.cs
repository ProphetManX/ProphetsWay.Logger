using System;
using System.IO;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class FileDestinationTests
	{
		private readonly string _fileName = $"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}FileDestinationTest.log";

		private BaseLoggingDestination InitFileDestination(bool resetFile = true)
		{
			var dest = new FileDestination(_fileName, LogLevels.Debug, resetFile);
			Utilities.Logger.AddDestination(dest);
			return dest;
		}

		private FileInfo InitTestLogFileTarget()
		{
			var fi = new FileInfo(_fileName);
			if (fi.Exists)
				fi.Delete();

			return fi;
		}

		[Fact]
		public void ShouldCreateNewFileForLogging()
		{
			//setup
			var fi = InitTestLogFileTarget();
			var dest = InitFileDestination();

			//act
			Utilities.Logger.Debug("Hello World!");
			fi.Refresh();

			//assert
			fi.Exists.Should().BeTrue();

			//cleanup
			fi.Refresh();
			if(fi.Exists)
				fi.Delete();

			Utilities.Logger.RemoveDestination(dest);
		}

		[Fact]
		public void ShouldWriteMessageToFile()
		{
			//setup
			const string msg = "Hello World!";
			string contents;

			var fi = InitTestLogFileTarget();
			var dest = InitFileDestination();

			//act
			Utilities.Logger.Debug(msg);
			using (var tr = fi.OpenText())
				contents = tr.ReadToEnd();

			//assert
			contents.Should().Contain(msg);

			//cleanup
			fi.Refresh();
			if (fi.Exists)
				fi.Delete();

			Utilities.Logger.RemoveDestination(dest);
		}

		[Fact]
		public void ShouldDeleteExistingLogFile()
		{
			//setup
			const string initialMsg = "This is a default Text File.";
			const string msg = "Hello World!";
			string contents;

			var fi = InitTestLogFileTarget();
			using (var tw = fi.CreateText())
			{
				tw.Write(initialMsg);
				tw.Flush();
				tw.Close();
			}

			var dest = InitFileDestination();

			//act
			Utilities.Logger.Debug(msg);
			using (var tr = fi.OpenText())
				contents = tr.ReadToEnd();

			//assert
			contents.Should().NotContain(initialMsg).And.Contain(msg);

			//cleanup
			fi.Refresh();
			if (fi.Exists)
				fi.Delete();

			Utilities.Logger.RemoveDestination(dest);
		}

		[Fact]
		public void ShouldAppendToExistingLogFile()
		{
			//setup
			const string initialMsg = "This is a default Text File.";
			const string msg = "Hello World!";
			string contents;

			var fi = InitTestLogFileTarget();
			using (var tw = fi.CreateText())
			{
				tw.Write(initialMsg);
				tw.Flush();
				tw.Close();
			}

			var dest = InitFileDestination(false);

			//act
			Utilities.Logger.Debug(msg);
			using (var tr = fi.OpenText())
				contents = tr.ReadToEnd();

			//assert
			contents.Should().Contain(initialMsg).And.Contain(msg);

			//cleanup
			fi.Refresh();
			if (fi.Exists)
				fi.Delete();

			Utilities.Logger.RemoveDestination(dest);
		}

	}
}