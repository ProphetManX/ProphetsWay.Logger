using System;
using System.IO;
using System.Text;

namespace ProphetsWay.Utilities.LoggerDestinations
{

	/// <summary>
	/// A basic destination that will write your logs to a File on the hard disk.
	/// </summary>
	public class FileDestination : TextBasedDestination
	{
		private readonly FileInfo _fi;
		private readonly Encoding _encoder;

		/// <summary>
		/// A basic destination that will write your logs to a File on the hard disk.
		/// </summary>
		/// <param name="fileName">The target file for writing the logs to.</param>
		/// <param name="reportingLevel">The level or levels that this destination should write log statements for.</param>
		/// <param name="resetFile">Default: true - will delete the current log file if it exists and recreate it.</param>
		/// <param name="encoder">Default: UTF8 - allows user to specify what encoding pattern to use when writing the output file.</param>
		public FileDestination(string fileName, LogLevels reportingLevel = LogLevels.Debug, bool resetFile = true, EncodingOptions encoder = EncodingOptions.UTF8) 
			: base(reportingLevel)
		{
			_fi = new FileInfo(fileName);
			_encoder = GetEncoding(encoder);

			InitFile(resetFile);
		}

		/// <summary>
		/// A basic destination that will write your logs to a File on the hard disk.
		/// </summary>
		/// <param name="fileName">The target file for writing the logs to.</param>
		/// <param name="strReportingLevel">The string representation of the level or levels that this destination should write log statements for.</param>
		/// <param name="resetFile">Default: true - will delete the current log file if it exists and recreate it.</param>
		/// <param name="encoder">Default: UTF8 - allows user to specify what encoding pattern to use when writing the output file.</param>
		public FileDestination(string fileName, string strReportingLevel, bool resetFile = true, EncodingOptions encoder = EncodingOptions.UTF8)
			: base(strReportingLevel)
		{
			_fi = new FileInfo(fileName);
			_encoder = GetEncoding(encoder);

			InitFile(resetFile);
		}

		/// <summary>
		/// A basic destination that will write your logs to a File on the hard disk.
		/// </summary>
		/// <param name="fileName">The target file for writing the logs to.</param>
		/// <param name="intReportingLevel">The integer representation of the level or levels that this destination should write log statements for.</param>
		/// <param name="resetFile">Default: true - will delete the current log file if it exists and recreate it.</param>
		/// <param name="encoder">Default: UTF8 - allows user to specify what encoding pattern to use when writing the output file.</param>
		public FileDestination(string fileName, int intReportingLevel, bool resetFile = true, EncodingOptions encoder = EncodingOptions.UTF8)
			: base(intReportingLevel)
		{
			_fi = new FileInfo(fileName);
			_encoder = GetEncoding(encoder);

			InitFile(resetFile);
		}

		private Encoding GetEncoding(EncodingOptions encodingOption)
		{
			switch (encodingOption)
			{
				case EncodingOptions.ASCII:
					return Encoding.ASCII;

				case EncodingOptions.BigEndianUnicode:
					return Encoding.BigEndianUnicode;

				case EncodingOptions.UTF32:
					return Encoding.UTF32;

				case EncodingOptions.UTF8:
					return Encoding.UTF8;

				case EncodingOptions.Unicode:
					return Encoding.Unicode;

				default:
					return null;
			}
		}

		private void InitFile(bool resetFile)
		{
			try
			{
				if (_fi.Exists && resetFile)
					_fi.Delete();

				if (!(_fi.Directory?.Exists).GetValueOrDefault())
					_fi.Directory?.Create();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				throw;
			}
		}

		protected override void PrintLogEntry(string message)
		{
			var line = $"{Environment.NewLine}{message}";
			var lineBytes = _encoder.GetBytes(line);

			lock(LoggerLock){
				using(var sr = _fi.Open(FileMode.OpenOrCreate, FileAccess.Write)){
					sr.Position = sr.Length;
					sr.Write(lineBytes, 0, lineBytes.Length);
					sr.Flush();
				}
			}
		}

		public enum EncodingOptions {
			ASCII,
			BigEndianUnicode,
			Unicode,
			UTF8,
			UTF32
		}
	}
}