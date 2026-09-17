# Scoped Label Policy Threat Model

This is a design-time review standard for the in-memory label value and pure destination
label policy. It translates accepted obligations into threats and review expectations;
it creates no new product requirements and passes no implementation security verdict.
It is not a whole-Logger assessment, dependency audit, deployment assessment or release clearance.

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
