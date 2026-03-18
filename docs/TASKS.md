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

---

## Infrastructure — Data Directory Centralization

| # | Task | Status |
|---|------|--------|
| D.1 | **Centralize mmap/vmap/maps data files.** Move all navmesh data (mmaps/, vmaps/, maps/) from build output dirs (`Bot/Debug/net8.0/`, `Bot/Release/net8.0/`) to a single `Data/` directory at the solution root. Add `WWOW_DATA_DIR` env var support. Update `NavigationFixture.EnsureDataDir()`, `PathfindingService`, and all data-loading code to use the centralized path. Add `Data/` to `.gitignore`. | Open |

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
- **Last updated:** 2026-03-18 (session 112, continued)
- **Branch:** `cpp_physics_system`
- **Completed this session:**
  - P7.1: Drift detection (commit eb828cb)
  - P7.2: Route affordance metadata — SegmentAffordance enum, PathAffordanceInfo on TraceSnapshot (commit 826e690)
  - P7.3: Spatial queries — IsPointOnNavmesh + FindNearestWalkablePoint full-stack C++→P/Invoke→gRPC→Client (commit ec761b1)
  - P7.4: Swim avoidance — GatherNodeTask IsSwimming check (commit eb828cb)
  - Collision-aware path following (wall normal steering + LOS lookahead) verified already implemented
  - Navigation.dll rebuilt with new spatial query exports, 136/136 physics tests pass
  - Data directory centralization TODO added (D.1)
- **P7 complete.** All 4 tasks done. Phase can be archived.
- **Data dirs:** Server reads from `D:/MaNGOS/data/`. VMaNGOS tools at `D:/vmangos-server/`. Source at `D:/vmangos/`.
- **Test baseline:** 136/137 physics (1 skip)
- **Next:**
  1. Move P7 completed items to ARCHIVE.md
  2. Continue broader TASKS.md work — P3 fishing parity, P4 movement flags, D.1 data centralization
  3. Work toward full BG/FG parity per user request
