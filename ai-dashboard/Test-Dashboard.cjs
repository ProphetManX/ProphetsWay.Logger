"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { pathToFileURL } = require("node:url");
const toolsRoot = process.env.DASHBOARD_TEST_TOOLS || path.join(process.env.LOCALAPPDATA, "ProphetsWay", "DashboardTools", "packages", "node_modules");
const { chromium } = require(path.join(toolsRoot, "playwright"));

async function checkStatusTooltips(page) {
    await page.locator('[data-view="work"]').click();
    await page.waitForSelector(".stage");
    const unreconciled = page.locator(".stage .badge.unreconciled").first();
    await unreconciled.hover();
    assert.match(await unreconciled.getAttribute("title"), /may already be completed and verified/);
    const partial = page.locator(".stage .badge.partial").first();
    await partial.hover();
    assert.match(await partial.getAttribute("title"), /does not mean an agent is working/);
    assert.match(await page.locator("#project-status .badge").getAttribute("title"), /not live telemetry/);

    const statuses = ["complete", "partial", "paused", "blocked", "failed", "changes", "incomplete", "unknown", "unreconciled", "pending", "working", "review", "discovery", "idle", "stale", "requested", "answered", "unrecognized-status"];
    await page.evaluate((values) => {
        const definition = window.AI_DASHBOARD_DATA.requirements.items[0];
        window.AI_DASHBOARD_DATA.requirements.items = values.map((status, index) => ({
            ...definition, id: `tooltip-${index}`, title: `Tooltip fixture ${status}`, status
        }));
    }, statuses);
    await page.locator('[data-view="requirements"]').click();
    await page.waitForSelector("tbody tr");
    const badges = page.locator("tbody .badge");
    assert.equal(await badges.count(), statuses.length);
    const descriptions = await badges.evaluateAll((elements) => elements.map((element) => element.title));
    descriptions.forEach((description, index) => assert.ok(description.length > 40, `Missing definition for ${statuses[index]}`));
    assert.equal(new Set(descriptions.slice(0, -1)).size, statuses.length - 1);
    assert.equal(descriptions.at(-1), descriptions[statuses.indexOf("unknown")]);
}

async function checkPilotViews(page, projectId, mode = "dashboard-pilot-v1") {
    await page.evaluate(({ projectId, mode }) => {
        window.AI_DASHBOARD_PILOT = { schemaVersion: 1, mode, projectId, publishedAt: "2000-01-01T00:00:00Z", projectUpdatedAt: "2000-01-01T00:00:00Z",
            projectSource: "live/project.json", milestones: [], slices: [], invocations: [], archivedInvocations: [], communications: [] };
        window.dispatchEvent(new Event("ai-dashboard-pilot-updated"));
    }, { projectId, mode });
    await page.locator('[data-view="handoffs"]').click();
    await page.locator('[data-view="handoffs"][aria-current="page"]').waitFor();
    assert.ok((await page.locator("#content").textContent()).includes("No recorded exchanges yet"));
    const now = new Date().toISOString();
    const request = { id: "request-test", kind: "review-request", to: "Code Reviewer v2", from: "Implementer v2", replyTo: null,
        subject: "Synthetic boundary review", scope: ["Example.cs:ChangedBranch"], delta: "One changed branch; initial review.", concern: "Boundary handling", message: "Inspect the changed branch.", evidence: [], sliceId: "M5-test", invocationId: "author-test", report: "../docs/requirements.md" };
    const reply = { ...request, id: "reply-test", kind: "reply", from: "Code Reviewer v2", to: "Implementer v2", replyTo: request.id, message: "Synthetic response; no actual approval.", invocationId: "reviewer-test" };
    const fixture = { schemaVersion: 1, mode, projectId, publishedAt: now, projectUpdatedAt: now, projectSource: "live/project.json",
        milestones: [{ id: "M5", title: "Synthetic milestone", progress: "partial", activity: "idle", forecastTotal: null, breakdownComplete: false, evidence: [] }],
        slices: [{ id: "M5-test", milestoneId: "M5", title: "Synthetic slice", progress: "partial", activity: "idle", forecast: false, owner: "Implementer v2", dependencies: [], requirements: ["R-17"], evidence: [] }],
        invocations: [{ invocationId: "author-test", agent: "Implementer v2", sliceId: "M5-test", objective: "Synthetic implementation", state: "FINALIZED", activity: "idle", outcome: "COMPLETE", summary: "Fixture only", body: "No product action.", updatedAt: now, evidence: [], report: "../docs/requirements.md", source: "live/project.json" }],
        archivedInvocations: [{ invocationId: "archived-test", agent: "Implementer v2", sliceId: "M5-test", objective: "Archived synthetic task", state: "FINALIZED", activity: "idle", outcome: "COMPLETE", archived: true, report: "../docs/requirements.md", source: "live/project.json" }], communications: [request, reply] };
    await page.evaluate((pilot) => { window.AI_DASHBOARD_PILOT = pilot; window.dispatchEvent(new Event("ai-dashboard-pilot-updated")); }, fixture);
    await page.locator('[data-view="work"]').click();
    await page.waitForSelector(".milestone-slices");
    assert.ok((await page.locator(".milestone-summary").textContent()).includes("1 known / total TBD"));
    await page.locator(".milestone-summary").click();
    assert.ok(await page.locator(".slice-row").isVisible());
    assert.equal(await page.locator(".slice-row .badge.partial").textContent(), "Partial");
    assert.equal(await page.locator(".slice-row .badge.idle").textContent(), "Idle");
    await page.locator(".slice-row .table-title").click();
    assert.ok((await page.locator("#detail-content").textContent()).includes("R-17"));
    await page.keyboard.press("Escape");
    await page.locator('[data-view="agents"]').click();
    await page.locator(".pilot-archive summary").click();
    await page.locator(".pilot-archive .table-title").click();
    assert.ok((await page.locator("#detail-content").textContent()).includes("Archived detail is retained"));
    assert.equal(await page.locator("#detail-content a.source-link").count(), 2);
    await page.keyboard.press("Escape");
    await page.locator('[data-view="handoffs"]').click();
    assert.equal(await page.locator("tbody tr").count(), 1);
    assert.equal(await page.locator("tbody .badge.answered").textContent(), "Reply recorded");
    await page.locator("tbody .table-title").click();
    const details = await page.locator("#detail-content").textContent();
    assert.ok(details.includes("Boundary handling") && details.includes("Example.cs:ChangedBranch") && details.includes("Synthetic response; no actual approval."));
    await page.keyboard.press("Escape");
    await page.locator("#status-filter").selectOption("requested");
    assert.equal(await page.locator("tbody tr").count(), 0);
    await page.evaluate(() => {
        window.AI_DASHBOARD_PILOT = { ...window.AI_DASHBOARD_PILOT, projectId: "WrongProject" };
        window.dispatchEvent(new Event("ai-dashboard-pilot-updated"));
    });
    assert.ok((await page.locator("#notification").textContent()).includes("does not match this project"));
}

async function checkRosterViews(page, projectId) {
    const schemaPath = path.resolve(__dirname, "..", "..", "prophets-pipelines", "conventions", "scripts", "agent-dashboard.schema.json");
    const agents = JSON.parse(fs.readFileSync(schemaPath, "utf8")).$defs.projectAgent.enum;
    await page.reload();
    await page.locator('[data-view="overview"]').click();
    await page.waitForSelector(".metrics");
    await page.evaluate(({ projectId, agents }) => {
        const now = new Date().toISOString();
        window.AI_DASHBOARD_PILOT = { schemaVersion: 1, mode: "dashboard-v1", projectId, publishedAt: now, projectUpdatedAt: now,
            projectSource: "live/project.json", milestones: [], slices: [], archivedInvocations: [], communications: [],
            invocations: agents.map((agent, index) => ({ invocationId: `all-roles-${index}`, agent, runId: "synthetic-rollout", sliceId: "synthetic-slice",
                objective: `${agent} synthetic report`, state: "FINALIZED", activity: "idle", outcome: "COMPLETE", reason: "NONE", continuation: "CONTINUE",
                updatedAt: now, summary: "Synthetic UI verification only", body: `Role-specific report preserved for ${agent}. No real agent action.`, evidence: [] })) };
        window.dispatchEvent(new Event("ai-dashboard-pilot-updated"));
    }, { projectId, agents });
    await page.locator('[data-view="agents"]').click();
    const reporting = page.locator("section").filter({ has: page.getByRole("heading", { name: "Current agent reporting", exact: true }) });
    await reporting.locator("h2").waitFor();
    assert.equal(await reporting.locator(".detail-report-row").count(), agents.length);
    assert.ok(!(await reporting.textContent()).includes("other agents retain their existing reports"));
    for (const agent of agents) {
        await reporting.getByRole("button", { name: `${agent} synthetic report`, exact: true }).click();
        assert.ok((await page.locator("#detail-content").textContent()).includes(`Role-specific report preserved for ${agent}`));
        await page.locator("#close-dialog").click();
    }
}

function waitForPublisher(child, phrase) {
    return new Promise((resolve, reject) => {
        let output = "";
        const cleanup = () => { clearTimeout(timer); child.stdout.off("data", onData); child.off("exit", onExit); child.off("error", onError); };
        const onData = (chunk) => { output += chunk.toString(); if (output.includes(phrase)) { cleanup(); resolve(); } };
        const onExit = (code) => { cleanup(); reject(new Error(`Publisher exited before ${phrase}: ${code}`)); };
        const onError = (error) => { cleanup(); reject(error); };
        const timer = setTimeout(() => { cleanup(); reject(new Error(`Publisher did not report ${phrase}.`)); }, 20000);
        child.stdout.on("data", onData);
        child.once("exit", onExit);
        child.once("error", onError);
    });
}

async function checkLivePublication(browser, data, screenshots, reportingMode = "dashboard-pilot-v1") {
    const os = require("node:os");
    const { spawn } = require("node:child_process");
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "logger-dashboard-browser-"));
    const repository = path.join(root, data.project);
    const dashboard = path.join(repository, "ai-dashboard");
    const run = path.join(root, ".agent-runs", "browser-pilot");
    const canonical = path.join(run, "author.json");
    const report = path.join(run, "author.md");
    const ledger = path.join(dashboard, "live", "project.json");
    const write = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + "\n");
    let child;
    let page;
    let stderr = "";
    try {
        fs.mkdirSync(path.join(dashboard, "live"), { recursive: true });
        fs.mkdirSync(path.join(dashboard, "data"), { recursive: true });
        fs.mkdirSync(run, { recursive: true });
        for (const name of ["statusreport.html", "dashboard.js", "dashboard.css", "lucide.min.js"]) fs.copyFileSync(path.join(__dirname, name), path.join(dashboard, name));
        for (const name of ["dashboard-data.js", "archive-data.js"]) fs.copyFileSync(path.join(__dirname, "data", name), path.join(dashboard, "data", name));
        fs.writeFileSync(path.join(run, "authority.md"), "Synthetic publisher fixture only.\n");
        const now = new Date().toISOString();
        const author = reportingMode === "dashboard-v1" ? "Test Designer v2" : "Implementer v2";
        const reviewer = reportingMode === "dashboard-v1" ? "Test Auditor v2" : "Code Reviewer v2";
        const record = { schemaVersion: 1, invocationId: "author-live", agent: author, runId: "browser-pilot", sliceId: "M5-live", targetRevision: "fixture-r1",
            scopeDecision: "PROCEED", scope: { included: ["Synthetic dashboard fixture"], excluded: ["Product operations"] }, plannedCheck: "Synthetic browser publication check",
            state: "STARTED", updatedAt: now, activity: "working", objective: "Synthetic watched task", summary: "Initial status", outcome: null, reason: null,
            continuation: null, verdict: null, body: "", evidence: [], communications: [] };
        const project = { schemaVersion: 1, reportingMode, projectId: data.project, repositoryRoot: repository, updatedAt: now,
            milestones: [{ id: "M5", title: "Synthetic watched milestone", progress: "partial", activity: "working", forecastTotal: 1, breakdownComplete: true, evidence: [] }],
            slices: [{ id: "M5-live", milestoneId: "M5", title: "Synthetic watched slice", progress: "partial", activity: "working", forecast: false, owner: author, dependencies: [], requirements: [], evidence: [] }],
            runs: [{ id: "browser-pilot", directory: run, authority: path.join(run, "authority.md") }],
            invocations: [{ id: "author-live", agent: author, runId: "browser-pilot", sliceId: "M5-live", targetRevision: "fixture-r1", recordPath: canonical, reportPath: report }] };
        write(ledger, project);
        write(canonical, record);
        const publisher = path.resolve(__dirname, "..", "..", "prophets-pipelines", "conventions", "scripts", "AgentDashboard.cjs");
        child = spawn(process.execPath, [publisher, "--project", ledger, "--watch"], { stdio: ["ignore", "pipe", "pipe"] });
        child.stderr.on("data", (chunk) => { stderr += chunk.toString(); });
        await waitForPublisher(child, "Watching registered canonical records only");
        page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
        const errors = [];
        page.on("pageerror", (error) => errors.push(error.message));
        await page.clock.install();
        await page.goto(pathToFileURL(path.join(dashboard, "statusreport.html")).href + "#work");
        await page.waitForSelector(".milestone-slices");
        assert.ok(await page.locator(".slice-row").isVisible());
        await page.locator(".milestone-summary").click();
        await page.clock.fastForward(15001);
        assert.ok(!await page.locator(".slice-row").isVisible(), "Automatic refresh must not reopen collapsed milestones.");

        Object.assign(record, { state: "FINALIZED", updatedAt: new Date().toISOString(), activity: "idle", outcome: "COMPLETE", reason: "NONE", continuation: "CONTINUE",
            summary: "Published automatically from the canonical record", body: "Synthetic completion only; no product action." });
        record.communications.push({ id: "watched-request", kind: "review-request", to: reviewer, replyTo: null, subject: "Watched review request",
            scope: ["Synthetic.cs:ChangedBranch"], delta: "One changed branch; initial review", concern: "Boundary behavior", message: "Synthetic request only", evidence: [] });
        const published = waitForPublisher(child, "Published");
        write(canonical, record);
        await published;
        assert.match(fs.readFileSync(report, "utf8"), /Outcome: COMPLETE/);
        await page.clock.fastForward(15001);
        await page.waitForFunction(() => window.AI_DASHBOARD_PILOT?.invocations[0]?.state === "FINALIZED");
        await page.locator('[data-view="handoffs"]').click();
        await page.waitForSelector("tbody .table-title");
        assert.equal(await page.locator("tbody .table-title").textContent(), "Watched review request");
        assert.equal(await page.locator("tbody .badge.requested").textContent(), "Awaiting reply");
        await page.locator("tbody .table-title").click();
        assert.ok((await page.locator("#detail-content").textContent()).includes("Boundary behavior"));
        assert.equal(await page.evaluate(() => window.AI_DASHBOARD_PILOT.mode), reportingMode);
        assert.ok((await page.locator("#detail-content").textContent()).includes(`${author} > ${reviewer}`));
        await page.screenshot({ path: path.join(screenshots, reportingMode === "dashboard-v1" ? "desktop-rollout-exchange.png" : "desktop-pilot-exchange.png"), fullPage: true, animations: "disabled" });
        assert.deepEqual(errors, []);
        assert.equal(stderr, "");
    } finally {
        if (page) await page.close();
        if (child && child.exitCode === null && child.signalCode === null) {
            await new Promise((resolve) => { child.once("exit", resolve); child.kill("SIGTERM"); });
        }
        fs.rmSync(root, { recursive: true, force: true });
    }
}

async function main() {
    const context = { window: {} };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, "data", "dashboard-data.js"), "utf8"), context);
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, "data", "archive-data.js"), "utf8"), context);
    const data = context.window.AI_DASHBOARD_DATA;
    const archive = context.window.AI_DASHBOARD_ARCHIVE;
    assert.equal(data.generation, archive.generation);
    assert.equal(data.reports.length + archive.reports.length, data.totalReports);
    assert.equal(new Set([...data.reports, ...archive.reports].map((report) => report.id)).size, data.totalReports);
    const browser = await chromium.launch({ channel: "msedge", headless: true });
    const errors = [];
    const remoteRequests = [];
    const screenshots = path.join(__dirname, "screenshots");
    fs.mkdirSync(screenshots, { recursive: true });
    try {
        const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
        page.on("pageerror", (error) => errors.push(error.message));
        page.on("request", (request) => { if (/^https?:/.test(request.url())) remoteRequests.push(request.url()); });
        await page.goto(pathToFileURL(path.join(__dirname, "statusreport.html")).href);
        await page.waitForSelector(".metrics");
        if (process.argv.includes("--pilot") || process.argv.includes("--rollout")) {
            const mode = process.argv.includes("--rollout") ? "dashboard-v1" : "dashboard-pilot-v1";
            await checkPilotViews(page, data.project, mode);
            if (mode === "dashboard-v1") await checkRosterViews(page, data.project);
            await checkLivePublication(browser, data, screenshots, mode);
            assert.deepEqual(errors, []);
            assert.deepEqual(remoteRequests, []);
            console.log(`PASS: ${mode} forecast/activity/exchanges, ${mode === "dashboard-v1" ? "all 28 leaf detail views, " : ""}real file-watcher publication, generated Markdown, automatic refresh and wrong-project rejection. Synthetic fixtures only; publisher stopped and fixtures removed.`);
            return;
        }
        if (process.argv.includes("--status-tooltips")) {
            await checkStatusTooltips(page);
            assert.deepEqual(errors, []);
            assert.deepEqual(remoteRequests, []);
            console.log("PASS: status definitions, native hover titles, unknown-status fallback, and recorded-snapshot wording; no runtime errors or network requests.");
            return;
        }
        assert.equal(await page.locator("h1").textContent(), data.project.replace(/^ProphetsWay\./, ""));
        assert.ok(await page.locator("svg path, svg circle, svg rect").count() > 10);
        const checkpoints = data.checkpoints.filter((checkpoint) => checkpoint.runId === data.runs[0].id);
        assert.equal(await page.locator(".completion").count(), checkpoints.length);
        assert.ok((await page.locator("#content").textContent()).includes("Future slice count: TBD"));
        await page.screenshot({ path: path.join(screenshots, "desktop-overview.png"), fullPage: true, animations: "disabled" });

        await page.locator('[data-view="work"]').click();
        await page.waitForSelector(".stage");
        assert.equal(await page.locator(".stage").count(), data.requirements.stages.length);
        await page.locator("#status-filter").selectOption("partial");
        assert.equal(await page.locator(".stage").count(), data.requirements.stages.filter((stage) => stage.status === "partial").length);

        await page.locator('[data-view="requirements"]').click();
        await page.waitForSelector("tbody tr");
        assert.equal(await page.locator("tbody tr").count(), data.requirements.items.length);
        const requirement = data.requirements.items.find((item) => item.id === "R-18") || data.requirements.items[0];
        await page.locator("#search").fill(requirement.id);
        assert.equal(await page.locator("tbody tr").count(), 1);
        await page.locator(".table-title").click();
        assert.ok(await page.locator("#detail-dialog").isVisible());
        assert.equal(await page.locator("#detail-title").textContent(), requirement.title);
        await page.keyboard.press("Escape");
        assert.ok(!await page.locator("#detail-dialog").isVisible());
        await page.locator("#search").fill("no-such-requirement-93821");
        assert.ok((await page.locator(".empty").textContent()).includes("No matches"));
        await page.locator("#clear-search").click();
        assert.equal(await page.locator("tbody tr").count(), data.requirements.items.length);

        await page.locator('[data-view="agents"]').click();
        await page.waitForSelector(".agent-card");
        const latestReports = data.reports.filter((report) => report.runId === data.runs[0].id);
        assert.equal(await page.locator(".agent-card").count(), new Set(latestReports.map((report) => report.role)).size);
        await page.screenshot({ path: path.join(screenshots, "desktop-agents.png"), fullPage: true, animations: "disabled" });
        await page.locator("#search").fill("Security Reviewer");
        assert.equal(await page.locator(".agent-card").count(), 1);
        await page.locator(".agent-card .icon-button").click();
        assert.ok((await page.locator("#detail-content").textContent()).includes("Scoped historical outcome"));
        await page.locator("#close-dialog").click();
        await page.locator("#run-filter").selectOption("all");
        await page.waitForFunction(() => window.AI_DASHBOARD_ARCHIVE?.reports?.length > 0);

        await page.locator('[data-view="history"]').click();
        await page.waitForSelector("tbody tr");
        assert.equal(await page.locator("tbody tr").count(), data.runs.length);
        await page.locator("tbody .table-title").last().click();
        await page.waitForSelector("#detail-content tbody tr");
        assert.equal(await page.locator("#detail-content tbody tr").count(), data.runs.at(-1).reports);
        await page.locator("#close-dialog").click();
        await page.locator(".coverage summary").click();
        assert.equal(await page.locator(".coverage li").count(), data.warnings.length);

        await page.evaluate(() => {
            const report = window.AI_DASHBOARD_DATA.reports.find((entry) => entry.runId === window.AI_DASHBOARD_DATA.runs[0].id);
            report.title = '<img src="missing" onerror="window.dashboardInjected=true">';
        });
        await page.locator('[data-view="agents"]').click();
        assert.equal(await page.evaluate(() => window.dashboardInjected), undefined);
        assert.equal(await page.locator("#content img").count(), 0);

        await page.reload();
        await page.setViewportSize({ width: 2560, height: 1440 });
        await page.locator('[data-view="overview"]').click();
        await page.waitForSelector(".metrics");
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1));
        await page.screenshot({ path: path.join(screenshots, "desktop-wide-overview.png"), fullPage: true, animations: "disabled" });
        for (const view of ["work", "requirements", "agents", "history"]) {
            await page.locator(`[data-view="${view}"]`).click();
            await page.waitForFunction((current) => document.querySelector('.navigation [aria-current="page"]')?.dataset.view === current, view);
            assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1), `${view} exceeds desktop width`);
        }
        assert.deepEqual(errors, []);
        assert.deepEqual(remoteRequests, []);
        console.log("PASS: data generations/identities; offline rendering; historical views; filtering/search; details/keyboard dismissal; archive loading; escaped report content; 1440px/2560px desktop layout; local icons.");
        console.log(`Screenshots: ${screenshots}`);
    } finally { await browser.close(); }
}

main().catch((error) => { console.error(error); process.exitCode = 1; });