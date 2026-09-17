# Logger v4 Product Brief

**Owner:** G. Gordon Nasseri. **Decision dates:** 2026-09-13 and 2026-09-15. **Reconciled:** 2026-09-16.
**State:** Accepted product intent, not a claim of implemented behavior or completed verification.

The binding decisions and dated owner quotations are in [decision-log.md](decision-log.md).
This brief summarizes D001-D021, including the dated, clause-specific supersession of earlier policy;
acceptance of those decisions does not replace delivery evidence.

**Pending alignment:** [requirements.md](requirements.md) is unmodified by this capture and retains its
earlier D001-D015 basis. Solution Architect v2 and independent Requirements Reviewer v2 must align the
affected R-15/R-20/R-21 clauses and related capture/reporting, tooling and guidance references before
new F1-F3 file/dispatch behavior is specified or implemented. Do not apply the old clauses unchanged
to the new decisions. Exact contracts, authorized specifications/audit and source implementation and
verification remain pending for F1-F3; toolchain success does not implement those rules. This revision
neither changes the independent pure-label contract nor claims completed label filtering, dispatch,
files, bridges, full-v4 or security-release qualification. README, changelog and security-document
promotions remain with their owners and are not completed by this capture.

## Purpose And Actors

Provide application diagnostics, searchable records and general-purpose logging for developers across
application and deployment styles. Make built-in/custom destination setup straightforward, followed by
static logging throughout an application, while retaining typed metadata and standard-logging interoperability.

| Actor | Intended outcome and responsibility |
| --- | --- |
| Application developer | Configure destinations centrally; supply messages, context and application-defined labels. |
| Destination/integration author | Preserve permitted information, choose explicit rendering/mapping, and own retained asynchronous work. |
| Host/operator | Control recipients, access, storage, retention, costs and safe application teardown. |
| Participating logging systems | Exchange available context through explicitly enabled Microsoft logging integrations. |
| Product owner | Decide scope, consequential policy changes and release commitments. |

## Selected Scope

| Stream | Full initial-v4 commitment |
| --- | --- |
| W1 | CR-01 safe concurrency/callback mutation; CR-02 warning/message/exception preservation; CR-03 non-destructive fallback; CR-04 absent-exception handling; CR-05 ordinary/typed isolation; CR-06 equivalent masks; CR-07 original-failure precedence. |
| W2 | Support/tooling/dependency/security and packaging work; meaningful refactors; tests, documentation and examples; S-01 unambiguous text-record framing. |
| W3 | Both explicitly enabled Microsoft logging bridge directions, preserving available structured information, scopes and origins. |
| W4 | Application-defined immutable labels, propagation without setup, optional whole-entry destination filters, inherited origins and safe check failures. |

Windows Event Log and Azure-specific destinations are explicitly deferred beyond initial v4.
Excluded: built-in field redaction, automatic classification, a closed label taxonomy, a replacement for
Security severity, automatic instrumentation, global flush/drain or background buffering, automatic pruning,
and guaranteed delivery, remote durability, audit storage or compliance. Specialized outputs remain consumer work.

## Workflows

1. Log without configuration for quick-start automatic output; developers should configure their intended destinations rather than rely on defaults as the main logging arrangement. A compatible explicit destination suppresses only its route's fallback, even when its filters reject an entry.
2. Log typed metadata through its exact declared-type route. Configure that type independently of ordinary logging and other types; compatible explicitly registered built-ins count as explicit destinations.
3. Enable either Microsoft bridge direction deliberately. Preserve available category, event, state, formatted message, exception and ordered scopes without requiring a custom destination for useful typed fallback.
4. Attach labels to entries or explicitly labeled enclosing scopes. Optionally select recipients using Exclude-any or Allow-Only-all; Allow-Only rejects effectively unlabeled entries. Inner entries cannot remove inherited labels.
5. Attempt all independently eligible synchronous outputs and perform safe reporting, then propagate configured rendering/output failures even if another output succeeded, without requiring strict selection (D018). Do not reroute failures to an unconfigured destination in the hope they are seen. Consumers own completion after an output returns.

## States And Invariants

Each call uses stable route membership/settings; removal or reconfiguration affects later captures, while
already captured work may finish. Mutation returns after publication, not draining. Routes cannot alias;
removing/disabling the last explicit destination restores only its route's fallback. Active reject-all
settings do not restore fallback. Synchronous invocation promises neither serialized callers nor remote completion.

Default establishment occurs on first actual eligible output, not through permission prechecks for every
message. If both initial locations fail, remember that failure for the current run: later default-dependent
calls throw without re-probing. A new application/session instance starts fresh; a compatible explicit
destination makes the failed default irrelevant for its route. No existing public instance/reset API,
new reset facility, observer or background retry is implied (D018/D019).

Keep selected automatic and explicit file paths fixed despite later current-directory changes. Use the
consuming host application's base directory normally, with application-specific LocalApplicationData
secondary and the existing explicit host-override option; never Logger's assembly folder or the hosting
`dotnet.exe` installation directory. Exact explicit relative-path capture remains reviewed contract work,
not a promise to follow CWD (D020).

Label identity is opaque and value-based. Matching may deduplicate identities but must preserve origins.
Sensitivity denial or failed checks withhold the affected recipient's entire payload before rendering or handoff;
neither creates a less restrictive fallback. Captured collection membership is stable, not arbitrary nested objects.
Supplied resources remain consumer-owned; owned resources retire only after captured users finish (D003-D012).
Shared producer/capture and opted-in filter-check failures retain their compatible D004/D008/D009 policy;
F1 changes configured rendering/output failures, not every failure class. Deliberate severity/label mismatch
and reject-all remain non-failures. Safe-reporting privacy, original-failure precedence and the limits on
arbitrary reflection/implicit object formatting remain unchanged (D018).

## Acceptance Outcomes

- Calls from different application locations reach the centrally configured recipient; ordinary and exact-type routes remain independent, including fallback replacement/reactivation.
- All six severities and valid composites preserve content and eligibility. A message mask of 9 reaches destination mask 15, not 8, and becomes one outbound Critical record retaining its original mask.
- Concurrent first use and callback mutation preserve each captured dispatch; equivalent mask representations agree; helper validation does not reject valid exceptionless Microsoft records.
- Same-run fallback reuse preserves earlier records; initial collision allocation never replaces an unrelated existing file. Successful qualifying initial secondary-location recovery returns normally. If neither location works for eligible default-dependent output, throw with fixed safe guidance to make an appropriate generic default location writable or configure a destination; reveal no actual paths, payloads, labels or raw causes.
- Automatic and explicit files append to the selected path or recreate a missing file there; actual I/O failures throw with independent attempts and safe reporting preserved. Initial collision allocation is distinct from later recreation. No physical-identity policing, content/binary detection, replay, copying or silent relocation is promised; existing-content suitability is the developer's responsibility. Append is the default; D012's separately explicit reset permission is not revoked or converted into implicit truncation.
- Structured values, duplicate names, scope layers and label origins survive controlled bridge round trips. Cyclic return is suppressed without losing unrelated or identical-text fresh entries.
- Denied-recipient canaries remain absent from raw payload handoffs and failure diagnostics. Failed checks withhold that recipient under their unchanged compatible policy; configured rendering/output failures follow D018's mandatory propagation, preserving original-failure precedence and bounded safe reporting even when another output succeeds.
- Built-in text output has one escaped physical line per record; unsupported values show a no-formatter marker without arbitrary getters or implicit object formatting. Capture failures follow the agreed shared/local boundary.
- Full-v4 completion requires the selected W1-W4 work plus independently reviewed contracts, safe audited tests, actual behavioral/support/package/security evidence and verified consumer documentation (G1-G6 in D014). D017 defers non-Windows execution for now; it neither supplies that evidence nor permanently removes it from qualification.

The immutable label value is the first bounded TDD milestone, not a reduction of full-v4 completion (D015).

## Data, Direction And Release

Consumers own classification, explicit mapping/sanitization, storage shape, recipient authorization, retention
and audit obligations. Labels do not guarantee confidentiality; no real-data access, service provisioning or
spend is implied. Development evidence uses synthetic data. Logger is a utility, not an observability platform.

The approved isolated-test parent is `c:/temp/logger tests/`, using fresh uniquely owned children and
only owned cleanup after users finish. No parent/pre-existing-file deletion, escape through links/junctions,
old fixed-filename test execution or file-output implementation follows from that location permission (D021).

Preserve the static/typed experience and established namespace; support modern .NET and .NET Framework consumers.
The support/dependency direction and review conditions are recorded in D014/D017, not a current inventory.
Proceed directly to xUnit v3 within the exact approved ON-02/A1 technical records. Focus execution on Windows
for now; Mac/Linux execution and full cross-platform proof remain deferred, not passed or permanently waived.
The test-project rename remains separate W2 work. No product-version change is authorized.
Breaking changes are accepted, but release timing, channel, deployment and publication require separate owner decisions.
No global trade-off ranking was selected: the concrete accepted trade-offs govern; agents must not invent a ranking.

## Authority Matrix

| Decision area | Who decides | What an agent may infer |
| --- | --- | --- |
| Purpose, scope, behavior and acceptance | OWNER | Preserve D001-D021 and their precise supersession; no unspoken feature, narrowed completion boundary or changed policy. |
| Exact public details within P08/P09 | INFER: owner-delegated contract author, subject to independent review | Choose names/signatures and bridge metadata keys only within accepted behavior; no flattening, lost origins, new policy or implicit file grant. |
| Other public surface, architecture, security, data/privacy/auth and money | OWNER | Nothing beyond explicit decisions; proposals require owner confirmation. |
| Dependency versions, tooling scope and test-project rename | OWNER with required review | Preserve D014/D017 and exact ON-02/A1 limits; no unlisted version, path or operation. Rename/solution/reference changes remain separately subject to explicit approval. |
| Release and operations | OWNER | No inferred version, channel, schedule, Git/publication or live-operation authority. |
| Editorial organization | INFER within the authorized documents | Summarize faithfully and cross-reference local decisions; invent no owner choice. |
