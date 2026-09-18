# Scoped Label Policy Data Classification

This inventory covers the fields and method-boundary carriers of `SensitivityLabel`,
`LabelFilterMode` and the reviewed `DestinationLabelPolicy` target, not arbitrary Logger
payloads or business entities. It is a design-time handling model, not an implementation
security verdict, built-in taxonomy, confidentiality promise or new product requirement.

The separate [ON-04 M2-A extension](#on-04-m2-a-field-extension) inventories native
severity and explicit synthetic-recipient carriers. Earlier tables retain their
ON-03 historical scope and statuses; they are not reclassified by this extension.

## Evidence And Status

Read completely: [SensitivityLabel.cs](../../ProphetsWay.Logger/SensitivityLabel.cs),
[LabelFilterMode.cs](../../ProphetsWay.Logger/LabelFilterMode.cs), and
[DestinationLabelPolicyTests.cs](../../ProphetsWay.Logger.Test/DestinationLabelPolicyTests.cs).
The fixed *Immutable Destination Label-Filter Contract* and its independent review define
the policy surface; its B01-B24 are used below. Exact operational provenance is retained
in the external ON-03 author report, not required to interpret these tables.

At the ON-03 document-authoring checkpoint, the label value and mode enum are source-read
existing types; the policy is a reviewed target awaiting implementation. Specifications
were read, not executed or audited here. Nothing in this document asserts completed
policy implementation, routing, payload withholding or F1-F3 behavior.

Authoritative intent is [decision-log.md](../decision-log.md), especially D003-D005,
D008-D013 and D018-D021, with criteria in [requirements.md](../requirements.md).
[threat-model.md](threat-model.md) defines the scope, trust crossings and review priorities.
No actual production identifiers, business data, credentials or deployment were inspected.

## Classification Meaning

These are analysis classes, not values recognized or enforced by `SensitivityLabel`.

| Class used here | Baseline meaning and its scope |
| --- | --- |
| Public | Fixed API names/constants convey no consumer data; no confidentiality control is needed for those literals. |
| Internal | Consumer-specific configuration or derived decisions need consumer-controlled access if exposed outside the intended callers. This defends against unintended configuration/decision readership; it does not add authentication to the library. |
| Confidential, provisional | Unbounded consumer content is not presumed public. The class's baseline is access control, protected transport and encryption at rest if a consumer persists/transmits that content, to address unintended readership, interception and stolen storage. D013 leaves actual classification and storage protections with that consumer. No storage, encryption default, key management, authentication system or deployment control is introduced or certified here. |

**Assumption A1:** Identifier meaning, membership associations, associated derived results
and foreign diagnostic contents are **unbounded**. Confidential is a precautionary handling assumption, not a
claim that the unseen values are known PII, Sensitive PII or Secret. The consumer's actual
inventory may establish a different class and any additional legal/handling obligations;
an innocuous-looking synthetic example does not settle it. No field is silently treated
as Public because its runtime type is `string`, `bool` or a collection.

All scoped operations are in-process. "At rest" and "in transit" below describe the
absence of a policy-owned storage/network boundary, not protection against process-memory
inspection, crash dumps, malicious consumer code or an eventual consumer export.

## Field Inventory

Each row identifies a public data field or separately exposed input/result component.
Aliases of the same value are explicit, not additional unseen entity fields. The predicate
has no message, exception payload, metadata, scope-origin, path, credential or tenant field.

| Entity and field/carrier | Class and rationale | At rest | In transit | In logs | API/local response treatment |
| --- | --- | --- | --- | --- | --- |
| `SensitivityLabel.identifier` input and retained `Identifier` | Confidential, provisional; **unbounded meaning**. Opaque application identity can reveal an association despite its syntax limit. | Retained in the object; no persistence/erasure facility. Consumer owns any storage/dumps. | Argument/reference and exact string return only; no network. | No logging introduced. Its argument errors must not copy/encode supplied content. Consumer logging of the getter is not redacted. | Exact unchanged identifier is deliberately readable. No HTTP/serialization contract or secret-hiding getter. |
| `SensitivityLabel.Equals` inputs `other`/`obj` | Confidential, provisional, for any label identity inspected; unrelated object content is not a label field. Null is an allowed comparison input, not a valid label. | No new persistence; comparison does not mutate identities. | In-process references only. | No comparison logging specified. | Input-only references; object overload recognizes labels, not arbitrary property inspection. |
| `SensitivityLabel.Equals` Boolean result | Confidential, provisional; reveals a relationship between supplied identities in an **unbounded consumer context**, not user authenticity. | No store introduced. | In-process result. | No emitted record; any consumer export retains context sensitivity. | Returned equality only; no authentication/authorization meaning. |
| `SensitivityLabel.GetHashCode` integer result | Confidential, provisional; identity-derived collection aid, linkable to its **unbounded consumer context**. It is neither confidential storage nor anonymization. | No persistent-hash promise; consumer must not treat it as durable identity or protected storage. | In-process result. | No logging introduced; publishing a hash is not redaction. | Equal labels have equal hashes; unequal labels may collide. No cryptographic or cross-process stability claim. |
| `LabelFilterMode` declared constants `NoFilter=0`, `Exclude=1`, `AllowOnly=2` | Public; fixed behavior vocabulary, not a sensitivity classification. | No store. | May be used as local API literals. | Literal names reveal no consumer membership by themselves. | Public non-flags enum; declaration alone validates/evaluates nothing. |
| Policy constructor `mode` and getter-only `Mode` | Internal; the selected consumer configuration, distinct from public enum literals. | Reviewed immutable value; no persistence. | Argument/getter within process only. | No policy diagnostics or record emission selected. | Local readback intentional; invalid values are rejected, not inferred from expression history. |
| Policy constructor `labels` membership and getter-only `Labels` | Confidential, provisional; **unbounded identities/associations** may describe sensitive categories or recipients. | Reviewed copied unique membership retained for the policy; no persistent store or retention duration. | Synchronous capture and local read-only view only. | No log/serialization behavior selected; NoFilter still retains configured membership. | One representative per configured identity, non-null read-only collection. No writable backing escape; order and collection/representative reference identity unspecified. |
| `Allows.effectiveLabels` membership | Confidential, provisional; **unbounded meaning** may associate an entry with sensitive categories. Already includes entry and enclosing-scope membership. | No persistence selected; evaluation is not a capture/archive API. | Supplied sequence within process only. | No label/payload emission selected. | Input-only; null is invalid, empty means unlabeled. No origin/entry transport or effective-set getter is granted. |
| `Allows` Boolean result | Confidential, provisional; discloses a relationship between effective and configured membership in an **unbounded consumer context**, not the payload's safety. | No audit or delivery store. | In-process return only. | No output record implied by true or false. | Membership permission only. False is mismatch; failed evaluation returns no Boolean. External callers/recipients are not authorized by this result. |
| Argument-error type and `ParamName` | Public for the fixed contract facts; `identifier`, `mode`, `labels`, `effectiveLabels` name parameters, not supplied values. | No exception archive selected. | Exceptional completion to caller. | Label-specific no-input-echo rule applies; policy specifies exact argument types/names, not a new diagnostic formatter. | Ordinary local argument contract, not a public HTTP error response. |
| Consumer sequence exception `Message` | Confidential, provisional; **unbounded** caller-produced text can contain unrelated sensitive data. No actual exception value was inspected. | No policy-owned archive specified. | Exceptional completion may carry foreign text; exact details unspecified. | No sanitization guarantee here. Future integration reporting excludes raw exception text under R-16. | Not a promised safe external response. |
| Consumer sequence exception `StackTrace` | Confidential, provisional; **unbounded** diagnostic text may disclose application/location details. | No archive specified. | Same exceptional method boundary. | Future R-16 excludes it; the predicate supplies no reporting channel. | No public diagnostic-exposure promise. |
| Consumer sequence exception `Data` | Confidential, provisional; **unbounded** values/object graph, not an inspected business-data schema. | No retention/deep-clone guarantee. | Foreign exception may carry references; no network introduced. | Future R-16 excludes it; no automatic redaction here. | Not policy readback and not approved serialization. |
| Consumer sequence exception `InnerException`/cause reference | Confidential, provisional; **unbounded** nested diagnostics and object references, if present. | No raw-cause store selected. | Wrapping/identity is unspecified by B18; only exceptional completion is required. | Future R-16 forbids raw causes/inner exceptions in its safe summaries. | No specific cause/wrapping shape or safe response is promised by this pure contract. |

The last four rows classify the possible diagnostic carriers named by the reviewed
sequence-failure boundary and D009/R-16, not unseen exception values as particular PII.
Other arbitrary consumer fields are not secretly included in the inventory. The fixed
policy contract adds no exact foreign message, wrapping, validation precedence or sequence
access-count requirement.

## Executable Inputs And Ownership

| Carrier | Treatment and accepted limit |
| --- | --- |
| Constructor `IEnumerable<SensitivityLabel>` | Consumer-owned executable access, not just passive data. On successful construction, membership must have been copied before return. Later source additions/removals/replacements cannot change the policy. |
| Evaluation `IEnumerable<SensitivityLabel>` | Consumer supplies stable already accumulated membership. Sequence access, including enumerator acquisition, iteration, element access and disposal, can execute code or fail. An encountered access failure cannot yield a prefix Boolean. |
| Returned `ReadOnlyCollection<SensitivityLabel>` | Read-only wrapping and backing ownership are separate obligations. Collection-interface mutation throws `NotSupportedException`; mode/membership remain unchanged. Immutable label references need no arbitrary-object deep clone. |

Rollback of caller-code effects is not promised. Producer mutation during capture, nonterminating
inputs and arbitrary resource consumption are outside B20/B21's guarantees. Do not convert
these accepted limits into a new input cap, execution deadline, cancellation API, sandbox
or high-severity baseline finding. This does not promise an attacker-controlled enumerable
is harmless; it states the same-process consumer-code boundary.

## Exposure And Minimization

There is no client/API/server split, row ownership field, password, token or key in the
scoped contract. Code holding a reference can read intentional getters; D013 assigns any
further recipients, authentication, storage and audit policy to the consuming application.
Request-only versus response data is therefore a local method distinction, not a fabricated
HTTP DTO. `Identifier`, configured membership and mode are readable; effective membership
is input-only; writable backing storage must never be exposed.

Data not supplied cannot leak through these objects. D003's opaque-identity intent lets
consumers use non-secret category identifiers instead of putting personal details or
credential material into labels. Choose the identity before construction: this library
must not truncate, tokenize, hash, normalize or silently rewrite it, since doing so would
change R-02 matching. Whether an external non-sensitive mapping suffices is the consumer's
classification decision, not a new mapping service or requirement in Logger.

Synthetic labels in the specifications demonstrate equality/membership, not a real taxonomy.
NoFilter's retained membership is still data; permitting every valid E does not erase C.
Deduplication removes repeated identity representatives, not sensitive meaning, and policy
membership does not replace preserved origin transport. Collection hashes are not encryption,
anonymization, password hashing or a substitute for equality. No cryptographic operation or
credential-storage feature is needed or selected for this surface.

Storage-layer encryption, if selected by a consumer for Confidential data, addresses stolen
disks/backups, not an over-permissive query, a compromised application or readable in-process
getters. No field-level encryption or searchable-encryption design is recommended here;
the consumer's actual data/storage assessment is absent and D013 owns that choice.

## Retention, Compliance And Later Data

No retention duration, consent basis, audit store or erasure mechanism is selected by this
predicate. Collection immutability and releasing a reference do not establish secure erasure
of strings or other copies. No soft-delete field exists in this inventory; no deletion or
erasure compliance follows from the model.

If actual identifiers or later payloads identify natural persons and the consumer falls
within a relevant privacy regime's territorial/organizational scope, that consumer must
settle classification, necessity, retention and erasure duties. For example, GDPR/UK GDPR
applicability depends on those facts, not the presence of a type named `SensitivityLabel`.
No such factual trigger was observed here, and no compliance obligation is asserted.

Future raw messages, exceptions/data, typed metadata, scope/origin records, paths, failure
descriptors and bridge records require their actual contracts/data inventory and separate
review. D004/D008-D013 and D018-D020 already constrain whole-entry withholding, safe
diagnostics, framing, fixed paths and consumer responsibilities; the predicate implements
none of those external crossings merely by returning a Boolean. R-16's safe-reporting
exclusions do not silently become a new pure-method exception-redaction contract.

**Open Questions proposed for ON-03:** None. A1 is explicitly provisional and replaced by
the consumer's actual inventory; consumer-specific processing and deployment questions
block only that consumer's data-handling assessment. Parent scope/trace verification and
Security Reviewer v2's later actual-code/dependency review remain separate gates. No
implementation, whole-system security, source-exposure or publication clearance is claimed.

## ON-04 M2-A Field Extension

**Design checkpoint: 2026-09-17.** This is the classification input for owner-approved
ON-04 revision 2, native M2-A only, before the C# contract gate and source implementation.
It extends rather than replaces the label/policy inventory. Exact operational target
provenance is in the run report; the approved obligations are stated in the
[threat-model extension](threat-model.md#on-04-m2-a-severity-boundaries), S1-S7.
R-07/R-08 and D002/D007/D010 were reopened in
[requirements.md](../requirements.md) and [decision-log.md](../decision-log.md).
The [product brief](../product-brief.md) supplies the consumer/developer/host roles,
not deployed users or a selected retention/compliance policy.

### Source Inventory And Assumption

Read the complete pre-M2-A files for this boundary:
[LogLevels.cs](../../ProphetsWay.Logger/LogLevels.cs),
[LoggingDestinationCore.cs](../../ProphetsWay.Logger/LoggingDestinationCore.cs),
[Logger.cs](../../ProphetsWay.Logger/Logger.cs),
[Generics/Logger.cs](../../ProphetsWay.Logger/Generics/Logger.cs),
[MetadataExtensions.cs](../../ProphetsWay.Logger/Generics/MetadataExtensions.cs),
[ILoggerMetadata.cs](../../ProphetsWay.Logger/Generics/ILoggerMetadata.cs),
[EventDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/EventDestination.cs),
[GenericEventDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/GenericEventDestination.cs)
and [TextBasedDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/TextBasedDestination.cs).
The declared types and carriers below are source-backed; target validity/guard rules are
requirements, not evidence that those sources implement them.

`ILoggerMetadata` declares no fields or members. Existing general typed Logger methods
do not constrain `T`; convenience extensions alone use that marker. This does not
authorize a new non-null metadata guard or mandatory interface on typed calls. The
contract author settles helper signatures/defaults and ordinary argument exceptions;
current optional defaults are not independent authority to permit invalid helper calls.

**Assumption S-A1:** Actual message, rendered content, exception graphs, arbitrary typed
metadata and diagnostic contents are **unbounded**. They receive provisional
Confidential handling, not an assertion that unseen values are PII, Sensitive PII or
Secret. Only declarations were inspected, not production values or business schemas.
A consumer's real inventory and classification replace this assumption. Synthetic
examples cannot establish a harmless class for future real payloads.

The Public/Internal/Confidential baselines above remain analysis guidance under D013:
fixed public vocabulary needs no confidentiality protection; consumer-specific routing
information needs controlled readership; unbounded payloads additionally need protected
transport/storage if a consumer exports or persists them. M2-A supplies no encryption,
authentication, storage, retention, secure-erasure or log-redaction facility. Local
object references are not a protected network transport or a memory-isolation boundary.

### Fields And Boundary Carriers

Aliases are grouped only where they carry the same value. `LoggerEventArgs` below
means both the ordinary and generic nested classes unless a row says generic-only.
Every declared event-argument property is included; the internal registry/lock/lifetime
state is not a new data-model workstream. An arbitrary `T` or `Exception` graph is
classified as an opaque whole, not as an invented list of business fields. Named
exception subcarriers distinguish rendered detail from other reachable raw data.

| Entity and field/carrier | Class and rationale | At rest | In transit | In logs | API/local response treatment |
| --- | --- | --- | --- | --- | --- |
| `LogLevels` fixed names and numbers | Public; approved native behavior vocabulary, not user data or clearance. | Constants only; no store. | Local enum/integer/string literals. | Fixed severity names may be rendered. | S1 defines six exact bits and inclusive masks; Security/SecurityOnly disappear, with no replacement alias. |
| Core `reportingLevel`, `intReportingLevel`, `strReportingLevel`; retained `_reportingLevel` | Internal; consumer-selected recipient configuration, distinct from public constants. | Retained on the destination; no persistence selected here. | Constructor arguments within process. | No configuration logging is added; malformed text is not assumed a safe diagnostic. | Input-only configuration, no new public getter. Equivalent valid representations, including zero, must agree; S2 rejects malformed/unsupported input. |
| Helper/direct `level`, validation `messageLevel`, event `LogLevel` | Internal; entry-specific routing/importance information. The combined payload retains its higher class. | Event argument may retain the value; no archive selected. | Local call and accepted callback. | Existing text record includes severity. | Exact helper bit or valid nonzero raw composite; S3/S5 withhold a rejected entry before recipient use. |
| `ValidateMessageLevel` Boolean result | Internal; reveals a configuration/entry relationship, not an access right. | No new store. | Local return. | No audit/report record implied. | True is severity eligibility only; false is normal mismatch. Invalid message input is an argument error, not permission. |
| Ordinary/typed registration `newDest` | Internal; consumer-selected executable recipient reference. | Existing host/route-held reference; no new ownership or lifetime policy. | Registration input only. | Not an authorized serialization/logging record. | Null rejected before registration effects; no duplicate/replacement/disposal rule is added by M2-A. |
| Typed route's declared `T` / `typeof(T)` | Internal; consumer implementation/routing identity, not a tenant identity. | Existing route infrastructure only; not redesigned. | Implicit generic type context in local calls. | No type/route diagnostic API added. | No new reference/type exposure, route-isolation proof or mandatory interface constraint. |
| `LoggingEvent` delegate and callback `sender` | Internal; subscriber/control references, not passive business data. | Consumer may retain references; no new lifetime promise. | In-process callback handoff only. | Neither is an approved serialized record. | Eligible callbacks intentionally execute consumer code; rejection invokes no callback. No subscriber authentication or sandbox is supplied. |
| Helper/direct `message`; event `RawMessage` | Confidential, provisional; **unbounded** producer text, including empty/whitespace or permitted absence. | Retained in accepted event arguments; no library archive/retention selected by this slice. | Local input and accepted ordinary/generic callback. | May contribute to accepted rendered text; no content sanitization is promised. | Raw text is intentional accepted exposure. S4 helper guards do not impose non-null on permitted raw/direct calls; denied recipients receive none. |
| Event `Message`; shared massage result | Confidential, provisional; **unbounded** text derived from message and exception detail, not a safe summary. | Accepted event arguments may retain it; no new archive. | Internal render result and accepted callback. | Intended accepted rendered content only; no new framing/redaction promise. | S6 preserves supplied context/detail for valid severities/composites without absent-exception failure. No new exact message syntax is specified. |
| Helper/direct `ex`; event `Exception` | Confidential, provisional; **unbounded** original diagnostic graph. | Reference retained in accepted event arguments; no deep clone or erasure promise. | Raw reference to intended accepted callback; not a sanitized reporting descriptor. | Text massage accesses message/stack details; an event consumer can inspect more of the raw graph. | Permitted absence remains absent. Denial precedes rendering/callback; acceptance does not prove the graph suitable for onward export. |
| Supplied exception `Message` | Confidential, provisional; **unbounded** diagnostic text. | Part of referenced graph or derived text. | Current massage reads it; accepted raw callback can read it. | Included as permitted detail, not redacted. | No HTTP-safe error or exact formatting contract follows. |
| Supplied exception `StackTrace` | Confidential, provisional; **unbounded** application/location diagnostics. | Part of referenced graph or derived text. | Current massage reads it; accepted raw callback can read it. | May be included with exception detail. | Not a public diagnostic response or proof of safe source/path exposure. |
| Supplied exception `Data` | Confidential, provisional; **unbounded** arbitrary keys/values and nested objects. | Reachable through the retained exception reference; not copied/archived by this slice. | Accessible through accepted raw `Exception`; no schema inspection was performed. | No requirement to render this dictionary is added. A callback may itself inspect/log it. | S5 gates the raw reference as well as text. Existing raw exposure is not an R-16 sanitized failure API. |
| Supplied exception `InnerException` / nested causes | Confidential, provisional; **unbounded** recursive diagnostics. | Reachable graph and potentially derived text. | Current massage traverses nested message/stack; callback may inspect the graph. | Permitted nested detail remains content, not a safe cause summary. | No new cause schema, cloning, recursion/resource guarantee or external-response approval. |
| Typed helper/extension/direct `metadata`; generic-only event `Metadata` | Confidential, provisional; **unbounded** arbitrary `T`, with no declared business schema in the marker interface. | Generic event arguments may retain the supplied value/reference; nested mutability is not frozen. | Local typed call and accepted generic callback. | No automatic metadata renderer or privacy filter is added. | Null/reference/value forms retain existing permitted metadata behavior. No new non-null or mandatory-interface constraint; rejection withholds metadata before callback. |
| Event `Timestamp`; time component of current text record | Internal; entry timing can reveal activity when associated with payload. | Existing event argument/text may retain it. | Accepted callback/text only. | Current source uses local `DateTime.Now`; no UTC/capture redesign in M2-A. | Existing local time exposure, not a new timestamp format, shared-event-time guarantee or audit proof. |
| `PrintLogEntry.message` combined text | Confidential, provisional; **unbounded** composition of timestamp, severity and massaged content. | Target uses an in-memory synthetic recorder only; actual recipient retention is consumer-owned. | Rendered text to the selected recipient after eligibility. | It is intended log content; this run authorizes no file/console recipient execution. | No print call on denial. The accepted string is not guaranteed single-line, redacted or suitable for a public response. |
| Future native argument-error type and `ParamName` | Public for fixed C# contract identifiers, not supplied values; exact selections remain pending. | No error archive selected. | Ordinary exceptional completion to caller. | No new reporting mechanism. | Contract author/reviewer chooses exact types/names. This table chooses no precedence, wrapping or message syntax. |
| Possible native argument-error diagnostic content | Confidential, provisional; **unbounded** if it contains consumer-supplied values. No concrete new exception contents were inspected. | No archive selected. | Caller receives whatever the reviewed ordinary argument contract defines. | No safe-reporting or input-echo guarantee is newly imposed here. | Do not serialize errors as known-safe responses. Existing label-error non-echo rules stay scoped to labels; R-16 is separate later work. |

### Exposure, Ownership And Minimization

SV1 is configuration/message-mask validation; SV2 is helper/registration input before
dispatch; SV3 is supplied direct/dispatched recipient rendering and callback/print.
All three are covered above and in the threat model. No network, database, API DTO,
tenant row, password store or soft-delete field is added. There is consequently no
server-only column or authenticated row-retrieval rule to invent. D013 assigns any
recipient access and onward export to the consuming host.

Configuration representation and destination registration are input-only; accepted event
properties are deliberate local readback/callback data. For a rejected entry, the whole
raw/rendered payload is withheld from recipient rendering, callback and print, including
the raw exception graph and typed metadata. Producer/custom code already holding those
references is not isolated. Blanket serialization of event arguments would include
unbounded raw data and is not authorized merely by a valid severity or a label.

Data not supplied cannot leak through this path. Consumers should first decide whether
diagnostic details and each metadata field are needed, and whether a non-identifying
category/token or externally sanitized value suffices. Synthetic canaries are sufficient
for this slice's guard/content checks. This is D013 consumer-side minimization, not
permission for Logger to truncate, hash, redact or silently discard supplied content
contrary to S6. No new classifier, credential-handling or privacy API is required.

If a consumer later persists Confidential content, encryption at rest addresses stolen
disks/backups, not a compromised application, an over-permissive read or an accepted raw
callback. Protected network transport would address interception only at a real export
boundary. Neither control substitutes for S5's pre-handoff guard. Field-level encryption
and its search/sort trade-offs are not selected for this synthetic in-process slice.

### Coverage And Remaining Decisions

Coverage is complete for the declared native configuration/argument carriers, all five
ordinary event-argument properties, the same five plus generic `Metadata`, text-record
handoff, marker-interface absence of fields, and SV1-SV3. Arbitrary business members
inside `T`/exception data were not inspected or falsely assigned individual classes;
their whole carrier remains unbounded under S-A1. Earlier label/policy history stays
unchanged, not freshly verified by this extension.

No exact retention duration, consent basis, erasure mechanism, real-data inventory or
deployment was supplied or selected. Those consumer choices block only a real consumer
data assessment. If actual logged values identify natural persons and territorial or
organizational facts bring processing within GDPR/UK GDPR, those facts would trigger
that consumer's legal assessment; the settling question is which persons' data, which
jurisdictions and which processing role. No such factual trigger was inspected, and no
compliance obligation is asserted. A record's absence from one denied recipient is not
erasure from the producer or other copies.

**Open Questions proposed for ON-04 M2-A:** None. Exact signatures/defaults and ordinary
argument exception details remain delegated C# contract work, not new product questions.
No additional metadata restriction, validation-priority rule or whole-v4 security
control is introduced. Label integration, registry/snapshots/lifetime, file/fallback/
recovery/framing, failure reporting, bridges and public-product qualification remain
outside this input. Independent implementation review and fresh scoped evidence are
still required later; this author ran no build/test and issues no security verdict.
