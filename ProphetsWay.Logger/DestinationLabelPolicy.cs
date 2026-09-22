using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProphetsWay.Utilities
{
	/// <summary>
	/// Captures an immutable destination label-selection policy.
	/// </summary>
	/// <remarks>
	/// Mode and configured membership never change. Reads and evaluation do not mutate policy state,
	/// labels or caller collections. Matching uses SensitivityLabel value equality: ordinal,
	/// case-sensitive, unnormalized identity, without a registry.
	/// This synchronous policy does not accumulate scopes/origins, render, invoke destinations,
	/// dispatch or select fallback. Producer mutation during input capture is unsupported;
	/// capture freezes membership, not arbitrary object graphs.
	/// Supplied sequence access can execute application code; rollback of its effects, validation
	/// precedence and enumeration timing/count are unspecified.
	/// No termination or resource bound for unbounded/nonterminating input is specified.
	/// </remarks>
	public sealed class DestinationLabelPolicy
	{
		private readonly HashSet<SensitivityLabel> _labels;

		/// <summary>
		/// Captures one mode and configured label membership.
		/// </summary>
		/// <param name="mode">Exactly NoFilter, Exclude or AllowOnly; this argument is required.</param>
		/// <param name="labels">
		/// Non-null sequence of non-null labels; empty input and duplicate identities are valid in every mode.
		/// </param>
		/// <exception cref="ArgumentNullException">labels is null; ParamName is "labels".</exception>
		/// <exception cref="ArgumentException">labels contains a null label; ParamName is "labels".</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// mode is not a declared value, including combined value 3; ParamName is "mode".
		/// </exception>
		/// <exception cref="Exception">
		/// Supplied sequence access fails; construction completes exceptionally without a usable policy.
		/// </exception>
		/// <remarks>
		/// Successful construction copies membership before returning; later source changes cannot affect
		/// Labels or Allows. Retain one representative per configured identity, including in NoFilter;
		/// never borrow caller-writable backing storage.
		/// A null element anywhere in a finite input invalidates it in every mode.
		/// Sequence-failure details beyond the named argument errors are unspecified.
		/// </remarks>
		public DestinationLabelPolicy(LabelFilterMode mode, IEnumerable<SensitivityLabel> labels)
		{
			if (mode != LabelFilterMode.NoFilter && mode != LabelFilterMode.Exclude && mode != LabelFilterMode.AllowOnly)
			{
				throw new ArgumentOutOfRangeException(nameof(mode));
			}

			if (labels == null)
			{
				throw new ArgumentNullException(nameof(labels));
			}

			var membership = new HashSet<SensitivityLabel>();
			var capturedLabels = new List<SensitivityLabel>();
			foreach (var label in labels)
			{
				if (label == null)
				{
					throw new ArgumentException("Labels must not contain null elements.", nameof(labels));
				}

				if (membership.Add(label))
				{
					capturedLabels.Add(label);
				}
			}

			_labels = membership;
			Mode = mode;
			Labels = new OwnedLabels(capturedLabels);
		}

		/// <summary>
		/// Gets the selected mode.
		/// </summary>
		/// <value>The unchanged mode supplied to successful construction; no public setter exists.</value>
		public LabelFilterMode Mode { get; }

		/// <summary>
		/// Gets the captured configured membership.
		/// </summary>
		/// <value>
		/// A non-null, possibly empty read-only collection containing one representative of every configured identity.
		/// </value>
		/// <remarks>
		/// No returned reference exposes writable policy storage. Mutation through collection interfaces
		/// throws NotSupportedException under ReadOnlyCollection&lt;T&gt;.
		/// Enumeration order, collection-reference identity and representative label-reference identity
		/// are unspecified; membership remains unchanged.
		/// </remarks>
		public ReadOnlyCollection<SensitivityLabel> Labels { get; }

		/// <summary>
		/// Tests label eligibility without delivering an entry.
		/// </summary>
		/// <param name="effectiveLabels">
		/// Non-null sequence of non-null labels already including entry and enclosing-scope labels;
		/// empty means unlabeled.
		/// </param>
		/// <returns>
		/// For valid input: true for NoFilter; for Exclude, true exactly when there is no intersection;
		/// for AllowOnly, true exactly when effective membership is nonempty and wholly configured.
		/// Otherwise false.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// effectiveLabels is null; ParamName is "effectiveLabels".
		/// </exception>
		/// <exception cref="ArgumentException">
		/// effectiveLabels contains a null label; ParamName is "effectiveLabels".
		/// </exception>
		/// <exception cref="Exception">
		/// Supplied sequence access fails; evaluation completes exceptionally without a Boolean result.
		/// Exact sequence-failure details are unspecified.
		/// </exception>
		/// <remarks>
		/// Inherited-only input is labeled. Duplicates and ordering do not change matching;
		/// unknown valid identities participate normally.
		/// Empty Exclude permits every valid input; empty AllowOnly denies every valid input.
		/// NoFilter ignores configured membership, not input validity.
		/// A null anywhere in a finite input is invalid even after a decisive match/mismatch.
		/// No partial or failed capture supplies a normal result.
		/// Repeated or concurrent evaluations of unchanged valid memberships preserve the same result
		/// and policy state; callers must keep supplied inputs stable during capture.
		/// False is an ordinary mismatch, not an output failure; a rejecting policy remains valid
		/// and does not activate fallback.
		/// True establishes label permission only, not delivery or pre-render/raw-handoff withholding.
		/// Failure handling and origin preservation belong to integration.
		/// </remarks>
		public bool Allows(IEnumerable<SensitivityLabel> effectiveLabels)
		{
			if (effectiveLabels == null)
			{
				throw new ArgumentNullException(nameof(effectiveLabels));
			}

			var hasLabels = false;
			var hasIntersection = false;
			var allConfigured = true;
			foreach (var label in effectiveLabels)
			{
				if (label == null)
				{
					throw new ArgumentException("Labels must not contain null elements.", nameof(effectiveLabels));
				}

				hasLabels = true;
				var configured = _labels.Contains(label);
				hasIntersection |= configured;
				allConfigured &= configured;
			}

			switch (Mode)
			{
				case LabelFilterMode.Exclude:
					return !hasIntersection;
				case LabelFilterMode.AllowOnly:
					return hasLabels && allConfigured;
				default:
					return true;
			}
		}

		private sealed class OwnedLabels : ReadOnlyCollection<SensitivityLabel>, ICollection
		{
			private readonly object _syncRoot = new object();

			internal OwnedLabels(IList<SensitivityLabel> labels) : base(labels)
			{
			}

			object ICollection.SyncRoot => _syncRoot;
		}
	}
}