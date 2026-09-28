using System;
using System.IO;
using System.Text;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>Appends permitted native text records to one explicitly selected file path.</summary>
	/// <remarks>
	/// <para>E01-E08/E12/E26: each constructor selects its full path once using
	/// the executing framework's FileInfo path semantics. Relative paths use
	/// construction-time Environment.CurrentDirectory. Later current-directory
	/// changes do not redirect this instance. This selects a pathname, not a
	/// persistent physical-file identity or a filesystem sandbox.</para>
	/// <para>All required argument validation completes before deletion or
	/// directory creation; no precedence between invalid arguments is promised.
	/// Construction prepares missing parent directories and, only when resetFile
	/// is true, deletes an existing selected file before returning. A missing
	/// log file is not created by construction: permitted output creates it.
	/// Construction does not register the destination, log an entry or select
	/// an automatic session. The default resetFile value is false.</para>
	/// <para>E09-E11: constructors retain the five EncodingOptions choices.
	/// Undefined encoder values are rejected before filesystem effects. Local
	/// path/preparation errors are not dispatch attempts and emit neither a
	/// DispatchFailed notification nor console output. The documented ordinary
	/// argument mappings remain applicable. Ordinary filesystem/access failures
	/// outside those mappings propagate as their original framework exceptions;
	/// do not normalize them to IOException or strip their diagnostic contents.
	/// Local messages, causes, Data and CLR diagnostics follow their existing
	/// local/framework contracts and are not promised path-free or sanitized.
	/// No fixed wording, null InnerException or empty Data guarantee is added.
	/// The dispatch-error contract remains separate.</para>
	/// <para>E13-E16: every permitted write appends to the selected file if it
	/// exists or creates a missing file at that same path. Existing bytes are
	/// not inspected, rewritten, transcoded or given a corrective separator.
	/// In particular, an existing unterminated prefix is followed immediately
	/// by the appended record. No content/binary or encoding-compatibility check
	/// is supplied; existing-content suitability is the developer's responsibility.
	/// TextBasedDestination still owns the complete native record grammar,
	/// escaping, exception fidelity and captured event time.</para>
	/// <para>E17-E20/E23-E26: physical writes through this instance are
	/// synchronous and serialized through its inherited LoggerLock. Each write
	/// releases its file handle before completion. Rendering and custom hooks
	/// keep their inherited concurrency contract; no ordering among concurrent
	/// callers or coordination with other destination instances/processes is
	/// promised. Repeated permitted calls append repeated records. Supplied
	/// entrypoints perform their inherited eligibility checks before rendering
	/// or writing; constructor preparation is separate from that per-entry rule.
	/// Registered instances remain borrowed. No retry, replay, copying,
	/// relocation, automatic fallback, new reset API, background completion,
	/// global flush or crash-safe/exactly-once/durability guarantee is added.</para>
	/// <para>E21-E22: an output failure through the supplied public entrypoints
	/// is an Output failure handled by their existing guarded boundary. Direct
	/// calls report their one-recipient failure and throw LogDispatchException;
	/// Logger attempts independent eligible recipients and safely reports before
	/// propagating. This sink does not emit a second report. Reporting failures
	/// do not replace or suppress the original result. Possible partial effects
	/// do not authorize replay or relocation.</para>
	/// </remarks>
	public class FileDestination : TextBasedDestination
	{
		private readonly FileInfo _fi;
		private readonly Encoding _encoder;

		/// <summary>Prepares an explicit file destination with a severity mask.</summary>
		/// <param name="fileName">Required filesystem path, selected once at construction.</param>
		/// <param name="reportingLevel">Known-bit mask 0 through 63; default Debug.</param>
		/// <param name="resetFile">False by default; true explicitly requests construction-time deletion of an existing selected file.</param>
		/// <param name="encoder">A declared EncodingOptions value; default UTF8.</param>
		/// <exception cref="ArgumentNullException">fileName is null; ParamName is fileName.</exception>
		/// <exception cref="ArgumentException">fileName is empty or rejected by the framework's path syntax rules; ParamName is fileName.</exception>
		/// <exception cref="ArgumentOutOfRangeException">reportingLevel is negative or has unknown bits, or encoder is undefined; ParamName identifies that argument.</exception>
		/// <exception cref="IOException">An ordinary filesystem I/O failure prevents path preparation or the requested reset; the original framework exception propagates.</exception>
		/// <exception cref="UnauthorizedAccessException">The executing framework denies filesystem access during path preparation or the requested reset; the original framework exception propagates.</exception>
		/// <exception cref="System.Security.SecurityException">The executing framework denies a required security permission during path preparation or the requested reset; the original framework exception propagates.</exception>
		/// <remarks>E02/E04-E12/E26: the type's common construction contract applies.
		/// Zero is valid active reject-all; unnamed known-bit combinations are
		/// valid. Argument validation precedes filesystem effects, without an
		/// invalid-argument precedence guarantee. No entry is rendered or written.</remarks>
		public FileDestination(string fileName, LogLevels reportingLevel = LogLevels.Debug, bool resetFile = false, EncodingOptions encoder = EncodingOptions.UTF8)
			: base(reportingLevel)
		{
			_fi = GetFileInfo(fileName);
			_encoder = GetEncoding(encoder);

			InitFile(resetFile);
		}

		/// <summary>Prepares an explicit file destination from a case-sensitive severity-mask string.</summary>
		/// <param name="fileName">Required filesystem path, selected once at construction.</param>
		/// <param name="strReportingLevel">Decimal integer or comma-separated recognized severity names, using the existing case-sensitive Enum.TryParse whitespace grammar.</param>
		/// <param name="resetFile">False by default; true explicitly requests construction-time deletion of an existing selected file.</param>
		/// <param name="encoder">A declared EncodingOptions value; default UTF8.</param>
		/// <exception cref="ArgumentNullException">fileName or strReportingLevel is null; ParamName identifies that argument.</exception>
		/// <exception cref="ArgumentException">fileName is empty or rejected by the framework's path syntax rules, or strReportingLevel cannot be parsed into 0 through 63; ParamName identifies that argument.</exception>
		/// <exception cref="ArgumentOutOfRangeException">encoder is undefined; ParamName is encoder.</exception>
		/// <exception cref="IOException">An ordinary filesystem I/O failure prevents path preparation or the requested reset; the original framework exception propagates.</exception>
		/// <exception cref="UnauthorizedAccessException">The executing framework denies filesystem access during path preparation or the requested reset; the original framework exception propagates.</exception>
		/// <exception cref="System.Security.SecurityException">The executing framework denies a required security permission during path preparation or the requested reset; the original framework exception propagates.</exception>
		/// <remarks>E03-E12/E26: the type's common construction contract applies.
		/// Zero is valid active reject-all; unnamed known-bit combinations are
		/// valid. Malformed text is not replaced with Information. Argument
		/// validation precedes filesystem effects without an invalid-argument
		/// precedence guarantee. No entry is rendered or written.</remarks>
		public FileDestination(string fileName, string strReportingLevel, bool resetFile = false, EncodingOptions encoder = EncodingOptions.UTF8)
			: base(strReportingLevel)
		{
			_fi = GetFileInfo(fileName);
			_encoder = GetEncoding(encoder);

			InitFile(resetFile);
		}

		/// <summary>Prepares an explicit file destination with an integer severity mask.</summary>
		/// <param name="fileName">Required filesystem path, selected once at construction.</param>
		/// <param name="intReportingLevel">Known-bit mask 0 through 63; zero is active reject-all.</param>
		/// <param name="resetFile">False by default; true explicitly requests construction-time deletion of an existing selected file.</param>
		/// <param name="encoder">A declared EncodingOptions value; default UTF8.</param>
		/// <exception cref="ArgumentNullException">fileName is null; ParamName is fileName.</exception>
		/// <exception cref="ArgumentException">fileName is empty or rejected by the framework's path syntax rules; ParamName is fileName.</exception>
		/// <exception cref="ArgumentOutOfRangeException">intReportingLevel is negative or has unknown bits, or encoder is undefined; ParamName identifies that argument.</exception>
		/// <exception cref="IOException">An ordinary filesystem I/O failure prevents path preparation or the requested reset; the original framework exception propagates.</exception>
		/// <exception cref="UnauthorizedAccessException">The executing framework denies filesystem access during path preparation or the requested reset; the original framework exception propagates.</exception>
		/// <exception cref="System.Security.SecurityException">The executing framework denies a required security permission during path preparation or the requested reset; the original framework exception propagates.</exception>
		/// <remarks>E02/E04-E12/E26: the type's common construction contract applies.
		/// No named enum constant is required. Argument validation precedes
		/// filesystem effects without an invalid-argument precedence guarantee.
		/// No entry is rendered or written.</remarks>
		public FileDestination(string fileName, int intReportingLevel, bool resetFile = false, EncodingOptions encoder = EncodingOptions.UTF8)
			: base(intReportingLevel)
		{
			_fi = GetFileInfo(fileName);
			_encoder = GetEncoding(encoder);

			InitFile(resetFile);
		}

		private static FileInfo GetFileInfo(string fileName)
		{
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));

			try
			{
				return new FileInfo(fileName);
			}
			catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException)
			{
				throw new ArgumentException(exception.Message, nameof(fileName), exception);
			}
		}

		private static Encoding GetEncoding(EncodingOptions encoder)
		{
			switch (encoder)
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
					throw new ArgumentOutOfRangeException(nameof(encoder));
			}
		}

		private void InitFile(bool resetFile)
		{
			if (resetFile && _fi.Exists)
				_fi.Delete();

			var directory = _fi.Directory;
			if (directory != null && !directory.Exists)
				directory.Create();
		}

		/// <summary>Appends one completed native record and its physical terminator.</summary>
		/// <param name="message">The non-null single-line record supplied by TextBasedDestination, without a terminator.</param>
		/// <exception cref="Exception">Encoding, opening, writing, flushing or closing fails; the invoking supplied guard treats it as one Output failure.</exception>
		/// <remarks>E13-E25: append exactly the selected Encoding.GetBytes result
		/// for message followed by one Environment.NewLine. Add no leading newline,
		/// extra blank line or encoding preamble. Encoding replacement behavior is
		/// that of the selected BCL encoding on the executing runtime; this does not
		/// promise arbitrary Unicode round-tripping through ASCII or other encodings.
		/// Do not reformat, re-escape or recapture time/context. Open/create and append
		/// at the same selected pathname, releasing the handle before completion.
		/// Apply the type's same-instance serialization and guarded failure rules.
		/// The protected input precondition comes from TextBasedDestination; arbitrary
		/// consumer calls into exposed/overridden hooks are outside supplied-entrypoint
		/// guarantees. No synchronous return proves external persistence.</remarks>
		protected override void PrintLogEntry(string message)
		{
			var lineBytes = _encoder.GetBytes(message + Environment.NewLine);

			lock (LoggerLock)
			{
				using (var stream = _fi.Open(FileMode.OpenOrCreate, FileAccess.Write))
				{
					stream.Position = stream.Length;
					stream.Write(lineBytes, 0, lineBytes.Length);
					stream.Flush();
				}
			}
		}

		/// <summary>Selects the existing BCL encoding used for appended record bytes.</summary>
		/// <remarks>E09-E10/E14: this is a choice enum, not a flags enum. Only the
		/// five declared values are valid. The optional constructor default is
		/// UTF8, whereas default(EncodingOptions) is ASCII. Selecting an encoding
		/// does not inspect or convert the file's existing bytes or add a preamble.</remarks>
		public enum EncodingOptions
		{
			/// <summary>Uses System.Text.Encoding.ASCII.</summary>
			ASCII = 0,
			/// <summary>Uses System.Text.Encoding.BigEndianUnicode.</summary>
			BigEndianUnicode = 1,
			/// <summary>Uses System.Text.Encoding.Unicode.</summary>
			Unicode = 2,
			/// <summary>Uses System.Text.Encoding.UTF8.</summary>
			UTF8 = 3,
			/// <summary>Uses System.Text.Encoding.UTF32.</summary>
			UTF32 = 4
		}
	}
}