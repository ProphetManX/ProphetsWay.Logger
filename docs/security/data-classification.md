# Scoped Label Policy Data Classification

This inventory covers the fields and method-boundary carriers of `SensitivityLabel`,
`LabelFilterMode` and the reviewed `DestinationLabelPolicy` target, not arbitrary Logger
payloads or business entities. It is a design-time handling model, not an implementation
security verdict, built-in taxonomy, confidentiality promise or new product requirement.

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
