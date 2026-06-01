# Handoff — continue the LongPathing closeout from iter 1

You are picking up a fresh Claude session for `e:/repos/Westworld of Warcraft/`
(WWoW, branch `main`). The previous session ran iter 1 of the
[NEXT_SESSION_LONGPATHING_HANDOFF](NEXT_SESSION_LONGPATHING_HANDOFF.md)
plan, landed commit `c19c51b8`, and surfaced. **Both target tests
still fail**, but the failure modes have shifted in important ways
and the handoff's two-failures-share-one-cause hypothesis was
empirically falsified. Your job is to decide whether to keep
iterating off-meshes, switch surfaces, or stop and escalate.

## Read first (in this order)

1. This file.
2. [NEXT_SESSION_LONGPATHING_HANDOFF.md](NEXT_SESSION_LONGPATHING_HANDOFF.md) — the iter-1 prompt; still relevant for fixture state, hard constraints, and the diagnostic loop. **Caveat:** the tile-coord convention it cites for the stall coord is WRONG — see the "Tile-coord convention correction" section below before running any bake or editing offmesh.txt.
3. [`docs/Plan/Pathfinding/COMPREHENSIVE_TEST_PLAN.md`](COMPREHENSIVE_TEST_PLAN.md) and [BAKE_RECIPE.md](BAKE_RECIPE.md).
4. [`E:/repos/CLAUDE.md`](../../../../CLAUDE.md) and [`E:/repos/Westworld of Warcraft/CLAUDE.md`](../../../CLAUDE.md) — monorepo rules (R13/R15/R16) and WWoW-specific rules.
5. Memory entries under `C:/Users/lrhod/.claude/projects/e--repos/memory/`:
   - `project_pfs_loop26_iter1_og_east_offmesh.md` — the canonical iter-1 record (off-mesh added, partial progress, NEW stall coord, OrgrimmarToUndercity diagnosis, tile-coord correction, recommendations).
   - `pfs-loop24-close-out-win.md` — the off-mesh pattern this loop reuses.
   - `project_pfs_loop25_phase_c1_hypothesis_falsified.md` — confirms the doodad wall at (1615.3,-4240.85) is NOT the actual stall (segments 62-64 of the smooth path walk through it cleanly).
   - `project_pathfinding_tile_coords.md` — **READ WITH SUSPICION.** The concrete file/tile numbers in it are correct but the explanatory text inverts which WoW axis derives which MmapGen tile axis. Trust the source over the explanation; see "Tile-coord convention correction" below.

Run `git log --oneline -10` to confirm `c19c51b8 pathing(offmesh): add OG east-wall steep-climb bypass on tile 39,28` is on `origin/main`.

## State at start

### What iter 1 shipped (commit `c19c51b8`)

A single 19-line addition to [tools/MmapGen/offmesh.txt](../../../tools/MmapGen/offmesh.txt):

```
1 39,28 (1627.600 -4151.800 37.273) (1622.160 -4163.970 45.635) 2.5
```

This is the loop-24 pattern reapplied to the OG eastern-approach
steep climb. Bake of `--tile 39,28` produced
`D:/wwow-bot/test-data/mmaps/0012839.mmtile` (1,750,908 bytes,
-11,620 vs baseline because `DT-POLY-CULL disabled 3990 final
suspicious Detour polygon(s)` with the off-mesh registered). Promoted
to prod-data; `wwow-pathfinding` + `wwow-scene-data` restarted clean;
runtime `[OFFLINK]` confirms both endpoints LINKED with dxz²=0.00 on
Detour tile (11,27).

### Test results after iter 1

| Test | Result | Detail |
|---|---|---|
| `OgZeppelin_BakeFixtureValidation` | 🟢 PASS | 2m11s combined with BRM |
| `BrmDungeon_BakeFixtureValidation` | 🟢 PASS | same run |
| `CrossroadsToUndercity_UsesFlightAndZeppelin` | 🔴 FAIL 4m26s | New stall at **(1608.1,-4382.3,10.0)** inside an OG building (screenshot evidence: bot among crates near NPC "Forrest Ambassador Naitarius"). Two `TRAVEL_WALK_STALL` replans visible in chat trace before the 15s creep timeout: count=1 at (1659.3,-4349.7,29.0) plan=1 idx=49; count=2 at (1625.3,-4366.2,28.1) plan=2 idx=8. Bot traversed **230y past the original (1627.6,-4151.8,36.9) stall** before hitting this one. |
| `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` | 🔴 FAIL 1m35s | `pos=(1320.1,-4653.2,53.9) transport=0x0`. Bot is at the boarding coord; the zeppelin transport never showed up within the evidence window. **Not a pathfinding stall** — it's a transport-detection/timing issue. Iter 1's off-mesh on tile (39,28) is geographically unrelated to this test (it stages directly at the boarding coord on tile (40,29)). |

### Why iter 1 helped at all (the off-mesh wasn't picked but the bake change still moved the bot)

In static `PathPhysicsProbe` with the same start/end, Detour still
prefers the natural corner path (~11.55y) over the new off-mesh
(~15.7y). The off-mesh was registered correctly (poly idx 9665,
polyType=1, area=63, flags=0x00FF) but A* found a cheaper natural
route.

However, the rebake also changed cull heuristics (the `-11620 bytes`
delta + 3990 polys culled). After the rebake, the raw/corridor path's
segment 0 is **StepUp 26.8° Clear** (`(1627.60,-4151.80,36.90) →
(1621.33,-4160.00,42.11)`, hDist=10.32 vDelta=5.21), whereas pre-bake
the raw segment 0 was `Blocked BlockedGeometry` over the full
32.35y/10.71y corner-to-corner hop. Since the smooth path still has
`SteepClimb` at segment 0 (62.99° at the densifier's first 0.5y
step), `NavigationPath.IsRouteSupported` returns false for smooth →
`ShouldPreferAlternatePath` returns true → bot uses corridor mode →
segment 0 is traversable → bot progresses. The off-mesh's value is
*indirect* (it altered the polygon graph enough that the corridor
fallback works) rather than direct (Detour picking the teleport).

## Tile-coord convention correction — read before any bake

The original handoff and `project_pathfinding_tile_coords` memory
both claim "World (1627.6,-4151.8) maps to MmapGen `--tile 28,39`,
file `0013928.mmtile`." **That is wrong.** Verified empirically by
this session:

- Running `bake-tile.ps1 -Map 1 -Tiles '28,39'` rebuilt
  `0013928.mmtile` but the dump-poly-stack at (1627.6,-4151.8,36.9)
  was bit-identical to the pre-bake state — the off-mesh wasn't in
  the bot's actual tile.
- Re-running with `-Tiles '39,28'` rebuilt `0012839.mmtile`, the
  bot's poly idx changed (646→657) AND a new off-mesh poly appeared
  (idx 9665, polyType=1) AND `[OFFLINK] base tile=(11,27)` printed at
  runtime confirming registration on the right Detour tile.

### Canonical rules

From `tools/MmapGen/contrib/mmap/src/`:

- **`TerrainBuilder::copyVertices`** swaps source `(x,y,z)` →
  Recast `(y,z,x)`. So **Recast X = WoW Y** and **Recast Z = WoW X**
  (confirmed by `TerrainBuilder.cpp:1063` comment).
- **`MapBuilder::getTileBounds`** writes `bmax[0] = (32 - tileX) *
  GRID_SIZE` to Recast X and `bmax[2] = (32 - tileY) * GRID_SIZE` to
  Recast Z.
- **`TileWorker.cpp:10319/11964`** writes the mmtile file as
  `sprintf "mmaps/%03u%02i%02i.mmtile", mapID, tileY, tileX`.
- **`generator.cpp:131-139`** CLI parser reads `--tile X,Y` as
  `tileX=X, tileY=Y`.
- **`TerrainBuilder.cpp:1051-1055`** offmesh.txt parser reads
  `%d %d,%d (...) (...) %f` as `mapID, tileX, tileY (start) (end) size`
  and matches `tileX == tx && tileY == ty` against the function args.

### Therefore

- **MmapGen `tileX` derives from WoW Y axis:**
  `tileX = 32 - ceil(WoW_Y / 533.33)`
- **MmapGen `tileY` derives from WoW X axis:**
  `tileY = 32 - ceil(WoW_X / 533.33)`
- **Filename:** `mmaps/<mapID:3><tileY:2><tileX:2>.mmtile`.
- **offmesh.txt grammar:** `mapID tileX,tileY (start_x start_y start_z) (end_x end_y end_z) size`.
- **CLI:** `--tile tileX,tileY`.

### Worked examples

- World (1627.6, -4151.8): tileX = 32 - ceil(-7.785) = 39, tileY = 32 - ceil(3.052) = 28.
  → MmapGen tile (39, 28) → file `0012839.mmtile`. offmesh.txt entry begins `1 39,28`.
- World (1320.14, -4653.16) (OG zeppelin boarding): tileX = 32 - ceil(-8.724) = 40, tileY = 32 - ceil(2.475) = 29.
  → MmapGen tile (40, 29) → file `0012940.mmtile`. offmesh.txt entry begins `1 40,29`. (The 8.7MB `0012940.mmtile` in prod-data is the canonical post-loop-24 OG-zep file with the deck-edge→BoardingPosition off-mesh. The `0014029.mmtile` file in prod-data is a separate, unrelated tile.)
- World (1608.1, -4382.3, 10.0) (the new stall): tileX = 32 - ceil(-8.217) = 40, tileY = 32 - ceil(3.016) = 28.
  → MmapGen tile (40, 28) → file `0012840.mmtile`. offmesh.txt entry would begin `1 40,28`.

When in doubt, after baking grep the `[bake-tile]` log for
`Building single tile [X,Y]` and `Generated file: mmaps/NNNNNN.mmtile`
to confirm both numbers match your intent.

## The two open failures, ranked by problem class

### Failure A — `CrossroadsToUndercity` new stall at (1608.1,-4382.3,10.0)

**Geometry:** The bot's poly is idx 493 (area=1, surfaceZ=10.476,
posOverPoly=1) in tile (40, 28) = file `0012840.mmtile`. Other polys
at this XY: idx 491/492 at z=9.4-9.8; corner 1 of a nearby probe at
(1608.1,-4382.3,**30.0**) shows only idx 495 at z=23.6-25.1 — there
is NO walkable surface at z=28-30 directly above the stall coord, so
the bot is in a sub-floor pocket of OG city interior. Screenshot
confirms an indoor scene with crates and an NPC.

**Smooth path from this coord to OG zeppelin boarding:** all 25
corners are `Walk Clear`, slopes 1-34°, no SteepClimb, no Blocked.
So the bake says the route is fine. The runtime physics sees a wall
the navmesh does not. Classic bake-vs-physics gap, but in a
**different problem class than iter 1's eastern-approach climb** —
this one is a *missing wall mesh* in the bake, not a *too-steep
slope* the densifier amplifies.

**Surface candidates for iter 2:**

A. **Off-mesh through the wall.** Add `1 40,28 (1608.100 -4382.300 10.500) (1608.100 -4420.000 11.500) 2.5` (or pick a different end coord after probing for nearby polys with cleaner connectivity). Lands ~38y south at same z. Caveat: Detour may not pick this either if the natural smooth path is shorter (it is — direct walk is 37.7y). Will it help anyway via the same corridor-fallback mechanism iter 1 exploited? Unclear without a live test.

B. **Cull poly 493 (the bot's stall poly).** Forces Detour to find a different route. Risky — could isolate the entire z=10 region from the rest of the navmesh and break unrelated bots that legitimately walk through.

C. **Skip this surface entirely and accept multi-stall close.** Two `TRAVEL_WALK_STALL` replans recovered earlier in the route; this third one finally tripped the 15s creep timeout. Maybe extending the creep timeout or adding a third replan budget would let the test complete naturally. That's a `BotRunner.NavigationPath` C# change, not a bake change, and the handoff calls it the "long-term fix" (out of scope here).

D. **Probe the screenshot.** The bot is among crates near NPC
"Forrest Ambassador Naitarius" — that NPC name doesn't match any
vanilla OG NPC, suggesting custom MaNGOS content or a typo. If this
is custom server geometry (a building added to the MaNGOS world but
not the WoW client data), the bake CAN see it but the physics engine
can't — or vice versa. Worth checking via `.gameobject near` /
`.npc near` SOAP commands at this coord before adding any off-mesh.

### Failure B — `OrgrimmarToUndercity` boarding miss at (1320.1,-4653.2,53.9)

The bot is staged directly at the boarding coord. It dispatches
`TravelTo Undercity`. The test waits for `[TRAVEL_LEG] start
type=Zeppelin` within 45s and then for `CurrentMapId ==
UndercityMapId || IsOnOrgrimmarUndercityZeppelinDeck` within
`GetZeppelinTransferEvidenceTimeout()`. The failure is
`transport=0x0 offset=(0.0,0.0,0.0)` — the bot is at the dock, but
no transport is currently within the bot's transport-detection
window. Test runtime 1m35s — short, suggesting the predicate fails
immediately rather than waiting the full timeout.

**This is not a pathfinding problem.** Off-mesh additions, tile
bakes, and Detour culls cannot fix it. Candidates:

- **Zeppelin spawn schedule misalignment.** The OG↔UC zeppelin in
  vanilla MaNGOS has a ~4 min cycle. If the test stages, dispatches,
  and starts waiting in the wrong phase of the cycle, the bot waits
  while no transport is at the dock. Inspect
  `LiveBotFixture.StageBotRunnerAtNavigationPointAsync` and the test
  for any `WaitForZeppelinAtDockAsync` pre-condition that's missing
  or broken.
- **FG transport-detection hook.** The bot's `IsOnOrgrimmarUndercityZeppelinDeck`
  predicate reads `snapshot.PlayerTransportGuid` and/or
  `MovementData.MovementFlags & ON_TRANSPORT`. If the FG hook isn't
  populating these correctly after my Navigation rebake (unlikely
  but possible), the predicate would return false even when the bot
  IS on the deck.
- **MaNGOS transport spawn config.** Check the `transport_template` /
  `transports` MySQL tables for the OG↔UC zeppelin and confirm it's
  spawning on schedule. SOAP `.gobject near 100` at the boarding
  coord during a known "transport-arrived" window should list a
  GameObject. If not, the server-side spawn is broken.

Decoupling iter 2 of pathfinding from this failure is *probably*
correct — they're independent.

## Hard constraints — DO NOT violate

These are the same as the original handoff but worth restating:

1. **`LiveBotFixture` holds a machine-wide mutex.** Only one test run
   at a time.
2. **Tile-coord convention:** use the rules above, not the original
   handoff's claims. Always verify after bake by re-probing the
   stall coord and checking the polyIdx changed.
3. **Settle Z is server-vmap-controlled** — bake changes don't move
   settle, so test on the resulting *physics behavior*, not on a
   settle-Z assertion.
4. **Cull blast radius:** always re-run `OgZeppelin_BakeFixtureValidation`
   AND `BrmDungeon_BakeFixtureValidation` after any tile change.
   `WWOW_OG_ZEP_BAKE_FIXTURE=1 WWOW_BRM_BAKE_FIXTURE=1
   dotnet test ... --filter 'FullyQualifiedName~BakeFixtureValidation'`.
5. **Off-mesh adds require a tile rebake.**
6. **Per R15:** commit + push every iteration. Even negative
   results get a commit.
7. **Per R12 / R16:** before claiming a Task/Action live-test failure
   from logs, READ the captured screenshot at `tmp/test-runtime/screenshots/long-pathing/`
   (the Read tool ingests PNGs).
8. **Per R18:** when replacing off-mesh entries, delete the prior
   ones — do not leave `// removed in iter N` comments or parallel
   entries.
9. **WoW.exe is auto-managed by the fixture.** Don't launch it
   manually.

## The diagnostic-and-fix loop (unchanged from iter 1)

```powershell
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'

# 1. Probe the stall column.
echo "1608.1,-4382.3,10.0" > tmp/probe-coord.txt
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
    --map 1 --path tmp/probe-coord.txt --dump-poly-stack --load-adt

# 2. Probe with route resolution to the OG zeppelin boarding coord.
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
    --map 1 --start 1608.1,-4382.3,10.0 --end 1320.14,-4653.16,53.89 `
    --detour-resolve --smooth --load-adt | Select-Object -First 50

# 3. Author off-mesh in tools/MmapGen/offmesh.txt (under the WWoW divider).

# 4. Bake the tile. For (1608.1,-4382.3,10.0) that's MmapGen tile
#    (tileX=40, tileY=28) → file 0012840.mmtile.
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,28' `
    -Variant og-interior-z10-bypass

# 5. Verify the off-mesh registered on the right tile (bake.log
#    should print: loadOffMeshConnections:: Found offmesh connection
#    for map 1 tile [40,28]: ...)

# 6. Promote + restart.
.\tools\MmapGen\promote-mmaps.ps1 -Map 1 -Tiles '40,28'
docker restart wwow-pathfinding wwow-scene-data

# 7. Re-run the bake-fixture pair (mandatory regression check).
$env:WWOW_OG_ZEP_BAKE_FIXTURE='1'
$env:WWOW_BRM_BAKE_FIXTURE='1'
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Debug `
    --filter 'FullyQualifiedName~BakeFixtureValidation' `
    --no-restore --logger 'console;verbosity=detailed'
# Expect: Test Run Successful, Total tests: 2, Passed: 2.
# If either regresses, REVERT THE TILE before proceeding.

# 8. Re-run the failing live test.
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Debug `
    --filter 'FullyQualifiedName~CrossroadsToUndercity_UsesFlightAndZeppelin' `
    --no-restore --logger 'console;verbosity=detailed'

# 9. Read the new failure screenshot (R16) before claiming a root cause.
```

**Always back up the .mmtile before culling:**

```powershell
Copy-Item 'D:\wwow-bot\test-data\mmaps\0012840.mmtile' `
          'D:\wwow-bot\test-data\mmaps\0012840.mmtile.precull-<ts>.bak'
```

## Definition of done

| Gate | Criterion |
|---|---|
| OG zep bake-fixture | passes, 11/11 walkable + 1/1 hole |
| BRM bake-fixture | passes, 2/2 walkable + 0 holes |
| `CrossroadsToUndercity_UsesFlightAndZeppelin` | passes — no 15s collision creep |
| `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` | passes — bot boards and disembarks at UC. **This will probably NOT close from pathfinding changes alone — see "Failure B" above.** Treat it as a separate problem class to investigate or escalate. |
| Docker prod | `wwow-pathfinding` + `wwow-scene-data` healthy with latest .mmtile bytes |
| Memory + plan docs | new memory entry summarizing the iteration; updates to `COMPREHENSIVE_TEST_PLAN.md` if applicable |

## Approaches NOT to try (re-affirmed from iter 1 + prior loops)

- **Bake-param changes to tile 3928 / tile 39,28 to address the
  doodad-wall** (loop-25 C1 line of work). The wall is geometrically
  traversable in the current bake (segments 62-64 of the smooth path
  walk through (1614,-4243,47) with `Walk Clear` validation). Don't
  re-investigate it.
- **Aggressive single-tile culls.** Loop-24 / loop-25 negative
  results documented in `tools/MmapGen/config.json` `_3446_NEGATIVE_RESULT*`
  and `_4029_NEGATIVE_RESULT*` blocks. Walls of caution there.
- **Single bake-param change to the eastern-approach steep climb on
  tile (39, 28).** The smooth path's segment-0 SteepClimb is a
  detail-mesh artifact within poly 657 — changing `walkableSlopeAngle`
  or `walkableErosionRadius` for that one tile won't move the
  underlying steep detail-mesh sample. (Loop-25 found this empirically
  for the doodad-wall column; same conclusion applies here.)

## Approaches that worked or are promising (iter 1 evidence)

- **Off-mesh entries that change the bake's polygon graph** even
  when Detour doesn't pick them as the primary edge. The corridor
  fallback in `NavigationPath.ShouldPreferAlternatePath` exploits
  the new polygon graph to find a `StepUp Clear` segment 0 that
  pre-bake was `BlockedGeometry`. This is the indirect-value
  mechanism iter 1 demonstrated.
- **Per-tile single-line off-mesh additions** (loop-24 pattern at
  (40,29); iter 1 pattern at (39,28)). Low blast radius if the
  endpoints land on real walkable polys; verify via re-probe.
- **Recovery branch in BotRunner** (`NavigationPath`-level "after N
  seconds of creep, abandon the smooth segment and reroute via a
  parallel candidate") — out of scope for an off-mesh-only iter but
  it's the long-term fix and would close multi-stall corridors like
  this one in one shot. If iter 2 of off-mesh whack-a-mole doesn't
  close the test, escalate to this.

## Stop conditions

1. **Both `CrossroadsToUndercity_UsesFlightAndZeppelin` and
   `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` pass** + bake-fixture
   pair still passes → COMMIT, push, write a memory entry, STOP.
2. **3 consecutive iterations with no progress** + each
   well-documented → STOP, surface to user with the iteration
   evidence and recommendation. Iter 1 made 230y of progress on
   CrossroadsToUndercity, so the first iter you run here would be
   the 2nd in this sequence — track that.
3. **A cull regresses the OG zep OR BRM bake-fixture** → IMMEDIATELY
   revert and try a different surface.
4. **Failure B (`OrgrimmarToUndercity`) is confirmed to be a non-pathfinding
   problem** (e.g., zeppelin spawn schedule, FG transport-detection
   hook, MaNGOS server-side issue) → surface to user with the
   diagnostic, do NOT add off-mesh entries trying to fix it.

## First action

Choose one of three orderings:

**A. Continue iter 2 on `CrossroadsToUndercity`'s new stall.**
Run probes A + B at (1608.1,-4382.3,10.0) toward the boarding coord.
Decide whether to add `1 40,28 (1608.100 -4382.300 10.500) (1608.100
-4420.000 11.500) 2.5` (or a probe-validated variant) to offmesh.txt.
Bake, validate fixtures, run the live test. Expect a *third* stall
location to emerge (multi-stall corridor pattern) — accept that and
either continue whack-a-mole or stop after iter 3 / iter 4.

**B. Investigate `OrgrimmarToUndercity` as a separate problem class.**
Use `LiveBotFixture.SendGmChatCommandAsync` to inspect the zeppelin
GameObject near (1320,-4653,54) during a known transport-arrived
window. Or read `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs:2363`
(`FailIfZeppelinBoardingLost`) and chase the predicate. This unblocks
a DoD gate that off-mesh additions cannot.

**C. Escalate to the long-term fix.** If iter 2 reveals yet another
stall coord, sketch a `BotRunner.NavigationPath` patch that handles
wall-collision-creep with a backoff-and-reroute strategy. This is the
handoff's recommended long-term fix; it would close multi-stall
corridors generally.

Recommend **B first** — it's the unblocker for the second DoD gate
and is decoupled from off-mesh iteration. Then **A** if Failure A
is still in scope after B.

Good luck. Iter 1's tile-coord correction and "off-mesh has indirect
value via the corridor-fallback path" finding are the most important
artifacts of this loop — don't forget them when authoring iter 2.
