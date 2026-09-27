using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>Renders permitted ordinary entries as single-line text records.</summary>
	/// <remarks>F15-F26/F29-F30: use the selected context's EventTimestampUtc,
	/// invariant O format, then " :: ", the full severity's general spelling
	/// padded left to 12, ":  ", and the massaged-message token. Append
	/// " | entryLabels=" with the entry attachment and " | scopes=" with every
	/// captured frame outermost first. Null attachments are null; present label
	/// attachments are bracketed ordered identifier tokens, including duplicates.
	/// Each scope is {labels=attachment,properties=[(key,value),...]}; preserve
	/// empty frames, pair order, repeated/null/empty keys and original value slots.
	/// Arrays/pairs use comma separators without added spaces. Enumerate completed
	/// scope/label membership, not arbitrary T or nested property-value objects.
	/// Values use FormatValue; keys and identifiers use their supplied strings.
	/// A component token is bare null for absent text,
	/// otherwise a double-quoted escaped string. Escape backslash as \\, quotation
	/// mark as \", CR as \r, LF as \n, TAB as \t; other Char.IsControl units and
	/// U+2028/U+2029 use \uXXXX with four uppercase hex digits. Preserve every other
	/// UTF-16 unit. Do not reinterpret pre-existing escape text or truncate content.
	/// Compose the entire record before a single PrintLogEntry call, without a
	/// line terminator. This is framing, not redaction or authorization. Custom
	/// implementations of output or replacement hooks remain consumer code; these
	/// supplied entrypoints are not a sandbox. All inherited guards, independent
	/// attempt/reporting rules, concurrency and borrowing limits remain.</remarks>
	public abstract class TextBasedDestination : BaseLoggingDestination
	{
		/// <summary>Renders and prints one already permitted ordinary entry.</summary>
		/// <param name="context">The complete selected context with this call's UTC time.</param>
		/// <param name="level">The validated, eligible full raw mask, 1 through 63.</param>
		/// <param name="message">Original optional message, including null or empty.</param>
		/// <param name="ex">Original optional exception; absent remains absent.</param>
		/// <exception cref="Exception">Massage, value rendering, composition or print
		/// fails; the enclosing guarded boundary handles it as one Output failure.</exception>
		/// <remarks>F15-F24/F26-F30: call MassageLogStatement once, apply the type's
		/// record rules and call PrintLogEntry once only after full rendering succeeds.
		/// Do not recapture scopes/time or run another eligibility check. The default
		/// massage preserves nested exception messages/available stacks for every valid
		/// mask; text escaping preserves that content. A failed print can already have
		/// effects; no rollback, retry or later-completion guarantee follows.</remarks>
		protected override void LogCore(LogContext context, LogLevels level, string message, Exception ex)
		{
			var massagedMessage = MassageLogStatement(level, message, ex);
			var record = LogTextRenderer.Render(context, level, massagedMessage, false, null, FormatValue);
			PrintLogEntry(record);
		}

		/// <summary>Creates a text destination with a known-bit severity mask.</summary>
		/// <param name="reportingLevel">Any mask from 0 through 63.</param>
		/// <exception cref="ArgumentOutOfRangeException">The mask is negative
		/// or contains an unknown bit; ParamName is reportingLevel.</exception>
		/// <remarks>F29: inherited core validation; zero is active reject-all.
		/// Construction performs no rendering or delivery.</remarks>
		protected TextBasedDestination(LogLevels reportingLevel) : base(reportingLevel) { }

		/// <summary>Creates a text destination from a case-sensitive mask string.</summary>
		/// <param name="strReportingLevel">A decimal integer or comma-separated
		/// recognized names, using the existing Enum.TryParse whitespace grammar.</param>
		/// <exception cref="ArgumentNullException">The string is null;
		/// ParamName is strReportingLevel.</exception>
		/// <exception cref="ArgumentException">Parsing fails or the mask is
		/// outside 0 through 63; ParamName is strReportingLevel.</exception>
		/// <remarks>F29: inherited core validation; zero is active reject-all,
		/// unnamed known-bit combinations are valid. No output occurs.</remarks>
		protected TextBasedDestination(string strReportingLevel) : base(strReportingLevel) { }

		/// <summary>Creates a text destination with an integer severity mask.</summary>
		/// <param name="intReportingLevel">Any mask from 0 through 63.</param>
		/// <exception cref="ArgumentOutOfRangeException">The integer is negative
		/// or contains an unknown bit; ParamName is intReportingLevel.</exception>
		/// <remarks>F29: inherited core validation; zero is active reject-all.
		/// Construction performs no rendering or delivery.</remarks>
		protected TextBasedDestination(int intReportingLevel) : base(intReportingLevel) { }

		/// <summary>Writes one completely rendered record to the recipient.</summary>
		/// <param name="message">Non-null single-line record text without a terminator.</param>
		/// <exception cref="Exception">Recipient output fails; handled by the invoking guard.</exception>
		/// <remarks>F24-F25/F30: called once after complete rendering and never for a
		/// rejected entry or failed rendering. The sink supplies any record terminator.
		/// Input is unbounded intended log content, not a safe diagnostic or redacted
		/// response. Effects may precede failure; synchronous invocation proves no
		/// persistence. Implementations own their concurrency and retention behavior.</remarks>
		protected abstract void PrintLogEntry(string message);
	}
}