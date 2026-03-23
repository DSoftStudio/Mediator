# Generates benchmarks/BENCHMARKS.md from BenchmarkDotNet result files.
# Called automatically by run-all-benchmarks.cmd after all benchmarks complete.
# "All Libraries" sections are assembled by concatenating isolated per-library tables.

param(
    [string]$ResultsDir = (Join-Path $PSScriptRoot "BenchmarkDotNet.Artifacts\results"),
    [string]$OutputFile = (Join-Path $PSScriptRoot "BENCHMARKS.md")
)

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
    # ── DispatchR ─────────────────────────────────────────────────
    "Benchmarks.DispatchRSendNoBehaviorsBenchmarks"     = "DispatchR - Send (No Behaviors)"
    "Benchmarks.DispatchRSendBenchmarks"                = "DispatchR - Send (Behaviors)"
    "Benchmarks.DispatchRPublishBenchmarks"             = "DispatchR - Publish"
    "Benchmarks.DispatchRPublishObjectBenchmarks"        = "DispatchR - Publish (Object)"
    "Benchmarks.DispatchRStreamBenchmarks"              = "DispatchR - Stream"
    "Benchmarks.DispatchRConcurrencyBenchmarks"         = "DispatchR - Concurrency"
    "Benchmarks.DispatchRColdStartBenchmarks"           = "DispatchR - Cold Start"
    "Benchmarks.DispatchRRealisticPipelineBenchmarks"     = "DispatchR - Realistic Pipeline"
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

# Build the markdown
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# Benchmarks")
[void]$sb.AppendLine()

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

# ── Emit per-library isolated sections ───────────────────────────────────
foreach ($key in $titleMap.Keys) {
    $file = Join-Path $ResultsDir "$key-report-github.md"
    $table = Get-Table $file
    if ($table) {
        $found++
        [void]$sb.AppendLine("## $($titleMap[$key])")
        [void]$sb.AppendLine()
        [void]$sb.AppendLine($table)
        [void]$sb.AppendLine()
    }
}

# ── Emit "All Libraries" combined sections (concatenated from isolated) ──
foreach ($title in $combinedSections.Keys) {
    $keys = $combinedSections[$title]
    $headerEmitted = $false
    $header = @()
    $libraryGroups = @()

    foreach ($k in $keys) {
        $file = Join-Path $ResultsDir "$k-report-github.md"
        if (-not $headerEmitted) {
            $header = Get-HeaderRows $file
            if ($header.Count -ge 2) { $headerEmitted = $true }
        }
        $rows = Get-DataRows $file
        $rows = @($rows | Where-Object { $_ -match '[a-zA-Z0-9]' })
        if ($rows.Count -gt 0) { $libraryGroups += ,@($rows) }
    }

    if ($headerEmitted -and $libraryGroups.Count -gt 0) {
        $found++
        [void]$sb.AppendLine("## $title")
        [void]$sb.AppendLine()
        [void]$sb.AppendLine(($header -join "`n"))

        # Build an empty separator row matching the table's column count
        $colCount = ($header[0] -replace '^\||\|$' -split '\|').Count
        $sepRow = '|' + (' |' * $colCount)

        for ($i = 0; $i -lt $libraryGroups.Count; $i++) {
            if ($i -gt 0) { [void]$sb.AppendLine($sepRow) }
            [void]$sb.AppendLine(($libraryGroups[$i] -join "`n"))
        }

        [void]$sb.AppendLine()
    }
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
[void]$sb.AppendLine("Results are saved to ``benchmarks/BenchmarkDotNet.Artifacts/results/``.")

$sb.ToString() | Set-Content $OutputFile -Encoding UTF8 -NoNewline
Write-Host "Generated: $OutputFile ($found sections)"
