using System;
using Microsoft.Extensions.Logging;

namespace ProphetsWay.Utilities
{
	/// <summary>Implements the inbound ILogger returned by MicrosoftLoggerProvider.</summary>
	/// <remarks>I13: assembly-created facade only; consumers use ILogger. The provider's
	/// concurrency, ownership, route and retirement rules apply to every method. No
	/// independent logger disposal, configuration, state inspection or public constructor
	/// is introduced. These XML rules describe observable calls through the returned
	/// Microsoft interface, not an additional public implementation requirement.</remarks>
	internal sealed class MicrosoftLogger : ILogger
	{
		private readonly MicrosoftLoggerProvider _provider;
		private readonly string _categoryName;
		/// <summary>Associates a facade with an existing provider and unchanged category.</summary>
		/// <param name="provider">Required owning provider, including an already retired provider.</param>
		/// <param name="categoryName">Required category; empty and whitespace remain valid.</param>
		/// <exception cref="ArgumentNullException">provider or categoryName is null; ParamName names it.</exception>
		/// <remarks>I14: association only; no callbacks, capture, registrations or output.
		/// Competing-invalid argument priority is unspecified. Provider lifetime controls
		/// subsequent operations; constructing the facade does not reopen a retired provider.</remarks>
		internal MicrosoftLogger(MicrosoftLoggerProvider provider, string categoryName)
		{
			if (provider == null)
				throw new ArgumentNullException(nameof(provider));
			if (categoryName == null)
				throw new ArgumentNullException(nameof(categoryName));

			_provider = provider;
			_categoryName = categoryName;
		}

		/// <summary>Opens an external logical scope on this provider's current scope source.</summary>
		/// <typeparam name="TState">Unconstrained scope-state type.</typeparam>
		/// <param name="state">Original state, including null or default(TState).</param>
		/// <returns>A non-null local cleanup handle; inert when the provider has retired.</returns>
		/// <exception cref="InvalidOperationException">A live supplied scope source returns no cleanup handle.</exception>
		/// <exception cref="Exception">The live source's Push fails; no usable local handle is returned.</exception>
		/// <remarks>I15-I17: a live opening passes the original state to one captured
		/// source's Push; values use ordinary boxing, without graph cloning. No mapping,
		/// property enumeration, ToString, producer formatting, recipient call or native
		/// BeginScope occurs. Scope membership and explicit mapping are captured by Log,
		/// not frozen by this opening; source changes before that capture can be observed.
		/// All this provider's loggers can observe the source's active scopes regardless
		/// of category. The owned source follows normal execution-context inheritance
		/// across await and queued children, with child-local changes and cleanup; deliberate
		/// flow suppression adds no inheritance and does not revoke already captured work.
		/// Supplied sources provide their own scope enumeration/flow behavior; the bridge
		/// cannot repair a context-discarding foreign source.
		/// Use the returned handle only in the opening operation or normal continuation,
		/// not an independent child's disposal. Local LIFO and absence checks follow
		/// native LogScopeHandle: disposing below an active bridge-opened child throws
		/// InvalidOperationException without changing local state or invoking the source
		/// handle; disposing a locally absent opening is no-op. Correctly ordered cleanup
		/// invokes its original source handle once and ends the local opening even if that
		/// invocation throws. Such a foreign exception is a local cleanup failure, not a
		/// DispatchFailed report; no rollback, retry or repair of foreign state is promised.
		/// Ordering relative to handles opened directly on a supplied source is that
		/// source's responsibility. No universal inherited-handle misuse detector exists.
		/// A retired provider returns an inert repeat-disposable handle without Push or
		/// state inspection. Retirement does not end previously opened scopes; their
		/// supported local cleanup remains available. Never dispose the state, source,
		/// mapper, factory, Logger or destinations. Opening/ending a scope changes no
		/// completed capture and emits no logging notification, including on local failure.</remarks>
		public IDisposable BeginScope<TState>(TState state)
		{
			return _provider.BeginScope(state);
		}

		/// <summary>Checks whether the live inbound route has potential severity-level delivery.</summary>
		/// <param name="logLevel">A defined Microsoft LogLevel, including None.</param>
		/// <returns>False for None or retirement. Otherwise true when no compatible enabled
		/// explicit registration suppresses fallback, or at least one such registration's
		/// captured registration mask includes the mapped bit; false otherwise.</returns>
		/// <exception cref="ArgumentOutOfRangeException">logLevel is not a defined ordinal; ParamName is "logLevel".</exception>
		/// <remarks>I18-I19: use one complete exact LogBridgeEntry route snapshot, not
		/// another route. Map Trace=TraceOnly, Debug=DebugOnly, Information=InformationOnly,
		/// Warning=WarningOnly, Error=ErrorOnly and Critical=Critical. Never numerically
		/// cast Microsoft ordinals to native flags. This conservative precheck invokes
		/// no custom severity callback, intrinsic/registration label check, mapper,
		/// formatter, scope provider, file probe or output. True need not mean that a
		/// destination's intrinsic mask, custom check or labels will permit a later Log.
		/// An active rejecting registration never revives fallback. Configuration may
		/// change immediately; Log captures and enforces its own gates. Undefined values
		/// remain argument errors even after retirement; valid None is always false.</remarks>
		public bool IsEnabled(LogLevel logLevel)
		{
			return _provider.IsEnabled(logLevel);
		}

		/// <summary>Captures and synchronously attempts one enabled inbound entry.</summary>
		/// <typeparam name="TState">Unconstrained declared producer state type.</typeparam>
		/// <param name="logLevel">A defined Microsoft level; None performs no delivery.</param>
		/// <param name="eventId">Original whole EventId, including default and optional Name.</param>
		/// <param name="state">Original typed state, including null or default(TState).</param>
		/// <param name="exception">Optional original logged exception; absence is legal at every level.</param>
		/// <param name="formatter">Required original producer delegate; its result may be null, empty or whitespace.</param>
		/// <exception cref="ArgumentNullException">formatter is null; ParamName is "formatter".</exception>
		/// <exception cref="ArgumentOutOfRangeException">logLevel is not a defined ordinal; ParamName is "logLevel".</exception>
		/// <exception cref="LogDispatchException">A captured failure requires safe propagation after independent attempts and reporting.</exception>
		/// <remarks>I20-I31: validate formatter and ordinal before callbacks or delivery;
		/// no competing-invalid priority is specified. For valid arguments, None and
		/// provider retirement are complete no-ops, including mapping, scope callbacks,
		/// formatting and file establishment. Do not impose Error/Critical convenience
		/// helper preconditions or fabricate message/exception content. No prior IsEnabled
		/// call is required: capture this call's live admission, common exact route,
		/// settings, intrinsic policies and scope source, and enforce the actual severity
		/// and complete label gates before any recipient payload work. Ordinary native
		/// scopes and one UTC event time are captured before recipient callbacks.
		/// For fresh external input, recognize only IEnumerable of KeyValuePair of string
		/// and object as structured state. Copy its finite stable membership synchronously;
		/// other state remains opaque with an empty Properties collection. Apply the same
		/// rule to each external scope state obtained from the captured source's ForEachScope.
		/// Preserve enumeration order, duplicates, empty/unlabeled/repeated frames, null
		/// keys/values and default pairs. Include acquisition, iteration, element access
		/// and enumeration disposal in capture. No lazy membership, arbitrary getters,
		/// implicit object ToString, nested enumeration or rendered-text parsing is used.
		/// Original state/delegate/exception and nested values remain originals; later
		/// collection edits cannot alter completed membership. Mutation during capture
		/// and infinite input termination are unsupported; caller effects are not rolled back.
		/// Invoke the optional mapper for new external attachments during shared capture.
		/// Preserve native context and external attachments in their separate representation
		/// domains. Recipient selection uses the union of all source native, current native
		/// and explicit external labels, deduplicated only for identity matching; all
		/// attachment occurrences remain available. Neither inner labels, null mapping,
		/// metadata keys nor a missing source annotation can subtract inherited labels.
		/// Obtain registration and supplied-base intrinsic label permission before handing
		/// any message, exception, state, formatter, properties or scopes to that recipient.
		/// Each custom severity callback is invoked at most once for an attempted recipient,
		/// only after its immutable gates permit proceeding; false is rejection, not failure.
		/// If no recipient survives actual gates, never invoke formatter. Mapping/capture
		/// needed to establish those gates may already have executed. A route already
		/// rejected by its registration masks need not capture payload at all.
		/// When fresh delivery is needed, invoke formatter(state, exception) exactly once
		/// before any recipient receives the entry. Store that result as FormattedMessage,
		/// retain the exact delegate and TState, and leave RawMessage null: Microsoft Log
		/// supplied no separate raw text. Deliver the completed LogBridgeEntry&lt;TState&gt;
		/// as declared LogBridgeEntry, its captured text as the native message, and its
		/// original exception. Use the same completed facts for every permitted recipient,
		/// in captured insertion order, through exactly one contextual or legacy handoff.
		/// Contextual handoff carries the current native capture; the carrier's NativeContext
		/// separately retains its source native context. Fresh ingress uses that same native
		/// capture as its source. Never rewrite native origin indices to index external frames.
		/// A genuine controlled forward preserves the original carrier facts, full native
		/// mask, declared state type, state, delegate, cached text, event identity and source
		/// category; it does not nest the carrier as substitute original state or invoke
		/// the transport formatter as a new producer. Its retained external frames keep
		/// attachments; additional host external frames are copied/mapped separately and
		/// retained without flattening. Use observed source enumeration order when frames
		/// are observed together; otherwise preserve retained source order followed by new
		/// host order, without claiming a global chronological interleaving. Privately
		/// identified replay frames contribute their original facts once, not another
		/// mapping. Equal state/value references alone never identify a replay. Current
		/// host native facts remain a separate capture and participate in every selection.
		/// Controlled forwarding preserves the entry's original event time; a fresh
		/// invocation captures its own time, with no uniqueness or monotonicity promise.
		/// Private per-entry route context, not a public key or carrier reference alone,
		/// identifies a controlled return. Suppress a return to a visited common route
		/// before payload work, without suppressing other original recipients, unrelated
		/// nested calls or fresh identical-text entries. Application key edits cannot
		/// manufacture or clear that context. Every exit restores private propagation.
		/// An application passing a retained carrier as ordinary state outside a controlled
		/// handoff starts a new external entry; it does not inherit guard authority.
		/// Shared scope/property capture, mapping or producer-format failure withholds the
		/// incomplete entry from all affected recipients and contributes one core capture
		/// failure, never an unlabeled/partial substitute. Opted-in label-check failure
		/// withholds only its recipient and records LabelCheck. These alone report and
		/// return under current default policy; separately reviewed explicit strict
		/// selection, if present, retains R-15 treatment. This API adds no strict switch.
		/// Custom severity-callback failure records Eligibility and requires propagation;
		/// configured recipient rendering/handoff and automatic-output failures record
		/// Output and require propagation. Attempt all other independent eligible recipients
		/// before reporting/throwing. A later capture failure cannot erase an already
		/// observed mandatory failure. Deliberate rejection is not failure and triggers
		/// neither fallback rescue nor retry. Automatic file recovery/failure memory keeps
		/// its existing R-21 rules, including qualifying secondary establishment success.
		/// Aggregate one bounded core notification, at most eight recipient descriptors
		/// plus overflow, and best-effort stderr of at most 512 UTF-16 units. Safe reports
		/// and propagated LogDispatchException contain only existing generated identifiers,
		/// stage codes, counts and permitted fixed guidance: no category, event, raw state,
		/// text, label, path, exception cause, callback, scope or private-context backlink.
		/// Reporting failure neither replaces original facts nor prevents mandatory
		/// propagation. Recursive notification suppression never suppresses unrelated logs.
		/// Normal return is not a delivery receipt. No effect rollback, arbitrary third-party
		/// scope/persistence fidelity, background completion or global deduplication follows.</remarks>
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
			Exception exception, Func<TState, Exception, string> formatter)
		{
			if (formatter == null)
				throw new ArgumentNullException(nameof(formatter));

			var level = Logger.MapMicrosoftLogLevel(logLevel);
			_provider.Log(_categoryName, level, eventId, state, exception, formatter);
		}
	}
}
