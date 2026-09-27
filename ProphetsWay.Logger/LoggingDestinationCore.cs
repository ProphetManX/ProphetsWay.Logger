using System;
using System.Threading;

namespace ProphetsWay.Utilities
{
	/// <summary>
	/// Core functionality that all Logging destinations will need to use, 
	/// manages log level comparison in a single location,
	/// includes a LoggerLock for use if needed in any inheriting destination.
	/// </summary>
	public abstract class LoggingDestinationCore : IDestination
	{
		/// <summary>Creates a destination with the supplied severity mask.</summary>
		/// <param name="reportingLevel">Any combination of known severity bits, from 0 through 63.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="reportingLevel"/> contains an unknown bit or is negative.</exception>
		/// <remarks>Zero is an active reject-all mask. Unnamed known-bit combinations are valid.</remarks>
		public LoggingDestinationCore(LogLevels reportingLevel)
		{
			if ((reportingLevel & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(reportingLevel));

			_reportingLevel = reportingLevel;
		}

		/// <summary>Creates a destination by parsing a case-sensitive severity mask.</summary>
		/// <param name="strReportingLevel">A decimal integer or comma-separated recognized severity names.</param>
		/// <exception cref="ArgumentNullException"><paramref name="strReportingLevel"/> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="strReportingLevel"/> cannot be parsed or represents a mask outside 0 through 63.</exception>
		/// <remarks>Uses the case-sensitive Enum.TryParse grammar, including its whitespace handling. Zero rejects all messages; unnamed known-bit combinations are valid.</remarks>
		public LoggingDestinationCore(string strReportingLevel)
		{
			_reportingLevel = ParseReportingLevel(strReportingLevel);
		}

		/// <summary>Creates a destination with the supplied integer severity mask.</summary>
		/// <param name="intReportingLevel">Any combination of known severity bits, from 0 through 63.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="intReportingLevel"/> contains an unknown bit or is negative.</exception>
		/// <remarks>Zero is an active reject-all mask. No named enum constant is required.</remarks>
		public LoggingDestinationCore(int intReportingLevel)
		{
			if ((intReportingLevel & ~(int)LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(intReportingLevel));

			_reportingLevel = (LogLevels)intReportingLevel;
		}

		private LogLevels ParseReportingLevel(string strReportingLevel)
		{
			if (strReportingLevel == null)
				throw new ArgumentNullException(nameof(strReportingLevel));

			if (!Enum.TryParse(strReportingLevel, out LogLevels reportingLevel) || (reportingLevel & ~LogLevels.Trace) != 0)
				throw new ArgumentException("The reporting level must be a mask of known severity bits.", nameof(strReportingLevel));

			return reportingLevel;
		}

		/// <summary>
		/// A lock object for use in making threadsafe destinations
		/// </summary>
		protected readonly object LoggerLock = new object();

		private readonly LogLevels _reportingLevel;
		private DestinationLabelPolicy _labelPolicy = new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]);

		/// <summary>Gets or replaces this supplied destination's whole-entry label restriction.</summary>
		/// <value>A non-null immutable DestinationLabelPolicy; initially NoFilter with empty configured membership.</value>
		/// <exception cref="ArgumentNullException">The assigned value is null; ParamName is "value"; the old policy remains.</exception>
		/// <remarks>B05: nonvirtual configuration, available after every existing constructor.
		/// Replace the complete reference atomically; return after publication without delivery,
		/// notification, disposal or draining. Successful replacement preserves the supplied policy's
		/// immutable mode/membership; object identity is unspecified. NoFilter adds no restriction;
		/// empty AllowOnly rejects all, without fallback. Capture one complete value per direct call.
		/// Logger captures supplied recipients' policy values before any recipient callback; changes
		/// thereafter affect later captures only. This is an additional restriction, never a replacement
		/// for registration settings or the destination's severity mask. It changes no registry setting.
		/// Concurrent read/replacement is supported, not a global transaction across destinations or
		/// a thread-safety guarantee for recipient state. Reassigning the same value causes no delivery.</remarks>
		public DestinationLabelPolicy LabelPolicy
		{
			get
			{
				return Volatile.Read(ref _labelPolicy);
			}
			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));

				Volatile.Write(ref _labelPolicy, value);
			}
		}

		/// <summary>Formats one metadata or scope-property value for a text recipient.</summary>
		/// <param name="value">The original value, including null; never a deep clone.</param>
		/// <returns>Unescaped text, or null for a null text token. The default returns
		/// null for null and otherwise the supported scalar text or unsupported marker.</returns>
		/// <exception cref="Exception">An explicit override can fail; the invoking
		/// guarded delivery treats that failure as Output, not capture or LabelCheck.</exception>
		/// <remarks>F06-F14/F26-F30: default string/char content is unchanged; Boolean
		/// uses True/False. SByte, Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64 and
		/// the numeric values of IntPtr/UIntPtr use invariant D; Single/Double use R,
		/// Decimal G, Guid D, DateTime/DateTimeOffset O and TimeSpan c, all invariant.
		/// Payload time kind/offset is preserved, not changed to event UTC. Enums use
		/// general G names/flags or invariant underlying decimal; alias choice follows
		/// the BCL. Nullable boxing follows the underlying value or null. BCL spellings
		/// are those of the executing runtime, not a cross-runtime formatting promise.
		/// Other values yield [no formatter: TypeName] using runtime Type.Name only.
		/// Default formatting performs no arbitrary getters, object ToString,
		/// IFormattable dispatch, equality, hashing, enumeration or object-graph walk.
		/// An explicit override may extend rendering and call this base implementation.
		/// Successful text rendering calls this hook once per metadata/property-value
		/// occurrence after eligibility; a failed recipient may stop formatting values.
		/// Keys, labels and message/exception text do not pass through it.
		/// Order across value calls is unspecified. Its result,
		/// including an unsupported marker, is escaped as data by the text renderer.
		/// A null override result is a null token, not a thrown failure or fallback.
		/// There is no cross-recipient formatted-value cache. Overrides may be called
		/// concurrently; their effects/retained data are consumer-owned. This hook adds
		/// no disposal, lock, registry, async work, cancellation or retry.</remarks>
		protected virtual string FormatValue(object value)
		{
			return LogTextRenderer.FormatValue(value);
		}

		/// <summary>
		/// Combines the supplied context with exception messages and available stack traces.
		/// </summary>
		/// <param name="level">The validated, eligible message mask.</param>
		/// <param name="message">Optional context, preserved without trimming or substitution.</param>
		/// <param name="ex">Optional exception whose nested messages and available stacks are included.</param>
		/// <returns>The original message, including null, when no exception is supplied; otherwise context and exception detail.</returns>
		/// <remarks>Applies to every valid mask. This hook neither dispatches nor prints and does not add an independent mask guard.
		/// <para>F15-F16: this remains unframed message/exception text. Supplied text
		/// delivery applies single-line escaping after this hook; event delivery keeps
		/// its existing massaged/raw distinction. This hook does not render scopes,
		/// typed metadata, Exception.Data or arbitrary diagnostic properties.</para></remarks>
		protected virtual string MassageLogStatement(LogLevels level, string message = null, Exception ex = null)
		{
			if (ex == null)
				return message;

			ExceptionDetailer(ex, out string exceptionMessage, out string exceptionStackTrace);
			return $"{message}{Environment.NewLine}{exceptionMessage}{Environment.NewLine}{exceptionStackTrace}";
		}

		/// <summary>
		/// Recursive function to peer deep into an Exception and its Inner Exceptions to capture the messages and stack traces within.
		/// </summary>
		private static void ExceptionDetailer(Exception ex, out string message, out string stack)
		{
			var imessage = string.Empty;
			var istack = string.Empty;

			if (ex.InnerException != null)
				ExceptionDetailer(ex.InnerException, out imessage, out istack);

			message = string.IsNullOrEmpty(imessage)
				? ex.Message
				: string.Format("{0}{1}{1}Inner Exception Message:{1}{2}", ex.Message, Environment.NewLine, imessage);

			stack = string.IsNullOrEmpty(istack)
				? ex.StackTrace
				: string.Format("{0}{1}{1}Inner Exception Stack Trace:{1}{2}", ex.StackTrace, Environment.NewLine, istack);
		}


		/// <summary>
		/// Tests whether the destination includes every requested message bit.
		/// </summary>
		/// <param name="messageLevel">A nonzero combination of known severity bits, from 1 through 63.</param>
		/// <returns>True when all message bits are accepted; false for a valid mismatch, including any message against destination mask zero.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="messageLevel"/> is zero, negative or contains an unknown bit.</exception>
		/// <remarks>This query does not dispatch or change the captured mask or registration.</remarks>
		public bool ValidateMessageLevel(LogLevels messageLevel)
		{
			if (messageLevel == 0 || (messageLevel & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(messageLevel));

			return (messageLevel & _reportingLevel) == messageLevel;
		}
	}
}
