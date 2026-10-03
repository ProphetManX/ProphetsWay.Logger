# Project AI Dashboard

This is a desktop-first, repository-local dashboard for Logger's existing work. Open
[statusreport.html](statusreport.html) after generating a snapshot. It needs no web server,
browser extension, internet connection, or Node runtime to display that snapshot.

## Refresh And Open

From the repository root, in PowerShell 7:

```powershell
pwsh -File ./ai-dashboard/Update-Dashboard.ps1 -Open
```

Omit `-Open` to refresh without opening a browser. The page's reload button reloads the
published snapshot; it does not run PowerShell or collect historical agent records. The
separately authorized structured-reporting mode below supplies an owned file watcher for new canonical reports.
No scheduled task or watcher is started merely by opening this page.

The updater discovers the repository from this folder's parent. It defaults to the sibling
`.agent-runs` directory and the preceding three days of detailed reports, keeping the latest
run's details available even when older. Optional parameters:

```powershell
pwsh -File ./ai-dashboard/Update-Dashboard.ps1 -RecentDays 3 -RunRoot C:/path/to/.agent-runs
```

## Views And Boundaries

- **Overview:** latest recorded assignment, completed component checkpoints, report outcomes,
  remaining milestone focus, and visible discovery/coverage gaps.
- **Work plan:** stages and completion boundaries from the requirements source, the latest
  run's remaining-work paragraph, and its recorded slice targets. Structured reporting adds
  expandable milestone/slice forecasts with separate progress and activity.
- **Requirements:** definitions and acceptance-criterion IDs. Completion stays unreconciled
  until requirement-level evidence is explicitly reconciled; no agent result completes an
  entire requirement by inference.
- **Agents:** latest and previous recorded tasks per role, with separate invocation identities,
  scope/findings, next-action excerpts, and source links. Inferred roles are labeled.
- **Run history:** preserved run outcomes and older invocation details. Earlier failures or
  partial outcomes remain historical, even when a later run resolved their cause.
- **Help & reviews:** explicit agent requests and linked responses, including scope, changed
  area, concern, evidence and participant identities. A reply is not automatic gate clearance.

Colors always have accompanying icons and text. Progress bars count recorded invocation
outcomes, not overall project completion. Additional future slices remain TBD rather than
being silently excluded from a project-wide percentage. A STARTED-only report is an incomplete
record, not evidence that an agent is still running.

## Data And Ownership

[Update-Dashboard.ps1](Update-Dashboard.ps1) writes the historical import and initializes a
missing reporting view to `null`; it never overwrites an existing publication. The separate
shared publisher owns new canonical-report projections. The HTML and renderer consume both; nobody
maintains a second HTML narrative. Original run summaries, reports, targets, approvals,
validators and evidence remain read-only to this importer. It executes none of their commands
and makes no product, Git, release, or live-agent changes.

Inputs are parsed with PowerShell's bundled Markdig parser. Candidate run folder names must
match the repository token, and an explicit repository declaration in the run, assignment,
or slice target must match the exact repository path. Unsupported folders and unreadable
records remain visible in **Run history / Import coverage**. This is not an exhaustive
legacy-format migration.

Run and report outcome fields come from their leading metadata, not a keyword search through
historical prose. A recorded consumed checkpoint is a run's reported disposition, not a new
Git or evidence verification. Source hashes identify the bytes imported; they do not certify
the underlying claims. Excerpts are bounded and full sources remain linked.

Generated files live in the ignored `data/` directory:

- `dashboard-data.js`: project definitions, run summaries, checkpoints, and recent details.
- `archive-data.js`: older invocation details, loaded only when requested.
- `pilot-data.js`: publisher-owned current forecast, canonical report summaries and exchanges.
- `pilot-publication-state.json`: publisher-owned integrity bindings, not a disposable cache
  that can be removed to accept changed finalized records.

The `pilot-` filenames and browser data key are retained for compatibility with the first pilot;
both `dashboard-pilot-v1` and the full-roster `dashboard-v1` mode use them.

The two historical files carry a matching generation ID. Files are published through temporary-file replacement,
and overlapping publication is rejected with a lock. History loading refuses a mismatched
generation. The three-day boundary changes presentation, not evidence retention: no source
report is moved or deleted. Regenerating replaces the two derived files rather than creating
another set of run receipts.

Data can contain project-sensitive descriptions despite limited credential-shaped-text
redaction. Do not publish or commit it. Relative source links work in this local checkout;
they are not portable copies of the external evidence. UI text is inserted as text, never
executed as imported HTML.

## Verification And Tools

The reader's focused checks require only PowerShell 7:

```powershell
pwsh -File ./ai-dashboard/Update-Dashboard.ps1 -SelfTest
```

[Test-Dashboard.cjs](Test-Dashboard.cjs) uses optional Node, Playwright, and installed Microsoft
Edge to check offline rendering, navigation, search, filters, details, archive generation
binding, escaped report content, and 1440px/2560px desktop layouts. Screenshots are ignored.
No Logger product tests or historical validators are run by these checks.

The prototype's development tools are installed per-user under
`$env:LOCALAPPDATA/ProphetsWay/DashboardTools`, with no elevation, permanent PATH change, or
repository package dependency. To use that portable installation in a new PowerShell session:

```powershell
$node = Get-ChildItem "$env:LOCALAPPDATA/ProphetsWay/DashboardTools/node-*-win-x64/node.exe" |
    Sort-Object FullName -Descending | Select-Object -First 1
& $node.FullName ./ai-dashboard/Test-Dashboard.cjs
```

Tests find Playwright in that installation, or in a `DASHBOARD_TEST_TOOLS` environment-variable
directory containing the package. Icons are bundled locally from Lucide under
[lucide.LICENSE](lucide.LICENSE); no external font or icon request is made by the page.

## Structured Agent Reporting

`dashboard-v1` supports Vanguard v2 and all 28 exact leaf roles in its allowlist. Toolbelt Keeper
remains outside the product-run workflow. The full contract, schema and readiness boundaries are in
[agent-dashboard-v1.md](../../prophets-pipelines/conventions/agent-dashboard-v1.md).
Vanguard proposes this mode for new dashboard-enabled runs with the normal explicit operational
authority. The existing design, role responsibilities and required verification policy are unchanged.

The earlier `dashboard-pilot-v1` mode remains restricted to Implementer and Code Reviewer under
[agent-dashboard-pilot-v1.md](../../prophets-pipelines/conventions/agent-dashboard-pilot-v1.md).
Existing pilot/Markdown runs retain their recorded format. A new full-roster run preserves their
history; it does not convert frozen records or add work to an expired assignment.

Vanguard owns the explicitly authorized `live/project.json` ledger: milestone and stable
slice IDs, forecast totals/TBD, progress/activity, dependencies, source evidence and exact
invocation registrations. Each participating leaf owns only its registered current-run JSON
record. That record contains its status, scope decision, intended check, actual outcomes,
report body and append-only help/review exchanges.

The shared publisher generates the compatibility Markdown that existing readers still need;
the leaf does not write a duplicate narrative. Required verification and independent review
remain unchanged. A new run must prove compatibility with its actual readers before product
work; frozen validators and historical reports are not silently converted or edited.

With an explicitly approved ledger and installed Node/Ajv, Vanguard starts the fixed helper
in an owned asynchronous terminal, using the actual resolved paths:

```powershell
& $approvedNode $approvedPublisher --project $approvedProjectLedger --watch
```

The publisher entry point is
[AgentDashboard.cjs](../../prophets-pipelines/conventions/scripts/AgentDashboard.cjs).
`--once` performs a one-shot publication/reconciliation instead. Node and Ajv are required
while publishing; neither is required just to view already-published files. The existing
per-user DashboardTools installation supplies them. No web server or external request is used.

The page checks its local publication every 15 seconds while visible. Source timestamps
remain separate from publication time; a working status older than five minutes is labeled
stale, not failed. Expansion state is preserved. Old finalized details are compacted to
clickable archive references after three days; canonical records and their derived reports
are not deleted. The publisher is stopped at pause/sign-off, leaving the last view readable.

Malformed records, identity/path changes, changed finalized reports, or invalid replies stop
publication for reconciliation. The last accepted snapshot remains. Never clear integrity
state, manually edit generated Markdown, or waive a gate to make an update appear successful.

This setup does not itself start a publisher or product run. A new full-roster run needs
fresh owner-authorized scope in a separate Vanguard session, after reloading customizations
and checking Chat Diagnostics. Requirement-level completion reconciliation remains separate
work; passing one invocation never completes a whole milestone. Readiness must be checked
against the real report readers, not inferred from generated headers or synthetic tests.

Focused checks are available without running Logger's product tests:

```powershell
& $node.FullName ../prophets-pipelines/conventions/scripts/Test-AgentDashboard.cjs
& $node.FullName ./ai-dashboard/Test-Dashboard.cjs --rollout
& $node.FullName ./ai-dashboard/Test-Dashboard.cjs --pilot
```

Run those commands from the repository root. The browser check uses only newly owned temporary
fixtures, starts/stops its own publisher, and does not create or change a real run. `--rollout`
checks all 28 role detail views and a full-mode update path; `--pilot` preserves the earlier mode.

Future repositories get their own `ai-dashboard/` folder and generated data. The renderer
and updater derive the project name and paths locally; generated Logger data must not be
copied into another project's dashboard.
