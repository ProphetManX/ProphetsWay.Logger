using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProphetsWay.Utilities
{
	/// <summary>Exposes bounded safe original-failure facts for one failed call.</summary>
	/// <remarks>B14-B17: sealed, library-created, no public constructor/factory, concurrent reads safe.
	/// At least one original failure exists. Total is CoreCaptureFailureCount + Failures.Count +
	/// OverflowCount, widening before addition. Retain at most eight recipient descriptors during
	/// accumulation, not collect-all-then-trim. Values/backing stay immutable across reads,
	/// callbacks and later calls; reference identity across views/observers is unspecified.
	/// No payload, labels, origins, scope data, raw cause, sequence, destination, delegate or
	/// control backlink is retained. Reporting errors do not change original facts.</remarks>
	public sealed class LogFailureReport
	{
		private readonly Guid _correlationId;
		private readonly ReadOnlyCollection<LogFailureDescriptor> _failures;
		private readonly int _overflowCount;
		private readonly int _coreCaptureFailureCount;

		internal LogFailureReport(List<LogFailureDescriptor> failures, int overflowCount)
			: this(failures, overflowCount, 0)
		{
		}

		internal LogFailureReport(List<LogFailureDescriptor> failures, int overflowCount, int coreCaptureFailureCount)
		{
			if (coreCaptureFailureCount < 0 || coreCaptureFailureCount > 1)
				throw new ArgumentOutOfRangeException(nameof(coreCaptureFailureCount));

			_correlationId = Guid.NewGuid();
			_failures = LogContext.CopyMembership(failures);
			_overflowCount = overflowCount;
			_coreCaptureFailureCount = coreCaptureFailureCount;
		}

		/// <summary>Correlates this failed call's safe facts.</summary>
		/// <value>A generated nonempty Guid, fresh per failed call and independent of consumer data.</value>
		/// <remarks>B16: not authenticated identity or a durable registration identifier.</remarks>
		public Guid CorrelationId
		{
			get
			{
				return _correlationId;
			}
		}

		/// <summary>Gets the first eight or fewer original recipient failures.</summary>
		/// <value>Non-null read-only membership of zero through eight non-null descriptors in encounter order.</value>
		/// <remarks>B14-B16: empty for core-only failure. No writable backing escapes through aliases,
		/// including collection interfaces; attempted membership writes throw NotSupportedException.
		/// Core/reporter failures are excluded, and the bound does not limit recipient attempts.</remarks>
		public ReadOnlyCollection<LogFailureDescriptor> Failures
		{
			get
			{
				return _failures;
			}
		}

		/// <summary>Counts original recipient failures beyond the first eight.</summary>
		/// <value>Nonnegative; zero for up to eight, one for nine failed recipients.</value>
		/// <remarks>B14/B15: neither shared core failure, mismatch nor reporter failure is overflow.</remarks>
		public int OverflowCount
		{
			get
			{
				return _overflowCount;
			}
		}

		/// <summary>Counts failure of the shared capture needed to form this entry.</summary>
		/// <value>One if that capture failed, otherwise zero.</value>
		/// <remarks>B15: count once regardless of affected recipients. Core-only means empty
		/// Failures and zero OverflowCount. This reveals no value/cause and implies no mandatory throw.</remarks>
		public int CoreCaptureFailureCount
		{
			get
			{
				return _coreCaptureFailureCount;
			}
		}
	}
}
