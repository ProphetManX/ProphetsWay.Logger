# Security Review - Scoped Logger Assessments

## M3 Native Context And Reporting

_S40 review and focused S44 re-review, 2026-09-26, against the existing [threat model](threat-model.md),
[data classification](data-classification.md), reviewed M3 P1-P10/addenda, exact
SA-02 replacements, [D024](../decision-log.md#L449) and composed Set A/Set B contracts._

### M3 Verdict

**No blocking issues found in the reviewed M3 scope. S40-01 is closed.** The repaired
report owns its descriptor membership and exposes a separate synchronization object,
so public collection aliases no longer provide the writable backing identified by S40.
The focused source/XML review and input-bound author probe support that closure.

**Open counts: Critical 0, High 0, Medium 0, Low 0, Informational 0; closed: one Medium.**
S44 re-reviewed only the report repair; unchanged M3 coverage is retained from [S40][m3-s40]
after input verification. Three fresh dependency queries completed. The permanent
report-specific SyncRoot regression gap remains, but does not block this security
closure for the verified source. Independent code/test/document gates and parent
FullFinal remain separate. This is neither release approval nor a claim that the
library is secure. This reviewer applied no source fix and ran no product test/probe.

### M3 Findings

#### [CLOSED] S40-01: Writable Failure-Report Backing

**Original severity:** Medium. **Closure:** S44, after [repair S42][m3-s42].

**Location:** repaired [LogFailureReport.cs:33](../../ProphetsWay.Logger/LogFailureReport.cs#L33),
with shared observer delivery in [Logger.cs:477](../../ProphetsWay.Logger/Logger.cs#L477)
and subsequent propagation in [Logger.cs:501](../../ProphetsWay.Logger/Logger.cs#L501).

**Original evidence, preserved:** at S40, the constructor wrapped `failures.ToArray()` in a plain
`ReadOnlyCollection<LogFailureDescriptor>`. That wrapper's non-generic
`ICollection.SyncRoot` exposed the backing array. S40's read-only framework check on
.NET 10.0.12, using only an empty object array, confirmed that the returned object
was the original array. No Logger object was invoked or mutated. This backing
representation predated M3 integration; the original finding and proposed correction
remain in the immutable S40 report, not retrospectively rewritten as a passing review.

**Pre-repair attack:** a misbehaving in-process report subscriber could obtain the
backing collection through the public synchronization property and replace, reorder
or clear descriptor elements. Later subscribers and `LogDispatchException.Report`
could read the changed facts without reflection or a public report constructor.

**Original impact:** recipient positions/stages could be falsified, or null elements could
make later observers fail and lose their notification. This concerned diagnostic integrity
and observer availability, not remote code execution, a label-denial bypass or raw
payload disclosure. The scalar core/overflow counts and mandatory-throw flag were not
changed by this alias. Medium reflected the required in-process observer access and
limited effect; no sandbox or tamper-proof audit-log guarantee is being invented.

**Threat basis:** Set B B14-B17 requires immutable original facts, non-null descriptors
and no writable backing through aliases. P10/B8 requires secondary observers not to
replace original facts. The current report's own
[remarks](../../ProphetsWay.Logger/LogFailureReport.cs#L8) state that same obligation.

**Repair verified:** the sole executable delta calls
[LogContext.CopyMembership](../../ProphetsWay.Logger/LogContext.cs#L56). It eagerly
copies into a fresh private list, then returns a private sealed read-only wrapper whose
[explicit ICollection.SyncRoot](../../ProphetsWay.Logger/LogContext.cs#L65) is a separate
plain object. No caller/source list or public collection API exposes the owned list.
Indexing/enumeration/CopyTo expose only sealed readonly descriptors; a public copy has
independent membership. The synchronization object supplies no membership interface,
and protected backing cannot be exposed by subclassing the sealed wrapper.

S44 independently checked the recorded before/after bytes against current source:
one report assignment, XML-only descriptor/exception changes, no other executable
delta. Structured C# 7.3/XML comparisons match Set B U4, including full positions,
Eligibility/Output/LabelCheck and capture/check-only default return. Counts, bounds,
safe exception members, original result policy and all selection code are unchanged.

**Runtime evidence, attributed:** S42's [actual-public-path observation][m3-probe]
and [encoded command record][m3-probe-command] use two real exact-T Logger calls on
.NET 10.0.12, not fabricated reports or private construction. Twenty output attempts
after a rejected first slot yielded the first eight full positions 2 through 9,
overflow 2 and core count 0. Both subscribers and both mandatory exceptions retained
equal original facts with fresh per-call correlation IDs. SyncRoot exposed no descriptor
membership, direct generic/non-generic writes were rejected, and changing a detached
public copy did not change the report. S44 decoded/read the command and verified its
current inputs/tool binding; **S44 did not execute that probe**. It is not a separate
net48 alias observation, native-fault/OOM exercise or permanent test addition.

**Residual coverage:** the existing
[report regression](../../ProphetsWay.Logger.Test/LoggerTests.cs#L1571) checks direct
IList writes, not SyncRoot. That durable call-site regression gap remains for Test
Designer v2 and focused Test Auditor v2 through the parent. It does not block S40-01
closure: the current ownership path is directly established by source and corroborated
by the bound real-report observation. The gap is future regression protection, not
evidence of a remaining public backing alias. It is not waived or falsely counted as
new suite coverage, and this disposition does not decide the independent code/test gate.

### M3 Basis And Exposure

Both canonical security-model files were reopened. Their earlier proposal-only and M2
checkpoint wording is historical, not evidence that the current types are absent.
This review composes their applicable rules with the September 19 P1-P10 input,
September 22 flow and field addenda, exact SA-02 replacement wording, September 23
conventional-scope amendment and Set B U3/U4 field reconciliation. The external
invocation record preserves their exact paths and current acceptance bindings.

S40 read all 17 scope35 production files, called surfaces and supplied-sink inheritance;
its inventory included untracked supporting types and contextual interfaces. S44 did
not repeat that audit. It reopened the three repaired files, actual owned-membership
helper, shared report delivery/propagation, named regression and author probe. Current
source matches the 49-file S42 snapshot; S40's 54-entry inventory differs only in the
three authorized repair files. Applicable model/contract inputs remain bound and current.

The modeled actors remain in-process producers, configurators, recipients and report
observers. Labels, origins, frames, arbitrary properties and raw payloads retain
provisional Confidential treatment; generated failure facts are Internal activity,
not credentials. [D013](../decision-log.md#L181) leaves classification, recipient
authorization, onward disclosure, sanitization, retention and audit duties with consumers.
No missing tenant/row authentication service is inferred in this logging library.

SA-02 permits original supplied property references, including graphs already holding
handles, delegates, destinations or enumerables. It prohibits Logger-added capture
backlinks, not those supplied capabilities. D024 permits conventional local cleanup
without universal creator detection; unsupported inherited-handle use is not promoted
to supported transfer or a new confidentiality guarantee.

### M3 Dependency Vulnerabilities

S44 ran three fresh queries successfully on **2026-09-26, 22:44:36-22:44:39 UTC**, using
SDK **10.0.401**, JSON output version 1 and the sole requested source
`https://api.nuget.org/v3/index.json`. Each used an exact absolute project path:

```powershell
dotnet package list --project C:/Projects/ProphetManX/ProphetsWay.Logger/ProphetsWay.Logger/ProphetsWay.Logger.csproj --vulnerable --include-transitive --no-restore --source https://api.nuget.org/v3/index.json --format json --output-version 1
dotnet package list --project C:/Projects/ProphetManX/ProphetsWay.Logger/ProphetsWay.Logger.Test/ProphetsWay.Logger.Test.csproj --vulnerable --include-transitive --no-restore --source https://api.nuget.org/v3/index.json --format json --output-version 1
dotnet package list --project C:/Projects/ProphetManX/ProphetsWay.Logger/ProphetsWay.Logger.Example/ProphetsWay.Logger.Example.csproj --vulnerable --include-transitive --no-restore --source https://api.nuget.org/v3/index.json --format json --output-version 1
```

All three exited 0, returned the requested project, no problems and no vulnerability
entries; [fresh query evidence][m3-scan] records exact arguments, times and input checks.
No restore, install, build, credentials or other network operation was used. S40's
successful 21:57 queries remain prior evidence, not runs by S44. All three project files
and three restored asset files still match their S40 identities; the fresh scan also
verified them and the dotnet executable unchanged before/after querying.

| Package | Version | Advisory | Severity | Direct/Transitive | Fixed in |
| --- | --- | --- | --- | --- | --- |
| None returned in examined resolved graphs | n/a | No advisory matches returned | n/a | Direct, implicit and transitive queried | n/a |

Clean query results omit framework arrays; coverage was therefore reconciled against
the actual restored asset target dictionaries, not interpreted as zero target graphs.

| Project | Restored graphs | Package/version identities per graph | Exposure |
| --- | --- | --- | --- |
| [Library](../../ProphetsWay.Logger/ProphetsWay.Logger.csproj#L4) | netstandard2.0; net10.0 | 2; 0 | Production: Standard2.0 resolves NETStandard.Library 2.0.3 and Microsoft.NETCore.Platforms 1.1.0; no direct package references. |
| [Tests](../../ProphetsWay.Logger.Test/ProphetsWay.Logger.Test.csproj#L4) | net48; net48/win-x86; net10.0; net10.0/win-x86 | 34; 34; 28; 28 | Non-packable developer/test graph; tools execute with developer/CI privileges when invoked. PrivateAssets is not a sandbox. |
| [Example](../../ProphetsWay.Logger.Example/ProphetsWay.Logger.Example.csproj#L5) | net10.0 | 0 | Example references the library, with no direct package references. |

Total: **seven restored graphs, 41 distinct package/version identities**. This is a
dated resolved-NuGet-graph query, not a scan of every bundled/native binary, package
signature, license, SDK, OS or shared-framework implementation. Older or deprecated
versions alone are not new security findings. No publication clearance follows.

### M3 Coverage

"S40 retained" means its completed assessment is carried forward over verified unchanged
inputs, not newly re-audited by S44. The repaired reporting boundary and fresh scan are
identified separately. No security review is exhaustive.

| Area / obligation | Reviewed | Evidence and limits |
| --- | --- | --- |
| Ordinary/exact-T whole-entry denial; P1/P5/P8 | S40 retained | [Ordinary](../../ProphetsWay.Logger/Logger.cs#L262) and [typed](../../ProphetsWay.Logger/Generics/Logger.cs#L155) capture before callbacks; disabled/mask/policy/severity rejection precedes raw or contextual delivery. Enabled reject-all stays active; rejection/check failure does not select rescue output. Metadata is not classified or runtime-rerouted. |
| Label validity and predicate; P2 | S40 retained | [Policy](../../ProphetsWay.Logger/DestinationLabelPolicy.cs#L135), SensitivityLabel, LogAnnotations and LabelFilterMode preserve full finite validation, ordinal membership and the reviewed truth table. NoFilter dispatch skips a redundant check only over valid library-owned membership; it does not recover a failed capture. |
| Frame, origin and property capture; P3/P4/P9, SA-02 | S40 retained; helper reopened | LogLabelOrigin, LogLabelContext, LogScopeFrame and [LogContext](../../ProphetsWay.Logger/LogContext.cs#L50) own membership and preserve original property references. Origins use the non-null annotation projection; duplicate/empty attachments survive. No arbitrary property getter, object ToString, nested enumeration or added live-stack/control backlink is used. |
| Native lifetime; P4/D024 | S40 static review retained | [LogScopeHandle](../../ProphetsWay.Logger/LogScopeHandle.cs#L26) uses an immutable parent/frame chain and AsyncLocal head. Current/below/absent cleanup is local; no shared consumed flag or history. Normal inheritance, suppression and outliving captures follow that runtime model; unsupported transfer remains the documented limit, not a creator-security detector. |
| Settings and policy ownership; P8/B05 | S40 retained | DestinationRegistrationSettings stores validated readonly values; registry entries/settings are replaced under private coordination. [LabelPolicy](../../ProphetsWay.Logger/LoggingDestinationCore.cs#L79) publishes whole sealed policy references with Volatile. Captures precede consumer callbacks; immutable configuration conveys no authentication or permanent permission. |
| Supplied-direct currentness and denial; B06-B08 | S40 retained | Both BaseLoggingDestination types and [DispatchDirect](../../ProphetsWay.Logger/Logger.cs#L368) validate arguments, compare ordered current opening references including empty/unlabeled frames, and recheck intrinsic mask/policy before hooks. No payload equality, stale-context permission or registration lookup is used. |
| Trusted delivery and public reentry; B04/B09 | S40 retained | Both internal nonvirtual LogCaptured methods invoke their hook once without recapture/local reporting. Public and explicit-interface entrypoints return through fresh guards. No ambient trusted-dispatch flag exists; a hook exception reaches the outer Output boundary. |
| Event, text and inherited sinks; P1/B11-B12 | S40 M3 crossing retained | EventDestination, GenericEventDestination and TextBasedDestination move payload work behind LogCore. Events retain original permitted raw/T/context values; manual constructors leave Context null. Console/File inherit the text guard. Existing rendering, filename and file-lifecycle behavior is not certified here. |
| Report facts, aliases and retention; P7/P10/B14-B17 | S44 re-reviewed; S40-01 closed | Only generated ID, immutable position/stage descriptors and counts are retained. CopyMembership owns the list and isolates SyncRoot. No raw cause/context/destination/delegate is stored or new backlink introduced. Public copies cannot change original membership. |
| Safe propagated error; B17 | S44 affected boundary; S40 limits retained | [LogDispatchException](../../ProphetsWay.Logger/LogDispatchException.cs#L19) retains the repaired report, fixed text/source, no cause constructor, initially empty Data and null InnerException/HelpLink. StackTrace returns null and ToString ignores mutable inherited content. Reflection, TargetSite, serialization, debugger state and post-catch mutation remain outside the safe-view promise. |
| Core/check failure handling and overflow; P6/B20 | S40 static review retained, not fault execution | Both partials and direct capture catch before handoff and complete with core count 1, no recipient descriptors and default return. LabelCheck records the full captured slot and continues. Mandatory severity/output status is independent of the retained eight descriptors, so overflow cannot suppress propagation. Allocation/runtime-fatal survival is not promised. |
| Default reporting and recursion; P7/P10/B18-B19 | S40 retained; report alias closed | [Completion](../../ProphetsWay.Logger/Logger.cs#L459) does not inspect caught causes. It individually contains captured subscribers and stderr, restores its thread-static reporting flag in finally, and never reports through fanout. Nested entries still run under their own gates/result policy. Fixed stderr plus maximum-width allowed scalars is 124 UTF-16 units including terminator, below 512; total arithmetic widens before addition. |
| Durable report-alias regression | Known remaining gap | [Existing report test](../../ProphetsWay.Logger.Test/LoggerTests.cs#L1571) checks direct IList writes, not SyncRoot. S42's real-report probe is temporary net10.0 author evidence; no report-specific permanent test or separate net48 alias probe was added. Nonblocking for this security closure only. |
| Availability and executable inputs | S40 accepted limits retained | Descriptor accumulation is bounded before insertion. Scope/value enumeration finishes before publication, including disposal. Accepted callbacks, producer sequences, scope depth, nested objects and cross-thread/asynchronous report cycles are not sandboxed or given new quotas/timeouts. No OOM or fabricated native-fault probe was run. |
| Secrets | S40 retained; repair inspected | No new secret literal found in the repair. S40's marker checks across 33 library/example C# files and three project files returned no hits; these were not rerun by S44. No values printed. Git history, ignored settings, machine credentials and exhaustive secret search remain excluded. |
| Injection, deserialization and cryptography | S40 retained; no new mechanism in repair | No SQL/command/LDAP/XPath/HTTP-fetch/type-permissive deserialization/credential-crypto mechanism is introduced. Collection hashes are equality aids. Permitted raw exception rendering is intentional; existing record framing, file-path handling and constructor diagnostics remain excluded later work. |
| Authentication, sessions, audit and compliance | S40 responsibility boundary retained | No endpoint, tenant/row/session/cookie/CORS mechanism exists in the named M3 surface. Labels/settings/context are not authority. Synchronous delivery and safe facts are not durable audit, retention, erasure or consumer-compliance guarantees. |
| Dependency graphs | S44 fresh required query | Three successful public no-restore/transitive queries; seven restored graphs and 41 identities independently reconciled to unchanged assets. Runtime/bundled-binary/license assessment is not implied. |

### M3 Evidence And Gates

S44's [independent comparisons][m3-verification] match **49/49 current source entries**,
**19/19 specification inputs**, **1,608/1,608 setup inputs**, **22/22 M3 frozen records**,
**8/8 review-identity inputs**, **378/378 toolchain inputs**, and both two-entry seals.
S40's 54-entry source baseline remains unchanged history: 51 still match, with exactly
the three authorized repaired files changed and no additions/removals. [Exact diff/XML
checks][m3-diff] bind the repair to current source; the [107-input review manifest][m3-inputs]
records the consulted sources, basis, prior records and tools. No protected baseline was refreshed.

S44 reparsed S42's actual TRXs and compared their recorded identities/outcomes/configuration
with the prior green matrix: **462 passed, 0 failed, 0 skipped on each Windows leg**.
Both test copies match their correct library asset; recorded library/example builds and
their output hashes remain valid. These are reused **author executions**, not fresh
S44 test/build results. The probe's input/tool hashes also match. No product execution,
example execution, test-quality audit, FullFinal or reading of the parallel S43 review occurred.

B20's ordinary-input-unforceable native branches retain S40's static assessment. No public
fault API, invalid internal object, memory-exhaustion experiment or working exploit
was created. The helper adds no payload inspection or new fault contract. The author probe
establishes its ordinary real-report observations only, not native-fault containment,
exhaustive scheduling or permanent regression coverage.

### M3 Worth Checking

- Test Designer v2 / Test Auditor v2 through the parent: retain and assess the named
  permanent report-specific SyncRoot gap under a separately authorized specification boundary.
  This review neither adds a test nor waives another reviewer's required gate.
- Code Reviewer v2: independent CR39-01/CR39-02 disposition remains your gate. S44 verified
  the repaired XML's security meaning and exact source delta, without consuming S43's work.
- Consumer/host: assess actual data classification, recipient access, retention and
  permitted object capabilities before use with real data; supported-use/CLR limits remain.
- Parent: preserve independent code/specification/document gates and run Full Final
  after the required documentation is ready; no merge, publication or deployment authority
  follows. Pipeline/infrastructure permissions, secret history and full-v4 file/bridge
  behavior were not reviewed. **No security review is exhaustive.**

[m3-s40]: ../../../.agent-runs/20260926-1138-logger-m3-alignment/40-m3-security-review.md
[m3-s42]: ../../../.agent-runs/20260926-1138-logger-m3-alignment/42-report-review-repair.md
[m3-probe]: ../../../.agent-runs/20260926-1138-logger-m3-alignment/evidence/s42-alias-20260926T223100-f179ab3b4a1a4b18b1c081d8bbf4daad-observation.json
[m3-probe-command]: ../../../.agent-runs/20260926-1138-logger-m3-alignment/evidence/s42-alias-20260926T223100-f179ab3b4a1a4b18b1c081d8bbf4daad.json
[m3-scan]: ../../../.agent-runs/20260926-1138-logger-m3-alignment/evidence/s44-dependency-scan-r1.json
[m3-verification]: ../../../.agent-runs/20260926-1138-logger-m3-alignment/evidence/s44-review-verification-r1.json
[m3-diff]: ../../../.agent-runs/20260926-1138-logger-m3-alignment/evidence/s44-repair-comparison-r1.json
[m3-inputs]: ../../../.agent-runs/20260926-1138-logger-m3-alignment/evidence/s44-reviewed-inputs-r1.json

## Historical Assessments

The ON-03 assessment below retains its **2026-09-17 historical scope and evidence**,
including its then-fresh public dependency scan and 110-case-per-leg execution.
The separate [ON-04 assessment](#on-04-native-m2-a) adds the eight native M2-A source
files and offline identity checks only. It records 300 cases per leg from the parent,
not a replacement count for ON-03. No fresh advisory, license, SDK/runtime or bundled
component clearance is supplied by ON-04. Neither assessment is whole-Logger or
full-v4 security certification.

## ON-03 Label Policy

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

## ON-04 Native M2-A

_Reviewed 2026-09-17 EDT against both complete canonical security documents, including
[S1-S7 and SV1-SV3](threat-model.md#on-04-m2-a-severity-boundaries) and the
[native field classification](data-classification.md#on-04-m2-a-field-extension).
Source and offline evidence inspection only; no product execution or fresh scan._

### Scoped Verdict

**Accept: no blocking issues found in the assigned native implementation scope.**
The eight complete source files and their diff against the approved starting commit
`5c5dc5a` implement the scoped validity, all-bits selection, pre-effect argument
guards and pre-recipient withholding controls. All S1-S7 source obligations were
reached; none was found unmet. The parent evidence corroborates those paths but is
not the sole basis for this verdict.

**Findings: Critical 0, High 0, Medium 0, Low 0, Informational 0.** No corrective
patch, new policy question or additional security requirement is proposed. Accepted
breaking changes, intentional raw exposure after acceptance and excluded future
features are not reclassified as vulnerabilities merely to populate findings.

This completes only the assigned source/identity assessment. It does not clear
independent correctness or specification-quality reviews, remaining documentation,
combined target acceptance, full-v4 gates, publication or deployment. It supplies
no Git authority and makes no assertion that Logger is secure.

### Scope And Trust Boundaries

Read all eight files linked in the control table completely, including both private
dispatch loops, recipient carriers, protected rendering and exception-detail paths.
Also read the current repository instructions, shared conventions/protocol, approved
target revision 2 with its inherited proposal, the entire reviewed B01-B33 contract,
contract review 05, threat input 03, TA-01 re-audit 09 and implementation report 11.
The contract's historical report-formatting status is not an open semantic finding;
the current parent record documents the mechanical correction and Ready disposition.

Reopened actual [D002](../decision-log.md#L49), [D007](../decision-log.md#L108),
[D010](../decision-log.md#L144) and [D013](../decision-log.md#L181). Ordinary argument
errors and exact public details are the approved contract choices. Their Message,
ActualValue, cause shape and competing-invalid precedence are not new security
requirements; the existing label-specific non-echo rule remains separately scoped.

- **SV1:** consumer configuration enters mask parsing/capture; a raw mask enters
  validation. The result is severity eligibility, not authentication or authorization.
- **SV2:** helpers forward severity, message, exception and optional typed metadata
  to host-selected recipients. Required-null checks precede route mutation, eligibility
  calls and fallback setup. Registration is controlled by code in the same process.
- **SV3:** supplied direct or dispatched event/text entrypoints decide validity and
  eligibility before any virtual massage, callback or print. Accepted raw event
  references and rendered text intentionally cross to the selected recipient.

There is no caller identity, tenant/row lookup, endpoint or external access-control
layer in these eight files. No consumer application was supplied to certify. A custom
recipient, renderer, exception implementation or callback is executable host code,
not sandboxed by a mask. Direct custom overrides and callers already holding payloads
remain outside the supplied-recipient withholding guarantee.

### Control Assessment

| Obligation | Reviewed control and source location | Result and limit |
| --- | --- | --- |
| S1; B01-B03/B11-B12 | [LogLevels](../../ProphetsWay.Logger/LogLevels.cs#L10), [ordinary helpers](../../ProphetsWay.Logger/Logger.cs#L70), [typed helpers](../../ProphetsWay.Logger/Generics/Logger.cs#L73), [extensions](../../ProphetsWay.Logger/Generics/MetadataExtensions.cs#L13) | Exactly six bits 1/2/4/8/16/32 and inclusive masks 1/3/7/15/31/63. Error emits ErrorOnly=2. Security/SecurityOnly and all three Security helper forms are removed; no replacement alias or public raw Logger entrypoint. Intentional breaks are not patch-safe compatibility claims. |
| S2; B04-B07/B22 | [Enum constructor](../../ProphetsWay.Logger/LoggingDestinationCore.cs#L16), [integer constructor](../../ProphetsWay.Logger/LoggingDestinationCore.cs#L38), [parser](../../ProphetsWay.Logger/LoggingDestinationCore.cs#L46) | Complement-of-63 validation accepts all masks 0-63, including unnamed combinations; rejects negative/unsupported bits. Integer input no longer depends on Enum.GetName. Case-sensitive Enum.TryParse retains approved decimal/name-list grammar, followed by bit validation, with no Information fallback. Null/invalid string and typed-mask errors use the reviewed exact exception types and parameter names. |
| S3; B08-B10/B20/B33 | [ValidateMessageLevel](../../ProphetsWay.Logger/LoggingDestinationCore.cs#L109), [ordinary dispatch](../../ProphetsWay.Logger/Logger.cs#L53), [typed dispatch](../../ProphetsWay.Logger/Generics/Logger.cs#L54) | Raw zero/negative/unknown bits throw before comparison. Validity is distinct from false eligibility; `(messageLevel & _reportingLevel) == messageLevel` requires every bit. Thus 9 reaches 15, not 8. The readonly mask and non-mutating query preserve repeated results. A registered zero-mask recipient remains counted, so rejection does not activate fallback. Registry redesign is not certified. |
| S4; B11-B20 | [Ordinary registration](../../ProphetsWay.Logger/Logger.cs#L17), [typed registration](../../ProphetsWay.Logger/Generics/Logger.cs#L18), helper families linked above | Both registration guards precede dictionary/list effects. All twelve direct helpers guard required message/exception before calling private dispatch; all six extensions immediately forward unchanged arguments to those guards. Empty/whitespace text and permitted absent Error context remain valid. The unchecked-route fallback is not executed by this review or by a null-guard probe. |
| S5; B23-B24 | [Event Log](../../ProphetsWay.Logger/LoggerDestinations/EventDestination.cs#L31), [generic event Log](../../ProphetsWay.Logger/LoggerDestinations/GenericEventDestination.cs#L35), [text Log](../../ProphetsWay.Logger/LoggerDestinations/TextBasedDestination.cs#L17) | Each first rejects invalid level with ArgumentOutOfRangeException naming level, then returns on false eligibility, before MassageLogStatement, event construction/callback, text composition or PrintLogEntry. Validation does not depend on subscriber presence. Both Logger loops separately condition custom raw Log calls on the recipient's Boolean; supplied direct guards do not conceal a custom-handoff bypass. |
| S6; B21/B25-B32 | [MassageLogStatement](../../ProphetsWay.Logger/LoggingDestinationCore.cs#L72), [ExceptionDetailer](../../ProphetsWay.Logger/LoggingDestinationCore.cs#L84), recipient sources above | Absent exception returns the original message, including null. A present exception contributes its current/nested messages and available stacks at every valid mask, without a severity-name branch erasing context. Accepted carriers retain the complete mask, original RawMessage/Exception and generic Metadata; no clone, fabricated raw value or lowest-bit reduction. Existing timestamp/text composition remains unchanged, not framing or redaction. |
| S7; B18/B21/B26-B27 and preservation | [Typed helpers](../../ProphetsWay.Logger/Generics/Logger.cs#L73), [extensions](../../ProphetsWay.Logger/Generics/MetadataExtensions.cs#L13), protected-input comparison | General T stays unconstrained; only extensions retain the existing marker constraint. Null/default metadata remains permitted. Label/policy sources and specifications match their protected hashes; their earlier review remains dated, not freshly re-audited here. No label integration, registry/lifetime, failure-reporting, privacy, bridge or scope-origin guarantee is added. |

### Data Exposure And Diagnostics

The classification model's S-A1 applies: actual message text, exception graphs,
metadata and derived text are unbounded and provisionally Confidential, not observed
PII or credentials. Only synthetic evidence was inspected. Severity names/constants
are public vocabulary; consumer-specific masks, timing and routing remain context.

After acceptance, event subscribers intentionally receive Message, RawMessage,
Exception, LogLevel and local Timestamp; generic subscribers also receive Metadata.
The raw exception exposes Data and nested causes even though shared massage renders
message/stack detail rather than serializing Data. Text recipients receive composed
content. None is a sanitized reporting DTO, safe HTTP response or authorization for
blanket serialization. D013 leaves onward readership, classification, sanitization,
retention and storage with the consumer; withholding from one denied recipient is
not secure erasure from all copies.

The current native guards use fixed text or parameter-only ordinary errors, without
attaching the rejected payload. That is a source observation, not a new promise about
exact diagnostic text or every exception thrown by host code. Exception getters,
virtual massage and callbacks can execute consumer code after acceptance. No safe
failure-reporting channel, recursion bound, execution deadline or hostile-code
isolation was selected or assessed by this slice.

### Offline Dependency Review

**No fresh vulnerability scan was run.** The approved no-network/no-restore envelope
expressly limits this gate to source assessment and offline graph identity. The
September 17 **04:09:17-04:09:19 UTC** public scan in the ON-03 section was reopened
with its actual recorded JSON, exit 0 and no returned advisory matches, solely as
dated context. Its result is not relabeled current.

Opened all three current projects and parsed all target/package/dependency dictionaries
in their restored assets using a structured JSON parser. The three project files and
three asset files byte-match the ON-03 parent build-input manifest. There are still
seven graphs and 41 distinct package/version identities, with no identity drift;
both RID graphs match their base graph's package identities and dependency edges.
The earlier graph table and seven test pins therefore describe the same resolutions,
not a new advisory query. Both test-runner switches remain false.

Historical S01-S04 package/edge absences remain graph observations, not claims about
every embedded binary. ON-02's September 16 license/bundle record, including bundled
Newtonsoft and the distinct Build.Tasks.Git producer-manifest observation, remains
dated and unrefreshed. No package files, signatures, supplemental advisory ranges,
native/private instrumentation, exact redistribution notices or license texts were
re-audited. `PrivateAssets` does not sandbox developer/CI tools. Installed OS,
SDK/runtime and shared-framework servicing are outside the NuGet identity comparison.
**No fresh advisory, license, SDK, runtime, bundle or publication clearance follows.**

### Execution Attribution And Integrity

The parent's independently generated summary is dated **2026-09-18 03:29:38 UTC**
(September 17 EDT). Its two no-restore builds passed; its focused Windows net48 and
net10.0 runs each record **300 executed, 300 passed, 0 failed, 0 skipped**. This is
ON-04 evidence, not the historical ON-03 110-case selection above, and not the full suite.

This reviewer freshly checked the immutable 19-entry specification/authority manifest,
29 protected existing inputs and all 42 parent build inputs: no added, removed or
changed entry, and no rebaseline. The actual two TRXs match their recorded hashes and
contain 300 passed identities each, equal ordinally to the same target's audited
specification identities and parent result records. The recorded dotnet executable
hash matches; no dotnet command or product assembly was executed by this reviewer.

Both actual TRX-linked Cobertura attachments retain their hashes and real Logger
module. Per-leg hit-line observations are core 47, ordinary Logger 51, typed Logger
50, extensions 18, event 24, generic event 26, text 12, label 26 and policy 48.
FileDestination, ConsoleDestination and ConsoleWrapper have zero hit lines. Actual
collector logs name the test-copy module, and its bytes match the recorded built
netstandard2.0 library for net48 and net10.0 library for net10.0. This is fresh offline
verification of attributed execution, not new execution or a security proof by coverage.

The [ordinary custom-handoff case](../../ProphetsWay.Logger.Test/LoggerTests.cs#L279)
and [typed case](../../ProphetsWay.Logger.Test/GenericLoggerTests.cs#L253) were read
with their unconditional recorders. A rejected call would capture raw payload even
if a built-in could self-filter; accepted controls also check original references.
The [invalid direct-mask probes](../../ProphetsWay.Logger.Test/BasicTests.cs#L245),
[rejected direct calls](../../ProphetsWay.Logger.Test/BasicTests.cs#L269) and their
accepted controls count actual virtual massage/callback/print work. Their passing
identities are present in both parent TRXs. This corroborates S4/S5; it does not
replace Test Auditor's separate specification-quality gate or claim mutation testing.

All 25 historical task definitions retain their order/content. The only three added
task labels are ON-04 validation entries, unchanged from the parent green inputs;
none was executed here. The implementation author's recorded 50-input comparison
identifies only its seven granted source changes, without additions/removals; the
fresh eight-file diff also includes the separately owned enum change. No project,
package, source, specification, task or version was changed by this reviewer.

### Checklist Coverage

| Area | Reviewed | Notes |
| --- | --- | --- |
| Access control and trust | Complete for SV1-SV3 | Severity selection and pre-handoff withholding only; no authentication, tenant or row authorization claim. Consumer access control is not an unimplemented native requirement. |
| Mask/input handling | Complete for S1-S4 | Every scoped constructor/helper/registration/query path read; exact validity, reject-all and before-effects ordering checked. |
| Disclosure and direct handoff | Complete for S5-S7 | All three supplied direct paths, both custom dispatch paths and accepted raw/event/text carriers read. Classification responsibility and diagnostic limits retained. |
| Injection, serialization, cryptography, secrets | Scoped source inspection | No SQL, command, LDAP, XPath, HTTP-fetch, deserialization, credential or cryptographic mechanism found in the eight files. Existing text composition is not output encoding or record-framing clearance. No repository/history secret scan. |
| Authentication, sessions, external APIs | Not present in this surface | No endpoint, cookie, token, CORS or CSRF control to certify; no invented external deployment. |
| Availability | Scoped assessment | Null misuse is rejected before dispatch and permitted absence is safe. Unbounded accepted content, exception recursion and consumer execution remain explicit limits, not newly imposed quotas or a denial-of-service clearance. |
| Audit, compliance and retention | Responsibility boundary only | No audit trail, durable delivery, erasure or compliance assurance. Synthetic evidence cannot settle consumer data/jurisdiction facts. |
| Dependencies and evidence integrity | Offline checks complete | Seven unchanged graphs, three project/asset pairs, 19/29/42 manifest comparisons, same-leg case identities, actual coverage/binding and preserved tasks. No fresh advisory/license/bundle/runtime scan. |
| Label/policy | Preservation only in ON-04 | Earlier ON-03 verdict and scan remain historical; current matching hashes do not recertify all prior or future behavior. |
| Other code and integration | Excluded, not passed | Full-v4/G1-G6, label transport, registry/snapshots/concurrency/lifetime, scopes, reporting/privacy, file/fallback/recovery, framing/UTC, bridges, Microsoft conversion, non-Windows, package/release and deployed consumer assessments. |

### Limits And Handoff

No unresolved in-scope suspicion or new product question was found. No secret value
was encountered in the selected source/metadata; that is not a repository/history
absence claim. No real data, file-output fixture, network, cloud or database was used.
No-setup Trace delivery and all-six automatic-file behavior remain unclaimed.

- **Code Reviewer v2:** independent correctness assessment remains separate; no
  correctness verdict is substituted here.
- **Test Auditor v2:** the Ready TA-01 re-audit remains its own gate; this supporting
  evidence read does not replace or reopen it.
- **README Author v2 / Changelog Author v2:** document the approved native breaks and
  limited implemented scope; neither full-v4 completion nor fresh package clearance.
- **Repo Analyst v2:** the historical AGENTS target/dependency inventory is not current
  package evidence; no unrelated cleanup is requested.
- **Pipeline Auditor v2 / Azure Deployment Reviewer v2:** their respective secrets,
  permissions and isolation surfaces were not reviewed.
- **Parent:** combine this source-gate pass with the independent code review, remaining
  document/example gates and final preservation/scope checks under the same target.
  Any eventual checkpoint still requires the designated verification and sole operator.

Later consumer and release reviews retain the previously stated classification,
advisory/license/bundle/runtime and integration limits. They are not new M2-A blockers
or permission for an excluded operation. **No security review is exhaustive.**

## M2-B Explicit Membership

_Reviewed 2026-09-18 EDT against [MB-R1-7](threat-model.md#m2-b-explicit-registration-membership)
and the [membership classification](data-classification.md#m2-b-membership-field-extension).
Acceptance revision 2 inherits revision 1 unchanged. Earlier assessments retain their dates._

### Scoped Verdict And Findings

**Accept: no blocking issues found in the two-partial explicit-membership implementation.**
All seven assigned threat obligations were reached; none was found unmet. **Critical 0,
High 0, Medium 0, Low 0, Informational 0.** No correction, waiver or new policy is proposed.
This is a source-security verdict, not whole-Logger, dependency, full-v4 or release clearance.

Read both complete production partials below, the generated implementation diff and
before/after comparisons, reviewed S1-S6/B01-B22 contract and independent contract review,
test re-audit/frozen binding, relevant synthetic tests, and the parent's independently
rerun final records. The core mask declaration was reopened for preservation evidence.
No command, test, hash computation, advisory query, live operation or subagent ran here.

### Control Assessment

| Obligation | Source evidence and assessment |
| --- | --- |
| MB-R1: explicit recipient isolation | [Logger.cs](../../ProphetsWay.Logger/Logger.cs#L9) owns a separate ordinary list; [Generics/Logger.cs](../../ProphetsWay.Logger/Generics/Logger.cs#L31) uses only `typeof(T)` keys. Destination-interface metadata cannot alias the ordinary list. No runtime metadata inspection or assignability search selects another explicit route. |
| MB-R2: complete ordered capture | [Ordinary capture](../../ProphetsWay.Logger/Logger.cs#L84) and [typed capture](../../ProphetsWay.Logger/Generics/Logger.cs#L95) call `ToArray()` under the same private lock used by every writer. Iteration reads the detached array, not a live list, before/after mutations. Append and reference-removal preserve insertion/survivor order. |
| MB-R3: no user code under registry coordination | [Ordinary loop](../../ProphetsWay.Logger/Logger.cs#L98) and [typed loop](../../ProphetsWay.Logger/Generics/Logger.cs#L108) invoke eligibility and callbacks after leaving the lock. Writer predicates call only `ReferenceEquals`; no recipient equality, hashing, formatting, eligibility or callback executes inside registry operations. Runtime `typeof(T)` keys do not invoke recipient-defined identity. |
| MB-R4: reference identity and route-scoped mutation | [Ordinary add/remove](../../ProphetsWay.Logger/Logger.cs#L24) and [typed add/remove](../../ProphetsWay.Logger/Generics/Logger.cs#L24) coordinate duplicate detection and mutation under one lock. Exact `ArgumentException` naming `newDest` precedes duplicate insertion; equal-but-distinct references coexist. Null/absent remove is a no-op. Each clear touches only its selected list; no cross-route deduplication or disposal appears. |
| MB-R5: publication, reentry and old captures | [Ordinary dispatch](../../ProphetsWay.Logger/Logger.cs#L81) and [typed dispatch](../../ProphetsWay.Logger/Generics/Logger.cs#L92) retain their local arrays while callbacks mutate membership. Mutations never wait for delivery to drain. Recursive logging enters a new capture; no blanket recursion suppression, caller serialization or global delivery order is introduced. |
| MB-R6: borrowed lifetime | [Ordinary removal/clear](../../ProphetsWay.Logger/Logger.cs#L47) and [typed removal/clear](../../ProphetsWay.Logger/Generics/Logger.cs#L54) only change membership. They never call `Dispose`, including for shared recipients. Older captures can still hand off after removal returns. The host must quiesce synchronous users before disposal; removal is not immediate revocation or erasure. |
| MB-R7: preserved raw-data gates | Both complete helper families retain required-null guards and exact severity forwarding. Both loops test `ValidateMessageLevel` before passing original message/exception/metadata to `Log`; rejected explicit membership does not become an empty route. Null add precedes mutation. [Core mask](../../ProphetsWay.Logger/LoggingDestinationCore.cs#L62) remains readonly. No public settings API, payload clone, redaction or arbitrary custom-state freeze is added. |

MB1 configuration and MB2 topology remain private process state, not authentication.
MB3 intentionally exposes original permitted payloads to the selected eligible callback.
The model's provisional Confidential classification for unbounded message, exception
and metadata contents remains applicable; synthetic examples do not downgrade it.
Duplicate errors use fixed text/parameter names without serializing the recipient.
Consumer callbacks can still block, recurse, throw, retain or export payloads with
host-process privileges. D005/D006/D013 accept these boundaries, not a sandbox guarantee.

### Supporting Evidence

Opened [identity and capture tests](../../ProphetsWay.Logger.Test/LoggerTests.cs#L312),
including hostile equality, callback clear, recursive capture, paused user code and
competing writers; and [cross-route tests](../../ProphetsWay.Logger.Test/GenericLoggerTests.cs#L288),
including the repaired typed-removal witness and declared-type routing. Existing
[ordinary raw-handoff](../../ProphetsWay.Logger.Test/LoggerTests.cs#L279) and
[typed raw-handoff](../../ProphetsWay.Logger.Test/GenericLoggerTests.cs#L253) cases use
unconditional recorders, so built-in self-filtering cannot conceal a dispatch bypass.

The parent's final summary at **2026-09-19 01:56:29 UTC** and corresponding input,
net48 and net10.0 records report **320 executed/passed, 0 failed/skipped per Windows
leg**, original 300 outcomes preserved, frozen 320 identities unchanged, and 69/69
inputs stable during execution. The before/after comparisons identify only the two
permitted production changes, unchanged helper tails and public signatures. Source
hashes in those records agree with the parent's final manifest. This leaf consumed
those generated comparisons; it did not independently recompute hashes or parse TRXs.
The parent's recorded asset binding and ten in-memory README compilations are supporting
execution evidence, not proof of every interleaving or later documentation content.

### Dependencies And Coverage

**No fresh vulnerability scan ran:** commands/network are outside this bounded target.
The supplied offline comparisons include unchanged project and restored package inputs;
input equality is not advisory freshness. ON-03's dated scan and historical S01-S04
dispositions above remain unchanged, as do the dated bundled-component/license limits.
No new package, advisory, license, runtime or publication clearance is claimed.

| Area | Reviewed | Limit |
| --- | --- | --- |
| Misrouting, substitution and access boundary | Complete MB-R1/3/4 source assessment | Exact explicit routes only; no principal, tenant, endpoint or consumer authorization certification. |
| Captures, races and user-code coordination | Complete MB-R2/3/5 source assessment | Bounded supporting tests, not exhaustive schedules, fairness, quotas or hostile-code isolation. |
| Borrowed resources and raw disclosure | Complete MB-R6/7 source assessment | No drain, revocation, erasure, custom-state immutability or permission to export raw graphs. |
| Injection, secrets, cryptography and deserialization | Changed registration/dispatch paths inspected | No new such sink/mechanism or secret literal found; no repository/history scan or downstream sink assessment. |
| Sessions, audit and compliance | Boundary assessed; no new mechanism present | No auth/session, durable audit, retention or real-data compliance claim. |
| Dependency and execution evidence | Supplied offline records read | No fresh scan, command execution or independently recomputed content/binary binding. |
| Other implementation and integration | Excluded, not passed | Unconfigured typed-to-ordinary and automatic-file paths, label integration, reporting, files/fallback, bridges, owned retirement, full-v4 and release. |

### Limits And Handoff

No unresolved in-scope suspicion or secret value was found in the inspected slice.
No new owner question is needed. Existing unconfigured forwarding at
[typed empty-route dispatch](../../ProphetsWay.Logger/Generics/Logger.cs#L102) and
[ordinary automatic-file setup](../../ProphetsWay.Logger/Logger.cs#L89) remains expressly
excluded and unqualified, not certified by the explicit-route verdict.
Code Reviewer v2 retains correctness/concurrency review; Test Auditor v2 retains
specification quality. The parent must verify the combined document scope and final
preservation checks; this verdict substitutes for neither those gates nor publication
authority. Later consumer/release reviews retain advisory, data-handling and integration
work outside this target. **No security review is exhaustive.**
