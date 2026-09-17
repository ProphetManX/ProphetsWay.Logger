# Security Review - ON-03 Label Policy

_Reviewed 2026-09-17 against [threat-model.md](threat-model.md) and
[data-classification.md](data-classification.md), including their complete scope,
field, trust-boundary and ranked-expectation tables._

## Verdict

**Accept: no blocking issues found in the examined scope.** The completed three-type
implementation preserves the reviewed identity, membership, finite-input failure,
owned configuration and local disclosure boundaries. The fresh public vulnerability
scan completed successfully for all three current projects with no advisory matches
in the resolved package graph.

This is the scoped ON-03 implementation/dependency security review, not a declaration
that Logger is secure. It does not establish whole-v4 completion, raw-payload
withholding, full G1-G6 qualification, source-exposure or license clearance, or
permission to commit, merge, publish or deploy. Parent acceptance and the other
required reviews remain separate.

## Scope And Basis

Read completely, including implementations and public XML:

- [SensitivityLabel.cs](../../ProphetsWay.Logger/SensitivityLabel.cs#L13): sealed
  `IEquatable<SensitivityLabel>` reference value; constructor, `Identifier`, typed
  and object `Equals`, and `GetHashCode`.
- [LabelFilterMode.cs](../../ProphetsWay.Logger/LabelFilterMode.cs#L5): non-flags
  enum containing exactly `NoFilter=0`, `Exclude=1`, `AllowOnly=2`.
- [DestinationLabelPolicy.cs](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L21):
  sealed Object-based class; required `(LabelFilterMode mode,
  IEnumerable<SensitivityLabel> labels)` constructor, getter-only `Mode` and
  `ReadOnlyCollection<SensitivityLabel> Labels`, and synchronous
  `bool Allows(IEnumerable<SensitivityLabel> effectiveLabels)`.

Also opened [AGENTS.md](../../AGENTS.md#L1), the shared conventions and full delegation
protocol, D001-D021 in [decision-log.md](../decision-log.md#L1),
[product-brief.md](../product-brief.md#L1), the complete aligned
[requirements.md](../requirements.md#L1), and the independently reviewed B01-B24 policy
contract. ON-03's activated immutable acceptance target authorizes this exact review.
The external completion record retains approval, contract and execution provenance;
the source and canonical standards linked here supply the portable review basis.

The models' "awaiting implementation" entries explicitly describe their earlier
document-authoring checkpoint. The policy now exists and its complete code was graded
against that reviewed target. Neither the old checkpoint wording nor earlier
missing-export test results are current absence findings. Requirements review 05 is
Ready; its draft/capture-time wording does not undo that recorded disposition.

The actual crossings are in-process: identifier to immutable value (TB1), executable
configuration sequence to owned membership (TB2), executable effective sequence to
Boolean or exception (TB3), and intentional getters to a reference holder (TB4).
No principal, tenant, payload, recipient registration, network, persistent store,
serializer or output operation is introduced by these three files. Source-flow
searches were restricted to these files and corroborated by the complete reads.

## Findings

No substantiated in-scope security finding. Counts: **Critical 0, High 0, Medium 0,
Low 0, Informational 0**. No corrective source/configuration patch, new policy decision
or added security requirement is proposed.

Intentional getters, noncryptographic collection hashes and the accepted executable,
nonterminating or concurrently mutated sequence limits are not findings by themselves.
Absence of future dispatch/file/bridge controls is not a defect in this pure predicate.

## Dependency Vulnerabilities

The reviewer ran the authorized public-only, no-restore scan on 2026-09-17,
**04:09:17-04:09:19 UTC**, against the current [Logger.sln](../../Logger.sln#L6).
From the repository root, the equivalent reproducible command is:

```powershell
dotnet list Logger.sln package --vulnerable --include-transitive --no-restore --source https://api.nuget.org/v3/index.json --format json --output-version 1
```

**Actual result:** exit 0; JSON version 1; parameters
`--vulnerable --include-transitive`; sole source
`https://api.nuget.org/v3/index.json`; exactly the library, test and example project
paths; no error entries and no vulnerability entries. Clean results omit framework
arrays, so graph coverage below comes from parsing all current restored assets, not
inventing empty framework results in the scan JSON. No restore, install, build or test
was performed by this reviewer.

| Package | Version | Advisory | Severity | Direct/Transitive | Fixed in |
| --- | --- | --- | --- | --- | --- |
| None in examined resolved graph | n/a | No returned advisory matches | n/a | Direct, implicit and transitive resolutions | n/a |

### Actual Graph Coverage

Opened all three project files and parsed every target/package/dependency dictionary
in their restored assets. Counts exclude project-reference placeholders.

| Project | Restored target graph | Package/version keys |
| --- | --- | --- |
| [Library](../../ProphetsWay.Logger/ProphetsWay.Logger.csproj#L4) | netstandard2.0 | 2 |
| Library | net10.0 | 0 |
| [Tests](../../ProphetsWay.Logger.Test/ProphetsWay.Logger.Test.csproj#L4) | net48 | 34 |
| Tests | net48/win-x86 | 34 |
| Tests | net10.0 | 28 |
| Tests | net10.0/win-x86 | 28 |
| [Example](../../ProphetsWay.Logger.Example/ProphetsWay.Logger.Example.csproj#L5) | net10.0 | 0 |

There are **seven graphs and 41 distinct package/version identities**. Both RID
graphs have the same package identities and dependency edges as their respective
base graph; RID-specific assets are not additional declared TFMs. All 41 identities
match the reopened September 16 ON-02 final security/license inventory, with no added
or removed identity. No new package required an additional provenance review here.

The library and example have no direct package references. The Standard2.0 graph
contains implicit `NETStandard.Library 2.0.3` with selected dependency
`Microsoft.NETCore.Platforms 1.1.0`. The modern library and example add no NuGet
packages; shared-framework and installed-runtime code is outside this package scan.

The seven current [test pins](../../ProphetsWay.Logger.Test/ProphetsWay.Logger.Test.csproj#L13)
are `coverlet.collector 6.0.4`, retained `FluentAssertions 5.10.3`,
`Microsoft.NET.Test.Sdk 18.10.1`, `Moq 4.20.72`, `Shouldly 4.3.0`,
`xunit.v3.mtp-off 4.0.1` and `xunit.runner.visualstudio 4.0.0`. The project remains
non-packable, with both runner switches false. Neither ordinary `xunit.v3`/MTP nor
SponsorLink appears as a resolved package/dependency edge. Build tasks, analyzers,
adapters and collectors still execute with developer/CI privileges when invoked;
`PrivateAssets` is propagation metadata, not a sandbox.

### Historical S01-S04

These dispositions concern the current package graphs, not the installed runtime or
all embedded binaries. The prior report was reopened, not treated as a fresh scan.

| Historical dependency finding | Fresh current observation | Disposition |
| --- | --- | --- |
| S01: Microsoft.NETCore.App 2.1.0 | Current project targets and all seven graph keys/edges contain no Microsoft.NETCore.App package. | Historical implicit package resolution remains removed; OS/runtime servicing not assessed. |
| S02: System.Net.Http 4.3.0 | No package or dependency edge in any graph; Standard2.0 selects Platforms only beneath NETStandard.Library. | Historical vulnerable resolution remains removed. A reference DLL name is not a package resolution. |
| S03: System.Text.RegularExpressions 4.3.0 | No package/edge or old Standard1.3/1.6 target in the current graph. | Historical vulnerable resolution remains removed; no whole-source regex audit claimed. |
| S04: Newtonsoft.Json 9.0.1 beneath old TestHost | No Newtonsoft.Json package/edge. Current net10.0 TestHost 18.10.1 selects ObjectModel 18.10.1; net48 has no TestHost package. | Historical vulnerable resolution remains absent. This does not mean every bundled Newtonsoft binary is absent. |

### Dated License And Bundle Limits

The matching identity set permits reuse of ON-02's **2026-09-16** license inventory
as dated context only. No license texts, package signatures, component binaries or
supplemental bundle advisory ranges were freshly re-audited here. In particular,
the prior review recorded Coverlet's bundled Newtonsoft 13.0.3 separately from S04,
and a Build.Tasks.Git producer-manifest advisory observation separately from shipped
consumer dependencies. This review neither silently recasts those observations as
new package matches nor claims a current all-binary clearance from version numbers.

Native/private instrumentation provenance, exact redistribution notices, stale or
aggregate third-party notices, and exact-release license-text gaps identified by
that prior review remain outside this gate. The NuGet graph scan does not enumerate
all bundled/native/SDK code. No exhaustive license, source-to-binary provenance or
publication conclusion follows, including for unchanged package pins.

## Coverage

Every ranked in-scope threat expectation was checked against actual code. The table
distinguishes source assessment, attributed execution evidence and excluded work.

| Area / obligation | Reviewed | Evidence and result |
| --- | --- | --- |
| Access control and permission boundary; TB1-TB4, D013, B23-B24 | Complete for the three types | [Allows](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L135) returns membership eligibility only. No caller identity, tenant lookup, payload read or recipient handoff exists. Correct classification, effective-label accumulation and recipient authorization are consumer/integration responsibilities, not implicit authentication by this predicate. |
| Exact API and exclusive mode; B01-B03 | Complete | [Enum](../../ProphetsWay.Logger/LabelFilterMode.cs#L5) is non-flags 0/1/2. [Constructor](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L47) rejects every other value with exact ArgumentOutOfRangeException naming mode. Immutable construction makes the switch's default the valid NoFilter path, not recovery from an invalid mode. No extra public behavior was found. |
| Identifier validation; TB1, R-01 | Complete | [Constructor](../../ProphetsWay.Logger/SensitivityLabel.cs#L30) rejects null, lengths outside 1-256 UTF-16 units, and each Char.IsWhiteSpace/Char.IsControl unit before retaining the exact string. No normalization, registry or extra syntax rule is applied. This is not general output encoding. |
| Value equality, hash and immutability; R-02/R-03, B10-B11 | Complete | [Typed equality](../../ProphetsWay.Logger/SensitivityLabel.cs#L68) uses StringComparison.Ordinal; the object overload only recognizes the sealed label type. [Hashing](../../ProphetsWay.Logger/SensitivityLabel.cs#L91) uses StringComparer.Ordinal, and HashSet lookup also checks equality. No hash-only identity, mutable label, arbitrary getter invocation or cryptographic promise was found. |
| Required sequences and null elements; B04-B05/B19 | Complete in all modes | [Configuration checks](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L54) and [evaluation checks](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L135) use exact ArgumentNullException for null sequences and ArgumentException for null elements, with labels/effectiveLabels parameter names. Null is not converted into empty or mismatch. |
| Owned configuration; TB2, B06-B09 | Complete | [Local HashSet and List](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L59) are newly allocated, deduplicated through label equality, and assigned only after enumeration completes. No caller-writable collection is retained. One representative per identity is kept even for NoFilter. |
| Readback and retention; TB4, D013 | Complete for local state | [Labels](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L97) wraps the privately owned list; collection-interface mutation cannot modify backing storage. [Identifier](../../ProphetsWay.Logger/SensitivityLabel.cs#L57), Mode and membership readback are intentional. Configured labels remain retained by the policy, including NoFilter; effective labels are not stored in policy fields. No persistence, retention deadline or secure erasure is implemented or promised. |
| NoFilter; B12 | Complete | [Return](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L164) is true only after normal completion of the full effective-input loop, including empty input. Retained configuration imposes no matching restriction but does not bypass validity. |
| Exclude; B13 | Complete | [Intersection accumulation](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L153) followed by [negation](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L160) denies any match; empty configuration/effective input permits. Unknown identities, duplicates and order do not introduce a permissive match. |
| AllowOnly; B14-B15 | Complete | [Return](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L162) requires hasLabels and allConfigured: nonempty whole-set containment, not any-match. Empty configuration/unlabeled input denies; unknown members deny. Already supplied inherited-only membership is treated as labeled; no origin transport is claimed. |
| Finite tails and sequence failures; TB2/TB3, B18-B20 | Complete | Both [constructor loop](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L61) and [evaluation loop](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L145) precede field publication/return. No early Boolean or catch/recovery hides a null/failing tail. GetEnumerator, MoveNext, Current and foreach disposal failures complete exceptionally, without a usable partial policy or Boolean. Competing-error precedence remains unspecified. |
| Stable reads and evaluation failure state; B07/B17 | Complete source assessment | [Evaluation-local accumulators](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L142) only read the private membership set; no method mutates it after construction. Repeated/concurrent stable-input calls and failed evaluations do not poison policy state. Recorded concurrency tests are bounded observations, not exhaustive scheduling proof. |
| Library-authored diagnostic privacy; TB1-TB3 | Complete | [Label errors](../../ProphetsWay.Logger/SensitivityLabel.cs#L34) and [policy errors](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L51) use fixed text/nameof parameters, with no supplied identifier, membership, encoded value, Data payload or cause attached. The null-element messages at construction/evaluation are fixed literals. |
| Foreign diagnostics and provisional classification; TB3/TB4 | Complete boundary assessment | [Evaluation](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L135) does not catch or sanitize consumer enumerable exceptions. Message/StackTrace/Data/InnerException may contain caller data. The classification model expressly treats these and identifier/membership/derived results as provisionally Confidential in consumer context. They are not safe external responses; R-16 reporting is a different future boundary, not a violated pure-method exception contract. |
| Injection, deserialization, secrets and cryptography | Complete only for the three source files | No SQL/command/HTML/log/file/network/serialization sink, credential literal, certificate override or encryption operation was found in those complete files. Collection hashing is not password protection/anonymization. No whole-repository or history secret search was performed. |
| Availability and executable input; B20-B21 | Complete accepted-limit assessment | [Class remarks](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L10) and loops agree: caller sequence access executes code, and producer mutation, arbitrary resource use, nontermination and effects rollback are not guaranteed. No new cap, deadline, sandbox or cancellation obligation is invented. |
| Current dependencies | Complete resolved-graph section | Fresh scan plus all three project/asset inventories, seven graphs, 41 identities and S01-S04 reconciliation above. No new package identity was found. |
| Test/copy-binding/coverage evidence | Records and relevant test sources read; not executed here | Recorded 110 passing cases per Windows leg, including the identifier diagnostic canary and policy boundary cases. Details and attribution below; not whole-suite or raw-recipient withholding proof. |
| Future integration and wider environment | Excluded, not passed | Existing Logger/file/sink source, F1-F3 runtime behavior, origins/routing/withholding/reporting/framing/bridges, external APIs/principals, installed OS/SDK/runtime servicing, secret history, full-source and publication/redistribution assessment were not assigned. |

No applicable ranked expectation was left unmet in the examined implementation.
R-10 was checked only for immutable copied settings, not atomic route publication;
R-04 only for already accumulated effective membership, not origin transport.
Future R-06/R-14/R-16 and M2-M5 obligations remain unverified here, not waived or
reported as vulnerabilities merely because this class does not implement them.

## Recorded Execution And Integrity

Opened the recovered Test Auditor 07a Ready report, implementation 09 and the parent's
2026-09-17 04:05:30 UTC green summary. The actual
[argument helper](../../ProphetsWay.Logger.Test/DestinationLabelPolicyTests.cs#L429)
already checks GetType against the exact expected exception and checks ParamName;
the withdrawn TA-01 claim is not a current defect. Both relevant test files were read
completely as supporting evidence, not re-audited as a substitute for Test Auditor.

The [identifier diagnostic canary](../../ProphetsWay.Logger.Test/SensitivityLabelTests.cs#L157)
checks Message and Data for raw and encoded rejected input. The
[policy specifications](../../ProphetsWay.Logger.Test/DestinationLabelPolicyTests.cs#L80)
exercise the truth table, ordinal identity, invalid tails, owned/read-only backing,
sequence failures and stable concurrent reads against the real library export.

Parent records show **110 executed, 110 passed, 0 failed, 0 skipped per leg**: 67 policy
cases plus 43 preserved existing cases on Windows net48 and net10.0, with ordinal
same-leg case reconciliation. The recorded builds/binding checks select Standard2.0
for net48 and net10.0 for net10.0, with matching copied-library hashes and target
attributes. Each leg records **48 policy and 26 label hit source lines** in the real
Logger module. Coverage hits do not prove security or every interleaving.

This reviewer independently compared all **45 protected inputs and 48 specification
entries** with their existing generated manifests, with no mismatch or rebaseline.
The three production sources, three project files and three asset files also match
the parent's green build-input hashes. These are fresh byte comparisons, not fresh
test, coverage or binary-load execution. Exact operational evidence is retained by
the delegated run; no private TRX path is needed to interpret this canonical review.

## Worth Checking

No unresolved in-scope suspicion requires a new ON-03 policy question. These later
checks remain explicit and do not turn accepted limits into new findings:

- **Consumer/host:** before exporting getters, eligibility results or foreign errors,
  establish actual classification and authorized recipients/storage/retention under
  D013. This review inspected synthetic inputs, not a deployed consumer's data.
- **Threat Modeler v2 / Security Reviewer v2:** separately review implemented
  origin accumulation, rejected-recipient raw-handoff canaries, reporting privacy,
  framing, F1-F3 and controlled bridges when those dependent surfaces are delivered.
- **Code Reviewer v2 / Test Auditor v2 / parent:** correctness, specification quality
  and final combined ON-03 acceptance retain their independent gates; this review
  does not substitute for them.
- **Release owner / Modernizer v2:** revisit actual package contents, native/bundled
  provenance, license redistribution obligations and installed SDK/runtime servicing
  for publication or source exposure. The clean package graph does not clear them.

No secret value was found in the selected source/metadata; this is not a claim that
the repository or its history contains none. Pipeline/infrastructure permissions
belong to their separate reviewers and were not reviewed. **No security review is
exhaustive.** A clean advisory result is a dated observation, not proof of the absence
of vulnerabilities.
