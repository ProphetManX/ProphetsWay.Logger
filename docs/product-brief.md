# Logger v4 Product Brief

**Owner:** G. Gordon Nasseri. **Decision date:** 2026-09-13.
**State:** Accepted product intent, not a claim of implemented behavior or completed verification.

The binding decisions and dated owner quotations are in [decision-log.md](decision-log.md).
This brief summarizes D001-D015; acceptance of those decisions does not replace delivery evidence.

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

1. Log without configuration to obtain useful automatic output. Register a compatible explicit destination to suppress that route's fallback, even when its filters reject an entry.
2. Log typed metadata through its exact declared-type route. Configure that type independently of ordinary logging and other types; compatible explicitly registered built-ins count as explicit destinations.
3. Enable either Microsoft bridge direction deliberately. Preserve available category, event, state, formatted message, exception and ordered scopes without requiring a custom destination for useful typed fallback.
4. Attach labels to entries or explicitly labeled enclosing scopes. Optionally select recipients using Exclude-any or Allow-Only-all; Allow-Only rejects effectively unlabeled entries. Inner entries cannot remove inherited labels.
5. Attempt all independently eligible synchronous outputs. Default failure handling notifies and returns; explicit strict handling throws after attempts/notification when any original output fails. Consumers own completion after an output returns.

## States And Invariants

Each call uses stable route membership/settings; removal or reconfiguration affects later captures, while
already captured work may finish. Mutation returns after publication, not draining. Routes cannot alias;
removing/disabling the last explicit destination restores only its route's fallback. Active reject-all
settings do not restore fallback. Synchronous invocation promises neither serialized callers nor remote completion.

Label identity is opaque and value-based. Matching may deduplicate identities but must preserve origins.
Sensitivity denial or failed checks withhold the affected recipient's entire payload before rendering or handoff;
neither creates a less restrictive fallback. Captured collection membership is stable, not arbitrary nested objects.
Supplied resources remain consumer-owned; owned resources retire only after captured users finish (D003-D012).

## Acceptance Outcomes

- Calls from different application locations reach the centrally configured recipient; ordinary and exact-type routes remain independent, including fallback replacement/reactivation.
- All six severities and valid composites preserve content and eligibility. A message mask of 9 reaches destination mask 15, not 8, and becomes one outbound Critical record retaining its original mask.
- Concurrent first use and callback mutation preserve each captured dispatch; equivalent mask representations agree; helper validation does not reject valid exceptionless Microsoft records.
- Same-run fallback reuse preserves earlier records; collisions never replace existing files. Successful initial secondary-location recovery returns even in strict mode; uncertain writes are never replayed elsewhere.
- Structured values, duplicate names, scope layers and label origins survive controlled bridge round trips. Cyclic return is suppressed without losing unrelated or identical-text fresh entries.
- Denied-recipient canaries remain absent from raw payload handoffs and failure diagnostics. Failed checks withhold that recipient; default/strict behavior and original-failure precedence remain consistent.
- Built-in text output has one escaped physical line per record; unsupported values show a no-formatter marker without arbitrary getters or implicit object formatting. Capture failures follow the agreed shared/local boundary.
- Full-v4 completion requires the selected W1-W4 work plus independently reviewed contracts, safe audited tests, actual behavioral/support/package/security evidence and verified consumer documentation (G1-G6 in D014).

The immutable label value is the first bounded TDD milestone, not a reduction of full-v4 completion (D015).

## Data, Direction And Release

Consumers own classification, explicit mapping/sanitization, storage shape, recipient authorization, retention
and audit obligations. Labels do not guarantee confidentiality; no real-data access, service provisioning or
spend is implied. Development evidence uses synthetic data. Logger is a utility, not an observability platform.

Preserve the static/typed experience and established namespace; support modern .NET and .NET Framework consumers.
The accepted support/dependency direction and its review conditions are recorded in D014, not a current inventory.
Breaking changes are accepted, but release timing, channel, deployment and publication require separate owner decisions.
No global trade-off ranking was selected: the concrete accepted trade-offs govern; agents must not invent a ranking.

## Authority Matrix

| Decision area | Who decides | What an agent may infer |
| --- | --- | --- |
| Purpose, scope, behavior and acceptance | OWNER | Preserve D001-D015; no new feature, narrowed completion boundary or changed policy. |
| Exact public details within P08/P09 | INFER: owner-delegated contract author, subject to independent review | Choose names/signatures and bridge metadata keys only within accepted behavior; no flattening, lost origins, new policy or implicit file grant. |
| Other public surface, architecture, security, data/privacy/auth and money | OWNER | Nothing beyond explicit decisions; proposals require owner confirmation. |
| Dependency versions, tooling scope and test-project rename | OWNER with required review | Preserve P12 direction; exact versions/writes need review and authorization; enumerate rename/solution/reference changes for explicit approval. |
| Release and operations | OWNER | No inferred version, channel, schedule, Git/publication or live-operation authority. |
| Editorial organization | INFER within the authorized documents | Summarize faithfully and cross-reference local decisions; invent no owner choice. |
