using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>Renders permitted exact-T entries as single-line text records.</summary>
	/// <typeparam name="T">Unconstrained declared metadata route type.</typeparam>
	/// <remarks>F12-F30: inherits the typed base's guarded direct/captured delivery.
	/// Uses TextBasedDestination's complete rendering and framing rules, adding
	/// " | metadata=" and the FormatValue metadata token immediately after the
	/// massaged-message token and before entryLabels. This field exists even for
	/// null/default(T). Metadata is never an annotation, a scope or a new route.
	/// No new constructor ownership, backend, fallback or format registry follows.</remarks>
	public abstract class GenericTextBasedDestination<T> : Generics.BaseLoggingDestination<T>
	{
		/// <summary>Creates a text destination with a known-bit severity mask.</summary>
		/// <param name="reportingLevel">Any mask from 0 through 63.</param>
		/// <exception cref="ArgumentOutOfRangeException">The mask is negative
		/// or contains an unknown bit; ParamName is reportingLevel.</exception>
		/// <remarks>F29: inherited core validation; zero is active reject-all.
		/// Construction performs no rendering or delivery.</remarks>
		protected GenericTextBasedDestination(LogLevels reportingLevel) : base(reportingLevel)
		{
		}

		/// <summary>Creates a text destination from a case-sensitive mask string.</summary>
		/// <param name="strReportingLevel">A decimal integer or comma-separated
		/// recognized names, using the existing Enum.TryParse whitespace grammar.</param>
		/// <exception cref="ArgumentNullException">The string is null;
		/// ParamName is strReportingLevel.</exception>
		/// <exception cref="ArgumentException">Parsing fails or the mask is
		/// outside 0 through 63; ParamName is strReportingLevel.</exception>
		/// <remarks>F29: inherited core validation; zero is active reject-all,
		/// unnamed known-bit combinations are valid. No output occurs.</remarks>
		protected GenericTextBasedDestination(string strReportingLevel) : base(strReportingLevel)
		{
		}

		/// <summary>Creates a text destination with an integer severity mask.</summary>
		/// <param name="intReportingLevel">Any mask from 0 through 63.</param>
		/// <exception cref="ArgumentOutOfRangeException">The integer is negative
		/// or contains an unknown bit; ParamName is intReportingLevel.</exception>
		/// <remarks>F29: inherited core validation; zero is active reject-all.
		/// Construction performs no rendering or delivery.</remarks>
		protected GenericTextBasedDestination(int intReportingLevel) : base(intReportingLevel)
		{
		}

		/// <summary>Renders and prints one already permitted exact-T entry.</summary>
		/// <param name="context">Complete selected context with this call's UTC time.</param>
		/// <param name="level">Validated, eligible full raw mask, 1 through 63.</param>
		/// <param name="metadata">Original T value/reference, including null/default(T).</param>
		/// <param name="message">Original optional message, including null or empty.</param>
		/// <param name="ex">Original optional exception reference.</param>
		/// <exception cref="Exception">Rendering or printing fails; the invoking
		/// guard handles it as one Output failure.</exception>
		/// <remarks>F12-F24/F26-F30: massage once and render the complete record
		/// before printing once. Do not recapture, recheck, mutate raw values or
		/// classify metadata. FormatValue and PrintLogEntry have the shared
		/// concurrency, escaping, failure and ownership boundaries.</remarks>
		protected override void LogCore(LogContext context, LogLevels level, T metadata, string message, Exception ex)
		{
			var massagedMessage = MassageLogStatement(level, message, ex);
			var record = LogTextRenderer.Render(context, level, massagedMessage, true, metadata, FormatValue);
			PrintLogEntry(record);
		}

		/// <summary>Writes one completely rendered typed record.</summary>
		/// <param name="message">Non-null single-line text without a terminator.</param>
		/// <exception cref="Exception">Recipient output fails; handled by the invoking guard.</exception>
		/// <remarks>F24-F25/F30: once after complete rendering; never on rejection
		/// or render failure. The sink owns termination, concurrency and retention.
		/// Content is unbounded and not redacted; output effects may precede failure.
		/// No retry, disposal or persistence guarantee is added.</remarks>
		protected abstract void PrintLogEntry(string message);
	}
}