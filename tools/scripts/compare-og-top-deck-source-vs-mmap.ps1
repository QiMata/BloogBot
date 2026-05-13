param(
    [string]$LatestDir = 'tmp\test-runtime\visualization\pathfinding\og-zeppelin\latest'
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

function New-Point([double]$X, [double]$Y, [double]$Z) {
    [pscustomobject]@{ X = $X; Y = $Y; Z = $Z }
}

function New-Bounds([double]$MinX, [double]$MinY, [double]$MinZ, [double]$MaxX, [double]$MaxY, [double]$MaxZ) {
    [pscustomobject]@{
        MinX = $MinX; MinY = $MinY; MinZ = $MinZ
        MaxX = $MaxX; MaxY = $MaxY; MaxZ = $MaxZ
    }
}

function Format-Number([double]$Value) {
    $Value.ToString('F2', $script:Invariant)
}

function Convert-ObjVertexToWow([double]$ObjX, [double]$ObjY, [double]$ObjZ) {
    New-Point $ObjX (-1.0 * $ObjZ) $ObjY
}

function Read-ObjTriangles([string]$Path) {
    if (-not (Test-Path $Path)) {
        throw "OBJ not found: $Path"
    }

    $vertices = [System.Collections.Generic.List[object]]::new()
    $triangles = [System.Collections.Generic.List[object]]::new()
    $activeMaterial = 'raw_geometry'

    foreach ($line in [IO.File]::ReadLines((Resolve-Path $Path))) {
        if ($line.StartsWith('usemtl ')) {
            $activeMaterial = $line.Substring(7).Trim()
            continue
        }
        if ($line.StartsWith('v ')) {
            $parts = $line.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
            $vertices.Add((Convert-ObjVertexToWow `
                ([double]::Parse($parts[1], $script:Invariant)) `
                ([double]::Parse($parts[2], $script:Invariant)) `
                ([double]::Parse($parts[3], $script:Invariant))))
            continue
        }
        if (-not $line.StartsWith('f ')) {
            continue
        }

        $parts = $line.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
        $indices = [System.Collections.Generic.List[int]]::new()
        for ($i = 1; $i -lt $parts.Length; $i++) {
            $idx = [int]($parts[$i].Split('/')[0])
            if ($idx -gt 0 -and $idx -le $vertices.Count) {
                $indices.Add($idx)
            }
        }
        if ($indices.Count -lt 3) {
            continue
        }

        for ($i = 1; $i -lt $indices.Count - 1; $i++) {
            $triangles.Add([pscustomobject]@{
                A = $vertices[$indices[0] - 1]
                B = $vertices[$indices[$i] - 1]
                C = $vertices[$indices[$i + 1] - 1]
                Material = $activeMaterial
            })
        }
    }

    return $triangles
}

function Get-NormalUp([object]$Tri) {
    $ux = $Tri.B.X - $Tri.A.X
    $uy = $Tri.B.Y - $Tri.A.Y
    $uz = $Tri.B.Z - $Tri.A.Z
    $vx = $Tri.C.X - $Tri.A.X
    $vy = $Tri.C.Y - $Tri.A.Y
    $vz = $Tri.C.Z - $Tri.A.Z
    $nx = $uy * $vz - $uz * $vy
    $ny = $uz * $vx - $ux * $vz
    $nz = $ux * $vy - $uy * $vx
    $len = [Math]::Sqrt($nx * $nx + $ny * $ny + $nz * $nz)
    if ($len -le 0.000001) {
        return 0.0
    }
    return [Math]::Abs($nz / $len)
}

function Get-TriangleBounds([object]$Tri) {
    $xs = @($Tri.A.X, $Tri.B.X, $Tri.C.X)
    $ys = @($Tri.A.Y, $Tri.B.Y, $Tri.C.Y)
    $zs = @($Tri.A.Z, $Tri.B.Z, $Tri.C.Z)
    New-Bounds (($xs | Measure-Object -Minimum).Minimum) (($ys | Measure-Object -Minimum).Minimum) (($zs | Measure-Object -Minimum).Minimum) `
        (($xs | Measure-Object -Maximum).Maximum) (($ys | Measure-Object -Maximum).Maximum) (($zs | Measure-Object -Maximum).Maximum)
}

function Test-BoundsIntersects([object]$A, [object]$B) {
    return $A.MaxX -ge $B.MinX -and $A.MinX -le $B.MaxX `
        -and $A.MaxY -ge $B.MinY -and $A.MinY -le $B.MaxY `
        -and $A.MaxZ -ge $B.MinZ -and $A.MinZ -le $B.MaxZ
}

function Test-PointInTriangle2D([double]$X, [double]$Y, [object]$Tri) {
    $den = (($Tri.B.Y - $Tri.C.Y) * ($Tri.A.X - $Tri.C.X)) + (($Tri.C.X - $Tri.B.X) * ($Tri.A.Y - $Tri.C.Y))
    if ([Math]::Abs($den) -le 0.000001) {
        return $false
    }

    $u = ((($Tri.B.Y - $Tri.C.Y) * ($X - $Tri.C.X)) + (($Tri.C.X - $Tri.B.X) * ($Y - $Tri.C.Y))) / $den
    $v = ((($Tri.C.Y - $Tri.A.Y) * ($X - $Tri.C.X)) + (($Tri.A.X - $Tri.C.X) * ($Y - $Tri.C.Y))) / $den
    $w = 1.0 - $u - $v
    return $u -ge -0.000001 -and $v -ge -0.000001 -and $w -ge -0.000001
}

function Get-Coverage([object[]]$Triangles, [object]$Crop, [double]$CellSize, [double]$UpThreshold) {
    $cells = @{}
    $usedTriangles = [System.Collections.Generic.List[object]]::new()
    $materialCounts = @{}

    foreach ($tri in $Triangles) {
        if ($tri.Material -eq 'liquid') {
            continue
        }
        if ((Get-NormalUp $tri) -lt $UpThreshold) {
            continue
        }

        $bounds = Get-TriangleBounds $tri
        if (-not (Test-BoundsIntersects $bounds $Crop)) {
            continue
        }

        $usedTriangles.Add($tri)
        if (-not $materialCounts.ContainsKey($tri.Material)) {
            $materialCounts[$tri.Material] = 0
        }
        $materialCounts[$tri.Material]++

        $minIx = [Math]::Max(0, [Math]::Floor(($bounds.MinX - $Crop.MinX) / $CellSize))
        $maxIx = [Math]::Min([Math]::Ceiling(($Crop.MaxX - $Crop.MinX) / $CellSize) - 1, [Math]::Floor(($bounds.MaxX - $Crop.MinX) / $CellSize))
        $minIy = [Math]::Max(0, [Math]::Floor(($bounds.MinY - $Crop.MinY) / $CellSize))
        $maxIy = [Math]::Min([Math]::Ceiling(($Crop.MaxY - $Crop.MinY) / $CellSize) - 1, [Math]::Floor(($bounds.MaxY - $Crop.MinY) / $CellSize))
        for ($ix = $minIx; $ix -le $maxIx; $ix++) {
            $x = $Crop.MinX + (($ix + 0.5) * $CellSize)
            for ($iy = $minIy; $iy -le $maxIy; $iy++) {
                $y = $Crop.MinY + (($iy + 0.5) * $CellSize)
                if (Test-PointInTriangle2D $x $y $tri) {
                    $cells["$ix,$iy"] = $true
                }
            }
        }
    }

    return [pscustomobject]@{
        Cells = $cells
        TriangleCount = $usedTriangles.Count
        MaterialCounts = $materialCounts
    }
}

function Get-MissingCells([hashtable]$Source, [hashtable]$Target) {
    $missing = @{}
    foreach ($key in $Source.Keys) {
        if (-not $Target.ContainsKey($key)) {
            $missing[$key] = $true
        }
    }
    return $missing
}

function Get-Components([hashtable]$Cells, [object]$Crop, [double]$CellSize) {
    $unvisited = @{}
    foreach ($key in $Cells.Keys) {
        $unvisited[$key] = $true
    }

    $components = [System.Collections.Generic.List[object]]::new()
    while ($unvisited.Count -gt 0) {
        $start = @($unvisited.Keys)[0]
        $queue = [System.Collections.Generic.Queue[string]]::new()
        $queue.Enqueue($start)
        $unvisited.Remove($start)

        $count = 0
        $minIx = [int]::MaxValue; $maxIx = [int]::MinValue
        $minIy = [int]::MaxValue; $maxIy = [int]::MinValue
        while ($queue.Count -gt 0) {
            $key = $queue.Dequeue()
            $parts = $key.Split(',')
            $ix = [int]$parts[0]
            $iy = [int]$parts[1]
            $count++
            $minIx = [Math]::Min($minIx, $ix); $maxIx = [Math]::Max($maxIx, $ix)
            $minIy = [Math]::Min($minIy, $iy); $maxIy = [Math]::Max($maxIy, $iy)

            foreach ($neighbor in @("$($ix - 1),$iy", "$($ix + 1),$iy", "$ix,$($iy - 1)", "$ix,$($iy + 1)")) {
                if ($unvisited.ContainsKey($neighbor)) {
                    $unvisited.Remove($neighbor)
                    $queue.Enqueue($neighbor)
                }
            }
        }

        $components.Add([pscustomobject]@{
            Cells = $count
            Area = $count * $CellSize * $CellSize
            MinX = $Crop.MinX + $minIx * $CellSize
            MaxX = $Crop.MinX + ($maxIx + 1) * $CellSize
            MinY = $Crop.MinY + $minIy * $CellSize
            MaxY = $Crop.MinY + ($maxIy + 1) * $CellSize
        })
    }

    return @($components | Sort-Object Cells -Descending)
}

function Write-CellsObj([hashtable]$Cells, [string]$Path, [object]$Crop, [double]$CellSize, [double]$Z) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    @'
newmtl missing_source_cell
Kd 1.00 0.10 0.05
Ka 0.05 0.00 0.00
d 0.70
'@ | Set-Content -Path (Join-Path (Split-Path -Parent $Path) 'coverage.mtl') -Encoding UTF8

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    $writer = [IO.StreamWriter]::new($Path, $false, $utf8NoBom)
    try {
        $writer.NewLine = "`n"
        $writer.WriteLine("# Source cells missing from target mmap sample")
        $writer.WriteLine("# OBJ frame: Y-up, X=WoW X, Y=WoW Z, Z=-WoW Y")
        $writer.WriteLine("mtllib coverage.mtl")
        $writer.WriteLine("o missing_source_cells")
        $writer.WriteLine("usemtl missing_source_cell")
        $next = 1
        foreach ($key in ($Cells.Keys | Sort-Object)) {
            $parts = $key.Split(',')
            $ix = [int]$parts[0]
            $iy = [int]$parts[1]
            $x0 = $Crop.MinX + $ix * $CellSize
            $x1 = $x0 + $CellSize
            $y0 = $Crop.MinY + $iy * $CellSize
            $y1 = $y0 + $CellSize
            $writer.WriteLine(("v {0} {1} {2}" -f (Format-Number $x0), (Format-Number $Z), (Format-Number (-1.0 * $y0))))
            $writer.WriteLine(("v {0} {1} {2}" -f (Format-Number $x1), (Format-Number $Z), (Format-Number (-1.0 * $y0))))
            $writer.WriteLine(("v {0} {1} {2}" -f (Format-Number $x1), (Format-Number $Z), (Format-Number (-1.0 * $y1))))
            $writer.WriteLine(("v {0} {1} {2}" -f (Format-Number $x0), (Format-Number $Z), (Format-Number (-1.0 * $y1))))
            $writer.WriteLine(("f {0} {1} {2} {3}" -f $next, ($next + 1), ($next + 2), ($next + 3)))
            $next += 4
        }
    }
    finally {
        $writer.Dispose()
    }
}

$latest = Resolve-RepoPath $LatestDir
$analysis = Join-Path $latest 'analysis'
New-Item -ItemType Directory -Force -Path $analysis | Out-Null

$crop = New-Bounds 1316 -4664 52.5 1348 -4636 54.8
$cellSize = 0.25
$upThreshold = 0.45

$sourceObj = Join-Path $latest 'source\compiled_adt_vmap_go_top_ramp_deck_all_sources.obj'
if (-not (Test-Path $sourceObj)) {
    $sourceObj = Join-Path $latest 'source\compiled_adt_vmap_go_top_ramp_deck_crop.obj'
}
$runtimeObj = Join-Path $latest 'mmap\mmap_top_ramp_deck_crop.obj'
$generatedObj = Join-Path $latest 'mmap\mmapgen_generated_top_ramp_deck_crop.obj'

$source = Get-Coverage (Read-ObjTriangles $sourceObj) $crop $cellSize $upThreshold
$runtime = Get-Coverage (Read-ObjTriangles $runtimeObj) $crop $cellSize $upThreshold
$generated = Get-Coverage (Read-ObjTriangles $generatedObj) $crop $cellSize $upThreshold

$missingRuntime = Get-MissingCells $source.Cells $runtime.Cells
$missingGenerated = Get-MissingCells $source.Cells $generated.Cells
$components = Get-Components $missingRuntime $crop $cellSize

Write-CellsObj $missingRuntime (Join-Path $analysis 'top_deck_source_not_runtime_mmap_cells.obj') $crop $cellSize 53.65
Write-CellsObj $missingGenerated (Join-Path $analysis 'top_deck_source_not_mmapgen_generated_cells.obj') $crop $cellSize 53.7

$componentRows = @()
for ($i = 0; $i -lt [Math]::Min(20, $components.Count); $i++) {
    $componentRows += [pscustomobject]@{
        rank = $i + 1
        cells = $components[$i].Cells
        area = (Format-Number $components[$i].Area)
        minX = (Format-Number $components[$i].MinX)
        maxX = (Format-Number $components[$i].MaxX)
        minY = (Format-Number $components[$i].MinY)
        maxY = (Format-Number $components[$i].MaxY)
    }
}
$componentRows | Export-Csv -NoTypeInformation -Path (Join-Path $analysis 'top_deck_surface_coverage_components.csv')

$sourceMaterials = ($source.MaterialCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ', '
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# Top Deck Surface Coverage")
$lines.Add("")
$lines.Add("Generated from the focused Orgrimmar zeppelin top-ramp/deck crop.")
$lines.Add("")
$lines.Add(("- Crop: wowX {0}..{1}, wowY {2}..{3}, z {4}..{5}; sample cell {6}y; upward normal threshold {7}." -f `
    (Format-Number $crop.MinX), (Format-Number $crop.MaxX), (Format-Number $crop.MinY), (Format-Number $crop.MaxY), `
    (Format-Number $crop.MinZ), (Format-Number $crop.MaxZ), (Format-Number $cellSize), (Format-Number $upThreshold)))
$lines.Add("- Source materials in this band: $sourceMaterials.")
$lines.Add(("- Source upward triangles: {0}; sampled cells: {1} ({2} yd^2)." -f $source.TriangleCount, $source.Cells.Count, (Format-Number ($source.Cells.Count * $cellSize * $cellSize))))
$lines.Add(("- Runtime Detour upward triangles: {0}; sampled cells: {1} ({2} yd^2)." -f $runtime.TriangleCount, $runtime.Cells.Count, (Format-Number ($runtime.Cells.Count * $cellSize * $cellSize))))
$lines.Add(("- Same-run generated Detour upward triangles: {0}; sampled cells: {1} ({2} yd^2)." -f $generated.TriangleCount, $generated.Cells.Count, (Format-Number ($generated.Cells.Count * $cellSize * $cellSize))))
$lines.Add(("- Source cells missing from runtime Detour: {0} ({1} yd^2), components: {2}." -f $missingRuntime.Count, (Format-Number ($missingRuntime.Count * $cellSize * $cellSize)), $components.Count))
$lines.Add(("- Source cells missing from same-run generated Detour: {0} ({1} yd^2)." -f $missingGenerated.Count, (Format-Number ($missingGenerated.Count * $cellSize * $cellSize))))
$lines.Add("")
$lines.Add("## Largest Source-Not-Runtime Components")
$lines.Add("")
$lines.Add("| Rank | Cells | Area | WowX | WowY |")
$lines.Add("|---:|---:|---:|---|---|")
for ($i = 0; $i -lt [Math]::Min(8, $components.Count); $i++) {
    $c = $components[$i]
    $lines.Add(("| {0} | {1} | {2} | {3}..{4} | {5}..{6} |" -f `
        ($i + 1), $c.Cells, (Format-Number $c.Area), (Format-Number $c.MinX), (Format-Number $c.MaxX), (Format-Number $c.MinY), (Format-Number $c.MaxY)))
}

$lines | Set-Content -Path (Join-Path $analysis 'top_deck_surface_coverage.md') -Encoding UTF8
Write-Host "[compare-og-top-deck-source-vs-mmap] DONE -> $(Join-Path $analysis 'top_deck_surface_coverage.md')" -ForegroundColor Green
