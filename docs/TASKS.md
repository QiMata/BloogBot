# Master Tasks

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute one local `TASKS.md` at a time in queue order.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live.** Never defer live validation tests.
5. **Compare to VMaNGOS server code** when implementing packet-based functionality.
6. Every implementation slice must add or update focused unit tests.
7. After each shipped delta, commit and push before ending the pass.

---

## P7 - Pathfinding Hardening (ACTIVE)

Core ghost-stuck, corridor collision, and object-aware paths all done (7.1-7.5 archived). Remaining: shoreline routes, route metadata, spatial queries.

| # | Task | Status |
|---|------|--------|
| 7.4 | **Ratchet shoreline/fishing-hole route hardening.** Native lateral detour generation in PathFinder.cpp works. Service-side `[PATH_DIAG]` logging works. Missing: bot-side execution trace (planned-vs-executed drift detection). | ~70% — in progress |
| 7.7 | **Route affordance metadata.** Classify path transitions (walk/step-up/jump/drop/swim/blocked). | Open |
| 7.8 | **Decision-grade spatial queries.** Reachability/LOS/surface queries for better approach points. | Open |

**Note:** P7.5 (object-aware path requests) is **COMPLETE** — proto contract, BotRunner overlay builder, service-side mount/unmount all shipping. P7.6 (overlay-aware validation) was **replaced** by corridor-based pathfinding (`FindPathCorridor`). Both archived.

---

## Navmesh — Full Map Rebuild (COMPLETE)

Both continents rebuilt with GO collision baking. 515 tiles (map 0) + 785 tiles (map 1) regenerated. Tiles copied to `Bot/Debug/net8.0/mmaps/`. PathfindingService: 39/40 pass (1 pre-existing). Physics: 109/0/1.

| # | Task | Status |
|---|------|--------|
| N.1 | Full tile rebuild for map 0 (Eastern Kingdoms) | **Done** |
| N.2 | Full tile rebuild for map 1 (Kalimdor) | **Done** |
| N.3 | Verify no pathfinding regressions | **Done** — 39/40 pass, 109/110 physics pass |

---

## P3 - Fishing Parity (Low Priority)

**FishingTask is implemented and passing live validation for both BG and FG.** BG: skill 75→76, pool detection, loot success confirmed. Remaining work is packet-level optimization, not core mechanics. Intermittent skips are pool respawn timers (world state, not bugs).

| # | Task | Status |
|---|------|--------|
| 3.1 | Capture FG fishing packets (cast → channel → bobber → custom anim) | Open — packet infrastructure ready |
| 3.2 | Compare BG fishing packets against FG capture | Blocked on 3.1 |
| 3.3 | Harden BG fishing parity to match FG packet/timing | Blocked on 3.2 |

---

## P4 - Movement Flags After Teleport (BT-MOVE-001/002)

ConnectionStateMachine handles MSG_MOVE_TELEPORT/ACK. MovementController.Reset() clears flags to MOVEFLAG_NONE. Recent fixes (`eda25b0`, `94f5d1a`) addressed post-teleport Z clamp and slope guard. Remaining: formal FG packet capture test to verify no flag divergence.

| # | Task | Status |
|---|------|--------|
| 4.1 | Capture FG teleport packets (MSG_MOVE_TELEPORT_ACK → first heartbeats) | Open — packet infra ready |
| 4.2 | Compare BG teleport behavior — identify remaining flag divergence | Blocked on 4.1 |
| 4.3 | Fix any remaining MovementController flag issues found | Blocked on 4.2 |

---

## Blocked - Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in `RecordedTests.Shared` | Requires `AWSSDK.S3` |
| `RTS-MISS-002` | Azure ops in `RecordedTests.Shared` | Requires `Azure.Storage.Blobs` |

## Capability Gaps (Low Priority)

| ID | Issue | Status |
|----|-------|--------|
| `BG-PET-001` | BG pet support — `Pet` returns null. Hunter/Warlock won't work. | Open |

---

## Canonical Commands

```bash
# Full LiveValidation suite
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Corpse-run only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m

# Combat tests only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests"

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# Full solution
dotnet test WestworldOfWarcraft.sln --configuration Release
```

## Session Handoff
- **Last updated:** 2026-03-15 (session 100)
- **Branch:** `cpp_physics_system`
- **Completed this session:** FG ghost corpse run fix (`5fe0ea1`). TASKS.md cleanup. Full navmesh rebuild — maps 0+1 (1300 tiles) with GO collision baking. PathCalculationTask threshold adjusted for new routing.
- **Next:**
  1. P7.4: Bot-side execution trace for shoreline route drift detection
  2. P3/P4: FG packet capture sessions (fishing + teleport) when convenient
