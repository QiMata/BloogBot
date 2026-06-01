# Phase 0 D2 -- Sweep aggregator.
#
# Walks the per-tile JSON output from phase0-sweep-map.ps1 and produces:
#   - Global affordance histogram (sum across all tiles)
#   - Top-N worst tiles by UnrecoverableNonWalk ratio
#   - Tile count + total segments / paths / errors summary
# Writes a markdown report to tmp/iter-overhaul-phase0/sweep-map<M>-summary.md
# and a JSON dump to sweep-map<M>-aggregate.json.
#
# Idempotent: re-running just rewrites both outputs against the current
# state of the per-tile JSONs. Safe to run on a partial in-flight sweep.
#
# Usage:
#   powershell tools/scripts/phase0-aggregate-sweep.ps1 -MapId 1
#   powershell tools/scripts/phase0-aggregate-sweep.ps1 -MapId 1 -TopN 30

param(
    [Parameter(Mandatory = $true)] [int]$MapId,
    [int]$TopN = 20,
    [string]$OutRoot = 'e:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0'
)

$ErrorActionPreference = 'Stop'

$inDir = Join-Path $OutRoot ('sweep-map' + $MapId)
$summaryFile = Join-Path $OutRoot ('sweep-map' + $MapId + '-summary.md')
$jsonFile = Join-Path $OutRoot ('sweep-map' + $MapId + '-aggregate.json')

if (-not (Test-Path $inDir)) { throw "sweep dir missing: $inDir" }

$tileFiles = @(Get-ChildItem -Path $inDir -Filter 'tile-*.json' | Sort-Object Name)
Write-Host "# aggregating $($tileFiles.Count) tile JSON files from $inDir"

$globalAffordance = @{}
$tileStats = @()
$skippedSentinels = 0
$parseErrors = 0
$totalSegments = 0
$totalNonWalk = 0
$totalUnrecoverable = 0
$totalPaths = 0

foreach ($f in $tileFiles) {
    try {
        $data = Get-Content -Path $f.FullName -Raw | ConvertFrom-Json
    }
    catch {
        $parseErrors++
        continue
    }

    # Sentinel skip files (e.g. iter-11 tile-30-30.json from the hung-validator workaround)
    # have a _skip_reason field and no real validator output. Skip them.
    if ($null -eq $data.AffordanceCounts -or $data.PSObject.Properties['_skip_reason']) {
        $skippedSentinels++
        continue
    }

    # Accumulate affordance counts into global histogram
    foreach ($prop in $data.AffordanceCounts.PSObject.Properties) {
        $key = $prop.Name
        $val = [int]$prop.Value
        if ($globalAffordance.ContainsKey($key)) {
            $globalAffordance[$key] += $val
        }
        else {
            $globalAffordance[$key] = $val
        }
    }

    $totalSegments += $data.TotalSegments
    $totalNonWalk += $data.NonWalkSegments
    $totalUnrecoverable += $data.UnrecoverableNonWalk
    $totalPaths += $data.PathsFound

    $unrecPct = if ($data.TotalSegments -gt 0) {
        [math]::Round(100.0 * $data.UnrecoverableNonWalk / $data.TotalSegments, 2)
    }
    else { 0 }

    $tileStats += [pscustomobject]@{
        TileX = $data.TileX
        TileY = $data.TileY
        TotalSegments = $data.TotalSegments
        NonWalk = $data.NonWalkSegments
        Unrecoverable = $data.UnrecoverableNonWalk
        UnrecoverablePct = $unrecPct
        PathsFound = $data.PathsFound
        FileName = $f.Name
    }
}

# Sort tiles by unrecoverable percentage (descending) for "top worst"
$worstTiles = @($tileStats | Sort-Object UnrecoverablePct -Descending | Select-Object -First $TopN)

# Sort affordance histogram by count descending for stable output
$affordanceSorted = @($globalAffordance.GetEnumerator() | Sort-Object Value -Descending)

# Write JSON aggregate
$aggregate = [pscustomobject]@{
    MapId = $MapId
    GeneratedAt = (Get-Date).ToString('o')
    TilesAggregated = $tileFiles.Count - $skippedSentinels - $parseErrors
    TilesTotal = $tileFiles.Count
    TilesSkippedSentinel = $skippedSentinels
    TileParseErrors = $parseErrors
    TotalSegments = $totalSegments
    TotalNonWalk = $totalNonWalk
    TotalUnrecoverable = $totalUnrecoverable
    TotalPaths = $totalPaths
    GlobalAffordanceCounts = $globalAffordance
    TopWorstTiles = $worstTiles
}
$aggregate | ConvertTo-Json -Depth 6 | Out-File -FilePath $jsonFile -Encoding utf8

# Write markdown summary
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Phase 0 D2 -- Map $MapId Sweep Aggregate")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Coverage")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Metric | Count |")
[void]$sb.AppendLine("|---|---|")
[void]$sb.AppendLine("| Tile JSONs found | $($tileFiles.Count) |")
[void]$sb.AppendLine("| Tiles aggregated | $($tileFiles.Count - $skippedSentinels - $parseErrors) |")
[void]$sb.AppendLine("| Sentinel skips (hung-validator workaround) | $skippedSentinels |")
[void]$sb.AppendLine("| Parse errors | $parseErrors |")
[void]$sb.AppendLine("| Total path queries that found a route | $totalPaths |")
[void]$sb.AppendLine("| Total segments classified | $totalSegments |")
[void]$sb.AppendLine("")

if ($totalSegments -gt 0) {
    $nonWalkPct = [math]::Round(100.0 * $totalNonWalk / $totalSegments, 2)
    $unrecPct = [math]::Round(100.0 * $totalUnrecoverable / $totalSegments, 2)
    [void]$sb.AppendLine("## Global affordance histogram")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| Affordance | Count | % of segments |")
    [void]$sb.AppendLine("|---|---|---|")
    foreach ($e in $affordanceSorted) {
        $pct = [math]::Round(100.0 * $e.Value / $totalSegments, 2)
        [void]$sb.AppendLine("| $($e.Key) | $($e.Value) | $pct% |")
    }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("**Non-Walk total: $totalNonWalk / $totalSegments ($nonWalkPct%)**")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("**Unrecoverable (Blocked + UnsafeDrop + Cliff): $totalUnrecoverable / $totalSegments ($unrecPct%)**")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Proposal §3 Phase 0 expected baseline: Unrecoverable 20-30%. Observed: $unrecPct%.")
    [void]$sb.AppendLine("")
}

[void]$sb.AppendLine("## Top $TopN worst tiles (by Unrecoverable %)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Rank | Tile (X,Y) | Total segs | Unrecov | Unrecov % | Paths found |")
[void]$sb.AppendLine("|---|---|---|---|---|---|")
$rank = 1
foreach ($t in $worstTiles) {
    [void]$sb.AppendLine("| $rank | ($($t.TileX),$($t.TileY)) | $($t.TotalSegments) | $($t.Unrecoverable) | $($t.UnrecoverablePct)% | $($t.PathsFound) |")
    $rank++
}
[void]$sb.AppendLine("")

$sb.ToString() | Out-File -FilePath $summaryFile -Encoding utf8

Write-Host "# wrote $summaryFile"
Write-Host "# wrote $jsonFile"
Write-Host "# tiles aggregated: $($tileFiles.Count - $skippedSentinels - $parseErrors); total segments: $totalSegments; unrecoverable: $totalUnrecoverable"
