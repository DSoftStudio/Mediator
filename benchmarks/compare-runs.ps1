# Compares two BenchmarkDotNet result directories and reports the delta per method.
#
# Absolute means drift on a desktop -- two runs of the SAME build minutes apart have differed by
# ~9% here -- so a raw before/after column is not on its own evidence of anything. Every suite has
# a baseline row (DirectCall, Direct_Publish, ...) marked Ratio 1.00; the overhead ABOVE that
# baseline, measured inside the same run, is what survives drift. Both are reported: Mean for
# scale, overhead-vs-baseline for the comparison you can actually trust.
#
#   .\compare-runs.ps1 -Before ..\..\baseline\benchmarks\BenchmarkDotNet.Artifacts\results `
#                      -After  .\BenchmarkDotNet.Artifacts\net10.0\results

param(
    [Parameter(Mandatory = $true)][string]$Before,
    [Parameter(Mandatory = $true)][string]$After,
    [string]$OutputFile = ""
)

function ToNs([string]$value) {
    if (-not $value) { return $null }
    $t = $value.Trim() -replace ',', ''
    if ($t -match '^(?<n>[0-9.]+)\s*(?<u>ns|us|μs|ms|s)$') {
        $n = [double]$Matches['n']
        switch ($Matches['u']) {
            'ns' { return $n }
            'us' { return $n * 1e3 }
            'μs' { return $n * 1e3 }
            'ms' { return $n * 1e6 }
            's'  { return $n * 1e9 }
        }
    }
    return $null
}

# Method name -> @{ Mean; Ratio; Allocated }, plus the suite's baseline mean (the Ratio 1.00 row).
function Read-Suite([string]$path) {
    if (-not (Test-Path $path)) { return $null }

    $rows = @{}
    $baselines = @{}
    $baselineSamples = @{}
    $header = $null
    $idx = @{}

    foreach ($line in Get-Content $path) {
        if ($line -notmatch '^\s*\|') { continue }
        $cells = ($line -split '\|')[1..(($line -split '\|').Count - 2)] | ForEach-Object { $_.Trim() }
        if ($cells.Count -lt 2) { continue }

        if (-not $header) {
            if ($cells[0] -eq 'Method') {
                $header = $cells
                for ($i = 0; $i -lt $cells.Count; $i++) { $idx[$cells[$i]] = $i }
            }
            continue
        }
        if ($cells[0] -match '^-+:?$' -or $cells[0] -match '^:?-+') { continue }   # separator
        if (-not $cells[0]) { continue }                                          # category spacer

        $mean = ToNs $cells[$idx['Mean']]
        if ($null -eq $mean) { continue }

        $ratio = $null
        if ($idx.ContainsKey('Ratio')) { [double]::TryParse(($cells[$idx['Ratio']] -replace ',', ''), [ref]$ratio) | Out-Null }

        $alloc = if ($idx.ContainsKey('Allocated')) { $cells[$idx['Allocated']] } else { '' }

        # A suite can carry SEVERAL baselines: DSoftConcurrencyBenchmarks groups by category and
        # marks one Ratio 1.00 row per group. Taking the last one would measure FanOut against the
        # Throughput baseline and report a ~1200 ns "overhead" that means nothing.
        $cat = if ($idx.ContainsKey('Categories')) { $cells[$idx['Categories']] } else { '' }

        $rows[$cells[0]] = @{ Mean = $mean; Ratio = $ratio; Allocated = $alloc; Category = $cat }

        # Do NOT pick the baseline by "Ratio is 1.00". Ratio is printed to two decimals, so in the
        # FanOut category BOTH rows read 1.00 (they are 0.2% apart) and whichever is taken last wins
        # -- which is how Direct_FanOut ended up reporting a -2.96 ns overhead against its rival
        # instead of 0 against itself. Mean/Ratio recovers the baseline from any row, so collect them
        # all and take the median: rounding error cancels and no row has to be identified.
        if ($null -ne $ratio -and $ratio -gt 0) {
            if (-not $baselineSamples.ContainsKey($cat)) { $baselineSamples[$cat] = [System.Collections.ArrayList]::new() }
            [void]$baselineSamples[$cat].Add($mean / $ratio)
        }
    }

    if ($rows.Count -eq 0) { return $null }

    foreach ($c in $baselineSamples.Keys) {
        $sorted = @($baselineSamples[$c] | Sort-Object)
        $n = $sorted.Count
        # True median, averaging the middle pair on an even count. With only two candidates that
        # both print Ratio 1.00 -- FanOut -- the data genuinely does not say which is the baseline,
        # and averaging bounds the error by the spread between them (~1.5 ns of 1300, 0.1%) instead
        # of picking one arbitrarily and being wrong by the whole gap.
        $baselines[$c] = if ($n % 2 -eq 1) { $sorted[[int]([math]::Floor($n / 2))] }
                         else { ($sorted[$n / 2 - 1] + $sorted[$n / 2]) / 2 }
    }

    return @{ Rows = $rows; Baselines = $baselines }
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# Benchmark comparison")
[void]$sb.AppendLine()
[void]$sb.AppendLine("- Before: ``$Before``")
[void]$sb.AppendLine("- After:  ``$After``")
[void]$sb.AppendLine()
[void]$sb.AppendLine("``Mean`` drifts between runs on a desktop; ``vs base`` is each method's overhead above")
[void]$sb.AppendLine("its own suite's baseline row, measured inside the same run, and is the column to read.")
[void]$sb.AppendLine()

$suites = Get-ChildItem -Path $After -Filter '*-report-github.md' -ErrorAction SilentlyContinue |
    Sort-Object Name

foreach ($suite in $suites) {
    $b = Read-Suite (Join-Path $Before $suite.Name)
    $a = Read-Suite $suite.FullName
    if (-not $a) { continue }

    $title = $suite.Name -replace '^Benchmarks\.', '' -replace '-report-github\.md$', ''

    if (-not $b) {
        [void]$sb.AppendLine("## $title")
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("_Not present in the before run - nothing to compare._")
        [void]$sb.AppendLine()
        continue
    }

    [void]$sb.AppendLine("## $title")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("| Method | Mean before | Mean after | Mean delta | vs base before | vs base after | vs base delta | Alloc before | Alloc after |")
    [void]$sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|")

    foreach ($m in ($a.Rows.Keys | Sort-Object)) {
        $af = $a.Rows[$m]
        if (-not $b.Rows.ContainsKey($m)) {
            [void]$sb.AppendLine("| $m | - | $([math]::Round($af.Mean,3)) | new | - | - | - | - | $($af.Allocated) |")
            continue
        }
        $bf = $b.Rows[$m]

        $dMean = $af.Mean - $bf.Mean
        $pct = if ($bf.Mean -ne 0) { 100 * $dMean / $bf.Mean } else { 0 }

        $bBase = $b.Baselines[$bf.Category]
        $aBase = $a.Baselines[$af.Category]
        $obBefore = if ($null -ne $bBase) { $bf.Mean - $bBase } else { $null }
        $obAfter = if ($null -ne $aBase) { $af.Mean - $aBase } else { $null }
        $obDelta = if (($null -ne $obBefore) -and ($null -ne $obAfter)) { $obAfter - $obBefore } else { $null }

        $f = { param($v) if ($null -eq $v) { '-' } else { '{0:N3}' -f $v } }

        [void]$sb.AppendLine(
            "| $m | $(& $f $bf.Mean) | $(& $f $af.Mean) | $('{0:+0.000;-0.000;0}' -f $dMean) ($('{0:+0.0;-0.0;0}' -f $pct)%) " +
            "| $(& $f $obBefore) | $(& $f $obAfter) | $(if ($null -eq $obDelta) { '-' } else { '{0:+0.000;-0.000;0}' -f $obDelta }) " +
            "| $($bf.Allocated) | $($af.Allocated) |")
    }

    $onlyBefore = $b.Rows.Keys | Where-Object { -not $a.Rows.ContainsKey($_) }
    if ($onlyBefore) {
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("_Only in the before run: $($onlyBefore -join ', ')_")
    }
    [void]$sb.AppendLine()
}

$text = $sb.ToString()
if ($OutputFile) {
    $text | Set-Content $OutputFile -Encoding UTF8
    Write-Host "Wrote: $OutputFile"
}
else {
    Write-Output $text
}
