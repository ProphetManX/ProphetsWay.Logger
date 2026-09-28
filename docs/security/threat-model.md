# Scoped Label Policy Threat Model

This is a design-time review standard for the in-memory label value and pure destination
label policy. It translates accepted obligations into threats and review expectations;
it creates no new product requirements and passes no implementation security verdict.
It is not a whole-Logger assessment, dependency audit, deployment assessment or release clearance.

The [ON-04 M2-A extension](#on-04-m2-a-severity-boundaries) below adds native severity
and argument-boundary design input. The preceding label/policy model retains its
ON-03 historical checkpoint and statuses; this extension does not re-grade that work.

The [M4-A promotion](#m4-a-native-rendering-and-framing) supplies the current bounded
M3-carrier/M4-A rendering model. Earlier sections remain historical assessments.

## Basis And Scope

Opened sources are [SensitivityLabel.cs](../../ProphetsWay.Logger/SensitivityLabel.cs),
[LabelFilterMode.cs](../../ProphetsWay.Logger/LabelFilterMode.cs), and the complete
[DestinationLabelPolicyTests.cs](../../ProphetsWay.Logger.Test/DestinationLabelPolicyTests.cs).
The policy basis is the fixed *Immutable Destination Label-Filter Contract*, including
its complete XML, matrix and B01-B24, and its independent contract review. ON-03 binds
that contract; the external author report records its exact operational provenance.
The operative rules below remain readable without those external artifacts.

Owner policy is [decision-log.md](../decision-log.md), especially D003-D005, D008-D013
and D016-D021. [requirements.md](../requirements.md) supplies the aligned acceptance
criteria; [product-brief.md](../product-brief.md) supplies purpose and actors. Their
historical pending-alignment wording is not implementation evidence. Data treatment is
in [data-classification.md](data-classification.md).

| Surface | Evidence status at the ON-03 document-authoring checkpoint | Model boundary |
| --- | --- | --- |
| `SensitivityLabel` | Existing source read completely. | Immutable opaque `Identifier`, validation, equality and collection hash; R-01 through R-03. |
| `LabelFilterMode` | Existing source read completely. | Non-flags enum: `NoFilter=0`, `Exclude=1`, `AllowOnly=2`. An enum alone performs no filtering. |
| `DestinationLabelPolicy` | Reviewed target awaiting implementation at this checkpoint, not an implementation assessed here. | Sealed class; required `(LabelFilterMode mode, IEnumerable<SensitivityLabel> labels)` constructor; getter-only `Mode`, `ReadOnlyCollection<SensitivityLabel> Labels`; synchronous `bool Allows(IEnumerable<SensitivityLabel> effectiveLabels)`. |
| Policy specifications | Source read, not executed or independently audited by this document author. | Synthetic examples of the reviewed contract, not evidence of runtime security or delivery. Missing policy export before implementation is expected, not a security defect by absence. |
| Logger, destinations, files, reporting and bridges | Future integration obligations only; their existing source is not audited here. | No claim that present output paths enforce the new predicate, payload withholding, F1-F3 or bridge rules. |

The scoped value/predicate introduces no principal, tenant record, endpoint, registration,
network connection, database, persistent store, serialization or log rendering. It is a
core utility inside the consumer process, not a SaaS service. Nothing here establishes
source-exposure, package-publication or platform qualification.

## Actors And Design Interview

| Question | Scoped answer and authority |
| --- | --- |
| Actors and trust | The application developer configures identities and membership; the host/operator controls recipients and storage; integration authors supply mapping and sequence implementations. These are the brief's actors, not new security principals. D013 assigns their responsibilities. |
| Input trust | Upstream-influenced strings need validation. Supplied sequence access is executable consumer code in the same process, not a sandboxed data reader; B20/D008. An untrusted upstream sender is an abuse scenario, not a claimed deployed user. |
| Tenancy and row ownership | There is no tenant or row in this surface. Whether a consuming application shares data across tenants is consumer-owned under D013; the policy cannot establish tenant isolation. That deployment-specific facet does not block this predicate model. |
| Topology | Synchronous in-process calls and object-reference ownership boundaries only, as defined by the read source and B01/B23. No hosting, network or storage topology is assumed. |
| Retention, erasure and consent | No duration, deletion facility or consent flow is selected. Any retained/exported label data is the consumer's responsibility under D013/R-24. Immutability is not retention enforcement or erasure. |
| Compliance | No actual business data or jurisdictional/organizational facts were supplied or inspected. Consumer applicability remains D013's responsibility; conditional triggers are below. |

One precautionary data assumption is recorded: identifier meaning, collection membership,
associated derived results and foreign diagnostic contents are **unbounded** and treated
as provisionally Confidential, not presumed harmless or public. A consumer's actual data
inventory and classification decision replaces this
assumption. This is an analysis classification, not a built-in label taxonomy or an
authentication/encryption feature. Finite readable inputs, stable during capture, are
the reviewed normal-completion precondition, not a new deployment assumption.

## Data Flows And Trust Boundaries

These are logical validation and ownership crossings within one process, not isolation
between mutually distrustful processes. No transport security is implemented by a method call.

| Boundary | Crossing and data | Required distinction |
| --- | --- | --- |
| TB1 | Consumer supplies `identifier` to `SensitivityLabel`; receives a label or argument error. | Validate the exact input without reinterpreting it; identifier readback is intentional, while validation errors must not copy/encode supplied content. |
| TB2 | Configurator supplies `mode` and `labels` to policy construction; sequence code runs during capture. | Successful construction owns copied, unique ordinal membership and immutable mode. A read-only wrapper over caller-writable storage is insufficient. |
| TB3 | Caller supplies already accumulated `effectiveLabels` to `Allows`; receives a Boolean or exceptional completion. | The caller/integration supplies entry plus inherited membership. The predicate neither discovers origins nor proves their completeness. |
| TB4 | Reference holder reads `Identifier`, equality/hash outputs, or policy `Mode`/`Labels`. | Values/read-only membership may return to that caller; writable policy backing storage must not escape. No API response or log export follows. |

For successful policy construction, configured membership C is copied before return,
with one representative per ordinal identity, including in `NoFilter`. For a valid
effective membership E, the reviewed predicate is:

| Mode | Normal result |
| --- | --- |
| `NoFilter` | `true` for every valid E, including empty E, regardless of retained C. |
| `Exclude` | `true` exactly when C and E do not intersect; empty C excludes nothing and empty E is permitted. |
| `AllowOnly` | `true` exactly when E is nonempty and every identity in E belongs to C; empty C denies every valid E. |

Unknown valid identities participate normally. Duplicates and ordering change no match;
case and Unicode-normalization variants remain distinct. An inherited-only E is labeled.
Policy membership is not origin transport and must not replace that later representation.

## Exposure And Authorization Responsibility

| Entity/carrier | Permitted local readback | Input-only part of this contract | Non-exposure boundary |
| --- | --- | --- | --- |
| `SensitivityLabel` | Exact `Identifier`, equality result and collection hash. | Constructor argument is also retained as `Identifier`; it is not a hidden credential. | Supplied identifier content is excluded from its validation messages/data. No secret field or server-only entity exists. |
| `LabelFilterMode` | Public enum constants and the selected policy mode. | Invalid enum values are rejected by policy construction. | No mode proves caller identity, clearance or recipient authorization. |
| `DestinationLabelPolicy` | `Mode`, unique read-only `Labels`, and the `Allows` result. | `effectiveLabels` has no public effective-set readback or transport member. | No returned reference exposes writable policy storage; no payload is returned or delivered by the predicate. |
| Sequence failure | Exceptional completion to the invoking code. | Consumer sequence implementation/state is not policy configuration readback. | Exact foreign exception contents/wrapping are unspecified; this boundary is not R-16's sanitized reporting API. |

There is no server or HTTP response contract here. Every retrieval is by code holding
the object reference, not by an authenticated user/tenant identifier. The host must decide
who may obtain those references or receive any serialized data under D013; no role or new
authentication scheme is invented. Blanket-serializing a label or policy would expose
identifiers and configured membership. Intentional local getters do not authorize that export.

`Allows == true` establishes **membership permission only**. It does not authenticate a
recipient, prove correct classification, authorize a business row, enforce severity, or
show that any formatter/callback/export was gated. `false` is a normal mismatch, not an
output failure. A failed evaluation supplies no Boolean. Route activity, fallback and
recipient withholding remain the separate integration obligations below.

## Ranked Review Expectations

Priority ranks consequence within this narrow design, not findings against current code.
Every expectation traces to existing rules; no additional global security baseline is created.

| Priority | Expectation and threat defended against | Existing trace |
| --- | --- | --- |
| 1 | Preserve the exact truth table, including empty sets, inherited-only membership and whole-set Allow-Only. This prevents a permissive Boolean for membership the configured rule denies. | D004; R-05/AC-05.1-3; B11-B15. |
| 1 | Reject undeclared mode values, including 3, with exact `ArgumentOutOfRangeException` naming `mode`. Null sequences produce exact `ArgumentNullException`; null elements produce exact `ArgumentException`, naming `labels` or `effectiveLabels`. A null anywhere in finite input remains invalid after a decisive prefix in every mode. Sequence-access failure produces neither a usable policy nor a Boolean; no prefix recovery. This prevents invalid/failed capture being treated as permission. | D007; AC-08.3; B02-B05/B18-B21. Competing-error precedence and foreign exception details remain unspecified. |
| 1 | Copy configured membership before constructor return; preserve mode and membership across reads/evaluations and later source changes. Returned collection mutation throws `NotSupportedException` without exposing writable backing storage. This prevents a caller's later collection edit from changing the configured decision. | D005; R-10's immutable-setting facet; B06-B09/B17. Atomic route publication itself is outside this predicate. |
| 2 | Preserve exact ordinal, case-sensitive, unnormalized identity and immutable labels; never substitute object-reference equality or a hash-only comparison. This prevents unintended aliasing or loss of a configured match. Unknown valid identities stay valid, not registry errors. | D003/D010; R-02/R-03; B10/B11. Hash collisions are permitted. |
| 2 | Preserve existing label validity: 1-256 UTF-16 units, with `Char.IsWhiteSpace` and `Char.IsControl` false for each code unit on the executing runtime. Do not copy/encode supplied content into label argument errors. This rejects the approved invalid identities and avoids diagnostic echo of rejected data. It is not a general payload sanitizer. | R-01; complete `SensitivityLabel` XML/source. This existing identifier bound is not a new collection-size limit. |
| 2 | Preserve the small synchronous public surface, non-mutating evaluation and stable concurrent reads of unchanged valid memberships. Do not add origins, rendering, fallback, recovery or classification behavior. This prevents a predicate from acquiring unreviewed disclosure/side-effect authority. | D008/D010/D013; B01/B07/B16/B17/B20-B24. Rollback of caller sequence-code effects is not promised. |

The highest-consequence modeled path is an incorrect or failed membership check being
treated as permission and later handing a sensitive payload to a denied recipient.
The first in-scope control is exact membership evaluation with exceptional completion,
not a permissive prefix result, for failed finite capture. Actual raw-payload withholding
still needs its own integration review; this is not a discovered runtime disclosure.

## Abuse Cases

| Scenario actor and goal | Concrete path | Accepted mitigation or explicit limit |
| --- | --- | --- |
| Upstream-influenced producer attempts to make restricted data appear eligible. | Omits a needed identity or supplies a visually similar but ordinally different one to TB1/TB3. | Exact identity is deliberate, not normalization or authenticity. Correct classification/mapping and supplying inherited labels remain D013/R-04 responsibilities; the predicate cannot inspect payload meaning. |
| Sequence provider tries to obtain a normal decision from incomplete input. | Supplies a decisive prefix followed by a null or a failing sequence operation at TB2/TB3. | Finite validity and exceptional completion, B18/B19; neither NoFilter nor a decisive match licenses partial recovery. No exact traversal count/order is demanded. |
| Code retaining configuration tries to alter future eligibility after construction. | Clears/replaces source list/array entries or mutates `Labels` through collection interfaces at TB2/TB4. | Independent copied backing membership and read-only return contract, B06-B09. Changes during capture are unsupported, not made safe by this rule. |
| Hostile or faulty sequence code consumes the calling thread or performs unrelated effects. | `IEnumerable`/enumerator access allocates, blocks, recurses or never terminates at TB2/TB3. | B20/B21 explicitly provide no sandbox, effect rollback, arbitrary termination/resource bound or mutation guarantee. Consumer code selection and stable input are the boundary; no new quota/deadline is required. |
| A reference holder or diagnostic adapter discloses label meaning. | Exports `Identifier`/`Labels`, or forwards consumer exception contents from TB1-TB4. | Readback is intentional, with consumer exposure responsibility. Existing label argument errors exclude input echo; foreign errors are not promised sanitized. R-16 is a future reporting boundary, not implemented here. |
| An integration author accidentally bypasses a configured denial. | Treats `false` as output failure, restores fallback, or renders before checking the predicate. | D004/R-06/R-15 forbid those integration behaviors. The pure policy has no destination/fallback operation and cannot alone enforce this path. |

## STRIDE By Crossing

Each row covers all six categories without inventing external endpoints or services.

| Crossing | Spoofing | Tampering | Repudiation | Information disclosure | Denial of service | Elevation of privilege |
| --- | --- | --- | --- | --- | --- | --- |
| TB1 | A valid identity is not proof of its asserted meaning. | Exact validation plus immutable ordinal identity; no rewriting. | No audit identity/trail; consumer obligation only. | Readable opaque text; no input echo in label validation errors. | Existing 256-unit identity limit only, not a process-resource promise. | A label grants no user/tenant privilege. |
| TB2 | Configurator/sequence code already executes in the host; no authentication claim. | Copy before return, immutable mode and membership. | Construction is not an auditable authorization event. | Configured labels remain readable; sequence exceptions are unbounded. | Supplied executable sequences have no arbitrary termination/resource guarantee. | Constructing a permissive policy is not acquiring recipient credentials or rights. |
| TB3 | Caller-supplied effective labels do not prove trustworthy classification. | Exact truth table and complete finite validity; stable input required. | Boolean/exception is not a delivery receipt or audit record. | Result can reveal a membership relationship; no external decision-oracle API is supplied. | Blocking/throwing/nonterminating sequence code is not sandboxed. | `true` is membership permission, not authorization; no failed-check rescue. |
| TB4 | Possession of a reference is not an authenticated principal. | Read-only views cannot expose writable policy backing state. | No access log or non-repudiation promise. | Identifier, membership and derived hashes are not redacted or anonymized. | No transport, request-rate or serialized-caller guarantee is introduced. | Readback does not authorize publication, a business row or a destination. |

## Future Integration Review

These are existing accepted obligations for later work, not tests or implementation claims
for the pure predicate. Existing logger/file/bridge code has not been graded against them here.

| Dependent boundary | Accepted rule to review when implemented |
| --- | --- |
| Entry/scopes to effective labels and route snapshots | R-04/R-10/R-14: accumulate inherited identities, preserve origins independently of deduplication, and capture complete immutable settings. Membership copying is not arbitrary-object deep cloning. |
| Registration/direct destination to formatter/raw callback/export | R-06: withhold message, exception/data, typed metadata and scopes before a rejected recipient sees them; built-ins/bases also enforce direct-call eligibility. Custom direct calls and producer code holding payloads are not sandboxed. |
| Failure to subscribers, stderr and propagated errors | D009/D018 and R-15/R-16: safe bounded summaries exclude labels, raw causes, exception contents and actual paths. Fixed guidance may use generic locations only. Configured rendering/output failures propagate after independent attempts/reporting; capture/check failures retain AC-15.3's distinct default/strict policy. Do not transfer either rule to the pure method's foreign exceptions. |
| Output to files | D012/D018-D020 and R-20/R-21: fixed paths, initial recovery versus remembered establishment failure versus later append/create, no replay or implicit truncation. External interference/content suitability remains developer-owned, not a new physical-identity or binary-detection requirement. |
| Text and controlled bridges | R-13/R-17-R-19: framing is not redaction; external labels need explicit mapping, origins survive, and visited-route context is private. No bridge cycle guard or text-rendering behavior is supplied by this predicate. |

D021's isolated fixture permission is settled; this document requires no file fixture or
operation. Raw payloads, paths, retained exception archives and business entities have not
been inventoried or classified as if they were implemented by the policy.

## Accepted Limits And Consumer Decisions

The owner selected opaque identity instead of a taxonomy (D003), optional filtering with
valid empty configurations (D004), membership capture without arbitrary deep cloning or
producer-mutation support (D008), and consumer-controlled classification/access/storage
(D013). The reviewed B20/B21 sequence limits preserve that core-utility boundary. These
are accepted design limits, not new High findings merely because a quota or sandbox is absent.
No additional risk is accepted on the owner's behalf by this model.

No compliance obligation is asserted. For example, personal information in actual consumer
labels or logs, together with applicable territorial/organizational facts, could trigger
GDPR or UK GDPR duties. The settling consumer-deployment question would be: "Which natural
persons' data is processed, in which jurisdictions, and in what organizational role?"
No such facts are present here. Retention limits, erasure paths and any consent requirement
depend on that consumer assessment; no duration or legal basis is invented. Collection
hashes do not anonymize identifiers, and labels are not credentials.

**Open Questions proposed for ON-03:** None. No unresolved actor, tenancy or security-policy
choice blocks this scoped model. Unknown consumer data/deployment details block only that
consumer's exposure, storage and compliance assessment, not the reviewed in-memory predicate.

## Handoff

The parent independently checks scope and trace before Security Reviewer v2 compares
actual completed label/policy code and the separately authorized dependency evidence.
Known contract limits must remain limits, not retroactive requirements. Later reviewers
need separate implementation/evidence for the future crossings above. API Designer v2
has no HTTP workstream here; an exposure table would need revisiting only if a real
external API is subsequently selected. No execution, dependency clearance or security
verdict is established by authoring this document.

## ON-04 M2-A Severity Boundaries

**Design checkpoint: 2026-09-17.** This additive section supplies the owner-approved
ON-04 revision-2 native M2-A input for independent C# contract review. Revision 2
inherits revision 1 and the exact proposal's behavior; its task-path amendment grants
this author no task or execution authority. Exact external provenance is in the run
report. This is requirements input, not source approval, completed implementation,
whole-v4 qualification or permission to publish the product.

Reopened authority: [decision-log.md](../decision-log.md), D002, D007 and D010, and
[requirements.md](../requirements.md), R-07/R-08. The owner ratification reads:
"Accept all recommendations for items P01 thru P12, all of them as recommended."
D002 also records: "for Critical we should require both an exception and message
(where warn is optional exception, and error is optional message)". The native
severity-only part of R-06/AC-06.2 and the narrow null/content compatibility from
R-12/AC-12.1 apply; their remaining integration/rendering requirements are not imported.

Opened source for the local controlling path:
[LogLevels.cs](../../ProphetsWay.Logger/LogLevels.cs),
[LoggingDestinationCore.cs](../../ProphetsWay.Logger/LoggingDestinationCore.cs),
[Logger.cs](../../ProphetsWay.Logger/Logger.cs),
[EventDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/EventDestination.cs),
[GenericEventDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/GenericEventDestination.cs)
and [TextBasedDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/TextBasedDestination.cs).
These are pre-M2-A source observations, not evidence that the target is implemented:
the core contains mask parsing, all-bits comparison and message/exception massage;
ordinary Logger dispatch checks a destination before calling it; supplied event/text
entrypoints independently reach massage and callback/print. The guard must therefore
cover the supplied direct path as well as the Logger path. The matching
[classification extension](data-classification.md#on-04-m2-a-field-extension) inventories
the complete ordinary, typed, metadata and direct-recipient carriers.

### Scoped Interview And Trust

| Question | Answer for M2-A |
| --- | --- |
| Actors and authority | D013 places classification, recipient access/authentication and storage with the consuming developer/host. Synthetic producers, configurators and recording recipients exercise the selected boundary; an upstream-influenced producer is an abuse scenario, not an invented deployed principal. |
| Tenancy and ownership | No tenant, business row or ownership lookup is added. Possession of a destination/event-argument reference permits its existing local operations; severity is not row authorization. Consumer-specific tenancy remains outside this slice. |
| Topology and trust | Synchronous in-process library calls into consumer-selected recipients. A renderer, subscriber or custom implementation is executable code with host privileges, not a separate security sandbox. No server, transport, database or deployment is selected. |
| Retention, erasure, consent and compliance | No new store or duration is selected; only synthetic evidence is authorized. D013 leaves real consumer duties undecided by this library. Those unknowns block consumer deployment/data certification, not native bit/argument contract review. |

**Assumption S-A1:** Actual message, exception and metadata contents remain unbounded;
provisional Confidential handling is precautionary, not identification of unseen PII
or permission to log real data. A consumer's actual inventory/classification replaces
it. No other actor, tenancy or topology assumption is needed for this in-process slice.
Labels and severity values are neither authentication nor a sensitivity clearance.

### Exact Contract Obligations

Priority expresses consequence within this slice, not a finding against source.

| Priority / ID | Required property and threat defended against | Trace |
| --- | --- | --- |
| 1 / S1 | Exact message bits are Critical=1, ErrorOnly=2, WarningOnly=4, InformationOnly=8, DebugOnly=16, TraceOnly=32. Inclusive masks Critical/Error/Warning/Information/Debug/Trace are 1/3/7/15/31/63. Remove Security/SecurityOnly; no CriticalOnly alias, replacement concern or compatibility wrapper. This prevents old numeric/name meaning from silently selecting different recipients. | D002; AC-07.1; ON-04 target 1. |
| 1 / S2 | Destination masks accept every combination of known bits, including zero; accepted enum, integer, numeric-string and recognized case-sensitive name combinations have identical eligibility. Reject negatives, unsupported bits and malformed names with ordinary argument errors, never Information fallback. Named-enum membership is not bit validation. This prevents representation-dependent permission or permissive recovery. | D007; AC-08.1; target 2. |
| 1 / S3 | Raw message masks are nonzero known-bit combinations; zero, negatives and unknown bits are argument errors. For valid masks, all requested bits must be present: `(messageMask & destinationMask) == messageMask`. Message 9 reaches 15, not 8. Destination zero is active reject-all, not a missing route or reason to restore fallback. This prevents invalid/overlap-only selection or bypass through denial recovery. | D002/D007; AC-07.2/AC-08.1; target 3. |
| 1 / S4 | Ordinary, typed and metadata helpers emit the same exact bits and apply the same guards: Trace/Debug/Info/Warn reject null message; Error rejects null exception but permits absent message; Critical requires both. Empty/whitespace messages remain valid. Reject null destination registration inputs before side effects. Guards precede dispatch/fallback side effects. This prevents invalid calls from handing off payloads or reaching fallback setup. | D007; AC-08.2/AC-08.3; target 4. |
| 1 / S5 | Supplied event, generic-event and text direct entrypoints validate the message mask and enforce destination eligibility before `MassageLogStatement`, recipient rendering, event callback or `PrintLogEntry`. A valid mismatch invokes none of them. This prevents a direct caller from bypassing the ordinary Logger check and exposing raw or rendered payloads. Arbitrary direct custom implementations are not sandboxed. | D010; AC-06.2, severity-only; target 3. |
| 2 / S6 | Preserve permitted raw/direct absent message/exception values without fabricating content or dereferencing an absent exception. Supplied message and exception detail/context survive the changed ErrorOnly bit, warnings and valid composites. This prevents null-triggered failure or severity-dependent loss on otherwise permitted input. No new framing, timestamp or formatter system follows. | D007; narrow AC-08.2/AC-12.1; target 5. |
| 2 / S7 | Typed metadata receives no new non-null restriction or mandatory metadata-interface constraint. Preserve existing label/policy contracts and namespaces; add no label integration, registry/snapshot/lifetime, failure-reporting/privacy API or bridge behavior. This prevents a severity correction from silently changing unrelated calling or exposure contracts. | D010; target 4 and preservation/exclusions. |

Exact helper signatures, defaults, exception types and parameter names belong to the
Interface Architect's bounded inventory and independent Contract Reviewer. This model
sets no competing-error priority, exception-message syntax, string grammar beyond the
approved recognized representations, payload content restriction or new security policy.
Rejection-before-effects is an ordering obligation, not a priority among invalid arguments.

### Crossings And Exposure

| Boundary | Data flow | Required ownership/exposure rule |
| --- | --- | --- |
| SV1 | Configurator supplies a destination mask representation to the core; caller supplies a message mask to validation. | S2/S3 distinguish valid configuration, invalid input and normal mismatch. A validity/eligibility result proves neither identity nor authorization. |
| SV2 | Ordinary/typed/metadata helper supplies exact severity, message, exception and optional typed value to dispatch; registration supplies a destination reference. | S4 applies before dispatch/fallback effects. The host selects who may configure recipients. Explicit reject-all remains present; denial does not authorize another output. No new registry policy is selected. |
| SV3 | Logger or direct caller enters supplied event/generic-event/text `Log`, then massage, event arguments/callback or rendered text/`PrintLogEntry`. | S5 gates all recipient work before payload observation. After acceptance, the existing recipient may intentionally receive raw message/exception and typed metadata or rendered text; no redaction, deep clone, recipient authentication or downstream durability is promised. |

| Entity/carrier | Permitted accepted exposure | Input-only or withheld part |
| --- | --- | --- |
| Destination configuration and message severity | Existing eligibility result and event `LogLevel`; approved severity names may appear in rendered text. | Constructor representation is configuration input, not a new exported property. Invalid input does not become eligible. |
| Ordinary and generic `LoggerEventArgs` | `Message`, `RawMessage`, `Exception`, `LogLevel`, `Timestamp`; generic events also expose `Metadata` to their intended callback. Exception references can expose `Data` and nested causes even when massage only renders message/stack. | A rejected entry supplies none of these payloads through callback or recipient rendering. The caller already holding the originals is not deprived of them. |
| Text record | Existing combined timestamp/level/message/exception text to the selected in-memory recording recipient. | No text reaches `PrintLogEntry` on rejection. No file/console output, framing promise or public API response is introduced. |

No HTTP response, authenticated retrieval or server-only field exists here. Do not treat
blanket serialization of event arguments as authorized: it would export raw diagnostics
and arbitrary metadata as well as rendered text. D013 assigns such onward access to the
consumer; an eligible severity does not establish that its contents are suitable for it.

### Abuse Cases And STRIDE

| Actor and goal | Path | Required defense or accepted limit |
| --- | --- | --- |
| Faulty or upstream-influenced configurator seeks a more permissive recipient. | Invalid name/negative/unknown bits or an unnamed valid composite at SV1. | S2 rejects invalid input consistently and accepts valid bit combinations without named-value or Information fallback. |
| Caller seeks a recipient excluding part of the entry. | Message 9 against mask 8, zero raw mask, or direct `Log` avoiding Logger at SV1/SV3. | S3/S5 require valid nonzero message masks and all-bits permission before rendering/callback; zero destination rejects all. |
| Producer causes work from a malformed helper/registration call. | Null required message/exception/destination reaches SV2, including an otherwise empty route. | S4 rejects before dispatch/fallback or registration effects. Tests must use explicit synthetic routes; the parent review checks ordering without executing fallback. |
| Producer supplies a permitted absent exception or composite with content. | Shared massage at SV3 assumes an exception or only handles an old cumulative name. | S6 preserves supplied content and permitted absence; arbitrary consumer code remains executable and unbounded. |

| Crossing | Spoofing | Tampering | Repudiation | Information disclosure | Denial of service | Elevation of privilege |
| --- | --- | --- | --- | --- | --- | --- |
| SV1 | A severity name is not an identity. | Equivalent bit validation; no permissive parse recovery. | Argument/result is not an audit record. | Configuration/results can reveal routing; no safe external diagnostic API. | Reject invalid masks; no new rate or size limit. | A valid mask grants no host, tenant or row privilege. |
| SV2 | Helpers do not authenticate producers/recipients. | Exact bits and consistent argument guards precede effects. | No receipt, audit trail or durability promise. | Invalid arguments must not cause payload dispatch/fallback effects; metadata remains unbounded. | Do not turn null misuse into dispatch work; custom recursion/registry concurrency is outside this slice. | Selecting a route is not access authorization; no label-derived rights. |
| SV3 | Callback identity is host-selected, not proven by eligibility. | Direct and dispatched calls apply the same all-bits rule before recipient work. | Callback/print return is not tamper-proof evidence. | Denied recipients get no raw/rendered entry; accepted callbacks retain deliberate raw exposure. | Null-safe compatibility avoids accidental absence failure; accepted renderers/subscribers are not sandboxed or time-bounded. | Direct custom overrides remain application responsibility; supplied guards do not isolate hostile in-process code. |

The highest-consequence modeled exposure is an excluded direct recipient observing an
unbounded raw exception graph or typed payload before severity rejection. The first
required control is S5: validate and decide all-bits eligibility before any supplied
recipient rendering/callback. This is a design risk, not a discovered exploit or a
security verdict. Numeric validity cannot establish payload sensitivity or correctness.

### Limits And Next Gate

Breaking Security removal is accepted, as are composites, active reject-all, optional
raw/direct content and unbounded consumer metadata. D013 assigns real-data classification
and recipient/storage access to consumers; no further risk is accepted here. Existing
label/policy facts and their ON-03 statuses above are not rewritten or re-certified.

No file, fallback/recovery redesign, framing/UTC, failure reporting, privacy API, bridge,
new collection/encoding input family or Microsoft conversion/None/ordinal work is included.
No-setup Trace delivery and all-six automatic-file coverage remain unclaimed. Real user
data, deployment, retention/erasure implementation and compliance certification remain
out of scope. No new compliance obligation is inferred; the conditional consumer-data
triggers described above still require actual data and jurisdictional/organizational facts.

**Open Questions proposed for ON-04 M2-A:** None. Public helper signatures and ordinary
argument exception details are the next contract author's bounded work, not new product
questions. Consumer data/topology/retention decisions block only a later consumer assessment.

Next: Interface Architect supplies the exact C# inventory, then independent Contract
Reviewer checks it against S1-S7. Security Reviewer reviews actual implementation and
fresh scoped evidence later, without transferring full-v4 requirements into this slice.
No HTTP API Designer handoff is needed. This model ran no build/test and supplies no
dependency, implementation, public-product or release clearance.

## M2-B Explicit Registration Membership

**Design checkpoint: 2026-09-18. Stage: SHAPE.** Additive input for independent C#
contract review against M2-B revision 2, inheriting revision 1. The setup amendment
changes no behavior; approved task setup is not an unexplained baseline change or
authority for this author to execute tasks. Earlier ON-03/ON-04 assessments retain
their dates and scope. No implementation verdict or acceptance expansion follows.

Reopened [decision-log.md](../decision-log.md), D005/D006/D010/D013, and
[requirements.md](../requirements.md), AC-09.1, AC-10.1-3 and the borrowed-resource
part of AC-11.1. D005 states "Ordinary and exact declared-metadata-type routes are
independent" and "Reject duplicate same-instance registration on one route; allow
the same object on distinct compatible routes". D006 states "Explicitly supplied
destinations, factories and loggers are borrowed. Removal never disposes them".
Only explicit destination membership is modeled here, not factories or bridges.

Read completely for this boundary: [Logger.cs](../../ProphetsWay.Logger/Logger.cs),
[Generics/Logger.cs](../../ProphetsWay.Logger/Generics/Logger.cs),
[LoggingDestinationCore.cs](../../ProphetsWay.Logger/LoggingDestinationCore.cs),
[IDestination.cs](../../ProphetsWay.Logger/IDestination.cs),
[ILoggingDestination.cs](../../ProphetsWay.Logger/ILoggingDestination.cs) and
[Generics/ILoggingDestination.cs](../../ProphetsWay.Logger/Generics/ILoggingDestination.cs).
They establish the registration, eligibility and raw-callback carriers, not proof
that M2-B is implemented. The ordinary route key and typed `typeof(T)` selection
make metadata declared as `ILoggingDestination` an explicit review case. Source
also forwards an unconfigured typed route to ordinary logging: that fallback is
excluded and unqualified, not evidence of whole-route independence.

### Interview And Boundary Flows

Actors are the consuming configurator/host, producers and selected destination
implementations. D013 assigns recipient access/authentication and real-data handling
to consumers. Custom eligibility/callback code runs with host-process privileges;
it is not sandboxed. Topology is synchronous in-process registration and dispatch.
No tenant, authenticated principal, business row, endpoint or deployment is added.
Consumer tenancy, retention, erasure, consent and compliance decisions block only
a future consumer assessment, not this membership contract. No such decision is
invented. **Assumption M-B1:** opaque message/exception/metadata contents remain
unbounded and provisionally Confidential; an actual consumer inventory replaces
this precaution, not a benign synthetic example.

| Boundary | Flow and authorization/exposure responsibility |
| --- | --- |
| MB1 | Host adds/removes a recipient reference or clears one explicit route. Private registry membership is selected by ordinary route kind or exact declared `T`. Host controls who may configure it; reference possession is not authentication. No public registry retrieval/serialization API is added. |
| MB2 | A producer call captures the route's complete ordered membership before executing eligibility. That membership, not a mutable live list, controls this call. The captured reference holder may still attempt a recipient removed afterward; this is not an access-revocation barrier. |
| MB3 | Dispatch supplies severity to `ValidateMessageLevel`; after accepted eligibility it hands original message/exception and, for typed calls, metadata to `Log`. Eligibility/callback code may mutate MB1 or recursively enter MB2 as a new call. Host controls recipient trust and quiesces users before disposing borrowed resources. |

### Required Membership Obligations

Priority ranks modeled consequence, not current-code findings. These obligations
specialize accepted behavior only; exact ordinary exception details remain D010's
contract-author/reviewer work.

| Priority / ID | Required property and threat defended against | Trace |
| --- | --- | --- |
| 1 / MB-R1 | Keep ordinary membership distinct from every exact declared metadata type, including `ILoggingDestination` and other destination-contract types. Neither runtime subtype nor assignability redirects a typed call. Prevents raw payload handoff to a different explicit route through identity aliasing. | D005; AC-09.1; target r1 independent-route rule, inherited by r2. |
| 1 / MB-R2 | Atomically publish and capture one complete ordered membership: concurrent captures see a complete old or new state, never a partly changed list. Capture finishes before the first eligibility/callback invocation; later iteration uses that membership in insertion order, synchronously. Prevents torn selection, skipped/added recipients within one call and mutation-invalidated iteration. | D005; AC-10.1/AC-10.2; target complete-snapshot rule. |
| 1 / MB-R3 | Coordinate registry writers without retaining registry locks across eligibility or callbacks. Registry identity operations must not invoke user equality/hashing at all. Prevents caller-controlled blocking/reentry from holding registry coordination or changing the identity decision. This does not constrain destination-owned locks. | D005; R-10; target lock and reference-identity rules. |
| 1 / MB-R4 | Add/remove use reference identity. Refuse the same instance twice on one route before mutation; distinct equal-valued objects remain distinct, and the same object may occupy distinct compatible routes. Absent removal is a no-op; clear is route-scoped. Prevents equality-based substitution/removal and duplicate fanout on one route, without inventing cross-route deduplication. | D005; AC-10.3; target duplicate/removal rules. |
| 1 / MB-R5 | Mutation returns after publication, not after old calls drain. Add/remove/clear/self-removal from eligibility/callback code affects later captures only; the current call keeps its captured membership. Recursive user logging captures anew and is not blanket-suppressed. Prevents self-wait and mid-call membership rewriting. No total order across threads or serialized callers is promised. | D005; AC-10.1-3; target callback/recursion rules. |
| 1 / MB-R6 | Removal and clear never dispose borrowed recipients, including objects shared across routes. Captured calls may finish after removal returns; hosts stop producers and await synchronous calls before disposal. Prevents invalidating a still-used or independently owned recipient. No public drain, immediate revocation, owned-resource retirement or asynchronous completion guarantee is added. | D006; borrowed part of AC-11.1; target non-disposal rule. |
| 2 / MB-R7 | Preserve severity/helper validation, rejection of null add before effects, existing null-removal behavior and original payload identity. Supplied destination masks remain immutable. Membership immutability does not freeze arbitrary custom state, metadata or exception graphs; add no public settings/replacement API. Prevents a registry change from silently changing payload or guard contracts. | Target preservation rules; D010; existing ON-04 obligations remain separately dated. |

### Exposure And Abuse Cases

| Carrier | Permitted local response/handoff | Input-only or non-exposure limit |
| --- | --- | --- |
| Route identity and recipient registration | Existing eligibility result; intentional execution of the selected recipient. | Add/remove references and declared type are control inputs, not new response fields. Mutable registry backing and lock objects must not escape as public state. |
| Ordered captured membership | Internal dispatch retains the selected borrowed references for the call. | No public snapshot/list API; removal is neither payload erasure nor cancellation of that captured handoff. |
| Severity, message, exception graph and typed metadata | The selected eligible callback intentionally receives the original permitted values/references. | Eligibility receives severity, not a new payload-inspection API. Preserve existing severity guards; no label gate, redaction or external response contract is introduced. |

No authenticated row retrieval or server-only entity exists. Blanket serialization
of a recipient, metadata object or exception graph is not authorized by registration;
it can expose unbounded contents. The [field extension](data-classification.md#m2-b-membership-field-extension)
records the control carriers and unchanged raw-data boundary.

| Actor and goal | Abuse path | Required defense or accepted limit |
| --- | --- | --- |
| Faulty or motivated in-process configurator seeks cross-route delivery. | Uses destination-interface metadata or a runtime subtype to alias another explicit route at MB1/MB2. | MB-R1 separates route kind/exact declared type; no tenant-isolation claim. |
| Custom recipient seeks to substitute/remove another instance or multiply attempts. | Overrides equality/hash or repeats its reference at MB1. | MB-R3/MB-R4 avoid user equality, refuse same-route duplicate references and remove only the named instance. |
| Recipient mutates membership during delivery to affect the remaining call. | Adds/removes/clears itself or another recipient, or recursively logs, at MB3. | MB-R2/MB-R5 preserve the old complete capture and give new calls fresh captures. |
| Blocking/reentrant eligibility or callback code stalls registry changes. | Waits for another writer or reenters registration while MB3 executes. | MB-R3 releases registry coordination first. Arbitrary code may still block its own caller or recurse without bound; no sandbox, timeout or quota is added. |
| Host treats removal as immediate revocation/disposal permission. | Disposes a recipient while an older MB2 capture still holds it, including on another route. | MB-R6 requires host quiescence and library non-disposal; old captured delivery remains deliberately permitted. |

### STRIDE And Review Limits

| Crossing | Spoofing | Tampering | Repudiation | Information disclosure | Denial of service | Elevation of privilege |
| --- | --- | --- | --- | --- | --- | --- |
| MB1 | Reference identity is not principal proof. | MB-R1/MB-R4 prevent route/equality substitution. | No registration audit trail added. | Registry topology is Internal, not approved export. | No user equality under or outside registry coordination. No quota promised. | Configurator already has host access; no new permission system. |
| MB2 | Declared type is routing, not caller identity. | MB-R2 captures complete ordered membership. | Snapshot/order is not a delivery receipt or tamper-proof audit. | A removed recipient in an old capture may still receive data. | Brief writer coordination; no drain wait or serialized callers. | No row or tenant authorization follows from route selection. |
| MB3 | Host selects callback code; severity does not authenticate it. | Preserve guards and payload identity; custom state is not frozen. | Callback return proves no remote durability. | Raw graphs remain intentionally readable by eligible recipients, not redacted. | MB-R3/MB-R5 avoid registry-lock/self-drain coupling; custom blocking/recursion remains unbounded. | Same-process consumer code is not isolated or deprived of references it already holds. |

Highest-consequence modeled exposure: route aliasing or torn membership directs an
unbounded raw payload to an unintended explicit recipient. The first required control
is MB-R1's distinct ordinary/exact-type identity, exercised together with MB-R2's
complete capture before user code. This is a design risk, not an implementation finding.

D005 accepts captured-call completion, synchronous reentry and no cross-thread order;
D006 leaves borrowed-resource quiescence with the host; D013 leaves consumer access,
classification and storage with that consumer. No additional risk is accepted here.
Unconfigured typed-to-ordinary fallback is explicitly unqualified and must not be
exercised by this slice's tests. Label integration, settings APIs, owned retirement,
files/fallback operations, bridges, reporting redesign and full-route independence
remain deferred. No real data, release or whole-system assurance is claimed.

No compliance regime is inferred. Actual identifiable-person payloads plus applicable
jurisdictional/organizational facts would require a consumer privacy assessment;
which persons' data, jurisdictions and processing role are involved would settle it.
Removal is not erasure of retained recipient/producer copies. Data minimization and
consumer storage protections cannot be replaced by registration identity.

**Open Questions proposed for M2-B:** None. No actor, tenancy or policy unknown blocks
this in-process slice. Next gate is independent Contract Reviewer against MB-R1-7
after the contract author fixes the mechanical C# details. Security Reviewer compares
actual implementation and fresh scoped evidence later. No HTTP API Designer work is
introduced, and no test execution or implementation verdict is supplied here.

## M4-A Native Rendering And Framing

**Promotion checkpoint: 2026-09-27. SourceNoCodeVerdict.** This section promotes the
completed [M4-A threat/data assessment, report 02](../../../.agent-runs/20260926-2305-logger-m4a-preparation/02-m4a-framing-threat-input.md)
under the owner's "Approve the m4-a envelope" activation in
[report 05](../../../.agent-runs/20260926-2305-logger-m4a-preparation/05-owner-activation.md).
The [revision-1 envelope](../../../.agent-runs/20260926-2305-logger-m4a-preparation/m4a-implementation-r1.md)
authorizes these two canonical security documents; its pending heading is historical.
The governing design is [M4A-C1, F01-F30 and its complete XML/grammar/integration](../../../.agent-runs/20260926-2305-logger-m4a-preparation/01-m4a-contract.md),
accepted by independent [contract review 03](../../../.agent-runs/20260926-2305-logger-m4a-preparation/03-m4a-contract-review.md).
Report 02's preparation-era source status and pending-promotion paragraph remain historical,
not rewritten. Earlier canonical sections are not new audits or current implementation claims.

Reopened [decision-log.md](../decision-log.md), D008/D010/D013/D018/D022, and
[requirements.md](../requirements.md), R-10 through R-16/R-24. D008 states "Framing
prevents forged record boundaries, not sensitive-content disclosure"; D013 assigns
classification, mapping/sanitization, recipient access/authentication, storage, retention
and audit duties to consumers. D018/D022 require propagation for configured Output and
custom-severity Eligibility failures, not a blanket replacement of capture/LabelCheck policy.
The [implementation author's report 22](../../../.agent-runs/20260926-2305-logger-m4a-preparation/22-m4a-implementation.md)
is status only: its green execution is not security proof. This author supplies requirements,
not a code verdict, dependency clearance, release approval or I/O certification.

### Current Scope And Interview

The [current field inventory](data-classification.md#m4-a-current-carrier-addendum)
links the actual carrier, event, core and renderer definitions opened for this promotion.
They establish what data exists, not whether code satisfies the controls below. This is
a synchronous in-process utility: native capture, selected ordinary/exact-T recipients,
text rendering, intentional raw/event handoff and a separate bounded failure channel.

| Interview item | Bounded answer |
| --- | --- |
| Actors/trust | Consumer developer/configurator, host/operator, producer, formatter, recipient and subscriber. Upstream-influenced input is an abuse scenario. Selected executable code retains host-process privileges; no benign-code assumption or sandbox. |
| Tenancy/authorization | No tenant, business row, HTTP endpoint or authenticated retrieval is introduced. Reference holders can read documented getters; D013 makes the consumer responsible for who receives references/outputs. Severity, labels, time and correlation IDs grant no identity or row rights. |
| Topology | Local object/call boundaries only. No server, network, database or deployment is selected. An eventual export needs its own consumer assessment. |
| Retention/erasure/compliance | No new duration, erasure/consent flow, encryption mechanism or legal obligation. Scope end/removal does not erase retained copies or revoke captured calls. Consumer-specific unknowns block that consumer's assessment only. |

**Assumption M4-A1 (report 02 A1):** Unseen messages, exception/property/metadata graphs,
keys, label meanings/associations, formatter results and dynamic marker type names are
unbounded and provisionally Confidential. An inspected consumer inventory/classification
replaces this precaution. No actual PII, Sensitive PII or Secret value is claimed inspected.
No unanswered actor, tenancy or topology decision blocks the approved utility slice.

### Boundaries And Exposure

B1-B6 retain report 02's identifiers. All are local trust/ownership crossings, not process
isolation. Intentional local response is not permission for blanket serialization.

| Boundary | Flow and permitted exposure | Required withholding/control |
| --- | --- | --- |
| B1 Capture | Producer/host to completed `LogContext`, full frames, explicit attachments and captured settings. Original nested objects remain accessible to intended context holders. | Complete immutable membership before recipients; no producer re-enumeration or library-added live-stack/handle/enumerator/recipient/history backlinks. Existing capabilities inside original values are not stripped. |
| B2 Selection | Captured ordinary/exact-T route, settings and intrinsic restrictions to eligibility; public direct input to current-opening validation. | Every required severity bit and label gate must permit before any payload hook/handoff. Direct calls recheck current openings/intrinsic policy without registration lookup; timestamp equality is not permission. Denial never enables fallback. |
| B3 Hooks | Permitted message/exception to massage; metadata/property-value occurrences to `FormatValue`. | Closed default scalar recognition; explicit extensions own inspection/effects. Keys/labels/message/exception text do not use the value hook. Nontext recipients do not acquire a value-formatting invocation. |
| B4 Text/output | All content results to final token encoding, full composition, then selected `PrintLogEntry`/console wrapper. | Quote/escape every component once; no print on render failure. One complete non-null unterminated record on success. Print effects can precede an output failure. |
| B5 Raw/event | One selected contextual or legacy handoff, original optional message/exception/exact-T metadata/property values; event `Message`, `RawMessage`, `Context`, level and legacy `Timestamp`. | No rejected recipient receives any payload. Keep event massage unescaped and raw references unchanged. Context/event/exception serialization can reveal data and capabilities the text renderer never discovers. |
| B6 Reporting | Capture/check/hook/output/callback failure to subscribers, stderr and propagated safe report. | Only generated correlation, captured position, stage and counts; no payload/context/time/labels/keys/paths/names/marker or formatter text, foreign cause or backlink. Local construction errors are not silently sanitized dispatch reports. |

Settings/policy getters remain intentional local readback; registry/control handles are
input/internal state, not new export fields. Raw recipients may inspect original exception
`Data` and nested values even when default text does not. No server-only business entity
or HTTP DTO exists. Supplied guards do not constrain hostile custom direct methods,
protected-hook calls or replacement nonsealed `LogCore` bodies.

### Ranked Required Controls

Priorities rank modeled consequences, not findings against implementation. These preserve
the accepted contract; they introduce no new policy or public surface.

| Priority / control | Required property and threat defended against | Trace |
| --- | --- | --- |
| P1 / M4-1 | Whole-entry eligibility first: preserve argument, all-bits severity, registration/intrinsic label, exact-T and direct-current-opening guards before massage, formatting, callbacks or print. Prevents a denied recipient observing raw graphs or invoking its formatter. Active reject-all/mismatch is not failure or fallback permission. | F13/F26/F29; R-06; D010/D013 |
| P1 / M4-2 | Encode every emitted content component at the final text boundary, including massaged exception text, keys, label identifiers, scalars, unsupported markers and explicit-hook results. Prevents forged records and reassigned field/attachment boundaries. Only fixed renderer delimiters are syntax. | F17-F19/F24-F25; D008/R-13 |
| P1 / M4-3 | Preserve payload-free bounded diagnostics and boundary-specific failure policy after independent attempts. Prevents a formatter failure leaking content through a supposedly safe channel or being hidden by another success/reporter failure. | F27-F28; D018/D022/R-15/R-16 |
| P2 / M4-4 | Capture one offset-zero `DateTimeOffset` per originating ordinary/exact-T/direct call before callbacks. Trusted handoff/same-call forwarding reuses it; public reentry/nested calls capture anew. Direct stamped views preserve supplied frames/entry labels without mutating retained context. Prevents recipient-time misattribution and stale-context permission reuse. | F01-F05/F23 |
| P2 / M4-5 | Restrict default formatting to the exact scalar set below, otherwise the escaped `[no formatter: TypeName]` using runtime `Type.Name` only. No arbitrary getter, object `ToString`, `IFormattable`, equality/hash, enumeration or graph discovery. Prevents implicit execution/disclosure merely from logging an object. | F06-F11; D008/R-12 |
| P2 / M4-6 | Invoke explicit `FormatValue` once per metadata/property-value occurrence on successful recipient rendering; order across values is unspecified and a failed recipient may stop. Results are recipient-local unescaped data, including a permitted null result; no cross-recipient cache or raw-value writeback. Prevents extension output bypassing framing and one recipient contaminating another's text. | F12-F14/F22-F23 |
| P2 / M4-7 | Preserve entry/scope attachment presence, every full frame outermost first, ordered property pairs, duplicate/null/empty keys and duplicate label occurrences. Never substitute `EffectiveLabels` or flatten to a dictionary. `ScopeIndex` indexes label-only `ScopeAnnotations`, not full `Scopes`. Prevents loss or reassignment of origin evidence. | F20-F23 |
| P2 / M4-8 | Massage once per permitted text/event delivery; preserve optional raw values and existing exception-chain messages/available stacks for masks 1-63. No added `Data` serializer, arbitrary getter discovery, exception `ToString` or aggregate traversal. Assemble completely before one print; retain independent synchronous attempts and borrowing limits. Prevents partial text emission and text-driven changes to raw/event semantics. | F15-F16/F24-F30 |

Default scalars are null, string/char, Boolean, SByte/Byte/Int16/UInt16/Int32/UInt32/
Int64/UInt64/IntPtr/UIntPtr, Single/Double, Decimal, Guid, DateTime/DateTimeOffset,
TimeSpan and enums. Nullable boxing follows its underlying value or null. Integers use
invariant `D`, floating values `R`, Decimal `G`, Guid `D`, payload times `O`, TimeSpan `c`,
enums general names/flags or invariant underlying decimal. Strings/chars stay unchanged
before encoding; Boolean is `True`/`False`. Payload time kind/offset is preserved; only
event time must be UTC. Floating spellings/enum aliases follow the executing BCL. Numeric
pointers are not dereferenced. Scalar support does not make the content Public.

The F17 token map is exact: backslash becomes `\\`, quote `\"`, CR/LF/TAB become
`\r`/`\n`/`\t`; every other `Char.IsControl` unit plus U+2028/U+2029 becomes uppercase
four-digit `\uXXXX`. Process original UTF-16 units once: actual LF and literal backslash-n
remain distinguishable. Preserve all other units, including unpaired surrogates, without
truncation/normalization. Confusables and other visual-format characters are not a promised
visual-spoofing defense; downstream encoding fidelity is not established.

Null text is bare `null`; non-null text is double-quoted escaped content. Null, empty,
whitespace and literal `null` therefore differ. A null hook result represents a null text
token, not proof its input was null. Order is invariant call-UTC `O` with `+00:00`, full
general severity spelling padded left to 12 without truncation, massaged-message token,
typed-only metadata token even for null/default(T), entry attachment, then full scopes.
Attachments are null or ordered token lists; frames contain labels and ordered `(key,value)`
pairs. Quoted delimiter-looking content must not be parsed as structure by ignoring quotes.
This is not lossless arbitrary-CLR serialization or a general parser API.

M4-3 retains at most eight recipient descriptors during accumulation, plus overflow and
one zero-or-one core-capture count; a descriptor is one-based captured position and stage.
Stderr is at most 512 UTF-16 units including its terminator. `Eligibility` and `Output`
failures require propagation after independent attempts/safe reporting; capture-only and
opted-in `LabelCheck`-only failures retain report-and-return when no mandatory failure is
present. No new strict selector. Supplied direct calls use the one-recipient reporting
boundary once; ordinary argument/noncurrent-context errors remain local errors.
Subscribers/writer failures are contained, preserving original-failure precedence.
Suppress recursive notification on the same thread, not independent logging; neither
cross-thread cycle protection nor guaranteed reporting/rate limiting follows. The authored
safe exception interface excludes raw causes but does not erase CLR state or cover
reflection, `TargetSite`, debugger, serialization or inherited post-catch mutation.

### Abuse Cases And STRIDE

| Actor and goal | Path | Control or accepted limit |
| --- | --- | --- |
| Upstream-influenced producer forges a record or attachment | Inserts newline, separator, quote or plausible prefix through B3/B4 message, key, label, value or exception detail. | M4-2 encodes every component once and M4-7 retains native boundaries. Framing does not conceal the contents. |
| Faulty formatter injects syntax or exposes arbitrary members | Returns delimiter text or implicitly formats an unsupported object at B3/B4. | M4-5/M4-6 separate closed default recognition from explicit code and encode all successful results as data. |
| Integration bypasses rejection or reuses old permission | Formats before B2, confuses overlapping severity bits with all-bits permission, or treats retained time/context as direct authorization. | M4-1/M4-4 require every gate and fresh public-direct selection. Arbitrary custom bypasses are not sandboxed. |
| Throwing formatter discloses a secret/path through diagnostics | Foreign Message/StackTrace/Data/InnerException enters B6. | M4-3 excludes the entire raw cause and content; independent success cannot suppress mandatory failure. |
| Overlapping/reentrant calls mix records or stall the host | Shared hook state, nested logging, mutable values, blocking output or recursive notification at B3-B6. | Per-call time/structure and recipient-local complete assembly; no registry lock through consumer callbacks. Consumer hooks/sinks own thread safety and may still block, recurse, retain or mutate. No timeout, size cap or rollback is added. |
| Host mistakes output/removal for durable audit or revocation | Treats B4 return, UTC, scope end or removal as persistence, uniqueness, erasure or immediate cancellation. | Borrowed-resource quiescence and captured-call limits remain; no replay, exactly-once, trusted clock, erasure or durability promise. |

| Crossing | Spoofing | Tampering | Repudiation | Information disclosure | Denial of service | Elevation of privilege |
| --- | --- | --- | --- | --- | --- | --- |
| B1 | Labels/time do not authenticate. | Completed ordered membership; nested values not frozen. | No audit identity. | Context graphs remain unbounded. | Stable finite capture expected; sequence effects unbounded. | Handles/context grant no row rights. |
| B2 | Host selects recipients. | Complete settings and fresh direct checks. | Permission is not receipt. | Whole-entry gate before payload work. | No registry lock through callbacks; no time bound. | No tenant/authentication guarantee. |
| B3 | Hook output proves no provenance. | Closed defaults; explicit output remains data. | Effects/retention not audited. | Accepted hooks see originals, not redacted values. | Hooks/exception access can block/throw/reenter. | Explicit code keeps host capabilities. |
| B4 | Escape forged boundaries. | Exact units/tokens before one print. | UTC/print are not durable evidence. | Framed sensitive content remains sensitive. | Unbounded allocation/output possible. | Text is not trusted command syntax. |
| B5 | Metadata/event is not a principal. | Preserve raw values and exact route. | Effects may precede failure. | Raw graphs remain deliberately readable. | Recipients can retain/block/recurse. | Original object capabilities survive capture. |
| B6 | Generated IDs are not authentication. | Immutable safe facts; original failure wins. | Notification may be unavailable. | No payload/cause/path/context backlinks. | Per-call bounds, not rate/cross-thread cycle limits. | No raw control handle exposed by safe fields; CLR inspection not isolated. |

### Limits And Handoff

Highest-consequence framing exposure is forged record/attachment authority; require M4-2
first for that risk, always after M4-1 eligibility. The separate highest-consequence
diagnostic path is raw data escaping through reporting; M4-3 preserves that boundary.
These are modeled risks, not discovered vulnerabilities or accepted new risk dispositions.

D008/D013's selected limits remain: framing is not redaction/confidentiality, unsupported
markers are not arbitrary-object persistence, copied membership is not deep freezing,
and consumer code can inspect/retain/mutate original objects. Overlapping calls are not
serialized; nested/public reentry gets a new capture without changing the outer call.
Time is neither unique/monotonic nor anti-replay evidence. Consumers choose necessity,
classification, sanitization, readers, storage and cleanup; the companion inventory
states minimization, credential and conditional-compliance boundaries.

No new compliance, consent, retention, tenancy, encryption or durability guarantee.
M4-B file lifecycle/recovery, physical termination/encoding, path/fixture behavior and
automatic-route correction remain excluded. Same-call forwarding retains context/time
only; it is not certification of typed-fallback metadata transport. The console contract
retains one `WriteLine` after renderer handoff, not physical-console/file proof. Bridges,
package/platform/full-v4/release qualification are also outside this promotion.

**Open Questions proposed for M4-A:** None. Consumer deployment/data decisions remain
dependency-scoped, not reopened utility policy. Next: Security Reviewer v2 independently
grades final source/evidence against this promoted design and M4A-C1. API Designer v2 has
no HTTP workstream here. No tests, builds, Git, subagents or physical I/O were run by this
author, and no implementation-security verdict is supplied.

## M4-B1 Explicit-File Design Addendum

**Promotion checkpoint: 2026-09-27. Approved design only; SourceNoCodeVerdict.** This
promotes [scoped input 02](../../../.agent-runs/20260927-1431-logger-m4b-continuation/02-m4b1-file-threat-input.md)
as constrained by the complete [repaired snapshot 04](../../../.agent-runs/20260927-1431-logger-m4b-continuation/04-m4b1-contract-repair.md)
and [acceptance 05](../../../.agent-runs/20260927-1431-logger-m4b-continuation/05-m4b1-contract-rereview.md).
[Proposal 06's explicit Threat Modeler row](../../../.agent-runs/20260927-1431-logger-m4b-continuation/06-m4b1-execution-proposal-r1.md#exact-writes-and-owners),
approved by [activation 14](../../../.agent-runs/20260927-1431-logger-m4b-continuation/14-m4b1-activation-and-extension.md),
authorizes this append-only promotion, not a new requirements or contract review.
All preceding sections remain verbatim historical records. The
[file carrier inventory](data-classification.md#m4-b1-explicit-file-design-addendum)
records the opened declarations and F-A1's unbounded, provisional Confidential treatment.

Actors remain the consumer configurator/host, producer, selected recipient, filesystem
reader/writer and diagnostic subscriber/operator. Topology is synchronous in-process
selection/rendering followed by OS file I/O and separate local diagnostics. D013 leaves
file readership and OS permissions with the consumer; severity/labels prove neither
identity nor authorization. No tenant, business row, HTTP response or deployment is
invented. Unseen consumer data and remote-path use need their own assessment, not a
new assumption that this slice supplies isolation or protected transport.

### Accepted Controls And Boundaries

F1-F5 and C1-C6 retain 02's identifiers. Priorities rank modeled consequences, not code
findings. Every control below specializes accepted D010/D012/D020, 04/05 and inherited
M4-A obligations; fixture controls apply to tests, not a production path sandbox.

| Boundary / priority | Actor, abuse path and exposure | Accepted control and defended property |
| --- | --- | --- |
| F1 Configuration/reset; P1 C1 | A configurator supplies an unintended path or omits reset intent; setup can delete data or expose an ordinary local error. | All three constructors default `resetFile` to false. Validate required path, severity and declared encoding before filesystem effects, without competing-error precedence. Select the full path once using FileInfo/construction-time CWD semantics; later CWD changes do not redirect it. Prepare missing directories; only explicit true deletes an existing selected file during construction. Missing log files are created by permitted output, not construction. Prevents implicit deletion and invalid-option effects, not misuse of host OS rights. |
| F2 Selection/rendering; P1 C3 | A producer uses direct calls or rejected labels/severity to expose raw content, invoke hooks or write a denied record. | Preserve inherited registration/intrinsic and supplied-direct gates before massage, hooks, record encoding and entry I/O. Denial invokes no print and enables no rescue output. Separately requested constructor/reset effects are setup, not accepted entries. Defends against denied-payload disclosure without sandboxing custom code. |
| F3 Physical append; P1 C2, P2 C5 | Forged delimiters or changed CWD redirect/misframe content; another process removes/replaces the file. | Append exactly selected `Encoding.GetBytes(message + Environment.NewLine)`: one suffix, no leading newline, extra blank line or preamble; no reformatting, re-escaping or recaptured context/time. Append existing bytes or create a missing file at the same selected pathname. Preserve any unterminated/differently encoded prefix without corrective separator, inspection or transcoding. Defends the approved new-record boundary and fixed selection, not persistent file identity or existing-content suitability. |
| F4 Failure reporting; P1 C4 | Encoding/open/write/flush/close errors carry path/payload-bearing causes, or a successful recipient/reporter failure hides output failure. | Supplied guarded entrypoints classify these as `Output`: direct calls report once and throw `LogDispatchException`; Logger completes independent eligible attempts, safely reports and propagates even if another succeeds. No sink-local second report, raw cause/path echo, ordinary fanout or rescue route. Original failure facts survive reporter/stderr failures. Defends the inherited diagnostic privacy and propagation boundary. |
| F5 Isolated fixture; P1 C6 | Stale names, traversal, links or unfinished workers redirect reset/cleanup toward unrelated data. | D021/proposal06 permit only fresh collision-rejecting, recorded-owned synthetic children beneath `C:/temp/logger tests/`. Check component containment and reparse ancestry before effects/cleanup; reject ambiguous or pre-existing ownership. Stop/join workers and release handles before reset/recreation/cleanup. Separate physical test hosts restore CWD/console/subscriptions/registrations in finally; use explicit recipients and no fallback. Defends unrelated files and global state; no parent creation/deletion, alternate root, ACL/elevation, network or disk-filling authority follows. |

The five existing BCL encodings remain available; optional UTF8 differs from enum-default
ASCII. Undefined values fail before effects, but selected BCL replacement behavior is
not arbitrary Unicode round-trip fidelity. Physical writes are synchronous, serialized
per instance through inherited `LoggerLock`, with handles released before completion;
rendering/hooks keep their inherited concurrency. No cross-instance/process ordering
is promised. Repeated calls append repeated records. Previously compiled callers may
retain embedded `resetFile=true` until recompiled; approval alone changes no caller.

**Local construction is not F4.** Accepted 04/E11 removes incidental console output and
`DispatchFailed` side output, while preserving original ordinary framework preparation/
access exceptions outside the documented argument mappings. No normalized IOException,
fixed/path-free message, null cause or empty Data promise is selected. The unsupported
constructor-sanitization promise was withdrawn in 04 and that repair accepted in 05;
it is not pending policy. Inherited bounded safe `Output` reporting remains mandatory:
at most eight descriptors plus overflow and existing capture count, stderr at most 512
UTF-16 units including terminator, with no raw diagnostic/payload/path backlinks.

### STRIDE By Crossing

| Crossing | Spoofing | Tampering | Repudiation | Information disclosure | Denial of service | Elevation of privilege |
| --- | --- | --- | --- | --- | --- | --- |
| F1 | Path is not ownership proof | Explicit reset; validation before effects | No audit/erasure receipt | Local errors may carry paths; no incidental echo | Setup can fail/block | Host OS rights, no sandbox |
| F2 | Labels/prefixes are not identity | Inherited gates and final token escaping | Time is not authenticity | Withhold before hooks/print | Payload/hook cost remains unbounded | No reader/tenant rights from eligibility |
| F3 | Pathname is not physical identity | Append/create, no implicit truncation | Return/flush is not durable audit | File readers remain consumer-controlled | Locked/full storage or blocking I/O | No cross-user/process isolation |
| F4 | Generated IDs are not principals | Preserve original bounded facts | Notification may be unavailable | No raw cause/path in safe output | Per-call bounds, not rate limits | No raw object/control backlinks |
| F5 | Unique name alone is insufficient | Ownership, containment and rejection before effects | Preserve operation/ownership evidence | Synthetic inputs only | Quiesce handles/workers and isolate global state | No elevation or escape authority |

Highest-consequence modeled exposure is denied unbounded content reaching the file;
require **C3, eligibility before payload work**, first. C4 separately prevents diagnostic
disclosure. This is a design risk statement, not a discovered exploit or source verdict.
D020 deliberately leaves external interference and existing-content suitability with the
developer: fixed path is not physical identity/content policing. D012 retains explicit
destructive reset; failure need not undo prior effects. No retry, replay, copying,
relocation, prefix repair, crash-safe/exactly-once durability or secure erasure is added.
Framing is not redaction. No new privacy, retention, compliance or identity guarantee is
selected; M4-B2 automatic-session/fallback behavior is not implemented or promoted here.

### Status And Dependent Gate

**Status refresh: 2026-09-27, after parent Final.** The absent-parent, unapproved-creation
and unimplemented state recorded in [promotion 22](../../../.agent-runs/20260927-1431-logger-m4b-continuation/22-m4b1-security-documentation.md)
is historical. [Direct approval 23](../../../.agent-runs/20260927-1431-logger-m4b-continuation/23-temp-directory-authorization.md)
and [provisioning 24](../../../.agent-runs/20260927-1431-logger-m4b-continuation/24-m4b1-parent-provision.md)
record the same `C:/temp/logger tests/` parent created at 18:09:47 EDT with ordinary
non-reparse ancestry. [SafeExecution closure 25](../../../.agent-runs/20260927-1431-logger-m4b-continuation/25-m4b1-safeexecution-closure.md)
closes SE-M4B1-01, not the implementation or security gates. Separate parent-creation
authority changes none of F5's owned-child-only cleanup limits.

[Candidate audit 26](../../../.agent-runs/20260927-1431-logger-m4b-continuation/26-m4b1-specification-audit.md)
is followed by the [frozen specification record](../../../.agent-runs/20260927-1431-logger-m4b-continuation/evidence/m4b1-frozen-specifications-r1.json)
and [implementation 27](../../../.agent-runs/20260927-1431-logger-m4b-continuation/27-m4b1-file-implementation.md).
The current FileDestination source was reopened: B1 is implemented, without blanket
sanitization of ordinary constructor errors. [Code review 28](../../../.agent-runs/20260927-1431-logger-m4b-continuation/28-m4b1-code-review.md)
accepts its production code-review gate only and records all **62 frozen specification/helper
inputs unchanged**. Reviewed red history remains separate from subsequent green results.

The separately opened [parent Final](../../../.agent-runs/20260927-1431-logger-m4b-continuation/evidence/m4b1-final-20260927T223829-94a133d82dff4ba38dec2279dc382559-final.json)
records **native 520/520 and physical 55/55 passed on each of net48 and net10.0,
with 0 failed and 0 skipped**. These measured results are neither static counts nor the
historical baseline relabeled as fresh; they define no new acceptance count. No product
blocker is reported in the supplied status packet. Independent Security Reviewer v2 and
post-document parent final acceptance remain pending: neither a preparation label nor
green execution certifies the modeled source/fixture controls or supplies security clearance.
No new Open Question, HTTP API Designer handoff or requirements review is introduced.
No checks were executed here beyond document validation. Later B2 status is recorded in
the [B2 addendum](#m4-b2-automatic-session-addendum); this B1 history remains unchanged.

## M4-B2 Automatic-Session Addendum

**Promotion checkpoint: 2026-09-28. SourceNoCodeVerdict.** Promote accepted
[threat input 08](../../../.agent-runs/20260927-1431-logger-m4b-continuation/08-m4b2-session-threat-input.md),
[design 07](../../../.agent-runs/20260927-1431-logger-m4b-continuation/07-m4b2-session-contract.md)
composed with [repair 10](../../../.agent-runs/20260927-1431-logger-m4b-continuation/10-m4b2-session-contract-repair.md)
under [acceptance 11](../../../.agent-runs/20260927-1431-logger-m4b-continuation/11-m4b2-session-rereview.md),
and current [owner B01-B12](../../../.agent-runs/20260927-2317-logger-m4b2-session/02-session-internal-contract.md).
[Assignment r1](../../../.agent-runs/20260927-2317-logger-m4b2-session/assignment-r1.md)
and [M4B2-integration-r1](../../../.agent-runs/20260927-2317-logger-m4b2-session/slice-02-r1.md)
authorize this scoped promotion. Earlier M4-A/B1 requirements/history remain; this is not
a new whole-repository audit, security verdict or release certification.

The actual [ordinary dispatcher/bridge](../../ProphetsWay.Logger/Logger.cs),
[exact-T dispatcher](../../ProphetsWay.Logger/Generics/Logger.cs),
[safe exception](../../ProphetsWay.Logger/LogDispatchException.cs),
[session owner](../../ProphetsWay.Logger/AutomaticFileSession.cs) and
[path resolver](../../ProphetsWay.Logger/AutomaticFilePaths.cs) were opened. They locate
the implemented boundary to which the accepted requirements below apply; source presence
does not establish independent security clearance. The
[B2 field inventory](data-classification.md#m4-b2-automatic-session-addendum) covers the
new carriers and retains AS-A1's unbounded, provisional Confidential treatment.

### Interview, Flows And Exposure

Actors remain the consumer configurator, host/operator, producer, selected executable
recipient/formatter, diagnostic observer and filesystem reader/writer. Topology is
synchronous local capture, route selection, native rendering, shared-owner OS file I/O
and separate bounded failure reporting. Executable consumer code has host-process rights.
D013 leaves deployment readers, tenancy, retention/erasure and OS/storage protection to
consumers; none is silently supplied by a shared file or deterministic directory name.
No unanswered scoped actor/tenancy/topology question blocks this utility design.

AS1-AS6 retain 08's identifiers. There is no HTTP response, business-row retrieval,
password store or soft-delete entity. For file readback the ownership rule is explicit:
only consumer-authorized OS readers should read it, enforced by the consumer's filesystem
and any export layer, not Logger. All such readers see all records in that file. Route,
severity and label selection are delivery gates, not tenant or per-record file ACLs.

| Boundary | Flow / permitted exposure | Required withholding and enforcement |
| --- | --- | --- |
| AS1 Host to selection | Public primary configuration; captured host base, lexical key/digest/component and conditional local-data root to private selection/OS | Configurator supplies primary only; resolver uses no payload/tenant identity. No public path/key/digest getter or dynamic safe guidance. Ordinary configuration errors retain local contracts. |
| AS2 Capture to route | One captured ordinary or exact declared-T plan/context/time to native text, retaining typed metadata including null/default(T) | Logger/native guards withhold the entire denied entry before payload work. One route's enabled explicit registration suppresses only its own implicit recipient even when it rejects/fails; never ordinary forwarding/resnapshot/rescue. |
| AS3 Owner to filesystem | Exclusive first reservation, then complete UTF-8 records at one selected path | Owner preserves occupied candidates, appends/creates without reset, and never replays/relocates uncertain writes. Consumer controls readers and external interference; selected path is not physical identity. |
| AS4 Route/lifetime to state | Per-call adapters share one owner in loaded static Logger state; private path/failure marker survives route changes | Owner coordinates state/file effects and per-attempt owned handles. No public reset, route-policy sharing, last-record/raw-cause archive or borrowed disposal. |
| AS5 Failures to observer/caller | Existing bounded generated facts and fixed guidance through DispatchFailed, stderr and LogDispatchException | Bridge/completion exclude payload, resolved paths, host/key/digest, foreign Message/StackTrace/Data/causes and control backlinks. Reports are not log-payload sinks. |
| AS6 Fixture to environment | Fresh isolated host copies, synthetic data and controlled roots/faults | Separate fixture/setup authority proves child ownership/containment before effects; no actual profile/install writes, unrelated cleanup, link escape, ACL/elevation or network operations follow. |

### Required Controls And Abuse Cases

These are retained design obligations, not findings against the implemented source.
Priorities express modeled consequence; all seven AS-C controls remain P1.

| Control / crossing | Actor, goal and path | Required property / accepted trace |
| --- | --- | --- |
| AS-C1 / AS2, AS4 | Producer or integration routes typed confidential data to an ordinary-only recipient, or uses rejection to reach fallback | Independent captured ordinary/exact-T selection; Trace/NoFilter automatic coverage of all six bits; native original context/time/metadata and whole-entry guards before hooks/output. S01-S08; inherited M4-1/M4-4/M4-7. |
| AS-C2 / AS1, AS3, AS4 | Concurrent caller or competing process collides with an occupied name or redirects initial output | One coordinated exclusive UTC/token allocation; no overwrite/precheck-based reservation. Freeze primary configuration at initialization; capture host once; secondary uses accepted host-key derivation, not override/CWD/payload. S09-S13/S26-S28; B01-B05. |
| AS-C3 / AS3 | Failing storage induces partial acceptance then duplicate replay to a secondary/new file | Secondary only for initial location/naming/preparation/reservation failure. Publish selected path before record work; encoding/seek/write/flush/close or later-open failures preserve it and propagate, never replay/copy/relocate. Later calls append/create at that same path. S14/S17-S19; B06-B09. |
| AS-C4 / AS4, AS5 | Repeated calls exhaust probing after double failure, or cached failure blocks unrelated explicit routes | Remember only bounded initial-failure state, with no new lookup/key derivation/probe. Enabled compatible explicit routes bypass it only for themselves, including rejection; reactivation does not reset state. Fresh report/correlation for each failed call. S15-S16; B06/B11. |
| AS-C5 / AS5 | Filesystem/formatter injects path/payload diagnostics, or a throwing/reentrant observer masks original failure | Per-call false means initial failure; thrown output means non-initial failure. One implicit Output descriptor after all captured explicit slots, never per root or core capture. Mandatory safe propagation, fixed initial guidance, original report bounds and contained reporters outside gates. S21-S25; B11. |
| AS-C6 / AS2-AS4 | Reentrant callbacks or concurrent route mutation deadlock output, interleave records or close another call's stream | One session gate for state and each complete physical attempt; no registry lock nesting. Native rendering, consumer callbacks and reporting stay outside. Trusted platform dependencies run inside, cannot reenter/log/notify/acquire registry locks, and are not consumer extension points. Owned returned streams are disposed per attempt; no persistent-stream retirement service. S20; B09-B10/B12. |
| AS-C7 / AS6 | Stale names, process-global changes or redirected paths make tests damage unrelated storage | Fresh owned children under the settled test parent, component-aware containment/reparse rejection, isolated loaded state, bounded authorized effects and quiesced owned cleanup. Fault wrappers preserve real owner/stream behavior; synthetic fixture output is not real-profile proof. D021/AC-22.4; B12; current assignment. |

**Selection and recovery detail:** normal primary is captured `AppContext.BaseDirectory`;
the public override uses configuration-time `DirectoryInfo.FullName` and freezes before
establishment. Configuration alone performs no file work. At first eligible completed
record, resolve full host availability once even with an override; an unavailable host
does not defeat a usable override. Normal host/local roots must be fully qualified before
BCL normalization. Missing/unavailable inputs do not authorize another root or CWD.

Only qualifying initial primary failure resolves the secondary: root-preserving trailing
separator removal, Windows slash conversion, otherwise ordinal case/Unicode preservation;
strict UTF-8 key; SHA-256, all 64 lowercase hex digits prefixed `app-`, directly beneath
the conditional LocalApplicationData root. Unavailable key means no local-root lookup;
unavailable naming/root is failed establishment, not a fabricated file-open attempt.
Successful primary never resolves unused secondary inputs. Shared keys share a directory;
aliases/casing/Unicode variants need not. This is neither authentication, tenant separation,
logical-application uniqueness, anonymization nor a hash-collision/physical-identity guarantee.

Initial reservation uses exclusive creation; only a positive already-exists result changes
the token, retaining allocation UTC. No overwrite, generic I/O-as-collision retry or cleanup
of incidental abandoned artifacts. Secondary success is normal success without an
intermediate failure report. Both locations unusable yields remembered failure, not a
cached exception. Established output appends UTF-8 plus exactly one suffix newline, without
preamble, leading separator, content/binary inspection or prefix repair. Missing file means
same-path creation; missing parent/later open failure is output failure, not recovery.

**Safe failure detail:** one positive implicit position follows preceding disabled explicit
slots. Preserve at most eight descriptors during accumulation plus overflow, existing
core-capture semantics, fresh correlation and stderr's 512 UTF-16-unit inclusive-terminator
limit. Only initial/remembered failure adds fixed location-kind/configure-destination
guidance to Message/ToString and bounded stderr; no new public failure shape or raw cause.
Rendering failures cannot masquerade as location failures. Subscriber/stderr exceptions
cannot replace the original result; managed-thread recursive-notification suppression
does not suppress nested logging or guarantee protection from cross-thread cycles.
Ordinary explicit construction/configuration errors remain outside this safe channel.

### B2 STRIDE By Crossing

| Crossing | Spoofing | Tampering | Repudiation | Information disclosure | Denial of service | Elevation of privilege |
| --- | --- | --- | --- | --- | --- | --- |
| AS1 | Host/digest is not authenticated identity | Frozen lexical selection, no payload redirection | Configuration is not audit evidence | Path/key/digest excluded from safe output | Unavailable roots fail; no remembered-state reprobes | Host OS rights, no path sandbox |
| AS2 | T/labels/time are not principals | Original route/context and native framing | No receipt/idempotence | Whole-entry guard; no cross-route fallback | Hooks/record size remain unbounded | Consumer code retains process rights |
| AS3 | Filename does not prove ownership | Exclusive initial allocation; fixed append/create, no replay | Flush/return is not durable audit | Shared readers see every accepted record | Collision pressure, blocking/full storage; no quota/pruning | No external-writer or physical-identity isolation |
| AS4 | New T/clear is not a new session | Coherent state; independent suppression | Failure marker is not history | No retained record/raw failure graph | Owned handles; no callback lock inversion | One route cannot redefine another's policy |
| AS5 | Generated IDs are not credentials | Immutable original facts; reporter failure contained | Notification may be absent | Bounded safe interface only, not CLR serialization | Per-call bounds, not rate limits | No cause/control backlink through reports |
| AS6 | A fresh-looking name is not ownership proof | Containment and real production-boundary exercise | Preserve evidence, not fabricated success | Synthetic data/controlled roots only | Quiesce hosts/handles before cleanup | No elevation or unrelated-root authority |

### Status, Limits And Handoff

[Implementation report 28](../../../.agent-runs/20260927-2317-logger-m4b2-session/28-integration-implementation.md)
records its bound Final run: **672 passed per net48/net10.0, zero failed/skipped**, including
14 integration cases per framework, with both library assets/example built. This is dated
reported execution, not a new acceptance count, rerun or independently reproduced result
by this author. Actual implementation is present; it is not described as untested merely
because external deployment conditions remain unverified. Bounded fresh-copy tests use
controlled owned roots; they do not prove unmodified real host/profile behavior, deployed
readership/permissions, cross-platform fidelity, crash durability or security clearance.

Highest-consequence modeled exposure remains unbounded typed payload crossing into the
wrong recipient/file readership. Require **AS-C1 first**; AS-C5 separately protects the
diagnostic channel. These are requirements, not a new vulnerability verdict. No new
accepted risk is invented: D012/D020 deliberately provide quick-start fixed-path output,
leaving existing-content suitability and external interference to developers; D013 leaves
readers, storage, costs and retention with consumers. No authentication, ACL, privacy,
redaction, quota, retention, replay, global ordering, exactly-once or durability service
is added. Attempted disposal cannot prove a defective trusted dependency/OS released its
resource; failure still propagates, without retries or retained causes (B09).

**Open Questions proposed:** none for this promotion. AS-A1 is the only retained
classification assumption, replaced by inspected consumer data/configuration. No new
legal obligation or consent rule follows; actual personal data plus jurisdiction/role
facts would trigger the consumer compliance questions in the classification document.
Independent **Security Reviewer v2, report 33**, must assess current source/evidence
against AS1-AS6/AS-C1-AS-C7 and inherited controls. Its verdict and parent acceptance
remain separate. No HTTP API Designer workstream, release approval or implementation
security clearance is supplied here.
