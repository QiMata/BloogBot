# Handoff — close out the remaining pathfinding live-test failures

You are picking up a fresh Claude session for `e:/repos/Westworld of Warcraft/`
(WWoW, branch `main`). The prior session shipped the Phase 1 test-scaffold,
rebaked every map, and closed the BRM bake-validation fixture. **Two
live LongPathingTests remain failing**, both traceable to the same
already-tracked loop-25 Phase C1 surface (M2-doodad wall in tile 3928 /
OG flight-master-to-zeppelin walk corridor). Your job is to close them.

## Read first (in this order)

1. This file.
2. [`docs/Plan/Pathfinding/COMPREHENSIVE_TEST_PLAN.md`](COMPREHENSIVE_TEST_PLAN.md) — Phase 1 plan + status.
3. [`docs/Plan/Pathfinding/BAKE_RECIPE.md`](BAKE_RECIPE.md) — bake driver reference.
4. [`E:/repos/CLAUDE.md`](../../../../CLAUDE.md) — monorepo rules (R13 ordering, R15 commit cadence, R16 screenshot reads).
5. [`E:/repos/Westworld of Warcraft/CLAUDE.md`](../../../CLAUDE.md) — WWoW-specific rules.
6. The config.json README annotations for the BRM/OG tile work — these are the prior-attempt history. Search [`tools/MmapGen/config.json`](../../../tools/MmapGen/config.json) for `_3928_`, `_3446_`, `_3546_`, `_README_` keys; they document every Surface that's been tried with verdicts.
7. The following memory entries (`C:/Users/lrhod/.claude/projects/e--repos/memory/`):
   - `project_pfs_loop24_close_out_win.md` — the canonical pattern for closing this class of issue.
   - `project_pfs_loop25_doodad_investigation` (search prefix `project_pfs_loop25` or `loop25_doodad`) — the in-progress C1 work that surfaced the M2-doodad wall.
   - `project_pathfinding_tile_coords.md` — tile coord conventions (CRITICAL — file naming is `<map><tileY><tileX>` and CLI `--tile` is X,Y).

Then run `git log --oneline -20` to confirm state.

## State at start

**Commits on `origin/main` from the prior session (newest first):**

| Commit | What it shipped |
|---|---|
| `0c93e3fe` | fix(bake-validation): redesign flamecrest-to-brm fixture (settle-Z is server-vmap-controlled; phantom-poly tests belong in BrmDungeonRouteDiagnostic, not BAKE-VAL) |
| `b9d3cd99` | tools(scripts): bake-all-maps.ps1 full-map bake driver |
| `818bb8e8` | test(pathing): Phase 1 scaffold — Status gating + multi-segment chain runner + 51 Experimental rows |

**Production state:**
- All 23 maps (0, 1, + 21 vanilla dungeon maps) freshly baked via `tools/scripts/bake-all-maps.ps1` (2.4 min wall time at `--threads 8`).
- `D:/wwow-bot/test-data/mmaps/` + `D:/wwow-bot/prod-data/mmaps/` synchronized: 41 .mmap headers + 1716 .mmtile files.
- Docker stack healthy: `wwow-pathfinding` + `wwow-scene-data` serving `PRELOAD_COMPLETE maps=41`.
- BotRunner.Tests pre-built at `Bot/Debug/net8.0/` (skip `--no-build`'s implicit rebuild unless you change runtime code).

**Bake-fixture tally (post-rebake, post-redesign):**

| Test | Verdict | Notes |
|---|---|---|
| `OgZeppelin_BakeFixtureValidation` | 🟢 PASS | 11/11 walkable + 1/1 hole, loop-24 fix bit-for-bit preserved |
| `BrmDungeon_BakeFixtureValidation` | 🟢 PASS | Redesigned to 2 walkable + 0 holes; UBRS portal coord was dropped because both portals trigger area-transition |
| **Combined sweep** | 🟢 2/2 PASS | No cross-contamination |

## The two failures you must close

### Failure 1 — `CrossroadsToUndercity_UsesFlightAndZeppelin`

```
Long-travel wall-collision creep before Orgrimmar flight master ->
zeppelin tower; FG physics rejects forward movement
(intent-flag set, currentSpeed=0.00 yd/s).
map=1 anchor=(1626.6,-4153.6) current=(1627.6,-4151.8,36.9)
flags=0x1 creep-window=15s.
```

**Pre-confirmed root cause (prior session probe):**

```powershell
# Repro:
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
    --map 1 --start 1627.6,-4151.8,36.9 --end 1640,-4427,22 `
    --detour-resolve --smooth --load-adt

# First non-Walk segment at idx=0:
#   1627.60,-4151.80,36.90 -> 1627.40,-4152.26,38.02
#   affordance=SteepClimb validation=Clear climb=1.12 slope=64.90°
```

The smooth path leaves the bot's stuck coord via a **64.9° SteepClimb**.
Detour considers it walkable (the bake's `walkableClimb` = 1.8 accepts the
1.12y vertical step), but the bot's runtime physics
(`IsSegmentLocallyReachableForAgent`) correctly rejects 65° slopes. Classic
bake-vs-physics gap.

**Tile:** `0013928.mmtile` (CLI `--tile 28,39`). Per the config.json
`_3928_README_doodad_gap` annotation: this tile contains two standalone
M2 doodad instances `224791+224792` forming a ~4.6y vertical-wall pair
at approx (1615.3, -4240.85), z=46.66..51.30. **Loop-25 Phase C1
investigated this on 2026-05-19 but shipped no bake change** — see the
config.json key for full context.

### Failure 2 — `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes`

```
The Orgrimmar -> Undercity zeppelin was detected at the dock, but the
bot missed boarding before the transport left.
failure: map=1 pos=(1320.1,-4653.2,53.9) distToUndercity=4902.3
transport=0x0 offset=(0.0,0.0,0.0) current=null
```

The bot DID reach the exact post-loop-24 off-mesh boarding coord
(1320.1, -4653.2, 53.9 — that's the `tools/MmapGen/offmesh.txt:38`
entry's endpoint). It just got there too late. **Almost certainly the
same tile-3928 doodad-wall stall**: the walk from the OG flight master
to the boarding coord crosses tile 3928, the bot creeps for 15s+ at the
SteepClimb, the zeppelin departs without it.

If you close Failure 1, Failure 2 likely closes for free.

## The diagnostic-and-fix loop (proven this session)

```powershell
# 1. Probe the stall column (find the offending poly).
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
echo "1627.6,-4151.8,36.9" > tmp/probe-coord.txt
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
    --map 1 --path tmp/probe-coord.txt --dump-poly-stack --load-adt
# Look for posOverPoly=1 — that's the polygon the bot is standing on.
# Note its polyref (decimal AND hex).

# 2. Probe with route resolution to find the FIRST non-Walk segment.
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
    --map 1 --start 1627.6,-4151.8,36.9 --end 1640,-4427,22 `
    --detour-resolve --smooth --load-adt | Select-Object -First 50

# 3. Decide the fix:
#    (a) Cull a polygon (if it's a phantom-poly that shouldn't be walkable)
#    (b) Add an off-mesh entry (if it's a real cliff/wall that needs a teleport bypass)
#    (c) Bake-param adjustment (if it's a systemic ceiling/erosion issue)

# (a) Cull example:
.\tools\MmapGen\build\NavMeshTileEditor.exe `
    D:\wwow-bot\test-data\mmaps\0013928.mmtile `
    --cull-polys <ref1>,<ref2> --dry-run
# Then again without --dry-run when satisfied.

# (b) Off-mesh example (edit tools/MmapGen/offmesh.txt):
#   1 28,39 (X1 Y1 Z1) (X2 Y2 Z2) 2.5 // WWoW: doodad-wall bypass
# Then rebake the affected tile:
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "28,39" -Variant doodad-bypass

# 4. Promote + restart Docker.
.\tools\MmapGen\promote-mmaps.ps1 -Map 1
docker restart wwow-pathfinding wwow-scene-data

# 5. Re-run the test (LiveBotFixture auto-launches StateManager + WoW.exe).
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Debug `
    --filter 'FullyQualifiedName~CrossroadsToUndercity_UsesFlightAndZeppelin' `
    --no-restore --logger 'console;verbosity=detailed'
```

**Always back up the .mmtile before culling**:

```powershell
Copy-Item 'D:\wwow-bot\test-data\mmaps\0013928.mmtile' `
          'D:\wwow-bot\test-data\mmaps\0013928.mmtile.precull-<ts>.bak'
```

**Always re-run the OG + BRM bake-fixtures after any tile change** — they
are the canonical "did I regress anything" gates:

```powershell
$env:WWOW_OG_ZEP_BAKE_FIXTURE='1'
$env:WWOW_BRM_BAKE_FIXTURE='1'
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Debug `
    --filter 'FullyQualifiedName~BakeFixtureValidation' `
    --no-restore --logger 'console;verbosity=detailed'
# Expect: Test Run Successful, Total tests: 2, Passed: 2
```

## Hard constraints — DO NOT violate

1. **`LiveBotFixture` holds a machine-wide mutex.** Only ONE test run at a
   time. Don't try to parallelize bake-fixture + LongPathing runs; the
   second will block waiting for the mutex.
2. **Tile filename convention is `<map:03d><tileY:02d><tileX:02d>.mmtile`.**
   The CLI `--tile X,Y` uses MmapGen-internal X/Y which is the swap of WoW
   X/Y. World (1627.6, -4151.8) maps to MmapGen `--tile 28,39`, file
   `0013928.mmtile`. The memory entry `project_pathfinding_tile_coords`
   has the full derivation; trust it over `docs/physics/MMAP_FORMAT.md` §3.
3. **Settle Z is server-vmap-controlled.** The previous session's BRM
   redesign commit explains this in detail. Do not author tests that
   assert settle-Z to validate bake-side culls — settle won't change.
   Routing concerns belong in `Tests/PathfindingService.Tests/`.
4. **Cull blast radius can break adjacent connectivity.** Previous
   session: culling polyIdx 637+3000 in tile 0004634 regressed LBRS
   portal connectivity to MISSING_SAMPLE. Always re-run the BRM + OG
   fixtures after any cull and accept the cull only if both pass.
5. **Off-mesh additions require a tile rebake**, not just a file
   reload. The PathfindingService loads only `.mmtile` data, not raw
   offmesh.txt.
6. **Per R15: commit + push every iteration.** Even a negative result
   ("Surface I tried polyIdx-range cull on tile 3928, regressed OG zep
   fixture") gets a commit so the next loop has the trail.
7. **WoW.exe is fully automated by the fixture.** Do NOT try to launch
   it manually or grab the cursor. `LiveBotFixture` handles inject +
   teardown via DLL injection of `Loader.dll`.

## Definition of done

| Gate | Criterion |
|---|---|
| OG zep bake-fixture | passes, 11/11 walkable + 1/1 hole |
| BRM bake-fixture | passes, 2/2 walkable + 0 holes |
| `CrossroadsToUndercity_UsesFlightAndZeppelin` | passes — no wall-collision creep |
| `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` | passes — bot boards before transport leaves |
| Docker prod | `wwow-pathfinding` + `wwow-scene-data` healthy with latest .mmtile bytes |
| Memory + plan docs updated | new memory entry summarizing the iteration; updates to `COMPREHENSIVE_TEST_PLAN.md` Phase 2/3 status |

## Approaches NOT to try (already disproven per config.json)

- **Bake-param changes to tile 3446** (Surface F walkableErosionRadius=0.0 + Surface G) regressed corridor termination; both REVERTED. See `_3446_NEGATIVE_RESULT_surface_G`.
- **Bake-param changes to tile 3928 alone** as a quick fix. The doodad-wall is M2 source-geometry; changing Recast knobs for one tile doesn't move the underlying walkable mass.
- **Single-tile aggressive cull on 3446** with `agentMaxClimbTerrain=0.2` — destroyed the route entirely. See `_3446_NEGATIVE_RESULT`.

## Approaches that WORK or are promising

- **Off-mesh-entry bypass** (loop-24 pattern, `tools/MmapGen/offmesh.txt:38`). This closed all 4 OG zep tile (40,29) failures in loop 24. For tile 3928, the natural off-mesh would be a short teleport across the doodad-wall gap (~5y). Endpoints would need to be measured via `PathPhysicsProbe --dump-poly-stack` on both sides of the wall.
- **Targeted single-polygon cull** when the offending poly is a "phantom" (5cm walkable, doesn't connect to anything real). Use `--dump-poly-stack` + `--detour-resolve --smooth` first to confirm the poly is on the path and the cull won't isolate a real corridor.
- **Recovery branch in BotRunner** (out-of-scope for this loop but tracked) — teach the bot to back off + try an alternate route when wall-collision creep is detected for >5s. This is the long-term fix; off-mesh is the short-term fix.

## Stop conditions

1. **`CrossroadsToUndercity_UsesFlightAndZeppelin` + `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` both pass** + bake-fixture pair still passes → COMMIT, push, write a memory entry, STOP. Surface to user.
2. **3 consecutive iterations with no progress** + each well-documented → STOP, surface to user with the iteration evidence and recommendation. Per loop-25 "geometric dead-end" precedent, this surface has hit walls before; don't grind indefinitely.
3. **A cull regresses the OG zep OR BRM bake-fixture** → IMMEDIATELY revert and try a different surface. Do not push a commit that breaks the green build.

## First action

Probe tile 3928 with full diagnostic enumeration. Run BOTH commands and read both outputs before deciding on a surface:

```powershell
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'

# Probe A — stall column (where the bot is stuck):
echo "1627.6,-4151.8,36.9" > tmp/stall-coord.txt
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
    --map 1 --path tmp/stall-coord.txt --dump-poly-stack --load-adt

# Probe B — doodad-wall column (from config.json _3928_README_doodad_gap):
echo "1615.3,-4240.85,46.7" > tmp/doodad-coord.txt
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
    --map 1 --path tmp/doodad-coord.txt --dump-poly-stack --load-adt --verbose
```

The `--verbose` flag dumps the endpoint surface enumeration including the
GroundZ breakdown across vmap/ADT/BIH sources. That's how you identify
which M2 doodad polygons (if any) Recast picked up vs. which surfaces the
runtime physics consults.

After reading the probe output, pick a surface — off-mesh vs cull vs
something new — write your hypothesis in a one-line `[SURFACE-N]` log,
implement, run the bake-fixtures + the LongPathingTest, commit. Repeat.

Good luck. The user's directive in the prior session was clear:
"work on baking all tiles such that they all work for all tests."
