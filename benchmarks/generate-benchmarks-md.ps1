# Generates benchmarks/BENCHMARKS.md from BenchmarkDotNet result files.
# Called automatically by run-all-benchmarks.cmd after all benchmarks complete.

param(
    [string]$ResultsDir = (Join-Path $PSScriptRoot "BenchmarkDotNet.Artifacts\results"),
    [string]$OutputFile = (Join-Path $PSScriptRoot "BENCHMARKS.md")
)

$titleMap = [ordered]@{
    "Benchmarks.DSoftSendNoBehaviorsBenchmarks"     = "DSoft - Send (No Behaviors)"
    "Benchmarks.DSoftSendBenchmarks"                = "DSoft - Send (Behaviors)"
    "Benchmarks.MediatRSendNoBehaviorsBenchmarks"   = "MediatR - Send (No Behaviors)"
    "Benchmarks.MediatRSendBenchmarks"              = "MediatR - Send (Behaviors)"
    "Benchmarks.DispatchRSendNoBehaviorsBenchmarks" = "DispatchR - Send (No Behaviors)"
    "Benchmarks.DispatchRSendBenchmarks"            = "DispatchR - Send (Behaviors)"
    "Benchmarks.PublishBenchmarks"                  = "Publish"
    "Benchmarks.StreamBenchmarks"                   = "Stream"
    "Benchmarks.ConcurrencyBenchmarks"              = "Concurrency"
    "Benchmarks.ColdStartBenchmarks"                = "Cold Start"
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

# Extract markdown table (lines starting with |) from a report file
function Get-Table([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    $lines = Get-Content $path -Encoding UTF8 | Where-Object { $_ -match '^\|' }
    if ($lines.Count -gt 0) { return ($lines -join "`n") }
    return $null
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
}

$found = 0
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
