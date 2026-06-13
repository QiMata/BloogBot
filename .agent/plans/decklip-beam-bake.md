# Execution Plan: Deck-Lip Beam Bake

## Goal
Make tile `1:40,29` bake walkable navmesh over the Orgrimmar zeppelin
deck-lip beam treads so the lip-to-deck climb can be represented by real
polygons, without off-mesh links, runtime physics tolerance changes, or consumer
movement changes.

## Current behavior
The target live test is green because `TravelTask` now finishes the plain walk
leg near the actual deck tier. The navmesh still notches around the small beam
treads: low-height filtering removes beam spans under the deck overhang, and
full-radius erosion removes more thin upper-beam spans outside the existing
z-banded erosion range.

## Proposed behavior
Add tile-scoped, JSON-gated MmapGen controls for tile `4029` that preserve only
the beam-zone spans through low-height filtering and erosion. With the keys
absent, the bake remains byte-identical on every other tile/map.

## Files likely to change
Frozen pathfinding surface:

- `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`
- `tools/MmapGen/config.json`

Plan/audit surface:

- `.agent/plans/decklip-beam-bake.md`

## Tests to add/update
No new test case is expected. Existing gates cover the behavior:

- `tools/scripts/navmesh_view.py` renders/connectivity-checks the rebaked tile.
- `PathPhysicsProbe.exe` resolves and classifies Grunt-to-Frezza.
- `tools/scripts/probe-routes.ps1` runs the OG zeppelin route manifest.
- `LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza` live-verifies the
  actual foreground traversal and screenshot artifact.

## Compatibility concerns
No public API, protobuf, serialized runtime contract, or BotRunner behavior
changes are planned. The MmapGen keys must be optional and no-op when absent.
The final `.mmtile` bytes should differ only for map `1` tile `40,29`.

## Migration concerns
No schema or persisted-state migration. Regenerated tile output lives in
`D:/wwow-bot/test-data/mmaps/0012940.mmtile` until explicitly promoted.

## Validation commands
```powershell
.\tools\MmapGen\build-mmapgen.ps1

Push-Location 'D:\wwow-bot\test-data'
& 'E:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe' 1 `
  --tile 40,29 --threads 10 --silent --debug `
  --configInputPath 'E:\repos\Westworld of Warcraft\tools\MmapGen\config.json' `
  --offMeshInput 'E:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt'
Pop-Location

py tools\scripts\navmesh_view.py D:\wwow-bot\test-data\meshes\map0012940navmesh.obj `
  --out tmp\test-runtime\navmesh-view\decklip-beam-bake `
  --crop 1320,-4662,1352,-4640 `
  --query 1337.6,-4650.8,50.5 `
  --query 1331.1,-4649.45,53.63 `
  --query 1344,-4646,53.2

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
.\Bot\Release\net8.0\PathPhysicsProbe.exe --map 1 `
  --start 1332.76,-4633.40,24.08 --end 1331.11,-4649.45,53.6269 `
  --detour-resolve --smooth

.\tools\scripts\probe-routes.ps1 -Manifest tools\scripts\routes\og-zeppelin.json `
  -DetourResolve -SmoothPath

dotnet test Tests\BotRunner.Tests\BotRunner.Tests.csproj -c Release --no-build `
  --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza" `
  -- RunConfiguration.TestSessionTimeout=1200000
```

## Rollback plan
Revert the TileWorker/config commit and restore the previous
`D:/wwow-bot/test-data/mmaps/0012940.mmtile` snapshot created by the bake
script or from git-tracked regeneration artifacts if promoted. The consumer
arrival fix remains untouched.

## Open questions
- Exact beam-preservation AABB and z-range must be pinned from the current debug
  bake and static-collision enumeration.
- The live test may still choose the established spiral route rather than the
  newly preserved treads; success requires the test to stay green and the mesh
  evidence to show the beams are now covered.
