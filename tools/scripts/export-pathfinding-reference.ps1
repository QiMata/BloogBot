param(
    [ValidateSet('og-zeppelin', 'og-city', 'brd', 'rfc', 'all')]
    [string]$Route = 'all',
    [string]$DataDir = $(if ($env:WWOW_DATA_DIR) { $env:WWOW_DATA_DIR } else { 'D:\MaNGOS\data' }),
    [string]$OutRoot = 'tmp\test-runtime\visualization\pathfinding',
    [string]$TrxPath = 'tmp\test-runtime\results-pathfinding\underpass_sim_anchor_diagnostics.trx',
    [string]$MmapGenExe = $(if (Test-Path 'tools\MmapGen\build\MmapGen.exe') { 'tools\MmapGen\build\MmapGen.exe' } else { 'D:\MaNGOS\source\bin\MoveMapGenerator.exe' }),
    [string[]]$GameObjectVariants,
    [switch]$RefreshRaw,
    [switch]$RefreshGameObjectVariants,
    [switch]$Resume,
    [switch]$CleanLegacyOgFolder
)

$ErrorActionPreference = 'Stop'
$script:Invariant = [Globalization.CultureInfo]::InvariantCulture
$script:RepoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
$script:SupportedGameObjectVariants = @(
    'base',
    'midsummer-fire-festival',
    'feast-of-winter-veil',
    'darkmoon-elwynn',
    'darkmoon-mulgore',
    'fireworks',
    'lunar-festival',
    'love-is-in-the-air',
    'harvest-festival',
    'hallows-end',
    'noblegarden',
    'new-years-eve',
    'org-city-trophies',
    'sw-city-trophies',
    'major-city-trophies'
)

function Get-CanonicalGameObjectVariant([string]$Variant) {
    if ([string]::IsNullOrWhiteSpace($Variant)) {
        throw 'GameObject variant names must be non-empty.'
    }

    $normalized = $Variant.Trim().ToLowerInvariant()
    if ($script:SupportedGameObjectVariants -notcontains $normalized) {
        throw "Unsupported -GameObjectVariants entry '$Variant'. Supported values: $($script:SupportedGameObjectVariants -join ', ')"
    }

    return $normalized
}

function Get-ResolvedGameObjectVariants([string[]]$Variants) {
    if ($null -eq $Variants -or $Variants.Count -eq 0) {
        return @()
    }

    $resolved = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $requestedBase = $false
    foreach ($variant in $Variants) {
        $entries = @($variant -split '[,;]' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        foreach ($entry in $entries) {
            $canonical = Get-CanonicalGameObjectVariant $entry
            if ($canonical -eq 'base') {
                $requestedBase = $true
            }
            if ($seen.Add($canonical)) {
                $resolved.Add($canonical)
            }
        }
    }

    if ($resolved.Count -gt 0 -and -not $requestedBase) {
        $resolved.Insert(0, 'base')
    }

    return @($resolved.ToArray())
}

$script:ResolvedGameObjectVariants = Get-ResolvedGameObjectVariants $GameObjectVariants

function Get-MonorepoRoot {
    $candidate = Split-Path -Parent $script:RepoRoot
    if ($candidate -and (Test-Path (Join-Path $candidate 'tools\recastnavigation\RecastDemo'))) {
        return $candidate
    }

    return $script:RepoRoot
}

function Resolve-RepoPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }
    return [IO.Path]::GetFullPath((Join-Path $script:RepoRoot $Path))
}

function Assert-UnderRepoTmp([string]$Path) {
    $full = Resolve-RepoPath $Path
    $allowed = [IO.Path]::GetFullPath((Join-Path $script:RepoRoot 'tmp\test-runtime\visualization'))
    if (-not $full.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside visualization tmp: $full"
    }
    return $full
}

function Reset-Directory([string]$Path) {
    $full = Assert-UnderRepoTmp $Path
    if (Test-Path $full) {
        Get-ChildItem -LiteralPath $full -Force | Remove-Item -Recurse -Force
    } else {
        New-Item -ItemType Directory -Force -Path $full | Out-Null
    }
    return $full
}

function Ensure-CategoryDirs([string]$LatestDir) {
    foreach ($name in 'source', 'mmap', 'overlays', 'analysis', 'logs') {
        New-Item -ItemType Directory -Force -Path (Join-Path $LatestDir $name) | Out-Null
    }
}

function Initialize-LatestDir([string]$RelativePath) {
    if ($Resume) {
        $latest = Assert-UnderRepoTmp $RelativePath
        New-Item -ItemType Directory -Force -Path $latest | Out-Null
        Ensure-CategoryDirs $latest
        return $latest
    }

    $latest = Reset-Directory $RelativePath
    Ensure-CategoryDirs $latest
    return $latest
}

function Format-GameObjectVariantLabel {
    if ($script:ResolvedGameObjectVariants.Count -eq 0) {
        return 'legacy-full-export'
    }

    return ($script:ResolvedGameObjectVariants -join '+')
}

function Format-GameObjectVariantSummary {
    if ($script:ResolvedGameObjectVariants.Count -eq 0) {
        return 'legacy full export from gameobject_spawns.json'
    }

    return ($script:ResolvedGameObjectVariants -join ', ')
}

function Ensure-GameObjectVariantExport([string]$VariantId) {
    $variantRoot = Join-Path $DataDir 'gameobject_spawns'
    $variantPath = Join-Path $variantRoot ($VariantId + '.json')
    if (-not $RefreshGameObjectVariants -and (Test-Path $variantPath)) {
        return $variantPath
    }

    New-Item -ItemType Directory -Force -Path $variantRoot | Out-Null
    $projectPath = Resolve-RepoPath 'tools\GameObjectExporter\GameObjectExporter.csproj'
    $args = @(
        'run',
        '--project', $projectPath,
        '--configuration', 'Release',
        '--',
        '--variant', $VariantId,
        '--out', $variantPath
    )

    Write-Host "[go-variants] exporting $VariantId -> $variantPath" -ForegroundColor DarkGray
    & dotnet @args | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "GameObjectExporter failed for variant '$VariantId' with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path $variantPath)) {
        throw "Expected variant export was not created: $variantPath"
    }

    return $variantPath
}

function Get-GameObjectSpawnIdentity($Spawn) {
    if ($null -ne $Spawn.PSObject.Properties['guid'] -and $null -ne $Spawn.guid) {
        return "guid:$($Spawn.guid)"
    }

    return "display:$($Spawn.displayId)|x:$($Spawn.x)|y:$($Spawn.y)|z:$($Spawn.z)|o:$($Spawn.o)|s:$($Spawn.s)"
}

function Write-ComposedGameObjectSpawnsJson([string[]]$VariantIds, [string]$OutPath) {
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $byMap = [ordered]@{}

    foreach ($variantId in $VariantIds) {
        $variantPath = Ensure-GameObjectVariantExport $variantId
        $root = [IO.File]::ReadAllText($variantPath) | ConvertFrom-Json
        foreach ($mapProperty in $root.PSObject.Properties) {
            if (-not $byMap.Contains($mapProperty.Name)) {
                $byMap[$mapProperty.Name] = New-Object System.Collections.ArrayList
            }

            foreach ($spawn in @($mapProperty.Value)) {
                $identity = '{0}|{1}' -f $mapProperty.Name, (Get-GameObjectSpawnIdentity $spawn)
                if ($seen.Add($identity)) {
                    [void]$byMap[$mapProperty.Name].Add($spawn)
                }
            }
        }
    }

    $orderedMaps = [ordered]@{}
    foreach ($mapKey in ($byMap.Keys | Sort-Object { [int]$_ })) {
        $orderedMaps[$mapKey] = @($byMap[$mapKey])
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutPath) | Out-Null
    $orderedMaps | ConvertTo-Json -Depth 6 -Compress | Set-Content -Path $OutPath -Encoding UTF8
}

function Initialize-GameObjectSpawnsForWorkDir([string]$WorkDir) {
    $destination = Join-Path $WorkDir 'gameobject_spawns.json'
    if ($script:ResolvedGameObjectVariants.Count -eq 0) {
        $legacyPath = Join-Path $DataDir 'gameobject_spawns.json'
        if (Test-Path $legacyPath) {
            Copy-Item -Force $legacyPath $destination
        }
        return
    }

    Write-ComposedGameObjectSpawnsJson $script:ResolvedGameObjectVariants $destination
}

function New-Point([double]$X, [double]$Y, [double]$Z, [string]$Label = '') {
    [pscustomobject]@{ X = $X; Y = $Y; Z = $Z; Label = $Label }
}

function New-Bounds([double]$MinX, [double]$MinY, [double]$MinZ, [double]$MaxX, [double]$MaxY, [double]$MaxZ) {
    [pscustomobject]@{
        MinX = [Math]::Min($MinX, $MaxX)
        MinY = [Math]::Min($MinY, $MaxY)
        MinZ = [Math]::Min($MinZ, $MaxZ)
        MaxX = [Math]::Max($MinX, $MaxX)
        MaxY = [Math]::Max($MinY, $MaxY)
        MaxZ = [Math]::Max($MinZ, $MaxZ)
    }
}

function Format-Number([double]$Value) {
    $Value.ToString('F4', $script:Invariant)
}

function Format-WowCoord($Point) {
    '{0},{1},{2}' -f (Format-Number $Point.X), (Format-Number $Point.Y), (Format-Number $Point.Z)
}

function Format-Crop($Bounds) {
    '{0},{1},{2},{3},{4},{5}' -f `
        (Format-Number $Bounds.MinX), (Format-Number $Bounds.MinY), (Format-Number $Bounds.MinZ), `
        (Format-Number $Bounds.MaxX), (Format-Number $Bounds.MaxY), (Format-Number $Bounds.MaxZ)
}

function Convert-RawDetourToWow($RawPoint) {
    # MmapGen debug OBJ vertices are stored as Detour/Recast (WoW_Y, WoW_Z, WoW_X).
    New-Point $RawPoint.Z $RawPoint.X $RawPoint.Y
}

function Convert-WowToObj($WowPoint) {
    # Stable viewer frame used by MmapVisualize: Y-up OBJ = (WoW X, WoW Z, -WoW Y).
    New-Point $WowPoint.X $WowPoint.Z (-1.0 * $WowPoint.Y)
}

function Test-InBounds($WowPoint, $Bounds) {
    if ($null -eq $Bounds) { return $true }
    return $WowPoint.X -ge $Bounds.MinX -and $WowPoint.X -le $Bounds.MaxX `
        -and $WowPoint.Y -ge $Bounds.MinY -and $WowPoint.Y -le $Bounds.MaxY `
        -and $WowPoint.Z -ge $Bounds.MinZ -and $WowPoint.Z -le $Bounds.MaxZ
}

function Test-TriangleIntersectsBounds($WowPoints, $Bounds) {
    if ($null -eq $Bounds) { return $true }
    if ($null -eq $WowPoints -or $WowPoints.Count -lt 3) { return $false }

    $first = $WowPoints[0]
    $minX = $first.X
    $maxX = $first.X
    $minY = $first.Y
    $maxY = $first.Y
    $minZ = $first.Z
    $maxZ = $first.Z
    for ($i = 1; $i -lt $WowPoints.Count; $i++) {
        $p = $WowPoints[$i]
        if ($p.X -lt $minX) { $minX = $p.X }
        if ($p.X -gt $maxX) { $maxX = $p.X }
        if ($p.Y -lt $minY) { $minY = $p.Y }
        if ($p.Y -gt $maxY) { $maxY = $p.Y }
        if ($p.Z -lt $minZ) { $minZ = $p.Z }
        if ($p.Z -gt $maxZ) { $maxZ = $p.Z }
    }

    return $maxX -ge $Bounds.MinX -and $minX -le $Bounds.MaxX `
        -and $maxY -ge $Bounds.MinY -and $minY -le $Bounds.MaxY `
        -and $maxZ -ge $Bounds.MinZ -and $minZ -le $Bounds.MaxZ
}

function Write-ReferenceMtl([string]$Path) {
@'
newmtl raw_geometry
Kd 0.62 0.62 0.58
Ka 0.03 0.03 0.03
d 0.72

newmtl terrain
Kd 0.42 0.72 0.34
Ka 0.10 0.10 0.10
d 0.82

newmtl vmap
Kd 0.55 0.55 0.85
Ka 0.10 0.10 0.10
d 0.82

newmtl gameobject
Kd 0.95 0.55 0.22
Ka 0.10 0.10 0.10
d 0.82

newmtl liquid
Kd 0.20 0.55 0.90
Ka 0.10 0.10 0.10
d 0.45

newmtl path_line
Kd 0.05 0.30 1.00
Ka 0.00 0.02 0.05

newmtl reference_point
Kd 1.00 0.55 0.00
Ka 0.05 0.03 0.00
'@ | Set-Content -Path $Path -Encoding UTF8
}

function Convert-RawMmapGenObj(
    [string]$RawObj,
    [string]$OutObj,
    $CropWow = $null)
{
    if (-not (Test-Path $RawObj)) {
        throw "Raw MmapGen OBJ not found: $RawObj"
    }

    $outDir = Split-Path -Parent $OutObj
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    Write-ReferenceMtl (Join-Path $outDir 'reference.mtl')

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $writer = [IO.StreamWriter]::new($OutObj, $false, $utf8NoBom)
    try {
        $writer.NewLine = "`n"
        $writer.WriteLine("# Compiled ADT/VMAP/GO bake-input geometry converted from MmapGen debug OBJ")
        $writer.WriteLine("# OBJ frame: Y-up, X=WoW X, Y=WoW Z, Z=-WoW Y")
        if ($null -ne $CropWow) {
            $writer.WriteLine(("# crop-wow=({0},{1},{2})..({3},{4},{5})" -f `
                (Format-Number $CropWow.MinX), (Format-Number $CropWow.MinY), (Format-Number $CropWow.MinZ), `
                (Format-Number $CropWow.MaxX), (Format-Number $CropWow.MaxY), (Format-Number $CropWow.MaxZ)))
        }
        $writer.WriteLine("mtllib reference.mtl")
        $writer.WriteLine("o compiled_adt_vmap_go")
        $writer.WriteLine("usemtl raw_geometry")

        if ($null -eq $CropWow) {
            foreach ($line in [IO.File]::ReadLines((Resolve-Path $RawObj))) {
                if ($line.StartsWith('v ')) {
                    $parts = $line.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
                    $raw = New-Point `
                        ([double]::Parse($parts[1], $script:Invariant)) `
                        ([double]::Parse($parts[2], $script:Invariant)) `
                        ([double]::Parse($parts[3], $script:Invariant))
                    $obj = Convert-WowToObj (Convert-RawDetourToWow $raw)
                    $writer.WriteLine(("v {0} {1} {2}" -f (Format-Number $obj.X), (Format-Number $obj.Y), (Format-Number $obj.Z)))
                } elseif ($line.StartsWith('g ') -or $line.StartsWith('usemtl ')) {
                    $writer.WriteLine($line)
                } elseif ($line.StartsWith('f ') -or $line.StartsWith('l ')) {
                    $writer.WriteLine($line)
                }
            }
            return
        }

        $wowVerts = New-Object System.Collections.Generic.List[object]
        $objVerts = New-Object System.Collections.Generic.List[object]
        foreach ($line in [IO.File]::ReadLines((Resolve-Path $RawObj))) {
            if (-not $line.StartsWith('v ')) { continue }
            $parts = $line.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
            $raw = New-Point `
                ([double]::Parse($parts[1], $script:Invariant)) `
                ([double]::Parse($parts[2], $script:Invariant)) `
                ([double]::Parse($parts[3], $script:Invariant))
            $wow = Convert-RawDetourToWow $raw
            $wowVerts.Add($wow)
            $objVerts.Add((Convert-WowToObj $wow))
        }

        $next = 1
        $currentGroup = 'compiled_adt_vmap_go_crop'
        $currentMaterial = 'raw_geometry'
        $writtenGroup = 'compiled_adt_vmap_go'
        $writtenMaterial = 'raw_geometry'
        foreach ($line in [IO.File]::ReadLines((Resolve-Path $RawObj))) {
            if ($line.StartsWith('g ')) {
                $currentGroup = $line.Substring(2).Trim()
                continue
            }
            if ($line.StartsWith('usemtl ')) {
                $currentMaterial = $line.Substring(7).Trim()
                continue
            }
            if (-not $line.StartsWith('f ')) { continue }
            $parts = $line.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
            $indices = New-Object System.Collections.Generic.List[int]
            $faceWowVerts = New-Object System.Collections.Generic.List[object]
            for ($i = 1; $i -lt $parts.Length; $i++) {
                $idx = [int]($parts[$i].Split('/')[0])
                if ($idx -le 0 -or $idx -gt $wowVerts.Count) { continue }
                $indices.Add($idx)
                $faceWowVerts.Add($wowVerts[$idx - 1])
            }
            $include = Test-TriangleIntersectsBounds $faceWowVerts $CropWow
            if (-not $include -or $indices.Count -lt 3) { continue }

            if ($currentGroup -ne $writtenGroup) {
                $writer.WriteLine("g $currentGroup")
                $writtenGroup = $currentGroup
            }
            if ($currentMaterial -ne $writtenMaterial) {
                $writer.WriteLine("usemtl $currentMaterial")
                $writtenMaterial = $currentMaterial
            }

            $face = New-Object System.Text.StringBuilder
            [void]$face.Append('f')
            foreach ($idx in $indices) {
                $obj = $objVerts[$idx - 1]
                $writer.WriteLine(("v {0} {1} {2}" -f (Format-Number $obj.X), (Format-Number $obj.Y), (Format-Number $obj.Z)))
                [void]$face.Append(' ')
                [void]$face.Append($next)
                $next++
            }
            $writer.WriteLine($face.ToString())
        }
    }
    finally {
        $writer.Dispose()
    }
}

function Invoke-MmapVisualize(
    [string]$Mmtile,
    [string]$OutObj,
    [object[]]$Markers,
    $CropWow = $null,
    [string]$PolyReport = '',
    [switch]$IncludeVmaps,
    [string]$ReachableFrom = '',
    [string]$ReachableMode = '')
{
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutObj) | Out-Null
    $argsList = @(
        'run', '--project', (Join-Path $script:RepoRoot 'tools\MmapVisualize\MmapVisualize.csproj'), '--',
        '--mmtile', $Mmtile,
        '--out', $OutObj,
        '--z-band', '2'
    )
    if ($IncludeVmaps) {
        $argsList += @('--include-vmaps', (Join-Path $DataDir 'vmaps'))
    }
    if ($null -ne $CropWow) {
        $argsList += @('--crop', (Format-Crop $CropWow))
    }
    if ($PolyReport) {
        $argsList += @('--poly-report', $PolyReport)
    }
    if ($ReachableFrom) {
        $argsList += @('--reachable-from', $ReachableFrom)
    }
    if ($ReachableMode) {
        $argsList += @('--reachable-mode', $ReachableMode)
    }
    foreach ($m in $Markers) {
        $argsList += @('--mark', (Format-WowCoord $m), $m.Label)
    }

    # PowerShell 5.1 treats any stderr output from a native command as a
    # NativeCommandError when $ErrorActionPreference='Stop'. MmapVisualize
    # writes its tile-header banner ("# tile=(...) ...") to stderr by design
    # so non-OBJ stream content does not pollute the OBJ files; that banner
    # is informational, not a failure. Do not redirect stderr (2>&1 makes 5.1
    # wrap each stderr line as ErrorRecord which itself triggers the same
    # stop). Instead, scope ErrorActionPreference to Continue for this call
    # and gate on the actual exit code.
    $previousEAP = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & dotnet @argsList
    }
    finally {
        $ErrorActionPreference = $previousEAP
    }
    if ($LASTEXITCODE -ne 0) {
        throw "MmapVisualize failed for $OutObj with exit code $LASTEXITCODE"
    }
}

function Invoke-MmapGenDebugTile(
    [int]$MapId,
    [int]$TileX,
    [int]$TileY,
    [string]$WorkDir,
    [string]$LogPath)
{
    $resolvedMmapGenExe = Resolve-RepoPath $MmapGenExe
    if (-not (Test-Path $resolvedMmapGenExe)) {
        throw "MmapGen exe not found: $resolvedMmapGenExe"
    }

    Reset-Directory $WorkDir | Out-Null
    $work = Resolve-RepoPath $WorkDir
    foreach ($dir in 'mmaps', 'meshes') {
        New-Item -ItemType Directory -Force -Path (Join-Path $work $dir) | Out-Null
    }
    foreach ($link in @(
        @{ Name = 'maps'; Target = (Join-Path $DataDir 'maps') },
        @{ Name = 'vmaps'; Target = (Join-Path $DataDir 'vmaps') }))
    {
        $path = Join-Path $work $link.Name
        if (-not (Test-Path $path)) {
            New-Item -ItemType Junction -Path $path -Target $link.Target | Out-Null
        }
    }

    Initialize-GameObjectSpawnsForWorkDir $work
    if ($script:ResolvedGameObjectVariants.Count -gt 0) {
        Write-Host ("[go-variants] using {0} for map={1} tile={2},{3}" -f `
            (Format-GameObjectVariantSummary),
            $MapId,
            $TileX,
            $TileY) -ForegroundColor DarkGray
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogPath) | Out-Null
    Push-Location $work
    try {
        & $resolvedMmapGenExe $MapId --tile "$TileX,$TileY" --silent --threads 1 --debug `
            --offMeshInput (Join-Path $script:RepoRoot 'tools\MmapGen\offmesh.txt') `
            --configInputPath (Join-Path $script:RepoRoot 'tools\MmapGen\config.json') 2>&1 |
            Tee-Object -FilePath $LogPath | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "MmapGen failed for map=$MapId tile=$TileX,$TileY with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    $tileKey = '{0:D3}{1:D2}{2:D2}' -f $MapId, $TileY, $TileX
    return [pscustomobject]@{
        RawObj = Join-Path $work "meshes\map$tileKey.obj"
        PolyMeshObj = Join-Path $work "meshes\map${tileKey}navmesh.obj"
        DetailMeshObj = Join-Path $work "meshes\map${tileKey}navmeshdetail.obj"
        Mmtile = Join-Path $work "mmaps\$tileKey.mmtile"
        Log = $LogPath
    }
}

function Get-MmapGenDebugTileResult(
    [int]$MapId,
    [int]$TileX,
    [int]$TileY,
    [string]$WorkDir,
    [string]$LogPath)
{
    $work = Resolve-RepoPath $WorkDir
    $tileKey = '{0:D3}{1:D2}{2:D2}' -f $MapId, $TileY, $TileX
    return [pscustomobject]@{
        RawObj = Join-Path $work "meshes\map$tileKey.obj"
        PolyMeshObj = Join-Path $work "meshes\map${tileKey}navmesh.obj"
        DetailMeshObj = Join-Path $work "meshes\map${tileKey}navmeshdetail.obj"
        Mmtile = Join-Path $work "mmaps\$tileKey.mmtile"
        Log = $LogPath
    }
}

function Read-PathFromTrx([string]$Path) {
    $points = New-Object System.Collections.Generic.List[object]
    if (-not (Test-Path $Path)) {
        return $points
    }

    $raw = [IO.File]::ReadAllText((Resolve-Path $Path))
    $matches = [regex]::Matches($raw, '\[(\d{1,3})\]\s+\((-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)\)')
    $byIndex = @{}
    foreach ($m in $matches) {
        $idx = [int]$m.Groups[1].Value
        if (-not $byIndex.ContainsKey($idx)) {
            $byIndex[$idx] = New-Point `
                ([double]::Parse($m.Groups[2].Value, $script:Invariant)) `
                ([double]::Parse($m.Groups[3].Value, $script:Invariant)) `
                ([double]::Parse($m.Groups[4].Value, $script:Invariant)) `
                ("wp_$idx")
        }
    }
    for ($i = 0; $byIndex.ContainsKey($i); $i++) {
        $points.Add($byIndex[$i])
    }
    return $points
}

function Write-Waypoints($Points, [string]$CsvPath, [string]$ObjPath) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $CsvPath) | Out-Null
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ObjPath) | Out-Null
    Write-ReferenceMtl (Join-Path (Split-Path -Parent $ObjPath) 'reference.mtl')

    $rows = New-Object System.Collections.Generic.List[string]
    $rows.Add('index,x,y,z')
    for ($i = 0; $i -lt $Points.Count; $i++) {
        $p = $Points[$i]
        $rows.Add(('{0},{1},{2},{3}' -f $i, (Format-Number $p.X), (Format-Number $p.Y), (Format-Number $p.Z)))
    }
    $rows | Set-Content -Path $CsvPath -Encoding UTF8

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $writer = [IO.StreamWriter]::new($ObjPath, $false, $utf8NoBom)
    try {
        $writer.NewLine = "`n"
        $writer.WriteLine("# Route waypoint polyline")
        $writer.WriteLine("# OBJ frame: Y-up, X=WoW X, Y=WoW Z, Z=-WoW Y")
        $writer.WriteLine("mtllib reference.mtl")
        $writer.WriteLine("o route_waypoints")
        $writer.WriteLine("usemtl path_line")
        foreach ($p in $Points) {
            $obj = Convert-WowToObj $p
            $writer.WriteLine(("v {0} {1} {2}" -f (Format-Number $obj.X), (Format-Number $obj.Y), (Format-Number $obj.Z)))
        }
        if ($Points.Count -gt 1) {
            $writer.WriteLine(("l {0}" -f ((1..$Points.Count) -join ' ')))
        }
    }
    finally {
        $writer.Dispose()
    }
}

function Write-Readme([string]$Path, [string]$Title, [string[]]$Lines) {
    $content = @()
    $content += "# $Title"
    $content += ""
    $content += "Generated: $([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss zzz'))"
    $content += ""
    $content += "All filenames are stable and are overwritten by the next export."
    $content += ""
    $content += $Lines
    $content | Set-Content -Path $Path -Encoding UTF8
}

function Get-RecastDemoMeshRoots {
    $roots = New-Object System.Collections.Generic.List[string]
    $monorepoRoot = Get-MonorepoRoot
    $sourceRoot = [IO.Path]::GetFullPath((Join-Path $monorepoRoot 'tools\recastnavigation\RecastDemo\Bin\Meshes\WorldOfWarcraft\Generated'))
    $roots.Add($sourceRoot)

    $runtimeDemoDir = [IO.Path]::GetFullPath((Join-Path $monorepoRoot 'tools\recastnavigation\build-msvc\RecastDemo'))
    if (Test-Path $runtimeDemoDir) {
        $roots.Add((Join-Path $runtimeDemoDir 'Meshes\WorldOfWarcraft\Generated'))
    }

    return $roots
}

function Reset-RecastDemoRouteDir([string]$Path) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Write-RecastDemoLayout(
    [string]$Path,
    [int]$MapId,
    [string]$Title,
    [object[]]$Markers,
    [string[]]$Files,
    [string]$DefaultStartMarker = '',
    [string]$DefaultEndMarker = '')
{
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $writer = [IO.StreamWriter]::new($Path, $false, $utf8NoBom)
    try {
        $writer.NewLine = "`n"
        $writer.WriteLine("# Auto-generated by export-pathfinding-reference.ps1")
        $writer.WriteLine("m mapId $MapId")
        if ($Title) {
            $writer.WriteLine("m title $Title")
        }
        if ($DefaultStartMarker) {
            $writer.WriteLine("m defaultStartMarker $DefaultStartMarker")
        }
        if ($DefaultEndMarker) {
            $writer.WriteLine("m defaultEndMarker $DefaultEndMarker")
        }
        foreach ($marker in $Markers) {
            $writer.WriteLine(("p {0} {1} {2} {3}" -f `
                $marker.Label,
                (Format-Number $marker.X),
                (Format-Number $marker.Y),
                (Format-Number $marker.Z)))
        }
        foreach ($file in $Files) {
            $writer.WriteLine("f $file")
        }
    }
    finally {
        $writer.Dispose()
    }
}

function Stage-RecastDemoRoute(
    [string]$RouteFolder,
    [int]$MapId,
    [object[]]$Markers,
    [object[]]$Assets,
    [object[]]$Layouts)
{
    foreach ($root in Get-RecastDemoMeshRoots) {
        $routeDir = Join-Path $root $RouteFolder
        Reset-RecastDemoRouteDir $routeDir
        Write-ReferenceMtl (Join-Path $routeDir 'reference.mtl')
        $stagedTargets = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

        foreach ($asset in $Assets) {
            $required = $true
            if ($null -ne $asset.Required) {
                $required = [bool]$asset.Required
            }
            if (-not (Test-Path $asset.Source)) {
                if ($required) {
                    throw "Cannot stage missing RecastDemo asset: $($asset.Source)"
                }

                Write-Warning "Skipping optional RecastDemo asset: $($asset.Source)"
                continue
            }
            Copy-Item -Force $asset.Source (Join-Path $routeDir $asset.Target)
            $null = $stagedTargets.Add([string]$asset.Target)
        }

        foreach ($layout in $Layouts) {
            $layoutMarkers = if ($null -ne $layout.Markers) { $layout.Markers } else { $Markers }
            $layoutFiles = New-Object System.Collections.Generic.List[string]
            $missingFiles = @()
            foreach ($file in $layout.Files) {
                if (-not $stagedTargets.Contains([string]$file)) {
                    $missingFiles += [string]$file
                } else {
                    $layoutFiles.Add([string]$file)
                }
            }
            if ($null -ne $layout.OptionalFiles) {
                foreach ($file in $layout.OptionalFiles) {
                    if ($stagedTargets.Contains([string]$file)) {
                        $layoutFiles.Add([string]$file)
                    }
                }
            }

            if ($missingFiles.Count -gt 0) {
                Write-Warning ("Skipping RecastDemo layout {0} because staged assets are missing: {1}" -f `
                    $layout.Name,
                    ($missingFiles -join ', '))
                continue
            }

            Write-RecastDemoLayout `
                (Join-Path $routeDir $layout.Name) `
                $MapId `
                $layout.Title `
                $layoutMarkers `
                $layoutFiles.ToArray() `
                $layout.DefaultStartMarker `
                $layout.DefaultEndMarker
        }
    }
}

function Copy-IfExists([string]$From, [string]$To) {
    if (Test-Path $From) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $To) | Out-Null
        Copy-Item -Force $From $To
    }
}

function Copy-OgMmapGenDebugStageFiles([string]$WorkDir, [string]$LatestDir) {
    $meshDir = Join-Path $WorkDir 'meshes'
    $mmapDir = Join-Path $WorkDir 'mmaps'
    if (-not (Test-Path $meshDir)) {
        return
    }

    Copy-IfExists (Join-Path $mmapDir '0012940.mmtile') (Join-Path $LatestDir 'mmap\mmapgen_generated_tile.mmtile')
    Copy-IfExists (Join-Path $meshDir 'map0012940.mtl') (Join-Path $LatestDir 'source\compiled_adt_vmap_go_full.mtl')
    Copy-IfExists (Join-Path $meshDir 'map0012940.source_triangles.csv') (Join-Path $LatestDir 'analysis\compiled_adt_vmap_go_source_triangles.csv')
    Copy-IfExists (Join-Path $meshDir 'map0012940_stage_heightfield_spans.csv') (Join-Path $LatestDir 'analysis\mmapgen_stage_heightfield_spans.csv')
    Copy-IfExists (Join-Path $meshDir 'map0012940_stage_compact_spans.csv') (Join-Path $LatestDir 'analysis\mmapgen_stage_compact_spans.csv')
    Copy-IfExists (Join-Path $meshDir 'map0012940_stage_contours.csv') (Join-Path $LatestDir 'analysis\mmapgen_stage_contours.csv')
    Copy-IfExists (Join-Path $meshDir 'map0012940navmesh.obj') (Join-Path $LatestDir 'mmap\mmapgen_polymesh.obj')
    Copy-IfExists (Join-Path $meshDir 'map0012940navmeshdetail.obj') (Join-Path $LatestDir 'mmap\mmapgen_detailmesh.obj')
    Copy-IfExists (Join-Path $meshDir 'map0012940.pmesh') (Join-Path $LatestDir 'mmap\mmapgen_polymesh.pmesh')
    Copy-IfExists (Join-Path $meshDir '0012940.pmesh') (Join-Path $LatestDir 'mmap\mmapgen_polymesh.pmesh')
    Copy-IfExists (Join-Path $meshDir 'map0012940.dmesh') (Join-Path $LatestDir 'mmap\mmapgen_detailmesh.dmesh')
    Copy-IfExists (Join-Path $meshDir '0012940.dmesh') (Join-Path $LatestDir 'mmap\mmapgen_detailmesh.dmesh')
    Copy-IfExists (Join-Path $meshDir 'map0012940.nav') (Join-Path $LatestDir 'mmap\mmapgen_final_detour_tile.nav')
}

function Export-OgZeppelin {
    $latest = Initialize-LatestDir (Join-Path $OutRoot 'og-zeppelin\latest')

    $markers = @(
        (New-Point 1357.583 -4516.667 28.583 'route_start'),
        (New-Point 1342.667 -4653.067 39.509 'bad_segment_94_from'),
        (New-Point 1340.800 -4652.000 40.509 'bad_segment_95_to'),
        (New-Point 1335.200 -4644.400 53.500 'top_ramp_lip'),
        (New-Point 1338.130 -4645.960 51.600 'deck_lip_stall'),
        (New-Point 1331.110 -4649.450 53.627 'frezza_spawn'),
        (New-Point 1320.140 -4653.160 53.890 'boarding_pos')
    )
    $topDeckMarkers = @(
        $markers | Where-Object { $_.Label -in @('top_ramp_lip', 'deck_lip_stall', 'frezza_spawn', 'boarding_pos') }
    )
    $towerCrop = New-Bounds 1308 -4668 18 1372 -4500 110
    $topDeckCrop = New-Bounds 1316 -4664 47 1348 -4636 67
    $mmtile = Join-Path $DataDir 'mmaps\0012940.mmtile'

    $sourceFull = Join-Path $latest 'source\compiled_adt_vmap_go_full.obj'
    $sourceTowerCrop = Join-Path $latest 'source\compiled_adt_vmap_go_tower_crop.obj'
    $sourceTopDeckCrop = Join-Path $latest 'source\compiled_adt_vmap_go_top_ramp_deck_crop.obj'
    $sourceTopDeckAllSources = Join-Path $latest 'source\compiled_adt_vmap_go_top_ramp_deck_all_sources.obj'
    $latestWorkRaw = Join-Path $latest '_work\mmapgen\meshes\map0012940.obj'
    $legacyRaw = Resolve-RepoPath 'tmp\test-runtime\visualization\og-zeppelin-tower\mmapgen-debug-work\meshes\map0012940.obj'
    $raw = if (Test-Path $latestWorkRaw) { $latestWorkRaw } elseif (Test-Path $legacyRaw) { $legacyRaw } else { $latestWorkRaw }
    $needsSourceFull = $RefreshRaw -or -not (Test-Path $sourceFull)
    $needsSourceTowerCrop = $RefreshRaw -or -not (Test-Path $sourceTowerCrop)
    $needsSourceTopDeckCrop = $RefreshRaw -or -not (Test-Path $sourceTopDeckCrop) -or -not (Test-Path $sourceTopDeckAllSources)
    $needsSource = $needsSourceFull -or $needsSourceTowerCrop -or $needsSourceTopDeckCrop
    if ($needsSource -and ($RefreshRaw -or -not (Test-Path $raw))) {
        $rawResult = Invoke-MmapGenDebugTile 1 40 29 `
            (Join-Path $OutRoot 'og-zeppelin\latest\_work\mmapgen') `
            (Join-Path $latest 'logs\mmapgen_tile_0012940.log')
        $raw = $rawResult.RawObj
    } elseif (Test-Path (Resolve-RepoPath 'tmp\test-runtime\visualization\og-zeppelin-tower\mmapgen_debug_export.log')) {
        Copy-Item -Force (Resolve-RepoPath 'tmp\test-runtime\visualization\og-zeppelin-tower\mmapgen_debug_export.log') (Join-Path $latest 'logs\mmapgen_tile_0012940.log')
    }

    if ($needsSource) {
        if ($needsSourceFull) { Convert-RawMmapGenObj $raw $sourceFull }
        if ($needsSourceTowerCrop) { Convert-RawMmapGenObj $raw $sourceTowerCrop $towerCrop }
        if ($needsSourceTopDeckCrop) {
            Convert-RawMmapGenObj $raw $sourceTopDeckCrop $topDeckCrop
            Copy-Item -Force $sourceTopDeckCrop $sourceTopDeckAllSources
        }
    }
    Copy-OgMmapGenDebugStageFiles (Join-Path $OutRoot 'og-zeppelin\latest\_work\mmapgen') $latest

    $mmapgenGeneratedTile = Join-Path $latest 'mmap\mmapgen_generated_tile.mmtile'
    if (Test-Path $mmapgenGeneratedTile) {
        Invoke-MmapVisualize $mmapgenGeneratedTile (Join-Path $latest 'mmap\mmapgen_generated_top_ramp_deck_crop.obj') $markers $topDeckCrop (Join-Path $latest 'analysis\mmapgen_generated_top_ramp_deck_polys.csv')
    }

    Invoke-MmapVisualize $mmtile (Join-Path $latest 'mmap\mmap_full_with_vmap_bounds.obj') $markers -IncludeVmaps
    Invoke-MmapVisualize $mmtile (Join-Path $latest 'mmap\mmap_tower_crop.obj') $markers $towerCrop (Join-Path $latest 'analysis\mmap_tower_crop_polys.csv')
    Invoke-MmapVisualize $mmtile (Join-Path $latest 'mmap\mmap_top_ramp_deck_crop.obj') $markers $topDeckCrop (Join-Path $latest 'analysis\mmap_top_ramp_deck_polys.csv')
    Invoke-MmapVisualize $mmtile (Join-Path $latest 'mmap\mmap_top_ramp_deck_reachable.obj') $markers $topDeckCrop (Join-Path $latest 'analysis\mmap_top_ramp_deck_reachable_polys.csv') -ReachableFrom '1357.5830,-4516.6670,28.5830' -ReachableMode 'only'
    Invoke-MmapVisualize $mmtile (Join-Path $latest 'mmap\mmap_top_ramp_deck_unreachable.obj') $markers $topDeckCrop (Join-Path $latest 'analysis\mmap_top_ramp_deck_unreachable_polys.csv') -ReachableFrom '1357.5830,-4516.6670,28.5830' -ReachableMode 'unreachable'

    $points = Read-PathFromTrx (Resolve-RepoPath $TrxPath)
    if ($points.Count -gt 0) {
        Write-Waypoints $points (Join-Path $latest 'analysis\route_waypoints.csv') (Join-Path $latest 'overlays\route_waypoints.obj')
    }

    Write-Readme (Join-Path $latest 'README.md') 'Orgrimmar Zeppelin Tower Pathfinding Reference' @(
        "- Correct map/tile: map 1, MmapGen tile 40,29, runtime 0012940.mmtile, vmap 001_40_29.vmtile.",
        "- source/ contains converted compiled ADT/VMAP/GO bake-input geometry; source/compiled_adt_vmap_go_top_ramp_deck_all_sources.obj keeps terrain/vmap/gameobject/liquid materials visible for direct source-vs-mmap inspection.",
        "- analysis/compiled_adt_vmap_go_source_triangles.csv tags raw source triangles as terrain, vmap, gameobject, or liquid when generated by the in-tree MmapGen.",
        "- analysis/mmapgen_stage_* CSVs are focused heightfield/compact-heightfield/contour debug exports for the configured top-ramp/deck crop when debugStageCropWow is active.",
        "- mmap/mmapgen_* contains MmapGen's poly mesh, detail mesh, binary intermediate mesh files, and final Detour tile when available.",
        "- mmap/ contains Detour/MMAP navmesh renderings and focused top-ramp/deck crops.",
        "- analysis/mmap_top_ramp_deck_polys.csv is the main deck/ramp polygon report.",
        "- analysis/mmap_top_ramp_deck_reachable_polys.csv and analysis/mmap_top_ramp_deck_unreachable_polys.csv split the same crop by single-tile graph reachability from the route start.",
        "- overlays/route_waypoints.obj is the latest route polyline extracted from the TRX when available.",
        "- Active GameObject set: $(Format-GameObjectVariantSummary). If the log has [GO] map=1 tile=40,29, the raw bake input includes those static GameObject placements."
    )

    Stage-RecastDemoRoute 'Orgrimmar' 1 $markers @(
        @{ Source = $sourceFull; Target = 'og_source_full.obj' },
        @{ Source = $sourceTowerCrop; Target = 'og_source_tower_crop.obj' },
        @{ Source = $sourceTopDeckAllSources; Target = 'og_source_top_deck_all_sources.obj' },
        @{ Source = (Join-Path $latest 'mmap\mmap_full_with_vmap_bounds.obj'); Target = 'og_runtime_mmap_full.obj' },
        @{ Source = (Join-Path $latest 'mmap\mmap_tower_crop.obj'); Target = 'og_runtime_mmap_tower_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\mmap_top_ramp_deck_crop.obj'); Target = 'og_runtime_mmap_top_deck_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\mmapgen_generated_top_ramp_deck_crop.obj'); Target = 'og_mmapgen_generated_top_deck_crop.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\mmapgen_polymesh.obj'); Target = 'og_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\mmapgen_detailmesh.obj'); Target = 'og_mmapgen_detailmesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'overlays\route_waypoints.obj'); Target = 'route_waypoints.obj'; Required = $false }
    ) @(
        @{
            Name = 'og_zeppelin_source_full.gset'
            Title = 'Orgrimmar Zeppelin Source Full'
            Files = @('og_source_full.obj')
            OptionalFiles = @('route_waypoints.obj')
            DefaultStartMarker = 'route_start'
            DefaultEndMarker = 'boarding_pos'
        },
        @{
            Name = 'og_zeppelin_source_tower_crop.gset'
            Title = 'Orgrimmar Zeppelin Source Tower Crop'
            Files = @('og_source_tower_crop.obj')
            OptionalFiles = @('route_waypoints.obj')
            DefaultStartMarker = 'route_start'
            DefaultEndMarker = 'boarding_pos'
        },
        @{
            Name = 'og_zeppelin_source_top_deck.gset'
            Title = 'Orgrimmar Zeppelin Source Top Deck'
            Markers = $topDeckMarkers
            Files = @('og_source_top_deck_all_sources.obj')
            OptionalFiles = @('route_waypoints.obj')
            DefaultStartMarker = 'frezza_spawn'
            DefaultEndMarker = 'boarding_pos'
        },
        @{
            Name = 'og_zeppelin_runtime_mmap_full.gset'
            Title = 'Orgrimmar Runtime MMAP Full'
            Files = @('og_runtime_mmap_full.obj')
            OptionalFiles = @('route_waypoints.obj')
            DefaultStartMarker = 'route_start'
            DefaultEndMarker = 'boarding_pos'
        },
        @{
            Name = 'og_zeppelin_runtime_mmap_tower_crop.gset'
            Title = 'Orgrimmar Runtime MMAP Tower Crop'
            Files = @('og_runtime_mmap_tower_crop.obj')
            OptionalFiles = @('route_waypoints.obj')
            DefaultStartMarker = 'route_start'
            DefaultEndMarker = 'boarding_pos'
        },
        @{
            Name = 'og_zeppelin_runtime_mmap_top_deck.gset'
            Title = 'Orgrimmar Runtime MMAP Top Deck'
            Markers = $topDeckMarkers
            Files = @('og_runtime_mmap_top_deck_crop.obj')
            OptionalFiles = @('route_waypoints.obj')
            DefaultStartMarker = 'frezza_spawn'
            DefaultEndMarker = 'boarding_pos'
        },
        @{
            Name = 'og_zeppelin_mmapgen_generated_top_deck.gset'
            Title = 'Orgrimmar MmapGen Generated Top Deck'
            Markers = $topDeckMarkers
            Files = @('og_mmapgen_generated_top_deck_crop.obj')
            OptionalFiles = @('route_waypoints.obj')
            DefaultStartMarker = 'frezza_spawn'
            DefaultEndMarker = 'boarding_pos'
        },
        @{
            Name = 'og_zeppelin_mmapgen_polymesh.gset'
            Title = 'Orgrimmar MmapGen Polymesh'
            Markers = $topDeckMarkers
            Files = @('og_mmapgen_polymesh.obj')
            DefaultStartMarker = 'frezza_spawn'
            DefaultEndMarker = 'boarding_pos'
        },
        @{
            Name = 'og_zeppelin_mmapgen_detailmesh.gset'
            Title = 'Orgrimmar MmapGen Detail Mesh'
            Markers = $topDeckMarkers
            Files = @('og_mmapgen_detailmesh.obj')
            DefaultStartMarker = 'frezza_spawn'
            DefaultEndMarker = 'boarding_pos'
        }
    )

    if (Test-Path (Join-Path $latest '_work')) {
        Remove-Item -LiteralPath (Join-Path $latest '_work') -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Export-OgCity {
    $latest = Initialize-LatestDir (Join-Path $OutRoot 'og-city\latest')

    $markers = @(
        (New-Point 1320.140 -4653.160 53.890 'zeppelin_tower_top'),
        (New-Point 1350.540 -4663.390 53.455 'zeppelin_campfire'),
        (New-Point 1536.940 -4409.440 8.059 'org_ony_head'),
        (New-Point 1537.890 -4421.620 7.553 'org_nef_head'),
        (New-Point 1668.130 -4419.980 17.398 'org_midsummer_probe'),
        (New-Point 1766.420 -4222.410 43.352 'org_main_gate')
    )

    $tiles = @(
        [pscustomobject]@{ Name = 'og_city_tile_3928'; Map = 1; TileX = 39; TileY = 28; Mmtile = Join-Path $DataDir 'mmaps\0012839.mmtile' },
        [pscustomobject]@{ Name = 'og_city_tile_3929'; Map = 1; TileX = 39; TileY = 29; Mmtile = Join-Path $DataDir 'mmaps\0012939.mmtile' },
        [pscustomobject]@{ Name = 'og_city_tile_4028'; Map = 1; TileX = 40; TileY = 28; Mmtile = Join-Path $DataDir 'mmaps\0012840.mmtile' },
        [pscustomobject]@{ Name = 'og_city_tile_4029'; Map = 1; TileX = 40; TileY = 29; Mmtile = Join-Path $DataDir 'mmaps\0012940.mmtile' }
    )

    foreach ($tile in $tiles) {
        $tileKey = '{0:D3}{1:D2}{2:D2}' -f $tile.Map, $tile.TileY, $tile.TileX
        $workDir = Join-Path $OutRoot "og-city\latest\_work\mmapgen_$tileKey"
        $logPath = Join-Path $latest "logs\mmapgen_tile_$tileKey.log"
        $rawResult = Get-MmapGenDebugTileResult $tile.Map $tile.TileX $tile.TileY $workDir $logPath
        if ($RefreshRaw -or -not ($Resume -and (Test-Path $rawResult.RawObj))) {
            $rawResult = Invoke-MmapGenDebugTile $tile.Map $tile.TileX $tile.TileY $workDir $logPath
        }

        Convert-RawMmapGenObj $rawResult.RawObj (Join-Path $latest "source\$($tile.Name)_compiled_adt_vmap_go_full.obj")

        Copy-IfExists $rawResult.Mmtile (Join-Path $latest "mmap\$($tile.Name)_mmapgen_generated_tile.mmtile")
        Copy-IfExists $rawResult.PolyMeshObj (Join-Path $latest "mmap\$($tile.Name)_mmapgen_polymesh.obj")
        Copy-IfExists $rawResult.DetailMeshObj (Join-Path $latest "mmap\$($tile.Name)_mmapgen_detailmesh.obj")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "$tileKey.pmesh") (Join-Path $latest "mmap\$($tile.Name)_mmapgen_polymesh.pmesh")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "$tileKey.dmesh") (Join-Path $latest "mmap\$($tile.Name)_mmapgen_detailmesh.dmesh")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "map$tileKey.nav") (Join-Path $latest "mmap\$($tile.Name)_mmapgen_final_detour_tile.nav")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "map${tileKey}.source_triangles.csv") (Join-Path $latest "analysis\$($tile.Name)_compiled_adt_vmap_go_source_triangles.csv")

        $generatedTile = Join-Path $latest "mmap\$($tile.Name)_mmapgen_generated_tile.mmtile"
        if (Test-Path $generatedTile) {
            Invoke-MmapVisualize $generatedTile (Join-Path $latest "mmap\$($tile.Name)_mmapgen_generated_full.obj") $markers
        }
        Invoke-MmapVisualize $tile.Mmtile (Join-Path $latest "mmap\$($tile.Name)_mmap_runtime_full.obj") $markers -IncludeVmaps
    }

    Write-Readme (Join-Path $latest 'README.md') 'Orgrimmar City Pathfinding Reference' @(
        "- Scope: connected 2x2 Orgrimmar city tile bundle covering the zeppelin tower, auction-house/city-gate trophy tile, and the main gate approach.",
        "- Tiles: (39,28), (39,29), (40,28), and (40,29) on map 1.",
        "- source/ contains converted compiled ADT/VMAP/GO bake-input geometry per tile.",
        "- mmap/ contains runtime MMAP renderings plus MmapGen-generated tile snapshots for the same city tiles.",
        "- Active GameObject set: $(Format-GameObjectVariantSummary).",
        "- The staged RecastDemo layouts are intended for comparing city-head, bonfire, and seasonal-prop collision coverage across connected Orgrimmar tiles."
    )

    Stage-RecastDemoRoute 'Orgrimmar' 1 $markers @(
        @{ Source = (Join-Path $latest 'source\og_city_tile_3928_compiled_adt_vmap_go_full.obj'); Target = 'og_city_tile_3928_source_full.obj' },
        @{ Source = (Join-Path $latest 'source\og_city_tile_3929_compiled_adt_vmap_go_full.obj'); Target = 'og_city_tile_3929_source_full.obj' },
        @{ Source = (Join-Path $latest 'source\og_city_tile_4028_compiled_adt_vmap_go_full.obj'); Target = 'og_city_tile_4028_source_full.obj' },
        @{ Source = (Join-Path $latest 'source\og_city_tile_4029_compiled_adt_vmap_go_full.obj'); Target = 'og_city_tile_4029_source_full.obj' },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_3928_mmap_runtime_full.obj'); Target = 'og_city_tile_3928_runtime_full.obj' },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_3929_mmap_runtime_full.obj'); Target = 'og_city_tile_3929_runtime_full.obj' },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_4028_mmap_runtime_full.obj'); Target = 'og_city_tile_4028_runtime_full.obj' },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_4029_mmap_runtime_full.obj'); Target = 'og_city_tile_4029_runtime_full.obj' },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_3928_mmapgen_generated_full.obj'); Target = 'og_city_tile_3928_mmapgen_generated_full.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_3929_mmapgen_generated_full.obj'); Target = 'og_city_tile_3929_mmapgen_generated_full.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_4028_mmapgen_generated_full.obj'); Target = 'og_city_tile_4028_mmapgen_generated_full.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_4029_mmapgen_generated_full.obj'); Target = 'og_city_tile_4029_mmapgen_generated_full.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_3928_mmapgen_polymesh.obj'); Target = 'og_city_tile_3928_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_3929_mmapgen_polymesh.obj'); Target = 'og_city_tile_3929_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_4028_mmapgen_polymesh.obj'); Target = 'og_city_tile_4028_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_4029_mmapgen_polymesh.obj'); Target = 'og_city_tile_4029_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_3928_mmapgen_detailmesh.obj'); Target = 'og_city_tile_3928_mmapgen_detailmesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_3929_mmapgen_detailmesh.obj'); Target = 'og_city_tile_3929_mmapgen_detailmesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_4028_mmapgen_detailmesh.obj'); Target = 'og_city_tile_4028_mmapgen_detailmesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\og_city_tile_4029_mmapgen_detailmesh.obj'); Target = 'og_city_tile_4029_mmapgen_detailmesh.obj'; Required = $false }
    ) @(
        @{ Name = 'og_city_source_tiles.gset'; Title = 'Orgrimmar City Source Tiles'; Files = @('og_city_tile_3928_source_full.obj', 'og_city_tile_3929_source_full.obj', 'og_city_tile_4028_source_full.obj', 'og_city_tile_4029_source_full.obj') },
        @{ Name = 'og_city_runtime_tiles.gset'; Title = 'Orgrimmar City Runtime Tiles'; Files = @('og_city_tile_3928_runtime_full.obj', 'og_city_tile_3929_runtime_full.obj', 'og_city_tile_4028_runtime_full.obj', 'og_city_tile_4029_runtime_full.obj') },
        @{ Name = 'og_city_mmapgen_generated_tiles.gset'; Title = 'Orgrimmar City MmapGen Generated Tiles'; Files = @('og_city_tile_3928_mmapgen_generated_full.obj', 'og_city_tile_3929_mmapgen_generated_full.obj', 'og_city_tile_4028_mmapgen_generated_full.obj', 'og_city_tile_4029_mmapgen_generated_full.obj') },
        @{ Name = 'og_city_mmapgen_polymesh_tiles.gset'; Title = 'Orgrimmar City MmapGen Polymesh Tiles'; Files = @('og_city_tile_3928_mmapgen_polymesh.obj', 'og_city_tile_3929_mmapgen_polymesh.obj', 'og_city_tile_4028_mmapgen_polymesh.obj', 'og_city_tile_4029_mmapgen_polymesh.obj') },
        @{ Name = 'og_city_mmapgen_detailmesh_tiles.gset'; Title = 'Orgrimmar City MmapGen Detail Mesh Tiles'; Files = @('og_city_tile_3928_mmapgen_detailmesh.obj', 'og_city_tile_3929_mmapgen_detailmesh.obj', 'og_city_tile_4028_mmapgen_detailmesh.obj', 'og_city_tile_4029_mmapgen_detailmesh.obj') }
    )

    if (Test-Path (Join-Path $latest '_work')) {
        Remove-Item -LiteralPath (Join-Path $latest '_work') -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Export-Brd {
    $latest = Initialize-LatestDir (Join-Path $OutRoot 'brd\latest')

    $markers = @(
        (New-Point -7518.7 -2159.9 131.9 'flamecrest_start'),
        (New-Point -7519.0 -2100.4 130.2 'live_stall_ubrs'),
        (New-Point -7504.8 -2104.4 132.0 'live_stall_brd'),
        (New-Point -7187.0 -958.0 254.0 'brd_approach'),
        (New-Point -7179.0 -921.0 165.0 'literal_brd_portal'),
        (New-Point -7949.7 -1162.8 170.8 'brm_south_trap_z170_8'),
        (New-Point -7825.4 -1129.2 133.8 'brm_south_new_trap_z133_8')
    )
    $flamecrestStallCrop = New-Bounds -7545 -2135 118 -7480 -2075 150
    $brdApproachCrop = New-Bounds -7240 -1010 140 -7135 -890 275
    $southTrapCrop = New-Bounds -8010 -1225 115 -7780 -1080 190

    $tiles = @(
        [pscustomobject]@{
            Name = 'flamecrest_stall'
            Map = 0
            TileX = 35
            TileY = 46
            Mmtile = Join-Path $DataDir 'mmaps\0004635.mmtile'
            Crop = $flamecrestStallCrop
        },
        [pscustomobject]@{
            Name = 'brd_approach'
            Map = 0
            TileX = 33
            TileY = 45
            Mmtile = Join-Path $DataDir 'mmaps\0004533.mmtile'
            Crop = $brdApproachCrop
        },
        [pscustomobject]@{
            Name = 'brm_south_trap'
            Map = 0
            TileX = 34
            TileY = 46
            Mmtile = Join-Path $DataDir 'mmaps\0004634.mmtile'
            Crop = $southTrapCrop
        }
    )

    foreach ($tile in $tiles) {
        $tileKey = '{0:D3}{1:D2}{2:D2}' -f $tile.Map, $tile.TileY, $tile.TileX
        $workDir = Join-Path $OutRoot "brd\latest\_work\mmapgen_$tileKey"
        $logPath = Join-Path $latest "logs\mmapgen_tile_$tileKey.log"
        $rawResult = Get-MmapGenDebugTileResult $tile.Map $tile.TileX $tile.TileY $workDir $logPath
        if ($RefreshRaw -or -not ($Resume -and (Test-Path $rawResult.RawObj))) {
            $rawResult = Invoke-MmapGenDebugTile $tile.Map $tile.TileX $tile.TileY $workDir $logPath
        }

        Convert-RawMmapGenObj $rawResult.RawObj (Join-Path $latest "source\$($tile.Name)_compiled_adt_vmap_go_full.obj")
        Convert-RawMmapGenObj $rawResult.RawObj (Join-Path $latest "source\$($tile.Name)_compiled_adt_vmap_go_crop.obj") $tile.Crop
        Copy-Item -Force (Join-Path $latest "source\$($tile.Name)_compiled_adt_vmap_go_crop.obj") (Join-Path $latest "source\$($tile.Name)_compiled_adt_vmap_go_all_sources.obj")

        Copy-IfExists $rawResult.Mmtile (Join-Path $latest "mmap\$($tile.Name)_mmapgen_generated_tile.mmtile")
        Copy-IfExists $rawResult.PolyMeshObj (Join-Path $latest "mmap\$($tile.Name)_mmapgen_polymesh.obj")
        Copy-IfExists $rawResult.DetailMeshObj (Join-Path $latest "mmap\$($tile.Name)_mmapgen_detailmesh.obj")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "$tileKey.pmesh") (Join-Path $latest "mmap\$($tile.Name)_mmapgen_polymesh.pmesh")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "$tileKey.dmesh") (Join-Path $latest "mmap\$($tile.Name)_mmapgen_detailmesh.dmesh")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "map$tileKey.nav") (Join-Path $latest "mmap\$($tile.Name)_mmapgen_final_detour_tile.nav")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "map${tileKey}_stage_heightfield_spans.csv") (Join-Path $latest "analysis\$($tile.Name)_mmapgen_stage_heightfield_spans.csv")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "map${tileKey}_stage_compact_spans.csv") (Join-Path $latest "analysis\$($tile.Name)_mmapgen_stage_compact_spans.csv")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "map${tileKey}_stage_contours.csv") (Join-Path $latest "analysis\$($tile.Name)_mmapgen_stage_contours.csv")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "map${tileKey}.source_triangles.csv") (Join-Path $latest "analysis\$($tile.Name)_compiled_adt_vmap_go_source_triangles.csv")

        $generatedTile = Join-Path $latest "mmap\$($tile.Name)_mmapgen_generated_tile.mmtile"
        if (Test-Path $generatedTile) {
            Invoke-MmapVisualize $generatedTile (Join-Path $latest "mmap\$($tile.Name)_mmapgen_generated_crop.obj") $markers $tile.Crop (Join-Path $latest "analysis\$($tile.Name)_mmapgen_generated_crop_polys.csv")
        }
        Invoke-MmapVisualize $tile.Mmtile (Join-Path $latest "mmap\$($tile.Name)_mmap_full_with_vmap_bounds.obj") $markers -IncludeVmaps
        Invoke-MmapVisualize $tile.Mmtile (Join-Path $latest "mmap\$($tile.Name)_mmap_crop.obj") $markers $tile.Crop (Join-Path $latest "analysis\$($tile.Name)_mmap_crop_polys.csv")
    }

    $referencePoints = New-Object System.Collections.Generic.List[object]
    foreach ($m in $markers) { $referencePoints.Add($m) }
    Write-Waypoints $referencePoints (Join-Path $latest 'analysis\reference_points.csv') (Join-Path $latest 'overlays\reference_points.obj')

    Write-Readme (Join-Path $latest 'README.md') 'BRD/BRM Pathfinding Reference' @(
        "- Scope: Flame Crest live-stall tile, Blackrock Depths approach, and the known BRM south-face trap tile.",
        "- Flame Crest stall tile: map 0, MmapGen tile 35,46, runtime 0004635.mmtile.",
        "- BRD approach tile: map 0, MmapGen tile 33,45, runtime 0004533.mmtile.",
        "- South trap tile: map 0, MmapGen tile 34,46, runtime 0004634.mmtile.",
        "- source/ contains converted compiled ADT/VMAP/GO bake-input geometry from MmapGen debug output; *_all_sources.obj keeps terrain/vmap/gameobject/liquid materials visible for direct source-vs-mmap inspection.",
        "- mmap/ contains Detour/MMAP renderings for the same tiles, including *_mmapgen_generated_* files before runtime promotion.",
        "- analysis/flamecrest_stall_mmap_crop_polys.csv, analysis/brd_approach_mmap_crop_polys.csv, and analysis/brm_south_trap_mmap_crop_polys.csv are the focused polygon reports.",
        "- Active GameObject set: $(Format-GameObjectVariantSummary). MmapGen logs in logs/ show whether static GameObject spawns were baked for each tile."
    )

    Stage-RecastDemoRoute 'BlackrockMountain' 0 $markers @(
        @{ Source = (Join-Path $latest 'source\flamecrest_stall_compiled_adt_vmap_go_full.obj'); Target = 'flamecrest_source_full.obj' },
        @{ Source = (Join-Path $latest 'source\brd_approach_compiled_adt_vmap_go_full.obj'); Target = 'brd_approach_source_full.obj' },
        @{ Source = (Join-Path $latest 'source\brm_south_trap_compiled_adt_vmap_go_full.obj'); Target = 'brm_south_trap_source_full.obj' },
        @{ Source = (Join-Path $latest 'source\flamecrest_stall_compiled_adt_vmap_go_crop.obj'); Target = 'flamecrest_source_crop.obj' },
        @{ Source = (Join-Path $latest 'source\brd_approach_compiled_adt_vmap_go_crop.obj'); Target = 'brd_approach_source_crop.obj' },
        @{ Source = (Join-Path $latest 'source\brm_south_trap_compiled_adt_vmap_go_crop.obj'); Target = 'brm_south_trap_source_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\flamecrest_stall_mmap_crop.obj'); Target = 'flamecrest_runtime_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\brd_approach_mmap_crop.obj'); Target = 'brd_approach_runtime_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\brm_south_trap_mmap_crop.obj'); Target = 'brm_south_trap_runtime_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\flamecrest_stall_mmapgen_generated_crop.obj'); Target = 'flamecrest_mmapgen_generated_crop.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\brd_approach_mmapgen_generated_crop.obj'); Target = 'brd_approach_mmapgen_generated_crop.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\brm_south_trap_mmapgen_generated_crop.obj'); Target = 'brm_south_trap_mmapgen_generated_crop.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\flamecrest_stall_mmapgen_polymesh.obj'); Target = 'flamecrest_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\brd_approach_mmapgen_polymesh.obj'); Target = 'brd_approach_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\brm_south_trap_mmapgen_polymesh.obj'); Target = 'brm_south_trap_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\flamecrest_stall_mmapgen_detailmesh.obj'); Target = 'flamecrest_mmapgen_detailmesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\brd_approach_mmapgen_detailmesh.obj'); Target = 'brd_approach_mmapgen_detailmesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\brm_south_trap_mmapgen_detailmesh.obj'); Target = 'brm_south_trap_mmapgen_detailmesh.obj'; Required = $false }
    ) @(
        @{ Name = 'brm_focus_source_tiles.gset'; Title = 'Blackrock Mountain Source Focus Tiles'; Files = @('flamecrest_source_crop.obj', 'brd_approach_source_crop.obj', 'brm_south_trap_source_crop.obj') },
        @{ Name = 'brm_focus_runtime_tiles.gset'; Title = 'Blackrock Mountain Runtime Focus Tiles'; Files = @('flamecrest_runtime_crop.obj', 'brd_approach_runtime_crop.obj', 'brm_south_trap_runtime_crop.obj') },
        @{ Name = 'brm_focus_mmapgen_generated_tiles.gset'; Title = 'Blackrock Mountain MmapGen Generated Focus Tiles'; Files = @('flamecrest_mmapgen_generated_crop.obj', 'brd_approach_mmapgen_generated_crop.obj', 'brm_south_trap_mmapgen_generated_crop.obj') },
        @{ Name = 'brm_focus_mmapgen_polymesh_tiles.gset'; Title = 'Blackrock Mountain MmapGen Polymesh Tiles'; Files = @('flamecrest_mmapgen_polymesh.obj', 'brd_approach_mmapgen_polymesh.obj', 'brm_south_trap_mmapgen_polymesh.obj') },
        @{ Name = 'brm_focus_mmapgen_detailmesh_tiles.gset'; Title = 'Blackrock Mountain MmapGen Detail Mesh Tiles'; Files = @('flamecrest_mmapgen_detailmesh.obj', 'brd_approach_mmapgen_detailmesh.obj', 'brm_south_trap_mmapgen_detailmesh.obj') },
        @{ Name = 'brm_source_full_tiles.gset'; Title = 'Blackrock Mountain Source Full Tiles'; Files = @('flamecrest_source_full.obj', 'brd_approach_source_full.obj', 'brm_south_trap_source_full.obj') }
    )

    if (Test-Path (Join-Path $latest '_work')) {
        Remove-Item -LiteralPath (Join-Path $latest '_work') -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Export-Rfc {
    $latest = Initialize-LatestDir (Join-Path $OutRoot 'rfc\latest')

    $markers = @(
        (New-Point -226.0 -60.0 -25.0 'rfc_corridor_start'),
        (New-Point -300.0 -40.0 -25.0 'rfc_corridor_end'),
        (New-Point -260.0 -52.0 -24.5 'rfc_center')
    )
    $corridorCrop = New-Bounds -340 -120 -60 -180 10 20

    $tiles = @(
        [pscustomobject]@{ Name = 'rfc_tile_3131'; Map = 389; TileX = 31; TileY = 31; Mmtile = Join-Path $DataDir 'mmaps\3893131.mmtile'; Crop = $corridorCrop },
        [pscustomobject]@{ Name = 'rfc_tile_3132'; Map = 389; TileX = 32; TileY = 31; Mmtile = Join-Path $DataDir 'mmaps\3893132.mmtile'; Crop = $corridorCrop },
        [pscustomobject]@{ Name = 'rfc_tile_3231'; Map = 389; TileX = 31; TileY = 32; Mmtile = Join-Path $DataDir 'mmaps\3893231.mmtile'; Crop = $corridorCrop },
        [pscustomobject]@{ Name = 'rfc_tile_3232'; Map = 389; TileX = 32; TileY = 32; Mmtile = Join-Path $DataDir 'mmaps\3893232.mmtile'; Crop = $corridorCrop }
    )

    foreach ($tile in $tiles) {
        $tileKey = '{0:D3}{1:D2}{2:D2}' -f $tile.Map, $tile.TileY, $tile.TileX
        $workDir = Join-Path $OutRoot "rfc\latest\_work\mmapgen_$tileKey"
        $logPath = Join-Path $latest "logs\mmapgen_tile_$tileKey.log"
        $rawResult = Get-MmapGenDebugTileResult $tile.Map $tile.TileX $tile.TileY $workDir $logPath
        if ($RefreshRaw -or -not ($Resume -and (Test-Path $rawResult.RawObj))) {
            $rawResult = Invoke-MmapGenDebugTile $tile.Map $tile.TileX $tile.TileY $workDir $logPath
        }

        Convert-RawMmapGenObj $rawResult.RawObj (Join-Path $latest "source\$($tile.Name)_compiled_adt_vmap_go_full.obj")
        Convert-RawMmapGenObj $rawResult.RawObj (Join-Path $latest "source\$($tile.Name)_compiled_adt_vmap_go_crop.obj") $tile.Crop
        Copy-Item -Force (Join-Path $latest "source\$($tile.Name)_compiled_adt_vmap_go_crop.obj") (Join-Path $latest "source\$($tile.Name)_compiled_adt_vmap_go_all_sources.obj")

        Copy-IfExists $rawResult.Mmtile (Join-Path $latest "mmap\$($tile.Name)_mmapgen_generated_tile.mmtile")
        Copy-IfExists $rawResult.PolyMeshObj (Join-Path $latest "mmap\$($tile.Name)_mmapgen_polymesh.obj")
        Copy-IfExists $rawResult.DetailMeshObj (Join-Path $latest "mmap\$($tile.Name)_mmapgen_detailmesh.obj")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "$tileKey.pmesh") (Join-Path $latest "mmap\$($tile.Name)_mmapgen_polymesh.pmesh")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "$tileKey.dmesh") (Join-Path $latest "mmap\$($tile.Name)_mmapgen_detailmesh.dmesh")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "map$tileKey.nav") (Join-Path $latest "mmap\$($tile.Name)_mmapgen_final_detour_tile.nav")
        Copy-IfExists (Join-Path (Split-Path -Parent $rawResult.RawObj) "map${tileKey}.source_triangles.csv") (Join-Path $latest "analysis\$($tile.Name)_compiled_adt_vmap_go_source_triangles.csv")

        $generatedTile = Join-Path $latest "mmap\$($tile.Name)_mmapgen_generated_tile.mmtile"
        if (Test-Path $generatedTile) {
            Invoke-MmapVisualize $generatedTile (Join-Path $latest "mmap\$($tile.Name)_mmapgen_generated_crop.obj") $markers $tile.Crop (Join-Path $latest "analysis\$($tile.Name)_mmapgen_generated_crop_polys.csv")
        }
        Invoke-MmapVisualize $tile.Mmtile (Join-Path $latest "mmap\$($tile.Name)_mmap_full_with_vmap_bounds.obj") $markers -IncludeVmaps
        Invoke-MmapVisualize $tile.Mmtile (Join-Path $latest "mmap\$($tile.Name)_mmap_crop.obj") $markers $tile.Crop (Join-Path $latest "analysis\$($tile.Name)_mmap_crop_polys.csv")
    }

    $referencePoints = New-Object System.Collections.Generic.List[object]
    foreach ($m in $markers) { $referencePoints.Add($m) }
    Write-Waypoints $referencePoints (Join-Path $latest 'analysis\reference_points.csv') (Join-Path $latest 'overlays\reference_points.obj')

    Write-Readme (Join-Path $latest 'README.md') 'Ragefire Chasm Pathfinding Reference' @(
        "- Scope: compact dungeon corridor inspection set for Ragefire Chasm (map 389).",
        "- Intended use: fast UI iteration and indoor hallway/doorway navmesh comparisons in RecastDemo.",
        "- source/ contains converted compiled ADT/VMAP/GO bake-input geometry per RFC tile.",
        "- mmap/ contains runtime MMAP renderings and MmapGen-generated tile snapshots for the same RFC tiles.",
        "- analysis/*_mmap_crop_polys.csv provides focused polygon reports around the RFC corridor probe.",
        "- Active GameObject set: $(Format-GameObjectVariantSummary)."
    )

    Stage-RecastDemoRoute 'RagefireChasm' 389 $markers @(
        @{ Source = (Join-Path $latest 'source\rfc_tile_3131_compiled_adt_vmap_go_crop.obj'); Target = 'rfc_tile_3131_source_crop.obj' },
        @{ Source = (Join-Path $latest 'source\rfc_tile_3132_compiled_adt_vmap_go_crop.obj'); Target = 'rfc_tile_3132_source_crop.obj' },
        @{ Source = (Join-Path $latest 'source\rfc_tile_3231_compiled_adt_vmap_go_crop.obj'); Target = 'rfc_tile_3231_source_crop.obj' },
        @{ Source = (Join-Path $latest 'source\rfc_tile_3232_compiled_adt_vmap_go_crop.obj'); Target = 'rfc_tile_3232_source_crop.obj' },
        @{ Source = (Join-Path $latest 'source\rfc_tile_3131_compiled_adt_vmap_go_full.obj'); Target = 'rfc_tile_3131_source_full.obj' },
        @{ Source = (Join-Path $latest 'source\rfc_tile_3132_compiled_adt_vmap_go_full.obj'); Target = 'rfc_tile_3132_source_full.obj' },
        @{ Source = (Join-Path $latest 'source\rfc_tile_3231_compiled_adt_vmap_go_full.obj'); Target = 'rfc_tile_3231_source_full.obj' },
        @{ Source = (Join-Path $latest 'source\rfc_tile_3232_compiled_adt_vmap_go_full.obj'); Target = 'rfc_tile_3232_source_full.obj' },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3131_mmap_crop.obj'); Target = 'rfc_tile_3131_runtime_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3132_mmap_crop.obj'); Target = 'rfc_tile_3132_runtime_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3231_mmap_crop.obj'); Target = 'rfc_tile_3231_runtime_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3232_mmap_crop.obj'); Target = 'rfc_tile_3232_runtime_crop.obj' },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3131_mmapgen_generated_crop.obj'); Target = 'rfc_tile_3131_mmapgen_generated_crop.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3132_mmapgen_generated_crop.obj'); Target = 'rfc_tile_3132_mmapgen_generated_crop.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3231_mmapgen_generated_crop.obj'); Target = 'rfc_tile_3231_mmapgen_generated_crop.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3232_mmapgen_generated_crop.obj'); Target = 'rfc_tile_3232_mmapgen_generated_crop.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3131_mmapgen_polymesh.obj'); Target = 'rfc_tile_3131_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3132_mmapgen_polymesh.obj'); Target = 'rfc_tile_3132_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3231_mmapgen_polymesh.obj'); Target = 'rfc_tile_3231_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3232_mmapgen_polymesh.obj'); Target = 'rfc_tile_3232_mmapgen_polymesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3131_mmapgen_detailmesh.obj'); Target = 'rfc_tile_3131_mmapgen_detailmesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3132_mmapgen_detailmesh.obj'); Target = 'rfc_tile_3132_mmapgen_detailmesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3231_mmapgen_detailmesh.obj'); Target = 'rfc_tile_3231_mmapgen_detailmesh.obj'; Required = $false },
        @{ Source = (Join-Path $latest 'mmap\rfc_tile_3232_mmapgen_detailmesh.obj'); Target = 'rfc_tile_3232_mmapgen_detailmesh.obj'; Required = $false }
    ) @(
        @{ Name = 'rfc_corridor_source_tiles.gset'; Title = 'Ragefire Chasm Corridor Source Tiles'; Files = @('rfc_tile_3131_source_crop.obj', 'rfc_tile_3132_source_crop.obj', 'rfc_tile_3231_source_crop.obj', 'rfc_tile_3232_source_crop.obj') },
        @{ Name = 'rfc_corridor_runtime_tiles.gset'; Title = 'Ragefire Chasm Corridor Runtime Tiles'; Files = @('rfc_tile_3131_runtime_crop.obj', 'rfc_tile_3132_runtime_crop.obj', 'rfc_tile_3231_runtime_crop.obj', 'rfc_tile_3232_runtime_crop.obj') },
        @{ Name = 'rfc_corridor_mmapgen_generated_tiles.gset'; Title = 'Ragefire Chasm MmapGen Generated Tiles'; Files = @('rfc_tile_3131_mmapgen_generated_crop.obj', 'rfc_tile_3132_mmapgen_generated_crop.obj', 'rfc_tile_3231_mmapgen_generated_crop.obj', 'rfc_tile_3232_mmapgen_generated_crop.obj') },
        @{ Name = 'rfc_corridor_mmapgen_polymesh_tiles.gset'; Title = 'Ragefire Chasm MmapGen Polymesh Tiles'; Files = @('rfc_tile_3131_mmapgen_polymesh.obj', 'rfc_tile_3132_mmapgen_polymesh.obj', 'rfc_tile_3231_mmapgen_polymesh.obj', 'rfc_tile_3232_mmapgen_polymesh.obj') },
        @{ Name = 'rfc_corridor_mmapgen_detailmesh_tiles.gset'; Title = 'Ragefire Chasm MmapGen Detail Mesh Tiles'; Files = @('rfc_tile_3131_mmapgen_detailmesh.obj', 'rfc_tile_3132_mmapgen_detailmesh.obj', 'rfc_tile_3231_mmapgen_detailmesh.obj', 'rfc_tile_3232_mmapgen_detailmesh.obj') },
        @{ Name = 'rfc_full_source_tiles.gset'; Title = 'Ragefire Chasm Full Source Tiles'; Files = @('rfc_tile_3131_source_full.obj', 'rfc_tile_3132_source_full.obj', 'rfc_tile_3231_source_full.obj', 'rfc_tile_3232_source_full.obj') }
    )

    if (Test-Path (Join-Path $latest '_work')) {
        Remove-Item -LiteralPath (Join-Path $latest '_work') -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Clear-LegacyOg {
    $legacy = Assert-UnderRepoTmp 'tmp\test-runtime\visualization\og-zeppelin-tower'
    if (Test-Path $legacy) {
        try {
            Get-ChildItem -LiteralPath $legacy -Force | Remove-Item -Recurse -Force
        } catch {
            Write-Warning "Could not fully clean legacy OG visualization folder: $($_.Exception.Message)"
        }
    }
}

if ($Route -eq 'og-zeppelin' -or $Route -eq 'all') {
    Export-OgZeppelin
}
if ($Route -eq 'og-city' -or $Route -eq 'all') {
    Export-OgCity
}
if ($Route -eq 'brd' -or $Route -eq 'all') {
    Export-Brd
}
if ($Route -eq 'rfc' -or $Route -eq 'all') {
    Export-Rfc
}
if ($CleanLegacyOgFolder) {
    Clear-LegacyOg
}

Write-Host "[export-pathfinding-reference] DONE -> $(Resolve-RepoPath $OutRoot)" -ForegroundColor Green
