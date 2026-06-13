# Prompt - restore MaNGOS server mmaps and isolate bot bakes

Use this prompt when picking up the mmap rescue work. The immediate goal is to
restore server NPC pathing by regenerating the MaNGOS server's own `mmaps/`
with the server-side generator, then separately analyze which bot route tiles
are actually needed for a smaller bot mmap set.

## Stop condition

Halt live long-pathing and transport debugging until this is resolved. Do not
copy any bot-generated `mmaps/` back into `D:/MaNGOS/data`.

The suspected bad state is present now:

| Path | Current observation |
|---|---|
| `D:/MaNGOS/data/mmaps` | 845 files, 777,293,504 bytes, prefixes: `001=845` only. Eastern Kingdoms map `000` is absent. |
| `D:/MaNGOS/data/mmaps.iter21-failed` | 3,079 files, 3,305,561,188 bytes. Explicitly named failed; do not promote or copy from it. |
| `D:/MaNGOS/data/mmaps_backup_20260315` | 2,004 files, includes `000=516`, `001=787`, instances, raids, and BGs. Candidate emergency restore baseline. |
| `D:/MaNGOS/data/mmaps_old_pre_reextract` | 2,004 files, similar prefix coverage to the March 15 backup. Candidate emergency restore baseline. |
| `D:/wwow-bot/test-data/mmaps` | 1,847 files, newest 2026-06-07, bot-owned scratch data. Not server input. |
| `D:/wwow-bot/prod-data/mmaps` | 1,828 files, bot production data for `wwow-pathfinding` and `wwow-scene-data`. Not server input. |

Comparisons:

- `D:/MaNGOS/data/mmaps` is missing 1,003 filenames that exist in
  `D:/wwow-bot/test-data/mmaps`, including `000.mmap` and Eastern Kingdoms
  tiles such as `0002239.mmtile`.
- `D:/MaNGOS/data/mmaps` is missing 983 filenames that exist in
  `D:/wwow-bot/prod-data/mmaps`.
- Many shared Kalimdor filenames differ in size, so this is not a clean copy
  of either bot root.

## Load-bearing rules

Read these before doing anything destructive:

1. `docs/physics/MMAP_DATA_FLOW.md`
2. `tools/MmapGen/AGENTS.md`
3. `tools/MmapGen/README.md`
4. `docs/Plan/Pathfinding/BAKE_RECIPE.md`

Rules to carry forward:

- `D:/MaNGOS/data` is server-owned and read-only for the bot pipeline.
- Bot iteration bakes to `D:/wwow-bot/test-data`; approved bot tiles promote to
  `D:/wwow-bot/prod-data` with `tools/MmapGen/promote-mmaps.ps1`.
- The MaNGOS server consumes `D:/MaNGOS/data` through
  `docker-compose.vmangos-linux.yml` as `/opt/vmangos/storage/data:ro`.
- The bot Docker services consume bot mmaps from `D:/wwow-bot/prod-data/mmaps`
  plus server-owned `maps/` and `vmaps/`.
- The in-tree `tools/MmapGen/build/MmapGen.exe` is for bot nav data. It uses
  bot-specific capsule/offmesh/config decisions. Do not use it as the first
  responder for server NPC mmaps.
- For server NPC pathing, use the server generator at
  `D:/MaNGOS/source/bin/MoveMapGenerator.exe`, or restore a known-good server
  backup as an emergency rollback.

## Prompt for the regeneration operator

You are restoring MaNGOS server mmaps after bot-generated/bad pathfinding files
were copied into `D:/MaNGOS/data/mmaps`, breaking NPC behavior.

Your priorities, in order:

1. Preserve evidence and make a reversible backup of the current bad server
   `mmaps/`.
2. Regenerate or restore the MaNGOS server's expected mmap set using
   `D:/MaNGOS/source/bin/MoveMapGenerator.exe`, not the bot `MmapGen.exe`.
3. Verify the regenerated server set has both map `000` and map `001`, plus the
   expected instance/map coverage.
4. Restart only the specific MaNGOS container that must reload mmaps. Do not
   kill broad `dotnet`, game, or Docker processes.
5. Keep bot mmap analysis isolated under `D:/wwow-bot/test-data` and
   `D:/wwow-bot/prod-data`.

### Preflight inventory

Run these read-only checks first:

```powershell
Get-Process MoveMapGenerator -ErrorAction SilentlyContinue

$roots = @(
  'D:\MaNGOS\data\mmaps',
  'D:\MaNGOS\data\mmaps.iter21-failed',
  'D:\MaNGOS\data\mmaps_backup_20260315',
  'D:\MaNGOS\data\mmaps_old_pre_reextract',
  'D:\wwow-bot\test-data\mmaps',
  'D:\wwow-bot\prod-data\mmaps'
)

foreach ($root in $roots) {
  if (-not (Test-Path -LiteralPath $root)) {
    [pscustomobject]@{ Path = $root; Exists = $false }
    continue
  }

  $files = Get-ChildItem -LiteralPath $root -File -Recurse
  $prefixes = $files |
    Group-Object { if ($_.Name -match '^(\d{3})') { $matches[1] } else { 'other' } } |
    Sort-Object Name |
    ForEach-Object { "$($_.Name)=$($_.Count)" }

  [pscustomobject]@{
    Path = $root
    Exists = $true
    Files = $files.Count
    Bytes = ($files | Measure-Object Length -Sum).Sum
    Newest = ($files | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
    Prefixes = ($prefixes -join ', ')
  }
}
```

### Backup and full server regeneration

Prefer a controlled maintenance window. If the server is running, stop or
restart only the named MaNGOS service when required.

```powershell
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$data = 'D:\MaNGOS\data'
$bad = Join-Path $data 'mmaps'
$backupName = "mmaps.bad-$stamp"

if (-not (Test-Path -LiteralPath $bad)) {
  throw "Expected server mmaps directory not found: $bad"
}

Rename-Item -LiteralPath $bad -NewName $backupName
New-Item -ItemType Directory -Path (Join-Path $data 'mmaps') | Out-Null

Push-Location $data
try {
  & 'D:\MaNGOS\source\bin\MoveMapGenerator.exe' --threads 8 --silent *> "server-mmap-all-$stamp.log"
  if ($LASTEXITCODE -ne 0) {
    throw "MoveMapGenerator failed with exit code $LASTEXITCODE"
  }
} finally {
  Pop-Location
}
```

If full generation is too slow and the immediate rescue only needs continents,
use the same backup steps and run map `0` and map `1` explicitly:

```powershell
Push-Location 'D:\MaNGOS\data'
try {
  & 'D:\MaNGOS\source\bin\MoveMapGenerator.exe' 0 --threads 8 --silent *> "server-mmap-map000-$stamp.log"
  if ($LASTEXITCODE -ne 0) { throw "Map 000 generation failed: $LASTEXITCODE" }

  & 'D:\MaNGOS\source\bin\MoveMapGenerator.exe' 1 --threads 8 --silent *> "server-mmap-map001-$stamp.log"
  if ($LASTEXITCODE -ne 0) { throw "Map 001 generation failed: $LASTEXITCODE" }
} finally {
  Pop-Location
}
```

Emergency rollback option, if regeneration cannot complete:

```powershell
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$data = 'D:\MaNGOS\data'
$bad = Join-Path $data 'mmaps'
$source = Join-Path $data 'mmaps_backup_20260315'

# Path-containment guard: refuse to mutate anything outside D:\MaNGOS\data.
$resolvedData = (Resolve-Path -LiteralPath $data).Path
$resolvedBad  = (Resolve-Path -LiteralPath $bad).Path
$resolvedSrc  = (Resolve-Path -LiteralPath $source).Path
$dataPrefix = $resolvedData.TrimEnd('\') + '\'
if (-not $resolvedBad.StartsWith($dataPrefix, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to mutate a path outside ${data}: $resolvedBad"
}
if (-not $resolvedSrc.StartsWith($dataPrefix, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Restore source outside ${data}: $resolvedSrc"
}

docker stop wow-mangosd   # stop only mangosd to avoid file locks during the copy
Rename-Item -LiteralPath $bad -NewName "mmaps.bad-$stamp"   # preserve the bad set as evidence
Copy-Item -LiteralPath $source -Destination $bad -Recurse
docker start wow-mangosd  # reloads the restored server mmaps
```

This emergency-restore path was the one actually used in the 2026-06-13 rescue
(the local generation environment was bot-contaminated: a bot-capsule
`config.json` in `D:\MaNGOS\data` plus broken 5/31 regen artifacts), so
regeneration was skipped in favor of restoring the known-good
`mmaps_backup_20260315`.

### Server verification

After regeneration/restoration:

```powershell
$files = Get-ChildItem -LiteralPath 'D:\MaNGOS\data\mmaps' -File
$files | Group-Object { if ($_.Name -match '^(\d{3})') { $matches[1] } else { 'other' } } |
  Sort-Object Name |
  Select-Object Name, Count

Test-Path 'D:\MaNGOS\data\mmaps\000.mmap'
Test-Path 'D:\MaNGOS\data\mmaps\001.mmap'
```

Expected minimum:

- `000.mmap` exists.
- `001.mmap` exists.
- Prefixes include `000` and `001`.
- If doing full server generation, instance/BG/raid prefixes should be similar
  to the March 2026 server backups, not a single `001`-only set.

Restart only the server container that must reload server mmaps:

```powershell
docker restart wow-mangosd
```

If login/auth state is also unhealthy, restart only the named dependency after
the health probe identifies it, for example `wow-realmd` or `maria-db`.

## Do not repeat these attempts

- Do not copy from `D:/wwow-bot/test-data/mmaps` into `D:/MaNGOS/data/mmaps`.
- Do not copy from `D:/wwow-bot/prod-data/mmaps` into `D:/MaNGOS/data/mmaps`.
- Do not promote `D:/MaNGOS/data/mmaps.iter21-failed`; its name and inventory
  mark it as failed experiment output.
- Do not validate by file count or byte size alone. Check prefixes and route
  behavior.
- Do not bake the OG tower using swapped coordinates. The correct MmapGen CLI
  tile for OG tower is `--tile 40,29`, which writes `0012940.mmtile`.
  `0014029.mmtile` is the swapped tile.
- Do not treat endpoint tile coverage as proof of full route coverage. Use
  route probes to collect the actual Detour corridor tiles before deleting or
  omitting tiles.
- Do not use managed-side path repairs to compensate for server NPC mmap
  corruption.

## Route coverage for a simpler bot mmap set

This is separate from server rescue. Do this only after the server mmaps are
healthy and bot data is isolated again.

Tile convention:

```text
tileX = floor((17066.6664 - worldY) / 533.3333)
tileY = floor((17066.6664 - worldX) / 533.3333)
filename = <map><tileY:02d><tileX:02d>.mmtile
```

Endpoint-only coverage from current checked-in route manifests:

| Route manifest | Endpoint mmtile candidates |
|---|---|
| `tools/scripts/routes/og-zeppelin.json` | `0012840.mmtile`, `0012940.mmtile` |
| `tools/scripts/routes/og-zeppelin-shortroutes.json` | `0012940.mmtile` |
| `tools/scripts/routes/brm-dungeons.json` | `0004636.mmtile`, `0004634.mmtile`, `0004533.mmtile` |

Important: this is a seed list only. It does not include every tile the solved
path corridor crosses.

Fresh non-destructive route probes were run on 2026-06-13 against
`D:/wwow-bot/test-data` and wrote artifacts under
`tmp/bake-sweeps/route-coverage-20260613T143352Z/`. The second manifest probe
overwrote the aggregate `probe-results.json`, but all per-route `probe-*.json`
files are present.

Waypoint-derived tile coverage from those probe outputs:

| Route group | Current waypoint-derived tiles | Read |
|---|---|---|
| OG staged/diagnostic routes | `0012840.mmtile`, `0012940.mmtile` | This is the current bot long-pathing route surface for the Orgrimmar tower/front-gate side. Several diagnostic routes still report first-failure segments, so this is coverage, not a green verdict. |
| BRM routes | `0004635.mmtile`, `0004636.mmtile` | These probes do not reach the requested BRM endpoints. Terminal distances remain about 909-1214 yards from the target, with first failure at segment 0 for all four Flame Crest routes. Do not use this as the full BRM tile set. |

Per-route probe summary:

| Route | Tiles | First failure | Terminal distance to requested end |
|---|---|---:|---:|
| `ClimbOrgrimmarTowerToBoardingPlatform` | `0012840`, `0012940` | 101 | 0.52 |
| `FlightMasterDescentControl` | `0012840` | 101 | 0 |
| `LowerCityCornerToBonfireSouthDescentUpper` | `0012840` | 9 | 0 |
| `BonfireSouthDescentUpperToBonfireSouthDescent` | `0012840` | -1 | 0 |
| `BonfireSouthDescentToBonfireSouthCrossing` | `0012840` | -1 | 0 |
| `BonfireSouthCrossingToBonfireWestSouthClear` | `0012840` | -1 | 0 |
| `BonfireWestSouthClearToBonfireWestLane` | `0012840`, `0012940` | 5 | 0 |
| `BonfireWestLaneToBonfireWestClear` | `0012940` | -1 | 0 |
| `BonfireWestClearToArgentLaneInner` | `0012940` | -1 | 0 |
| `OgApproachToBoardingPosition` | `0012940` | 0 | 0.52 |
| `OgFrezzaToBoardingPosition` | `0012940` | 1 | 0.52 |
| `DeckLipStallToFrezza` | `0012940` | 0 | 0 |
| `FlameCrestToBlackrockDepths` | `0004635`, `0004636` | 0 | 1213.72 |
| `FlameCrestToLowerBlackrockSpire` | `0004635`, `0004636` | 0 | 916.11 |
| `FlameCrestToUpperBlackrockSpire` | `0004635`, `0004636` | 0 | 909.22 |
| `FlameCrestToBlackwingLair` | `0004635`, `0004636` | 0 | 929.36 |

To get real route coverage, run probes against bot test-data:

```powershell
$variant = "route-coverage-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))"

.\tools\scripts\probe-routes.ps1 `
  -Manifest tools\scripts\routes\og-zeppelin.json `
  -DataDir D:\wwow-bot\test-data `
  -Variant $variant `
  -DetourResolve `
  -SmoothPath

.\tools\scripts\probe-routes.ps1 `
  -Manifest tools\scripts\routes\brm-dungeons.json `
  -DataDir D:\wwow-bot\test-data `
  -Variant $variant `
  -DetourResolve `
  -SmoothPath
```

Then inspect `tmp/bake-sweeps/<variant>/probe-results.json` and each
`probe-*.json`. If the probe output does not record touched tiles directly,
compute the tile for each emitted waypoint using the convention above and
deduplicate by `(map, tileX, tileY)`.

Candidate simplification tracks:

1. Server NPC rescue set: regenerate the full server set first. After NPC
   behavior is healthy, evaluate whether the server can run with continents
   only (`0`, `1`) plus specific active instances. Do not make that reduction
   during the rescue pass.
2. Bot long-pathing set: for Crossroads -> Orgrimmar -> Undercity, begin with
   the route-observed `0012840` and `0012940` surface. For Flame Crest -> BRM,
   do not shrink yet: the current failing partial paths touch `0004635` and
   `0004636`, while the endpoint seed list still includes `0004634` and
   `0004533`. BRM needs an endpoint-reaching probe before any reduced set is
   trustworthy.
3. Bot regression set: retain the vanilla dungeon/raid/BG maps that current
   tests or catalogs actually exercise. `tools/scripts/bake-all-maps.ps1`
   currently defaults to maps `0`, `1`, `33`, `34`, `36`, `43`, `47`, `48`,
   `70`, `90`, `109`, `129`, `189`, `209`, `229`, `230`, `289`, `329`,
   `349`, `389`, and `429`; shrink this only after tests prove unused maps are
   genuinely outside the current validation surface.

## Current handoff state

- Live Crossroads -> Undercity transport/pathing attempts are paused.
- A focused transport logic unit slice was green before this pause, but live
  validation is not trustworthy while the server mmap root is suspect.
- The next irreversible action should be a backup/regen or backup/restore of
  `D:/MaNGOS/data/mmaps`, not another BotRunner pathing retry.
