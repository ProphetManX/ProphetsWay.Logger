using System;
using Microsoft.Extensions.Logging;

namespace ProphetsWay.Utilities.LoggerDestinations
{
	/// <summary>Forwards permitted ordinary Logger entries to a supplied Microsoft logger or factory.</summary>
	/// <remarks>
	/// <para>O01-O05: installs only an explicitly registered outbound recipient, never
	/// an inbound provider or another destination. Ordinary and exact-T registrations,
	/// settings, insertion order and fallback suppression retain Logger's native rules.
	/// An enabled rejecting registration remains active and never revives fallback.
	/// Inherited public Log and LogWithContext keep the supplied base's nonvirtual
	/// argument, full-mask, current-frame correspondence and intrinsic LabelPolicy
	/// guards. Direct use performs no registration lookup. A supplied source carrier
	/// or old context grants no direct-call exemption. Every public direct invocation
	/// is new and receives its newly captured event time; an accepted current context
	/// contributes unchanged frame/label facts, not its old time. Trusted LogCaptured
	/// instead preserves the admitted call's context/time and captured policies.
	/// Registered delivery enforces both registration and intrinsic restrictions before
	/// this recipient sees payload. Denial invokes no Microsoft factory/logger method,
	/// recipient formatter, scope export or rescue. Native context capture needed for
	/// selection may already have occurred. No new native public member is required.</para>
	/// <para>O04-O05: calls are synchronous and non-idempotent, with concurrent calls
	/// permitted and no cross-thread total order or serialization of consumer code.
	/// Complete snapshots precede callbacks; callback registration/policy changes affect
	/// later captures, not this attempt. No registry or adaptation lock encloses foreign
	/// callbacks. The supplied factory or fixed logger is borrowed. A logger obtained
	/// from that factory remains factory-managed, not individually owned by this adapter.
	/// Removal/clear/replacement does not dispose them or drain old captures. Captured
	/// users finish; hosts quiesce them before borrowed-resource disposal. Adaptation
	/// owns only its transient transport, private call bookkeeping and returned export
	/// cleanup handles. Those stay available through their admitted attempt and unwind
	/// before it finishes. No persistent owned logger, category cache, new disposal
	/// protocol, queue, retry, process-exit flush or global shutdown is selected.
	/// Retaining recipients own their copies and later completion; scope end is not
	/// erasure. Consumers still own classification, sanitization, readership, storage,
	/// retention and costs. This bridge is not a sandbox or a delivery receipt.</para>
	/// <para>O08-O10/O16/O24: after complete native permission, factory mode calls
	/// CreateLogger once for the attempt with the available originating CategoryName,
	/// or defaultCategoryName only when that origin is null. Empty/whitespace supplied
	/// categories are real values, not absence. No call-stack or runtime-type category
	/// inference occurs. Fixed mode uses exactly its supplied logger and never claims
	/// to inspect or change that logger's category. Retain the origin in the entry even
	/// when fixed routing differs. Explicitly map the lowest set native message bit:
	/// Critical to Microsoft Critical; ErrorOnly to Error; WarningOnly to Warning;
	/// InformationOnly to Information; DebugOnly to Debug; TraceOnly to Trace. Retain
	/// the full original mask; 9 yields one Critical invocation, not multiple records.
	/// Do not cast native flags to Microsoft ordinals. Call the selected logger's
	/// IsEnabled once with that mapped level. False is normal rejection: no BeginScope
	/// or Log, no failure or fallback. True permits the one export attempt; it is not
	/// a promise that foreign configuration cannot change. Neither this query nor
	/// CreateLogger is the native ValidateMessageLevel callback. Shared native bridge
	/// capture can have completed before the Microsoft query. No Microsoft None or
	/// unknown ordinal is produced from a valid native mask.</para>
	/// <para>O11-O14: for a fresh ordinary native call create a completed
	/// LogBridgeEntry of string whose State is the supplied native message. This is
	/// the adapter's direct use of the actual string argument as Microsoft state,
	/// not discovery of an additional producer field. RawMessage and FormattedMessage
	/// both retain that already available string, including null, empty or whitespace.
	/// There was no producer delegate: ProducerFormatter is null. CategoryName and
	/// EventId are null because native ordinary input supplies neither. Properties
	/// and external Scopes are empty; Annotations is null because no external mapping
	/// occurred. The native entry/scope annotations remain in NativeContext, which
	/// is this call's complete native capture. OriginalLevel and Exception retain
	/// the actual mask and original optional exception. No helper-specific message
	/// or exception requirement is imposed on raw/direct input.</para>
	/// <para>O12-O14: the generic companion uses its exact declared T metadata as
	/// State in LogBridgeEntry of T, including null/default(T), with no marker or
	/// runtime-subtype routing. Its raw/captured text, absent producer/category/event,
	/// exception and native-context rules are otherwise the same. Recognize only
	/// IEnumerable of KeyValuePair of string and object as event-property input;
	/// copy that finite stable membership once for the shared outbound capture.
	/// Other metadata remains opaque with empty Properties. Preserve duplicate,
	/// null/empty/whitespace keys, null values and default pairs in original order.
	/// Preserve original state and nested references; do not inspect arbitrary getters,
	/// call implicit object ToString, enumerate nested values, deep-clone, reverse-parse
	/// text or classify annotation-shaped metadata. Later source membership edits
	/// cannot affect the completed capture; mutation during capture and termination
	/// of infinite inputs are unsupported. Capture includes sequence acquisition,
	/// iteration, element access and disposal, with no rollback of caller effects.
	/// Native bridge preparation is shared by this call's admitted outbound recipients,
	/// not repeated independently inside each LogCore. No preparation is needed if
	/// native gates reject every outbound recipient. A failed shared bridge capture
	/// withholds every affected outbound attempt; independent native recipients that
	/// do not consume that bridge capture still follow their own native contract.
	/// No recipient receives a partial bridge record or an unlabeled substitute.</para>
	/// <para>O15-O20: a specifically trusted controlled handoff instead carries its
	/// already completed original entry. Preserve its declared state type, original
	/// state/delegate association, captured text, optional raw text, exception, event
	/// Id/Name, source category and full native mask. Never wrap that entry as a new
	/// original state, execute its producer delegate again, or treat the transport
	/// formatter as a producer. Untrusted public/direct calls with a retained entry
	/// as metadata remain fresh exact-T calls, with that object as original metadata;
	/// they do not gain its archived labels or guard authority automatically.
	/// Preserve each source/current native LogContext intact, including original time,
	/// frames, entry annotations and native origins. ScopeIndex continues to address
	/// that context's label-only ScopeAnnotations, never all frames or external frames.
	/// Additional current-host native captures travel separately, not by rewriting
	/// the entry's NativeContext. Carry every external frame and mapped attachment,
	/// including empty/opaque/scalar/repeated frames and absent versus present-empty
	/// annotations. Preserve retained source order followed by new host order where
	/// there is no joint observed order, as in the accepted inbound contract.
	/// Native and external captures are different domains, not an invented global
	/// chronology. Complete eligibility uses the union of all source/current native
	/// and explicitly mapped external labels. Deduplicate identities only for matching;
	/// preserve attachment occurrences and their original indexing. Null/empty inner
	/// attachments and application keys cannot subtract inherited labels. New host
	/// facts must be captured and included before recipient selection, then carried
	/// onward privately as well as in the transport views. Public native contexts
	/// remain native captures, not containers for fabricated external annotations.</para>
	/// <para>O21: call ILogger.Log with MicrosoftLogTransport as its declared state
	/// type. The event envelope contains exactly these three ordered ordinal keys:
	/// 0 "ProphetsWay.Entry" = the completed LogBridgeEntry;
	/// 1 "ProphetsWay.NativeContexts" = a non-null ReadOnlyCollection&lt;LogContext&gt;;
	/// 2 "ProphetsWay.ExternalScopes" = a non-null ReadOnlyCollection&lt;LogBridgeScope&gt;.
	/// The native list contains the available source capture first, then additional
	/// captured native contexts in controlled-hop encounter order, including current
	/// context. A capture already carried as that same capture is not appended again.
	/// Overlapping views of an opening do not create new label attachments. The
	/// external list is the complete composed external sequence, retaining the entry's
	/// source frames and any new host frames without modifying that entry. Its positions
	/// identify that transport view; source carrier positions remain unchanged.
	/// Lists own completed membership; nested values remain originals. On a fresh
	/// native call the native list has this call's one context and the external list
	/// is empty. The entry's six existing BridgeMetadata keys and values remain
	/// unchanged inside the entry. These three outer grouping keys are not additions
	/// to that six-key collection. No application pair is moved to the envelope's top
	/// level, normalized, flattened, overwritten or dropped, even with matching keys.
	/// The entry preserves source annotations; other contexts preserve their native
	/// attachments; external frames preserve their attachments and occurrence positions.
	/// All are available to a cooperating receiver, not automatically serialized.</para>
	/// <para>O16/O23-O24: pass the retained EventId when present. When absent, use
	/// default(EventId) solely for ILogger.Log's non-nullable argument; the entry's
	/// EventId remains null, so controlled return does not fabricate an original event
	/// identity. Pass the original exception separately, including null at any valid
	/// severity. Supply a non-null transport formatter that returns this delivery's
	/// captured FormattedMessage unchanged, including null. Invoking it does not call
	/// the original producer, inspect state/exception, massage exception text or apply
	/// native text framing. Repeated foreign formatter invocations return that same
	/// captured text, not repeated producer work. Original formatter identity remains
	/// in the entry; this transport delegate is not substituted for it. The bridge
	/// performs one ILogger.Log invocation after successful required scope openings;
	/// arbitrary factory fanout is not a promise of one remote or persisted record.</para>
	/// <para>O22/O25: for each frame to replay, pass a separate MicrosoftLogTransport
	/// to BeginScope. A native-frame envelope contains exactly one pair with key
	/// "ProphetsWay.NativeScope" and the original LogScopeFrame as value. An external
	/// frame envelope instead has key "ProphetsWay.ExternalScope" and its original
	/// LogBridgeScope. Frame Properties/State/Annotations stay inside their own frame;
	/// there is no merged event/scope dictionary. Replay native captures in their
	/// transport-list order and each capture's outer-to-inner frame order, then the
	/// composed external sequence in its retained order. Replay a native opening only
	/// once where private controlled provenance proves correspondence across captures;
	/// equal values, annotation objects or application keys alone prove nothing.
	/// Distinct openings and repeated external occurrences remain distinct. These
	/// deterministic domain groups do not assert cross-domain chronological nesting.
	/// Existing Microsoft host scopes remain in place outside these added openings;
	/// never replace, pop or edit them. Scope support belongs to the selected logger.
	/// Captured frame facts remain in the event envelope even when no scope handle is
	/// returned. Private replay correspondence lets controlled inbound capture retain
	/// each exported occurrence once without mapping it again. Newly observed host
	/// frames keep the accepted inbound composition/mapping rules. Unknown foreign
	/// correspondence is not guessed from state equality or rendered text.</para>
	/// <para>O26-O27: ILogger.BeginScope may return null; that means no cleanup handle
	/// for that opening, not a failed capture or a synthetic failure. Continue the
	/// remaining openings and Log. This is distinct from the inbound scope source's
	/// required Push handle. Save each non-null returned handle by successful opening
	/// occurrence, including separate occurrences that return the same reference.
	/// On normal return or failure, attempt each such cleanup once in reverse opening
	/// order. If an opening throws, do not open subsequent frames or call Log for
	/// this recipient; unwind the handles already obtained. If Log or cleanup throws,
	/// continue remaining older cleanup attempts. Never retry a failed handle, dispose
	/// raw state/frames/values, or end native/current host scopes. Foreign code may have
	/// effects before throwing; no rollback, recovery of an unreturned handle, repair
	/// of a broken foreign scope stack or asynchronous cleanup guarantee is supplied.
	/// Keep any original failure fact despite later cleanup failures. One or more
	/// export/cleanup failures make this recipient one Output failure, not one failure
	/// descriptor per exported frame. Cleanup-only failure is also Output.</para>
	/// <para>O28-O29: transfer entry identity and visited native-route membership only
	/// through the specifically controlled outbound invocation and its matching inbound
	/// receipt. Private per-entry context is outside every enumerable key, public carrier
	/// and safe report. Neither carrier/transport reference alone, matching text nor an
	/// ambient "currently exporting" flag authorizes a controlled return. Mark actual
	/// ordinary/exact-T route visits when those routes are entered, not destinations
	/// as globally visited; a direct attempt fabricates no registered route. Suppress
	/// only re-entry of a visited route for that same controlled entry, before payload
	/// work, without abandoning other original recipients. Fresh public/native/Microsoft
	/// calls, including callback logging during factory creation, enablement, scope
	/// opening/cleanup, mapping, formatting or delivery, are independent unless they
	/// are the specifically bound controlled transfer itself. A retained carrier used
	/// outside that transfer starts a new call. Application property edits cannot
	/// create/reset the guard. Restore private propagation and replay bindings on every
	/// exit, including capture, callback, cleanup and reporting failures. Do not reuse
	/// Logger's reporting-suppression flag as scope or route state. No public reset,
	/// global duplicate store, creator/fork detector or instrumentation is introduced.</para>
	/// <para>O30-O33: use the admitted native call's failure accumulator. Shared
	/// property/provenance capture failure contributes one core capture fact; opted-in
	/// label-check failure is recipient-local LabelCheck. Alone these retain default
	/// safe-report-and-return and any separately reviewed explicit strict treatment;
	/// this API supplies no strict selector. Native custom severity failures remain
	/// Eligibility with mandatory propagation under D022. Factory CreateLogger failure,
	/// a null factory result, or a selected Microsoft IsEnabled, BeginScope, Log or
	/// cleanup failure is recipient Output, requiring safe propagation after other
	/// independently eligible attempts and cleanup. No foreign error-text inspection
	/// selects policy. A later shared capture failure cannot erase an already observed
	/// mandatory failure. Deliberate rejection adds no failure or fallback rescue.</para>
	/// <para>O32: one guarded root call and only its specifically bound controlled
	/// continuations share one failure accumulator and one completion. The root is
	/// the originating ordinary, exact-T or supplied-direct guarded invocation; a
	/// fresh Microsoft ingress uses its admitted exact LogBridgeEntry route as root.
	/// A newly entered controlled route is part of that completion, not a new report.
	/// Public recursive calls and unrelated callback logging remain independent roots
	/// with their own captures, positions and completion, under O28-O29 and the native
	/// reporting-recursion rule. A public carrier, transport reference, exception type
	/// or CorrelationId cannot associate an independent call with this completion.</para>
	/// <para>O32: define this controlled call's full diagnostic membership by ordered
	/// blocks of captured native recipient slots. The root block is first. A registered
	/// block contains its entire captured insertion-order membership and settings,
	/// including disabled, rejected and successful slots. Include the native implicit
	/// final automatic-recipient slot exactly when that captured route has no enabled
	/// compatible explicit registration. An enabled rejecting registration still
	/// suppresses that slot. A supplied-direct root block contains exactly its one
	/// recipient at position 1, without a fabricated registration or registry lookup.
	/// Reserve the complete block before attempting any recipient in that block;
	/// do not append individual slots as their callbacks happen or failures occur.</para>
	/// <para>O32: when the existing private handoff first enters another unvisited
	/// native route for this entry, capture that route at its accepted admission
	/// boundary and append its complete block after all previously admitted blocks.
	/// Blocks are ordered by this call's controlled route-entry encounters, root first;
	/// slots within each block retain native insertion order. A block's prefix is the
	/// total number of slots in earlier blocks. RegistrationId is that prefix plus
	/// the recipient's positive one-based local position. Root positions are therefore
	/// unchanged. Later blocks never insert into, compact or renumber earlier blocks.
	/// This orders diagnostic membership only: it does not change recipient attempt
	/// order, impose a joint atomic capture of different routes, precapture a route
	/// before it is entered, or promise a cross-thread total order. Each route keeps
	/// its original captured membership, settings, policies, context and event-time
	/// rules; callback changes can affect later captures, never completed ones.</para>
	/// <para>O32: a visited-route return suppressed under O28-O29 contributes no new
	/// block, recipient attempt or failure. A no-op that enters no native route
	/// contributes no block. A completed block keeps all its slots even if subsequent
	/// checks reject them or shared capture fails. If no complete membership is
	/// obtained, invent no partial block or recipient descriptor for that failure;
	/// it remains the existing core-capture fact. Different compatible registrations
	/// of the same destination object are different captured slots, not an object-wide
	/// duplicate to remove. External loggers/providers and exported scope handles are
	/// not additional native recipient slots. NativeContexts, scope/origin indices,
	/// labels and transport pairs do not supply or alter this diagnostic numbering.</para>
	/// <para>O32: retain the first eight or fewer original recipient failures across
	/// the entire shared accumulator in actual failure-encounter order, not ascending
	/// RegistrationId or block order. The eight-descriptor bound applies during
	/// accumulation, not separately per route followed by collect-all-and-trim.
	/// Each distinct failed recipient slot contributes once with its observed native
	/// stage; each additional failed slot beyond the first eight increases OverflowCount
	/// once. Several export or cleanup faults for one recipient remain one Output
	/// fact under O26-O27. Shared capture contributes only CoreCaptureFailureCount,
	/// zero or one for this entry, never a position-zero descriptor or overflow.
	/// Core-only failure has empty Failures and zero OverflowCount. Total remains
	/// CoreCaptureFailureCount plus Failures.Count plus OverflowCount, widening before
	/// addition. The eventual report has one fresh nonempty generated CorrelationId
	/// for these safe immutable facts. Positions are meaningful only with it, never
	/// stable identifiers, hashes, recipient names or lookup capabilities.</para>
	/// <para>O33: associate observed failures with their admitted block and local
	/// slot inside the private integration boundary, before raw provenance is lost.
	/// Translate to the call-wide position before bounded recording. A trusted nested
	/// result returns already-accounted safe facts and mandatory status to the same
	/// accumulator; it is neither notified again nor counted as another failure of
	/// the exporter merely because it traverses that exporter. Do not reconstruct
	/// attribution by merging independently completed reports or reading exceptions.
	/// A default-treated nested capture/check result does not become Output through
	/// forwarding. An actual independent foreign callback throw still contributes
	/// that exporter's Output fact at its own captured position. A caught exception's
	/// type or public Report is never proof of a trusted already-accounted result.
	/// Independent recursive calls keep their own results; an uncaught error from
	/// such a call can still fail its caller's foreign callback under that boundary.</para>
	/// <para>O33: the outer guarded completion, not LogCore or a nested continuation,
	/// reports and applies the retained return/throw policy after independent eligible
	/// attempts and export cleanup. Native Eligibility and actual Output failures
	/// remain mandatory; shared capture and LabelCheck alone retain default safe
	/// reporting-and-return and any separately reviewed explicit strict treatment.
	/// This API adds no strict selector. A later capture or reporter failure cannot
	/// erase an already observed mandatory failure. With no original failure there
	/// is no notification. Preserve individual subscriber containment, the native
	/// recursive-notification suppression rule and best-effort stderr of at most
	/// 512 UTF-16 units including terminator. Reporter failure changes neither the
	/// original safe facts nor required propagation. Restore private state on every
	/// exit, including cleanup and reporting failures, without suppressing new calls.</para>
	/// <para>O32-O33: the full diagnostic membership is a definition for attribution,
	/// not a new public collection or report backlink. Reports and LogDispatchException
	/// retain only the existing generated identifiers, stage codes, counts and permitted
	/// fixed guidance. They retain no route/block/snapshot object, destination, category,
	/// event, state, text, original exception cause, path, label/origin, delegate, scope
	/// handle or private control backlink. No membership map is exported in a payload
	/// key or added to a representation/native context. The existing exception's safe
	/// views and immutable report rules remain unchanged. No raw cause archive, effect
	/// rollback, rescue destination, new reporting channel or completed-call history
	/// is added.</para>
	/// <para>O05/O29: all fidelity and private-context guarantees concern the controlled
	/// adapters and available facts. A foreign provider may ignore structured values,
	/// scopes or the formatter, discard private context, retain data or do its own
	/// fanout. This is not arbitrary-provider persistence, confidentiality, framing,
	/// exactly-once output, remote durability or prevention of arbitrary custom recursion.</para>
	/// </remarks>
	public sealed class MicrosoftLoggerDestination : BaseLoggingDestination
	{
		private readonly ILoggerFactory _factory;
		private readonly ILogger _logger;
		private readonly string _defaultCategoryName;
		/// <summary>Creates an ordinary destination using per-entry factory category routing.</summary>
		/// <param name="reportingLevel">Required known-bit destination mask, 0 through 63; zero is active reject-all.</param>
		/// <param name="factory">Required borrowed Microsoft logger factory.</param>
		/// <param name="defaultCategoryName">Required fallback category for absent origin; defaults to "ProphetsWay.Logger". Empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">factory or defaultCategoryName is null; ParamName names the argument.</exception>
		/// <exception cref="ArgumentOutOfRangeException">reportingLevel is negative or contains unknown bits; ParamName is "reportingLevel".</exception>
		/// <remarks>O06/O08: validate arguments before any foreign operation; no competing-invalid
		/// priority is promised. Retain immutable constructor choices and inherited initial
		/// NoFilter LabelPolicy. No CreateLogger, IsEnabled, scope, formatting, registration,
		/// output, file probe or disposal occurs during construction. Each construction
		/// creates an independent destination; it does not deduplicate or install it.</remarks>
		public MicrosoftLoggerDestination(LogLevels reportingLevel, ILoggerFactory factory,
			string defaultCategoryName = "ProphetsWay.Logger") : base(reportingLevel)
		{
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));
			if (defaultCategoryName == null)
				throw new ArgumentNullException(nameof(defaultCategoryName));

			_factory = factory;
			_defaultCategoryName = defaultCategoryName;
		}

		/// <summary>Creates an ordinary destination using one fixed Microsoft logger.</summary>
		/// <param name="reportingLevel">Required known-bit destination mask, 0 through 63; zero is active reject-all.</param>
		/// <param name="logger">Required borrowed fixed logger; its category remains its own.</param>
		/// <exception cref="ArgumentNullException">logger is null; ParamName is "logger".</exception>
		/// <exception cref="ArgumentOutOfRangeException">reportingLevel is negative or contains unknown bits; ParamName is "reportingLevel".</exception>
		/// <remarks>O07/O09: validate before foreign operations without competing-invalid
		/// priority. Retain this exact logger, no factory or inferred category. Initial
		/// LabelPolicy remains NoFilter. Construction performs no callback, output,
		/// registration, scope operation, ownership transfer or disposal. Repeated
		/// construction creates independent destinations, not idempotent registration.</remarks>
		public MicrosoftLoggerDestination(LogLevels reportingLevel, ILogger logger) : base(reportingLevel)
		{
			if (logger == null)
				throw new ArgumentNullException(nameof(logger));

			_logger = logger;
		}

		/// <summary>Exports one admitted ordinary entry using the completed shared bridge facts.</summary>
		/// <param name="context">Non-null selected native capture; never replaced by ambient recapture.</param>
		/// <param name="level">Validated permitted full native mask, 1 through 63.</param>
		/// <param name="message">Original optional text; null, empty and whitespace are preserved.</param>
		/// <param name="ex">Original optional exception being logged.</param>
		/// <exception cref="Exception">An actual Microsoft callback or adaptation output fails; the enclosing guarded dispatch converts it to its safe Output result.</exception>
		/// <remarks>O03/O08-O33: all type-level export, provenance, ordering, cleanup
		/// and failure rules apply. This hook is not a new direct entrypoint. Shared
		/// capture/label selection has its own preceding integration boundary; do not
		/// implement it as an unclassified throw from this hook. A null CreateLogger
		/// result is an invalid output operation, never permission to use another logger.
		/// Always unwind successful scope handles before returning an export failure
		/// to the enclosing accumulator. Its O32 block/slot association locates this
		/// recipient; O33 carries trusted downstream facts without attributing them
		/// to this recipient or emitting a second local DispatchFailed. The hook does
		/// not create or merge another completed report. An actual export/cleanup
		/// failure still records this recipient's own Output fact.</remarks>
		protected override void LogCore(LogContext context, LogLevels level,
			string message, Exception ex)
		{
			Logger.ExportMicrosoft(_factory, _logger, _defaultCategoryName,
				context, level, message, message, message, ex);
		}
	}
}
