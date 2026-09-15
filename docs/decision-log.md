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
