using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProphetsWay.Utilities
{
	/// <summary>Exposes the completed label facts of one captured entry.</summary>
	/// <remarks>A01/A02/A05-A08/A39: immutable, library-created, without public construction.
	/// Capture completes before exposure. Lists own read-only membership; collection-interface
	/// mutation throws NotSupportedException. Concurrent reads are supported; view and label
	/// representative reference identity are unspecified. Scope end cannot alter this value.
	/// Only labels and their local attachment relationships are retained, not general scope
	/// properties, handles, callbacks or payload graphs. It is not an authorization token.</remarks>
	public sealed class LogLabelContext
	{
		private readonly LogAnnotations _entryAnnotations;
		private readonly ReadOnlyCollection<LogAnnotations> _scopeAnnotations;
		private readonly ReadOnlyCollection<LogLabelOrigin> _origins;
		private readonly ReadOnlyCollection<SensitivityLabel> _effectiveLabels;

		/// <summary>Gets the entry's explicit annotation, when supplied.</summary>
		/// <value>Null for absence; otherwise the immutable supplied occurrence values.</value>
		/// <remarks>A01/A06: a non-null empty annotation remains distinguishable from null.
		/// Neither absence nor empty membership removes inherited labels.</remarks>
		public LogAnnotations EntryAnnotations
		{
			get
			{
				return _entryAnnotations;
			}
		}

		/// <summary>Gets enclosing scope annotations in nesting order.</summary>
		/// <value>A non-null, possibly empty list of non-null annotations, outermost first.</value>
		/// <remarks>A06: project LogContext.Scopes in order, retaining exactly the frames
		/// whose Annotations is non-null. Preserve empty annotations and repeated uses of
		/// the same annotation object as separate entries. No non-label property is retained.</remarks>
		public ReadOnlyCollection<LogAnnotations> ScopeAnnotations
		{
			get
			{
				return _scopeAnnotations;
			}
		}

		/// <summary>Gets the origin of every contributed label occurrence.</summary>
		/// <value>A non-null, possibly empty list of non-null origins, scopes first then entry.</value>
		/// <remarks>A07: scope order is outermost first; occurrence indices within each
		/// attachment increase from zero. Emit exactly one origin per LabelOccurrences
		/// element, including duplicates. Empty and absent attachments add no origin.
		/// Every origin's label and indices agree with the source in this same context.</remarks>
		public ReadOnlyCollection<LogLabelOrigin> Origins
		{
			get
			{
				return _origins;
			}
		}

		/// <summary>Gets complete identity membership for recipient label selection.</summary>
		/// <value>A non-null, possibly empty read-only collection, one label per distinct origin identity.</value>
		/// <remarks>A08/A31: exactly the union of Origins labels using ordinal value equality,
		/// never hashes alone. Inherited-only membership is labeled. Order and representative
		/// reference identity are unspecified. Reading membership does not evaluate permission.</remarks>
		public ReadOnlyCollection<SensitivityLabel> EffectiveLabels
		{
			get
			{
				return _effectiveLabels;
			}
		}

		internal LogLabelContext(LogAnnotations entryAnnotations, ReadOnlyCollection<LogScopeFrame> scopes)
		{
			var annotations = new List<LogAnnotations>();
			var origins = new List<LogLabelOrigin>();
			var membership = new HashSet<SensitivityLabel>();
			var labels = new List<SensitivityLabel>();
			foreach (var frame in scopes)
			{
				if (frame.Annotations == null)
					continue;

				var scopeIndex = annotations.Count;
				annotations.Add(frame.Annotations);
				AppendOrigins(frame.Annotations, scopeIndex, origins, membership, labels);
			}

			if (entryAnnotations != null)
				AppendOrigins(entryAnnotations, null, origins, membership, labels);

			_entryAnnotations = entryAnnotations;
			_scopeAnnotations = LogContext.CopyMembership(annotations);
			_origins = LogContext.CopyMembership(origins);
			_effectiveLabels = LogContext.CopyMembership(labels);
		}

		private static void AppendOrigins(LogAnnotations annotations, int? scopeIndex,
			List<LogLabelOrigin> origins, HashSet<SensitivityLabel> membership, List<SensitivityLabel> labels)
		{
			for (var occurrenceIndex = 0; occurrenceIndex < annotations.LabelOccurrences.Count; occurrenceIndex++)
			{
				var label = annotations.LabelOccurrences[occurrenceIndex];
				origins.Add(new LogLabelOrigin(label, scopeIndex, occurrenceIndex));
				if (membership.Add(label))
					labels.Add(label);
			}
		}
	}
}