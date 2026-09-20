using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProphetsWay.Utilities
{
	/// <summary>Provides the bounded safe failure facts for one dispatch.</summary>
	/// <remarks>B08-B12: immutable, library-created and safe for concurrent reads; no payload/cause backlinks.
	/// Instances describe failures only. No public constructor or factory exists. Reference identity of
	/// reports, views or elements across observers/reads is unspecified; their values remain unchanged.</remarks>
	public sealed class LogFailureReport
	{
		private readonly Guid _correlationId;
		private readonly ReadOnlyCollection<LogFailureDescriptor> _failures;
		private readonly int _overflowCount;

		internal LogFailureReport(List<LogFailureDescriptor> failures, int overflowCount)
		{
			_correlationId = Guid.NewGuid();
			_failures = new ReadOnlyCollection<LogFailureDescriptor>(failures.ToArray());
			_overflowCount = overflowCount;
		}

		/// <summary>Correlates safe diagnostics belonging to this failed dispatch.</summary>
		/// <value>A generated nonempty Guid, fresh for this dispatch and unrelated to consumer data.</value>
		/// <remarks>B09: not a durable registration ID; separate failed calls generate fresh IDs.</remarks>
		public Guid CorrelationId
		{
			get
			{
				return _correlationId;
			}
		}

		/// <summary>Exposes the first bounded set of original recipient failures.</summary>
		/// <value>A non-null collection of one through eight non-null immutable descriptors in encounter order.</value>
		/// <remarks>B08/B11: library-owned backing cannot be mutated through aliases; collection-interface
		/// writes throw NotSupportedException. Later logging or registry changes cannot change this view.</remarks>
		public ReadOnlyCollection<LogFailureDescriptor> Failures
		{
			get
			{
				return _failures;
			}
		}

		/// <summary>Counts original failures omitted from the bounded descriptor list.</summary>
		/// <value>A nonnegative count; total original failures equal Failures.Count plus OverflowCount.</value>
		/// <remarks>B08: zero for at most eight failures; nine failures produce eight descriptors and one overflow.
		/// Reporter errors are excluded. This bound never limits recipient attempts.</remarks>
		public int OverflowCount
		{
			get
			{
				return _overflowCount;
			}
		}
	}
}
