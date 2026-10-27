using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProphetsWay.Utilities
{
	/// <summary>Attaches immutable label occurrences separately from application metadata.</summary>
	/// <remarks>C01-C05: this value owns copied occurrence membership, not a registry or scope lifetime.
	/// Reusing it does not combine attachments or erase their separate origins. Labels retain their
	/// existing ordinal value identity. Concurrent reads are supported; no formatter or logging is invoked.</remarks>
	public sealed class LogAnnotations
	{
		/// <summary>Copies the labels belonging to one explicit attachment.</summary>
		/// <param name="labels">A finite, stable, non-null sequence of non-null labels; empty is valid.</param>
		/// <exception cref="ArgumentNullException">labels is null; ParamName is "labels".</exception>
		/// <exception cref="ArgumentException">A null element occurs anywhere; ParamName is "labels".</exception>
		/// <exception cref="Exception">Sequence access fails; no usable annotation value is produced.</exception>
		/// <remarks>C02-C04: copy before return, preserving sequence order and every duplicate occurrence.
		/// Later additions, removals or replacements cannot change this value. A failed prefix is never
		/// published. Sequence access includes acquisition, iteration, element access and disposal.
		/// Foreign error details, competing-error precedence and access counts are unspecified; these are
		/// local construction errors, not sanitized dispatch reports. Caller mutation during capture is
		/// unsupported. No rollback of caller-code effects or unbounded-sequence termination is promised.</remarks>
		public LogAnnotations(IEnumerable<SensitivityLabel> labels)
		{
			if (labels == null)
			{
				throw new ArgumentNullException(nameof(labels));
			}

			var capturedLabels = new List<SensitivityLabel>();
			foreach (var label in labels)
			{
				if (label == null)
				{
					throw new ArgumentException("Labels must not contain null elements.", nameof(labels));
				}

				capturedLabels.Add(label);
			}

			LabelOccurrences = new OwnedLabelOccurrences(capturedLabels);
		}

		/// <summary>Gets every occurrence supplied to the successful attachment capture.</summary>
		/// <value>A non-null, possibly empty read-only ordered list of non-null labels, including repeats.</value>
		/// <remarks>C03-C05: occurrence count/order remain fixed. Equality is SensitivityLabel equality,
		/// not reference equality. Returned views expose no writable backing; collection-interface writes
		/// throw NotSupportedException. View and label representative reference identity are unspecified.</remarks>
		public ReadOnlyCollection<SensitivityLabel> LabelOccurrences { get; }

		private sealed class OwnedLabelOccurrences : ReadOnlyCollection<SensitivityLabel>, ICollection
		{
			private readonly object _syncRoot = new object();

			internal OwnedLabelOccurrences(IList<SensitivityLabel> labels) : base(labels)
			{
			}

			object ICollection.SyncRoot => _syncRoot;
		}
	}
}
