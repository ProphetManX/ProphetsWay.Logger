using System;

namespace ProphetsWay.Utilities
{
	/// <summary>Signals mandatory dispatch failure after independent attempts and safe reporting.</summary>
	/// <remarks>B17/B18: library-created with no public constructor or raw cause. Report has the
	/// same immutable safe values as notification, including when recursive reporting was suppressed.
	/// Initial InnerException/HelpLink are null, Data empty, Source the fixed ProphetsWay.Logger name.
	/// Message and ToString contain fixed prose and allowed generated IDs, boundary codes and counts
	/// only; wording is unspecified. StackTrace returns null, not erasure of CLR diagnostic state.
	/// Inherited post-catch mutation, reflection, TargetSite, debugger and serialization are outside
	/// the sanitized interface. Default-return capture/check failure alone produces no such exception.
	/// No strict-selection surface or raw diagnostic channel is introduced.</remarks>
	public sealed class LogDispatchException : Exception
	{
		private const string FailureMessage = "Configured log dispatch failed.";
		private readonly LogFailureReport _report;

		internal LogDispatchException(LogFailureReport report) : base(FailureMessage)
		{
			_report = report;
			Source = "ProphetsWay.Logger";
		}

		/// <summary>Exposes the original dispatch's bounded safe cause summary.</summary>
		/// <value>A non-null immutable report, independent of secondary reporter failures.</value>
		/// <remarks>B19: available even when recursive reporting was suppressed; no raw InnerException.</remarks>
		public LogFailureReport Report
		{
			get
			{
				return _report;
			}
		}

		/// <summary>Withholds runtime stack text from this diagnostic property.</summary>
		/// <value>Always null.</value>
		/// <remarks>B20-B21: does not promise to remove CLR diagnostic state or prevent stack inspection.</remarks>
		public override string StackTrace
		{
			get
			{
				return null;
			}
		}

		/// <summary>Returns a safe textual dispatch-failure summary.</summary>
		/// <returns>Non-null fixed prose and optionally generated IDs, stage codes and counts only.</returns>
		/// <remarks>B20: excludes stack/paths, raw causes and mutable inherited diagnostic content.
		/// Exact formatting is unspecified; this is not serialization of the exception graph.</remarks>
		public override string ToString()
		{
			return FailureMessage;
		}
	}
}
