using System;

namespace ProphetsWay.Utilities
{
	/// <summary>Signals mandatory dispatch failure after independent attempts and safe reporting.</summary>
	/// <remarks>B07/B19-B21: library-created, with no public constructors or raw causes. Report values equal
	/// the notification's values. Initial InnerException and HelpLink are null, Data is empty and Source
	/// is the fixed library name ProphetsWay.Logger. Message contains only fixed prose and safe report values;
	/// wording is unspecified. Inherited Exception mutation is not report mutation or a sanitation guarantee.
	/// Runtime/debugger/serialization/TargetSite inspection is outside the sanitized reporting interface.</remarks>
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
