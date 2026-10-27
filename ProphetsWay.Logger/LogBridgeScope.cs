using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProphetsWay.Utilities
{
	/// <summary>Preserves one external logical scope as data separate from native scopes.</summary>
	/// <remarks>B28-B32: library-created, sealed, with no public construction, mutation
	/// or disposal member. Its position is supplied by the containing entry's Scopes
	/// sequence. It is not a live handle or an implicit native scope opening. Structural
	/// membership supports concurrent reads; original state and nested values remain
	/// caller-owned, possibly mutable graphs. No deep freeze, erasure, capability
	/// stripping or safe diagnostic representation is supplied. Native source frames
	/// remain in the entry's NativeContext rather than being converted to these frames.</remarks>
	public sealed class LogBridgeScope
	{
		/// <summary>Captures the supplied pair membership for one external frame.</summary>
		/// <param name="state">Original opaque, scalar or structured scope state; null is retained.</param>
		/// <param name="properties">Required finite stable sequence of recognized pairs; empty is valid.</param>
		/// <param name="annotations">Optional explicit application mapping for this frame only.</param>
		/// <exception cref="ArgumentNullException">properties is null; ParamName is "properties".</exception>
		/// <exception cref="Exception">Sequence access fails; no usable frame is produced.</exception>
		/// <remarks>B29: copy membership synchronously before exposure. Later source
		/// changes do not change it. Sequence acquisition, iteration, element access
		/// and disposal are caller-code boundaries; a failed prefix is not exposed.
		/// Foreign exception detail, access counts and competing-error precedence are
		/// unspecified. Concurrent producer mutation and infinite-sequence termination
		/// are not supported guarantees. No caller effects are rolled back. Retain
		/// original state without inspecting it, calling ToString, enumerating its
		/// nested values or disposing it. No ambient stack is read or changed.</remarks>
		internal LogBridgeScope(object state,
			IEnumerable<KeyValuePair<string, object>> properties, LogAnnotations annotations)
		{
			if (properties == null)
			{
				throw new ArgumentNullException(nameof(properties));
			}

			Properties = LogContext.CopyMembership(properties);
			State = state;
			Annotations = annotations;
		}

		/// <summary>Gets the original external scope state, not a rendered substitute.</summary>
		/// <value>The supplied object reference or boxed value, including null.</value>
		/// <remarks>B30: preserve opaque/scalar state even when no property pairs are
		/// available. It may itself be an enumerable or retain capabilities; original
		/// retention does not make it lazy membership backing or authorize graph inspection.</remarks>
		public object State { get; }

		/// <summary>Gets this frame's captured ordered application pairs.</summary>
		/// <value>A non-null, possibly empty read-only collection of original pairs.</value>
		/// <remarks>B31: preserve duplicates, null/empty/whitespace keys, null values
		/// and default pairs. No name normalization, merge with event properties or
		/// other frames, implicit labels, reverse parsing or deep cloning occurs.
		/// No writable library-owned backing or lazy source/enumerator is exposed;
		/// collection-interface mutation throws NotSupportedException. Original
		/// nested values may still mutate. View identity is unspecified.</remarks>
		public ReadOnlyCollection<KeyValuePair<string, object>> Properties { get; }

		/// <summary>Gets the explicit external label attachment belonging to this frame.</summary>
		/// <value>Null for absence, or the supplied immutable annotation occurrences.</value>
		/// <remarks>B32: present-empty remains distinct from absent. Preserve every
		/// repeated occurrence and each frame's separate attachment, including when
		/// an annotation instance is reused. An origin is this frame's Scopes index
		/// plus its LabelOccurrences index, not a native LogLabelOrigin.ScopeIndex.
		/// Mapping is explicit; a property name or annotation-shaped raw state does
		/// not automatically classify the frame. No attachment subtracts inherited labels.</remarks>
		public LogAnnotations Annotations { get; }
	}
}