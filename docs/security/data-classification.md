# Scoped Label Policy Data Classification

This inventory covers the fields and method-boundary carriers of `SensitivityLabel`,
`LabelFilterMode` and the reviewed `DestinationLabelPolicy` target, not arbitrary Logger
payloads or business entities. It is a design-time handling model, not an implementation
security verdict, built-in taxonomy, confidentiality promise or new product requirement.

The separate [ON-04 M2-A extension](#on-04-m2-a-field-extension) inventories native
severity and explicit synthetic-recipient carriers. Earlier tables retain their
ON-03 historical scope and statuses; they are not reclassified by this extension.

The [M4-A current carrier addendum](#m4-a-current-carrier-addendum) covers the current
M3 structures and approved M4-A rendering boundary without re-auditing those histories.

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

## M2-B Membership Field Extension

**Design checkpoint: 2026-09-18. Stage: SHAPE.** This additive inventory supports
M2-B revision 2, inheriting revision 1, before independent C# contract review.
It covers explicit registration membership only; older ON-03/ON-04 inventories retain
their dates and status. The [membership model](threat-model.md#m2-b-explicit-registration-membership)
defines MB1-MB3 and MB-R1-7. Reopened authority is D005/D006/D010/D013 in
[decision-log.md](../decision-log.md) and AC-09.1, AC-10.1-3 and the borrowed part of
AC-11.1 in [requirements.md](../requirements.md).

Read completely: [Logger.cs](../../ProphetsWay.Logger/Logger.cs),
[Generics/Logger.cs](../../ProphetsWay.Logger/Generics/Logger.cs),
[LoggingDestinationCore.cs](../../ProphetsWay.Logger/LoggingDestinationCore.cs),
[IDestination.cs](../../ProphetsWay.Logger/IDestination.cs),
[ILoggingDestination.cs](../../ProphetsWay.Logger/ILoggingDestination.cs) and
[Generics/ILoggingDestination.cs](../../ProphetsWay.Logger/Generics/ILoggingDestination.cs).
These are the actual control and payload contracts, not evidence of M2-B completion.
The two destination interfaces add methods, not a business-field schema; `T` remains
unconstrained. Snapshot/route rows below describe required private logical state,
not new public types, fields or settings APIs.

**Assumption M-B1:** message, exception and arbitrary metadata contents are unbounded,
provisionally Confidential, not inspected PII, Sensitive PII or Secret. A consumer's
real inventory/classification replaces this assumption. Arbitrary recipient internals
are not inspected or assigned invented field classes. An Internal control reference
does not classify its reachable object graph as harmless.

### Membership And Handoff Inventory

Internal means consumer-controlled access to configuration/decision data. Provisional
Confidential additionally means protected transport and encrypted storage if the
consumer exports/persists it, defending against interception and stolen media. No
library encryption, access-control system or persistence is selected by this slice.
All crossings here are in-process; references are not memory-isolation controls.

| Field/carrier | Class and rationale | At rest | In transit | In logs | Local response/exposure |
| --- | --- | --- | --- | --- | --- |
| Ordinary route identity | Internal; consumer routing selection, not a principal. | Private process state only. | Ordinary registration/call selection. | No route diagnostic record added. | Must not alias any typed route; no new identity getter. |
| Typed declared `T` / `typeof(T)` | Internal; exact consumer routing/type information. | Private route state only. | Generic call/registration context; never runtime-subtype routing. | No type-name export added. | Control input, not tenant authorization; includes `ILoggingDestination` as legal metadata type. |
| Add `newDest` | Internal as a control reference; executable recipient choice. | Route/capture may retain the borrowed reference. | Registration input. | No object serialization or implicit identity formatting required. | Reject null and same-reference same-route duplicate before mutation; no ownership transfer. |
| Remove `destToRemove` | Internal as a control reference; exact instance to remove. | No removal archive added; older captures may retain it. | Removal input. | No equality/hash/diagnostic code is needed for identity. | Reference identity only; absent removal no-op, existing null-removal behavior preserved. |
| Route membership and insertion order | Internal; recipient topology and attempt ordering. | Private atomically published membership; no persistent store. | Complete old/new capture before eligibility/callback. | No membership dump or audit record added. | Internal-only snapshot; writable registry backing must not escape. Clear affects only the selected route. |
| Captured recipient references | Internal as control handles; payload sensitivity is separate. | Held for the captured call even after removal; no secure-erasure or exact GC deadline. | Synchronous iteration of fixed membership. | No retained-recipient report added. | Borrowed, not disposed by removal/clear; same instance may occur on distinct compatible routes. |
| Core `_reportingLevel` | Internal; selected supplied-destination severity configuration. | Existing immutable destination field. | Used in eligibility, not replaced by a new settings API. | No mask report introduced. | Membership capture does not freeze arbitrary custom recipient state. |
| `ValidateMessageLevel.messageLevel` and callback `Log.level` | Internal; entry-specific severity and eligibility input. | No new archive. | Severity to eligibility, then permitted callback. | Existing accepted severity behavior preserved. | Guard semantics unchanged; eligibility does not receive a new raw-payload parameter. |
| `ValidateMessageLevel` result | Internal; reveals a configuration/entry relationship. | No decision store added. | Local Boolean. | No receipt or audit implied. | Eligibility only, not authentication, label permission or data suitability. |
| `Log.message` | Confidential, provisional; unbounded producer text. | Recipient may retain it under consumer policy. | Original permitted string to the selected eligible recipient. | Existing accepted output is intentional; no redaction added. | No new external response contract or content restriction. |
| `Log.ex` | Confidential, provisional; unbounded raw exception graph. | Recipient can retain original reference; no deep clone/erasure. | Original permitted reference, including permitted absence. | Not a sanitized reporting descriptor. | Reachable Message/StackTrace/Data/InnerException carriers retain the earlier unbounded classification; no unseen values were inspected. |
| Typed `Log.metadata` | Confidential, provisional; unbounded arbitrary `T`. | Recipient can retain the original value/reference. | Original permitted typed payload; no new non-null/interface constraint. | No metadata renderer or automatic classification added. | Nested values are not frozen by membership capture. Public serialization is not authorized. |

### Exposure, Lifetime And Minimization

MB1 registration/removal references and route selection are input-only control data;
MB2's ordered membership stays private. MB3 exposes severity to eligibility and the
original permitted payload to the selected eligible callback. There is no server,
HTTP response, authenticated row retrieval or new server-only business field here.
Do not blanket-serialize destinations or payload graphs as a consequence of registering
them. D013 assigns readership and onward export to the consuming host.

Immutable membership is neither immutable payload nor immutable recipient state.
Removal/clear publishes future membership without draining or disposing borrowed
recipients; it neither revokes an earlier capture's handoff nor erases copies held
by producers/recipients. D006 requires hosts to stop producers and await synchronous
calls before disposal. No new lifetime lease, retirement mechanism or public drain
is selected. Recursive user logging is a new capture, not proof of deduplication.

Data not supplied cannot leak through this handoff. Consumers should decide whether
each message detail/metadata field is needed, or a non-identifying token would suffice,
before calling Logger; synthetic canaries suffice for this slice. Logger must not
silently truncate, hash or clone payloads, nor use user equality to identify recipients.
Encryption at rest addresses stolen storage/backups, not an unintended raw callback
or compromised host. Transport encryption addresses a later real network export,
not local route aliasing. No field-level encryption/search/sort redesign is selected.

All declared membership/control carriers and raw callback parameters in the six read
files are covered. No custom business fields, actual exception values, production
data, retention duration, consent basis, deployment or compliance regime were inspected
or invented. Actual personal data and applicable territorial/organizational facts
would require a consumer privacy assessment; which data, jurisdictions and processing
role are involved would settle applicability. These consumer decisions do not block
the explicit in-process membership slice.

**Open Questions proposed for M2-B:** None. Label integration, settings replacement,
file/fallback behavior, owned resources, bridges and reporting remain deferred.
Unconfigured typed-to-ordinary fallback is unqualified and excluded from tests here;
the inventory establishes no whole-route independence or tenant/authentication
guarantee. Contract review and later independent implementation security review remain
separate. No real-data, implementation or release verdict is issued.

## M4-A Current Carrier Addendum

**Promotion checkpoint: 2026-09-27. SourceNoCodeVerdict.** This is the authorized canonical
promotion of [report 02's complete data/threat addendum](../../../.agent-runs/20260926-2305-logger-m4a-preparation/02-m4a-framing-threat-input.md),
not a new whole-Logger inventory or a restatement of its preparation-era implementation
status. The owner's "Approve the m4-a envelope" in
[activation 05](../../../.agent-runs/20260926-2305-logger-m4a-preparation/05-owner-activation.md)
activates the [revision-1 promotion grant](../../../.agent-runs/20260926-2305-logger-m4a-preparation/m4a-implementation-r1.md).
[M4A-C1 F01-F30](../../../.agent-runs/20260926-2305-logger-m4a-preparation/01-m4a-contract.md)
and [accepted contract review 03](../../../.agent-runs/20260926-2305-logger-m4a-preparation/03-m4a-contract-review.md)
remain authoritative. The [current threat section](threat-model.md#m4-a-native-rendering-and-framing)
defines B1-B6 and M4-1 through M4-8; earlier sections retain their dates/scope. Report 02
and its old EOF warning were not edited. Implementation status/green results are not a
security verdict or I/O certification.

### Opened Definitions And Treatment

Current definitions read for this addendum:

- Structure: [LogContext.cs](../../ProphetsWay.Logger/LogContext.cs), [LogScopeFrame.cs](../../ProphetsWay.Logger/LogScopeFrame.cs), [LogAnnotations.cs](../../ProphetsWay.Logger/LogAnnotations.cs), [LogLabelContext.cs](../../ProphetsWay.Logger/LogLabelContext.cs), [LogLabelOrigin.cs](../../ProphetsWay.Logger/LogLabelOrigin.cs).
- Selection: [SensitivityLabel.cs](../../ProphetsWay.Logger/SensitivityLabel.cs), [DestinationLabelPolicy.cs](../../ProphetsWay.Logger/DestinationLabelPolicy.cs), [DestinationRegistrationSettings.cs](../../ProphetsWay.Logger/DestinationRegistrationSettings.cs), [LoggingDestinationCore.cs](../../ProphetsWay.Logger/LoggingDestinationCore.cs).
- Rendering/events: [LogTextRenderer.cs](../../ProphetsWay.Logger/LogTextRenderer.cs), [TextBasedDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/TextBasedDestination.cs), [GenericTextBasedDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/GenericTextBasedDestination.cs), [EventDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/EventDestination.cs), [GenericEventDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/GenericEventDestination.cs).
- Diagnostics: [LogFailureReport.cs](../../ProphetsWay.Logger/LogFailureReport.cs), [LogFailureDescriptor.cs](../../ProphetsWay.Logger/LogFailureDescriptor.cs), [LogFailureStage.cs](../../ProphetsWay.Logger/LogFailureStage.cs), [LogDispatchException.cs](../../ProphetsWay.Logger/LogDispatchException.cs).

**M4-A1 (report 02 A1):** All unseen payload meanings, nested graphs, keys, label
associations, explicit-hook text and dynamic type names are **unbounded**, provisionally
Confidential (C). No real values/business schemas were inspected or guessed to be PII,
Sensitive PII or Secret. An actual consumer inventory/classification replaces M4-A1.
Internal (I) covers operational/control facts; Public (P) covers fixed vocabulary only.
Reachable payload graphs do not become Internal merely because a control reference is I.

The existing class baselines apply under D013: I needs consumer-controlled readership;
C additionally needs protected transport/encrypted storage if the consumer exports or
persists it, against interception and stolen storage. These are handling responsibilities,
not new library guarantees. Every crossing here is local; references do not isolate memory.
In the tables, **retained** means objects/strings or recipient copies, not an archive,
retention duration or secure-erasure service. Every payload row is withheld before any
rejected recipient's hooks/callbacks/print. Intended log content is never safe-report data.

### Native Context Fields

| Field | Class / rationale | At rest | In transit | In logs | Local response/exposure |
| --- | --- | --- | --- | --- | --- |
| `LogContext.EventTimestampUtc` | I; call activity time | Immutable retained fact | Selected context; trusted same-call reuse | Invariant offset-zero O prefix | Getter; fresh direct selected view does not mutate retained context; not identity/permission |
| `LogContext.Labels` | C; associations | Same completed capture | Accepted contextual handoff | Entry attachment source | Non-null label-facts getter; not general property/handle storage |
| `LogContext.Scopes` | C; property-bearing boundaries | Copied ordered membership | Full capture, outermost first | Every frame, including empty/unlabeled | Read-only membership; original nested objects remain accessible |
| `LogScopeFrame.Annotations` | C; attachment association | Fixed at opening | Immutable attachment facts | Null versus present-empty preserved | Optional annotation getter; no property-derived labels |
| `LogScopeFrame.Properties` | C; ordered associations | Copied pairs, not deep-cloned values | Captured membership, not source re-enumeration | Pair order and duplicate keys retained | Read-only list; no writable library backing |
| Property `Key` | C; unbounded string meaning | Captured string | Direct token input, not value hook | Quote/escape null/empty/whitespace distinctly | No normalization, merge or implicit labels |
| Property `Value` | C; unbounded graph/capabilities | Original value/reference | Accepted raw context or explicit hook | Supported scalar, marker or explicit result | Handles/delegates/enumerables within it are not stripped or frozen |
| `LogAnnotations.LabelOccurrences` | C; ordered label associations | Copied occurrence membership | Attachment facts | Every occurrence, including repeats | Read-only list; reuse does not merge attachments |
| `LogLabelContext.EntryAnnotations` | C; entry association | Retained immutable attachment | Context readback | Separate entryLabels field | Null differs from empty; neither subtracts inherited labels |
| `LogLabelContext.ScopeAnnotations` | C; label-only projection | Ordered immutable membership | Context readback | Full frames govern rendering, not this projection alone | Non-null attachments only, including empty attachments |
| `LogLabelContext.Origins` | C; occurrence relationships | Immutable ordered origins | Context readback | Preserved through attachment/frame structure | Scopes then entry; duplicates survive |
| `LogLabelContext.EffectiveLabels` | C; derived membership | Distinct ordinal union | Eligibility input/readback | Never substitutes for origin/occurrence text | Reading is not a permission decision |
| `LogLabelOrigin.Label` | C; one occurrence identity | Immutable label | Context readback | Original attachment token | Existing ordinal identity, no normalization |
| `LogLabelOrigin.ScopeIndex` | C; attachment relationship | Immutable nullable index | Context readback | Structural association | Null means entry; otherwise ScopeAnnotations index, never Scopes index |
| `LogLabelOrigin.OccurrenceIndex` | C; occurrence relationship | Immutable index | Context readback | Original order retained | Local index, not a durable ID/principal/handle |
| `SensitivityLabel.Identifier` | C; unbounded opaque meaning | Immutable exact string | Intentional getter/token input | Quoted/escaped; validity is not encoding | Existing 1-256-unit validity/ordinal identity; no diagnostic echo |

M3's structural exclusions prohibit library-added live-stack/handle/source-enumerator/
formatter/recipient/history backlinks, not capabilities already reachable from supplied
property values. Membership immutability is not sanitization. Arbitrary metadata, even
annotation-shaped T, dictionaries or enumerables, remains an opaque value, not a new
entry-property carrier, native structure or implicit label source.

### Raw And Text Carriers

The earlier native inventory's individual `Message`, `StackTrace`, `Data` and
`InnerException` rows remain provisionally C. Their current treatment is reaffirmed below
from the opened core/event declarations, not by re-dating the old assessment.

| Field/component | Class / rationale | At rest | In transit | In logs | Local response/exposure |
| --- | --- | --- | --- | --- | --- |
| `message` / event `RawMessage` | C; unbounded original text | Raw/recipient copies | Original optional string | Massage then text-token encoding | Original null/empty/whitespace/raw escapes unchanged |
| `ex` / event `Exception` | C; original diagnostic graph | Original reference | Intended accepted raw handoff | Existing chain messages/available stacks only by default | Data/nested causes remain reachable raw; no deep clone/redaction |
| Massage result / event `Message` | C; derived unbounded text | Derived/event string | Text renderer or event callback | Escape only in text composition | Event Message remains unescaped, possibly multiline; no scopes added by massage |
| `metadata` / generic event `Metadata` / metadata hook input | C; arbitrary unconstrained T | Original value/reference | Exact declared-T handoff/hook | Typed-only field even for null/default(T) | Original raw value; not a label source |
| Ordinary/generic event `Context` | C; property/label graph | Selected retained capture | Accepted callback | Text uses native structure, not event serialization | Completed context for library delivery; public manual constructor leaves it null |
| Ordinary/generic event `Timestamp` | I; event activity time | Retained event value | Accepted event callback | Distinct from text UTC prefix | Existing local-time DateTime; not redefined as EventTimestampUtc |
| `level` / event `LogLevel` | I; importance/routing | Event/record value | Eligibility, then accepted handoff | Full validated mask's general spelling | No classification/identity authority |
| `FormatValue` result | C; arbitrary extension text | Recipient-local string | Final token encoder | Always encoded as data; null result is null token | No raw-value replacement or cross-recipient cache |
| Unsupported runtime `Type.Name` | C; dynamic name meaning | Temporary marker text | Type-name metadata only | Encoded within marker | Name intentionally visible; not FullName/member discovery or object persistence |
| Fixed marker shell/grammar literals | P; authored syntax | Constants | Composition only | Fixed ASCII vocabulary | Dynamic content is separately C |
| `PrintLogEntry.message` / console-wrapper input | C; complete intended record | Consumer may retain/store | One complete non-null string | No renderer terminator; sink supplies it | Not redacted, diagnostic-safe, public-response-safe or a durability receipt |

Default scalar recognition and invariant formats are fixed by M4-5/F06-F11, not an
invitation to classify numbers, Guid or times as harmless. Default formatting performs no
arbitrary object inspection; explicit hooks may inspect, retain, mutate, throw, block or
reenter. Hooks can overlap and own their thread safety. Existing exception-message/stack
access is explicit executable behavior, not forbidden arbitrary discovery. No added
Exception.Data/aggregate walk or nontext value formatter is required.

### Control And Diagnostic Fields

| Field/component | Class / rationale | At rest | In transit | In logs | Local response/exposure |
| --- | --- | --- | --- | --- | --- |
| Settings `Enabled` | I; participation choice | Immutable setting | Selection | No new export | False retains disabled registration; enabled reject-all stays active |
| Settings `ReportingLevel` / intrinsic mask | I; selected restriction | Captured setting/fixed core mask | All-bits checks | Not new configuration output | Registration and intrinsic restrictions both apply |
| Settings/core `LabelPolicy` / policy `Labels` | C; configured associations | Immutable policy membership; atomic core reference | Selection before payload work | Excluded from safe output | Existing local readback; no writable backing |
| Policy `Mode` | I; filter choice | Immutable value | Selection | No new export | Existing local getter |
| `Allows` result | C; membership relationship | No decision archive | Local gate | Not an audit receipt | Permission facet only, not payload suitability |
| Severity result | I; control outcome | No archive | Local gate | False is not failure | No recipient authentication; all required bits, not overlap |
| Route/exact-T identity, recipient/settings references, captured positions | I; control facts | Private snapshot/borrowed handles | Selected route only | No registry dump | Control use, not a new public registry or ownership API |
| Sequence/scope/formatter/output/subscriber handles | I; executable/control references | Consumer/control lifetime | Local code/reference use | Not default object serialization | No sandbox or harmless classification of reachable contents |
| Foreign failure `Message` | C; unbounded text | No library raw-failure archive | Caught boundary, not safe report | Excluded | No safe diagnostic exposure |
| Foreign failure `StackTrace` | C; diagnostic/location text | No raw-failure archive | Same boundary | Excluded | No foreign stack handoff |
| Foreign failure `Data` | C; arbitrary values/backlinks | No raw-failure archive | Same boundary | Excluded | No raw-data handoff |
| Foreign failure `InnerException` / cause | C; nested graph | No raw-cause retention | Same boundary | Excluded | No inner-cause/backlink in safe report |
| `LogFailureReport.CorrelationId` | I; generated call correlation | Immutable safe report | Observers/caller | Permitted generated ID | Non-payload-derived; not authentication/durable identity |
| `LogFailureReport.Failures` | I; bounded ordered facts | At most eight while accumulating | Safe report | Permitted facts only | Read-only; no raw payload/control backlinks |
| `LogFailureReport.OverflowCount` | I; excess recipient count | Immutable nonnegative count | Safe report | Permitted count | Nine failures gives eight descriptors plus one overflow |
| `LogFailureReport.CoreCaptureFailureCount` | I; shared capture fact | Immutable zero or one | Safe report | Permitted count | Count once, not a recipient descriptor |
| `LogFailureDescriptor.RegistrationId` | I; captured position | Immutable descriptor | Safe report | Permitted one-based position | Includes preceding disabled/rejected/successful slots; direct position 1; not lookup key |
| `LogFailureDescriptor.Stage` | I; observed boundary | Immutable descriptor | Safe report | Eligibility/Output/LabelCheck | Not inferred from foreign cause; fixed enum vocabulary is P |
| `LogDispatchException.Report` | I; bounded original facts | Retained safe report | Mandatory propagated error | Safe facts only | Reporter failure cannot replace original facts |
| Authored exception `Message` / `ToString` / stderr summary | I; fixed prose plus permitted operational facts | No raw archive | Caller/report channel | Exclude payload/time/context/labels/paths/names/formatter causes; stderr <=512 UTF-16 units including terminator | Safe authored interface, not whole CLR serialization |
| Initial exception `InnerException`, `HelpLink`, `Data`, `Source`; `StackTrace` getter | P; fixed/null/empty facts | Null/null/empty/fixed library source | Authored exception surface | No foreign diagnostic content | StackTrace returns null; not erasure of CLR state |
| Caller-added exception contents | C; unbounded post-catch data | Consumer retention | Consumer export | Outside safe authored guarantee | Reflection/TargetSite/debugger/serialization/inherited mutation not sanitized |

No event UTC, context, rendered record, dynamic marker name or formatter cause becomes a
new safe-report field. Ordinary argument and scope/annotation construction failures retain
their local contracts; foreign construction exceptions are not promised sanitized. Report
bounds limit retained facts, not recipient attempts, call rate, record size or execution time.

### Exposure And Consumer Duties

Coverage includes every M4-A component and B1-B6: context/frame/annotation/origin fields,
ordinary/exact-T raw/event fields, scalar/marker/hook results, completed text, selection
controls and safe/foreign diagnostics. Opaque nested business fields remain unbounded,
not falsely enumerated. There is no HTTP response, authenticated row retrieval, tenant
column, password store or soft-delete record. Getters authorize local readback only;
D013 makes consumer readership and onward export decisions necessary. Never blanket-
serialize context/events/exceptions on the strength of a label match or escaped text.

Data not submitted cannot leak through this path. Before supplying details, consumers
should ask whether each message/stack/property/metadata field or label meaning is needed,
or whether a non-identifying category, token or upstream-sanitized value suffices. This
reduces unnecessary disclosure/retention; it is not authority for Logger to truncate,
hash, normalize, redact or silently rewrite supplied content. Framing preserves content
boundaries, not confidentiality. Unsupported markers intentionally limit representation,
not raw access or lossless arbitrary-object persistence.

Credential material should not be logged: recoverable log copies would become a credential
store. A separate consumer password system, if present, needs salted memory-hard password
hashing, never reversible encryption or fast general-purpose hashes. ProphetsWay.Hasher
must never be used for credentials. No such field or hashing feature is introduced here.

Consumer storage encryption protects stolen disks/backups, not a compromised application,
over-permissive read, raw callback or explicit hook already holding data. Protected network
transport addresses interception at a real export, not local object access. No field-level
encryption is selected; it would require actual field requirements and changes searching/
sorting. Actual PII would require the consumer's minimization, retention limit, erasure
and log-redaction decisions, not an assumption that Logger supplies them.

No compliance obligation is asserted. Conditional question for a future consumer data/
deployment stream: "Which natural persons' data will be logged, in which jurisdictions
and organizational role, and what retention, erasure and lawful-processing obligations
apply?" Actual personal data plus applicable territorial/organizational facts could trigger
GDPR/UK GDPR duties; no such facts, duration, legal basis or consent requirement were supplied.
Scope disposal/removal/releasing references is not erasure of retained copies.

**Open Questions proposed for M4-A:** None. Only M4-A1 is assumed; the consumer's inspected
inventory replaces it. No new tenancy, privacy, retention, encryption, durability or
redaction guarantee is selected. M4-B file lifecycle/recovery/termination/encoding, typed
fallback qualification, bridges and full product/release assessment remain outside scope.
Independent Security Reviewer v2 assesses implementation after promotion; no HTTP API
Designer work is introduced. This inventory passes no implementation-security verdict.

## M4-B1 Explicit-File Design Addendum

**Promotion checkpoint: 2026-09-27. Approved design only; SourceNoCodeVerdict.** This
promotes the scoped [threat/classification input 02](../../../.agent-runs/20260927-1431-logger-m4b-continuation/02-m4b1-file-threat-input.md)
with the complete [repaired contract 04](../../../.agent-runs/20260927-1431-logger-m4b-continuation/04-m4b1-contract-repair.md)
and [acceptance 05](../../../.agent-runs/20260927-1431-logger-m4b-continuation/05-m4b1-contract-rereview.md),
under [proposal 06](../../../.agent-runs/20260927-1431-logger-m4b-continuation/06-m4b1-execution-proposal-r1.md)
as approved by [activation 14](../../../.agent-runs/20260927-1431-logger-m4b-continuation/14-m4b1-activation-and-extension.md).
All preceding sections retain their original text and historical scope. The companion
[explicit-file model](threat-model.md#m4-b1-explicit-file-design-addendum) states the
accepted behavior and fixture limits; none is certified implemented by this inventory.

At the 2026-09-27 promotion checkpoint, opened the complete then-current
[FileDestination.cs](../../ProphetsWay.Logger/LoggerDestinations/FileDestination.cs)
and 04's complete declaration/XML snapshot: three constructor parameter sets, `_fi`,
`_encoder`, `PrintLogEntry`, its bytes and the five encoding choices. The snapshot is
the target, not a description of the then-unchanged production implementation.

**F-A1, retained from 02:** Unseen path meanings, existing bytes, payloads and foreign
diagnostics are **unbounded**, provisionally Confidential (C), not identified PII,
Sensitive PII or Secret. An inspected consumer inventory replaces this assumption.
Internal (I), Public (P) and C retain the existing D013 handling baselines; no writable
path, benign contents, stable physical-file identity or execution permission is assumed.

### File Carriers And Exposure

Treatment below is the accepted design. These are local configuration, OS file I/O and
diagnostic crossings, not an HTTP request/response or authenticated retrieval service.
Existing M4-A context, payload and safe-report field inventories remain unchanged.

| Field/carrier | Class / rationale | At rest | In transit | In logs | Local response/exposure |
| --- | --- | --- | --- | --- | --- |
| `fileName`; `_fi` name/directory/full path | C; unbounded location meaning | Private state and filesystem namespace | Constructor to OS; ordinary errors may carry it | Excluded from safe dispatch reports | Configuration input; no new path getter or public export |
| `resetFile` | I; destructive selection | Constructor option, not retention policy | Explicit configuration | No side-output record | Input-only; default false, true requests eager deletion of the selected existing file |
| Valid `reportingLevel`, `strReportingLevel`, `intReportingLevel`; inherited mask | I; recipient configuration | Inherited destination state | Constructor and eligibility | No new configuration dump | Existing mask rules; not reader authorization |
| Malformed supplied severity text | C; unbounded rejected input | No new archive | Ordinary argument error boundary | Not a safe diagnostic by assumption | Local/framework error contract, not blanket sanitization |
| `EncodingOptions.ASCII`, `BigEndianUnicode`, `Unicode`, `UTF8`, `UTF32` | P; fixed values 0-4 and BCL mappings | Constants | Local API vocabulary | No consumer data in the literals | Public choice enum, not flags or a sensitivity class |
| `encoder`; `_encoder` selection | I; representation choice | Private destination state | Selected BCL encoding operation | Does not sanitize contents | Input-only; optional default UTF8, enum default ASCII; undefined values rejected before effects |
| Existing target bytes | C; opaque unbounded prior data | Consumer-controlled file | Append positioning; no inspection/conversion required | Preserved unless reset expressly requested | No library read/export API; no prefix repair or suitability guarantee |
| Target existence/length | C; activity associated with unbounded contents | Filesystem facts | Preparation/open/append | No safe-report export | Internal I/O use, not authenticated ownership evidence |
| `PrintLogEntry.message` | C; complete intended record | Transient string and eventual file content | Permitted native renderer to sink | Intended accepted content, not redacted | Denied entries withheld; protected-hook bypasses are outside supplied-entrypoint guarantees |
| `lineBytes` | C; encoded record content | Transient bytes and selected file | Selected encoder to append/create | Exact selected BCL bytes, not a second formatter | No public byte getter; encoding fallback is not arbitrary Unicode round-trip assurance |
| `Environment.NewLine` and fixed grammar | P; record structure | Encoded suffix | Sink adds one trailing terminator | No leading newline, extra blank line or preamble | Framing only, not confidentiality or a delivery receipt |
| Local/guarded failure `Message` | C; unbounded diagnostic text | No approved raw-failure archive | Ordinary constructor caller, or caught Output boundary | No constructor console echo; excluded from safe dispatch output | Constructor text may contain paths; no forced safe wrapper or message stripping |
| Local/guarded failure `StackTrace` | C; location/implementation diagnostics | No raw-failure archive | Same distinct error boundaries | Excluded from safe dispatch output | Ordinary local diagnostics retained; no constructor stack-erasure promise |
| Local/guarded failure `Data` | C; arbitrary reachable values | No raw-failure archive | Same distinct error boundaries | Excluded from safe dispatch output | No constructor empty-Data promise; no raw-data handoff through safe reports |
| Local/guarded failure `InnerException` / raw cause | C; nested unbounded diagnostics | No approved raw-cause retention | Same distinct error boundaries | Excluded from safe dispatch output | Original ordinary construction failure propagates; no normalization or cause stripping; guarded reporting retains no raw cause |
| Safe correlation/registration identifiers, stage, counts and authored text | I; bounded generated facts, fixed vocabulary P | Existing immutable safe report | Subscribers, stderr and guarded caller | Existing R-16 facts only | No actual path, payload, label, user name, raw cause or backlink |

Construction may prepare directories or explicitly reset before any entry exists; entry
eligibility does not promise side-effect-free setup. Construction emits neither console
output nor `DispatchFailed` in the approved design, but its original ordinary framework
exceptions are not safe reports. The withdrawn constructor-sanitization promise is not
promoted. Guarded `Output` privacy and mandatory propagation remain separate obligations.

File readers and onward export remain consumer-controlled under D013; labels and encoding
do not authorize readership. Omit unnecessary details before logging; Logger gains no
silent truncation/redaction. Physical framing does not conceal content. No new privacy,
retention, compliance, erasure or identity guarantee, and no B2 design, is introduced.

### Status And Evidence Limit

**Status refresh: 2026-09-27, after parent Final.** The pre-provisioning and
pre-implementation status in [promotion 22](../../../.agent-runs/20260927-1431-logger-m4b-continuation/22-m4b1-security-documentation.md)
is historical. [Approval 23](../../../.agent-runs/20260927-1431-logger-m4b-continuation/23-temp-directory-authorization.md)
authorized parent creation; [provisioning 24](../../../.agent-runs/20260927-1431-logger-m4b-continuation/24-m4b1-parent-provision.md)
records `C:/temp/logger tests/` created at 18:09:47 EDT with ordinary non-reparse
ancestry. [Closure 25](../../../.agent-runs/20260927-1431-logger-m4b-continuation/25-m4b1-safeexecution-closure.md)
closes SE-M4B1-01 only; fixture cleanup still owns only fresh children, never the parent.

[Audit 26](../../../.agent-runs/20260927-1431-logger-m4b-continuation/26-m4b1-specification-audit.md)
and the [freeze record](../../../.agent-runs/20260927-1431-logger-m4b-continuation/evidence/m4b1-frozen-specifications-r1.json)
retain the reviewed Candidate/red history. [Implementation 27](../../../.agent-runs/20260927-1431-logger-m4b-continuation/27-m4b1-file-implementation.md)
records B1 implemented; the current FileDestination source was reopened for this refresh.
[Code review 28](../../../.agent-runs/20260927-1431-logger-m4b-continuation/28-m4b1-code-review.md)
accepts only its code-review gate and records all **62 frozen specification/helper
inputs unchanged**. Ordinary local constructor errors remain distinct from safe Output
reports; no blanket sanitization is claimed.

The separately opened [parent Final](../../../.agent-runs/20260927-1431-logger-m4b-continuation/evidence/m4b1-final-20260927T223829-94a133d82dff4ba38dec2279dc382559-final.json)
records **native 520/520 and physical 55/55 passed on each of net48 and net10.0,
with 0 failed and 0 skipped**. These are dated execution results, not static case counts,
a rebranding of the historical baseline or new fixed acceptance counts. No checks were
rerun here beyond document validation. Independent Security Reviewer v2 and post-document
parent final acceptance remain pending; preparation labels and green execution alone
certify neither source nor fixture controls. No product blocker is reported in the supplied
status packet. B2 remains unimplemented; this refresh changes no design or security verdict.
