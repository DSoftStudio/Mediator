# Generates benchmarks/BENCHMARKS.md from BenchmarkDotNet result files.
# Called automatically by run-all-benchmarks.cmd after all benchmarks complete.
# "All Libraries" sections are assembled by concatenating isolated per-library tables.

param(
    # Which run to read. The runners write per target framework -- see the "Results:" line they
    # echo -- and this script used to default to the flat directory they wrote to BEFORE the repo
    # multi-targeted. Left alone it regenerated an older run and looked like it had worked.
    [string]$Tfm = "",
    [string]$ResultsDir = "",
    [string]$OutputFile = ""
)

$artifacts = Join-Path $PSScriptRoot "BenchmarkDotNet.Artifacts"

if (-not $ResultsDir) {
    if ($Tfm) {
        $ResultsDir = Join-Path $artifacts "$Tfm\results"
    }
    else {
        # No TFM given: take the most recently written per-TFM run, falling back to the flat
        # directory so an older layout still generates something rather than silently nothing.
        $candidates = @(Get-ChildItem -Path $artifacts -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-Path (Join-Path $_.FullName "results") } |
            ForEach-Object {
                $r = Join-Path $_.FullName "results"
                $newest = Get-ChildItem -Path $r -Filter "*-report-github.md" -ErrorAction SilentlyContinue |
                    Sort-Object LastWriteTime -Descending | Select-Object -First 1
                if ($newest) { [pscustomobject]@{ Tfm = $_.Name; Dir = $r; When = $newest.LastWriteTime } }
            } | Sort-Object When -Descending)

        if ($candidates.Count -gt 0) {
            $Tfm = $candidates[0].Tfm
            $ResultsDir = $candidates[0].Dir
        }
        else {
            $ResultsDir = Join-Path $artifacts "results"
        }
    }
}

if (-not $OutputFile) {
    # One file per TFM, named after it, so two runs cannot silently overwrite each other.
    $OutputFile = if ($Tfm -and $Tfm -ne "net10.0") {
        Join-Path $PSScriptRoot "BENCHMARKS-$Tfm.md"
    } else {
        Join-Path $PSScriptRoot "BENCHMARKS.md"
    }
}

if (-not (Test-Path $ResultsDir)) {
    Write-Error "No results directory: $ResultsDir. Run benchmarks\run-all-benchmarks.cmd first."
    exit 1
}

Write-Host "Reading: $ResultsDir"

# ── Per-library isolated sections (each ran in its own process) ──────────
$titleMap = [ordered]@{
    # ── DSoft ──────────────────────────────────────────────────────
    "Benchmarks.DSoftSendNoBehaviorsBenchmarks"         = "DSoft - Send (No Behaviors)"
    "Benchmarks.DSoftSendBenchmarks"                    = "DSoft - Send (Behaviors)"
    "Benchmarks.DSoftSendObjectBenchmarks"              = "DSoft - Send (Object)"
    "Benchmarks.DSoftPublishBenchmarks"                 = "DSoft - Publish"
    "Benchmarks.DSoftPublishObjectBenchmarks"            = "DSoft - Publish (Object)"
    "Benchmarks.DSoftStreamBenchmarks"                  = "DSoft - Stream"
    "Benchmarks.DSoftConcurrencyBenchmarks"             = "DSoft - Concurrency"
    "Benchmarks.DSoftColdStartBenchmarks"               = "DSoft - Cold Start"
    "Benchmarks.DSoftRealisticPipelineBenchmarks"         = "DSoft - Realistic Pipeline"
    "Benchmarks.DSoftBehaviorScalingBenchmarks"          = "DSoft - Behavior Scaling"
    # ── MediatR ───────────────────────────────────────────────────
    "Benchmarks.MediatRSendNoBehaviorsBenchmarks"       = "MediatR - Send (No Behaviors)"
    "Benchmarks.MediatRSendBenchmarks"                  = "MediatR - Send (Behaviors)"
    "Benchmarks.MediatRSendObjectBenchmarks"            = "MediatR - Send (Object)"
    "Benchmarks.MediatRPublishBenchmarks"               = "MediatR - Publish"
    "Benchmarks.MediatRPublishObjectBenchmarks"          = "MediatR - Publish (Object)"
    "Benchmarks.MediatRStreamBenchmarks"                = "MediatR - Stream"
    "Benchmarks.MediatRConcurrencyBenchmarks"           = "MediatR - Concurrency"
    "Benchmarks.MediatRColdStartBenchmarks"             = "MediatR - Cold Start"
    "Benchmarks.MediatRRealisticPipelineBenchmarks"       = "MediatR - Realistic Pipeline"
    "Benchmarks.MediatRBehaviorScalingBenchmarks"        = "MediatR - Behavior Scaling"
    # ── DispatchR ─────────────────────────────────────────────────
    "Benchmarks.DispatchRSendNoBehaviorsBenchmarks"     = "DispatchR - Send (No Behaviors)"
    "Benchmarks.DispatchRSendBenchmarks"                = "DispatchR - Send (Behaviors)"
    "Benchmarks.DispatchRPublishBenchmarks"             = "DispatchR - Publish"
    "Benchmarks.DispatchRPublishObjectBenchmarks"        = "DispatchR - Publish (Object)"
    "Benchmarks.DispatchRStreamBenchmarks"              = "DispatchR - Stream"
    "Benchmarks.DispatchRConcurrencyBenchmarks"         = "DispatchR - Concurrency"
    "Benchmarks.DispatchRColdStartBenchmarks"           = "DispatchR - Cold Start"
    "Benchmarks.DispatchRRealisticPipelineBenchmarks"     = "DispatchR - Realistic Pipeline"
    "Benchmarks.DispatchRBehaviorScalingBenchmarks"      = "DispatchR - Behavior Scaling"
    # ── Mediator Source Gen ───────────────────────────────────────
    "Benchmarks.MediatorSGSendNoBehaviorsBenchmarks"    = "Mediator (Source Gen) - Send (No Behaviors)"
    "Benchmarks.MediatorSGSendBenchmarks"               = "Mediator (Source Gen) - Send (Behaviors)"
    "Benchmarks.MediatorSGSendObjectBenchmarks"         = "Mediator (Source Gen) - Send (Object)"
    "Benchmarks.MediatorSGPublishBenchmarks"            = "Mediator (Source Gen) - Publish"
    "Benchmarks.MediatorSGPublishObjectBenchmarks"       = "Mediator (Source Gen) - Publish (Object)"
    "Benchmarks.MediatorSGStreamBenchmarks"             = "Mediator (Source Gen) - Stream"
    "Benchmarks.MediatorSGConcurrencyBenchmarks"        = "Mediator (Source Gen) - Concurrency"
    "Benchmarks.MediatorSGColdStartBenchmarks"          = "Mediator (Source Gen) - Cold Start"
    "Benchmarks.MediatorSGRealisticPipelineBenchmarks"    = "Mediator (Source Gen) - Realistic Pipeline"
    "Benchmarks.MediatorSGBehaviorScalingBenchmarks"     = "Mediator (Source Gen) - Behavior Scaling"
}

# ── "All Libraries" sections: concatenate isolated tables by operation ───
# Each entry maps a section title to the list of isolated benchmark keys whose
# tables are merged (data rows only) under a shared header row.
$combinedSections = [ordered]@{
    "Send - All Libraries (No Behaviors)" = @(
        "Benchmarks.DSoftSendNoBehaviorsBenchmarks",
        "Benchmarks.MediatRSendNoBehaviorsBenchmarks",
        "Benchmarks.DispatchRSendNoBehaviorsBenchmarks",
        "Benchmarks.MediatorSGSendNoBehaviorsBenchmarks"
    )
    "Send - All Libraries (Behaviors)" = @(
        "Benchmarks.DSoftSendBenchmarks",
        "Benchmarks.MediatRSendBenchmarks",
        "Benchmarks.DispatchRSendBenchmarks",
        "Benchmarks.MediatorSGSendBenchmarks"
    )
    "Send (Object) - All Libraries" = @(
        "Benchmarks.DSoftSendObjectBenchmarks",
        "Benchmarks.MediatRSendObjectBenchmarks",
        "Benchmarks.MediatorSGSendObjectBenchmarks"
    )
    "Publish - All Libraries" = @(
        "Benchmarks.DSoftPublishBenchmarks",
        "Benchmarks.MediatRPublishBenchmarks",
        "Benchmarks.DispatchRPublishBenchmarks",
        "Benchmarks.MediatorSGPublishBenchmarks"
    )
    "Publish (Object) - All Libraries" = @(
        "Benchmarks.DSoftPublishObjectBenchmarks",
        "Benchmarks.MediatRPublishObjectBenchmarks",
        "Benchmarks.DispatchRPublishObjectBenchmarks",
        "Benchmarks.MediatorSGPublishObjectBenchmarks"
    )
    "Stream - All Libraries" = @(
        "Benchmarks.DSoftStreamBenchmarks",
        "Benchmarks.MediatRStreamBenchmarks",
        "Benchmarks.DispatchRStreamBenchmarks",
        "Benchmarks.MediatorSGStreamBenchmarks"
    )
    "Concurrency - All Libraries" = @(
        "Benchmarks.DSoftConcurrencyBenchmarks",
        "Benchmarks.MediatRConcurrencyBenchmarks",
        "Benchmarks.DispatchRConcurrencyBenchmarks",
        "Benchmarks.MediatorSGConcurrencyBenchmarks"
    )
    "Cold Start - All Libraries" = @(
        "Benchmarks.DSoftColdStartBenchmarks",
        "Benchmarks.MediatRColdStartBenchmarks",
        "Benchmarks.DispatchRColdStartBenchmarks",
        "Benchmarks.MediatorSGColdStartBenchmarks"
    )
    "Realistic Pipeline - All Libraries" = @(
        "Benchmarks.DSoftRealisticPipelineBenchmarks",
        "Benchmarks.MediatRRealisticPipelineBenchmarks",
        "Benchmarks.DispatchRRealisticPipelineBenchmarks",
        "Benchmarks.MediatorSGRealisticPipelineBenchmarks"
    )
}

if (-not (Test-Path $ResultsDir)) {
    Write-Error "Results directory not found: $ResultsDir"
    exit 1
}

# Extract environment info from the first available report
$envInfo = ""
$firstReport = Get-ChildItem "$ResultsDir\*-report-github.md" | Select-Object -First 1
if ($firstReport) {
    $content = Get-Content $firstReport.FullName -Raw -Encoding UTF8
    if ($content -match '(?s)```\s*\r?\n(.+?)```') {
        $envInfo = $Matches[1].Trim()
    }
}

# Extract ALL markdown table lines (lines starting with |) from a report file
function Get-Table([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    $lines = Get-Content $path -Encoding UTF8 | Where-Object { $_ -match '^\|' }
    if ($lines.Count -gt 0) { return ($lines -join "`n") }
    return $null
}

# Extract only data rows from a table (skip header + separator = first 2 lines)
function Get-DataRows([string]$path) {
    if (-not (Test-Path $path)) { return @() }
    $lines = @(Get-Content $path -Encoding UTF8 | Where-Object { $_ -match '^\|' })
    if ($lines.Count -gt 2) { return $lines[2..($lines.Count - 1)] }
    return @()
}

# Extract header rows (first 2 lines: column names + separator)
function Get-HeaderRows([string]$path) {
    if (-not (Test-Path $path)) { return @() }
    $lines = @(Get-Content $path -Encoding UTF8 | Where-Object { $_ -match '^\|' })
    if ($lines.Count -ge 2) { return $lines[0..1] }
    return @()
}

# ── Column normalization helpers (for "All Libraries" combined tables) ────

# Canonical column order emitted by BenchmarkDotNet
$canonicalOrder = @('Method','Mean','Error','StdDev','Median','Ratio','RatioSD','Rank','Gen0','Gen1','Gen2','Allocated','Alloc Ratio')

# Parse trimmed column names from a markdown header line
function Get-ColumnNames([string]$headerLine) {
    $parts = @($headerLine -split '\|')
    $cols = @()
    for ($i = 1; $i -lt $parts.Count - 1; $i++) {
        $trimmed = $parts[$i].Trim()
        if ($trimmed) { $cols += $trimmed }
    }
    return $cols
}

# Remap a data row from source columns to superset columns, filling '-' for missing
function Remap-Row([string]$row, [string[]]$srcCols, [string[]]$superCols) {
    $parts = @($row -split '\|')
    $cells = @()
    for ($i = 1; $i -lt $parts.Count - 1; $i++) { $cells += $parts[$i].Trim() }

    $map = @{}
    for ($i = 0; $i -lt [Math]::Min($srcCols.Count, $cells.Count); $i++) {
        $map[$srcCols[$i]] = $cells[$i]
    }

    $out = foreach ($col in $superCols) {
        if ($map.ContainsKey($col)) { $map[$col] } else { '-' }
    }
    return '| ' + ($out -join ' | ') + ' |'
}

# Build the markdown
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# Benchmarks")
[void]$sb.AppendLine()
if ($Tfm) { [void]$sb.AppendLine("Target framework: ``$Tfm``") }
[void]$sb.AppendLine()

# The net11 numbers are not reproducible on an older preview, so the document has to say which one
# produced them. Emitted from the generator rather than pasted into the output, which is regenerated.
if ($Tfm -like 'net11*') {
    [void]$sb.AppendLine('> **These numbers need .NET 11 Preview 7 or later, with runtime-async on** (the switch lives in')
    [void]$sb.AppendLine('> `benchmarks/Directory.Build.props`). Two Preview 7 runtime changes carry most of the difference')
    [void]$sb.AppendLine('> against `net10.0`, and neither exists in Preview 6:')
    [void]$sb.AppendLine('>')
    [void]$sb.AppendLine('> - Async methods now go through tiered compilation. Before, they ran tier0 code forever, so every')
    [void]$sb.AppendLine('>   `async` method on the dispatch path stayed unoptimized no matter how hot it got.')
    [void]$sb.AppendLine('> - A hot `await` on an already-completed task folds into a status-flag check instead of a helper')
    [void]$sb.AppendLine('>   call, which is the shape this library is built around.')
    [void]$sb.AppendLine('>')
    [void]$sb.AppendLine('> Preview 7 also fixed a flag check that skipped saving and restoring the async context across an')
    [void]$sb.AppendLine('> `await` in a `ValueTask`-returning method. That is the entire dispatch surface here, so anything')
    [void]$sb.AppendLine('> measured on an earlier preview with runtime-async on is unreliable for code that reads')
    [void]$sb.AppendLine('> `AsyncLocal`, `Activity.Current` or `IHttpContextAccessor`.')
    [void]$sb.AppendLine()
}

if ($envInfo) {
    [void]$sb.AppendLine("``````")
    [void]$sb.AppendLine($envInfo)
    [void]$sb.AppendLine("``````")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("> **Note:** Each library's benchmarks run in **isolated processes** (only that library active).")
    [void]$sb.AppendLine("> The ``All Libraries`` sections below concatenate those isolated results for easy comparison.")
    [void]$sb.AppendLine()
}

$found = 0

# ── Note attached to every startup table ─────────────────────────────────
# Most of a startup figure is .NET and the DI container, which every library on this page pays
# equally. Published as a single number it reads as the mediator's cost, and it is not: measured,
# building the provider and resolving IMediator was 13.7 ms of a 17.2 ms total. The paired rows exist
# so a reader can see which part belongs to the library — and this says so where they will actually
# read it, rather than in a comment in the benchmark source.
$coldStartNote = @'
> **Read the gap, not the total.** `Startup_ContainerOnly` builds the DI container and resolves the
> mediator. `Startup_WithFirstDispatch` does the same and then dispatches one request. Nearly all of
> either number is .NET runtime startup and DI container construction, which every library on this
> page pays alike. **The difference between the two rows is the part that belongs to the library.**
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean — and
> the first row executed also absorbs the machine's own file-cache warm-up, which inflates it and so
> understates the gap.
'@

# ── Emit per-library isolated sections ───────────────────────────────────
foreach ($key in $titleMap.Keys) {
    $file = Join-Path $ResultsDir "$key-report-github.md"
    $table = Get-Table $file
    if ($table) {
        $found++
        [void]$sb.AppendLine("## $($titleMap[$key])")
        [void]$sb.AppendLine()
        if ($key -like "*ColdStart*") {
            [void]$sb.AppendLine($coldStartNote)
            [void]$sb.AppendLine()
        }
        [void]$sb.AppendLine($table)
        [void]$sb.AppendLine()
    }
}

# ── Emit "All Libraries" combined sections (column-normalized) ───────────
foreach ($title in $combinedSections.Keys) {
    $keys = $combinedSections[$title]

    # 1. Collect headers and data rows per file, build column superset
    $fileColumns = @{}
    $fileDataRows = [ordered]@{}
    $allColumnSet = [ordered]@{}

    foreach ($k in $keys) {
        $file = Join-Path $ResultsDir "$k-report-github.md"
        $hdr = Get-HeaderRows $file
        if ($hdr.Count -ge 2) {
            $cols = Get-ColumnNames $hdr[0]
            $fileColumns[$k] = $cols
            foreach ($c in $cols) {
                if (-not $allColumnSet.Contains($c)) { $allColumnSet[$c] = $true }
            }
        }
        $rows = @(Get-DataRows $file | Where-Object { $_ -match '[a-zA-Z0-9]' })
        if ($rows.Count -gt 0) { $fileDataRows[$k] = $rows }
    }

    if ($allColumnSet.Count -eq 0 -or $fileDataRows.Count -eq 0) { continue }

    # 2. Build superset columns in canonical order
    $superCols = @($canonicalOrder | Where-Object { $allColumnSet.Contains($_) })

    # 3. Build header row and separator
    $headerRow = '| ' + ($superCols -join ' | ') + ' |'
    $sepCells = foreach ($col in $superCols) {
        if ($col -eq 'Method') { '---' } else { '---:' }
    }
    $sepRow = '| ' + ($sepCells -join ' | ') + ' |'

    # 4. Build empty row for visual group separation
    $emptyRow = '|' + (' |' * $superCols.Count)

    $found++
    [void]$sb.AppendLine("## $title")
    [void]$sb.AppendLine()
    # The combined table is the one people actually compare libraries in, so it needs the caveat
    # more than the per-library ones do.
    if ($title -like "*Cold Start*") {
        [void]$sb.AppendLine($coldStartNote)
        [void]$sb.AppendLine()
    }
    [void]$sb.AppendLine($headerRow)
    [void]$sb.AppendLine($sepRow)

    $groupIndex = 0
    foreach ($k in $keys) {
        if (-not $fileDataRows.Contains($k)) { continue }
        if ($groupIndex -gt 0) { [void]$sb.AppendLine($emptyRow) }
        $srcCols = $fileColumns[$k]
        foreach ($row in $fileDataRows[$k]) {
            [void]$sb.AppendLine((Remap-Row $row $srcCols $superCols))
        }
        $groupIndex++
    }

    [void]$sb.AppendLine()
}

if ($found -eq 0) {
    Write-Warning "No benchmark report files found in $ResultsDir"
    exit 1
}

# Running instructions
[void]$sb.AppendLine("## Running Benchmarks")
[void]$sb.AppendLine()
[void]$sb.AppendLine("Close Visual Studio and heavy apps before running for best accuracy.")
[void]$sb.AppendLine()
[void]$sb.AppendLine("``````sh")
[void]$sb.AppendLine("# All benchmarks sequentially (recommended)")
[void]$sb.AppendLine("benchmarks\run-all-benchmarks.cmd")
[void]$sb.AppendLine("``````")
[void]$sb.AppendLine()
[void]$sb.AppendLine("Results are saved to ``benchmarks/BenchmarkDotNet.Artifacts/<tfm>/results/``.")

$sb.ToString() | Set-Content $OutputFile -Encoding UTF8 -NoNewline
Write-Host "Generated: $OutputFile ($found sections)"
