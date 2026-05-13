param(
    [ValidateSet('og-zeppelin', 'brd', 'all')]
    [string]$Route = 'all',
    [string]$OutRoot = 'tmp\test-runtime\visualization\pathfinding',
    [string]$DataDir = $(if ($env:WWOW_DATA_DIR) { $env:WWOW_DATA_DIR } else { 'D:\MaNGOS\data' })
)

$ErrorActionPreference = 'Stop'
$script:Invariant = [Globalization.CultureInfo]::InvariantCulture
$script:RepoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName

function Resolve-RepoPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }
    return [IO.Path]::GetFullPath((Join-Path $script:RepoRoot $Path))
}

function Format-Number([double]$Value) {
    $Value.ToString('F3', $script:Invariant)
}

function Get-PolyStats([string]$CsvPath) {
    if (-not (Test-Path $CsvPath)) {
        return [pscustomobject]@{ Exists = $false; Count = 0 }
    }

    $rows = Import-Csv $CsvPath | ForEach-Object {
        [pscustomobject]@{
            polyIndex = [int]$_.polyIndex
            centroidX = [double]$_.centroidX
            centroidY = [double]$_.centroidY
            centroidZ = [double]$_.centroidZ
            zRange = [double]$_.zRange
            maxEdge2D = [double]$_.maxEdge2D
            horizontalArea2D = [double]$_.horizontalArea2D
        }
    }

    $worstZ = $rows | Sort-Object zRange -Descending | Select-Object -First 1
    $worstEdge = $rows | Sort-Object maxEdge2D -Descending | Select-Object -First 1
    $largest = $rows | Sort-Object horizontalArea2D -Descending | Select-Object -First 1
    [pscustomobject]@{
        Exists = $true
        Count = $rows.Count
        WorstZ = $worstZ
        WorstEdge = $worstEdge
        Largest = $largest
    }
}

function Get-GoLines([string]$LogPath) {
    if (-not (Test-Path $LogPath)) {
        return @("missing log: $LogPath")
    }
    return @(Select-String -Path $LogPath -Pattern '\[GO\]' | ForEach-Object { $_.Line.Trim() })
}

function Add-StatsLines([System.Collections.Generic.List[string]]$Lines, [string]$Title, $Stats) {
    $Lines.Add("### $Title")
    $Lines.Add("")
    if (-not $Stats.Exists) {
        $Lines.Add("- Missing polygon CSV.")
        $Lines.Add("")
        return
    }

    $Lines.Add("- Polygons: $($Stats.Count)")
    if ($null -ne $Stats.WorstZ) {
        $p = $Stats.WorstZ
        $Lines.Add(("- Worst zRange: poly {0}, zRange={1}y, centroid=({2},{3},{4}), maxEdge2D={5}y, area2D={6}" -f `
            $p.polyIndex, (Format-Number $p.zRange), (Format-Number $p.centroidX), (Format-Number $p.centroidY), (Format-Number $p.centroidZ), `
            (Format-Number $p.maxEdge2D), (Format-Number $p.horizontalArea2D)))
    }
    if ($null -ne $Stats.WorstEdge) {
        $p = $Stats.WorstEdge
        $Lines.Add(("- Longest edge: poly {0}, maxEdge2D={1}y, zRange={2}y, area2D={3}" -f `
            $p.polyIndex, (Format-Number $p.maxEdge2D), (Format-Number $p.zRange), (Format-Number $p.horizontalArea2D)))
    }
    if ($null -ne $Stats.Largest) {
        $p = $Stats.Largest
        $Lines.Add(("- Largest horizontal area: poly {0}, area2D={1}, zRange={2}y, maxEdge2D={3}y" -f `
            $p.polyIndex, (Format-Number $p.horizontalArea2D), (Format-Number $p.zRange), (Format-Number $p.maxEdge2D)))
    }
    $Lines.Add("")
}

function Invoke-MmapVisualizePoly(
    [string]$Mmtile,
    [int]$PolyIndex,
    [string]$OutObj,
    [string]$OutCsv,
    [object[]]$Markers)
{
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutObj) | Out-Null
    $argsList = @(
        'run', '--no-build', '--project', (Join-Path $script:RepoRoot 'tools\MmapVisualize\MmapVisualize.csproj'), '--',
        '--mmtile', $Mmtile,
        '--out', $OutObj,
        '--polys', $PolyIndex.ToString($script:Invariant),
        '--poly-report', $OutCsv,
        '--quiet'
    )
    foreach ($m in $Markers) {
        $coord = '{0},{1},{2}' -f `
            ([double]$m.X).ToString('F4', $script:Invariant), `
            ([double]$m.Y).ToString('F4', $script:Invariant), `
            ([double]$m.Z).ToString('F4', $script:Invariant)
        $argsList += @('--mark', $coord, $m.Label)
    }

    & dotnet @argsList
    if ($LASTEXITCODE -ne 0) {
        throw "MmapVisualize failed while exporting suspicious poly $PolyIndex from $Mmtile"
    }
}

function New-Marker([double]$X, [double]$Y, [double]$Z, [string]$Label) {
    [pscustomobject]@{ X = $X; Y = $Y; Z = $Z; Label = $Label }
}

function Write-OgSummary {
    $latest = Resolve-RepoPath (Join-Path $OutRoot 'og-zeppelin\latest')
    $analysis = Join-Path $latest 'analysis'
    New-Item -ItemType Directory -Force -Path $analysis | Out-Null

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# Orgrimmar Zeppelin Tower Visual Summary")
    $lines.Add("")
    $lines.Add("Generated: $([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss zzz'))")
    $lines.Add("")
    $lines.Add("Inspection target: top ramp/deck where screenshots show a mostly flat deck but mmap polygons appear degraded.")
    $lines.Add("")

    $topDeckStats = Get-PolyStats (Join-Path $analysis 'mmap_top_ramp_deck_polys.csv')
    Add-StatsLines $lines "Top Ramp / Deck Crop" $topDeckStats
    Add-StatsLines $lines "Top Ramp / Deck Reachable From Route Start" (Get-PolyStats (Join-Path $analysis 'mmap_top_ramp_deck_reachable_polys.csv'))
    Add-StatsLines $lines "Top Ramp / Deck Unreachable From Route Start" (Get-PolyStats (Join-Path $analysis 'mmap_top_ramp_deck_unreachable_polys.csv'))

    if ($topDeckStats.Exists -and $null -ne $topDeckStats.WorstZ) {
        Invoke-MmapVisualizePoly `
            (Join-Path $DataDir 'mmaps\0012940.mmtile') `
            $topDeckStats.WorstZ.polyIndex `
            (Join-Path $analysis 'suspicious_poly_top_ramp_deck.obj') `
            (Join-Path $analysis 'suspicious_poly_top_ramp_deck.csv') `
            @(
                (New-Marker 1335.2 -4644.4 53.5 'top_ramp_lip'),
                (New-Marker 1338.13 -4645.96 51.6 'deck_lip_stall'),
                (New-Marker 1320.14 -4653.16 53.89 'boarding_pos')
            )
        $lines.Add("- Suspicious polygon OBJ: analysis/suspicious_poly_top_ramp_deck.obj")
        $lines.Add("")
    }

    $lines.Add("### GO Bake Evidence")
    $lines.Add("")
    foreach ($line in Get-GoLines (Join-Path $latest 'logs\mmapgen_tile_0012940.log')) {
        $lines.Add("- $line")
    }
    $lines.Add("")
    $lines.Add("### Current Interpretation")
    $lines.Add("")
    $lines.Add("- The corrected tile is Orgrimmar map 1 tile 40,29 / runtime 0012940.mmtile.")
    $lines.Add("- The GO bake log proves static GameObject placement data was loaded for this tile.")
    $topDeckLooksSane = $topDeckStats.Exists `
        -and $null -ne $topDeckStats.Largest `
        -and $null -ne $topDeckStats.WorstZ `
        -and $topDeckStats.Largest.horizontalArea2D -lt 80.0 `
        -and $topDeckStats.WorstZ.zRange -lt 1.5
    if ($topDeckLooksSane) {
        $lines.Add("- The focused deck crop no longer shows the previously confirmed large bridge polygon; the remaining worst zRange/area values are below the mesh-quality regression thresholds.")
        $lines.Add("- Continue with Detour corridor/path selection only if route queries still choose a bad corridor on this corrected tile.")
    } else {
        $lines.Add("- A large/high-span polygon on the deck crop is mesh-side evidence to inspect before changing BotRunner path execution.")
    }

    $lines | Set-Content -Path (Join-Path $analysis 'summary.md') -Encoding UTF8
}

function Write-BrdSummary {
    $latest = Resolve-RepoPath (Join-Path $OutRoot 'brd\latest')
    $analysis = Join-Path $latest 'analysis'
    New-Item -ItemType Directory -Force -Path $analysis | Out-Null

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# BRD / BRM Visual Summary")
    $lines.Add("")
    $lines.Add("Generated: $([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss zzz'))")
    $lines.Add("")
    $lines.Add("Inspection target: Flame Crest to BRD approach plus known BRM south-face trap.")
    $lines.Add("")

    $brdStats = Get-PolyStats (Join-Path $analysis 'brd_approach_mmap_crop_polys.csv')
    $brmStats = Get-PolyStats (Join-Path $analysis 'brm_south_trap_mmap_crop_polys.csv')
    Add-StatsLines $lines "BRD Approach Crop" $brdStats
    Add-StatsLines $lines "BRM South Trap Crop" $brmStats

    if ($brdStats.Exists -and $null -ne $brdStats.WorstZ) {
        Invoke-MmapVisualizePoly `
            (Join-Path $DataDir 'mmaps\0004533.mmtile') `
            $brdStats.WorstZ.polyIndex `
            (Join-Path $analysis 'suspicious_poly_brd_approach.obj') `
            (Join-Path $analysis 'suspicious_poly_brd_approach.csv') `
            @(
                (New-Marker -7187 -958 254 'brd_approach'),
                (New-Marker -7179 -921 165 'literal_brd_portal')
            )
        $lines.Add("- BRD suspicious polygon OBJ: analysis/suspicious_poly_brd_approach.obj")
    }
    if ($brmStats.Exists -and $null -ne $brmStats.WorstZ) {
        Invoke-MmapVisualizePoly `
            (Join-Path $DataDir 'mmaps\0004634.mmtile') `
            $brmStats.WorstZ.polyIndex `
            (Join-Path $analysis 'suspicious_poly_brm_south_trap.obj') `
            (Join-Path $analysis 'suspicious_poly_brm_south_trap.csv') `
            @(
                (New-Marker -7949.7 -1162.8 170.8 'brm_south_trap_z170_8'),
                (New-Marker -7825.4 -1129.2 133.8 'brm_south_new_trap_z133_8')
            )
        $lines.Add("- BRM suspicious polygon OBJ: analysis/suspicious_poly_brm_south_trap.obj")
    }
    if (($brdStats.Exists -and $null -ne $brdStats.WorstZ) -or ($brmStats.Exists -and $null -ne $brmStats.WorstZ)) {
        $lines.Add("")
    }

    $lines.Add("### GO Bake Evidence")
    $lines.Add("")
    foreach ($log in 'mmapgen_tile_0004533.log', 'mmapgen_tile_0004634.log') {
        foreach ($line in Get-GoLines (Join-Path $latest "logs\$log")) {
            $lines.Add("- ${log}: $line")
        }
    }
    $lines.Add("")
    $lines.Add("### Current Interpretation")
    $lines.Add("")
    $lines.Add("- Large zRange polygons around the BRD/BRM crops point at bake/filter/WMO connectivity before BotRunner execution.")
    $lines.Add("- These tiles currently bake no model-backed GO geometry, only fallback span boxes.")

    $lines | Set-Content -Path (Join-Path $analysis 'summary.md') -Encoding UTF8
}

if ($Route -eq 'og-zeppelin' -or $Route -eq 'all') {
    Write-OgSummary
}
if ($Route -eq 'brd' -or $Route -eq 'all') {
    Write-BrdSummary
}

Write-Host "[summarize-pathfinding-reference] DONE -> $(Resolve-RepoPath $OutRoot)" -ForegroundColor Green
