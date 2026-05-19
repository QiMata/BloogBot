# Phase 4 GO Variants — Implementation Handoff Prompt

> Paste this whole file into a fresh Claude Code session in `e:\repos`.
> The new session will pick up where the prior one left off (BRM south-face
> stall localized to GameObject `displayId=4652`, design doc landed) and
> implement the variant-aware GO bake + runtime per the design.
>
> **Owner concept** for this work: every server-spawnable GameObject the
> bot might collide with belongs to a named variant. The bake produces base
> + variant-delta tiles. The runtime composes the requested variant set at
> request time. The BotRunner's StateManager observes server world-state
> and threads the active variants into every path request. Nothing about
> GO collision lives in BotRunner repair logic.

---

## 0. Mission

Implement the Phase 4 design in `Westworld of Warcraft/docs/physics/PHASE4_GO_VARIANTS.md`
in five sub-phases, each independently shippable and live-test-validatable.
The validation North Star throughout is the
`BotRunner.Tests.LiveValidation.LongPathingTests.FlameCrestToBrmDungeonEntrance`
[Theory] (4 cases: BRD, LBRS, UBRS, BWL) gated on
`WWOW_BRM_DUNGEON_TRAVEL_TEST=1`. The protected regression is the OG
zeppelin live test `DeckLipClimbFromGruntToFrezza` gated on
`WWOW_DECKLIP_CLIMB_TEST=1`.

Phase A unblocks the BRM live theory by making the runtime physics see
the always-on GameObject collision the bake already produces polygons over.
Phases B–E layer in event variants.

**You are NOT done after Phase A.** The user wants comprehensive coverage
including event-conditional spawns (Onyxia head, Darkmoon Faire, Hallow's
End, etc.). Phase A is just the smallest live-test-validatable starting
slice; B–E are required deliverables.

---

## 1. Required reading (first iteration only, in order)

Read top to bottom — do not skip:

1. `e:\repos\CLAUDE.md` (root monorepo rules) and
   `e:\repos\Westworld of Warcraft\CLAUDE.md` (repo-local).
2. **`Westworld of Warcraft/docs/physics/PHASE4_GO_VARIANTS.md`** — the
   design doc this prompt operationalizes. Read every section, especially
   §3 (principles), §4–6 (pipeline), §8 (phases), §11 (decisions already
   made — see also §10 below), §12 (Phase A deliverable).
3. `Westworld of Warcraft/docs/physics/PATHFINDING_OVERHAUL.md` — Phase 4
   is part of this overhaul; freeze contract still applies.
4. `Westworld of Warcraft/docs/ANTI_PATTERNS.md` (note especially the
   "tolerances-defined-in-tests" and "env-var-driven behavior" rows).
5. `C:\Users\lrhod\.claude\projects\e--repos\memory\MEMORY.md` — full
   index. Specifically read these entries top-to-bottom:
   - `project_pfs_overhaul_006_brm_gameobject_4652.md` — the BRM root cause
     diagnosis that made this Phase 4 expansion necessary.
   - `project_pfs_overhaul_006_pathphysicsprobe_brm_route.md` — how to drive
     the probe and what the histogram tells you.
   - `project_pfs_overhaul_006_brm_singletile_negative.md` — the Cycle-17e
     precedent disqualification; explains why the fix has to be GO-aware,
     not just bake-parameter tuning.
   - `project_pfs_overhaul_006_decklip_solution.md` — the OG zeppelin
     Cycle-17e precedent (still in effect; protected regression).
   - `project_pfs_overhaul_006_tile_coord_inversion.md` — MmapGen CLI vs
     filename convention. You WILL need this.
   - `feedback_pathfinding_freeze.md` — what's frozen and what isn't.
   - `feedback_pathfinding_anti_patterns.md` — do NOT lower
     `walkableSlopeAngle`/`walkableClimb` from harvested values; do NOT
     add bot-side jump-up; etc.
   - `feedback_pathfinding_docker_reload.md` — Docker container reload
     ritual after every bake change that needs to ship to production.
   - `reference_wwow_data_dir.md` — `D:\MaNGOS\data` is canonical;
     `D:\wwow-bot\test-data` is the test rig with symlinked maps/vmaps.
6. `e:/repos/.claude/skills/mmo-physics-pathing-probe/SKILL.md` —
   PathPhysicsProbe usage. **You will use this every iteration.**
7. `e:/repos/.claude/skills/mmo-pathfinding/SKILL.md` — bake-side iteration
   loop, navmesh authoring rules.
8. `e:/repos/.claude/skills/mmo-fg-client-re/SKILL.md` — only relevant if
   you end up needing FG-side telemetry (you probably won't for Phase A–C).
9. `e:/repos/.claude/skills/mmo-statemanager-orchestration/SKILL.md` — for
   Phase D when you wire StateManager world-state observation.

After this initial reading, fall back to file-by-file lookups via Glob /
Grep / Read. Do not re-read everything every iteration.

---

## 2. Skills you will invoke

These are **named skills** in the repo's `.claude/skills/` tree. Invoke
them via the Skill tool, not by re-implementing their contracts:

- `mmo-physics-pathing-probe` — every time you make a bake change or scene-
  cache change, run the probe BEFORE the live test. Without `--load-adt`
  the probe is data-blind on most outdoor map-0 XYs; always pass it.
- `mmo-pathfinding` — for navmesh authoring decisions, off-mesh-link
  authoring, ARPG vs MMO fixture handling.
- `mmo-statemanager-orchestration` — for Phase D StateManager wiring.
- `mmo-protobuf-contracts` — for Phase C protobuf field addition.
- `mmo-live-fixtures` — for the live test invocations and screenshot
  artifact contract.

If a skill matches the trigger, invoke it instead of replicating its
work in the main context.

---

## 3. Current state at handoff

### Committed (origin/main HEAD = 9f3c55a3 as of handoff)

```
9f3c55a3 feat(pathphysicsprobe): --load-adt flag pre-loads tiles for corner sequence
7b0bd502 test(pathfinding): BRM dungeon route diagnostic + per-tile bake negative result
a5531f08 fix(pathfinding): cliff classifier requires steep slope alongside raw -6y dz
a8232189 fix(pathfinding): graceful truncation in findSmoothPath; cap MAX_POINT_PATH_LENGTH=1024
87da0952 test(pathfinding): adjust BRD/BWL targets to bot-reachable approach positions
efddd505 fix(pathfinding): bump MAX_POINT_PATH_LENGTH 740→2400 for long smooth paths
```

`a5531f08` is the cliff-classifier slope-aware fix. UBRS now classifies as
`supported:Drop cliffs=0` instead of `unsupported:Cliff cliffs=1` — the
bot accepts the route, then stalls on actual GO-collision parity gap
(this Phase 4's target).

### Uncommitted in working tree at handoff

- `docs/physics/PHASE4_GO_VARIANTS.md` — the design. **Commit this first
  thing in your session** as `docs(physics): Phase 4 GO variants design`
  so subsequent commits can reference it.
- `docs/physics/NEXT_SESSION_PROMPT.md` — this file. Either commit alongside
  the design doc as the active handoff, or leave untracked.
- `.env` — user's local Mangos config; **do NOT touch or commit**.

### Memory snapshots already in place

Don't re-run prior probe rounds. The findings are:

- BRM stall coord: `(-7949.7, -1162.8, 170.8)` on map 0.
- Live failure: bot reaches that coord, `currentSpeed=0` for 60+ seconds,
  flags oscillating between `0x0001` (FORWARD) and `0x2001` (FORWARD|JUMPING).
- Probe with `--load-adt`: `adt=158.378, vmap=-200000, bih=-200000` at the
  stall XY. Navmesh poly there has `surfaceZ=171.24`. So there's a 12y
  navmesh-vs-ADT gap with no vmap WMO to account for it.
- Single GameObject within 50y of the stall:
  `displayId=4652 at (-7940.6, -1142.4, 172.8)`. Source:
  `D:\MaNGOS\data\gameobject_spawns.json`. This is the structure the bot
  is standing on at z=170.8.
- Per-tile `agentMaxClimbTerrain` override (Cycle-17e precedent) was tested
  at 1.0y (negligible) and 0.2y (destructive). Documented as
  `_3446_NEGATIVE_RESULT` in `tools/MmapGen/config.json`. Do NOT re-attempt
  this lever — fixed cause is GO collision parity, not climb threshold.

### Protected regression baselines (must hold across every phase)

| Baseline | Command | Expected |
|---|---|---|
| Unit | `tools\scripts\run-pathfinding-tests.ps1 -TestSet unit` | 213 passed / 0 failed / 7 skipped (post-Round-2 diagnostic Fact may bump this to 214/0/7 — confirm by counting) |
| Physics | `tools\scripts\run-pathfinding-tests.ps1 -TestSet physics` | 19 passed / 0 failed / 0 skipped |
| Layer B (OG zeppelin static-path) | `WaypointGenerationTests` | 8/2 envelope (3/5 + 5/5; 2 documented spurious-strict failures) |
| OG zeppelin LIVE | `WWOW_DECKLIP_CLIMB_TEST=1` + filter `~DeckLipClimbFromGruntToFrezza` | PASS, ~113s, 0 stalls, 0 crashes, deck Z=53.9–54.1 |

Run all four after every bake or runtime change. **Three or more newly-failing
unit/physics tests → revert immediately and surface to the user.**

---

## 4. Phase-by-phase implementation plan

The design doc (§8) lists Phases A–E. Per-phase deliverables and exit
criteria below. Each phase ends with a request for user confirmation
before commit.

### Phase A — always-on GO collision (no variants yet)

**Why this first:** smallest viable slice that unblocks the BRM live theory
without introducing the protobuf / StateManager changes. Validates the
bake-and-runtime architecture against a real failing test before scaling.

**Build:**
1. **Extend `tools/GameObjectExporter`** to output a single
   `gameobject_spawns/base.json` (rename or re-emit; the existing
   `D:\MaNGOS\data\gameobject_spawns.json` is the always-on superset for
   now — base.json filters out spawns flagged with non-null `event_id` or
   `pool_template`. Read the source schema first to confirm the filter
   columns).
2. **Hook `MapBuilder::buildTransports`** in
   `tools/MmapGen/contrib/mmap/src/MapBuilder.cpp` to also iterate
   `gameobject_spawns/base.json` (rename the function to
   `buildPersistentGameObjects` and keep the hardcoded transport list as
   a fallback for now). For each spawn whose displayId resolves to a
   `.vmo` file, call existing `buildGameObject(...)` with the spawn's
   `(x, y, z, o, scale)`.
3. **Create `tools/SceneCacheBuilder/`** — minimal CLI:
   ```
   SceneCacheBuilder.exe --map <id> [--tile X,Y] --spawns <path>.json --out <dir>
   ```
   For each spawn in the input JSON, resolve displayId → model triangles
   (via `temp_gameobject_models` + the `.vmo` model file), transform by
   `(x, y, z, o, scale)`, write packed binary `(magic, version, mapId,
   tileX, tileY, triCount, triangles[][3][3])` to
   `scene-cache/<mapId>_<tileY>_<tileX>.scenecache` (sibling-tile naming
   matches mmap convention).
4. **Add `LoadSceneCacheForMap(mapId, dirPath)` C export** to
   `Exports/Navigation/PhysicsTestExports.cpp`. Iterates `<dirPath>`,
   loads every `<mapId>_*.scenecache` it finds, calls
   `DynamicObjectRegistry`'s existing `RegisterTriangles` (or whatever the
   public ingestion API is — read `DynamicObjectRegistry.h:1-165`). The
   triangles end up in the same query path that already serves
   `GetDynamicGroundZ` and the classifier's collision sampling.
5. **Wire `InitializePhysics`** to scan `scene-cache/` for the relevant
   map and call `LoadSceneCacheForMap` after `LoadDisplayIdMapping`.
6. **Extend `tools/PathPhysicsProbe`** with a `--scenecache` source row
   in the GroundZ breakdown so the per-segment probe shows whether the
   GO collision is being seen. (Currently `combined / vmap / adt / bih /
   sceneCache`; the new sceneCache value should fire for points inside
   GO 4652's footprint.)

**Phase A exit criteria:**
- Probe at `(-7949.7, -1162.8, 170.8)` map 0 with `--load-adt` returns
  `sceneCache != -200000` (specifically, ≥170y, matching GO 4652's
  walkable surface).
- PathPhysicsProbe full-route on FlameCrest→UBRS shows substantially
  more `Walk/Clear` segments than the Round-4 baseline (target: ≥600/1072
  vs current 299/1072).
- All four protected regression baselines hold.
- Live `FlameCrestToBrmDungeonEntrance` UBRS case progresses past the
  (-7949.7, -1162.8) area. (May still fail at a NEW geometric site if
  there are more GO-collision gaps further along the path; that's fine,
  Phase A's job is to fix the FIRST gap, not all of them.)
- OG zeppelin live test still passes.

Commit this phase as `feat(pathfinding): always-on GameObject collision in
bake + runtime` plus a follow-up for the `SceneCacheBuilder` tool itself.

### Phase B — variant manifest + per-variant deltas

**Build:**
1. **`tools/MmapGen/variants/manifest.yaml`** — schema per design doc §4a.
   Start with three entries: `base`, `ony-head-org`, `darkmoon-elwynn`.
   Source the spawn filters from MaNGOS DB (`game_event`,
   `game_event_gameobject`, `pool_template`); read-only MySQL is OK per
   CLAUDE.md "MaNGOS Data Access" rule.
2. **Extend `GameObjectExporter`** to emit one JSON file per manifest
   entry (`gameobject_spawns/base.json`, `.../ony-head-org.json`, etc.).
3. **Extend `MmapGen` CLI** with `--variants V1,V2,...` flag. For each
   variant + each affected tile, write `<mapId>_<tileY>_<tileX>.<variant>.mmtile`.
   Default variant if flag omitted: `base`.
4. **Extend `SceneCacheBuilder`** symmetrically — produces
   `<mapId>_<tileY>_<tileX>.<variant>.scenecache`.
5. **Per-tile variant manifest** (`<mapId>_<tileY>_<tileX>.variants.json`)
   — list of variants that touch this tile. Used by Phase C cache lookup.

**Phase B exit:**
- `MmapGen.exe 1 --tile 29,40 --variants base,ony-head-org` produces both
  `0014029.base.mmtile` and `0014029.ony-head-org.mmtile`.
- Probe with `--variants base` reproduces Phase A behavior; probe with
  `--variants base,ony-head-org` shows the head GO as collidable.

### Phase C — PathfindingService variant-aware request

**Build:**
1. **Add `repeated string active_variants = 7;`** to the
   `PathfindingService.PathRequest` protobuf message. Use the
   `mmo-protobuf-contracts` skill for the rename / regen ritual; keep
   wire compatibility (all existing callers send empty list →
   `service defaults to ["base"]`).
2. **Service composes navmesh** at request time from base + named variant
   deltas. Cache composed navmesh keyed by `(mapId, sorted variant set)`.
   LRU bound = 16 per map (per design doc §11).
3. **DynamicObjectRegistry composite load** — `LoadVariantSceneCache(mapId,
   path, variantId)` and `UnloadVariant(mapId, variantId)`. Variant
   ownership tracked per registered triangle so unload cleanly removes
   only that variant's triangles.

**Phase C exit:**
- New live test `Variant_OnyHeadOrg_RoutesAroundHeadAtCityGate` (skippable
  fact) that:
  - Issues path through Org city gate with `active_variants=[base]` →
    expects path to walk under the gate.
  - Issues same path with `active_variants=[base, ony-head-org]` →
    expects path to detour around the head.
  Skip-if-not-baked: if `0014029.ony-head-org.mmtile` is missing, the
  test skips with a guard message; CI runs it only when the variant bake
  is up to date.
- All four protected baselines still hold.

### Phase D — StateManager world-state observation

**Build:**
1. **`Services/WoWStateManager/WorldStateObserver.cs`** — periodic poll of
   `game_event` table (or SOAP `.event list` if MaNGOS supports it; check
   first). Publish active set as `StateManager.GetActiveVariantsAsync()`.
2. **BotRunner snapshot field** — `WoWActivitySnapshot.ActiveVariants`
   carries the set into every bot. `NavigationPath` / `PathfindingClient`
   passes it through transparently on every path request.
3. **End-to-end live test**: trigger Onyxia head event via SOAP
   (`.event start <ID>`), wait for StateManager to observe, dispatch a
   `TravelTo` action that crosses the Org city gate, observe path detour.
   Stop the event afterwards (`.event stop <ID>`).

### Phase E — coverage rollout

**Build:**
1. Iterate every `game_event` row in MaNGOS DB.
2. For each event with at least one `gameobject_spawn` linkage, add a
   variant manifest entry.
3. Nightly CI bake: `MmapGen --variants <all-variants> --maps 0,1` on a
   build agent.
4. `tools/scripts/promote-mmaps.ps1` extended to promote per-variant tiles.

Phase E is grunt work; no architectural decisions left.

---

## 5. Build / test commands

### Native bake (MmapGen)

```powershell
cd D:\wwow-bot\test-data
& 'e:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1'   # rebuild if needed
# Single tile:
& 'e:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe' 0 --tile 34,46 --silent --configInputPath 'e:\repos\Westworld of Warcraft\tools\MmapGen\config.json'
# Phase B+ with variants:
& 'e:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe' 0 --tile 34,46 --variants base --silent --configInputPath '...'
```

### .NET build

```powershell
cd 'e:\repos\Westworld of Warcraft'
dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release -v minimal
dotnet build tools/PathPhysicsProbe/PathPhysicsProbe.csproj --configuration Release -v minimal
dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release -v minimal
```

### Native Navigation.dll (after C++ changes)

```powershell
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild 'Exports\Navigation\Navigation.vcxproj' -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -nologo
& $msbuild 'Exports\Navigation\Navigation.vcxproj' -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -v:minimal -nologo
```

Rebuild BOTH bitnesses sequentially; shared intermediate dirs corrupt
parallel builds. x86 is for FG-injected `WoW.exe`; x64 is for
PathfindingService and BG.

### Pre-flight diagnostic

```powershell
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
& dotnet test 'Tests\PathfindingService.Tests\PathfindingService.Tests.csproj' --no-build --configuration Release --filter 'FullyQualifiedName~Audit_BrmDungeonEndpoints_ResolveAndCorridor' --logger 'console;verbosity=normal'
```

### PathPhysicsProbe drive

```powershell
# Single-segment with full diagnostic (use on suspect coords)
& '.\Bot\Release\net8.0\PathPhysicsProbe.exe' --map 0 --start -7949.7,-1162.8,170.8 --end -7950.7,-1162.77,170.83 --load-adt --verbose

# Full-route smoothPath classification (use to baseline a phase)
& '.\Bot\Release\net8.0\PathPhysicsProbe.exe' --map 0 --start -7518.7,-2159.9,131.9 --end -7524,-1233,287 --detour-resolve --smooth --load-adt > /tmp/probe-phaseN.txt
```

### Live tests (use monorepo-test-runner subagent)

```powershell
# OG zeppelin protected regression (~3 min)
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
$env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'
$env:WWOW_TEST_PATHFINDING_PORT='5101'
$env:WWOW_DECKLIP_CLIMB_TEST='1'
& dotnet test 'Tests\BotRunner.Tests\BotRunner.Tests.csproj' --no-build --configuration Release --filter 'FullyQualifiedName~DeckLipClimbFromGruntToFrezza' --logger 'console;verbosity=normal' -- RunConfiguration.TestSessionTimeout=600000

# BRM theory (~25-35 min for 4 cases)
$env:WWOW_BRM_DUNGEON_TRAVEL_TEST='1'
& dotnet test 'Tests\BotRunner.Tests\BotRunner.Tests.csproj' --no-build --configuration Release --filter 'FullyQualifiedName~FlameCrestToBrmDungeonEntrance' --logger 'console;verbosity=normal' -- RunConfiguration.TestSessionTimeout=2400000
```

### Docker container reload (after Phase D ships)

```powershell
docker compose -f docker-compose.vmangos-linux.yml build wwow-pathfinding wwow-scene-data
docker compose -f docker-compose.vmangos-linux.yml up -d wwow-pathfinding wwow-scene-data
```

---

## 6. Decision points already settled (per design doc §11)

Don't re-ask the user about these unless your implementation discovers
they're wrong:

| # | Decision | Default chosen |
|---|---|---|
| 1 | Variant identifier format | Human-readable strings (`base`, `ony-head-org`, `darkmoon-elwynn`) |
| 2 | Variant manifest format | YAML at `tools/MmapGen/variants/manifest.yaml` |
| 3 | Per-tile variant manifest | Per-tile JSON sibling `<mapId>_<tileY>_<tileX>.variants.json` |
| 4 | LRU cache size for composed navmeshes | 16 per map; revisit after measuring |
| 5 | Active-variants snapshot TTL | 60s; refresh poll on StateManager |
| 6 | Legacy `0001403.mmtile` filename strategy | Keep as symlink to `.base.mmtile` during rollout; drop after Phase D ships |

---

## 7. Hard stops / anti-patterns

Per `feedback_pathfinding_anti_patterns.md` and the freeze contract in
`PATHFINDING_OVERHAUL.md`:

- **Do NOT** lower `walkableSlopeAngle` / `walkableClimb` from harvested
  client values. Per-tile config tightening for specific demonstrated
  defects is allowed (precedent: OG zeppelin `4029` config), but global
  threshold tightening is not.
- **Do NOT** add bot-side jump-up for regular pathing. If the bake says
  walkable but the bot can't traverse, fix the bake or the runtime
  collision data, not BotRunner.
- **Do NOT** extend `Navigation.cs` repair pipeline (the 5,600-line
  managed pipeline is frozen). Fix at the bake or in `Exports/Navigation`.
- **Do NOT** add `allowDirectFallback` or any "ignore the navmesh" runtime
  workaround.
- **Do NOT** mass-disable Recast filters. The OG zeppelin Cycle-17e
  precedent specifically tightens per-tile, not loosens.
- **Do NOT** modify tests to define their own tolerances. Tests observe
  production-code SIGNALS; tolerances live in production.
- **Do NOT** blanket-kill `dotnet.exe` or `Game.exe`. CLAUDE.md "Process
  Safety" — only kill specific PIDs your session launched.
- **Do NOT** commit without explicit user request — propose first, wait.
- **Do NOT** skip hooks (`--no-verify`) or bypass signing.
- **Do NOT** push to remote unless the user explicitly asks.

If you find yourself tempted by any of these, STOP and surface to the user
with a clear description of why the legitimate path is blocked.

---

## 8. Iteration loop per phase

For Phases A, B, C, D:

1. **Read related code** before editing — repo-local CLAUDE.md / AGENTS.md,
   the file you're touching, and adjacent files for convention.
2. **Make ONE focused change** per round. Spawn a Plan agent for non-trivial
   edits to MapBuilder.cpp, SceneQuery.cpp, DynamicObjectRegistry.cpp.
3. **Build affected projects** (.NET + native if C++ touched). Both x64 and
   x86 if Navigation.dll changes. Sequentially, not in parallel.
4. **Pre-flight: PathPhysicsProbe** at the suspect coord (`--load-adt
   --verbose`). Confirm the change moves the right needle (e.g., Phase A:
   `sceneCache` field populates).
5. **Pre-flight: full-route probe** if structural change. Capture histogram
   diff to /tmp/ for the phase report.
6. **Regression: unit + physics** via `run-pathfinding-tests.ps1`. Confirm
   213 (or 214) /0/7 + 19/0/0 hold.
7. **Regression: OG zeppelin live**. Use the monorepo-test-runner subagent.
   Must PASS in ~113s, 0 stalls, 0 crashes.
8. **If regression breaks: REVERT** and try a different approach. Save the
   negative result to memory.
9. **Live BRM theory** only after regression is clean. Use the
   monorepo-test-runner subagent (~25-35 min).
10. **Brief status update at end of round** (1-2 sentences): what changed,
    live test pass/fail count, next move.
11. **Save what you learned** to memory if non-obvious. Update MEMORY.md
    with a one-line pointer.

---

## 9. Communication protocol with the user

- After each phase exit-criteria is met: surface the diff summary,
  regression evidence, and propose a commit message. **Wait for user
  approval before committing.**
- After 5 consecutive iterations within a phase without progress on the
  phase's exit criteria: surface to the user with what was tried and the
  proposed pivot.
- If the design doc itself is wrong (you discover a decision point that
  needs redoing): surface to the user with the specific finding before
  changing course.
- Brief status updates between iterations (1-2 sentences). Don't narrate
  every grep / read.

---

## 10. Useful subagent patterns

- **Plan agent** for non-trivial C++/C# edits, especially in MapBuilder.cpp,
  SceneQuery.cpp, DynamicObjectRegistry.cpp, NavigationPath.cs.
- **Explore agent** for "where is X defined" or "map the data flow" questions.
- **monorepo-test-runner** for regression / live test runs. Keeps the main
  context clean and lets long tests (~30 min) run without burning your
  cache.
- **monorepo-explorer** for read-only architectural questions.
- **codex:rescue** if you get stuck in a debugging spiral and want a fresh
  pair of eyes (per `e:/repos/.claude/agents/codex/rescue.md`).

---

## 11. End-of-session checkpoint

When the session ends (compaction, manual stop, or phase rollover):

1. Update `docs/physics/NEXT_SESSION_PROMPT.md` (this file) with the
   current phase, last commit, and any in-flight uncommitted work.
2. Save key non-obvious findings to memory, update `MEMORY.md` index.
3. Leave the working tree clean OR document exactly what's uncommitted
   and why.

---

## 12. North-star scoreboard

| Goal | Status | Verification |
|---|---|---|
| BRM 4652 case unblocked | TODO | Probe `sceneCache` populates at the stall coord; live UBRS gets past (-7949.7, -1162.8) |
| Always-on GO collision parity | TODO (Phase A) | Probe Walk count on FlameCrest→UBRS rises from 299 to ≥600 |
| Onyxia head event-aware routing | TODO (Phase B+C+D) | Live test: event ON → bot detours; event OFF → bot uses gate |
| Darkmoon Faire event-aware routing | TODO (Phase B+C+D) | Live test in active host city during faire window |
| All-event coverage | TODO (Phase E) | Every `game_event` with GO spawns has a manifest entry |
| OG zeppelin protected | OK at handoff | Live test passes in ~113s through every phase |
| Cliff classifier slope-aware | OK at handoff (commit `a5531f08`) | UBRS classifies as supported:Drop |

Good luck. Read the design doc carefully — it captures the WHY behind
each architectural choice, and you'll need that context when sub-decisions
come up that aren't explicit in this prompt.
