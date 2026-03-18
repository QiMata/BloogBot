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

Core ghost-stuck, corridor collision, and object-aware paths all done (archived). Remaining: shoreline routes, route metadata, spatial queries, swim avoidance.

| # | Task | Status |
|---|------|--------|
| 7.1 | **Ratchet shoreline/fishing-hole route hardening.** Native lateral detour generation in PathFinder.cpp works. Service-side `[PATH_DIAG]` logging works. Missing: bot-side execution trace (planned-vs-executed drift detection). | ~70% — in progress |
| 7.2 | **Route affordance metadata.** Classify path transitions (walk/step-up/jump/drop/swim/blocked). | Open |
| 7.3 | **Decision-grade spatial queries.** Reachability/LOS/surface queries for better approach points. | Open |
| 7.4 | **Swim-avoidance for land-only tasks.** Pathfinding must avoid routing into deep water for tasks that can't be performed while swimming (fishing, gathering, combat). FishingTask already checks `IsSwimming` and aborts (line 414), but pathfinding doesn't know to stay on land. Need navmesh area cost weighting or path post-filter to prefer shore routes. | Open |

---

## P3 - Fishing Parity (Low Priority)

**FishingTask is implemented and passing live validation for both BG and FG.** Remaining work is packet-level optimization, not core mechanics.

| # | Task | Status |
|---|------|--------|
| 3.1 | Capture FG fishing packets (cast → channel → bobber → custom anim) | Open — packet infra ready |
| 3.2 | Compare BG fishing packets against FG capture | Blocked on 3.1 |
| 3.3 | Harden BG fishing parity to match FG packet/timing | Blocked on 3.2 |

---

## P4 - Movement Flags After Teleport (BT-MOVE-001/002)

ConnectionStateMachine handles MSG_MOVE_TELEPORT/ACK. MovementController.Reset() clears flags to MOVEFLAG_NONE. Remaining: formal FG packet capture test to verify no flag divergence.

| # | Task | Status |
|---|------|--------|
| 4.1 | Capture FG teleport packets (MSG_MOVE_TELEPORT_ACK → first heartbeats) | Open — packet infra ready |
| 4.2 | Compare BG teleport behavior — identify remaining flag divergence | Blocked on 4.1 |
| 4.3 | Fix any remaining MovementController flag issues found | Blocked on 4.2 |

---

## Cleanup — PhysicsEngine Dead Code

| # | Task | Status |
|---|------|--------|
| C.1 | **Remove PhysicsThreePass.cpp.** Dead code — functions never called by StepV2. Not even in vcxproj. Deleted .cpp (727 lines) + .h (148 lines). | **Done** |
| C.2 | **High-impact magic number extraction.** Replaced ~30 instances: `1e-6f`→`VECTOR_EPSILON` (25x), `1e-4f`→`GROUND_SNAP_EPSILON` (4x), `60.0f`→`TERMINAL_VELOCITY`, `0.7f`→`OVERLAP_NORMAL_Z_FILTER`, `0.05f`→`MAX_DEFERRED_DEPEN_PER_TICK`, `4`→`MAX_OVERLAP_RECOVER_ITERATIONS`, `0.5f`→`WATER_ENTRY_VELOCITY_DAMP`. ~50 replay-tuning numbers left (context-specific, low priority). | **Done** |

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
- **Last updated:** 2026-03-18 (session 111)
- **Branch:** `cpp_physics_system`
- **Completed this session:**
  - Investigated pathfinding through steep slopes and game objects (P7.1 root cause)
  - Added NAV_STEEP_SLOPES (0x10) to MoveMapSharedDefines.h matching VMaNGOS flag
  - Added area cost penalty (10x) for AREA_STEEP_SLOPE (3) and AREA_STEEP_SLOPE_MODEL (4) in PathFinder.cpp createFilter()
  - Tested walkableClimb=8/walkableHeight=8 config — caused 4 regressions (physics sweep failures on new routes). Reverted to defaults.
  - Regenerated 25 mmap tiles for Valley of Trials, Ratchet, and Orgrimmar using MoveMapGenerator.exe with `D:/vmangos/contrib/mmap/config.json`
  - Verified: 40/40 pathfinding tests pass, 136/137 physics tests pass (baseline)
  - Fishing live validation: pre-existing failure (bobber detection, not pathfinding)
  - Added NavPath protected accessor on BotTask for diagnostic access
- **Data dirs:** Server reads from `D:/MaNGOS/data/` (DataDir in mangosd.conf). VMaNGOS tools at `D:/vmangos-server/`. Source at `D:/vmangos/`.
- **Mmap regen:** Run from `e:/repos/Westworld of Warcraft/Bot/Debug/net8.0/` with `"D:/vmangos-server/MoveMapGenerator.exe" 1 --tile X,Y --configInputPath "D:/vmangos/contrib/mmap/config.json" --silent`
- **Test baseline:** 40/40 pathfinding, 136/137 physics, 1267 WoWSharpClient
- **Next:**
  1. P7.1: Finish Ratchet shoreline route hardening (bot-side execution trace for planned-vs-executed drift)
  2. P7.4: Swim-avoidance for land-only tasks (NAV_WATER area cost in createFilter)
  3. Run live parity suite to measure cliff detection improvement
