using System;
using System.Collections;
using System.Collections.Generic;

namespace ProphetsWay.Utilities
{
	/// <summary>Exposes one completed ordered transport envelope through the Microsoft state protocol.</summary>
	/// <remarks>O35: assembly-created adaptation type, not a public capture API or a
	/// replacement LogBridgeEntry. Event and single-frame envelopes have exactly the
	/// schemas documented on MicrosoftLoggerDestination; this container does not infer
	/// kind, classification or control authority from key spellings. Membership is
	/// owned, immutable and concurrently readable, not backed by a mutable caller list
	/// or a retained lazy enumerable. Nested entry/frame/state values remain original
	/// objects and can retain executable capabilities; this is not sanitized data or
	/// deep freezing. No property in the enumerable is a handle, private guard or route
	/// token added by the adapter. Separately bound private invocation bookkeeping is
	/// not exposed by the indexer/enumerators, and object identity alone is not authority.
	/// No new equality, ToString, serializer, public construction or disposal contract
	/// is added. The transport formatter belongs to the export attempt, not this type.</remarks>
	internal sealed class MicrosoftLogTransport : IReadOnlyList<KeyValuePair<string, object>>
	{
		private readonly KeyValuePair<string, object>[] _items;
		/// <summary>Copies the already selected envelope membership before exposing a transport value.</summary>
		/// <param name="items">Required finite stable ordered pairs; empty is permitted by this container.</param>
		/// <exception cref="ArgumentNullException">items is null; ParamName is "items".</exception>
		/// <exception cref="Exception">Sequence access fails; no usable transport is produced.</exception>
		/// <remarks>O36: validate the required sequence before accessing it, then copy
		/// completely, including successful enumeration disposal. Preserve pair order,
		/// duplicates, null keys/values and default pairs; do not inspect nested values,
		/// normalize names, invoke formatters or classify anything. The adapter supplies
		/// the fixed schemas; this constructor is not a user-extensible key registry.
		/// Acquisition, iteration, Current and disposal can execute source code; foreign
		/// exception details, access counts and competing-error priority are unspecified.
		/// No prefix publication, effect rollback, concurrent producer mutation support
		/// or infinite-input termination is promised. No native scope or route is read
		/// or changed. Construction itself creates no controlled-transfer authority.
		/// Export callers build envelopes from completed owned facts; per-recipient
		/// transport construction failure is Output, not a new producer-capture attempt.</remarks>
		internal MicrosoftLogTransport(IEnumerable<KeyValuePair<string, object>> items)
		{
			if (items == null)
				throw new ArgumentNullException(nameof(items));

			var captured = new List<KeyValuePair<string, object>>();
			foreach (var item in items)
				captured.Add(item);
			_items = captured.ToArray();
		}

		/// <summary>Gets the captured number of envelope pairs.</summary>
		/// <value>The fixed nonnegative membership count; zero for an empty container.</value>
		/// <remarks>O37: no source enumeration, callback, recapture, formatting or mutation
		/// occurs. Event envelopes have count three and frame envelopes count one under
		/// the adapter schema; the container imposes no independent domain schema.</remarks>
		public int Count
		{
			get { return _items.Length; }
		}

		/// <summary>Gets the captured pair at a zero-based envelope position.</summary>
		/// <param name="index">Position measured from the first pair, from zero through Count minus one.</param>
		/// <returns>The original captured pair/value references at that position.</returns>
		/// <exception cref="ArgumentOutOfRangeException">index is negative or at least Count; ParamName is "index".</exception>
		/// <remarks>O38: no lookup by key, value formatting or membership mutation.
		/// Duplicate keys occupy distinct positions. No valid index exists when empty.</remarks>
		public KeyValuePair<string, object> this[int index]
		{
			get
				{
					if (index < 0 || index >= _items.Length)
						throw new ArgumentOutOfRangeException(nameof(index));
					return _items[index];
				}
		}

		/// <summary>Enumerates the completed envelope in captured order.</summary>
		/// <returns>A non-null independent enumerator over the fixed pairs.</returns>
		/// <remarks>O39: successful MoveNext exposes positions zero through Count minus
		/// one; it returns false after the last and immediately for empty membership.
		/// Use Current only after successful MoveNext under the normal IEnumerator
		/// protocol. No original source or nested-value enumeration, capture, formatter,
		/// output or label check occurs. Concurrent independent enumerations are allowed.
		/// Enumeration disposal does not dispose entries, values, loggers or scopes.
		/// Reset support and enumerator identity are not additional guarantees.</remarks>
		public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
		{
			return ((IEnumerable<KeyValuePair<string, object>>)_items).GetEnumerator();
		}

		/// <summary>Provides the same ordered membership to nongeneric enumeration.</summary>
		/// <returns>A non-null independent IEnumerator over the same fixed pairs.</returns>
		/// <remarks>O40: the generic enumeration's protocol and side-effect limits apply;
		/// Current boxes each KeyValuePair under normal value semantics. Boxing identity
		/// is unspecified. This is not erasure of the original producer state/delegate
		/// retained in the entry, nor exposure of mutable membership or private control.</remarks>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return _items.GetEnumerator();
		}
	}
}
