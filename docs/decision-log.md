# Logger v4 Decision Log

**Decision owner:** G. Gordon Nasseri. **Record date:** 2026-09-13.

These entries record accepted intent, not implementation, passing tests or release readiness.
Read [product-brief.md](product-brief.md) for the summary and [../AGENTS.md](../AGENTS.md) for repository guidance.
Append future decisions; never delete, renumber or rewrite an entry. A replacement must identify the superseded decision.

## Approval Provenance

On 2026-09-13 the owner accepted the complete Logger v4 consolidated recommendations P01-P12,
revision 2, following independent requirements re-review and the owner-answer discussion:

> "Accept all recommendations for items P01 thru P12, all of them as recommended."

The accepted substance is recorded below so no external session record is needed to understand it.
P identifiers retain their approval identity; they are not unanswered questions. Earlier alternatives were not selected.
All entries are recorded as accepted on 2026-09-13; earlier direct quotations retain their original dates.

## D001 - Purpose And Full Scope

**Accepted:** 2026-09-13. **Provenance:** original owner intent, 2026-09-12, retained by P12.

> "yes i want to fix all of these findings"
>
> "we're not 'Done' until all the new refactor/features are in and working"

Serve developers across application/deployment styles with diagnostics, searchable records and general logging.
Retain easy central destination setup, static use throughout the application and typed metadata extensibility.

| Stream | Selected outcomes |
| --- | --- |
| W1 | All seven corrections listed below, including their exceptional and concurrent cases. |
| W2 | Supported-use modernization; dependency/security triage; packaging; meaningful refactors; tests, documentation and working examples; S-01 record integrity. |
| W3 | Both Microsoft logging bridge directions with available structured data and scope fidelity. |
| W4 | Immutable application-defined labels, optional whole-entry filtering, inherited origins and safe failure handling. |

- CR-01: concurrent first use, dispatch and callback registration changes must preserve stable calls without registry corruption.
- CR-02: warning messages and supplied exception details survive ordinary, typed, text and callback delivery; valid composites do not erase messages.
- CR-03: automatic fallback reuse is non-destructive and does not supplement an active compatible explicit route.
- CR-04: helper preconditions are consistent while permitted absent exceptions on raw/Microsoft records cause no accidental failure.
- CR-05: ordinary and typed registrations cannot alias, even when metadata uses a destination-contract type.
- CR-06: equivalent valid destination masks have identical eligibility regardless of accepted representation.
- CR-07: a reporting failure cannot replace the original file failure; safe original-cause provenance remains available.
- S-01: untrusted multiline/control text cannot masquerade as additional text-log records; framing is not redaction.

On 2026-09-12 the owner said "we can put these specific destinations on a backlog and remove them from the initial v4.0 release" about Windows Event Log and Azure-specific destinations. Both are deferred; Microsoft interoperability and sensitivity remain selected. No service, platform package arrangement or later release date is selected.

## D002 - Severity And Composite Meaning

**Accepted:** 2026-09-13. **Provenance:** owner severity approvals, 2026-09-12; P05/P09 retain them.

> "criticall at bit 1 is fine with me, it has the same ordering as before (sever to weakest)"
>
> "for Critical we should require both an exception and message (where warn is optional exception, and error is optional message)"

- Six exact severity bits: Critical=1, ErrorOnly=2, WarningOnly=4, InformationOnly=8, DebugOnly=16, TraceOnly=32.
- Inclusive destination masks: Critical=1, Error=3, Warning=7, Information=15, Debug=31, Trace=63. Add Trace/Critical and ErrorOnly; remove Security throughout, with no replacement concern/event-kind feature or additional CriticalOnly alias.
- Convenience calls emit their exact severity bit. Composite messages remain legal; a recipient must include every requested bit, not merely overlap.
- Outbound Microsoft conversion emits one record at the severity represented by the lowest set message bit, explicitly mapped rather than numerically cast; retain the original composite mask. For example, message 9 reaches mask 15, not mask 8, and maps to Critical.
- Convenience-helper requirements do not become restrictions on incoming Microsoft records. Input boundaries are D007.

## D003 - Label Identity - P01

**Accepted:** 2026-09-13. **Provenance:** blanket P01-P12 approval above; P01 in full.

- Labels carry opaque, application-defined identities, not a library taxonomy, URI interpretation or secret-bearing display text.
- Identity uses ordinal, case-sensitive value comparison, independent of object-reference identity. Do not trim, case-fold or normalize Unicode.
- Accept identifiers of 1-256 UTF-16 code units; reject whitespace and control characters.
- Identical effective labels collapse for membership checks while per-origin entries remain preserved. Unknown valid identities are not registry errors.
- The immutable reference-value direction and reviewed public-detail delegation are D010. No automatic classification or extra generic label parameter is selected.

## D004 - Optional Filters And Inheritance - P02

**Accepted:** 2026-09-13. **Provenance:** retained label/filter approvals and blanket P02 approval.

- Label propagation works without configuration. Filtering is optional per destination; no filter adds no label restriction. There is no built-in field redaction.
- Exclude rejects an entry if any effective label matches its configured set. Allow-Only requires every effective label to be allowed and rejects effectively unlabeled entries.
- Empty Exclude is valid and excludes nothing; empty Allow-Only is valid and receives nothing. Null configured collections are invalid; duplicate identities add no matching power.
- Reject-all remains an active registration, never a reason to restore a broader fallback. Denial is not an output failure.
- Explicit labels from enclosing scopes accumulate with entry labels; origins survive, and inner scopes/entries cannot remove inherited labels. Inherited-only entries count as labeled.
- A destination selects one mutually exclusive mode; combined Allow-Only and Exclude is explicitly rejected.
- Failed opted-in checks withhold the whole payload from that recipient before formatting/callback/export, continue independent recipients and use D009 failure handling. No permissive fallback is introduced.

## D005 - Registration And Snapshots - P03

**Accepted:** 2026-09-13. **Provenance:** P03; earlier snapshot and synchronous-invocation approvals retained.

- Ordinary and exact declared-metadata-type routes are independent. Nested runtime types or assignability do not redirect a typed call.
- A compatible explicit registration suppresses only its route's automatic fallback; remove/disable the last to reactivate that route. Explicitly registered built-ins count as explicit, regardless of their destination kind.
- Each call sees one atomically published membership/settings snapshot. Settings are immutable copies replaced together; publication cannot expose half-updated policy.
- Add/remove/enable/disable/mask/mode replacement returns after publication, not after old calls drain. Captured calls may finish; callback mutations affect later captures only, and self-removal never waits on itself.
- Coordinate registry writers briefly without holding registry locks through callbacks. Preserve insertion-order attempts within a snapshot, with no cross-thread total order or serialized-caller promise.
- Reject duplicate same-instance registration on one route; allow the same object on distinct compatible routes. Removing an absent registration is a no-op; clear is route-scoped.
- Invoke destinations synchronously. Recursive user logging is a new call, not blanket-dropped; arbitrary custom recursion and later asynchronous failures are not bridge/core completion guarantees.

## D006 - Ownership And Lifetime - P04

**Accepted:** 2026-09-13. **Provenance:** blanket P04 approval, including provider and scope rules.

- Explicitly supplied destinations, factories and loggers are borrowed. Removal never disposes them; hosts stop producers and await synchronous calls before disposing borrowed resources.
- Library-created fallback/adaptation resources are owned and retire only after captured users finish.
- Inbound provider disposal is idempotent and nonthrowing, disables only its registrations and permits captured dispatches to finish. Later logging is a no-op, enablement is false and new scopes are inert.
- Scope disposal is flow-local and last-in-first-out; double disposal is a no-op. Reject out-of-order disposal without changing the scope stack; ended scopes do not affect later outside calls or unrelated operations.
- Retaining/queuing destinations own capture before return, asynchronous completion and cleanup.
- Initial v4 has no automatic process-exit flush, background queue, public global drain/shutdown facility or remote-durability promise. Synchronous invocation does not establish external persistence.

## D007 - Arguments And Invalid Inputs - P05

**Accepted:** 2026-09-13. **Provenance:** blanket P05 approval; D002 helper distinctions retained.

- Raw message masks accept nonzero combinations of the six known bits. Zero, negative or unsupported bits are explicit argument errors.
- Destination mask zero is valid active reject-all. Enum, integer, numeric-string and recognized case-sensitive enum-name combinations parse equivalently; malformed names never silently become Information.
- Reject null destinations, null required collections and invalid encodings before side effects.
- Ordinary, typed and metadata convenience calls agree: Trace/Debug/Info/Warn require a non-null message; Error requires a non-null exception and permits an absent message; Critical requires both. Empty/whitespace messages are allowed.
- Raw/direct/Microsoft records permit absent message/exception as their contracts allow; fabricate neither. Microsoft None is disabled/no-op; unknown ordinals are rejected.
- Use ordinary argument errors; the contract author maps exact exception types and public details from these accepted rules rather than inventing content restrictions.

## D008 - Rendering And Capture - P06

**Accepted:** 2026-09-13. **Provenance:** blanket P06 approval, including S-01 and failure boundaries.

- Default supported values are null, strings/chars, Boolean/numeric primitives, decimal, Guid, DateTime/DateTimeOffset, TimeSpan and enum names, rendered invariantly. Known structured entries/scopes retain their boundaries.
- Unsupported values show `[no formatter: TypeName]`; do not reflect over arbitrary getters or call implicit object ToString. Explicit formatters extend rendering; custom typed destinations still receive permitted original payloads.
- Include supplied exception details with context for every valid severity/composite, not only a cumulative severity-name match.
- Each text record occupies one physical line. Escape backslash, CR/LF, tabs, control characters and Unicode line separators in payload, metadata, exception and marker text. Capture one UTC event timestamp per call, separately from filename allocation.
- Determine recipient label eligibility before invoking its formatter. Capture producer-formatted text once when delivery is needed, and copy property/scope collection membership synchronously without arbitrary deep cloning.
- Producer mutation during capture is unsupported. Later collection additions/removals cannot change captured membership; nested mutable values are not promised frozen. Retaining destinations preserve their needed data before returning.
- A shared producer/capture failure withholds the incomplete entry from every affected recipient and reports one core failure. Destination-local rendering/filter failures withhold only that destination and continue independent recipients; both use D009 default/strict treatment.
- Framing prevents forged record boundaries, not sensitive-content disclosure. Unsupported markers are explicit limits, not lossless arbitrary-object persistence.

## D009 - Output Failures And Safe Reporting - P07

**Accepted:** 2026-09-13. **Provenance:** retained continue-and-notify/strict approvals; P06/P07 check/capture extensions.

- Attempt all independently eligible destinations. Default behavior contains output failures, notifies and returns; explicitly selected strict mode throws after attempts/notification if any original destination failed, even when another succeeded.
- Subscriber presence or notification success does not change the strict trigger. A filter mismatch is not failure; successful internal fallback recovery is not failure. No unconfigured rescue destination or buffered recovery is implied.
- Issue one bounded notification per failed log call: at most eight safe descriptors plus overflow count, and a best-effort stderr summary of at most 512 characters.
- Expose only generated registration/correlation identifiers, stage codes and counts. Exclude original message/state/labels, paths, exception text/stack/Data, raw causes and user-provided names.
- Strict errors carry the same safe cause summaries without raw inner exceptions. Preserve original-failure precedence rather than exposing a secondary reporting error; no trusted raw-cause channel or retained raw-failure archive is selected.
- Invoke subscribers individually and contain their failures. Do not send reports through ordinary Logger fanout or automatic secondary destinations; subscriber failures do not trigger new strict escalation.
- Suppress recursive notification, not ordinary independent entries. Reporting may be unavailable or unobserved; one notification per call is not rate limiting or a guarantee when all outputs/reporters fail.

## D010 - Public Direction And Direct Calls - P08

**Accepted:** 2026-09-13. **Provenance:** blanket P08 approval, including limited reviewed design delegation.

- Use a small, non-inheritable immutable library-owned reference value for labels; avoid silently default-constructed invalid label identities. Neither a marker contract, arbitrary label type nor closed enumeration is selected.
- Keep optional immutable call annotations for labels/origins separate from application metadata. Preserve ordinary and typed use rather than require every caller to construct a common record.
- Provide consistent context-bearing destination use and deliberate raw/composite logging. Metadata convenience use does not define label identity or impose a mandatory constraint on all typed logging.
- Supplied built-ins/bases enforce configured eligibility before rendering even on direct calls. Logger enforces registration policy before custom handoff; arbitrary direct application calls into custom destinations remain application responsibility, not a sandbox guarantee.
- The owner delegates exact names/signatures to the contract author, subject to independent review against accepted behavior. This is bounded design discretion, not new policy, architectural freedom, an implicit source-file grant or permission to skip review.

## D011 - Microsoft Interoperability - P09

**Accepted:** 2026-09-13. **Provenance:** both bridge/fidelity approvals retained; P09 representation/setup approval.

- Both inbound and outbound bridges are selected, each explicitly configured; enabling one never installs the reverse. Useful typed fallback needs no application-written destination subclass.
- Preserve available category, event identity, original state, producer formatter/formatted message, exception, structured properties and logical scopes. State may be opaque or scalar, not necessarily a dictionary; do not parse rendered text to invent templates/properties.
- Use a stable extensible typed representation. Keep labels/origins and original composite bits in distinct ordered bridge-owned metadata, never flattened over application keys; retain repeated names and original event/scope entries.
- Explicit application mapping supplies labels for external records; arbitrary property names are not an authoritative native Microsoft sensitivity classification.
- Outbound factory use selects supplied category or configurable default `ProphetsWay.Logger`. A supplied fixed Microsoft logger keeps its category, carrying origin category as metadata where supported; no call-stack inference.
- Export ordered distinct scope frames and dispose them in reverse. Preserve event/scope origins without silently overwriting current host scopes.
- Cyclic-return protection uses private per-entry route identity/context outside application-editable properties, with exception-safe cleanup. Suppress only a visited-route return; other original destinations, unrelated nested calls and fresh identical-text entries remain eligible.
- Inbound delivery enforces enablement without requiring a prior precheck. A level/route enablement precheck is not proof of later label acceptance.
- Contract design may settle exact metadata keys under independent review, without flattening or losing origins. Round-trip guarantees cover controlled adapters, not arbitrary providers' persistence or components that discard private context; no global deduplication or automatic instrumentation is promised.

## D012 - Automatic File Behavior - P10

**Accepted:** 2026-09-13. **Provenance:** retained timestamp/location/recovery approvals; P10 transitions and explicit append default.

- Independent ordinary/typed automatic routes share one coordinated physical file per application run. Route suppression remains independent; one file never implies one global filter.
- Prepare route information at setup/first use, but open lazily only for actual eligible output. Automatic output accepts all six severities and uses UTF-8; reactivation appends within the same run.
- Use the consuming host's application base directory normally, with an explicit host override where needed; application-specific local application data is secondary. Never select Logger's assembly folder or silently rely on the current working directory.
- Use invariant UTC timestamped naming plus exclusive collision allocation, never timestamp-only uniqueness or replacement of an existing file. Exact filename/application-folder spelling belongs to reviewed configuration design.
- Recover to the secondary location only after initial primary establishment fails before any record bytes are written. Successful recovery returns normally, including in strict mode; both locations failing follows D009.
- Once writing begins or acceptance is uncertain, report failure without replay, copying or a new session file. Later calls try the established file; no crash-safe or exactly-once storage guarantee follows.
- Explicit files retain their selected path and encoding/reset choices; append is the default, destructive reset requires explicit request. Never silently relocate an explicit output.
- No automatic deletion/pruning or permanent one-file-per-application bound across restarts. Consumers own cleanup; configuring a route does not delete its existing automatic file or bypass rejection via another route.

## D013 - Data And Consumer Responsibilities - P11

**Accepted:** 2026-09-13. **Provenance:** blanket P11 approval; no additional audit/retention/compliance outcome requested.

- Consumers own correct classification, explicit external mapping and sanitization, destination storage shape, recipient access/authentication, retention and audit obligations.
- Library guarantees are approved label transport/whole-entry selection and record framing, not automatic classification, field redaction, confidentiality or compliance assurance.
- Whole-entry withholding covers raw message, exceptions and their data, typed metadata, scopes and affected recipient formatters/callbacks/exports. It cannot sandbox consumer code or producer formatters already holding those objects.
- Development evidence uses synthetic data; no deployment or real-data certification is part of the library gates.
- No paid service, resource provisioning, authentication setup, storage readership or spend authorization is inferred from selecting an integration. Specialized destinations remain consumer-owned work.

## D014 - Support, Quality And Gates - P12

**Accepted:** 2026-09-13. **Provenance:** blanket P12 approval, including exact-detail conditions and G1-G6.

- Accepted support direction: netstandard2.0 and net10.0 library assets; net10.0 examples; net48/net10.0 tests and consumers, with Windows verification of the Framework leg. This is intended support, not a claim about current project configuration.
- Prefer the smallest compatible Microsoft logging abstraction/registration dependencies in the main distribution. Review assets, licenses and advisories before exact version selection; no Event Log, Azure or compliance-redaction dependency is required for initial v4.
- Modernize test framework/runner/SDK, retain justified mocking, migrate assertions to Shouldly and add coverage through the appropriate owners. Reassess actual dependencies; historical scans are neither fresh findings nor clearance.
- Correct package homepage/tags/repository kind; assess XML documentation, source/symbol and reproducibility settings and inspect actual outputs. Retain the intentional namespace and packaged README/changelog/icon.
- Test-project naming correction remains W2 work. Enumerate rename, solution and reference edits and obtain explicit approval before including them; do not silently omit or bundle them.
- Exact dependency versions, project/tooling changes and file grants require review and authorization before edits. Directional acceptance is not permission to select arbitrary versions or expand file scope.

| Gate | Required completion evidence, not supplied by this decision record |
| --- | --- |
| G1 | Approved behavior baseline, exact public/member/file inventory and independently reviewed contracts without unresolved consequential semantics. |
| G2 | Approved tooling; synthetic destinations and uniquely owned isolated fixtures; actual discovery and protected specification/input records; no arbitrary pre-existing-file deletion. |
| G3 | Observed, nonzero behavioral/edge/concurrency execution, no unexplained skips, independently audited specifications and preserved expectations through implementation. |
| G4 | Approved library builds/packages and full test/consumer legs; Windows Framework behavior and cross-platform core restore/reference/build/load; actual dependency, metadata, asset, XML/source/symbol and fresh license/advisory evidence. |
| G5 | Design threat assessment and implementation security review; rejected-recipient/raw-payload/diagnostic canaries, record framing, lifetime/concurrency and private bridge-context checks. |
| G6 | Correct, compiled quick-start/custom/typed/helper/bridge examples; verified labels, formatting, lifetime, failure and migration guidance; release narrative and complete W1-W4 coverage. |

Breaking changes are accepted. No version edit, tag, channel, date, feed, publication or deployment is authorized by the v4 direction or these gates. No benchmark threshold, delivery SLA or shutdown deadline is invented. A global ranking of correctness/security/delivery speed/cost/simplicity was not selected; the explicit accepted trade-offs govern.

## D015 - Durable Records And First TDD Milestone

**Accepted:** 2026-09-13. **Provenance:** current owner request after P01-P12 approval.

> "I would like the documentation agents to author some of the project requirements, plans, intentions, whatever we wanna call them, decisions into the applicable project, so that these current documents youv'e created are included into the project/repo for posterity."
>
> "but then i also want you to begin working with the TDD process to work on the beginning stages of actual coding"

These portable records preserve accepted policy rather than re-presenting old options for approval.
The orchestrator selected the approved immutable label value as the first bounded milestone; the owner did not narrow full-v4 completion to it. Requirements and detailed contracts proceed through their authors and independent reviewers, followed by real specifications/audit and implementation under scoped authority.
No tests, modernization, contract gate, implementation milestone or release gate are claimed complete by authoring these documents. Detailed names/signatures belong to D010/D011's reviewed delegation, not a new owner-policy questionnaire.

## September 15 Amendments - Recorded 2026-09-16

D001-D015 above remain verbatim historical decisions. The owner is G. Gordon Nasseri for every
entry below; acceptance occurred on 2026-09-15 and durable capture on 2026-09-16. These are confirmed
answers, not the earlier preparation report's proposed readings or a new interview.

Source records are the complete [September 15 owner message](../../.agent-runs/20260915-2052-logger-overnight-approval/slice-01-r1.md#owner-message),
the [ON-02 exact document grant](../../.agent-runs/20260915-2052-logger-overnight-approval/ON-02-approval-r1.md#other-exact-allowed-product-paths),
the [F1-F3 answers and ON-02 activation](../../.agent-runs/20260915-2052-logger-overnight-approval/ON-02-activation-r1.md#exact-owner-confirmation),
and the [A1 activation](../../.agent-runs/20260915-2052-logger-overnight-approval/ON-02-A1-activation-r1.md#owner-confirmation).
The relevant owner quotations are reproduced below so the decisions do not depend on retention of
external run artifacts. The original proposal's unapproved status is historical; the activation supplies
approval, and its F1 answer selects the alternative to that proposal's non-strict return recommendation.

### Dated Supersession Markers

| Historical clause | Disposition effective 2026-09-15, recorded 2026-09-16 |
| --- | --- |
| D009 default contains output failures and returns; throwing requires explicit strict mode | **Partially superseded by D018:** configured destination rendering/output failures propagate after independent attempts and safe reporting, even if another destination succeeds. Compatible check/capture treatment and safe-reporting limits are not blanket-repealed. |
| D008 sends destination-local rendering/filter failures to D009 default/strict treatment | **Partially superseded by D018 for configured rendering failures only.** Shared producer/capture and opted-in filter-check failure policy is not changed by F1. |
| D012 both initial locations failing follows D009 | **Superseded by D018:** eligible default-dependent output throws when neither initial location is usable, regardless of strict selection. Successful qualifying secondary establishment remains normal success. |
| D012 later calls try the established file | **Clarified by D019/D020:** an established fixed path still receives actual I/O attempts; failed initial establishment is remembered without re-probing for the current run. These are different states. |
| D012 host-base location, collision allocation, explicit path/reset and append default | **Clarified by D020, not deleted:** fixed paths and later same-path append/create do not imply physical-identity policing or content detection. Initial collision allocation and separately explicit destructive-reset permission remain distinct. |
| D014 test-tooling direction and G4 cross-platform evidence | **Narrowed for current execution by D017:** xUnit v3 is selected and verification focuses on Windows for now. Non-Windows evidence is deferred, not passed or permanently removed from full qualification. |
| D015 durable capture and reviewed downstream work | **Extended by D016-D021:** confirmed intent is recorded; requirements, contracts and new behavior still need their own alignment and delivery gates. |

## D016 - Confirmed Document Authority

**Accepted:** 2026-09-15. **Recorded:** 2026-09-16. **Provenance:** ON-02 and A1 owner activations linked above.

> Checkpointed; approve ON02
>
> Approve ON-02 A1

ON-02's exact Product Discovery grant permits dated additions to [decision-log.md](decision-log.md)
and narrow alignment of [product-brief.md](product-brief.md), preserving history. This capture uses
only those two product paths; it does not exercise the envelope's grants to other authors.
A1 amends the tooling variant, not product behavior or release authority. No version change,
Git operation, publication, new public surface or implementation follows from writing these records.

## D017 - Test Tooling And Windows-First Verification

**Accepted:** 2026-09-15. **Recorded:** 2026-09-16. **Provenance:** owner message items 1/7; A1 activation.

> 1:   proceed directly to xUnit v3
>
> 7:  focus on windows for now,  if you can establish mac and linux verifiecation somehow within my current system, then i'm open to discuss your suggestions, but i'm not worrying about building/testing that at this time.

- Select xUnit v3 directly; do not re-open a v2-versus-v3 choice. The exact A1-authorized variant is `xunit.v3.mtp-off` 4.0.1, not the original ordinary `xunit.v3` candidate. The activation records the bounded amendment; it is not a claim about today's installed or resolved inventory.
- Retain the reviewed tooling/file/operation limits in ON-02 as amended by A1. Exact package and execution records stay in those technical records, not in product intent as a mutable inventory. Approval alone proves neither runtime compatibility nor security clearance.
- Focus current execution on Windows. Mac/Linux execution and full cross-platform proof are deferred, not certified, permanently waived, or a new platform/release promise. D014's other support, quality and review conditions remain.
- The test-project rename remains separate deferred W2 work, not silently included or removed. Product-version and release authority are unchanged.

## D018 - Failure Propagation And Safe Developer Guidance

**Accepted:** 2026-09-15. **Recorded:** 2026-09-16. **Provenance:** owner message item 2 and activated F1.

> 2:  in general, the app should be able to write where the exe is, and if for some reason it can't, the appData backup location should also be accessible.  if neither are usable, i feel like we should throw an exception to the user.  If the user hasn't established any destinations themselves, and the two default file options don't work/can't write, we need to let the developer know.  we can tell the user that they must either verify the app can write to one of the generic/default locations, or they must setup a destination themselves for use.   otherwise there is no point in having a logger that can't/doesn't log the messages anywhere.    we shouldn't be checking for default permissions every log statement, really just that first time, right?
>
> F1:  if a configured destination is unable to render logs, it should throw an exception.  there's no gaurantee that configured destinations will be logging/managing error/critical level messages, so i'm not sure we should try to just log information elsewhere and "hope" it gets seen.  if the developer has configured something to log data of a determined level, then we should do so, and if we cannot, throw so they can understand why and fix it.   if you have another suggestion, i'm open to hearing it.

- A configured destination's rendering/output failure must propagate to the caller after all independently eligible destinations have been attempted and safe reporting performed. Another destination succeeding, subscriber presence or reporting success does not turn it into a successful return. Explicit strict selection is no longer required for these failures; do not retain D009's old non-strict swallowing here.
- When eligible automatic output has no compatible explicit destination and neither initial default location is usable, throw with safe developer guidance. Try actual first-use establishment, not repeated permission prechecks. Qualifying initial secondary-location success remains normal success; later real I/O can still fail.
- Fixed safe guidance may name generic default locations and tell the developer to make an appropriate default location writable or configure a destination. It must not expose actual/resolved paths, payloads, labels, user-provided names, raw causes or exception contents. Preserve D009's bounded safe descriptors/reporting, subscriber isolation and original-failure precedence; a reporting failure cannot replace the original failure.
- No unconfigured rescue destination, recursive ordinary-fanout report or hope-based rerouting is selected. Deliberate severity/label mismatch and active reject-all remain non-failures, even if no recipient accepts an entry; neither activates fallback.
- This supersedes only the identified rendering/output and initial-default failure clauses. D004/D008/D009's compatible treatment of shared producer/collection-capture failures and opted-in filter-check failures remains; do not infer a new policy for those classes or add strict options, exception names or callback APIs. D008's no-arbitrary-reflection/implicit-object-formatting limits are unchanged.

## D019 - Remember Failed Default Initialization

**Accepted:** 2026-09-15. **Recorded:** 2026-09-16. **Provenance:** activated F2, following owner message item 2.

> F2:  if it's not too much overhead, you may remember the failed default initialization, so that if a log later tries to write without having any new destinations configured, then it can just throw again.  restarting the application/instance will reset this, and if the user configures a destination, then the status of the default doesn't matter.

- Remember failed initial default establishment for the current application/session run. Later default-dependent calls throw from that remembered failure without new path or permission probes; waiting or changing permissions alone does not require another attempt.
- A new application/session instance starts fresh. This describes the run boundary, not an existing public instance/reset facility and not permission to introduce a public reset API, configuration observer, background retry or recovery service.
- A compatible explicit destination makes the default failure irrelevant for its route, including when its deliberate filters reject an entry. Other routes remain independent. If a route later needs its default again in the same run, remembered failed initialization is not silently re-probed.
- Failure to establish any default differs from a later failure at an already established path. The latter still uses D020's fixed-path actual-I/O behavior; neither state promises cross-instance ownership or guaranteed recovery.

## D020 - Fixed Paths And Ordinary Append Or Create

**Accepted:** 2026-09-15. **Recorded:** 2026-09-16. **Provenance:** owner message items 3/4 and activated F3; ON-02's settled path clarification.

> 3:  i'm not sure i understand the premis of the host changing their configured directory from A to B?   is this the app's "Current Directory" pointer, and so we'll just chase/follow that with the application?    I don't think our default log file should do that, nor should any developer configured destinations for that matter.   default files should look for the exe and save there by default, appData as a fallback, and no matter what the app does, we just stay where we're at.   Ideally, the developer should always define custom destinations for their messages, and not rely on the defaults.    the default is just there to help get the user up and running asap, and shouldn't be relied on as the main logging destination/target.
>
> 4:  my kneejerk for these default locations is to "append or create"  if the file exists, open as text and append to it, if not, create it.  if you can't write/open as text or whatever to interface with the file, maybe its a jpeg or whatever, then throw an exception.   this is a core utility, i don't think we need to plan for every possible way that the world could disrupt the expected workflows.  the developer should be aware of processes running in their environemnt, and messing with their file log destinations during execution is unwise in general.  if they need/want access to the contents of said logs, they can setup a custom destination to trigger an event and receive each log message immediately to any subscriber, no need to read it from a text file.   so even for a developer's destination, if it's a file destination, append or create, if unable, throw.
>
> F3:  I concur.  we don't need to overthink and attempt to avoid any wonky-ness the developer could throw our way.  file logs are intended to work as expected, if they're messing with them out of our expected use case, then they'll have to deal with the ramifications in their own way.

- Keep a selected automatic or explicit file path fixed despite later current-directory changes. The normal default is the consuming host application's base directory, with application-specific LocalApplicationData secondary; it is neither Logger's assembly folder nor the installation directory of a hosting `dotnet.exe`. D012's explicit host override remains. Exact public handling of explicit relative-path capture belongs to the contract author/reviewer, not a promise to follow later CWD changes.
- Append to an existing selected file or create a missing file at that same path. Actual open/write failures throw; Logger invocation preserves independent eligible attempts and safe reporting before propagation. Initial successful secondary recovery remains the D018 exception to treating the failed primary establishment as a failed call.
- F3 confirms ordinary I/O, not content/binary detection. The earlier JPEG example is not a guarantee that opening for text append rejects binary data: writable non-log content can accept appended bytes. Existing-content suitability and interference by other processes remain developer responsibilities.
- Do not add persistent physical-identity policing, replay, copying, relocation, a new session file after uncertain acceptance, or crash-safe/exactly-once guarantees. Missing-file recreation at the established path is distinct from initial session allocation; D012's exclusive collision allocation must still avoid replacing an unrelated existing file.
- Append remains the default for both automatic and explicit files. D012's separately explicit destructive-reset permission and encoding choices were not expressly revoked; retain them as distinct from default append/reuse. No implicit truncation, deletion, pruning or unrelated-file cleanup follows.
- Automatic output is quick-start support. Developers should deliberately configure their intended destinations rather than treat defaults as their main logging arrangement. Existing custom/event destination use is not a grant for a new callback surface.

## D021 - Isolated Test Parent

**Accepted:** 2026-09-15. **Recorded:** 2026-09-16. **Provenance:** owner message item 6 and ON-02's exact isolated-fixture limits.

> 6:  I approve of what you want to do here, do i need ot tell you a location to use for writing the files now?   can you use c:\temp\logger tests or something like that, just anything under c:\temp shoudl suffice

The approved parent is `c:/temp/logger tests/`, for fresh uniquely owned children and synthetic test data.
Only verified run-owned children may be cleaned up after their users/handles finish; never delete the
parent, alter pre-existing files or follow links/junctions outside the owned tree. This is sufficient
location permission, not a question to ask again, a grant to run old fixed-filename tests, or a grant
to implement file output. No fixture operations are performed by this document capture.

## Required Alignment After This Capture

**Recorded:** 2026-09-16. The confirmed F1-F3 answers require no new owner-policy interview.
[requirements.md](requirements.md) remains unmodified on its D001-D015 basis. Its affected clauses,
especially R-15, R-20/R-21 and their R-06/R-14/R-16 cross-references, require targeted Solution Architect v2
alignment and independent Requirements Reviewer v2 review before new file/dispatch behavior is specified
or implemented. R-22 and G4 need the selected tooling direction and deferred platform-evidence distinction;
R-23 consumer guidance must reflect the fixed-path, quick-start and failure limits. Do not treat the old
default/strict or recovery wording as governing F1-F3 unchanged, and do not blanket-change other failure classes.

The contract author and independent Contract Reviewer retain exact public-detail work under D010/D011,
including explicit relative-path capture, without a new reset API or changed pure-label contract.
Later authorized specifications/audit and source implementation/verification remain pending for the new
F1-F3 behavior; tooling success is not evidence of that behavior. No label-filter integration, dispatch,
file, bridge, full-v4 or security-release completion is established here.

README, changelog and security-document promotions remain with their existing owners and were not reached
by this capture. No requirements, source, test, project, configuration, task, open-question, feature-index
or other product document is changed here. Release/version authority, privacy rules, independent destinations
and the approved reflection limits remain intact. D010/D011's prior reviewed delegation is unchanged;
no new OWNER decision area is downgraded to INFER.

## D022 - Custom Severity Callback Failures And Conditional Nightly Approval

**Accepted and recorded:** 2026-09-19. **Owner:** G. Gordon Nasseri. **Provenance:** current owner
acceptance of the preceding Q-C1 recommendation, captured under D015's durable-record intent.
Sources: [current approval and capture target](../../.agent-runs/20260919-0110-logger-m2c-policy/slice-01-r1.md),
[Q-C1 proposal](../../.agent-runs/20260919-0032-logger-m2c-preparation/01-m2c-proposal.md#questions-and-routing)
and [independent review](../../.agent-runs/20260919-0032-logger-m2c-preparation/02-m2c-requirements-review.md#q-c1-disposition).

The parent's preceding recommendation for custom `IDestination.ValidateMessageLevel` exceptions was:

> withhold that recipient, attempt the others, report safely, then throw

The owner accepted it:

> i agree with the recommendation, if the error/throw is a misconfiguration, it should be handled by the dev, i'm not sure where or why we'd ever hide an error ourside our default file destination stuff we've already discussed

- **Q-C1 is resolved:** during Logger dispatch, a configured custom `IDestination.ValidateMessageLevel` throwing for a valid message mask withholds the payload from that recipient. Finish all other independent eligible attempts, report safely, then throw to the caller, regardless of another output succeeding and without strict opt-in.
- This is a callback-boundary rule, not a cause-classification heuristic. Do not inspect foreign exception text or payload to guess whether it is a misconfiguration before deciding to propagate. It extends D018's mandatory propagation to this specific boundary, not to every kind of check or capture failure.
- Intentional false eligibility and active reject-all remain non-failures and do not activate fallback. Qualifying successful default-file recovery remains success; existing default-file rules are unchanged.
- Preserve D009/D018's bounded safe reporting, privacy limits, subscriber/stderr containment and original-failure precedence. Reporting failure cannot replace the original failure or suppress required propagation. No raw-error channel, new strict switch, exception name or public signature is selected; exact public details remain D010's independently reviewed contract work.

**Broader concern, not a blanket policy replacement:** the owner's objection to hidden failures is
recorded above, but does not explicitly choose a replacement for [requirements.md](requirements.md)
AC-14.2/AC-15.3 shared producer/collection-capture or opted-in label-check treatment, or repeal
AC-16.3 secondary-reporting safeguards. Those separate policy changes need explicit owner disposition;
Q-C1 does not need another answer. Label/capture features may remain outside the current configured
severity/output slice; this approval requires no new strict mechanism for those excluded paths.

The owner also said:

> and you are approved for the nightly run when ready

**Conditional nightly direction:** approval is when ready, with the existing no-push restriction and
stop by **05:00 EDT (UTC-04:00), 2026-09-19** unchanged. Exact accepted scope, reviewed contracts,
file/author grants and fresh executable validation readiness remain prerequisites, not completed gates.
This capture starts or schedules no implementation and grants no task/validator edits, unspecified Git
checkpoint, public API or release action. Earlier decisions and their history remain unchanged.

## D023 - Logical-Flow Label Inheritance And Isolation

**Accepted and recorded:** 2026-09-20. **Owner:** G. Gordon Nasseri. **Provenance:** current owner
approval of the immediately preceding recommendation and explanation in the attended conversation,
preserved in the [current acceptance target, revision 1](../../.agent-runs/20260920-1114-logger-flow-decision/slice-01-r1.md#approval-and-meaning)
and [current run record](../../.agent-runs/20260920-1114-logger-flow-decision/run.md#owner-decision-and-boundary).
This is a new acceptance, not an inference that an earlier recommendation was already approved.

The immediately preceding policy recommendation was:

> inherit labels automatically, but keep scope changes and cleanup isolated between logical flows.

The owner's exact current response was:

> in concur with your recommendation.  thank you for the clear example

The accepted explanation establishes these consequences:

- A logical flow follows asynchronous work across `await`, not one physical thread. A child inherits the active labels captured when it is queued.
- Scope additions and cleanup affect only their logical flow. Child-only changes must not alter the parent's or a sibling's labels or interfere with their later scope cleanup.
- In the explanation's example, a child inherits `PersonalData` and adds `Billing` in its own nested scope. That child has both labels; its parent and sibling do not acquire `Billing`. Leaving the child's `Billing` scope restores its inherited `PersonalData`. These are illustrative application labels, not a library taxonomy.
- Ending a label scope ends that flow's scope; it does not dispose Logger or its destinations.
- A child that outlives its parent's scope retains its captured labels. The parent ending its scope is not revocation of an already captured child context.

**Preserved rules:** D006 still requires flow-local, last-in-first-out disposal, double disposal as a
no-op, and rejection of out-of-order disposal without changing the scope stack. Ended scopes do not
affect later outside calls or unrelated operations; the inherited child's continuing context above
is not a new outside call by the parent. D006's resource ownership and other lifetime obligations
remain unchanged. D004's accumulation, non-subtraction of inherited labels, preserved origins and
optional whole-entry recipient filtering remain unchanged. Labels do not redact or encrypt content;
D013's classification, sanitization, recipient access, retention and other consumer responsibilities
remain with consumers. This decision does not change strict treatment or any failure policy.

**Historical Q-FLOW evidence:** the earlier [contract author's Q-FLOW and U1-U4 omissions](../../.agent-runs/20260919-2330-logger-m3a-readiness/02-label-context-contract.md#unreached-contracts-and-questions),
[independent Q-FLOW disposition](../../.agent-runs/20260919-2330-logger-m3a-readiness/03-contract-review.md#q-flow-disposition)
and [prior proposed recommendation](../../.agent-runs/20260919-2330-logger-m3a-readiness/06-implementation-readiness.md#the-one-owner-decision)
remain unchanged historical records. The prior recommendation proposed the same core inheritance
and per-flow isolation; the current approval and explanation above supply its acceptance now.
**Core Q-FLOW is resolved; do not ask for that policy again.** This supplements D004/D006/D010/D013,
not a blanket approval of every case in the earlier compound question or of a completed contract.

**Unselected details:** suppressed execution-context flow, manual handle transfer and misuse outside
ordinary inherited flows are still named boundaries, not policies silently filled with defaults.
No permission to dispose a transferred handle in another context is inferred. Exact callback and
handle APIs remain D010's bounded, independently reviewed contract work; its authority is not widened.
The explanation's `BeginLabelScope` name was explicitly illustrative and unimplemented, not a selected
API. No implementation mechanism, body, generic constraint, exception choice or new strict selector
is selected. U1-U4 remain unwritten and require fresh bounded authoring authority for Interface
Architect v2, followed by independent Contract Reviewer v2 review. Policy acceptance establishes no
complete-contract, test, implementation or release status.

**Expired-window boundary:** the prior M3-A envelope expired at **2026-09-20 05:00 EDT (UTC-04:00)**;
the parent recorded **11:14:47 EDT** for this fresh attended capture, after that cutoff. This decision
neither continues that run nor renews its deadline. No overnight continuation, source/test/setup
work, command execution, Git action, release action or new deadline is granted. Only the narrow
decision capture is current authority; all earlier entries and old-run reports are preserved.

## D024 - Conventional Scope Handles And Deliberate Flow Suppression

**Accepted:** 2026-09-23 (Q05); 2026-09-22 (suppression). **Recorded:** 2026-09-23.
**Owner:** G. Gordon Nasseri. **Provenance:**
[current owner quote and immediately preceding final option](../../.agent-runs/20260923-0011-logger-conventional-scopes/run.md#exact-owner-decision),
[acceptance target, revision 1](../../.agent-runs/20260923-0011-logger-conventional-scopes/slice-01-r1.md), and
[earlier owner suppression answer](../../.agent-runs/20260922-2138-logger-overnight-preflight/owner-response-03.md#exact-owner-response).

The latest owner response was:

> i feel like this is a feature that we're significantly over engineering for a use case that is unlikely and potentially adding bloat to our software.  go with option 2, we want to roll this out with the basic features working.  we can't account for every use case and every possiblilty of how someone might break it, or even worse, if someone tries to break it.  this is a logging utility, it shouldn't be this complicated to trigger the events out?

The immediately preceding final response defines its **option 2** as:

> Conventional scope handles: handles are documented as non-transferable, with local ordering and isolation enforced, but without promising detection of every inherited-child misuse. This narrows the guarantee.

This is not option 2 from the differently ordered research list; no explicit operation boundary was selected.

The earlier owner suppression answer was:

> I concur, no automatic inheritance when flow is deliberately suppressed, user intended actions should override any default we setup for a general use case.

- **Selected guarantee (Q05):** a scope handle belongs to the operation that opened it and its normal continuation. Applications must not pass it to an independent child or unrelated operation for disposal. Local ordering and isolation remain enforced; reliable identification or automatic rejection of every indistinguishable inherited-handle misuse is not promised. Unsupported transfer is not a supported ownership-transfer feature or a security boundary.
- **Rejected complexity:** no automatic creator-ID/fork detector, new public operation/fork/transfer boundary, tracking subsystem or speculative misuse-hardening is selected. The owner favors working basic logging features over bloat for unlikely or deliberate misuse, not over supported-use correctness.
- **Preserved normal behavior:** labels follow ordinary continuation across `await` and normally captured children. Child-created additions and cleanup stay isolated, cannot remove parent/sibling labels or consume their legitimate cleanup, and restore inherited labels when the child's nested scope ends. A child outliving its parent's scope retains captured labels; parent scope end is not revocation. Supported child cleanup does not subtract inherited labels.
- **Preserved cleanup:** D006's flow-local last-in-first-out cleanup, repeat-disposal no-op and non-mutating out-of-order rejection remain. Scope ending does not dispose Logger or destinations, and does not affect unrelated operations.
- **Accepted suppression:** deliberately suppressed execution-context flow causes no new automatic label inheritance; it does not revoke already captured labels. This intentional application choice overrides the general inheritance default, not Logger's internal lifecycle obligations.
- **Preserved data and failure boundaries:** D004's accumulation, origins and optional whole-entry filtering, D008's original-object and membership-only capture limits, safe diagnostics, resource ownership and existing failure policies remain. D013's classification, sanitization, recipient access, retention and other consumer responsibilities are unchanged; labels are not a confidentiality or sandbox guarantee.

**Dated qualification:** effective 2026-09-23, this settles D023's previously unselected suppression
and handle-transfer boundaries only as above, recording suppression accepted on 2026-09-22. Any
universal creator-versus-inherited-child detection/rejection promise is superseded by Q05's narrower
guarantee. D006's local cleanup and D023's supported inheritance/isolation are not withdrawn. All
prior entries, including D023's historical wording, remain unchanged.

**Authority and limits:** Q05 is answered, not replaced by a new question. Exact local mechanics,
exceptions and public details remain D010's bounded contract-author work with independent review;
this entry chooses none. It grants no implementation, specification changes, Git action, version or
publication authority. "Roll this out" is not a release approval. No completed contract, full M3/Set B,
test result, implementation or release readiness is established by this policy capture.
