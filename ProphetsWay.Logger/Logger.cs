using System;
using System.Collections.Generic;
using System.Globalization;
using ProphetsWay.Utilities.LoggerDestinations;

namespace ProphetsWay.Utilities
{
	/// <summary>Synchronously dispatches ordinary and exact-declared-type entries using captured route, scope and label facts.</summary>
	/// <remarks>A18-A40: validate required producer arguments before dispatch, capture complete
	/// ordered route membership and immutable registration settings atomically, and capture
	/// the current logical-flow frames plus explicit entry annotation before recipient callbacks.
	/// Capture produces one coherent LogContext; no recipient receives an incomplete context.
	/// The relative capture order of independent registry and flow state is not a global
	/// transaction or cross-thread ordering promise. Callbacks cannot change this call's capture.
	/// Disabled registrations are not attempted. Enabled registrations remain active for fallback
	/// suppression even if their masks or label policies reject all entries. Eligibility requires
	/// all registration mask bits, the destination's severity permission and the captured label
	/// policy's permission. No relative order among rejecting pre-handoff checks is promised;
	/// recipient-owned payload code always follows successful label selection. Logger invokes
	/// the custom severity callback at most once per recipient attempt, and must obtain a true
	/// result before handoff. A short-circuited rejection need not invoke that callback.
	/// Attempt independently eligible recipients in captured insertion order. A contextual
	/// recipient receives exactly one contextual handoff; otherwise it receives exactly one
	/// legacy raw handoff, never both. Preserve original message, exception and exact-T metadata
	/// values/references. No automatic metadata classification, arbitrary getter inspection,
	/// implicit object ToString or arbitrary deep cloning occurs in transport. Legacy recipients
	/// need not implement the optional context capability but receive no new context parameter.
	/// Label denial or check failure withholds all entry data from that recipient before any
	/// formatter, callback or export, including context and scope properties. Independent permitted
	/// recipients continue. Capture/check and configured severity/output failures retain their
	/// distinct reporting and propagation rules; they are not all unconditional throws.
	/// Configured severity-callback and output failures require bounded safe reporting and
	/// LogDispatchException after independent attempts, even if other recipients succeeded.
	/// Shared capture failure withholds the incomplete entry from all affected recipients and
	/// counts once; a recipient-local label-check failure withholds only that recipient. Alone,
	/// those failures report and return under the retained default treatment. Accepted explicitly
	/// selected strict treatment is conditional on a separately reviewed selector, not a switch
	/// supplied by these APIs. Any mandatory failure still requires propagation. Pure scope/value
	/// construction and argument errors are local errors, not fabricated failed dispatches.
	/// Registry coordination never encloses consumer code. Registration/settings and scope changes
	/// from callbacks affect subsequent captures, including recursive logging as a new call.
	/// Synchronous calls may run concurrently; arbitrary recipients/property values are not made
	/// thread-safe and no cross-thread total order is promised. Calls are not idempotent delivery:
	/// each invocation is a new attempt; earlier recipient effects are not rolled back or retried.
	/// Borrowed destinations are not disposed by registration or scope operations. Captured calls
	/// may finish after publication or scope end; hosts quiesce callers before disposing borrowed
	/// resources. Retaining recipients own capture before return and later completion/cleanup.
	/// Fallback is selected only for a route without an enabled compatible explicit registration,
	/// never because of denial or failure. Ordinary and exact-T routes remain distinct, including
	/// when another route is populated. Automatic file behavior follows the session contract below;
	/// adaptation is outside this slice.
	/// Reports expose only generated identifiers, boundary codes and counts, never entry/context,
	/// raw failure causes or backlinks. Secondary reporters cannot change original result policy.
	/// Recursive notification suppression is not entry suppression or a scope-propagation store.
	/// These APIs add no disposal of Logger, global drain, background queue, durability guarantee,
	/// classification, redaction, scope ownership transfer or access-control service.
	/// <para>F01-F05: each validated ordinary or exact-T invocation captures one UTC
	/// event timestamp before recipient callbacks as part of its completed context.
	/// Trusted handoffs and any internal forwarding of that same invocation retain
	/// its time and membership; recursive calls capture anew. The timestamp is
	/// independent of filename allocation and promises neither uniqueness nor order.</para>
	/// <para>S01-S08: ordinary and each exact declared-T route select their automatic
	/// recipient only when the original captured route has no enabled compatible
	/// explicit registration. An enabled rejecting or failing recipient still suppresses
	/// that route's default. An automatic recipient is an implicit final captured slot,
	/// not a published explicit registration or a forward to another route. Its mask is
	/// Trace and it adds no label filter. Typed fallback uses the native typed renderer
	/// and retains metadata, including null/default(T), with the original captured
	/// context and event time. No public direct-call guard is re-entered.</para>
	/// <para>S09-S20: all automatic routes of this loaded static Logger session share
	/// one owner and one selected UTF-8 pathname. Eligibility and complete native
	/// rendering precede initial establishment. ConfigureAutomaticFileHostDirectory
	/// can replace only the primary root, and only before establishment starts; it
	/// does not determine the secondary component. The normal primary is the consuming
	/// host's AppContext.BaseDirectory. The secondary is one application-associated
	/// component directly under Environment.GetFolderPath with
	/// Environment.SpecialFolder.LocalApplicationData, as specified below.</para>
	/// <para>S09.A-C: on the coordinated transition to Initializing, freeze the selected
	/// primary override and capture AppContext.BaseDirectory once, resolving its full
	/// path or unavailable result. This happens in guarded automatic output, not in
	/// configuration or static initialization. Null, empty, failed retrieval, a path
	/// that is not fully qualified, or Path.GetFullPath failure means unavailable.
	/// Require full qualification before normalization: a complete platform root
	/// independent of current directory, current drive and per-drive current directory.
	/// On Windows, drive-relative C:folder and current-drive-rooted \folder are not
	/// fully qualified; complete drive-absolute/UNC roots follow the executing BCL's
	/// supported filesystem-path rules. On Unix the root is /. Do not manually trim
	/// whitespace, expand environment variables, substitute roots, inspect permissions
	/// or existence, or resolve physical identity/links. Otherwise use the executing
	/// BCL's full-path semantics and preserve case for filesystem use. An unavailable
	/// host base cannot prevent using a configured primary override. Do not recapture
	/// the host value for secondary selection or later calls.</para>
	/// <para>S09.D-E: derive the secondary naming key from that full host base, never
	/// from the explicit override. Remove trailing native or alternate directory
	/// separators only while the string is longer than Path.GetPathRoot(fullHostBase),
	/// retaining the complete root. On Windows replace backslashes in that result
	/// with /. On other platforms leave remaining characters unchanged. Preserve case
	/// and every other character ordinally; do not normalize Unicode, trim additional
	/// whitespace, expand variables or extract a filename/stem. Only this key is
	/// transformed, not the filesystem root. Encode the complete key as strict UTF-8
	/// without a BOM, equivalent to new System.Text.UTF8Encoding(false, true).GetBytes(key).
	/// An unpaired UTF-16 surrogate makes derivation unavailable, not a replacement
	/// character or fallback name. SHA-256 those bytes and render all 32 digest bytes
	/// in order as 64 lowercase ASCII hexadecimal digits, without separators or
	/// truncation. Prefix exactly app- to form one 68-character component. No salt,
	/// randomness, event time, process ID or per-run token participates; derivation
	/// failure makes the component unavailable. Record encoding remains S19's rule.</para>
	/// <para>S09.F-G: only after qualifying initial primary failure, attempt secondary
	/// resolution. Derive the component first from the captured host base. If that base
	/// or derivation is unavailable, fail this secondary attempt without looking up
	/// LocalApplicationData or trying a substitute location. Otherwise obtain the
	/// stated LocalApplicationData value once and apply the same full-path availability
	/// rules. If unavailable, fail secondary establishment without substitution.
	/// Otherwise use exactly Path.Combine(fullLocalApplicationData, component), prepare
	/// the directory and perform S10/S11 exclusive allocation there. Path construction,
	/// preparation or reservation failure is establishment failure. No alternative
	/// profile/root, shortened name, CWD, Logger/entry/calling assembly, process
	/// installation, payload, metadata, labels, scopes or tenant identity supplies a
	/// fallback. A missing normal-primary base is an initial primary resolution failure
	/// only when no explicit override supplies that root. A successful primary never
	/// resolves the component or LocalApplicationData and is not failed by an unused
	/// secondary's unavailability. Location resolution is part of the required
	/// secondary attempt; failure to name it does not claim a file was opened.</para>
	/// <para>S09.H-I: equal naming keys share a component, including logical applications
	/// sharing a host base. Missing or shared entry assemblies do not affect selection.
	/// Physical aliases, casing and Unicode variants need not share a key/component,
	/// and digest collisions are not promised impossible. This is host-base association,
	/// not logical-application uniqueness, authentication, tenant separation, physical
	/// ownership, anonymization, confidentiality or a filesystem sandbox. Shared
	/// directories still require exclusive session-file allocation. Host paths, naming
	/// keys, components and digests stay out of safe reports and dynamic guidance.
	/// No new public identity input, reset or lifecycle service follows.</para>
	/// <para>S10-S20: invariant UTC session naming with exclusive collision allocation
	/// is independent of event time. One coordinated initial attempt establishes the
	/// primary or, only on initial pre-record primary establishment failure, the
	/// secondary. Successful secondary recovery is not a failed recipient. Unavailable
	/// required location/naming inputs count as establishment failure; record-rendering
	/// or record-encoding failure alone does not. If neither initial location is usable,
	/// remember a safe failure marker without caught causes and apply S21-S25's one
	/// Output failure, fresh safe guidance and mandatory propagation. Later
	/// default-dependent calls do not look up host/root inputs, derive names or probe
	/// paths/permissions again. After selection, append or create at that same pathname;
	/// writing or uncertain acceptance never permits replay, relocation or another
	/// session filename. Host, environment and CWD changes cannot move it. Removing or
	/// disabling explicit routes never resets path or failure memory. An explicit route
	/// bypasses that memory only for itself. A new loaded session starts fresh. Ordinary
	/// explicit construction/configuration errors remain local, not sanitized dispatch
	/// reports. No automatic cleanup of incidental failed-allocation artifacts follows.</para>
	/// <para>S18-S25: append one completed native record and a suffix Environment.NewLine
	/// using UTF-8, without an encoding preamble, leading separator or content/identity
	/// policing. Session-owned file effects are serialized and handles released per
	/// attempt; rendering, consumer callbacks, registration publication and reporting
	/// remain outside session coordination. No global caller ordering or durability
	/// follows. A failed automatic recipient contributes one Output descriptor at its
	/// captured position, not a core capture count or one descriptor per attempted root.
	/// Apply existing independent attempts, bounded safe reporting and mandatory
	/// LogDispatchException. Failed initial establishment includes fixed private-path-safe
	/// guidance; secondary reporting cannot change the original result. Existing local
	/// argument errors, capture/check treatment and explicit borrowed ownership remain.</para></remarks>
	public static partial class Logger
	{
		/// <summary>Notifies observers of one bounded safe original-failure report.</summary>
		/// <remarks>B18/B19: shared by ordinary, exact-T and supplied-direct guarded calls. Report is
		/// non-null, including core-only reports. After applicable independent attempts, capture the
		/// subscriber list, invoke each entry synchronously in subscription order outside registry
		/// coordination, then attempt safe stderr text of at most 512 UTF-16 units including terminator.
		/// Use only fixed prose, generated IDs, boundary codes and counts, including the core count in
		/// totals. Contain each subscriber/writer failure; none changes original facts or throw/return.
		/// No subscriber is required. Normal delegate add/remove rules apply: null no-op, duplicates
		/// and multicast entries allowed, removal of the last matching subsequence, atomic publication;
		/// changes after capture affect later notifications. Retain subscribers until removed, with no
		/// disposal/drain. Do not report through Logger fanout or secondary destinations.
		/// On this synchronous managed-thread reporting stack, nested logging still runs with its own
		/// result policy, but emits no nested event or stderr; restore reporting after every exit.
		/// Other threads are independent; asynchronous/cross-thread cycles are not prevented. No callback
		/// serialization, timeout, guaranteed observation or process-fatal containment is promised.
		/// No original failure means no notification. Mandatory failures throw after reporting attempts;
		/// capture/check-only failures report and return. Reporting is neither a strict selector nor scope state.</remarks>
		public static event Action<LogFailureReport> DispatchFailed;

		private static readonly object DestinationLock = new object();
		private static readonly List<OrdinaryRegistration> OrdinaryDestinations = new List<OrdinaryRegistration>();
		private static readonly DestinationRegistrationSettings DefaultSettings = new DestinationRegistrationSettings(
			true, LogLevels.Trace, new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]));
		private const int RetainedFailureLimit = 8;
		[ThreadStatic]
		private static bool _reportingFailure;
		private static readonly AutomaticFileSession AutomaticSession = new AutomaticFileSession();

		/// <summary>Registers a borrowed recipient on the ordinary route with no added restrictions.</summary>
		/// <param name="newDest">Required non-null ordinary destination.</param>
		/// <exception cref="ArgumentNullException">newDest is null; ParamName is "newDest".</exception>
		/// <exception cref="ArgumentException">The same reference is already registered here;
		/// ParamName is "newDest".</exception>
		/// <remarks>A23-A25/A29: equivalent settings are Enabled true, ReportingLevel Trace
		/// (63), and NoFilter with empty configured labels. The recipient's severity behavior
		/// is retained, not inferred or replaced. Use reference identity, never user equality
		/// or hashing. Append once; duplicates, including disabled membership, are rejected
		/// without mutation. Other compatible routes remain independent. Publication completes
		/// before return without invoking/disposal of recipients or draining old captures.</remarks>
		public static void AddDestination(ILoggingDestination newDest)
		{
			AddDestination(newDest, DefaultSettings);
		}

		/// <summary>Registers a borrowed ordinary recipient with one complete settings value.</summary>
		/// <param name="newDest">Required non-null ordinary destination.</param>
		/// <param name="settings">Required immutable enablement, mask and label policy.</param>
		/// <exception cref="ArgumentNullException">newDest or settings is null; ParamName names that argument.</exception>
		/// <exception cref="ArgumentException">The same reference is already registered here;
		/// ParamName is "newDest".</exception>
		/// <remarks>A23-A26/A29/A38: append membership and copied immutable settings as one
		/// publication. Null/duplicate rejection publishes nothing; invalid-argument priority
		/// is unspecified. Disabled membership still occupies its insertion position and
		/// cannot be added twice. No destination getters, equality, callbacks or disposal are
		/// invoked. Old captures finish independently; future replacement changes only this
		/// registration, even when settings or the recipient are reused on another route.</remarks>
		public static void AddDestination(ILoggingDestination newDest, DestinationRegistrationSettings settings)
		{
			if (newDest == null)
				throw new ArgumentNullException(nameof(newDest));
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			lock (DestinationLock)
			{
				if (OrdinaryDestinations.Exists(registration => ReferenceEquals(registration.Destination, newDest)))
					throw new ArgumentException("The destination is already registered on this route.", nameof(newDest));

				OrdinaryDestinations.Add(new OrdinaryRegistration(newDest, settings));
			}
		}

		/// <summary>Atomically replaces all settings of an existing ordinary registration.</summary>
		/// <param name="destination">Required exact registered destination reference.</param>
		/// <param name="settings">Required immutable replacement settings.</param>
		/// <exception cref="ArgumentNullException">destination or settings is null; ParamName names that argument.</exception>
		/// <exception cref="ArgumentException">destination is not registered on this route;
		/// ParamName is "destination".</exception>
		/// <remarks>A23/A27/A29/A34: this is replacement, not an upsert. Preserve insertion
		/// position, recipient reference and every other route. Absence is checked in the same
		/// coordinated publication as replacement; failure leaves registry state unchanged.
		/// Publish enabled, mask and policy together before returning, without a drain or
		/// recipient invocation/disposal. Repeating the same values has the same effective
		/// settings; no view/reference identity or publication-version promise is made.
		/// Existing captures keep old settings. A concurrent remove/re-add is ordered by
		/// reference-based publication, not an undisclosed registration-generation token.
		/// No priority is promised between invalid arguments.</remarks>
		public static void SetDestinationSettings(ILoggingDestination destination, DestinationRegistrationSettings settings)
		{
			if (destination == null)
				throw new ArgumentNullException(nameof(destination));
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			lock (DestinationLock)
			{
				var index = OrdinaryDestinations.FindIndex(registration => ReferenceEquals(registration.Destination, destination));
				if (index < 0)
					throw new ArgumentException("The destination is not registered on this route.", nameof(destination));

				OrdinaryDestinations[index] = new OrdinaryRegistration(destination, settings);
			}
		}

		/// <summary>Removes a borrowed recipient from the ordinary route only.</summary>
		/// <param name="destToRemove">Exact instance; null or absent is a no-op.</param>
		/// <remarks>A17/A24/A28/A34: reference identity only. Preserve survivor order and
		/// other routes; remove its settings with membership. Return after publication,
		/// not after old captures finish. Never invoke or dispose the recipient. Repeating
		/// removal is a no-op; hosts quiesce producers before disposing borrowed resources.</remarks>
		public static void RemoveDestination(ILoggingDestination destToRemove)
		{
			lock (DestinationLock)
			{
				OrdinaryDestinations.RemoveAll(registration => ReferenceEquals(registration.Destination, destToRemove));
			}
		}

		/// <summary>Clears ordinary explicit registrations and their settings.</summary>
		/// <remarks>A17/A28/A29/A34: an empty route is a no-op. Leave all typed routes and
		/// ambient scopes unchanged. Publish empty membership before return without invoking,
		/// draining or disposing recipients. Old captures may finish. Subsequent ordinary
		/// calls may need that route's fallback; clear does not reset a file/session failure.</remarks>
		public static void ClearDestinations()
		{
			lock (DestinationLock)
			{
				OrdinaryDestinations.Clear();
			}
		}

		/// <summary>Opens a native label scope with no non-label properties.</summary>
		/// <param name="annotations">Optional attachment; null means no explicit labels.</param>
		/// <returns>A non-null non-transferable handle for the new current-flow frame.</returns>
		/// <remarks>A01/A06/A09-A17: equivalent to the two-argument BeginScope with an
		/// empty property sequence. Null and non-null empty attachments remain distinct.
		/// Opening adds a frame; repeated opening adds another frame, never a deduplicated
		/// lifetime. Opening this scope cannot remove active outer labels. Dispose the
		/// handle only in the opening operation or its normal continuation. LogScopeHandle
		/// specifies inherited capture, suppression and local cleanup, not universal misuse
		/// detection. No logging, recipient code, registration change or destination disposal
		/// occurs merely by opening a scope.</remarks>
		public static LogScopeHandle BeginScope(LogAnnotations annotations)
		{
			return BeginScope(annotations, new KeyValuePair<string, object>[0]);
		}

		/// <summary>Opens a native scope after capturing its labels and property membership.</summary>
		/// <param name="annotations">Optional immutable attachment; null means no attachment.</param>
		/// <param name="properties">Required finite, stable ordered sequence of property pairs; empty is valid.</param>
		/// <returns>A non-null non-transferable current-flow handle only after the entire frame capture succeeds.</returns>
		/// <exception cref="ArgumentNullException">properties is null; ParamName is "properties".</exception>
		/// <exception cref="Exception">Sequence access fails; no usable new scope or handle is published.</exception>
		/// <remarks>A03-A06/A09-A17: copy membership synchronously, including successful
		/// enumeration disposal, before publishing the new innermost frame. Preserve all
		/// supplied pairs, their order, duplicate keys and original values/references, as
		/// specified by LogScopeFrame.Properties. Capture executes no arbitrary value
		/// getters, implicit ToString or nested enumeration. Later source edits cannot
		/// alter the frame. Producer mutation during capture is unsupported.
		/// Acquisition, iteration, Current and disposal may execute caller code. Foreign
		/// exception details, competing-failure precedence and access counts are unspecified.
		/// Logger publishes no partial frame on failure; caller-code effects are not rolled
		/// back. No termination bound for unbounded input is promised. These are local scope
		/// construction failures, not dispatch failures; no DispatchFailed report is emitted.
		/// A successful capture pushes on the current flow at publication. No registry lock
		/// is held through supplied sequence code. The handle owns no destination or Logger.
		/// Dispose it only in the opening operation or its normal continuation, under the
		/// non-transferable use and local lifetime checks specified by LogScopeHandle.</remarks>
		public static LogScopeHandle BeginScope(LogAnnotations annotations, IEnumerable<KeyValuePair<string, object>> properties)
		{
			if (properties == null)
				throw new ArgumentNullException(nameof(properties));

			return new LogScopeHandle(new LogScopeFrame(annotations, properties));
		}

		/// <summary>Logs an ordinary raw entry with explicit optional annotations.</summary>
		/// <param name="annotations">Null for no entry attachment; otherwise immutable explicit occurrences.</param>
		/// <param name="level">A nonzero combination of known severity bits, from 1 through 63.</param>
		/// <param name="message">Original optional text; null, empty and whitespace are permitted.</param>
		/// <param name="ex">Original optional exception; null remains absent.</param>
		/// <exception cref="ArgumentOutOfRangeException">level is zero, negative or has unknown bits;
		/// ParamName is "level".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A01/A18/A19/A21-A22/A31-A40: raw severity rules apply, not convenience
		/// helper requirements. Null/empty annotations never subtract ambient labels. Capture
		/// every current frame, its label projection and this explicit attachment once before
		/// recipient callbacks. Follow Logger's synchronous capture, independent-attempt,
		/// withholding and failure-class contract. A normal return is not proof of delivery;
		/// all recipients may deliberately reject or a default-treated capture/check may fail.
		/// No message/exception content is fabricated, transformed or mutated by transport.</remarks>
		public static void LogAnnotated(LogAnnotations annotations, LogLevels level, string message = null, Exception ex = null)
		{
			if (level == 0 || (level & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(level));

			DispatchOrdinary(annotations, level, message, ex);
		}

		private static void Log(LogLevels level, string message, Exception ex = null)
		{
			DispatchOrdinary(null, level, message, ex);
		}

		private static void DispatchOrdinary(LogAnnotations annotations, LogLevels level, string message, Exception ex)
		{
			OrdinaryRegistration[] destinations;
			LogContext context;
			try
			{
				lock (DestinationLock)
				{
					destinations = OrdinaryDestinations.ToArray();
				}
				context = LogScopeHandle.Capture(annotations, DateTimeOffset.UtcNow);
			}
			catch (Exception)
			{
				CompleteDispatchFailure(null, 0, 1, false);
				return;
			}

			DestinationLabelPolicy[] intrinsicPolicies;
			try
			{
				intrinsicPolicies = new DestinationLabelPolicy[destinations.Length];
				for (var index = 0; index < destinations.Length; index++)
				{
					var supplied = destinations[index].Destination as LoggingDestinationCore;
					if (supplied != null)
						intrinsicPolicies[index] = supplied.LabelPolicy;
				}
			}
			catch (Exception)
			{
				CompleteDispatchFailure(null, 0, 1, false);
				return;
			}

			if (!Array.Exists(destinations, registration => registration.Settings.Enabled))
			{
				DispatchAutomatic(destinations.Length + 1,
					print => new AutomaticOrdinaryDestination(print).LogCaptured(context, level, message, ex));
				return;
			}

			List<LogFailureDescriptor> failures = null;
			var overflowCount = 0;
			var mustThrow = false;
			for (var index = 0; index < destinations.Length; index++)
			{
				var registration = destinations[index];
				var settings = registration.Settings;
				if (!settings.Enabled || (settings.ReportingLevel & level) != level)
					continue;

				try
				{
					if (settings.LabelPolicy.Mode != LabelFilterMode.NoFilter && !settings.LabelPolicy.Allows(context.Labels.EffectiveLabels))
						continue;

					var intrinsicPolicy = intrinsicPolicies[index];
					if (intrinsicPolicy != null && intrinsicPolicy.Mode != LabelFilterMode.NoFilter && !intrinsicPolicy.Allows(context.Labels.EffectiveLabels))
						continue;
				}
				catch (Exception)
				{
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.LabelCheck);
					continue;
				}

				var destination = registration.Destination;
				try
				{
					var supplied = destination as LoggingDestinationCore;
					if (supplied != null && !supplied.ValidateMessageLevel(level))
						continue;
					if (!destination.ValidateMessageLevel(level))
						continue;
				}
				catch (Exception)
				{
					mustThrow = true;
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.Eligibility);
					continue;
				}

				try
				{
					var supplied = destination as BaseLoggingDestination;
					var contextual = destination as IContextLoggingDestination;
					if (supplied != null)
						supplied.LogCaptured(context, level, message, ex);
					else if (contextual != null)
						contextual.LogWithContext(context, level, message, ex);
					else
						destination.Log(level, message, ex);
				}
				catch (Exception)
				{
					mustThrow = true;
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.Output);
				}
			}

			CompleteDispatchFailure(failures, overflowCount, 0, mustThrow);
		}

		private static void DispatchAutomatic(int registrationId, Action<Action<string>> render)
		{
			var initialFailure = false;
			try
			{
				render(record => initialFailure = !AutomaticSession.TryWriteRecord(record));
				if (!initialFailure)
					return;
			}
			catch (Exception)
			{
				initialFailure = false;
			}

			List<LogFailureDescriptor> failures = null;
			var overflowCount = 0;
			RecordFailure(ref failures, ref overflowCount, registrationId, LogFailureStage.Output);
			CompleteDispatchFailure(failures, overflowCount, 0, true, initialFailure);
		}

		private sealed class AutomaticOrdinaryDestination : TextBasedDestination
		{
			private readonly Action<string> _print;

			internal AutomaticOrdinaryDestination(Action<string> print) : base(LogLevels.Trace)
			{
				_print = print;
			}

			protected override void PrintLogEntry(string message)
			{
				_print(message);
			}
		}

		internal static void DispatchDirect(LoggingDestinationCore destination, LogContext context, LogLevels level, Action<LogContext> deliver)
		{
			if (level == 0 || (level & ~LogLevels.Trace) != 0)
				throw new ArgumentOutOfRangeException(nameof(level));

			LogContext current;
			DestinationLabelPolicy policy;
			try
			{
				current = LogScopeHandle.Capture(null, DateTimeOffset.UtcNow);
				policy = destination.LabelPolicy;
			}
			catch (Exception)
			{
				CompleteDispatchFailure(null, 0, 1, false);
				return;
			}

			if (context != null)
			{
				if (context.Scopes.Count != current.Scopes.Count)
					throw new ArgumentException("The context does not match the current scope openings.", nameof(context));

				for (var index = 0; index < current.Scopes.Count; index++)
				{
					if (!ReferenceEquals(context.Scopes[index], current.Scopes[index]))
						throw new ArgumentException("The context does not match the current scope openings.", nameof(context));
				}

				try
				{
					context = new LogContext(context, current.EventTimestampUtc);
				}
				catch (Exception)
				{
					CompleteDispatchFailure(null, 0, 1, false);
					return;
				}
			}
			else
			{
				context = current;
			}

			if (!destination.ValidateMessageLevel(level))
				return;

			List<LogFailureDescriptor> failures = null;
			var overflowCount = 0;
			try
			{
				if (policy.Mode != LabelFilterMode.NoFilter && !policy.Allows(context.Labels.EffectiveLabels))
					return;
			}
			catch (Exception)
			{
				RecordFailure(ref failures, ref overflowCount, 1, LogFailureStage.LabelCheck);
				CompleteDispatchFailure(failures, overflowCount, 0, false);
				return;
			}

			try
			{
				deliver(context);
			}
			catch (Exception)
			{
				RecordFailure(ref failures, ref overflowCount, 1, LogFailureStage.Output);
			}

			CompleteDispatchFailure(failures, overflowCount, 0, true);
		}

		private sealed class OrdinaryRegistration
		{
			internal readonly ILoggingDestination Destination;
			internal readonly DestinationRegistrationSettings Settings;

			internal OrdinaryRegistration(ILoggingDestination destination, DestinationRegistrationSettings settings)
			{
				Destination = destination;
				Settings = settings;
			}
		}

		private static void RecordFailure(ref List<LogFailureDescriptor> failures, ref int overflowCount, int registrationId, LogFailureStage stage)
		{
			if (failures == null)
				failures = new List<LogFailureDescriptor>(RetainedFailureLimit);

			if (failures.Count < RetainedFailureLimit)
				failures.Add(new LogFailureDescriptor(registrationId, stage));
			else
				overflowCount++;
		}

		private static void ThrowDispatchFailure(List<LogFailureDescriptor> failures, int overflowCount)
		{
			CompleteDispatchFailure(failures, overflowCount, 0, true);
		}

		private static void CompleteDispatchFailure(List<LogFailureDescriptor> failures, int overflowCount, int coreCaptureFailureCount, bool mustThrow, bool initialFailure = false)
		{
			if (failures == null && coreCaptureFailureCount == 0)
				return;

			var report = new LogFailureReport(failures ?? new List<LogFailureDescriptor>(), overflowCount, coreCaptureFailureCount);
			if (!_reportingFailure)
			{
				_reportingFailure = true;
				try
				{
					var subscribers = DispatchFailed;
					if (subscribers != null)
					{
						foreach (Action<LogFailureReport> subscriber in subscribers.GetInvocationList())
						{
							try
							{
								subscriber(report);
							}
							catch (Exception)
							{
							}
						}
					}

					try
					{
						Console.Error.Write(string.Format(CultureInfo.InvariantCulture,
							"Log dispatch failed. CorrelationId={0:D}; failures={1}; overflow={2}.{3}\n",
							report.CorrelationId, (long)report.CoreCaptureFailureCount + report.Failures.Count + report.OverflowCount, report.OverflowCount,
							initialFailure ? " " + LogDispatchException.InitialFailureGuidance : string.Empty));
					}
					catch (Exception)
					{
					}
				}
				finally
				{
					_reportingFailure = false;
				}
			}

			if (mustThrow)
				throw new LogDispatchException(report, initialFailure);
		}

		/// <summary>Logs a message with the exact TraceOnly bit.</summary>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">message is null before dispatch; ParamName is "message".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A20-A22/A31-A40: capture current scopes with absent entry annotations;
		/// follow Logger's ordinary-route capture, withholding, attempts and failure rules.</remarks>
		public static void Trace(string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.TraceOnly, message);
		}

		/// <summary>Logs a message with the exact DebugOnly bit.</summary>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">message is null before dispatch; ParamName is "message".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A20-A22/A31-A40: capture current scopes with absent entry annotations;
		/// follow Logger's ordinary-route capture, withholding, attempts and failure rules.</remarks>
		public static void Debug(string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.DebugOnly, message);
		}

		/// <summary>Logs a message with the exact InformationOnly bit.</summary>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">message is null before dispatch; ParamName is "message".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A20-A22/A31-A40: capture current scopes with absent entry annotations;
		/// follow Logger's ordinary-route capture, withholding, attempts and failure rules.</remarks>
		public static void Info(string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.InformationOnly, message);
		}

		/// <summary>Logs context with the exact WarningOnly bit.</summary>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <param name="ex">Optional original exception; null is permitted.</param>
		/// <exception cref="ArgumentNullException">message is null before dispatch; ParamName is "message".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A20-A22/A31-A40: capture current scopes with absent entry annotations;
		/// preserve raw inputs and follow Logger's ordinary-route attempt/failure contract.</remarks>
		public static void Warn(string message, Exception ex = null)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.WarningOnly, message, ex);
		}

		/// <summary>Logs an exception with the exact ErrorOnly bit.</summary>
		/// <param name="ex">Required original non-null exception.</param>
		/// <param name="message">Optional original text, including null, empty or whitespace.</param>
		/// <exception cref="ArgumentNullException">ex is null before dispatch; ParamName is "ex".</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A20-A22/A31-A40: absence stays absent. Capture current scopes with no entry
		/// attachment; follow Logger's ordinary-route capture, withholding and failure contract.</remarks>
		public static void Error(Exception ex, string message = null)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));

			Log(LogLevels.ErrorOnly, message, ex);
		}

		/// <summary>Logs an exception and context with the exact Critical bit.</summary>
		/// <param name="ex">Required original non-null exception.</param>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">ex or message is null before dispatch;
		/// ParamName names that argument, without a competing-error priority.</exception>
		/// <exception cref="LogDispatchException">An original failure requires propagation under Logger's contract.</exception>
		/// <remarks>A20-A22/A31-A40: capture current scopes with absent entry annotations;
		/// follow Logger's ordinary-route capture, withholding, attempts and failure rules.</remarks>
		public static void Critical(Exception ex, string message)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.Critical, message, ex);
		}

		/// <summary>Configures the primary host directory for this run's automatic file before initial establishment starts.</summary>
		/// <param name="directory">Required nonempty directory path. Relative paths use the current directory at this configuration call, not at a later log call.</param>
		/// <exception cref="ArgumentNullException">directory is null; ParamName is directory.</exception>
		/// <exception cref="ArgumentException">directory is empty or the executing framework rejects its path syntax; ParamName is directory for the ordinary argument mapping.</exception>
		/// <exception cref="InvalidOperationException">Automatic initial establishment has already started, succeeded or failed; configuration is unchanged.</exception>
		/// <exception cref="Exception">Other framework path-normalization errors propagate under their ordinary local contracts, without a dispatch report.</exception>
		/// <remarks>
		/// <para>S26-S28: normalize once using the executing framework's DirectoryInfo
		/// full-path semantics, then publish the complete primary-directory override
		/// atomically while the shared automatic session is Uninitialized. Null and
		/// empty are invalid, not requests to reset or restore a default. No competing
		/// invalid-argument or invalid-state precedence is promised. A failed call
		/// publishes nothing. Do not trim, reinterpret the path as a filename or add
		/// an existence, writability, content or filesystem-identity precondition.</para>
		/// <para>Before initialization, later successful calls replace the earlier
		/// primary override; repeating the same selected directory has the same
		/// effective configuration. At transition to Initializing the selected
		/// configuration is frozen. A concurrent configuration/initialization race
		/// publishes a complete override before that transition or rejects configuration
		/// without changing it. Every call after that transition is rejected, even for
		/// the same directory. It cannot move an established file or erase failure memory.</para>
		/// <para>S08/S09/S16/S20/S30: this synchronous operation performs no directory
		/// creation, file open, permission probe, logging, notification or recipient
		/// callback. It does not register a recipient, change ordinary/exact-T policy,
		/// configure the secondary application's identity, modify scope state or dispose
		/// anything. It owns no caller resource and introduces no async/cancellation
		/// or lifetime handle. Paths and local path exceptions are ordinary configuration
		/// information, not promised sanitized dispatch diagnostics. The default without
		/// an override is the consuming host application's base directory. Secondary
		/// recovery and actual output remain subject to Logger's automatic-session rules.</para>
		/// </remarks>
		public static void ConfigureAutomaticFileHostDirectory(string directory)
		{
			AutomaticSession.ConfigureHostDirectory(directory);
		}
	}
}
