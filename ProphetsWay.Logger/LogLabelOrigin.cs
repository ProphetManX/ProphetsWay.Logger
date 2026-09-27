using System;

namespace ProphetsWay.Utilities
{
	/// <summary>Identifies one label occurrence in a completed label-context capture.</summary>
	/// <remarks>A02/A06-A08/A39: immutable and library-created, without public construction.
	/// Positions belong only to the containing LogLabelContext, not to a live stack, handle,
	/// thread, principal or durable identity. Equal labels at different positions retain
	/// different origins. This record retains no payload, scope handle, recipient or callback.</remarks>
	public sealed class LogLabelOrigin
	{
		private readonly SensitivityLabel _label;
		private readonly int? _scopeIndex;
		private readonly int _occurrenceIndex;

		/// <summary>Gets the label at the identified attachment position.</summary>
		/// <value>A non-null label equal to the indexed source occurrence.</value>
		/// <remarks>A02/A07: equality is the existing ordinal SensitivityLabel equality;
		/// no identity normalization, registry lookup or reference-identity promise is added.</remarks>
		public SensitivityLabel Label
		{
			get
			{
				return _label;
			}
		}

		/// <summary>Identifies the occurrence's enclosing label attachment, or the entry.</summary>
		/// <value>Null for EntryAnnotations; otherwise a zero-based valid ScopeAnnotations index.</value>
		/// <remarks>A06/A07: zero is the outermost captured non-null scope annotation.
		/// Empty annotations occupy positions; frames with no annotation do not. This is
		/// not an index into LogContext.Scopes and not a persistent scope identifier.</remarks>
		public int? ScopeIndex
		{
			get
			{
				return _scopeIndex;
			}
		}

		/// <summary>Locates the occurrence within its source attachment.</summary>
		/// <value>A zero-based index below the source attachment's LabelOccurrences.Count.</value>
		/// <remarks>A07: the source is EntryAnnotations for a null ScopeIndex, otherwise
		/// ScopeAnnotations[ScopeIndex]. Duplicate occurrences have separate positions.</remarks>
		public int OccurrenceIndex
		{
			get
			{
				return _occurrenceIndex;
			}
		}

		internal LogLabelOrigin(SensitivityLabel label, int? scopeIndex, int occurrenceIndex)
		{
			_label = label;
			_scopeIndex = scopeIndex;
			_occurrenceIndex = occurrenceIndex;
		}
	}
}