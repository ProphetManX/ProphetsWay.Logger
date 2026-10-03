#Requires -Version 7.0
[CmdletBinding()]
param(
    [string] $RunRoot,
    [ValidateRange(1, 30)]
    [int] $RecentDays = 3,
    [switch] $Open,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$null = ConvertFrom-Markdown -InputObject '# Dashboard'
$pipelineBuilder = [Markdig.MarkdownPipelineBuilder]::new()
$null = [Markdig.MarkdownExtensions]::UseAdvancedExtensions($pipelineBuilder)
$script:MarkdownPipeline = $pipelineBuilder.Build()
$script:MarkdownMethods = @{}
foreach ($operation in @('Parse', 'ToPlainText')) {
    $script:MarkdownMethods[$operation] = [Markdig.Markdown].GetMethods() |
        Where-Object { $_.Name -eq $operation -and $_.GetParameters().Count -eq 3 -and $_.GetParameters()[0].ParameterType -eq [string] } |
        Select-Object -First 1
    if (-not $script:MarkdownMethods[$operation]) {
        throw "The PowerShell Markdown parser does not supply $operation."
    }
}

function Invoke-MarkdownMethod {
    param([string] $Operation, [string] $Text)
    $method = $script:MarkdownMethods[$Operation]
    $arguments = [object[]]::new($method.GetParameters().Count)
    $arguments[0] = $Text
    $arguments[1] = $script:MarkdownPipeline
    return ,$method.Invoke($null, $arguments)
}

function Get-MarkdownBlocks {
    param([string] $Text)
    $document = Invoke-MarkdownMethod -Operation Parse -Text $Text
    foreach ($block in $document) {
        $kind = $block.GetType().Name
        if ($kind -notin @('HeadingBlock', 'ParagraphBlock', 'ListBlock', 'Table')) {
            continue
        }
        $length = $block.Span.End - $block.Span.Start + 1
        if ($length -le 0 -or $block.Span.Start -lt 0) { continue }
        $source = $Text.Substring($block.Span.Start, $length)
        $plain = Invoke-MarkdownMethod -Operation ToPlainText -Text $source
        $rows = @()
        if ($kind -eq 'Table') {
            $rows = @(foreach ($row in $block) {
                $cells = @(foreach ($cell in $row) {
                    $cellSource = $Text.Substring($cell.Span.Start, $cell.Span.End - $cell.Span.Start + 1)
                    (Invoke-MarkdownMethod -Operation ToPlainText -Text $cellSource).Trim()
                })
                [pscustomobject]@{ Cells = $cells }
            })
        }
        [pscustomobject]@{
            Kind = $kind
            Level = $(if ($kind -eq 'HeadingBlock') { $block.Level } else { 0 })
            Line = $block.Line + 1
            Source = $source
            Text = $plain.Trim()
            Rows = $rows
        }
    }
}

function Get-ReportHeader {
    param([object[]] $Blocks)
    $fields = @{}
    foreach ($block in $Blocks) {
        if ($block.Level -ge 2) { break }
        if ($block.Kind -eq 'HeadingBlock') { continue }
        foreach ($line in ($block.Text -split '\r?\n')) {
            if ($line -match '^\s*(?:[-*]\s+)?(?<key>[A-Za-z][A-Za-z /-]{0,44}):\s*(?<value>.+)$') {
                $key = $Matches.key.Trim()
                if (-not $fields.ContainsKey($key)) {
                    $fields[$key] = $Matches.value.Trim()
                }
                elseif ($fields[$key] -ne $Matches.value.Trim()) {
                    $fields['Conflicting header'] = 'true'
                }
            }
        }
    }
    return $fields
}

function Get-RecordedStatus {
    param([hashtable] $Header, [switch] $IsRun)
    if ($Header['Conflicting header']) { return 'unknown' }
    $state = [string] $Header['State']
    $outcome = [string] $Header['Outcome']
    $reason = [string] $Header['Reason']
    $finalized = $Header['Record status'] -eq 'FINALIZED' -or $state -match '^(FINALIZED|SIGN_OFF)\b'
    if ($state -match '^STARTED\b' -and -not $finalized) { return 'incomplete' }
    if ($IsRun -and $Header['Assignment status'] -match '^PAUSED\b') { return 'paused' }
    if ($outcome -eq 'FAILED') { return 'failed' }
    if ($outcome -eq 'BLOCKED') { return 'blocked' }
    if ($outcome -eq 'PARTIAL') {
        if ($reason -eq 'REVIEW') { return 'changes' }
        if ($reason -eq 'BUDGET') { return 'paused' }
        if ($reason -in @('OWNER_DECISION', 'ENVIRONMENT', 'PROTOCOL', 'VALIDATION')) { return 'blocked' }
        return 'partial'
    }
    if ($outcome -in @('COMPLETE', 'NO_CHANGE') -and $finalized) { return 'complete' }
    return 'unknown'
}

function Test-RepositoryDeclaration {
    param([object[]] $Blocks, [string] $RepositoryRoot)
    $expected = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@('\', '/'))
    foreach ($block in $Blocks) {
        if ($block.Kind -eq 'HeadingBlock') { continue }
        $declarations = @($block.Text -split '\r?\n')
        if ($block.Kind -eq 'Table') {
            $declarations += @(foreach ($row in $block.Rows) {
                if ($row.Cells.Count -ge 2 -and $row.Cells[0] -match '^(Repository(?: root)?|Allowed repositories|Repositories)$') {
                    "Repository: $($row.Cells[1])"
                }
            })
        }
        foreach ($line in $declarations) {
            $candidate = $null
            if ($line -match '^\s*(?:[-*]\s+)?(?:Repository(?: root)?|Allowed repositories|Repositories)\s*:\s*(?<path>[A-Za-z]:[/\\].+)$') {
                $candidate = $Matches.path -replace '\s+only\.?$', ''
            }
            elseif ($line -match '^All paths below are relative to (?<path>[A-Za-z]:[/\\].+)\.$') {
                $candidate = $Matches.path
            }
            elseif ($line -match '^Repository\s+(?<path>[A-Za-z]:[/\\][^;]+);') {
                $candidate = $Matches.path
            }
            if ($candidate) {
                $candidate = $candidate.Trim().TrimEnd('.').TrimEnd([char[]]@('\', '/'))
                try {
                    if ([IO.Path]::GetFullPath($candidate).Equals($expected, [StringComparison]::OrdinalIgnoreCase)) {
                        return $true
                    }
                }
                catch { continue }
            }
        }
    }
    return $false
}

function Get-SectionText {
    param([object[]] $Blocks, [string] $Heading, [int] $MaximumLength = 600)
    $selected = $false
    $parts = [Collections.Generic.List[string]]::new()
    foreach ($block in $Blocks) {
        if ($block.Kind -eq 'HeadingBlock') {
            if ($selected) { break }
            $selected = $block.Text -match $Heading
            continue
        }
        if ($selected -and $block.Text) { $parts.Add($block.Text) }
    }
    $text = $parts -join "`n`n"
    if ($text.Length -gt $MaximumLength) { return $text.Substring(0, $MaximumLength) + '...' }
    return $text
}

function Get-SafeText {
    param([AllowEmptyString()][string] $Text, [int] $Limit = 900)
    $text = $Text -replace '(?im)\b(password|passwd|pwd|api[_ -]?key|access[_ -]?token|client[_ -]?secret|connection string|authorization)\s*[:=][^\r\n]+', '$1: [redacted]'
    if ($text.Length -gt $Limit) { return $text.Substring(0, $Limit) + '...' }
    return $text
}

function Get-SourceReference {
    param([string] $Path, [int] $Line = 1)
    $relative = [IO.Path]::GetRelativePath($PSScriptRoot, $Path).Replace('\', '/')
    return (($relative.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/') + "#L$Line"
}

function Read-DashboardSource {
    param([string] $Path)
    $file = Get-Item -LiteralPath $Path
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $file.Length -gt 4MB) {
        throw 'Linked or oversized input is not imported.'
    }
    $before = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    $text = [IO.File]::ReadAllText($Path)
    $after = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($before -ne $after) { throw 'Input changed during import.' }
    $blocks = @(Get-MarkdownBlocks -Text $text)
    return [pscustomobject]@{
        Path = $file.FullName
        Hash = $after
        UpdatedAt = $file.LastWriteTimeUtc.ToString('o')
        Blocks = $blocks
        Header = (Get-ReportHeader -Blocks $blocks)
        Title = $(if ($blocks.Count -and $blocks[0].Kind -eq 'HeadingBlock') { $blocks[0].Text } else { $file.BaseName })
    }
}

function Get-ReportRole {
    param([hashtable] $Header, [string] $Title, [string] $Name)
    if ($Header['Agent']) { return @{ name = (Get-SafeText $Header['Agent'] 80); inferred = $false } }
    $identity = "$Name $Title"
    $rules = [ordered]@{
        'Requirements Reviewer' = 'requirements.*(review|audit)'
        'Test Auditor' = '(setup|specification|specs|test|assertion).*(audit|reaudit)|audit.*(setup|specification|test)'
        'Security Reviewer' = 'security|vulnerab|advisory'
        'Contract Reviewer' = 'contract.*review|reference.review|requirements.review'
        'Code Reviewer' = 'code.review|independent.*readback|correctness.review'
        'Commit Author' = 'commit.message'
        'Repository Operator' = 'operator|checkpoint.commit|branch.prepar'
        'Session Scribe' = 'session.wrapup|session.handoff|handoff.*correction|scribe'
        'README Author' = 'readme'
        'Changelog Author' = 'changelog'
        'Test Harness Engineer' = 'setup|validation.remainder|harness|fixture'
        'Interface Architect' = 'contract|interface.authorship|interface.materialization'
        'Test Designer' = 'specification|test.design|test.author|assertion.*migration'
        'Modernizer' = 'moderniz|project.config|dependency'
        'Implementer' = 'implement|materializ|source.*repair'
        'Owner Delegate' = 'owner.delegate|delegated.decision'
    }
    foreach ($role in $rules.Keys) {
        if ($identity -match $rules[$role]) { return @{ name = $role; inferred = $true } }
    }
    return @{ name = 'Unclassified'; inferred = $true }
}

function Get-RequirementsData {
    param([string] $RepositoryRoot)
    $path = Join-Path $RepositoryRoot 'docs/requirements.md'
    $requirements = [Collections.Generic.List[object]]::new()
    $stages = [Collections.Generic.List[object]]::new()
    if (-not (Test-Path -LiteralPath $path)) {
        return @{ items = @(); stages = @(); source = $null; disposition = 'No requirements source found.' }
    }
    $source = Read-DashboardSource $path
    $current = $null
    $inStages = $false
    foreach ($block in $source.Blocks) {
        if ($block.Kind -eq 'HeadingBlock') {
            if ($current) { $requirements.Add($current); $current = $null }
            $inStages = $block.Text -eq 'Staged Delivery'
            if ($block.Text -match '^(?<id>R-\d+)\s+-\s+(?<title>.+)$') {
                $current = [ordered]@{
                    id = $Matches.id
                    title = ($Matches.title -replace '\s+\(D.*$', '')
                    description = ''
                    criteria = [Collections.Generic.List[string]]::new()
                    status = 'unreconciled'
                    source = (Get-SourceReference $path $block.Line)
                }
            }
            continue
        }
        if ($current) {
            if (-not $current.description -and $block.Kind -eq 'ParagraphBlock') {
                $current.description = Get-SafeText $block.Text 900
            }
            foreach ($match in [regex]::Matches($block.Text, '\bAC-\d+\.\d+:')) {
                $criterion = $match.Value.TrimEnd(':')
                if (-not $current.criteria.Contains($criterion)) { $current.criteria.Add($criterion) }
            }
        }
        if ($inStages -and $block.Kind -eq 'Table') {
            foreach ($row in $block.Rows) {
                if ($row.Cells.Count -ge 3 -and $row.Cells[0] -match '^(?<id>M\d+)\s+-\s+(?<title>.+)$') {
                    $stages.Add(@{
                        id = $Matches.id
                        title = $Matches.title
                        scope = (Get-SafeText $row.Cells[1])
                        boundary = (Get-SafeText $row.Cells[2])
                        status = 'unreconciled'
                        source = (Get-SourceReference $path $block.Line)
                    })
                }
            }
        }
    }
    if ($current) { $requirements.Add($current) }
    return @{
        items = $requirements.ToArray()
        stages = $stages.ToArray()
        source = (Get-SourceReference $path)
        disposition = (Get-SafeText ([string] $source.Header['Disposition']))
        sourceHash = $source.Hash
        updatedAt = $source.UpdatedAt
    }
}

function Initialize-PilotSnapshot {
    param([string] $Directory)
    $path = Join-Path $Directory 'pilot-data.js'
    if (Test-Path -LiteralPath $path) {
        if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Pilot snapshot must not be a link or junction.'
        }
        return
    }
    $temporary = Join-Path $Directory ('.pilot-bootstrap-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllText($temporary, "window.AI_DASHBOARD_PILOT = null;`n", [Text.UTF8Encoding]::new($false))
        try { [IO.File]::Move($temporary, $path, $false) }
        catch [IO.IOException] { if (-not [IO.File]::Exists($path)) { throw } }
    }
    finally { if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) } }
}

function Test-DashboardParser {
    $sample = @'
# Example window

**State:** SIGN_OFF
**Record status:** FINALIZED
**Outcome:** PARTIAL
**Reason:** BUDGET
**Assignment status:** PAUSED at a capacity boundary

## Current State

Two components complete. The project remains unfinished.

## Historical state

Outcome: COMPLETE
'@
    $blocks = @(Get-MarkdownBlocks -Text $sample)
    $header = Get-ReportHeader -Blocks $blocks
    $checks = [ordered]@{
        'Markdown heading extracted' = $blocks[0].Text -eq 'Example window'
        'Historical outcome cannot override header' = $header['Outcome'] -eq 'PARTIAL'
        'Partial finalized run remains paused' = (Get-RecordedStatus -Header $header -IsRun) -eq 'paused'
        'STARTED alone is not live activity' = (Get-RecordedStatus -Header @{ State = 'STARTED' }) -eq 'incomplete'
        'STARTED plus COMPLETE is not accepted' = (Get-RecordedStatus -Header @{ State = 'STARTED'; Outcome = 'COMPLETE' }) -eq 'incomplete'
        'Completed reviewer is a completed report' = (Get-RecordedStatus -Header @{ State = 'FINALIZED'; Outcome = 'COMPLETE' }) -eq 'complete'
        'Partial review requests changes' = (Get-RecordedStatus -Header @{ State = 'FINALIZED'; Outcome = 'PARTIAL'; Reason = 'REVIEW' }) -eq 'changes'
        'Missing metadata stays unknown' = (Get-RecordedStatus -Header @{}) -eq 'unknown'
        'Conflicting metadata stays unknown' = (Get-RecordedStatus -Header @{ State = 'FINALIZED'; Outcome = 'COMPLETE'; 'Conflicting header' = 'true' }) -eq 'unknown'
        'Section extraction stops at next heading' = (Get-SectionText -Blocks $blocks -Heading '^Current State$') -eq 'Two components complete. The project remains unfinished.'
    }
    $root = Join-Path ([IO.Path]::GetTempPath()) 'DashboardParserRepository'
    $matching = @(Get-MarkdownBlocks -Text "- Repository: $root only.")
    $neighbor = @(Get-MarkdownBlocks -Text "- Repository: ${root}-other only.")
    $checks['Exact repository identity accepted'] = Test-RepositoryDeclaration -Blocks $matching -RepositoryRoot $root
    $checks['Neighboring repository rejected'] = -not (Test-RepositoryDeclaration -Blocks $neighbor -RepositoryRoot $root)
    $table = @(Get-MarkdownBlocks -Text "| Stage | Scope | Boundary |`n| --- | --- | --- |`n| M5 - Bridges | R-17 | Both directions |")
    $checks['Markdown tables use structured parsing'] = $table[0].Rows[1].Cells[0] -eq 'M5 - Bridges'
    $checks['Credential-shaped text redacted'] = (Get-SafeText 'password=do-not-export') -eq 'password: [redacted]'
    $scopeTable = @(Get-MarkdownBlocks -Text "| Field | Boundary |`n| --- | --- |`n| Allowed repositories | $root only |")
    $checks['Explicit repository in slice table accepted'] = Test-RepositoryDeclaration -Blocks $scopeTable -RepositoryRoot $root
    $checks['Repository in slice table is still exact'] = -not (Test-RepositoryDeclaration -Blocks $scopeTable -RepositoryRoot "${root}-other")
    $checks['Annotated finalized state accepted'] = (Get-RecordedStatus -Header @{ State = 'FINALIZED; scoped outcome'; Outcome = 'COMPLETE' }) -eq 'complete'
    $fixtureDirectory = Join-Path ([IO.Path]::GetTempPath()) ('dashboard-bootstrap-' + [guid]::NewGuid().ToString('N'))
    $null = [IO.Directory]::CreateDirectory($fixtureDirectory)
    try {
        Initialize-PilotSnapshot $fixtureDirectory
        $fixturePath = Join-Path $fixtureDirectory 'pilot-data.js'
        $checks['Fresh dashboard gets an empty pilot view'] = [IO.File]::ReadAllText($fixturePath).Contains('AI_DASHBOARD_PILOT = null')
        $existingPublication = 'window.AI_DASHBOARD_PILOT = {"fixture":true};'
        [IO.File]::WriteAllText($fixturePath, $existingPublication)
        Initialize-PilotSnapshot $fixtureDirectory
        $checks['Historical refresh preserves pilot publication'] = [IO.File]::ReadAllText($fixturePath) -eq $existingPublication
    }
    finally { [IO.Directory]::Delete($fixtureDirectory, $true) }
    $failed = @($checks.Keys | Where-Object { -not $checks[$_] })
    if ($failed.Count) { throw "Parser checks failed: $($failed -join '; ')" }
    Write-Output "PASS: $($checks.Count) dashboard parser checks; no run or repository data changed."
}

if ($SelfTest) {
    Test-DashboardParser
    return
}

$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$repositoryName = Split-Path -Leaf $repositoryRoot
$repositoryToken = ($repositoryName -split '\.')[-1]
if (-not $RunRoot) { $RunRoot = Join-Path (Split-Path -Parent $repositoryRoot) '.agent-runs' }
$RunRoot = (Resolve-Path -LiteralPath $RunRoot).Path
if ((Get-Item -LiteralPath $RunRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'The run root must not be a link or junction.'
}

$runs = [Collections.Generic.List[object]]::new()
$reports = [Collections.Generic.List[object]]::new()
$checkpoints = [Collections.Generic.List[object]]::new()
$warnings = [Collections.Generic.List[string]]::new()
$seenCheckpoints = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$candidates = @(Get-ChildItem -LiteralPath $RunRoot -Directory |
    Where-Object { $_.Name -match '^\d{8}-\d{4}-' -and $_.Name -like "*-$repositoryToken*" -and
        -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) } |
    Sort-Object Name -Descending)

foreach ($directory in $candidates) {
    $runPath = Join-Path $directory.FullName 'run.md'
    if (-not (Test-Path -LiteralPath $runPath)) {
        $warnings.Add("$($directory.Name): no run summary; not imported.")
        continue
    }
    try {
        $source = Read-DashboardSource $runPath
        $confirmed = Test-RepositoryDeclaration -Blocks $source.Blocks -RepositoryRoot $repositoryRoot
        if (-not $confirmed) {
            foreach ($authorityFile in @(Get-ChildItem -LiteralPath $directory.FullName -File -Filter '*.md' |
                Where-Object Name -Match '^(assignment|slice|acceptance|target)[-_]')) {
                $authority = Read-DashboardSource $authorityFile.FullName
                if (Test-RepositoryDeclaration -Blocks $authority.Blocks -RepositoryRoot $repositoryRoot) {
                    $confirmed = $true
                    break
                }
            }
        }
        if (-not $confirmed) {
            $warnings.Add("$($directory.Name): no matching explicit repository declaration; not imported.")
            continue
        }
    }
    catch {
        $warnings.Add("$($directory.Name): summary unreadable, changed during import, or unsupported; not imported.")
        continue
    }

    $runReports = [Collections.Generic.List[object]]::new()
    foreach ($reportFile in @(Get-ChildItem -LiteralPath $directory.FullName -File -Filter '*.md' |
        Where-Object Name -Match '^\d{2,3}[-_]')) {
        try {
            $report = Read-DashboardSource $reportFile.FullName
            $role = Get-ReportRole -Header $report.Header -Title $report.Title -Name $reportFile.Name
            $summary = Get-SectionText -Blocks $report.Blocks -Heading '^(Summary|What changed|Work completed|Scope|Findings|Current State|Observed Identity|Result|Changes)\b' -MaximumLength 1100
            $next = Get-SectionText -Blocks $report.Blocks -Heading '^(Next|Disposition|Continuation|Blockers)\b' -MaximumLength 900
            $record = @{
                id = "$($directory.Name)/$($reportFile.Name)"
                runId = $directory.Name
                sequence = [int] ([regex]::Match($reportFile.Name, '^\d+').Value)
                title = (Get-SafeText $report.Title 180)
                role = $role.name
                roleInferred = $role.inferred
                status = (Get-RecordedStatus -Header $report.Header)
                outcome = [string] $report.Header['Outcome']
                reason = [string] $report.Header['Reason']
                continuation = [string] $report.Header['Continuation']
                target = (Get-SafeText ([string] $report.Header['Target']) 250)
                objective = (Get-SafeText ([string] $report.Header['Objective']) 450)
                summary = (Get-SafeText $summary 1100)
                next = (Get-SafeText $next 900)
                verdict = (Get-SafeText ([string] $report.Header['Scoped verdict']) 450)
                source = (Get-SourceReference $reportFile.FullName)
                sourceHash = $report.Hash
                updatedAt = $report.UpdatedAt
            }
            $runReports.Add($record)
            $reports.Add($record)
        }
        catch { $warnings.Add("$($directory.Name)/$($reportFile.Name): record could not be imported.") }
    }

    $remaining = @($source.Blocks | Where-Object Text -Match '^Exact remaining work:' | Select-Object -First 1)
    $remainingText = if ($remaining.Count) { $remaining[0].Text -replace '^Exact remaining work:\s*', '' } else { '' }
    $slices = @(foreach ($sliceFile in @(Get-ChildItem -LiteralPath $directory.FullName -File -Filter 'slice-*.md')) {
        try {
            $slice = Read-DashboardSource $sliceFile.FullName
            @{
                id = $sliceFile.BaseName
                title = (Get-SafeText $slice.Title 180)
                source = (Get-SourceReference $sliceFile.FullName)
                status = 'unreconciled'
            }
        }
        catch { $warnings.Add("$($directory.Name)/$($sliceFile.Name): target could not be imported.") }
    })
    foreach ($block in $source.Blocks) {
        foreach ($match in [regex]::Matches($block.Text, '(?<id>CK-[A-Za-z0-9-]+):\s*CONSUMED,\s*(?<status>COMPLETE|GREEN_PARTIAL)\b')) {
            if ($seenCheckpoints.Add($match.Groups['id'].Value)) {
                $identifier = $match.Groups['id'].Value
                $title = $identifier -replace '^CK-', '' -replace '-\d{8}-\d+$', '' -replace '-', ' '
                $checkpoints.Add(@{
                    id = $identifier
                    title = $title
                    status = $(if ($match.Groups['status'].Value -eq 'COMPLETE') { 'complete' } else { 'partial' })
                    runId = $directory.Name
                    summary = (Get-SafeText $block.Text 1000)
                    source = (Get-SourceReference $runPath $block.Line)
                })
            }
        }
    }
    $runs.Add(@{
        id = $directory.Name
        title = (Get-SafeText $source.Title 180)
        status = (Get-RecordedStatus -Header $source.Header -IsRun)
        outcome = [string] $source.Header['Outcome']
        reason = [string] $source.Header['Reason']
        assignment = (Get-SafeText ([string] $source.Header['Assignment status']) 900)
        currentOwner = (Get-SafeText ([string] $source.Header['Current owner']) 200)
        target = (Get-SafeText ([string] $source.Header['Current target']) 350)
        summary = (Get-SafeText (Get-SectionText -Blocks $source.Blocks -Heading '^Current State$' -MaximumLength 1200) 1200)
        remaining = (Get-SafeText $remainingText 2600)
        source = (Get-SourceReference $runPath)
        sourceHash = $source.Hash
        updatedAt = $source.UpdatedAt
        reports = $runReports.Count
        completedReports = @($runReports | Where-Object status -EQ 'complete').Count
        slices = $slices
    })
}

$requirements = Get-RequirementsData -RepositoryRoot $repositoryRoot
$latest = if ($runs.Count) { $runs[0] } else { $null }
if ($latest) {
    $focusIds = @([regex]::Matches($latest.assignment, '\bM\d+\b') | ForEach-Object Value | Select-Object -Unique)
    foreach ($stage in $requirements.stages) {
        if ($stage.id -in $focusIds -and $latest.assignment -match 'unfinished|incomplete|remaining') {
            $stage.status = 'partial'
        }
    }
}
$cutoff = [DateTime]::UtcNow.AddDays(-$RecentDays)
$recentRunIds = @($runs | Where-Object { [DateTimeOffset]::Parse($_.updatedAt).UtcDateTime -ge $cutoff -or $_.id -eq $latest.id } | ForEach-Object id)
$currentReports = @($reports | Where-Object { $_.runId -in $recentRunIds })
$archivedReports = @($reports | Where-Object { $_.runId -notin $recentRunIds })
$generation = [guid]::NewGuid().ToString('N')
$data = [ordered]@{
    schemaVersion = 1
    generation = $generation
    project = $repositoryName
    generatedAt = [DateTime]::UtcNow.ToString('o')
    mode = 'recorded-snapshot'
    recentDays = $RecentDays
    runs = $runs.ToArray()
    reports = $currentReports
    checkpoints = $checkpoints.ToArray()
    requirements = $requirements
    archiveCount = $archivedReports.Count
    totalReports = $reports.Count
    candidateRuns = $candidates.Count
    warnings = $warnings.ToArray()
    discovery = 'Additional future slices are not reconciled into a project-wide inventory. No discovery completion is inferred.'
}
$archive = @{ schemaVersion = 1; generation = $generation; reports = $archivedReports }
$outputRoot = Join-Path $PSScriptRoot 'data'
if (Test-Path -LiteralPath $outputRoot) {
    if ((Get-Item -LiteralPath $outputRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Dashboard data output must not be a link or junction.'
    }
}
$null = [IO.Directory]::CreateDirectory($outputRoot)
$lockPath = Join-Path $outputRoot '.update.lock'
$lock = [IO.File]::Open($lockPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    Initialize-PilotSnapshot $outputRoot
    foreach ($export in @(
        @{ Name = 'archive-data.js'; Global = 'AI_DASHBOARD_ARCHIVE'; Value = $archive },
        @{ Name = 'dashboard-data.js'; Global = 'AI_DASHBOARD_DATA'; Value = $data }
    )) {
        $json = ConvertTo-Json -InputObject $export.Value -Depth 14 -Compress -EscapeHandling EscapeHtml
        $temporary = Join-Path $outputRoot "$($export.Name).$generation.tmp"
        [IO.File]::WriteAllText($temporary, "window.$($export.Global) = $json;`n", [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporary, (Join-Path $outputRoot $export.Name), $true)
    }
}
finally {
    $lock.Dispose()
    [IO.File]::Delete($lockPath)
}
Write-Output "Imported $($runs.Count)/$($candidates.Count) matching run folders, $($reports.Count) reports, $($checkpoints.Count) recorded checkpoints and $($requirements.items.Count) requirements."
Write-Output "Current detail: $($currentReports.Count); archived detail: $($archivedReports.Count); import warnings: $($warnings.Count)."
Write-Output "Dashboard: $(Join-Path $PSScriptRoot 'statusreport.html')"
if ($Open) { Start-Process -FilePath (Join-Path $PSScriptRoot 'statusreport.html') }