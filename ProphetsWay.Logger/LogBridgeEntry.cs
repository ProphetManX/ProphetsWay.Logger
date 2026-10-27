using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace ProphetsWay.Utilities
{
	/// <summary>Exposes the common facts of one completed controlled bridge entry.</summary>
	/// <remarks>B01-B06: library-created; no public or protected constructor or creation
	/// method. Only the sealed generic entry supplies producer-specific views. This is
	/// captured data, not a logger, scope handle, classification service or permission.
	/// Membership is copied before exposure and supports concurrent reads. Original
	/// state, exception, delegate and nested property objects are not cloned or frozen;
	/// their use, retention and eventual cleanup remain with their owners. Retaining
	/// the entry can retain those graphs. Capture does not dispose supplied objects.
	/// Getters perform no formatting, ambient capture, label selection or delivery.
	/// NativeContext and Scopes preserve distinct source domains. Native origin indices
	/// keep their existing meaning; external annotation origins use this entry or the
	/// Scopes position and the attachment's occurrence position. These positions are
	/// local to this capture, not durable identities or a reconstructed global order.
	/// Denied recipients must not receive this payload; raw transport is not sanitized
	/// diagnostics, redaction or a sandbox for code already holding the objects.
	/// No public field, metadata key or object identity is a cycle-control authority.
	/// Scope end or adapter disposal does not erase a retained capture.</remarks>
	public abstract class LogBridgeEntry
	{
		/// <summary>Initializes completed common data for the library's generic entry.</summary>
		/// <param name="categoryName">Original category, or null when unavailable.</param>
		/// <param name="eventId">Original Microsoft event identity, or null when unavailable.</param>
		/// <param name="originalLevel">Full nonzero known-bit native message mask.</param>
		/// <param name="rawMessage">Original separately supplied text, or null for absence.</param>
		/// <param name="formattedMessage">Already captured producer text, or null for absence.</param>
		/// <param name="exception">Original optional exception being logged.</param>
		/// <param name="properties">Required finite, stable ordered application pairs; empty is valid.</param>
		/// <param name="scopes">Required finite, stable ordered external frames; empty is valid.</param>
		/// <param name="annotations">Optional explicitly mapped external entry attachment.</param>
		/// <param name="nativeContext">Original completed native capture, or null when unavailable.</param>
		/// <exception cref="ArgumentNullException">properties or scopes is null;
		/// ParamName names the missing argument.</exception>
		/// <exception cref="ArgumentOutOfRangeException">originalLevel is zero, negative
		/// or contains unknown bits; ParamName is "originalLevel".</exception>
		/// <exception cref="ArgumentException">scopes contains a null frame; ParamName is "scopes".</exception>
		/// <exception cref="System.Exception">Sequence access fails; no usable entry is produced.</exception>
		/// <remarks>B07-B09: copy properties and scopes synchronously, preserving their
		/// order, duplicates and supplied values. Reject null required sequences and an
		/// invalid originalLevel before accessing either sequence. Priority among those
		/// checks is unspecified; null frame elements are checked during copying. Sequence
		/// acquisition, iteration, element access and disposal can execute caller code.
		/// A failing prefix is not published. Foreign exception detail, competing-error
		/// precedence and access counts are unspecified. No rollback of caller-code
		/// effects, termination of infinite input or mutation-during-capture support
		/// is promised. This constructor neither discovers properties on original state
		/// nor invokes a producer delegate. In dispatch, a failure completing shared
		/// capture is handled by the existing shared-capture failure boundary, not by
		/// exporting an incomplete or unlabeled entry.</remarks>
		internal LogBridgeEntry(string categoryName, EventId? eventId,
			LogLevels originalLevel, string rawMessage, string formattedMessage,
			Exception exception, IEnumerable<KeyValuePair<string, object>> properties,
			IEnumerable<LogBridgeScope> scopes, LogAnnotations annotations,
			LogContext nativeContext)
		{
			if (properties == null)
			{
				throw new ArgumentNullException(nameof(properties));
			}
			if (scopes == null)
			{
				throw new ArgumentNullException(nameof(scopes));
			}
			if (originalLevel == 0 || (originalLevel & ~LogLevels.Trace) != 0)
			{
				throw new ArgumentOutOfRangeException(nameof(originalLevel));
			}

			Properties = LogContext.CopyMembership(properties);
			Scopes = LogContext.CopyMembership(ValidateScopes(scopes));
			CategoryName = categoryName;
			EventId = eventId;
			OriginalLevel = originalLevel;
			RawMessage = rawMessage;
			FormattedMessage = formattedMessage;
			Exception = exception;
			Annotations = annotations;
			NativeContext = nativeContext;
			BridgeMetadata = LogContext.CopyMembership(new[]
			{
				new KeyValuePair<string, object>("ProphetsWay.CategoryName", CategoryName),
				new KeyValuePair<string, object>("ProphetsWay.EventId", EventId),
				new KeyValuePair<string, object>("ProphetsWay.OriginalLevel", OriginalLevel),
				new KeyValuePair<string, object>("ProphetsWay.NativeContext", NativeContext),
				new KeyValuePair<string, object>("ProphetsWay.Annotations", Annotations),
				new KeyValuePair<string, object>("ProphetsWay.Scopes", Scopes)
			});
		}

		/// <summary>Gets the originating category without selecting an output category.</summary>
		/// <value>The unchanged category string; null denotes unavailable information.</value>
		/// <remarks>B10: empty and whitespace strings remain unchanged. No default or
		/// call-stack-derived name is inserted here. A fixed logger's category is not
		/// changed by this origin fact; factory/default routing belongs to the adapter.</remarks>
		public string CategoryName { get; }

		/// <summary>Gets the available original Microsoft event identity.</summary>
		/// <value>The unchanged EventId value, including its Id and Name, or null for absence.</value>
		/// <remarks>B11: a present default EventId is distinct from absence. This carrier
		/// imposes no new identifier/name validation and derives no identity from text.</remarks>
		public EventId? EventId { get; }

		/// <summary>Gets the full native message mask retained for this entry.</summary>
		/// <value>A nonzero combination of the six known bits, numeric value 1 through 63.</value>
		/// <remarks>B12: preserve composites, not only the single Microsoft level used
		/// for export. Mask 9 remains 9 when exported as one Critical record. For a
		/// Microsoft-origin entry the adapter supplies the explicitly mapped native
		/// bit; Microsoft None is not an entry and unknown ordinals are not accepted
		/// by treating their numeric values as native masks.</remarks>
		public LogLevels OriginalLevel { get; }

		/// <summary>Gets separately supplied original message text when it exists.</summary>
		/// <value>The original string; null, empty and whitespace are permitted.</value>
		/// <remarks>B13: native raw text retains its raw meaning. Microsoft Log has no
		/// separate raw-message argument; its formatter result is FormattedMessage,
		/// not evidence for reconstructing a raw template or additional properties.</remarks>
		public string RawMessage { get; }

		/// <summary>Gets producer text already captured for this entry.</summary>
		/// <value>The captured formatter result or otherwise already available producer
		/// text; null, empty and whitespace remain permitted.</value>
		/// <remarks>B14: producer formatting is captured once when delivery is needed,
		/// before this completed value is exposed. Reads and controlled forwarding use
		/// the captured value, not another invocation of the retained original delegate.
		/// When producer text is unavailable, do not fabricate it. This is neither
		/// recipient-local massaged/framed output nor a reverse-parsed state template.</remarks>
		public string FormattedMessage { get; }

		/// <summary>Gets the original exception payload, if supplied.</summary>
		/// <value>The same exception reference, or null for an exceptionless entry.</value>
		/// <remarks>B15: no helper-specific requirement for an exception is imposed.
		/// Message, StackTrace, Data and nested causes are not cloned, stripped or
		/// inspected by capture. This is the exception being logged, not an exception
		/// thrown while logging; it must not be copied into the safe failure channel.</remarks>
		public Exception Exception { get; }

		/// <summary>Gets captured application event-property membership.</summary>
		/// <value>A non-null, possibly empty read-only ordered collection of original pairs.</value>
		/// <remarks>B16: preserve duplicate names, null/empty/whitespace keys, null
		/// values and default pairs. Do not normalize, merge, reverse-parse text or
		/// discover arbitrary object members. OriginalState remains separately
		/// available even when it supplied recognized pairs. Later source edits do
		/// not change this membership; nested values remain original objects.
		/// No lazy source/enumerator backs this view. Collection-interface mutation
		/// throws NotSupportedException; view identity is unspecified.</remarks>
		public ReadOnlyCollection<KeyValuePair<string, object>> Properties { get; }

		/// <summary>Gets the separately captured external scope frames.</summary>
		/// <value>A non-null, possibly empty read-only list of non-null frames, in the
		/// source's outer-to-inner order when that order is available.</value>
		/// <remarks>B17: retain the supplied enumeration order where only enumeration
		/// order is available; do not infer another order. Keep empty, opaque, scalar,
		/// repeated and unannotated frames separate. NativeContext.Scopes remains a
		/// distinct native sequence; this list neither replaces it nor includes copies
		/// of it. Later source edits and scope exit cannot change list membership.
		/// Collection-interface mutation throws NotSupportedException. View identity
		/// is unspecified. The frames are data, not export-disposal handles.</remarks>
		public ReadOnlyCollection<LogBridgeScope> Scopes { get; }

		/// <summary>Gets the explicitly mapped external entry-label attachment.</summary>
		/// <value>Null for no mapping attachment, or the supplied immutable annotations.</value>
		/// <remarks>B18: preserve present-empty versus absent and all duplicate label
		/// occurrences. Entry occurrence positions are local to this attachment.
		/// This excludes native entry annotations already preserved in NativeContext.
		/// A sensitivity-looking application property supplies no annotation by itself.
		/// Neither absence nor an empty attachment removes inherited labels.</remarks>
		public LogAnnotations Annotations { get; }

		/// <summary>Gets source native facts without recapturing or extending native context.</summary>
		/// <value>The original completed native context, or null when none is available.</value>
		/// <remarks>B19: retain its event time, native frames, entry and scope annotations,
		/// effective identities and all origins without reindexing. Its origin ScopeIndex
		/// still addresses its own Labels.ScopeAnnotations, never this entry's Scopes.
		/// This property is source data, not the current host's scope stack or evidence
		/// of current-frame correspondence for a supplied-base direct call. Getters
		/// do not open scopes, restamp time or bypass current recipient eligibility.</remarks>
		public LogContext NativeContext { get; }

		/// <summary>Gets ordered bridge-owned facts outside the application-property namespace.</summary>
		/// <value>A non-null read-only collection with the six entries specified below.</value>
		/// <remarks>B20: positions and exact ordinal keys are:
		/// 0 "ProphetsWay.CategoryName" = CategoryName;
		/// 1 "ProphetsWay.EventId" = boxed present EventId, or null;
		/// 2 "ProphetsWay.OriginalLevel" = boxed OriginalLevel;
		/// 3 "ProphetsWay.NativeContext" = NativeContext;
		/// 4 "ProphetsWay.Annotations" = Annotations;
		/// 5 "ProphetsWay.Scopes" = Scopes.
		/// Include absent values as null without inventing payload. These values agree
		/// with the typed getters and retain attachment/frame boundaries. No application
		/// pair is moved, overwritten or removed even when its key is identical. These
		/// are payload facts, not instructions, authenticated labels or guard tokens.
		/// Collection-interface mutation throws NotSupportedException. There is no
		/// writable membership backing or lazy source; view identity is unspecified.</remarks>
		public ReadOnlyCollection<KeyValuePair<string, object>> BridgeMetadata { get; }

		/// <summary>Gets the original state through the common entry view.</summary>
		/// <value>The generic State value, boxed when necessary; null remains null.</value>
		/// <remarks>B21: preserve reference-type identity and value-type value, including
		/// opaque/scalar/default state. Boxing identity is unspecified; reachable
		/// reference members are not deep-copied. Do not substitute Properties or text.</remarks>
		public abstract object OriginalState { get; }

		/// <summary>Gets the declared producer state type retained by the generic entry.</summary>
		/// <value>Exactly typeof(TState), non-null even when State is null.</value>
		/// <remarks>B22: this is the declared generic type, not a runtime-subtype inference
		/// and not a request to redirect the native exact-type metadata route.</remarks>
		public abstract Type StateType { get; }

		/// <summary>Gets the original typed producer delegate through a common view.</summary>
		/// <value>The same Func&lt;TState, Exception, string&gt; delegate as ProducerFormatter,
		/// viewed as Delegate, or null when no producer formatter was supplied.</value>
		/// <remarks>B23: no object-typed replacement delegate or closure is substituted.
		/// Possession exposes an executable capability, not a pure or sanitized function.
		/// Reading it does not invoke it; cached FormattedMessage remains independent
		/// of any later caller invocation or mutation of reachable state.</remarks>
		public abstract Delegate OriginalProducerFormatter { get; }

		private static IEnumerable<LogBridgeScope> ValidateScopes(IEnumerable<LogBridgeScope> scopes)
		{
			foreach (var scope in scopes)
			{
				if (scope == null)
				{
					throw new ArgumentException("A scope frame cannot be null.", nameof(scopes));
				}
				yield return scope;
			}
		}
	}
}