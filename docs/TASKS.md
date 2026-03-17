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

## P1 - BG Movement Physics Calibration (HIGH PRIORITY)

BG bot rubber-bands, stands in air, and fails to clamp to slopes. Movement flags don't match what the server expects. This breaks gathering, NPC interaction, and loot tests because the bot can't reliably reach targets.

| # | Task | Status |
|---|------|--------|
| 1.1 | **Moveflag calibration tests.** 6 tests covering grounded flicker, walk-off-ledge, landing, airborne velocity lock, flag transitions, slope clamping. All 13 physics tests pass. | **Done** (5b4a1c5) |
| 1.2 | **Airborne horizontal velocity lock (C++).** Added `input.fallTime == 0` guard in StepV2 — horizontal velocity set only on first airborne frame. Prevents mid-air steering. | **Done** (5b4a1c5) |
| 1.3 | **False-freefall guard hardening (C#).** Added `_hasPhysicsGroundContact` tracking. Guard requires confirmed ground contact before engaging, preventing elevated-spawn hover. Retains `_currentPath != null` for production safety. | **Done** (5b4a1c5) |
| 1.4 | **Spline movement lockout.** During server-driven spline movement (knockback, charge, etc.), player input must be suppressed until the spline completes. Verify MovementController handles this. | Open |
| 1.5 | **Post-teleport settle.** After teleport, ensure movement flags reset to MOVEFLAG_NONE and first heartbeat has correct ground-clamped Z before allowing any new movement. | Open |

---

## P7 - Pathfinding Hardening (ACTIVE)

Core ghost-stuck, corridor collision, and object-aware paths all done (7.1-7.5 archived). Remaining: shoreline routes, route metadata, spatial queries, swim avoidance.

| # | Task | Status |
|---|------|--------|
| 7.4 | **Ratchet shoreline/fishing-hole route hardening.** Native lateral detour generation in PathFinder.cpp works. Service-side `[PATH_DIAG]` logging works. Missing: bot-side execution trace (planned-vs-executed drift detection). | ~70% — in progress |
| 7.7 | **Route affordance metadata.** Classify path transitions (walk/step-up/jump/drop/swim/blocked). | Open |
| 7.8 | **Decision-grade spatial queries.** Reachability/LOS/surface queries for better approach points. | Open |
| 7.9 | **Swim-avoidance for land-only tasks.** Pathfinding must avoid routing into deep water for tasks that can't be performed while swimming (fishing, gathering, combat). FishingTask already checks `IsSwimming` and aborts (line 414), but pathfinding doesn't know to stay on land. Need navmesh area cost weighting or path post-filter to prefer shore routes. | Open |

**Note:** P7.5 (object-aware path requests) is **COMPLETE** — proto contract, BotRunner overlay builder, service-side mount/unmount all shipping. P7.6 (overlay-aware validation) was **replaced** by corridor-based pathfinding (`FindPathCorridor`). Both archived.

---

## Navmesh — Full VMAP Re-extraction + Rebuild (COMPLETE)

Re-extracted vmaps from 1.12.1 client MPQs using VMaNGOS VMapExtractor (was using old CMaNGOS 2022 data). Regenerated mmaps for maps 0+1. Valley of Trials tile gained 119 polygons, 97 vertices. Added post-corridor segment validation with lateral repair.

| # | Task | Status |
|---|------|--------|
| N.1 | Full tile rebuild for map 0 (Eastern Kingdoms) — 687 tiles | **Done** |
| N.2 | Full tile rebuild for map 1 (Kalimdor) — 1018 tiles | **Done** |
| N.3 | VMAP re-extraction from WoW 1.12.1 MPQs with VMaNGOS tools | **Done** (2026-03-17) |
| N.4 | Post-corridor ValidateWalkableSegment with lateral repair | **Done** (57ec3eb) |
| N.5 | BG undermap fall on downhill (ReverseHill Z=-2083) — path-based underground snap at -10y below waypoint Z | **Done** (5a73465) |
| N.6 | FG rock collision — walkableRadius=2 in config.json erodes 2 cells (0.533y) from walls. Mmaps regenerated + deployed. | **Done** (config.json, data deployed) |
| N.7 | Wall-stuck repath suppression — physicsHitWall was permanently resetting stall counter. Now tracks consecutive wall hits (15 threshold) and forces repath. | **Done** |

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
- **Last updated:** 2026-03-17 (session 105)
- **Branch:** `cpp_physics_system`
- **Completed this session:**
  - N.5: Path-based underground snap — catches terrain fallthrough at -10y below waypoint Z (5a73465)
  - N.6: walkableRadius=2 in config.json for maps 0+1, mmaps regenerated + deployed
  - N.7: Wall-stuck repath — NavigationPath tracks consecutive physicsHitWall ticks (threshold=15), forces repath instead of permanently suppressing stall detection
- **Data dirs:** Server reads from `D:/MaNGOS/data/` (DataDir in mangosd.conf). VMaNGOS tools at `D:/vmangos-server/`. Source at `D:/vmangos/`.
- **Next:**
  1. P1.4: Spline movement lockout
  2. P1.5: Post-teleport settle
  3. P7.4: Ratchet shoreline route hardening
