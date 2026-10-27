using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProphetsWay.Utilities
{
	/// <summary>Exposes the annotations and copied property membership of one native scope frame.</summary>
	/// <remarks>A03-A06/A09/A39/A40: library-created, without public construction. Annotations
	/// and property membership are fixed at successful scope opening. Frames retain supplied
	/// boundaries and outer-to-inner placement, including empty and unlabeled frames.
	/// This is structural immutability, not a deep freeze of property values. Concurrent
	/// membership reads are supported; concurrent use of nested objects remains caller-owned.
	/// Apart from retaining supplied property values, Logger adds no handle, live-stack,
	/// source-enumerator, formatter, recipient or diagnostic-history backlink to the frame's
	/// capture structure. This exclusion does not describe the objects already reachable
	/// through supplied property values; Properties preserves those original references.</remarks>
	public sealed class LogScopeFrame
	{
		private readonly LogAnnotations _annotations;
		private readonly ReadOnlyCollection<KeyValuePair<string, object>> _properties;

		/// <summary>Gets the explicit label attachment to this frame, if supplied.</summary>
		/// <value>Null for absence; otherwise the unchanged immutable annotation occurrences.</value>
		/// <remarks>A01/A06: empty annotations are retained. Reusing an annotation on
		/// several frames does not combine frames. Properties never supply implicit labels.</remarks>
		public LogAnnotations Annotations
		{
			get
			{
				return _annotations;
			}
		}

		/// <summary>Gets the ordered property membership copied when this scope opened.</summary>
		/// <value>A non-null, possibly empty read-only list of supplied string/object pairs.</value>
		/// <remarks>A03-A05/A39/A40: preserve order, repeated keys and original values/references.
		/// Null, empty and whitespace keys, null values and default pairs are permitted;
		/// keys are not interpreted, normalized or merged. Later source-list changes cannot
		/// alter membership. Nested values are not cloned or promised immutable. No arbitrary
		/// getters, implicit object ToString or value enumeration are used for capture.
		/// A supplied value may itself be a handle, destination, delegate, enumerable or a graph
		/// retaining such objects. Capture does not inspect or rewrite those graphs, strip
		/// references or reject a pair merely because its value contains such a reference.
		/// Membership copying is not sanitization or removal of existing object capabilities.
		/// The returned collection exposes no writable library-owned membership backing;
		/// collection-interface writes throw NotSupportedException. This does not prevent
		/// mutation through an original property value. View and representative frame reference
		/// identity are unspecified.</remarks>
		public ReadOnlyCollection<KeyValuePair<string, object>> Properties
		{
			get
			{
				return _properties;
			}
		}

		internal LogScopeFrame(LogAnnotations annotations, IEnumerable<KeyValuePair<string, object>> properties)
		{
			_properties = LogContext.CopyMembership(properties);
			_annotations = annotations;
		}
	}
}