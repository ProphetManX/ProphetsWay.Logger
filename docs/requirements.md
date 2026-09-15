# Logger v4 Requirements And Staged Plan

**Basis:** accepted D001-D015 in [decision-log.md](decision-log.md), summarized by
[product-brief.md](product-brief.md). P01-P12 are accepted, not questions awaiting approval.
**Disposition:** requirements draft for independent Requirements Reviewer v2 review.
Acceptance criteria below are obligations, not execution results or gate-clearance claims.
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
| Independent automatic routes sharing one coordinated per-run file. | A shared global filter or truncate-on-reactivation breaks isolation or destroys earlier output (D012). |

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

### R-06 - Withholding And Direct Calls (D004/P02, D010/P08, D013/P11)

Eligibility precedes recipient formatting and raw handoff; withholding covers message, exception/data, metadata and scopes.

- AC-06.1: A denied recipient observes no payload through its formatter, callback or export. A check failure also withholds that recipient, attempts independent recipients and follows R-15/R-16 without permissive rescue.
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

### R-09 - Independent Routes (D005/P03, D012/P10)

Ordinary and exact declared-metadata-type registrations/settings are independent, including their automatic fallback state.

- AC-09.1: Ordinary logging, metadata declared as a destination-contract type and two different declared metadata types cannot alias registrations. Runtime subtype/assignability does not reroute a typed call.
- AC-09.2: A compatible explicit destination, including a registered built-in, suppresses only its route's fallback even when it rejects the entry. Removing/disabling the last restores only that route; no-setup typed fallback requires no custom subclass.

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

### R-14 - Capture Boundaries (D008/P06)

Capture producer-formatted text once when delivery is needed and copy property/scope membership synchronously, not arbitrary object graphs.

- AC-14.1: Later collection additions/removals cannot change captured membership. Nested mutable values are not promised frozen; producer mutation during capture is unsupported. All-rejected entries do not invoke recipient formatters.
- AC-14.2: Shared producer/collection-capture failure withholds the incomplete entry from all affected recipients and reports one core failure. Recipient-local rendering/filter failure withholds only that recipient; independent recipients continue under R-15/R-16.

### R-15 - Output Failure Policy (D009/P07)

Default handling attempts independently eligible recipients, notifies and returns; explicitly selected strict handling throws after attempts/notification for an original failure.

- AC-15.1: One failing output plus one successful output still throws in strict mode, regardless of subscribers or successful reporting; default returns. Check/capture/render failures use the same applicable policy.
- AC-15.2: A filter mismatch and successful internal initial-file recovery are not failures. No unconfigured rescue destination, buffered recovery or later asynchronous-completion guarantee is introduced.

### R-16 - Safe Original-Cause Reporting (D009/P07, CR-07)

Report safe provenance without retaining/exposing raw failure payloads or allowing reporting failures to replace original failures.

- AC-16.1: A failed call produces one bounded notification with at most eight safe descriptors plus overflow count; best-effort stderr summary is at most 512 characters. A nine-failure synthetic call exposes eight descriptors and overflow one.
- AC-16.2: Only generated registration/correlation identifiers, stage codes and counts are exposed. Message/state/labels, paths, exception text/stack/Data, raw causes and user names are absent; strict errors have the same safe summaries and no raw inner exceptions/archive.
- AC-16.3: Subscribers are invoked individually; one throwing does not stop others or create new strict escalation. Reporting never uses ordinary fanout/secondary destinations; recursive notification is suppressed, not unrelated entries. Unavailable reporting is not guaranteed delivery or rate limiting.

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

### R-20 - Automatic File Session (D012/P10)

Ordinary and typed automatic routes share one coordinated UTF-8 file per run, retaining independent policies and all-six-severity coverage.

- AC-20.1: Prepare route information at setup/first use but open only for eligible output; concurrent first use shares the coordinated file. Reactivation appends earlier records rather than truncating, and rejects never bypass policy through another route.
- AC-20.2: Default location is the consuming host's application base directory, with explicit host override and application-specific local application data secondary. Invariant UTC naming uses exclusive collision allocation, never replacing existing files or relying on Logger's assembly folder/current working directory.

### R-21 - File Recovery And Explicit Outputs (D012/P10)

Initial recovery is distinct from uncertain writes; explicit destinations retain their selected path and encoding/reset choices.

- AC-21.1: If initial primary establishment fails before any record bytes are written, the secondary location must be attempted. If secondary writing succeeds, return normally even in strict mode; both locations failing follows R-15/R-16 and preserves safe original-cause provenance.
- AC-21.2: After writing starts or acceptance is uncertain, do not replay, copy or create another session file. Report failure and let later calls try the established file; do not claim crash-safe/exactly-once storage.
- AC-21.3: Explicit files append by default, reset only on explicit request and never silently relocate. Route configuration does not delete an existing automatic file; no automatic pruning or across-restart one-file bound is imposed.

### R-22 - Intended Support And Tooling (D014/P12)

D014's library/example/test-consumer support matrix is intended design, not current project inventory; exact versions and writes remain review/authorization conditions.

- AC-22.1: Approved builds and consumer checks cover that matrix, Windows Framework behavior and cross-platform core restore/reference/build/load. One runtime's success is not evidence for other required legs.
- AC-22.2: Review actual compatible assets, licenses and fresh advisories before exact dependency selection; prefer minimal Microsoft abstraction/registration dependencies in the main distribution. Initial v4 requires no Event Log, Azure or compliance-redaction dependency.
- AC-22.3: Approved tooling modernizes tests/runner/SDK, migrates assertions to Shouldly, adds coverage and retains justified mocks. Enumerate test-project rename, solution and reference edits for explicit approval; neither omit that W2 work nor silently bundle it into label coding.

### R-23 - Packaging And Consumer Proof (D014/P12)

Preserve the intentional namespace and packaged consumer material; meaningful refactors preserve approved behavior and protected specifications.

- AC-23.1: Actual package metadata has a populated homepage/tags and repository kind `git`, with the retained README/changelog/icon and approved assets present. Record XML documentation, source/symbol and reproducibility assessment against actual outputs, not project-property assertions alone.
- AC-23.2: Compile quick-start, custom, typed, helper and both bridge examples; verify label/filter, formatter, ownership/lifetime, failure and breaking-migration guidance against behavior. Release narrative traces all W1-W4 obligations, not just a successful label milestone.

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

| Stage | Scope and prerequisite | Completion boundary |
| --- | --- | --- |
| M0 - Requirements | Independent Requirements Reviewer v2 checks these requirements, brief and D001-D015. | Resolve findings through the author; at most one repair/re-review. A draft is not approved by silence. |
| M1 - Label value | R-01 through R-03 after M0; reviewed public detail and available isolated runner. | The ten label criteria and the contract/test/implementation gates above, with no integration or I/O. |
| M2 - Dispatch foundation | R-07 through R-11, R-15/R-16; reviewed severity, argument, snapshot, lifetime and reporting contracts. | Synthetic recipients prove concurrency, route isolation, callbacks, ownership and safe failures; no file/backend dependency for these checks. |
| M3 - Label integration | R-04 through R-06 and R-14 after M1/M2. | Reviewed annotations/origins, capture and whole-entry filtering; inherited-label and denied-recipient canaries before handoff. |
| M4 - Text and fallback | R-12/R-13, R-20/R-21 after M2/M3; authorize uniquely owned isolated fixtures first. | Warning/exception fidelity, escaped framing, lazy non-destructive allocation, recovery and uncertain-write cases; preserve shared/local failure distinctions. |
| M5 - Bridges | R-17 through R-19 after route/lifetime, label/capture and failure contracts; reviewed P09 details and exact dependencies. | Both directions, ordered scope/state/origin fidelity, enablement and private cycle guard; controlled synthetic round trips only. |
| M6 - Full-v4 qualification | R-22/R-23 and all W1-W4 evidence; support/tooling prerequisites may be separately authorized earlier where needed. | Complete G1-G6, including approved rename/support/package work and consumer examples; this is not publication authority. |

## Verification Gates

G1-G6 are pending evidence obligations from D014, not statuses passed by this document or by M1 alone.

| Gate | Required evidence and owner boundary |
| --- | --- |
| G1 | Approved behavior and exact public/member/file inventory; Interface Architect and independent Contract Reviewer. P08/P09 delegate detail, not new policy or extra files. |
| G2 | Authorized tooling, actual discovery, synthetic recipients and uniquely owned isolated fixtures; generated protected specification/input baseline. No arbitrary existing-file deletion. |
| G3 | Test Designer/Test Auditor separation, nonzero observed behavioral/boundary/concurrency results with identities, no unexplained skips and unchanged approved expectations through implementation. |
| G4 | Approved build/package and complete intended test/consumer legs; Windows Framework and cross-platform core evidence; actual assets/metadata/XML/source/symbol and fresh license/advisory review. |
| G5 | Threat Modeler design assessment and independent implementation security review: raw-payload/reporting canaries, framing, lifetime/concurrency and private bridge-context checks. |
| G6 | Compiled examples and verified migration/label/rendering/lifetime/failure guidance plus complete W1-W4 trace and release narrative. |

No tests, builds, packages, runtime inspection, security clearance or independent review are established
by authoring requirements. Record execution identity/configuration and protected inputs with real results;
static counts and diagnostics do not substitute for execution. Release operations need separate exact authority.

## Downstream Readiness

| Owner | Ready input and remaining gate |
| --- | --- |
| Interface Architect | M1 validation/equality/immutability boundaries are specified; exact normal exception/member/operator details require its contract and independent review. Later origins and policies remain separate stages. |
| API Designer | No HTTP resource, verb, endpoint or authorization contract is selected; no HTTP workstream is fabricated. |
| Test Designer | Ten M1 criteria are measurable; reviewed contract details, actual runner discovery and protected specification records precede execution claims. Later I/O requires isolated-fixture authority. |
| Threat Modeler | Application identifiers/payloads are consumer-classified; recipient raw handoff, text output, stderr/subscribers and controlled bridges are trust boundaries. M1 crosses none of those output boundaries. |
| Implementer | M1 scope is bounded, but implementation waits for reviewed contracts, audited specifications and available focused verification. Later stages and release are not implicitly authorized. |

No new owner-policy question is introduced. P08/P09 detail review, P12 exact dependency/file/rename
authorization and runner availability remain dependencies, not reasons to re-present accepted P01-P12.
