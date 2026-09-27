using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProphetsWay.Utilities
{
	/// <summary>Exposes one completed native scope and label capture for a log call.</summary>
	/// <remarks>A05-A08/A22/A34/A39-A40: library-created, without public construction or
	/// a public ambient-capture factory. Both getters describe the same capture; no partial
	/// value is delivered. Structural membership is immutable and supports concurrent reads;
	/// nested scope property values remain original caller-owned objects. Reference identity
	/// is unspecified. Logger does not retain the producer's enumerable or enumerator as
	/// lazy capture backing, or add live-stack, handle, recipient or diagnostic-history links
	/// to the context structure. These structural exclusions do not exclude objects retained
	/// as original supplied property values, or the graphs reachable through those values.
	/// Possession permits local readback, not authorization, direct-call bypass, erasure or
	/// automatic classification. Retaining recipients own their retention and later cleanup.
	/// <para>F01-F05/F23: fixed for this context, including after scope end.
	/// Logger captures this value once before recipient callbacks and shares it
	/// through trusted delivery; it is not a filename or recipient-output time.
	/// A supplied-base public direct call is a new call. When LogWithContext
	/// accepts a current supplied context, it preserves its scope/label facts in
	/// the selected context but assigns the new call's captured UTC time there.
	/// It never changes this retained instance. Recursive calls also capture anew.
	/// Fresh capture does not promise distinct or increasing clock values.
	/// Reading this property performs no capture, formatting or delivery.</para></remarks>
	public sealed class LogContext
	{
		private readonly LogLabelContext _labels;
		private readonly ReadOnlyCollection<LogScopeFrame> _scopes;
		private readonly DateTimeOffset _eventTimestampUtc;

		/// <summary>Gets the UTC event time captured for this log call.</summary>
		/// <value>The single call-captured DateTimeOffset with offset zero.</value>
		/// <remarks>F01-F05/F23: fixed for this context, including after scope end.
		/// Logger captures this value once before recipient callbacks and shares it
		/// through trusted delivery; it is not a filename or recipient-output time.
		/// A supplied-base public direct call is a new call. When LogWithContext
		/// accepts a current supplied context, it preserves its scope/label facts in
		/// the selected context but assigns the new call's captured UTC time there.
		/// It never changes this retained instance. Recursive calls also capture anew.
		/// Fresh capture does not promise distinct or increasing clock values.
		/// Reading this property performs no capture, formatting or delivery.</remarks>
		public DateTimeOffset EventTimestampUtc
		{
			get
			{
				return _eventTimestampUtc;
			}
		}

		/// <summary>Gets the entry and enclosing-scope label facts for this capture.</summary>
		/// <value>A non-null completed LogLabelContext, including when all membership is empty.</value>
		/// <remarks>A06-A08: ScopeAnnotations is the ordered non-null Annotations projection
		/// of Scopes. EntryAnnotations belongs to this entry, not to another scope frame.</remarks>
		public LogLabelContext Labels
		{
			get
			{
				return _labels;
			}
		}

		/// <summary>Gets all native frames enclosing the call at capture.</summary>
		/// <value>A non-null, possibly empty read-only list of non-null frames, outermost first.</value>
		/// <remarks>A05/A11-A13/A22: include inherited frames and current-flow additions,
		/// including empty frames and frames without annotations. Membership cannot change
		/// after capture through source edits, scope exit or callback-created scopes.
		/// Collection-interface mutation throws NotSupportedException. This list is not
		/// indexed by LogLabelOrigin.ScopeIndex; Labels exposes the label-only projection.</remarks>
		public ReadOnlyCollection<LogScopeFrame> Scopes
		{
			get
			{
				return _scopes;
			}
		}

		internal LogContext(IEnumerable<LogScopeFrame> scopes, LogAnnotations entryAnnotations, DateTimeOffset eventTimestampUtc)
		{
			_scopes = CopyMembership(scopes);
			_labels = new LogLabelContext(entryAnnotations, _scopes);
			_eventTimestampUtc = eventTimestampUtc;
		}

		internal LogContext(LogContext context, DateTimeOffset eventTimestampUtc)
		{
			_scopes = context._scopes;
			_labels = context._labels;
			_eventTimestampUtc = eventTimestampUtc;
		}

		internal static ReadOnlyCollection<T> CopyMembership<T>(IEnumerable<T> source)
		{
			var members = new List<T>();
			foreach (var member in source)
				members.Add(member);

			return new OwnedMembership<T>(members);
		}

		private sealed class OwnedMembership<T> : ReadOnlyCollection<T>, ICollection
		{
			private readonly object _syncRoot = new object();

			internal OwnedMembership(IList<T> members) : base(members)
			{
			}

			object ICollection.SyncRoot => _syncRoot;
		}
	}
}