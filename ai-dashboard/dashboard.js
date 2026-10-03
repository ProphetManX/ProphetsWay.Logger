(() => {
    "use strict";
    const byId = (id) => document.getElementById(id);
    const views = new Set(["overview", "work", "requirements", "agents", "handoffs", "history"]);
    const labels = {
        complete: ["Completed", "circle-check",
            "The record reports its assigned scope complete. This does not establish completion of a broader feature, milestone, or project."],
        partial: ["Partial", "circle-help",
            "The recorded scope is unfinished. It may be idle or paused; this does not mean an agent is working or identify the active slice."],
        paused: ["Paused", "pause",
            "Work was recorded as paused or stopped at a budget limit. Unfinished work may remain; this is not a live activity indicator."],
        blocked: ["Blocked", "circle-alert",
            "The recorded work could not proceed because a required decision, input, prerequisite, or check was unresolved. A later record may resolve the blocker."],
        failed: ["Failed", "circle-x",
            "The record reports an execution or validation failure. A later recovery does not change this historical outcome."],
        changes: ["Changes requested", "message-square",
            "A review requested corrections within its scope. The review itself did not fail; a later report may have resolved its findings."],
        incomplete: ["Incomplete record", "clock-3",
            "The report was started but lacks a valid completion record. This is not proof that the agent is still working."],
        unknown: ["Unknown", "circle-help",
            "The importer could not establish a reliable status from the report metadata. Completion, failure, and current activity are not inferred."],
        unreconciled: ["Not reconciled", "circle-help",
            "Completion has not been mapped to evidence in this dashboard. The work may already be completed and verified; no missing or failed verification is implied."],
        pending: ["Pending", "clock-3",
            "The work is recorded as waiting to begin or be scheduled. No progress or active execution is implied."],
        working: ["Working", "loader-circle",
            "The latest status record says this work is in progress. This is a recorded snapshot, not a live agent heartbeat."],
        review: ["Pending review", "scan-eye",
            "The work is awaiting the required review. This label does not establish review approval or final completion."],
        discovery: ["TBD", "circle-help",
            "The remaining scope or future slice count is not yet determined. The current work list must not be treated as exhaustive."],
        idle: ["Idle", "pause",
            "No active work is reported for this item. Progress is separate: it may be pending, partly complete, or already complete."],
        stale: ["Update stale", "clock-3",
            "This working status has not been updated recently. The agent may still be working; this is not a live heartbeat or proof of failure."],
        requested: ["Awaiting reply", "message-circle",
            "A help or review request has been recorded, but no linked response is present. The request itself grants no execution or review approval."],
        answered: ["Reply recorded", "message-circle-check",
            "A linked response is present. This does not automatically close its findings, approve the work, or satisfy any required verification gate."]
    };
    const state = {
        data: window.AI_DASHBOARD_DATA,
        view: "overview", search: "", status: "all", run: "latest",
        archiveLoaded: false, archivePromise: null, reports: [], pilot: null, pilotLoading: false,
        milestoneExpansion: new Map()
    };
    const dateFormat = new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
    function date(value) {
        const parsed = new Date(value);
        return Number.isNaN(parsed.getTime()) ? "Date not recorded" : dateFormat.format(parsed);
    }
    function node(tag, className, text) {
        const result = document.createElement(tag);
        if (className) result.className = className;
        if (text !== undefined && text !== null) result.textContent = String(text);
        return result;
    }
    function icon(name) {
        const result = node("i");
        result.dataset.lucide = name;
        result.setAttribute("aria-hidden", "true");
        return result;
    }
    function paintIcons() { window.lucide?.createIcons({ attrs: { "stroke-width": 1.8 } }); }
    function badge(status, text) {
        const selected = labels[status] || labels.unknown;
        const result = node("span", `badge ${labels[status] ? status : "unknown"}`);
        result.title = text === "Recorded snapshot"
            ? "These statuses were imported from saved reports. They are not live telemetry or a fresh verification of the work."
            : selected[2];
        result.append(icon(selected[1]), node("span", "", text || selected[0]));
        return result;
    }
    function iconButton(name, title, action) {
        const button = node("button", "icon-button");
        button.type = "button";
        button.title = title;
        button.setAttribute("aria-label", title);
        button.append(icon(name));
        button.addEventListener("click", action);
        return button;
    }
    function sourceLink(source, title = "Source record") {
        if (!source || !/^(?:\.\.\/)*[\w.%/+-]+\.(?:md|json)(?:#L\d+)?$/.test(source)) return node("span", "muted", "Source unavailable");
        const link = node("a", "source-link", title);
        link.href = source;
        link.target = "_blank";
        link.rel = "noopener noreferrer";
        link.append(icon("arrow-up-right"));
        return link;
    }
    function section(title, caption, link) {
        const root = node("section", "section");
        const heading = node("div", "section-heading");
        const copy = node("div");
        copy.append(node("h2", "", title));
        if (caption) copy.append(node("p", "section-caption", caption));
        heading.append(copy);
        if (link) {
            const anchor = node("a", "section-link", link.text);
            anchor.href = `#${link.view}`;
            heading.append(anchor);
        }
        root.append(heading);
        return root;
    }
    function empty(title = "No matches", message = "No records match these filters.") {
        const result = node("div", "empty");
        result.append(icon("search"), node("h2", "", title), node("p", "", message));
        return result;
    }
    function notice(title, message, kind = "warning") {
        const result = node("div", `notice ${kind}`);
        const copy = node("div");
        copy.append(node("strong", "", title), node("p", "", message));
        result.append(icon(kind === "warning" ? "circle-help" : "info"), copy);
        return result;
    }
    function latestRun() { return state.data.runs[0]; }
    function matches(text, status) {
        return String(text).toLowerCase().includes(state.search.toLowerCase()) && (state.status === "all" || state.status === status);
    }
    function orderReports(records) { return [...records].sort((left, right) => right.runId.localeCompare(left.runId) || right.sequence - left.sequence); }
    function selectedReports() {
        return orderReports(state.reports.filter((report) => state.run === "all" || report.runId === (state.run === "latest" ? latestRun()?.id : state.run)));
    }
    function setCount(count, noun) { byId("result-count").textContent = `${count} ${noun}`; }
    function table(headers) {
        const wrap = node("div", "table-wrap");
        const grid = node("table");
        const head = node("thead");
        const row = node("tr");
        headers.forEach((header) => {
            const cell = node("th", header.className || "", header.text || header);
            cell.scope = "col";
            row.append(cell);
        });
        head.append(row);
        const body = node("tbody");
        grid.append(head, body);
        wrap.append(grid);
        return { wrap, body };
    }
    function reportTable(records) {
        const { wrap, body } = table(["Task", { text: "Agent", className: "role-column" }, "Recorded outcome"]);
        records.forEach((report) => {
            const row = node("tr");
            const subject = node("td");
            const button = node("button", "table-title", report.title);
            button.type = "button";
            button.addEventListener("click", () => showReport(report));
            subject.append(button, node("span", "table-secondary mono", `#${report.sequence} / ${report.runId}`));
            const status = node("td", "outcome-cell");
            status.append(badge(report.status));
            row.append(subject, node("td", "role-column", report.role), status);
            body.append(row);
        });
        return wrap;
    }
    function metric(title, value, note, symbol, color) {
        const result = node("div", `metric ${color}`);
        const label = node("div", "metric-label");
        label.append(icon(symbol), node("span", "", title));
        result.append(label, node("div", "metric-value", value), node("p", "metric-note", note));
        return result;
    }
    function reportProgress(records) {
        const root = node("div", "progress-block");
        const completed = records.filter((report) => report.status === "complete").length;
        const heading = node("div", "progress-heading");
        heading.append(node("span", "", "Successful report outcomes"), node("strong", "", `${completed} / ${records.length}`));
        const track = node("div", "progress-track");
        track.setAttribute("role", "progressbar");
        track.setAttribute("aria-label", "Successful invocation reports, not project completion");
        track.setAttribute("aria-valuemin", "0");
        track.setAttribute("aria-valuemax", String(Math.max(1, records.length)));
        track.setAttribute("aria-valuenow", String(completed));
        const counts = new Map();
        records.forEach((report) => counts.set(report.status, (counts.get(report.status) || 0) + 1));
        [...counts].sort(([left], [right]) => (left === "complete" ? -1 : right === "complete" ? 1 : left.localeCompare(right))).forEach(([status, count]) => {
            const segment = node("span", `progress-segment ${status}`);
            segment.style.width = `${count / Math.max(1, records.length) * 100}%`;
            segment.title = `${labels[status]?.[0] || status}: ${count}`;
            track.append(segment);
        });
        const legend = node("div", "legend");
        const good = node("span");
        good.append(node("span", "legend-dot"), node("span", "", `${completed} completed`));
        const other = node("span");
        other.append(node("span", "legend-dot other"), node("span", "", `${records.length - completed} other outcomes`));
        legend.append(good, other, node("span", "", "Not a feature-completion percentage"));
        root.append(heading, track, legend);
        return root;
    }
    function renderOverview() {
        const run = latestRun();
        const fragment = document.createDocumentFragment();
        if (state.pilot) fragment.append(renderPilotActivity());
        if (!run) return empty("No confirmed runs", "The import has not identified a run belonging to this repository.");
        const recent = orderReports(state.reports.filter((report) => report.runId === run.id));
        const done = state.data.checkpoints.filter((checkpoint) => checkpoint.runId === run.id);
        const band = node("section", "session-band");
        const copy = node("div", "session-copy");
        copy.append(node("h2", "", run.title), node("p", "", run.assignment || run.summary || "Assignment status not recorded."));
        band.append(icon("pause"), copy, sourceLink(run.source));
        fragment.append(band);
        const focus = state.data.requirements.stages.filter((stage) => stage.status === "partial");
        const metrics = node("div", "metrics");
        metrics.append(
            metric("Completed components", done.length, "recorded checkpoints in latest window", "circle-check", "teal"),
            metric("Invocation reports", recent.length, `${run.completedReports} recorded successful outcomes`, "file-text", "blue"),
            metric("Remaining focus", focus.map((stage) => stage.id).join(" + ") || "TBD", "full milestone completion still pending", "flag", "amber"),
            metric("History coverage", `${state.data.runs.length} / ${state.data.candidateRuns}`, `${state.data.totalReports} reports imported`, "archive", "")
        );
        fragment.append(metrics);
        const columns = node("div", "overview-grid");
        const left = node("div");
        const completed = section("Completed in the latest window", "Recorded component completion, not whole-project sign-off.", { text: "All runs", view: "history" });
        done.forEach((checkpoint) => {
            const row = node("article", "completion");
            const mark = node("span", "completion-mark");
            mark.append(icon("check"));
            const description = node("div");
            description.append(node("h3", "", checkpoint.title), node("p", "mono", checkpoint.id), node("p", "", "Local checkpoint recorded complete. No publication is inferred."));
            row.append(mark, description, iconButton("arrow-up-right", `Inspect ${checkpoint.title}`, () => showCheckpoint(checkpoint)));
            completed.append(row);
        });
        if (!done.length) completed.append(empty("No completed checkpoint recorded", "Other scoped work may have finished; see the run's reports."));
        left.append(completed);
        const handoffs = section("Recent agent handoffs", "Latest recorded tasks and their own scoped outcomes.", { text: "Agent activity", view: "agents" });
        handoffs.append(reportTable(recent.slice(0, 6)));
        left.append(handoffs);
        const right = node("div");
        const progress = section("Latest window", `Source record updated ${date(run.updatedAt)}.`);
        progress.append(reportProgress(recent));
        const owner = node("div", "focus-row");
        const ownerText = node("div");
        ownerText.append(node("h3", "", "Reported current owner"), node("p", "", run.currentOwner || "Not recorded"));
        owner.append(ownerText);
        progress.append(owner);
        right.append(progress);
        const next = section("Still ahead", "Defined work and the limits of the current forecast.", { text: "Work plan", view: "work" });
        focus.forEach((stage) => {
            const row = node("div", "focus-row");
            const text = node("div");
            text.append(node("h3", "", `${stage.id} / ${stage.title}`), node("p", "", "Latest assignment records this milestone as unfinished."));
            row.append(text, badge("partial"));
            next.append(row);
        });
        const discovery = node("div", "discovery-note");
        const text = node("div");
        text.append(node("h3", "", "Future slice count: TBD"), node("p", "", state.data.discovery));
        discovery.append(icon("circle-help"), text);
        next.append(discovery);
        right.append(next);
        right.append(notice("Requirement completion is not reconciled", `${state.data.requirements.items.length} definitions are imported. A passing agent report does not mark an entire requirement complete.`, "info"));
        columns.append(left, right);
        fragment.append(columns);
        return fragment;
    }
    function renderWork() {
        const fragment = document.createDocumentFragment();
        fragment.append(renderPilotPlan());
        fragment.append(notice("Future slices remain TBD", state.data.discovery));
        const stages = state.data.requirements.stages.filter((stage) => matches(`${stage.id} ${stage.title} ${stage.scope}`, stage.status));
        setCount(stages.length, "stages");
        const plan = section("Defined delivery stages", "Completion boundaries from the project requirements; evidence status is separate.");
        const list = node("div", "stage-list");
        stages.forEach((stage) => {
            const row = node("article", "stage");
            const text = node("div");
            text.append(node("h3", "", stage.title), node("p", "", stage.scope), sourceLink(stage.source, "Completion boundary"));
            row.append(node("span", "stage-id", stage.id), text, badge(stage.status));
            list.append(row);
        });
        plan.append(stages.length ? list : empty());
        fragment.append(plan);
        const remaining = section("Remaining work recorded at closeout", "Work areas, not a complete or newly approved slice schedule.");
        const remainingList = node("ul", "remaining-list");
        const remainingText = latestRun()?.remaining;
        if (remainingText) {
            remainingText.split(/;\s*/).filter(Boolean).forEach((part) => {
                const item = node("li");
                item.append(icon("corner-down-right"), node("span", "", part.trim()));
                remainingList.append(item);
            });
            remaining.append(remainingList, sourceLink(latestRun().source));
        } else remaining.append(empty("Remaining work not extracted", "The source run summary remains available; no empty backlog is inferred."));
        fragment.append(remaining);
        const targets = section("Targets used in the latest run", "Recorded acceptance targets; their presence alone does not establish completion.");
        const rows = node("div", "targets");
        (latestRun()?.slices || []).forEach((slice) => {
            const row = node("div", "target-row");
            row.append(node("span", "", slice.title), sourceLink(slice.source, "Target"));
            rows.append(row);
        });
        targets.append(rows);
        fragment.append(targets);
        return fragment;
    }
    function renderRequirements() {
        const fragment = document.createDocumentFragment();
        fragment.append(notice("Definitions are not completion evidence", state.data.requirements.disposition || "Requirement completion has not been reconciled against authoritative review and execution records.", "info"));
        const items = state.data.requirements.items.filter((item) => matches(`${item.id} ${item.title} ${item.description}`, item.status));
        setCount(items.length, "requirements");
        const { wrap, body } = table(["ID", "Requirement", "Criteria", "Evidence status"]);
        items.forEach((item) => {
            const row = node("tr");
            const subject = node("td");
            const button = node("button", "table-title", item.title);
            button.type = "button";
            button.addEventListener("click", () => showRequirement(item));
            subject.append(button);
            const status = node("td", "outcome-cell");
            status.append(badge(item.status));
            row.append(node("td", "id-cell", item.id), subject, node("td", "mono", item.criteria.length), status);
            body.append(row);
        });
        fragment.append(items.length ? wrap : empty());
        return fragment;
    }
    function renderAgents() {
        const fragment = document.createDocumentFragment();
        fragment.append(renderPilotActivity());
        fragment.append(notice("Imported historical activity", `The latest imported run is ${labels[latestRun()?.status]?.[0].toLowerCase() || "of unknown status"}. These older reports are separate from the current reporting records above; STARTED alone does not prove an agent is still working.`, "info"));
        const groups = new Map();
        selectedReports().forEach((report) => {
            if (!groups.has(report.role)) groups.set(report.role, []);
            groups.get(report.role).push(report);
        });
        const selected = [...groups].filter(([role, records]) => matches(`${role} ${records[0].title} ${records[0].objective}`, records[0].status));
        setCount(selected.length, "agent roles");
        const grid = node("div", "agent-grid");
        selected.forEach(([role, records]) => {
            const report = records[0];
            const card = node("article", "agent-card");
            const heading = node("div", "agent-card-heading");
            const mark = node("span", "agent-icon");
            mark.append(icon(role.includes("Reviewer") || role.includes("Auditor") ? "scan-eye" : "user-round"));
            const title = node("div");
            title.append(node("h2", "", role), node("p", "agent-meta", report.roleInferred ? "Role inferred from record title" : "Explicit report identity"));
            heading.append(mark, title);
            card.append(heading, badge(report.status), node("p", "agent-task", report.title));
            card.append(node("p", "agent-summary", report.objective || report.target || "No separate task objective recorded."));
            const previous = node("div", "agent-previous");
            previous.append(node("span", "agent-meta", "PREVIOUS RECORDED TASK"), node("p", "", records[1]?.title || "No earlier task in this selection."));
            card.append(previous);
            const bottom = node("div", "agent-bottom");
            bottom.append(node("span", "", `#${report.sequence} / ${date(report.updatedAt)}`), iconButton("arrow-up-right", `Inspect ${role} task`, () => showReport(report, records)));
            card.append(bottom);
            grid.append(card);
        });
        fragment.append(selected.length ? grid : empty());
        return fragment;
    }

    function activityBadge(activity, updatedAt) {
        const stale = activity === "working" && updatedAt && Date.now() - Date.parse(updatedAt) > 5 * 60 * 1000;
        const result = badge(stale ? "stale" : activity === "waiting-review" ? "review" : activity || "idle");
        if (activity === "working" && updatedAt) {
            result.dataset.pilotActivity = activity;
            result.dataset.pilotUpdated = updatedAt;
        }
        return result;
    }
    function renderPilotPlan() {
        const root = section("Milestone slices", "Persistent slice identities and Vanguard's recorded forecast.");
        if (!state.pilot?.milestones.length) {
            root.append(empty("Slice forecast not published", "No structured milestone-by-milestone breakdown has been published. Historical targets remain below; additional slices are still TBD."));
            return root;
        }
        state.pilot.milestones.forEach((milestone) => {
            if (!matches(`${milestone.id} ${milestone.title}`, milestone.progress)) return;
            const slices = state.pilot.slices.filter((slice) => slice.milestoneId === milestone.id);
            const group = node("details", "milestone-slices");
            group.open = state.milestoneExpansion.get(milestone.id) ?? slices.some((slice) => slice.activity !== "idle");
            group.addEventListener("toggle", () => state.milestoneExpansion.set(milestone.id, group.open));
            const heading = node("summary", "milestone-summary");
            const name = node("div", "milestone-name");
            name.append(icon("chevron-right"), node("strong", "", `${milestone.id} / ${milestone.title}`));
            const forecast = milestone.forecastTotal === null ? `${slices.length} known / total TBD` : `${slices.length} known / ${milestone.forecastTotal} ${milestone.breakdownComplete ? "in complete breakdown" : "forecast"}`;
            heading.append(name, node("span", "mono muted", forecast), badge(milestone.progress), activityBadge(milestone.activity, state.pilot.projectUpdatedAt));
            group.append(heading);
            if (!milestone.breakdownComplete) group.append(node("p", "slice-remainder", "Unplanned remainder: TBD. Further slices may be needed."));
            for (const slice of slices) {
                const row = node("div", "slice-row");
                const subject = node("div");
                const button = node("button", "table-title", slice.title);
                button.type = "button";
                button.addEventListener("click", () => showSlice(slice));
                subject.append(button, node("span", "table-secondary mono", `${slice.id}${slice.forecast ? " / tentative forecast" : ""}`));
                const states = node("div", "slice-states");
                states.append(badge(slice.progress), activityBadge(slice.activity, state.pilot.projectUpdatedAt));
                row.append(subject, node("span", "muted", slice.owner || "Unassigned"), states);
                group.append(row);
            }
            if (!slices.length) group.append(node("p", "slice-remainder", "No slices enumerated yet."));
            root.append(group);
        });
        root.append(sourceLink(state.pilot.projectSource, "Vanguard's project ledger"));
        return root;
    }
    function showSlice(slice) {
        const root = openDetail(`${state.pilot.mode === "dashboard-v1" ? "PROJECT" : "PILOT"} SLICE / ${slice.id}`, slice.title);
        const statuses = node("div", "detail-meta");
        statuses.append(badge(slice.progress), activityBadge(slice.activity, state.pilot.projectUpdatedAt));
        root.append(statuses, detailSection("Owner", slice.owner || "Unassigned"), detailSection("Requirements", slice.requirements.join(" / ") || "Not mapped yet."),
            detailSection("Dependencies", slice.dependencies.join(" / ") || "None recorded."), detailSection("Evidence references", slice.evidence.join("\n") || "No completion evidence recorded."));
        if (slice.forecast) root.append(notice("Tentative forecast", "This item is planning, not an authorized or started invocation."));
        const records = state.pilot.invocations.filter((record) => record.sliceId === slice.id);
        const list = section("Agent invocations", "Attempts and handoffs do not create additional completed slices.");
        records.forEach((record) => list.append(pilotRecordRow(record)));
        root.append(list, sourceLink(state.pilot.projectSource, "Project ledger"));
        paintIcons();
    }
    function pilotRecordRow(record) {
        const row = node("div", "detail-report-row");
        const subject = node("div");
        const button = node("button", "table-title", record.objective || `${record.agent}: registered`);
        button.type = "button";
        button.addEventListener("click", () => showPilotRecord(record));
        subject.append(button, node("span", "table-secondary mono", `${record.invocationId || record.id} / ${record.agent}`));
        row.append(subject, activityBadge(record.activity, record.updatedAt));
        return row;
    }
    function renderPilotActivity() {
        const fullRoster = state.pilot?.mode === "dashboard-v1";
        const root = section(fullRoster ? "Current agent reporting" : "Current reporting pilot",
            fullRoster ? "Vanguard and its registered project-run agents; each invocation owns its own record."
                : "Vanguard, Implementer and Code Reviewer; other agents retain their existing reports.", { text: "Help & reviews", view: "handoffs" });
        if (!state.pilot || (!state.pilot.invocations.length && !state.pilot.archivedInvocations.length)) {
            root.append(empty("No reporting invocation recorded", "The reporting integration is available, but no new task record has been published."));
            return root;
        }
        state.pilot.invocations.forEach((record) => root.append(pilotRecordRow(record)));
        if (state.pilot.archivedInvocations.length) {
            const archive = node("details", "pilot-archive");
            archive.append(node("summary", "", `${state.pilot.archivedInvocations.length} older finalized records`));
            state.pilot.archivedInvocations.forEach((record) => archive.append(pilotRecordRow(record)));
            root.append(archive);
        }
        return root;
    }
    function showPilotRecord(record) {
        const root = openDetail(`CANONICAL AGENT RECORD / ${record.invocationId || record.id}`, record.objective || record.agent);
        root.append(activityBadge(record.activity, record.updatedAt), detailSection("Recorded outcome", [record.state, record.outcome, record.reason, record.continuation].filter(Boolean).join(" / ")),
            detailSection("Planned scope / check", record.scope ? `Scope decision: ${record.scopeDecision}\nIncluded: ${record.scope.included.join("; ")}\nExcluded: ${record.scope.excluded.join("; ")}\nCheck: ${record.plannedCheck}` : ""),
            detailSection("Summary", record.summary), detailSection("Report", record.body || (record.archived ? "Archived detail is retained in the canonical JSON and compatibility report linked below." : "No finalized report body yet.")), detailSection("Evidence references", (record.evidence || []).join("\n")));
        const links = node("div", "detail-meta");
        links.append(sourceLink(record.source, "Canonical JSON"), sourceLink(record.report, "Compatibility report"));
        root.append(links);
        for (const exchange of state.pilot.communications.filter((entry) => entry.invocationId === record.invocationId)) {
            const button = node("button", "exchange-link", `${exchange.kind}: ${exchange.subject}`);
            button.type = "button";
            button.addEventListener("click", () => showExchange(exchange));
            root.append(button);
        }
        paintIcons();
    }
    function renderHandoffs() {
        const fragment = document.createDocumentFragment();
        fragment.append(notice("Agent communication record", "A recorded reply is not automatically an accepted review or a resolved finding.", "info"));
        const exchanges = state.pilot?.communications || [];
        const requests = exchanges.filter((entry) => entry.kind !== "reply").map((request) => ({ ...request,
            replies: exchanges.filter((entry) => entry.replyTo === request.id) }));
        const selected = requests.filter((request) => matches(`${request.subject} ${request.from} ${request.to} ${request.scope.join(" ")} ${request.concern}`, request.replies.length ? "answered" : "requested"));
        setCount(selected.length, "requests");
        if (!selected.length) {
            fragment.append(empty(exchanges.length ? "No matching requests" : "No recorded exchanges yet", "Only explicit structured requests and linked replies appear here. Historical report excerpts remain available under Agents and Run history."));
            return fragment;
        }
        const grid = table(["Request", "From / to", "Status"]);
        for (const request of selected) {
            const row = node("tr");
            const subject = node("td");
            const button = node("button", "table-title", request.subject);
            button.type = "button";
            button.addEventListener("click", () => showExchange(request));
            subject.append(button, node("span", "table-secondary mono", `${request.sliceId} / ${request.id}`));
            const status = node("td", "outcome-cell");
            status.append(badge(request.replies.length ? "answered" : "requested"));
            row.append(subject, node("td", "", `${request.from} > ${request.to}`), status);
            grid.body.append(row);
        }
        fragment.append(grid.wrap);
        return fragment;
    }
    function showExchange(exchange) {
        const request = exchange.kind === "reply" ? state.pilot.communications.find((entry) => entry.id === exchange.replyTo) : exchange;
        if (!request) return;
        const root = openDetail(`${request.kind.toUpperCase()} / ${request.id}`, request.subject);
        root.append(detailSection("Routing", `${request.from} > ${request.to}\nSlice: ${request.sliceId}`), detailSection("Requested scope", request.scope.join("\n")),
            detailSection("What changed / previous review", request.delta), detailSection("Why this needs attention", request.concern), detailSection("Request", request.message),
            detailSection("Evidence references", request.evidence.join("\n")), sourceLink(request.report, "Requester's report"));
        const replies = state.pilot.communications.filter((entry) => entry.replyTo === request.id);
        root.append(detailSection("Response state", replies.length ? "Reply recorded. Gate acceptance and finding resolution remain separate." : "No linked response recorded."));
        for (const reply of replies) {
            root.append(detailSection(`Reply from ${reply.from}`, reply.message), detailSection("Reply evidence", reply.evidence.join("\n")), sourceLink(reply.report, "Responder's report"));
        }
        paintIcons();
    }
    function acceptPilot() {
        const candidate = window.AI_DASHBOARD_PILOT;
        if (!candidate) return;
        if (candidate.schemaVersion !== 1 || candidate.projectId !== state.data?.project || !["milestones", "slices", "invocations", "archivedInvocations", "communications"].every((key) => Array.isArray(candidate[key]))) {
            showNotification("Reporting data does not match this project or supported format. The previous accepted view is retained.");
            return;
        }
        const changed = state.pilot?.publishedAt !== candidate.publishedAt;
        state.pilot = candidate;
        byId("pilot-time").textContent = `${candidate.mode === "dashboard-v1" ? "Reporting" : "Pilot"} published ${date(candidate.publishedAt)}`;
        byId("pilot-time").title = "Last file publication, not an agent heartbeat. Working records older than five minutes are marked stale.";
        if (changed) render();
    }
    function refreshPilot() {
        if (document.hidden || state.pilotLoading) return;
        state.pilotLoading = true;
        const script = document.createElement("script");
        script.src = `data/pilot-data.js?refresh=${Date.now()}`;
        script.onload = () => { state.pilotLoading = false; script.remove(); acceptPilot(); };
        script.onerror = () => { state.pilotLoading = false; script.remove(); byId("pilot-time").textContent = "Reporting publication unavailable"; };
        document.head.append(script);
    }
    function renderHistory() {
        const fragment = document.createDocumentFragment();
        fragment.append(notice("Historical outcomes are preserved", `${state.data.archiveCount} older invocation records are in the dashboard archive. Original reports and failed checks have not been rewritten or deleted.`, "info"));
        const runs = state.data.runs.filter((run) => matches(`${run.title} ${run.id} ${run.assignment}`, run.status));
        setCount(runs.length, "runs");
        const { wrap, body } = table(["Work window", "Report outcomes", "Run status"]);
        runs.forEach((run) => {
            const row = node("tr");
            const subject = node("td");
            const button = node("button", "table-title", run.title);
            button.type = "button";
            button.addEventListener("click", () => showRun(run));
            subject.append(button, node("span", "table-secondary mono", run.id));
            const counts = node("td");
            counts.append(node("strong", "", `${run.completedReports} / ${run.reports}`), node("span", "table-secondary", "successful reports, not slices"));
            const status = node("td", "outcome-cell");
            status.append(badge(run.status));
            row.append(subject, counts, status);
            body.append(row);
        });
        fragment.append(runs.length ? wrap : empty());
        const coverage = node("details", "coverage");
        coverage.id = "import-coverage";
        coverage.append(node("summary", "", `Import coverage: ${state.data.runs.length} of ${state.data.candidateRuns} candidate folders`));
        coverage.append(node("p", "", "Candidate names match this repository's token. Import also requires an explicit repository declaration. Unsupported or missing records remain unclassified, not successful."));
        const warnings = node("ul");
        state.data.warnings.forEach((warning) => warnings.append(node("li", "", warning)));
        coverage.append(warnings);
        fragment.append(coverage);
        return fragment;
    }
    function detailSection(title, text) {
        if (!text) return node("div");
        const result = node("section", "detail-section");
        result.append(node("h3", "", title), node("p", "detail-text", text));
        return result;
    }
    function openDetail(eyebrow, title) {
        byId("detail-eyebrow").textContent = eyebrow;
        byId("detail-title").textContent = title;
        byId("detail-content").replaceChildren();
        if (!byId("detail-dialog").open) byId("detail-dialog").showModal();
        byId("detail-content").scrollTop = 0;
        return byId("detail-content");
    }
    function showReport(report, related = []) {
        const root = openDetail(`INVOCATION #${report.sequence} / ${report.role}`, report.title);
        root.append(badge(report.status));
        const metadata = node("div", "detail-meta");
        metadata.append(node("span", "", `Record updated ${date(report.updatedAt)}`), sourceLink(report.source));
        root.append(metadata);
        root.append(notice("Scoped historical outcome", "A completed invocation is not whole-feature completion. A later report may resolve a finding or recover an incomplete return.", "info"));
        root.append(detailSection("Objective / target", [report.objective, report.target].filter(Boolean).join("\n")));
        root.append(detailSection("Recorded scope / findings", report.summary || "No concise scope or findings section was extracted. The original report is linked above."));
        root.append(detailSection("Verdict", report.verdict));
        root.append(detailSection("Next action / help requested", report.next || "Not separately captured in the imported fields."));
        root.append(detailSection("Protocol result", [report.outcome, report.reason, report.continuation].filter(Boolean).join(" / ")));
        if (related.length > 1) {
            const section = detailSection("Earlier tasks in this selection", "");
            section.append(node("h3", "", "Earlier tasks in this selection"));
            related.slice(1, 5).forEach((earlier) => {
                const row = node("div", "detail-report-row");
                const button = node("button", "table-title", `#${earlier.sequence} ${earlier.title}`);
                button.type = "button";
                button.addEventListener("click", () => showReport(earlier));
                row.append(button, badge(earlier.status));
                section.append(row);
            });
            root.append(section);
        }
        root.append(node("div", "detail-identity", `Source SHA-256 at import\n${report.sourceHash}`));
        paintIcons();
    }
    function showCheckpoint(checkpoint) {
        const root = openDetail("RECORDED COMPONENT CHECKPOINT", checkpoint.title);
        root.append(badge(checkpoint.status), detailSection("Run record", checkpoint.summary), sourceLink(checkpoint.source));
        root.append(detailSection("Boundary", "This is the run's recorded component disposition, not a new verification, a release, or completion of its entire milestone."));
        paintIcons();
    }
    function showRequirement(item) {
        const root = openDetail(`REQUIREMENT ${item.id}`, item.title);
        root.append(badge(item.status), detailSection("Definition", item.description), detailSection("Acceptance criterion IDs", item.criteria.join(" / ")));
        root.append(detailSection("Completion evidence", "Not yet reconciled at requirement level. No approval or completion is inferred from the definition or from a passing individual report."), sourceLink(item.source, "Full requirement and criteria"));
        paintIcons();
    }
    async function ensureArchive() {
        if (state.archiveLoaded || !state.data.archiveCount) return;
        if (state.archivePromise) return state.archivePromise;
        state.archivePromise = new Promise((resolve, reject) => {
            const script = document.createElement("script");
            script.src = `data/archive-data.js?generation=${encodeURIComponent(state.data.generation)}`;
            script.onload = () => {
                script.remove();
                if (window.AI_DASHBOARD_ARCHIVE?.generation !== state.data.generation) {
                    state.archivePromise = null;
                    reject(new Error("History and snapshot generations differ. Reload the published snapshot."));
                    return;
                }
                state.reports = [...state.data.reports, ...window.AI_DASHBOARD_ARCHIVE.reports];
                state.archiveLoaded = true;
                resolve();
            };
            script.onerror = () => {
                script.remove();
                state.archivePromise = null;
                reject(new Error("Archived detail is unavailable. The source reports have not been removed."));
            };
            document.head.append(script);
        });
        return state.archivePromise;
    }
    async function showRun(run) {
        let root = openDetail("RECORDED WORK WINDOW", run.title);
        root.append(badge(run.status), detailSection("Assignment", run.assignment), detailSection("Latest summary", run.summary), sourceLink(run.source));
        const detail = section("Invocation history", "Each result applies to its own recorded scope.");
        detail.append(node("p", "muted", "Loading recorded tasks..."));
        root.append(detail);
        paintIcons();
        try {
            await ensureArchive();
            if (byId("detail-title").textContent !== run.title) return;
            detail.lastChild.remove();
            const records = orderReports(state.reports.filter((report) => report.runId === run.id));
            detail.append(records.length ? reportTable(records) : empty("No invocation detail imported", "The run summary remains available."));
            paintIcons();
        } catch (error) { detail.lastChild.textContent = error.message; }
    }
    function configureFilters() {
        byId("filters").hidden = state.view === "overview";
        byId("run-filter-label").hidden = state.view !== "agents";
        const options = state.view === "requirements" ? ["unreconciled"] : state.view === "work" ? ["pending", "partial", "complete", "unreconciled"] : state.view === "handoffs" ? ["requested", "answered"] : ["complete", "paused", "partial", "blocked", "failed", "changes", "incomplete", "unknown"];
        byId("status-filter").replaceChildren(new Option("All statuses", "all"), ...options.map((status) => new Option(labels[status][0], status)));
        byId("search").placeholder = { work: "Search delivery stages", requirements: "Search requirements", agents: "Search agents and latest tasks", handoffs: "Search requests, agents and concerns", history: "Search work windows" }[state.view] || "Search";
        byId("run-filter").replaceChildren(new Option("Latest run", "latest"), new Option("All imported runs", "all"), ...state.data.runs.map((run) => new Option(run.id, run.id)));
        byId("search").value = state.search;
        byId("status-filter").value = state.status;
        byId("run-filter").value = state.run;
        byId("clear-search").hidden = !state.search;
    }
    function render() {
        const titles = { overview: state.data.project.replace(/^ProphetsWay\./, ""), work: "Work plan", requirements: "Requirements", agents: "Agent activity", handoffs: "Help & reviews", history: "Run history" };
        const subtitles = {
            overview: state.data.requirements.stages.filter((stage) => stage.status === "partial").map((stage) => stage.title).join(" / ") || "Project delivery status",
            work: "Completed components, defined stages, and work still to be scheduled.",
            requirements: `${state.data.requirements.items.length} defined requirements / completion evidence not yet reconciled`,
            agents: "Latest recorded assignment per role, with each invocation kept separate.",
            handoffs: "Scoped requests, linked replies, and the evidence behind each exchange.",
            history: `${state.data.runs.length} confirmed runs / ${state.data.totalReports} invocation reports`
        };
        byId("page-title").textContent = titles[state.view];
        byId("eyebrow").textContent = state.view === "overview" ? "PROJECT OVERVIEW" : state.data.project.toUpperCase();
        byId("page-subtitle").textContent = subtitles[state.view];
        byId("project-status").replaceChildren(badge(latestRun()?.status || "unknown", state.view === "overview" ? undefined : "Recorded snapshot"));
        const renderers = { overview: renderOverview, work: renderWork, requirements: renderRequirements, agents: renderAgents, handoffs: renderHandoffs, history: renderHistory };
        byId("content").replaceChildren(renderers[state.view]());
        document.querySelectorAll(".navigation [data-view]").forEach((link) => {
            if (link.dataset.view === state.view) link.setAttribute("aria-current", "page");
            else link.removeAttribute("aria-current");
        });
        byId("clear-search").hidden = !state.search;
        paintIcons();
    }
    function navigate() {
        const requested = location.hash.slice(1);
        state.view = views.has(requested) ? requested : "overview";
        state.search = "";
        state.status = "all";
        state.run = "latest";
        configureFilters();
        render();
    }
    function showNotification(message) {
        byId("notification").textContent = message;
        byId("notification").hidden = false;
    }
    byId("reload").addEventListener("click", () => location.reload());
    byId("close-dialog").addEventListener("click", () => byId("detail-dialog").close());
    byId("detail-dialog").addEventListener("click", (event) => {
        if (event.target === byId("detail-dialog")) {
            const bounds = event.target.getBoundingClientRect();
            if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) event.target.close();
        }
    });
    if (!state.data || state.data.schemaVersion !== 1) {
        byId("content").append(empty("Snapshot unavailable", "No supported dashboard data has been published for this checkout."));
        byId("page-subtitle").textContent = "Local project dashboard";
        paintIcons();
        return;
    }
    state.reports = [...state.data.reports];
    document.title = `${state.data.project} | Project Dashboard`;
    byId("sidebar-project").textContent = state.data.project;
    byId("breadcrumb-project").textContent = state.data.project.replace(/^ProphetsWay\./, "");
    byId("snapshot-time").textContent = `Snapshot ${date(state.data.generatedAt)}`;
    byId("snapshot-time").title = `Imported ${state.data.generatedAt}. This is not an agent heartbeat.`;
    byId("footer-counts").textContent = `${state.data.runs.length} runs / ${state.data.totalReports} reports / ${state.data.requirements.items.length} requirements`;
    byId("search").addEventListener("input", (event) => { state.search = event.target.value; render(); });
    byId("clear-search").addEventListener("click", () => { state.search = ""; byId("search").value = ""; render(); byId("search").focus(); });
    byId("status-filter").addEventListener("change", (event) => { state.status = event.target.value; render(); });
    byId("run-filter").addEventListener("change", async (event) => {
        state.run = event.target.value;
        try { if (state.run !== "latest") await ensureArchive(); render(); }
        catch (error) { showNotification(error.message); }
    });
    window.addEventListener("hashchange", navigate);
    window.addEventListener("ai-dashboard-pilot-updated", acceptPilot);
    navigate();
    acceptPilot();
    setInterval(() => {
        refreshPilot();
        document.querySelectorAll('[data-pilot-activity="working"]:not(.stale)').forEach((element) => {
            if (Date.now() - Date.parse(element.dataset.pilotUpdated) > 5 * 60 * 1000) {
                element.replaceWith(activityBadge("working", element.dataset.pilotUpdated));
            }
        });
        paintIcons();
    }, 15000);
})();