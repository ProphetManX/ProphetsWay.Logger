# Logger v4 Requirements And Staged Plan

**Basis:** accepted D001-D021 in [decision-log.md](decision-log.md), summarized by
[product-brief.md](product-brief.md). P01-P12 are accepted, not questions awaiting approval.
**Alignment:** 2026-09-16. D001-D015 retain their 2026-09-13 provenance; D016-D021 were accepted
2026-09-15 and recorded 2026-09-16. Apply their dated, clause-specific supersession, not a blanket
replacement of capture/filter policy. The confirmed F1-F3 answers need no new policy interview.
**Disposition:** requirements draft for independent Requirements Reviewer v2 review.
Acceptance criteria below are obligations, not execution results or gate-clearance claims.
This revision establishes no F1-F3 file/dispatch implementation, bridge delivery or full-v4 completion.
This is utility-level planning, not a new multi-layer application architecture.
Read [../AGENTS.md](../AGENTS.md) and actual project files for current repository facts.

## Shape And Boundaries

| Selected shape | Rejected alternative and reason |
| --- | --- |
| Retain static logging and independent exact-type metadata routes in the Logger utility. | Application layers or a mandatory common-record calling model add structure not selected by D001/D010. |
| A sealed immutable reference label, separate from metadata and later call annotations. | A closed enum/registry excludes application identities; a value type permits invalid default instances; another generic label parameter is not selected (D003/D010). |
| Atomically published route snapshots with synchronous destination invocation. | Locks held through callbacks and drain-on-removal conflict with callback mutation and captured-call completion (D005/D006). |
| Optional whole-entry recipient filtering before that recipient's rendering/handoff. | Field redaction and automatic classification are not the selected confidentiality boundary (D004/D013). |
| Explicitly enabled bridges and private per-entry visited-route context. | Automatic reverse installation or text-based global deduplication loses independent entries (D011). |
| Independent automatic routes sharing one coordinated fixed file path per run, with remembered failed initial establishment. | A shared global filter, truncate-on-reactivation, CWD-following relocation or repeated failed-initialization probes conflicts with D012/D019/D020. Same-path recreation is not physical-identity policing. |

## Original Scope Trace

| Original obligation | Requirements | Delivery stages |
| --- | --- | --- |
| W1 / CR-01: concurrency and callback mutation | R-09, R-10, R-11 | M2 |
| W1 / CR-02: warnings, messages and exceptions | R-07, R-08, R-12, R-17 | M2, M4, M5 |
| W1 / CR-03: non-destructive fallback | R-09, R-20, R-21 | M2, M4 |
| W1 / CR-04: permitted absent exceptions | R-08, R-12, R-17 | M2, M4, M5 |
| W1 / CR-05: ordinary/typed isolation | R-09, R-10 | M2 |
| W1 / CR-06: equivalent mask representations | R-07, R-08 | M2 |
| W1 / CR-07: original-failure precedence | R-15, R-16, R-21 | M2, M4 |
| W2 / S-01: framing; support, quality, examples and meaningful refactors | R-13, R-22, R-23 | M4, M6 |
| W3: both Microsoft bridges and fidelity | R-11, R-17, R-18, R-19 | M5 |
| W4: labels, propagation, filters and safe check failures | R-01 through R-06, R-14 through R-16, R-18 | M1, M2, M3, M5 |
| Full-scope limits and consumer responsibilities | R-24 | Every stage |

## Accepted Requirements

### R-01 - Label Validity (D003/P01, D010/P08)

An identifier is opaque application data, 1-256 UTF-16 code units, with no whitespace or control characters.

- AC-01.1: Accept one ASCII letter and 256 ASCII letters; reject null, empty and 257-letter inputs without producing a usable label.
- AC-01.2: Reject identifiers containing space, tab, CR, LF, NUL, DEL or U+00A0, including embedded whitespace; the rule is not an ASCII-only whitelist.
- AC-01.3: Count UTF-16 units, not visual characters: 128 repetitions of a supplementary character such as U+1F600 occupy 256 units and are valid; 129 exceed the limit.
- AC-01.4: Accept an unknown valid identifier such as `team:alpha/v1` without registration, URI interpretation or a library taxonomy.

### R-02 - Label Value Identity (D003/P01)

Equality uses the exact ordinal case-sensitive identifier, never object-reference identity or normalization.

- AC-02.1: Separately created labels with the same identifier compare equal and produce equal hash codes, including when used as collection keys.
- AC-02.2: `PII` and `pii` are distinct; U+00E9 and U+0065 followed by U+0301 remain distinct valid identifiers. No trimming, folding or Unicode normalization occurs.
- AC-02.3: Equality is reflexive, symmetric and transitive; a non-null label is unequal to null and an unrelated object. Reviewed operators, if supplied, agree with value equality.
- AC-02.4: Repeated reads, comparisons and hashing preserve identity and hash stability for the instance; do not require a fixed numeric hash or unequal hashes for every unequal pair.

### R-03 - Label Immutability (D010/P08)

Use a small sealed library-owned reference value, provisionally `SensitivityLabel`, in `ProphetsWay.Utilities`.

- AC-03.1: Public-contract inspection establishes a non-inheritable reference type with no public identity mutation and no creation path yielding an invalid non-null default instance.
- AC-03.2: Creating other labels or placing a label in collections cannot change its identifier, equality or hash. Construction/comparison requires no logger, registry, file, backend or mutable application metadata.

### R-04 - Transport And Origins (D003/P01, D004/P02, D010/P08)

Optional immutable call annotations carry labels/origins separately from application metadata; propagation needs no setup.
Context-bearing destination use is consistent across ordinary/typed calls, without requiring metadata
convenience contracts for every typed call; deliberate raw/composite use remains available (D010).

- AC-04.1: Entry labels and explicitly labeled enclosing scopes accumulate. An inherited-only entry is labeled, and inner scopes or entries cannot remove inherited labels.
- AC-04.2: Repeated identical identities collapse for membership checks while all origin entries remain available; unknown valid identities propagate unchanged through ordinary and typed calls.

### R-05 - Optional Recipient Filters (D004/P02)

A destination selects no label filter, Exclude, or Allow-Only, never both configured modes together.

- AC-05.1: No filter adds no label restriction. Exclude `{A}` denies effective `{A,B}` but permits `{B}` and an unlabeled entry; empty Exclude excludes nothing.
- AC-05.2: Allow-Only `{A,B}` permits `{A}` and `{A,B}`, denies `{A,C}` and unlabeled entries; empty Allow-Only receives nothing. Inherited labels participate identically.
- AC-05.3: Reject null configured collections and combined modes. Duplicate identities add no matching power; valid reject-all configuration stays active and never activates fallback.

### R-06 - Withholding And Direct Calls (D004/P02, D010/P08, D013/P11, D018)

Eligibility precedes recipient formatting and raw handoff; withholding covers message, exception/data, metadata and scopes.

- AC-06.1: A denied recipient observes no payload through its formatter, callback or export. An opted-in check failure also withholds that recipient and attempts independent recipients under AC-15.3 and R-16, without permissive rescue. It is not reclassified as a configured rendering/output failure by D018.
- AC-06.2: Supplied built-ins/bases apply configured eligibility even on direct calls. Logger checks registration policy before custom handoff; arbitrary direct application calls into custom destinations are not sandboxed.

### R-07 - Severity Vocabulary (D002)

Exact bits are Critical=1, ErrorOnly=2, WarningOnly=4, InformationOnly=8, DebugOnly=16, TraceOnly=32.
Inclusive destination masks Critical/Error/Warning/Information/Debug/Trace are 1/3/7/15/31/63 respectively.

- AC-07.1: Convenience calls emit their exact bit; Trace, Critical and ErrorOnly are supported. Security is removed throughout, without a concern/event-kind replacement or extra CriticalOnly alias.
- AC-07.2: A destination includes every requested message bit: message 9 reaches mask 15, not 8. Its outbound Microsoft conversion is one Critical record, explicitly mapped from the lowest set bit and retaining mask 9.

### R-08 - Argument Boundaries (D007/P05)

Use ordinary argument contracts without fabricating message/exception content or imposing helper rules on raw records.

- AC-08.1: Raw masks accept nonzero known-bit combinations and reject zero, negative or unsupported bits. Destination zero remains active reject-all; equivalent enum, integer, numeric-string and case-sensitive recognized-name combinations agree; malformed names do not default to Information.
- AC-08.2: Ordinary, typed and metadata helpers agree: Trace/Debug/Info/Warn require non-null message; Error requires exception but permits absent message; Critical requires both. Empty/whitespace messages are accepted; permitted raw/direct/Microsoft absences cause no accidental exception.
- AC-08.3: Reject null destinations, required null collections and invalid encodings before side effects. Microsoft None is disabled/no-op and unknown ordinals are rejected. Contract review maps exact exceptions; no new validation-order priority is specified.

### R-09 - Independent Routes (D005/P03, D012/P10, D019)

Ordinary and exact declared-metadata-type registrations/settings and fallback suppression/reactivation are
independent; the shared automatic file's establishment state follows R-20/R-21.

- AC-09.1: Ordinary logging, metadata declared as a destination-contract type and two different declared metadata types cannot alias registrations. Runtime subtype/assignability does not reroute a typed call.
- AC-09.2: A compatible explicit destination, including a registered built-in, suppresses only its route's fallback even when it rejects the entry. Removing/disabling the last restores only that route; no-setup typed fallback requires no custom subclass. Restored default use follows AC-21.4/AC-21.5: reactivation does not reset remembered failed initialization.

### R-10 - Snapshot Publication (D005/P03)

Capture membership and immutable copied settings atomically; invoke synchronously in snapshot insertion order without registry locks through callbacks.

- AC-10.1: Concurrent first use and registration/settings changes expose complete old or new snapshots, never partially updated masks/modes/membership. Captured calls may finish after removal or replacement returns.
- AC-10.2: Callback add/remove/clear/self-removal affects later captures without invalidating iteration or waiting on itself. Recursive user logging is a new call; neither cross-thread total ordering nor serialized callers is promised.
- AC-10.3: Reject duplicate same-instance registration on one route; allow it on distinct compatible routes. Removing an absent registration is a no-op and clear affects only its route. Mutation returns after publication, not draining.

### R-11 - Ownership And Scopes (D006/P04)

Supplied destinations/factories/loggers are borrowed; library-created fallback/adaptation resources are owned.

- AC-11.1: Removal never disposes borrowed resources. Owned retirement waits for captured users; retaining/queuing destinations capture what they need before return and own later completion. Hosts quiesce callers before disposing borrowed resources.
- AC-11.2: Inbound provider disposal is idempotent/nonthrowing, disables only its registrations and lets captured calls finish; subsequent logging is no-op, enablement false and new scopes inert.
- AC-11.3: Scope disposal is flow-local and LIFO; double disposal is no-op, out-of-order disposal is rejected without stack mutation, and ended scopes do not affect later outside calls or unrelated flows.

### R-12 - Rendering And Exception Fidelity (D008/P06)

Default support covers null, strings/chars, Boolean/numeric primitives, decimal, Guid, DateTime/DateTimeOffset,
TimeSpan and enum names, rendered invariantly; recognized structured entries/scopes keep their boundaries.

- AC-12.1: Supported values render independently of current culture; supplied exception details retain message/context for warnings and every valid composite. Permitted absent exceptions remain absent and do not fail rendering.
- AC-12.2: An unsupported object yields `[no formatter: TypeName]` without arbitrary getter access or implicit object ToString. An explicit formatter can extend rendering; permitted custom typed destinations still receive original payloads.

### R-13 - Record Framing (D008/P06, S-01)

Built-in text records occupy one physical line; framing is not redaction.

- AC-13.1: Backslash, CR/LF, tabs, controls and Unicode line separators are escaped in message, metadata, exception and marker text. Synthetic forged-prefix/multiline input cannot become a second physical record.
- AC-13.2: One UTC event timestamp is captured per call and shared across that call's records, independently of filename allocation; escaping does not silently discard supplied content.

### R-14 - Capture Boundaries (D008/P06, D018)

Capture producer-formatted text once when delivery is needed and copy property/scope membership synchronously, not arbitrary object graphs.

- AC-14.1: Later collection additions/removals cannot change captured membership. Nested mutable values are not promised frozen; producer mutation during capture is unsupported. All-rejected entries do not invoke recipient formatters.
- AC-14.2: Shared producer/collection-capture failure withholds the incomplete entry from all affected recipients and reports one core failure under AC-15.3. An opted-in filter-check failure withholds only that recipient under the same retained policy. Configured recipient-local rendering failure withholds only that recipient but requires AC-15.1 propagation; independent recipients continue and all reporting follows R-16.

### R-15 - Output Failure Policy (D009/P07, D018-D020)

Attempt all independently eligible recipients before safe reporting and required propagation. D018 replaces
the old non-strict return rule for configured rendering/output failures, not for every failure class.
Automatic file failures follow R-21; compatible shared capture and opted-in check policy remains below.

- AC-15.1: One configured rendering/output failure plus one successful output throws after all independently eligible attempts and safe reporting, with or without strict selection. Reversing recipient order does not omit an eligible attempt. Another output's success, subscriber presence or reporting success cannot suppress propagation.
- AC-15.2: Deliberate severity/label mismatch and active reject-all are non-failures even when every recipient rejects; neither restores a less restrictive fallback. Qualifying successful initial secondary-file recovery is not itself a failure (AC-21.1). No unconfigured rescue destination, buffered recovery or later asynchronous-completion guarantee is introduced.
- AC-15.3: With no failure requiring mandatory propagation present, shared producer/collection-capture and opted-in filter-check failures retain default reporting-and-return and explicitly selected strict throwing after independent attempts and safe reporting, even if another recipient succeeds. Withholding follows R-06/R-14. A check/capture failure cannot suppress a configured rendering/output or automatic-file failure that requires propagation under AC-15.1/R-21; no new strict option is introduced.

### R-16 - Safe Original-Cause Reporting (D009/P07, D018, CR-07)

Report safe provenance without retaining/exposing raw failure payloads or allowing reporting failures to replace original failures.

- AC-16.1: A failed call produces one bounded notification with at most eight safe descriptors plus overflow count; best-effort stderr summary is at most 512 characters. A nine-failure synthetic call exposes eight descriptors and overflow one.
- AC-16.2: Dynamic failure details expose only generated registration/correlation identifiers, stage codes and counts. D018's fixed developer guidance may name generic default locations and advise making an appropriate location writable or configuring a destination. Actual/resolved paths, message/state/labels, exception text/stack/Data, raw causes and user names remain absent. Every propagated error, including retained strict errors, uses the same safe summaries without raw inner exceptions or a raw-failure archive.
- AC-16.3: Subscribers are invoked individually; one throwing does not stop others or create new strict escalation. Subscriber/stderr failures cannot replace the original failure or suppress propagation required by R-15/R-21. Reporting never uses ordinary fanout/secondary destinations; recursive notification is suppressed, not unrelated entries. Unavailable reporting is not guaranteed delivery or rate limiting.

### R-17 - Explicit Bidirectional Bridges (D011/P09)

Select each Microsoft bridge direction explicitly; preserve available information rather than parsing rendered text to invent structure.

- AC-17.1: Enabling inbound or outbound alone never installs the reverse. Controlled round trips retain category, event identity, original state, producer formatter/formatted message, exception, structured properties and logical scopes, including opaque/scalar state.
- AC-17.2: Useful typed fallback works without application-written destination subclasses. Raw exceptionless records remain legal; a rendered string is not reverse-parsed into fabricated properties/templates.

### R-18 - Bridge Representation (D011/P09)

Use a stable extensible typed representation with distinct ordered bridge-owned metadata, retaining application keys and origins.

- AC-18.1: Duplicate application property names, separate scope frames, original composite bits and per-origin labels survive controlled adapters without flattening or overwriting event entries/current host scopes. Exported frames are disposed in reverse order.
- AC-18.2: External labels require explicit application mapping; a property merely named like a sensitivity field grants no native classification. Exact reviewed metadata keys cannot discard origins or collide by overwriting application values.
- AC-18.3: Factory routing uses supplied category or configurable default `ProphetsWay.Logger`; a fixed logger keeps its category and carries origin category as metadata where supported. No call-stack category inference occurs.

### R-19 - Bridge Reentrancy And Enablement (D011/P09)

Private per-entry visited-route identity/context stays outside application-editable properties, with exception-safe cleanup.

- AC-19.1: A cyclic return to a visited route is suppressed while other original recipients, unrelated nested calls and fresh identical-text entries remain eligible. Application property edits cannot manufacture/reset the guard; cleanup survives exceptions.
- AC-19.2: Inbound delivery enforces enablement without a prior precheck. A level/route precheck does not promise label acceptance; guarantees cover controlled adapters, not arbitrary context-discarding providers, global deduplication or instrumentation.

### R-20 - Automatic File Session (D012/P10, D018-D020)

Ordinary and typed automatic routes share one coordinated UTF-8 file path per run, retaining independent
policies and all-six-severity coverage. Automatic output is quick-start support; guidance recommends
configuring intended destinations rather than relying on defaults as the main logging arrangement.

- AC-20.1: Prepare route information at setup/first use but attempt actual establishment only for eligible output, not permission prechecks for every message; concurrent first use shares the coordinated file. Reactivation of an established default appends rather than truncating; failed initial establishment follows AC-21.4/AC-21.5. Rejects never bypass policy through another route.
- AC-20.2: Default location is the consuming host application's base directory, with the existing explicit host override and application-specific LocalApplicationData secondary, never Logger's assembly folder or a hosting `dotnet.exe` installation directory. A later CWD change from A to B leaves the selected path unchanged. Initial invariant UTC naming uses exclusive collision allocation without replacing an unrelated existing file; later same-path recreation follows AC-21.6, not a new session allocation.

### R-21 - File Recovery And Explicit Outputs (D012/P10, D018-D020)

Distinguish an uninitialized default, an established fixed path and remembered failed initial establishment.
Initial secondary recovery is distinct from later I/O or uncertain writes; compatible explicit routes bypass
default state without resetting it. Explicit destinations retain their selected path and encoding/reset choices.

- AC-21.1: For eligible default-dependent output without a compatible explicit destination, initial primary establishment failure before any record bytes are written requires a secondary-location attempt. Qualifying secondary success returns normally even in strict mode, unless another independent failure requires propagation. If neither initial location is usable, throw regardless of strict selection after independent eligible attempts and safe reporting, with R-16's fixed safe guidance to make an appropriate generic default location writable or configure a destination.
- AC-21.2: After writing starts or acceptance is uncertain, do not replay, copy, relocate or create another session file. Actual I/O failure propagates after independent eligible attempts and safe reporting; later default-dependent calls attempt the established path under AC-21.6. Do not claim crash-safe or exactly-once storage.
- AC-21.3: Explicit files append by default, retain encoding/reset choices, reset only on explicit request and never silently relocate or follow later CWD changes. Exact explicit relative-path capture belongs to D010's reviewed contract detail; it cannot make the destination follow later CWD. Route configuration does not delete an existing automatic file; no implicit truncation, automatic pruning or across-restart one-file bound is imposed.
- AC-21.4: Remember both initial locations failing for the current application/session run. Later default-dependent calls throw with the same safe guidance without new path or permission probes; waiting or changing permissions alone does not cause re-establishment. No configuration observer, background retry, recovery service or new public reset API is introduced.
- AC-21.5: A new application/session instance starts fresh. In the same run, a compatible explicit destination makes remembered default failure irrelevant for its route, including deliberate rejection; another default-dependent route still throws from the remembered failure. Removing/disabling the last compatible explicit destination does not erase that failure or cause re-probing when its route needs the default again.
- AC-21.6: At an established automatic or selected explicit path, append if the file exists or create a missing file at that same path; actual open/write failures throw under R-15/R-16. Require neither persistent physical-identity policing nor content/binary detection: writable non-log content can accept appended bytes and need not be rejected as binary. Existing-content suitability and external interference remain developer responsibilities. Missing-file recreation does not authorize replay, copying, relocation or implicit destructive reset.

### R-22 - Intended Support And Tooling (D014/P12, D017, D021)

D014's library/example/test-consumer matrix remains intended support, not current project inventory.
D017 selects tooling and defers non-Windows execution for now; exact technical records and write grants
remain authoritative, not permission for additional package, project or operation changes.

- AC-22.1: Focus current approved execution on Windows, including Framework behavior and the applicable intended test/consumer legs. Record Mac/Linux execution and cross-platform core restore/reference/build/load evidence as deferred under D017, not passed or permanently waived from full qualification. One runtime's success is not evidence for other required legs.
- AC-22.2: Review actual compatible assets, licenses and fresh advisories before exact dependency selection; prefer minimal Microsoft abstraction/registration dependencies in the main distribution. Initial v4 requires no Event Log, Azure or compliance-redaction dependency.
- AC-22.3: Use the selected xUnit v3 MTP-off direction (`xunit.v3.mtp-off`) within D017's exact ON-02/A1 technical authorization, retaining its VSTest route and other reviewed tooling limits. Approved tooling modernizes tests/runner/SDK, migrates assertions to Shouldly, adds coverage and retains justified mocks. The test-project rename remains separate deferred W2 work; enumerate rename, solution and reference edits for explicit approval, without silently bundling or dropping them.
- AC-22.4: D021 approves `c:/temp/logger tests/` as the isolated-test parent. Use fresh uniquely owned children and synthetic data; clean only verified owned children after users/handles finish. Never delete the parent, alter pre-existing files or escape through links/junctions. This location permission neither authorizes old fixed-filename/destructive tests nor supplies file-output implementation or fixture-operation authority for a documentation-only task.

### R-23 - Packaging And Consumer Proof (D014/P12, D017-D020)

Preserve the intentional namespace and packaged consumer material; meaningful refactors preserve approved behavior and protected specifications.

- AC-23.1: Actual package metadata has a populated homepage/tags and repository kind `git`, with the retained README/changelog/icon and approved assets present. Record XML documentation, source/symbol and reproducibility assessment against actual outputs, not project-property assertions alone.
- AC-23.2: Compile quick-start, custom, typed, helper and both bridge examples; verify label/filter, formatter, ownership/lifetime, failure and breaking-migration guidance against behavior. Include R-15/R-16's failure-class distinction and private-path-safe guidance, and R-20/R-21's quick-start limits, fixed paths, append/create limits, remembered initialization failure and explicit-route bypass. Distinguish D017's deferred Mac/Linux evidence from passed or permanently waived qualification. Release narrative traces all W1-W4 obligations, not just a successful label milestone or this document alignment.

### R-24 - Responsibility And Exclusions (D001, D013/P11, D014/P12)

Consumers own classification/mapping/sanitization, recipients/access/authentication, storage, retention, costs and audit obligations.

- AC-24.1: Security evidence uses synthetic data and checks denied-recipient/raw-handoff/reporting canaries. Guidance distinguishes label transport/selection and framing from confidentiality/compliance or sandboxing producer/custom code holding payloads.
- AC-24.2: Initial-v4 scope excludes Windows Event Log/Azure-specific destinations, built-in field redaction, automatic classification/instrumentation, global drain/shutdown/process-exit flush, background buffering and automatic pruning. No guaranteed delivery, remote durability, audit storage, release date/channel or live-operation authority is implied.

## First Milestone - M1 Immutable Label Only

The first milestone is exactly R-01 through R-03 and their ten acceptance criteria, under D015.
It is a pure in-memory value slice, not a label-routing implementation and not full-v4 completion.
Contract authoring settles normal null/argument exceptions, parameter names, creation/identity/equality
members and any optional equality operators under D010's independent review. Tests follow that reviewed
contract; implementation does not choose new semantics. No validation-order priority or extra label
parsing/conversion/convenience API is required. Null references are not valid label instances.

Origin bookkeeping is deliberately reserved for R-04/R-18 integration, not stored or implemented here.
Exclude annotations, scopes, filters, severity changes, registration, dispatch, formatters, bridges,
fallbacks, files, backend access, global state and project/package modernization from M1 implementation.
Reuse the existing test project and applicable xUnit/assertion conventions; do not manufacture a second harness.

Exit requires independently reviewed label contracts (with scoped threat input where the contract gate
requires it), Test Designer specifications, independent Test Auditor review, observed nonzero focused
execution and independent final verification of unchanged specifications and implementation behavior.
An available runner and safe selection must be established before any red/green claim. Zero discovery,
unavailable execution, a declaration stub or a build-only result is not a passing label test.
Do not include existing fixed-filename/destructive file tests. A tooling dependency returns to its
authorized owner with exact needed changes; it neither changes label semantics nor widens M1 writes.

## Staged Delivery

Stages organize dependencies, not new file grants. Split later groups into scoped reviewed TDD targets;
contracts, specifications and implementation retain separate owners. Independent review precedes dependent work.

A separately scoped pure R-05 membership predicate is independent of F1-F3 file/dispatch work. Its
permitting result establishes only membership-policy permission, not delivery or payload withholding.
M3's integration prerequisites do not apply to that isolated predicate; its reviewed public details
remain under D010, without changing R-01 through R-05 or making file work a new prerequisite.

| Stage | Scope and prerequisite | Completion boundary |
| --- | --- | --- |
| M0 - Requirements | Independent Requirements Reviewer v2 checks these requirements, brief and D001-D021 with dated supersession. | Resolve findings through the author; at most one repair/re-review. A draft is not approved by silence. |
| M1 - Label value | R-01 through R-03 after M0; reviewed public detail and available isolated runner. | The ten label criteria and the contract/test/implementation gates above, with no integration or I/O. |
| M2 - Dispatch foundation | R-07 through R-11, R-15/R-16; reviewed severity, argument, snapshot, lifetime and reporting contracts. | Synthetic recipients prove concurrency, route isolation, callbacks, ownership, failure-class-specific propagation and safe reporting; no file/backend dependency for these checks. |
| M3 - Label integration | R-04 through R-06 and R-14 after M1/M2. | Reviewed annotations/origins, capture and whole-entry filtering; inherited-label and denied-recipient canaries before handoff. |
| M4 - Text and fallback | R-12/R-13, R-20/R-21 after M2/M3; D021 supplies the isolated parent, while exact fixture/specification/implementation work needs its own scoped authority. | Warning/exception fidelity, framing, initial recovery/failure memory, fixed-path append/create and uncertain-write cases; preserve capture/check versus rendering/output failure treatment. |
| M5 - Bridges | R-17 through R-19 after route/lifetime, label/capture and failure contracts; reviewed P09 details and exact dependencies. | Both directions, ordered scope/state/origin fidelity, enablement and private cycle guard; controlled synthetic round trips only. |
| M6 - Full-v4 qualification | R-22/R-23 and all W1-W4 evidence; support/tooling prerequisites may be separately authorized earlier where needed. | Complete G1-G6, including approved rename/support/package work and examples; D017's deferred non-Windows proof is not supplied by Windows-only completion. This is not publication authority. |

## Verification Gates

G1-G6 are evidence obligations from D014 with D017's current-execution deferral and D021's fixture
boundary, not statuses passed by this document or by a bounded label/policy milestone.

| Gate | Required evidence and owner boundary |
| --- | --- |
| G1 | Approved behavior and exact public/member/file inventory; Interface Architect and independent Contract Reviewer. P08/P09 delegate detail, not new policy or extra files. |
| G2 | Authorized tooling, actual discovery, synthetic recipients and uniquely owned isolated fixtures within D021/AC-22.4; generated protected specification/input baseline. No parent/pre-existing-file deletion, link/junction escape or implicit old fixed-filename test execution. |
| G3 | Test Designer/Test Auditor separation, nonzero observed behavioral/boundary/concurrency results with identities, no unexplained skips and unchanged approved expectations through implementation. |
| G4 | Approved build/package and complete intended test/consumer legs; actual assets/metadata/XML/source/symbol and fresh license/advisory review. Current execution is Windows-first under D017, including Framework behavior; Mac/Linux and cross-platform core proof remain deferred evidence for full qualification, not passed or permanently removed. |
| G5 | Threat Modeler design assessment and independent implementation security review: raw-payload/reporting canaries, framing, lifetime/concurrency and private bridge-context checks. |
| G6 | Compiled examples and verified migration/label/rendering/lifetime/failure guidance, including R-23's failure/path/reuse and deferred-platform distinctions, plus complete W1-W4 trace and release narrative. |

No tests, builds, packages, runtime inspection, security clearance or independent review are established
by authoring requirements. Record execution identity/configuration and protected inputs with real results;
static counts and diagnostics do not substitute for execution. Release operations need separate exact authority.

## Downstream Readiness

| Owner | Ready input and remaining gate |
| --- | --- |
| Interface Architect | M1 and pure R-05 membership semantics are unchanged. R-15/R-20/R-21 define failure classes and file states for separately scoped contract work; exact exception/member and relative-path capture details retain D010/D011's reviewed delegation, not a new owner-policy question. |
| API Designer | No HTTP resource, verb, endpoint or authorization contract is selected; no HTTP workstream is fabricated. |
| Test Designer | Ten M1 criteria remain measurable; the pure membership predicate needs no file/dispatch implementation. R-15/R-21 specify failure precedence and run/path transitions; reviewed contracts, audited specifications, actual discovery and protected inputs precede execution claims. D021 supplies the later isolated parent, not unscoped I/O authority. |
| Threat Modeler | Application identifiers/payloads are consumer-classified; recipient raw handoff, text output, stderr/subscribers and controlled bridges are trust boundaries. M1 crosses none of those output boundaries. |
| Implementer | Each scoped implementation follows reviewed contracts, audited specifications and focused verification. The pure membership predicate remains independent; aligned F1-F3 requirements do not deliver or authorize runtime changes, either bridge or full-v4 completion. |

No new owner-policy question is introduced. D010/D011 detail review, D014/D017's exact dependency/file/
rename authorization boundaries and scoped verification remain dependencies, not reasons to re-present
accepted P01-P12, F1-F3, tooling, isolated-parent or current-platform decisions.
