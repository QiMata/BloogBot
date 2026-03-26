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

## P4 - Movement Flags After Teleport (BT-MOVE-001/002) — CLOSED

ConnectionStateMachine handles MSG_MOVE_TELEPORT/ACK. MovementController.Reset() clears flags to MOVEFLAG_NONE. FG packet evidence captured; BG flag reset verified by deterministic test.

| # | Task | Status |
|---|------|--------|
| 4.1 | Capture FG teleport packets (MSG_MOVE_TELEPORT_ACK → first heartbeats) | **Done** (session 188) — FG `packets_TESTBOT1.csv` shows TELEPORT_ACK recv→send at 193→208ms, first heartbeat at 684ms |
| 4.2 | Compare BG teleport behavior — identify remaining flag divergence | **Done** — `NotifyTeleportIncoming` clears `MovementFlags` to `MOVEFLAG_NONE`; deterministic test added |
| 4.3 | Fix any remaining MovementController flag issues found | **Done** — no divergence found; BG already matches FG |

---

## P5 - Ragefire Chasm 10-Man Dungeoneering Test

**Goal:** A live integration test that launches 10 bots (1 FG + 9 BG) as a raid group, enters Ragefire Chasm (map 389), and clears the dungeon using coordinated tank/heal/DPS rotations. Validates that the dungeoneering orchestration, group coordination, and class-role combat rotations all work end-to-end.

### Raid Composition

| Slot | Account | Role | Class | Race | Gender | Runner |
|------|---------|------|-------|------|--------|--------|
| 1 | TESTBOT1 | Main Tank / Raid Leader | Warrior | Orc | Female | Foreground |
| 2 | RFCBOT2 | Off-Tank | Shaman | Orc | Female | Background |
| 3 | RFCBOT3 | Healer | Druid | Tauren | Male | Background |
| 4 | RFCBOT4 | Healer | Priest | Undead | Male | Background |
| 5 | RFCBOT5 | DPS | Warlock | Undead | Male | Background |
| 6 | RFCBOT6 | DPS | Hunter | Orc | Female | Background |
| 7 | RFCBOT7 | DPS | Rogue | Undead | Female | Background |
| 8 | RFCBOT8 | DPS | Mage | Troll | Male | Background |
| 9 | RFCBOT9 | DPS | Warrior | Orc | Female | Background |
| 10 | RFCBOT10 | DPS | Warrior | Tauren | Female | Background |

*Trope: physical classes (Warrior, Hunter, Paladin, Rogue) = Female; magic classes (Druid, Priest, Shaman, Warlock, Mage) = Male.*

### Implementation Tasks

| # | Task | Status |
|---|------|--------|
| 5.1 | Create MaNGOS accounts (RFCBOT2–RFCBOT10) + GM level 6 via SOAP. Characters auto-created on first bot login, then leveled via `.character level` | **Done** (SOAP) |
| 5.2 | Create `RagefireChasm.settings.json` — 10-bot StateManager config with dungeoneering mode | **Done** (eb3fddd) |
| 5.3 | Restore `DungeoneeringTask` from commit `0e7e0bf` — adapt to current BotRunner architecture (IBotTask, behavior trees, NavigationPath) | **Done** (541a941) |
| 5.4 | Add dungeoneering coordinator to StateManager — group formation, raid conversion, ready check, dungeon entry at RFC portal (1811, -4410, -18) on Kalimdor | **Done** (5a2ae0b) |
| 5.5 | Implement role-aware combat sequences — tank (hold aggro, skull mark), healer (lowest-HP party member), DPS (assist skull target), off-tank (pickup adds) | **Done** (DungeoneeringCoordinator + DungeoneeringTask) |
| 5.6 | Add rest/buff coordination — CanProceed check (all members HP>85%, mana>80%) before pulls | **Done** (541a941, built into DungeoneeringTask) |
| 5.7 | Create `RagefireChasmTests.cs` — test fixture launches StateManager with RFC config, asserts: group formed, dungeon entered (map=389), mobs killed, forward progress | **Done** (eb3fddd) |
| 5.8 | Add dungeon waypoint data for RFC map 389 — encounter positions from `creature` table for mapId=389 | **Done** (541a941, DungeonWaypoints.cs) |

### Key Architecture

- **Test fixture** simply launches StateManager with `RagefireChasm.settings.json` and polls snapshots
- **StateManager** coordinates group formation: FG bot (TESTBOT1) invites all 9 BG bots, converts to raid, sets loot rules, then teleports all to RFC entrance
- **DungeoneeringTask** (restored from `0e7e0bf`) handles in-dungeon behavior: leader navigates waypoints, pulls encounters with skull marks; non-leaders follow leader within 15y
- **BotProfiles** provide class-specific rotations (already exist for Warrior, Warlock, Mage, etc.)
- **MapTransitionGraph** already has RFC portal coordinates: Kalimdor (1811, -4410, -18) ↔ RFC (3, -11, -18)
- **PartyNetworkClientComponent** handles all group/raid operations (invite, accept, convert, ready check)
- **GroupManager** provides role-aware target selection (skull first, heal lowest HP)

### Reference

- Old DungeoneeringTask: `git show 0e7e0bf:BotRunner/Tasks/DungeoneeringTask.cs`
- Old working commit: `git show 0e7e0bf` ("Working dungeoneering... again")
- Functional pathfinding: `git show 9d3fb0c` ("Functional dungeon pathfinding implemented")
- Basic crawling: `git show 2b39d21` ("Basic dungeon crawling implemented")
- Group formation test: `Tests/BotRunner.Tests/LiveValidation/GroupFormationTests.cs`
- Party sequences: `Exports/BotRunner/BotRunnerService.Sequences.Party.cs`
- RFC portal: `Exports/BotRunner/Movement/MapTransitionGraph.cs:157`

---

## Blocked - Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in `RecordedTests.Shared` | Requires `AWSSDK.S3` |
| `RTS-MISS-002` | Azure ops in `RecordedTests.Shared` | Requires `Azure.Storage.Blobs` |

---

## Canonical Commands

```bash
# Full LiveValidation suite
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Corpse-run only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m

# Combat tests only (BG + FG collections)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatBgTests|FullyQualifiedName~CombatFgTests"

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# Full solution
dotnet test WestworldOfWarcraft.sln --configuration Release
```

## P6 — AABB Collision Rewrite (WoW.exe Exact Parity) — COMPLETE

**Status: ALL 13 ITEMS IMPLEMENTED. 29/29 unit tests pass. ~2100 lines of workarounds deleted.**

### Completed Items

| # | Task | Status |
|---|------|--------|
| 6.1 | `SweepAABB` + `TestTerrainAABB` with SAT AABB-triangle (13 axes) | **Done** |
| 6.2 | `TestTerrainAABB` with barycentric Z interpolation | **Done** |
| 6.3 | `CollisionStepWoW` with exact WoW.exe bounds + 2-pass swept AABB | **Done** |
| 6.4 | Delete 3-pass system (964 lines: DecomposeMovement, ExecuteUp/Side/Down, PerformThreePassMove, GroundMoveElevatedSweep) | **Done** |
| 6.5 | Remove terrain-following hack from CollideAndSlide | **Done** |
| 6.6 | Remove false-FALLINGFAR stripping from MovementController | **Done** |
| 6.7 | Remove ground contact persistence (multi-probe rescue) | **Done** |
| 6.8 | Remove walk experiment, teleport Z clamp, dead reckoning, slope guards (631 lines from MC) | **Done** |
| 6.9 | 29 unit tests: flat, uphill, downhill, ledge, landing, diagonal, backward, walk, gravity, jump, terminal vel, facing, heartbeat, combat approach, Undercity WMO probe | **Done** |
| 6.10 | RFC corridor tests | Deferred — needs SceneCache for map 389 |
| 6.11 | Physics replay: avg 0.095y (ground-only ~0.06y; inflated by elevator transport frames) | **Investigated** |
| 6.12 | Live tests: speed PASS, combat PASS, basic/lifecycle/equip PASS | **Done** |
| 6.13 | Diagonal damping sin(45°) for forward+strafe (was 41% too fast) | **Done** |

### WoW.exe Constant Parity (All Verified Against Binary)

| Constant | Binary VA | Value | Status |
|----------|-----------|-------|--------|
| Gravity | 0x0081DA58 | 19.29110527 | Exact |
| Jump velocity | 0x7C626F | 7.955547 | Exact |
| Swim jump velocity | 0x7C6266 | 9.096748 | Exact |
| Terminal velocity | 0x0087D894 | 60.148003 | Exact |
| Safe fall velocity | 0x0087D898 | 7.0 | Exact |
| Step height | CMovement+0xB4 | 2.027778 | Exact |
| Collision skin | CMovement+0xB0 | 0.333333 | Exact |
| Slope limit | 0x0080E008 | tan(50°) = 1.19175 | Exact |
| Walkable threshold | 0x0080DFFC | cos(50°) = 0.6428 | Exact |
| Diagonal damping | 0x0081DA54 | sin(45°) = 0.70711 | Exact |
| Flag mask | 0x618909 | 0x75A07DFF | Exact |
| Heartbeat interval | 0x5E2110 | 100ms | Exact |
| Facing threshold | 0x80C408 | 0.1 rad | Exact |
| Delta clamp | 0x618D0D | [-500ms, +1000ms] | Exact |
| Collision skin epsilon | 0x80DFEC | 1/720 = 0.001389 | Exact |
| AABB diagonal factor | 0x80E00C | √2 = 1.41421 | Exact |
| Speed jitter threshold | 0x80C5BC | 9.0 (3² y/s) | Exact |
| Teleport speed threshold | 0x80C734 | 3600.0 (60² y/s) | Exact |

---

## P7 — Transport/Elevator Coordinate Transforms (WoW.exe Parity)

**Goal:** Handle transport entry/exit coordinate transforms matching WoW.exe's CMovement::Update (VA 0x618C30). This is the remaining calibration gap — elevator rides produce 40-55y Z errors because we don't transform between world and transport-local coordinates.

### Problem
Physics replay calibration shows:
- **Ground mode (non-transport):** avg 0.06y — excellent
- **Ground mode (with transport):** avg 0.165y — inflated by elevator Z jumps
- **Transport mode:** avg 0.301y — elevator position sync lag
- **Worst frame:** 6.41y from Undercity elevator recording (frame 912: Z jumps 40.9y at transport entry)

Root cause: Recording `Dralrahgra_Undercity_2026-02-13_19-26-54` captures an elevator ride:
- Frames 0-911: walking underground at Z=-43.1 (WMO floor — our data IS correct)
- Frame 912: steps onto Undervator → `transportGuid` changes → position switches to transport-local coordinates → Z appears to jump 40.9y
- Frame 1525: steps off elevator → back to world coordinates → Z jumps 55.6y

### WoW.exe Transport Handling (from binary decompilation)

**CMovement::Update (0x618C30):**
```
1. Check spline (+0xA4) → hasSpline flag
2. Check transport GUID (+0x38, +0x3C):
   - If transportGuid != 0: set flag 0x2000000 in MovementInfo
   - Position in packets = transport-local offset
   - Collision uses world-space position = transport.position + rotate(offset, transport.orientation)
3. Vec3TransformCoord (0x4549A0): rotates displacement by transport's 3x3 matrix
```

**CMovement::CollisionStep (0x633840):**
```
// Lines 0x6338E8-0x633977: Transport coordinate transform
if (transportGuid != 0) {
    // Build 3x3 rotation matrix from transport orientation
    matrix = RotationMatrix(transport.orientation)
    // Transform displacement from world to transport-local
    displacement = matrix * displacement
    // Transform position from transport-local to world for collision
    worldPos = transport.pos + matrix * localOffset
}
// ... collision in world space ...
// Inverse transform result back to transport-local
if (transportGuid != 0) {
    localOffset = inverseMatrix * (worldPos - transport.pos)
}
```

**MovementInfo wire format (0x7C6340):**
```
+0x08  uint64  transportGuid     (0 if not on transport)
+0x10  uint32  flags | 0x2000000  (set when on transport)
+0x18  Vec3    transportOffset   (position relative to transport origin)
+0x24  float   transportFacing   (facing relative to transport)
```

### Implementation Tasks

| # | Task | Status |
|---|------|--------|
| 7.1 | Detect transport entry/exit in physics replay frames (transportGuid field changes) | **Done** |
| 7.2 | Implement world↔transport coordinate transform in `CollisionStepWoW` matching 0x633840 | **Done** |
| 7.3 | Transform displacement by transport orientation matrix before collision (0x4549A0 `Vec3TransformCoord`) | **Done** |
| 7.4 | Inverse-transform result position back to transport-local after collision | **Done** |
| 7.5 | Handle elevator spline evaluation — Undercity elevators use gameobject transport splines | **Done** |
| 7.6 | Update `MovementController` to track transport state and switch coordinate frames | **Done** |
| 7.7 | Update heartbeat packets to include transport offset when on transport (flag 0x2000000) | **Done** |
| 7.8 | Add Undercity elevator ride recording/parity test (BG rides elevator, compare Z trajectory with FG) | **Done** |
| 7.9 | Add second Orgrimmar transport recording/parity test | Open — the FG recorder can now resolve/inject the active MoTransport even when visible-object enumeration misses it, but the repo still needs a fresh Orgrimmar capture that exercises that path |
| 7.10 | Fix physics replay to exclude transport-transition frames from ground mode scoring | **Done** |
| 7.11 | Calibration gate: ground avg < 0.08y, transport avg < 0.15y, aggregate p99 < 2.0y | **Done** |

### Latest Outcome (2026-03-23)

- Transport packet/world-state parity is now implemented for BG:
  - wire packets always serialize world `Position/Facing` plus transport-local offset/orientation when `MOVEFLAG_ONTRANSPORT` is active,
  - `MovementController` now switches physics input/output between world and transport-local frames,
  - `WoWSharpObjectManager` now keeps passenger world coordinates synced from transport-local state each game loop.
- Native replay parity is back within the intended gate:
  - `UndercityElevatorReplay_TransportAverageStaysWithinParityTarget`: transport avg `0.0303y`, p99 `0.2169y`, max `0.3619y`
  - `ElevatorRideV2_FrameByFrame_PositionMatchesRecording`: avg `0.0142y`, steady-state p99 `0.1190y`, max `0.3619y`
  - `AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds`: avg `0.0124y`, p99 `0.1279y`, worst `2.2577y`
- Managed/runtime transport mover parity is now in place:
  - direct `SMSG_MONSTER_MOVE` routing now activates gameobject transport splines at runtime,
  - moving transport gameobjects advance their own spline state in the object manager loop,
  - passengers riding those movers stay stable in transport-local coordinates while their world coordinates resync each spline tick.
- Remaining P7 follow-ups are narrower:
  - `7.9` additional Orgrimmar transport replay coverage: the repo contains `Dralrahgra_Durotar_2026-02-08_11-06-02` (Orgrimmar zeppelin) but it loses dynamic object snapshots as soon as boarding starts, so only the ground-side transition windows can be replayed today. The FG recorder now has the missing GUID-resolution/injection path for future MoTransport captures; what remains is collecting a fresh recording during the final live-validation pass.

### Key Files
- `Exports/Navigation/PhysicsEngine.cpp` — `CollisionStepWoW` transport transform
- `Exports/WoWSharpClient/Movement/MovementController.cs` — transport state tracking
- `Exports/WoWSharpClient/Parsers/MovementPacketHandler.cs` — transport offset in packets
- `Exports/WoWSharpClient/Models/WoWUnit.cs` — TransportGuid, TransportOffset fields
- `Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs` — existing elevator tests
- `Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs` — new elevator parity tests

### WoW.exe Binary References
| Address | Function | Purpose |
|---------|----------|---------|
| 0x633840 | `CMovement::CollisionStep` | Transport coordinate transform at entry |
| 0x6338E8 | Transport matrix build | 3x3 rotation from transport orientation |
| 0x633977 | Post-collision inverse transform | Result back to transport-local |
| 0x4549A0 | `Vec3TransformCoord` | Matrix × vector rotation |
| 0x618C30 | `CMovement::Update` | Transport GUID check, flag 0x2000000 |
| 0x7C6340 | `FillMovementInfo` | Transport offset serialization |
| 0x7C6490 | `BeginFall` | Transport fall handling |

### Undercity Elevator Data (from recording analysis)
- **Elevator GUID:** 17374887708928814949 (gameobject entry 20655, displayId 455, "Undervator")
- **Lower position:** Z ≈ -40.8 (underground Undercity)
- **Upper position:** Z ≈ 55.4 (surface, Lordaeron ruins)
- **Travel distance:** ~96y vertical
- **Transport type:** gameObjectType=11 (GAMEOBJECT_TYPE_TRANSPORT)
- **WMO floor confirmed:** GetGroundZ at (1558, 229, -43) returns -43.103 (0.003y error)

---

## Session Handoff
- **Last updated:** 2026-03-26 (session 215)
- **Branch:** `main`
- **Session 215 — `0x632A30` wrapper gates and `0x6376A0` selector-plane init are now pinned as pure binary seams:**
  - Added pure [InitializeSelectorSupportPlane(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [ClampSelectorReportedBestRatio(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), and [FinalizeSelectorTriangleSourceWrapper(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Refactored [EvaluateSelectorDirectionRanking(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) to use the same binary-backed reported-ratio clamp instead of duplicating the inline `0x80DFEC` zero-clamp logic.
  - Added deterministic coverage in [WowSelectorSourceWrapperTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceWrapperTests.cs), which now pins the `(0,0,1,0)` selector-plane initializer, the exact reported-ratio zero clamp, the no-override early failure path, the override bypass, and the success-path zero clamp from `0x632A30`.
  - Practical implication: the wrapper around `0x632280` is no longer inferred at its visible edges. The remaining open work there is the full `0x631BE0 -> 0x631E70 -> 0x632280` data transaction, not the wrapper’s early-return or reported-ratio behavior.
- **Fresh binary evidence (session 215):**
  - Added raw captures [0x632A30_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x632A30_disasm.txt) and [0x6376A0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6376A0_disasm.txt), then updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) to record the exact wrapper flow and the selector-plane initializer.
- **Test baseline (session 215):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSourceWrapperTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests|FullyQualifiedName~WowAabbMergeTests" --logger "console;verbosity=minimal"`
    - Passed (`17/17`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 214 — `0x6373B0` AABB merge helper is now pinned as a pure binary seam:**
  - Added pure [MergeAabbBounds(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp), and added matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Replaced the local merged-query lambda in [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) with the new binary-backed helper so the start/end/half-step query volume is built through the same named seam the tests pin.
  - Added deterministic coverage in [WowAabbMergeTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowAabbMergeTests.cs), which now pins the exact componentwise min/max union semantics from `0x6373B0`, including shared-face preservation.
  - Practical implication: one more piece of the unresolved `0x631E70` / merged-query cache-miss path is no longer inferred. The remaining open work there is `0x637300`, `0x6372D0`, `0x61E9C0`, the optional swim-side `0x30000` query, and the `0x632A30` wrapper that decides when to invoke the path.
- **Fresh binary evidence (session 214):**
  - Added raw capture [0x6373B0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6373B0_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) to record the exact componentwise min/max behavior.
- **Test baseline (session 214):**
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowAabbMergeTests|FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowTerrainQueryMaskTests|FullyQualifiedName~WowAabbContainmentTests" --logger "console;verbosity=minimal"`
    - Passed (`13/13`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 213 — `0x631E70` projected query bounds are now pinned as a pure binary seam:**
  - Added pure [BuildTerrainQueryBounds(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowTerrainQueryBoundsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTerrainQueryBoundsTests.cs), which now pins the exact `0x631E70` projected AABB shape: `XY` min/max from `this+0xB0`, `Z` min at feet level, and `Z` max at `feet + this+0xB4`, plus the double-corner cache-fit shape when paired with `0x637350`.
  - Practical implication: the remaining native gap inside `0x631E70` is no longer the projected query-box layout feeding the cached-bounds gate. The open work is now the post-cache-miss expansion/merge transaction, optional swim-side query, and transform rewrite of `0xC4E534`.
- **Fresh binary evidence (session 213):**
  - Added raw capture [0x631E70_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x631E70_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) to record the exact projected query AABB and the two `0x637350` corner checks.
- **Test baseline (session 213):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowTerrainQueryMaskTests|FullyQualifiedName~WowAabbContainmentTests" --logger "console;verbosity=minimal"`
    - Passed (`11/11`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 212 — `0x6315F0` terrain-query mask is now pinned as a pure binary seam:**
  - Added pure [BuildTerrainQueryMask(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowTerrainQueryMaskTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTerrainQueryMaskTests.cs), which now pins the `0x5FA550` base-mask split, the strict `this+0x20 > 0x80DFE8` `0x30000` gate, the swim exclusion, and the two-bit `0x8000` augment from `0x6315F0`.
  - Practical implication: the remaining native gap inside `0x631E70` is no longer the query-mask math feeding `0x6721B0`. The open work is the rest of the merged-query builder transaction and the `0x632A30` wrapper that decides when to invoke it.
- **Fresh binary evidence (session 212):**
  - Added raw capture [0x6315F0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6315F0_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) to record the exact base-mask and augmentation gates `0x631E70` feeds into `0x6721B0`.
- **Test baseline (session 212):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTerrainQueryMaskTests|FullyQualifiedName~WowAabbContainmentTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - Passed (`17/17`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 211 — cached query-bounds containment gate is now pinned as a pure binary seam:**
  - Added pure [IsPointInsideAabbInclusive(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowAabbContainmentTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowAabbContainmentTests.cs), which now pins the inclusive min/max behavior on both bounds and the below-min / above-max rejection paths from `0x637350`.
  - Practical implication: the remaining native gap inside the unresolved `0x631E70` / `0x632A30` setup side is no longer the cached-query AABB reuse gate. The open work is the larger query-builder / selector-wrapper transaction around that gate.
- **Fresh binary evidence (session 211):**
  - Added raw capture [0x637350_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x637350_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) to record that `0x631E70` uses `0x637350` to decide whether the cached bounds at `0xC4E5A0` already contain both current and projected points before rebuilding the merged query.
- **Test baseline (session 211):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowAabbContainmentTests" --logger "console;verbosity=minimal"`
    - Passed (`34/34`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 210 — post-selector z-match gates are now pinned as pure binary seams:**
  - Added pure [HasSelectorCandidateWithNegativeDiagonalZ(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and [HasSelectorCandidateWithUnitZ(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorCandidateZMatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorCandidateZMatchTests.cs), which now pins the direct-return `0x635410` negative-diagonal match, the alternate-path `0x6353D0` unit-Z match, the binary epsilon window, and the bounded-candidate-count behavior.
  - Practical implication: the remaining native gap in `0x6351A0` is no longer these tiny post-selector buffer scans. The open work is the unresolved `0x632A30` / `0x631E70` setup/gating side of `0x632BA0` and the broader `0x6351A0` transaction around the selected index and paired payload.
- **Fresh binary evidence (session 210):**
  - Added raw captures [0x635410_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x635410_disasm.txt) and [0x6353D0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6353D0_disasm.txt), and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) to record that both helpers scan the local `0x10`-stride candidate buffer's `normal.z` field rather than any world-height field.
- **Test baseline (session 210):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests|FullyQualifiedName~WowSelectorCandidateZMatchTests" --logger "console;verbosity=minimal"`
    - Passed (`31/31`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 209 — selector direction ranking core is now pinned as a pure binary seam:**
  - Added pure [EvaluateSelectorDirectionRanking(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorDirectionRankingTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorDirectionRankingTests.cs), which now pins the `0x632BA0` chooser core’s dot-reject path, builder-reject path, evaluator-reject path, overwrite/append/swap behavior, and final `0x80DFEC` zero-clamp.
  - Practical implication: the selector chain is now pinned through both caller-side ranking bodies. The remaining native gap around `0x632BA0` is the unresolved setup/gating work (`0x632A30` / `0x631E70`) plus the downstream `0x6351A0` / `0x635410` selection gate, not the 5-direction quad-record ranking core itself.
- **Fresh binary evidence (session 209):**
  - Updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the `0x632BA0` section now explicitly records the production-DLL mirror for the second-half chooser core and also keeps the unresolved `0x632A30` / `0x631E70` setup gates explicit.
- **Test baseline (session 209):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - Passed (`27/27`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 208 — selector source ranking is now pinned as a pure binary seam:**
  - Added pure [EvaluateSelectorTriangleSourceRanking(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorSourceRankingTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceRankingTests.cs), which now pins the `0x632280` dot-reject path, builder-reject path, evaluator-reject path, overwrite path, and append-and-swap near-tie path against the binary `0x80DFEC` epsilon window.
  - Practical implication: the selector chain is now pinned through the first caller-side multi-source ranking body. The remaining native gap in this branch is the 5-direction chooser in `0x632BA0` and its handoff into `0x6351A0`, not the 4-source overwrite/append/swap loop inside `0x632280`.
- **Fresh binary evidence (session 208):**
  - Updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the `0x632280` section now explicitly records the production-DLL mirror: translated selector-triplet clip planes from `0x632460`, `0x632700` evaluation against the already-pinned record set, and the caller-visible overwrite/append/swap rules on the 5-slot best-candidate buffer.
- **Test baseline (session 208):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests" --logger "console;verbosity=minimal"`
    - Passed (`22/22`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 207 — selector quad-record builders are now pinned as a pure binary seam:**
  - Added pure [BuildSelectorCandidateQuadPlaneRecord(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorCandidateQuadPlaneRecordTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorCandidateQuadPlaneRecordTests.cs), which now pins the four oriented side planes emitted from the 4-byte selector ring, the translated source-plane anchor in slot 4, and the early-fail path when one side plane degenerates below the binary epsilon.
  - Practical implication: the selector chain is now pinned through both record-builder shapes consumed by the caller-side evaluator. The remaining native gap is the multi-record ranking path (`0x632280` / `0x632BA0`) and its handoff into `0x6351A0`, not the geometry builder inside `0x632F80`.
- **Fresh binary evidence (session 207):**
  - Added raw capture [0x632F80_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x632F80_disasm.txt), then updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) with the exact 4-selector ring walk, previous-point flip, and slot-4 source-plane anchor behavior.
- **Test baseline (session 207):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests" --logger "console;verbosity=minimal"`
    - Passed (`17/17`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 206 — selector record evaluation is now pinned as a pure binary seam:**
  - Added pure [ClipSelectorPointStripAgainstPlanePrefix(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and [EvaluateSelectorCandidateRecordSet(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorCandidateRecordSetTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorCandidateRecordSetTests.cs), which now pins the `0x631870` plane-prefix early-fail path, the `0x632700` dot-reject path, the clip-reject path, and the lowest-ratio record selection/update path.
  - Practical implication: the selector chain is now pinned through the first caller-side record evaluator. The remaining native gap is the record-builder/ranking path (`0x632F80` / `0x632280`) and its handoff into `0x6351A0`, not the per-record filter/clip/validate/update body inside `0x632700`.
- **Fresh binary evidence (session 206):**
  - Added raw captures [0x631870_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x631870_disasm.txt) and [0x632700_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x632700_disasm.txt), then updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) with the exact `0x34` record layout, local strip seeding, prefix clip order, and caller-best update rule.
- **Test baseline (session 206):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests" --logger "console;verbosity=minimal"`
    - Passed (`15/15`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 205 — selector candidate-plane records are now pinned as a pure binary seam:**
  - Added pure [BuildSelectorCandidatePlaneRecord(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorCandidatePlaneRecordTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorCandidatePlaneRecordTests.cs), which now pins the three oriented side planes emitted from the selector triangle, the translated source-plane anchor in slot 3, and the early-fail path when one side plane degenerates below the binary epsilon.
  - Practical implication: the selector chain is now pinned through the exact `0x632460` record builder. The remaining native gap is the caller-side evaluator/ranking path (`0x632700` / `0x632280`) and its handoff into `0x633720` / `0x635090`, not the per-record plane geometry itself.
- **Fresh binary evidence (session 205):**
  - Added raw captures [0x632460_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x632460_disasm.txt) and [0x637480_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x637480_disasm.txt), then updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) with the exact side-plane build, opposite-point flip, and translated source-plane anchor behavior.
- **Test baseline (session 205):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests" --logger "console;verbosity=minimal"`
    - Passed (`11/11`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 204 — selector candidate validation is now pinned as a pure binary seam:**
  - Added pure [EvaluateSelectorPlaneRatio(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [ClipSelectorPointStripAgainstPlane(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [ClipSelectorPointStripExcludingPlane(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), and [ValidateSelectorPointStripCandidate(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorCandidateValidationTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorCandidateValidationTests.cs), which now pins the `0x6329E0` ratio formula, the `0x6318C0` strip clipping output/plane-index tagging, the `0x632830` first-pass best-ratio update path, and the strict second-pass rejection path.
  - Practical implication: the pure selector chain is now pinned through the validator body itself. The remaining native gap is the caller-side candidate-record producer path (`0x632700` / `0x632280`) and its handoff into `0x633720` / `0x635090`, not the ratio/clip/rebuild math inside `0x632830`.
- **Fresh binary evidence (session 204):**
  - Added raw captures [0x6329E0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6329E0_disasm.txt), [0x632830_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x632830_disasm.txt), and [0x6318C0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6318C0_disasm.txt), then updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) with the exact strip-buffer shape and threshold logic.
- **Test baseline (session 204):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests" --logger "console;verbosity=minimal"`
    - Passed (`9/9`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 203 — selector neighborhood/table is now pinned as a pure binary seam:**
  - Added pure [BuildSelectorNeighborhood(...) in PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) plus the production-DLL export [BuildWoWSelectorNeighborhood(...) in PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp), with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added new deterministic coverage in [WowSelectorNeighborhoodTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorNeighborhoodTests.cs), which pins the exact 9-point layout and 32-byte selector table emitted by binary helper `0x631BE0`.
  - Practical implication: both upstream selector builders are now exact in the production DLL. The remaining selector-chain unknown is the candidate-validation/rebuild logic around `0x6329E0` / `0x632830` / `0x6318C0`, not the plane strip or neighborhood data they consume.
- **Fresh binary evidence (session 203):**
  - Added raw capture [0x631BE0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x631BE0_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) with the `0x631BE0` point/table builder.
- **Test baseline (session 203):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests" --logger "console;verbosity=minimal"`
    - Passed (`4/4`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 202 — selector support-plane strip is now pinned as a pure binary seam:**
  - Added pure [BuildSelectorSupportPlanes(...) in PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) plus the production-DLL export [BuildWoWSelectorSupportPlanes(...) in PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp), with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added new deterministic coverage in [WowSelectorSupportPlaneTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSupportPlaneTests.cs), which pins the exact 9-plane support strip emitted by binary helper `0x631440`: the `±X`, `±Y`, and `+Z` planes plus the four diagonal planes driven by `0x80DFE4 = 0.8796418905f` and `0x80DFE0 = 0.4756366014f`.
  - Practical implication: the next selector-chain unit can stop guessing at the support-plane layout and move one step deeper into `0x631BE0` / `0x632830` with the real binary strip already available in the production DLL.
- **Fresh binary evidence (session 202):**
  - Added raw capture [0x631440_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x631440_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) with the `0x631440` support-plane strip.
- **Test baseline (session 202):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests" --logger "console;verbosity=minimal"`
    - Passed (`2/2`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`16/16`)
- **Session 201 — frame-16 merged query proves the selector gap is earlier than the direct-pair gate:**
  - Promoted the selected-contact threshold/prism math into pure [EvaluateSelectedContactThresholdGate(...) in PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and exported it through [EvaluateWoWSelectedContactThresholdGate(...) in PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp), with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Tightened [UndercityUpperDoorContactTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs) so the packet-backed frame-16 merged-query scan is now a pinned regression, not an exploratory dump.
  - New deterministic result: the entire merged query contains zero contacts that satisfy the binary `0x633760 -> 0x6335D0` direct-pair gate under either the relaxed or standard thresholds. The remaining mismatch is therefore earlier in the selector-builder path (`0x632280` / `0x632830` / `0x6318C0`), not a missed good contact later in `0x633760`.
- **Fresh binary evidence (session 201):**
  - Added raw capture [0x632280_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x632280_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) with the newly confirmed `0x632280` four-entry source loop plus the `0x632830` / `0x6329E0` helper constraints.
- **Test baseline (session 201):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
    - Passed (`9/9`)
- **Session 200 — selected-contact threshold/prism trace proves frame-16 wall stays on the alternate path:**
  - Extended [GroundedWallResolutionTrace in PhysicsEngine.h](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.h), [ResolveGroundedWallContacts(...) in PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), and [EvaluateGroundedWallSelection(...) in PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) so the production DLL now records the selected contact’s threshold point, selected `normal.z`, current/projected `0x6335D0` prism inclusion, and the direct-pair outcome under both the relaxed and standard `0x633760` thresholds.
  - Added matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs) plus a new packet-backed regression in [UndercityUpperDoorContactTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs).
  - The new deterministic result tightens the next parity target: once the runtime has already selected WMO wall instance `0x00003B34` on frame 16, the projected `position + requestedMove` point is outside the expanded triangle prism, so that wall stays on the alternate `0x635090` path under both threshold modes. The remaining blocker is therefore earlier in the selector chain (`0x632BA0` / `0x632280`), not a threshold-mode guess inside `0x633760`.
- **Fresh binary evidence (session 200):**
  - Added raw captures [0x6351A0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6351A0_disasm.txt) and [0x632BA0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x632BA0_disasm.txt), and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) with the five-slot `0x632BA0` candidate-buffer note plus the projected-prism constraint on the frame-16 selected wall.
- **Test baseline (session 200):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
    - Passed (`8/8`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`7/7`)
- **Session 195 — shared grounded-wall transaction trace now runs through the production resolver:**
  - Added shared [ResolveGroundedWallContacts(...) in PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and [GroundedWallResolutionTrace in PhysicsEngine.h](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.h), then routed the grounded runtime wall lambda through that helper. The native export and the runtime now execute the same selected-contact and branch-resolution codepath.
  - Extended [EvaluateGroundedWallSelection(...) in PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) and the matching [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs) interop so deterministic tests can record state before/after, branch kind, merged/final wall normals, and horizontal-vs-final projected moves without a separate native tester project.
  - Updated [UndercityUpperDoorContactTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs) to pin the production-helper result. The critical new finding is that frame 16 does not select the stateful elevator support face the earlier managed reconstruction implied; the production resolver picks WMO wall instance `0x3B34` (`point=(1553.8352, 242.3765, -9.1597)`, `normal≈+X`, `oriented≈-X`) and stays on the horizontal branch.
- **Test baseline (session 195):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"`
    - Passed (`4/4`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`7/7`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysics|FullyQualifiedName~PhysicsReplayTests" --logger "console;verbosity=minimal"`
    - Passed (`55/56`, one skipped MPQ extraction test)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Session 194 — native grounded-wall trace seam added to the production DLL:**
  - Added [EvaluateGroundedWallSelection(...) in PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) plus the matching [GroundedWallSelectionTrace interop](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs). This export mirrors the current grounded blocker-selection path and returns the chosen contact, raw/oriented oppose scores, reorientation bit, and stateful `CheckWalkable` result from the real `Navigation.dll`.
  - Updated [UndercityUpperDoorContactTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs) so the frame-16 blocker-selection regression now queries that native trace directly instead of rebuilding the selector in C#.
  - Extended [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) with the newly confirmed `0x6351A0` branch shape: after `0x632BA0` and `0x633720`, the function either returns `0xC4E544[index]` directly, returns a zeroed pair with success, or falls through the `0x7C5DA0` / `0x6353D0` / `0x635090` alternate path.
- **Session 193 — grounded-wall state carried through replay and constrained to the selected-contact path:**
  - Updated [PhysicsBridge.h](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsBridge.h), [PhysicsEngine.h](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.h), [Physics.cs](/E:/repos/Westworld of Warcraft/Services/PathfindingService/Repository/Physics.cs), [ReplayEngine.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs), and [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs) so `groundedWallState` survives native step/replay boundaries. This keeps the packet-backed deterministic harness on the same frame-to-frame state path as the runtime instead of resetting the selected-contact walkability bit every step.
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so grounded wall resolution now applies `WoWCollision::CheckWalkable(...)` only to the selected primary contact, uses a local `0x635C00`-shaped Z-only correction on the stateful walkable branch, marks the bit after the non-walkable vertical branch, and reuses that state when later choosing grounded support contacts.
  - Added fresh binary structure notes to [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md): `0x6367B0` consumes one selected `0xC4E534[index]` contact and one paired `0xC4E544[index]` selector payload, which keeps the parity constraint explicit and rules out merged-query broadcast walkability.
  - Tightened [UndercityUpperDoorContactTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs) so the real packet-backed frame-16 query now asserts the remaining blocker-selection invariant directly: a statefully walkable horizontal contact exists, but it only becomes opposing after orienting the normal against the current collision position. That reorientation is still an inference pinned by deterministic evidence, not a named binary helper claim.
- **Session 192 — packet-backed Undercity frame-15 contact probe locked into deterministic coverage:**
  - Extended [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with `QueryTerrainAABBContacts(...)` and exposed the matching `TerrainAabbContact` interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs). This turns the merged `TestTerrainAABB` contact feed into a repeatable recorder on the production `Navigation.dll` instead of a one-off temp harness.
  - Added [UndercityUpperDoorContactTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs), which reconstructs the exact merged frame-15 query from the packet-backed upper-door replay and proves the query already contains the elevator deck support face at deck height with a signed downward normal and raw `walkable=0`.
  - The same deterministic probe also proves the pure `0x6334A0` helper only promotes that support face on its stateful path and that the same state would also promote many wall contacts in the same merged query if applied indiscriminately. That closes the tempting shortcut: do not blanket-replace `contact.walkable` with stateful `CheckWalkable(...)` across the merged query.
  - The immediate native blocker is therefore narrower and clearer than before: reproduce the binary-selected contact / grounded-wall-state path feeding `0x6334A0` (`0xC4E544` producer chain), then route the helper through that path. Do not spend another run on a broadcast helper hookup.
- **Session 191 — `TestTerrain` signed contact orientation aligned to `0x6721B0` + `0x637330`:**
  - Captured fresh binary evidence in [0x6721B0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6721B0_disasm.txt). The new note records the relevant `0x6721B0` behavior the static AABB path was still missing: `TestTerrain` copies matching `0x34` contact structs byte-for-byte from the spatial-query buffer, and the follow-on helper [0x637330](/E:/repos/Westworld of Warcraft/docs/physics/0x6721B0_disasm.txt) is a pure three-component negate. The client therefore preserves a signed contact normal and only flips it once, instead of upward-normalizing it.
  - Updated [SceneQuery.h](/E:/repos/Westworld of Warcraft/Exports/Navigation/SceneQuery.h) and [SceneQuery.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SceneQuery.cpp) so `TestTerrainAABB` now builds signed box-relative contacts through `BuildTerrainAABBContact(...)`: the stored contact normal faces the query box center, `planeDistance` matches that signed normal, and `walkable` now uses signed `normal.z >= cos(50)` instead of `abs(normal.z)`.
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) so the pure `0x6334A0` helper now consumes the signed contact normal/plane feed rather than the raw triangle winding, which matches the binary's post-`Vec3Negate` data flow.
  - Added new deterministic coverage in [TerrainAabbContactOrientationTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/TerrainAabbContactOrientationTests.cs) plus a pure orientation export in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs). The new tests pin the exact distinction that was missing before: support below the query box stays upward and walkable, geometry above the query box becomes downward and non-walkable, and wall contacts face the box center.
  - The signed-orientation change held the focused native slice and both live Durotar parity routes. This is the first session where the `TestTerrain` contact-orientation blocker itself moved forward cleanly, so the next native pass can retry runtime `0x6334A0` usage on top of a parity-safe signed contact feed instead of the old upward-flattened one.
- **Test baseline (session 194):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"`
    - Passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName=Navigation.Physics.Tests.PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"`
    - Passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests" --logger "console;verbosity=minimal"`
    - Passed (`7/7`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=minimal"`
    - Passed (`4/4`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`29/29`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - Passed (`9/9`)
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround" --logger "console;verbosity=minimal"`
    - Passed (`8/8`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`38/38`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 194):**
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsBridge.h`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Services/PathfindingService/Repository/Physics.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/SceneQuery.h`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/TerrainAabbContactOrientationTests.cs`
  - `docs/physics/0x6721B0_disasm.txt`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep the new native grounded-wall trace seam frozen, then extend it one level deeper so deterministic tests can also capture the paired `0xC4E544` selector payload and which `0x6351A0` branch produced it before the next runtime behavior edit.
- **Next command:** `py -c "from capstone import *; import pathlib; code=pathlib.Path(r'D:/World of Warcraft/WoW.exe').read_bytes(); md=Cs(CS_ARCH_X86, CS_MODE_32); start=0x635090; data=code[start-0x400000:start-0x400000+1024]; [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') for i in md.disasm(data, start)]"`
- **Session 190 — `0x6334A0` `CheckWalkable` semantics captured and locked in deterministic coverage:**
  - Captured fresh binary evidence in [0x6334A0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6334A0_disasm.txt). The new note includes the full `0x6334A0` body plus the two supporting helper findings that matter for parity: `0x6333D0` checks the current contact plane against the four top-footprint corners with `1/720`, and `0x6335D0` accepts the current position only when it sits inside all three triangle edge planes with `1/12`.
  - Extended [SceneQuery.h](/E:/repos/Westworld of Warcraft/Exports/Navigation/SceneQuery.h) and [SceneQuery.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SceneQuery.cpp) so `TestTerrainAABB` contacts now preserve the raw triangle vertices, raw plane normal, and plane distance the binary helper actually reasons about instead of collapsing everything down to a single upward-facing `walkable` bit.
  - Added a binary-backed pure helper in [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), exposed it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) / [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs), and pinned the rule in new [WowCheckWalkableTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowCheckWalkableTests.cs). The deterministic coverage now locks the strict signed-normal thresholds, the top-corner touch rule, and the `0x04000000` consumed-flag behavior.
  - Important: a direct runtime hookup of that helper into the current grounded wall resolver was attempted and immediately regressed both live Durotar parity routes. That hookup was reverted before handoff. This session therefore ships the binary evidence, raw-contact plumbing, and deterministic helper tests only, while deliberately leaving the live grounded runtime unchanged until the `TestTerrain` contact-orientation / normal-flip path itself is brought into parity.
- **Test baseline (session 190):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround" --logger "console;verbosity=minimal"`
    - Passed (`5/5`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 190):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/SceneQuery.h`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowCheckWalkableTests.cs`
  - `docs/physics/0x6334A0_disasm.txt`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep the new `0x6334A0` helper/tests frozen, then align the `TestTerrain` contact-orientation / `Vec3Negate` path before routing that helper into the grounded runtime. Only after that should the next native pass return to the still-open `0x636100` branch-gate helper.
- **Next command:** `rg -n "637330|Vec3Negate|0x6334A0|0x6721B0" docs/physics/0x6367B0_disasm.txt docs/physics/wow_exe_decompilation.md -S`
- **Session 189 — native top-level CollisionStep branch order aligned to `0x633840`:**
  - Captured fresh binary evidence in [0x633840_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x633840_disasm.txt). The relevant top-level branch order is explicit: `0x633A29` / `0x633A4C` checks the airborne helper first (`test ah, 0x20`), `0x633B5E` checks swimming second (`test eax, 0x200000`), and the grounded branch does not start until `0x633C7B`.
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so `StepV2` now preserves that same precedence. When airborne flags and `MOVEFLAG_SWIMMING` overlap on the same frame, BG now takes the airborne path instead of incorrectly routing through `ProcessSwimMovement`.
  - Added deterministic coverage in [FrameAheadIntegrationTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/FrameAheadIntegrationTests.cs): `AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround` proves a dry-ground `FALLINGFAR | SWIMMING` frame descends like pure airborne motion and clears `MOVEFLAG_SWIMMING` in the output.
  - The focused proof set held after the rebuild: native `Navigation.dll`, the local `Navigation.Physics.Tests` build, the new precedence test plus existing jump/swim regressions, and the live [MovementParityTests.cs](/E:/repos/Westworld of Warcraft/Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs) redirect route all passed unchanged.
  - This cleans up one real top-level mismatch without pretending the grounded helper is solved. The remaining native blocker is still the grounded post-`TestTerrain` sequence: current BG logic still simplifies `0x6334A0` and `0x636100`, which is where the live Durotar turn-start route still diverges.
- **Test baseline (session 189):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~FrameAheadIntegrationTests.JumpArc_FlatGround_PeakHeightMatchesPhysics|FullyQualifiedName~PhysicsReplayTests.SwimForward_FrameByFrame_PositionMatchesRecording" --logger "console;verbosity=minimal"`
    - Passed (`3/3`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 189):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/FrameAheadIntegrationTests.cs`
  - `docs/physics/0x633840_disasm.txt`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep the `0x633840` branch precedence frozen, then move to the still-open grounded parity blocker: disassemble `0x6334A0` `CheckWalkable`, replace the current fixed walkability simplification, and only then revisit the unresolved `0x636100` branch-gate helper.
- **Next command:** `py -c "from capstone import *; f=open(r'D:/World of Warcraft/WoW.exe','rb'); f.seek(0x6334A0-0x400000); code=f.read(768); md=Cs(CS_ARCH_X86, CS_MODE_32); [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') or (i.address >= 0x633560 and i.mnemonic in ('ret','retn') and (_ for _ in ()).throw(SystemExit)) for i in md.disasm(code, 0x6334A0)]"`
- **Session 188 — managed SET_FACING packet path corrected to match WoW.exe; native collision audit surfaced the next real blocker:**
  - Re-audited the managed facing send path against `WoW.exe` instead of the older heuristic notes. Binary evidence from `0x60E1EA` shows `MSG_MOVE_SET_FACING` is gated by the float at `0x80C408`, which reads as `0.1f`, and the send path falls directly into the movement send helper without a synthetic `MSG_MOVE_HEARTBEAT` before the facing packet.
  - Updated [MovementController.cs](/E:/repos/Westworld of Warcraft/Exports/WoWSharpClient/Movement/MovementController.cs) so `SendFacingUpdate(...)` now emits only `MSG_MOVE_SET_FACING`, records the opcode in the frame diagnostics, and keeps `_lastPacketTime` / `_lastPacketPosition` in sync with the actual sent packet.
  - Updated [WoWSharpObjectManager.Movement.cs](/E:/repos/Westworld of Warcraft/Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs) so local facing still updates on any real delta, but explicit `MSG_MOVE_SET_FACING` sends are now gated by the binary-backed `0.1 rad` threshold instead of the prior `0.02f` / `0.20f` split heuristics.
  - Tightened deterministic coverage in [MovementControllerTests.cs](/E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs) and [ObjectManagerWorldSessionTests.cs](/E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs) to pin the new semantics: standing and post-move facing updates now send only `MSG_MOVE_SET_FACING`, and a sub-threshold in-motion delta (`0.08 rad`) stays local-only.
  - The live forced-turn Durotar parity route remains the best proof bundle for this managed slice. `Parity_Durotar_RoadPath_Redirect` passed unchanged, and `Parity_Durotar_RoadPath_TurnStart` passed on rerun with the shared FG/BG `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD` opening pair intact. The first rerun missed the stop-edge bound by `9ms`, which reinforces that the remaining blocker is native route-grounding drift (`FALLINGFAR` churn / Z bounce), not packet-ordering drift.
  - Parallel binary audit for the next native slice confirmed the current high-signal blockers: [CollisionStepWoW](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) still only implements the grounded path even though `0x633840` branches falling first, then swimming, then grounded; `0x6334A0` `CheckWalkable` is more complex than the current fixed `normal.z >= 0.6428` gate; and the current `0x636100` driver comment in native code still admits an unsupported `.z > 0.01` equivalence heuristic.
- **Test baseline (session 188):**
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.SendMovementStartFacingUpdate_SendsSetFacingOnly|FullyQualifiedName~MovementControllerTests.SendFacingUpdate_StandingStill_SendsSetFacingOnly|FullyQualifiedName~MovementControllerTests.SendFacingUpdate_AfterMovement_SendsSetFacingOnly|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_IdleInControl_SendsSetFacingBeforeForwardIntent|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_AlreadyMovingForward_SendsSetFacingOnRedirect|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_AlreadyMovingForward_SubThresholdFacingChange_NoSetFacingPacket|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"`
    - Passed (`7/7`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - First rerun failed at stop-edge delta `609ms`; immediate rerun passed (`1/1`)
- **Files changed (session 188):**
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- **Next priorities:** keep the managed facing send path frozen at the binary-backed `0.1 rad` rule, then return to the native parity blocker exposed by the same live route: implement the real `0x633840` top-level branch order and remove the remaining unsupported grounded-helper heuristics around `0x636100` / `0x6334A0`.
- **Next command:** `py -c "from capstone import *; f=open(r'D:/World of Warcraft/WoW.exe','rb'); f.seek(0x633840-0x400000); code=f.read(2048); md=Cs(CS_ARCH_X86, CS_MODE_32); [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') or (i.mnemonic in ('ret','retn') and (_ for _ in ()).throw(SystemExit)) for i in md.disasm(code, 0x633840)]"`
- **Session 176 — packet-backed controller cadence aligned to FG traces; compact underground/elevator regressions added:**
  - Captured three fresh PacketLogger-backed FG recordings into the canonical repo corpus with the automated recording path: [Urgzuga_Durotar_2026-03-25_03-07-08.json](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/Recordings/Urgzuga_Durotar_2026-03-25_03-07-08.json), [Urgzuga_Undercity_2026-03-25_10-00-52.json](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/Recordings/Urgzuga_Undercity_2026-03-25_10-00-52.json), and [Urgzuga_Undercity_2026-03-25_10-01-09.json](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/Recordings/Urgzuga_Undercity_2026-03-25_10-01-09.json) plus `.bin` sidecars. These now provide compact packet-backed proof for flat-ground cadence, underground lower-route seating, and the west Undercity elevator up-ride.
  - Tightened [MovementControllerRecordedFrameTests.cs](/E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Movement/MovementControllerRecordedFrameTests.cs) so packet parity only selects clean grounded forward segments with a real stop frame, adds a synthetic preroll when the capture starts mid-run, and executes the stop transition. The recorded-frame opcode parity harness now selects the straight Durotar packet-backed run and proves `MSG_MOVE_START_FORWARD` / heartbeat / `MSG_MOVE_STOP` distribution against real FG packets instead of deferring for missing data.
  - Updated [MovementController.cs](/E:/repos/Westworld of Warcraft/Exports/WoWSharpClient/Movement/MovementController.cs) heartbeat cadence from the stale 100ms assumption to the packet-backed FG cadence of ~500ms while moving. Narrow controller and controller-physics timing tests were updated to match the new evidence and stayed green.
  - Added fast packet-backed replay regressions in [PhysicsReplayTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs) for the new Durotar flat run plus Undercity lower-route and elevator-up captures. The new Undercity checks explicitly assert the replay remains underground on the lower route and reaches the upper deck after the elevator ride.
- **Test baseline (session 176):**
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"`
    - Passed (`45/45`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysicsTests.Forward_FlatTerrain_PacketTimingAndPositionDeltas|FullyQualifiedName~MovementControllerPhysicsTests.HeartbeatInterval_500ms" --logger "console;verbosity=minimal"`
    - Passed (`2/2`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedFlatRun_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityLowerRoute_ReplayRemainsUnderground|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=detailed"`
    - Passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`30/30`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementScenarioRunnerTests|FullyQualifiedName~ObjectManagerMovementTests|FullyQualifiedName~MovementRecorderTransportHelperTests|FullyQualifiedName~PacketLoggerBinaryAuditTests" --logger "console;verbosity=minimal"`
    - Passed (`23/23`)
- **Files changed (session 176):**
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Services/ForegroundBotRunner/MovementScenarioRunner.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Movement.cs`
  - `Tools/RecordingMaintenance/Program.cs`
  - `Tests/ForegroundBotRunner.Tests/MovementScenarioRunnerTests.cs`
  - `Tests/ForegroundBotRunner.Tests/ObjectManagerMovementTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerRecordedFrameTests.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/TestConstants.cs`
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
  - `Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs`
  - `docs/TASKS.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
- **Next priorities:** use the new compact packet-backed corpus as the fast proof set, keep the 500ms controller cadence locked unless new packet traces contradict it, and move back onto the remaining real native blocker: the unresolved grounded post-`TestTerrain` wall/corner helper (`0x6367B0` plus `0x635C00` / `0x635D80`) with verified wall fixtures rather than synthetic heuristics.
- **Next command:** `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~FrameByFramePhysicsTests.StormwindCity_WalkIntoWall_Blocked|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=detailed"`
- **Branch:** `main`
- **Session 163 — moving-base query identity aligned across capsule and AABB collision paths:**
  - Updated [SceneQuery.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SceneQuery.cpp) so every remaining dynamic-object branch in `SweepCapsule` now forwards stable runtime instance IDs from `DynamicObjectRegistry` instead of synthesizing `0x80000000 | triangleIndex`. That keeps overlap, penetration, and swept capsule hits on elevators and doors aligned with the moving-base support token already emitted by the grounded AABB support path.
  - Added reusable Undercity elevator support-frame setup plus [ElevatorScenarioTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs) coverage for `UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken`. The new regression proves a real frame (`912`) reports the same moving-base runtime ID through both `StepPhysicsV2` and `SweepCapsule`.
  - Re-scanned `WoW.exe` disassembly windows `0x618C30..0x618D60` and `0x633840..0x6339C0`. The binary still reinforces transport-local persistence plus world-space collision only; no static terrain-triangle token cache surfaced.
  - Restarted the host-side [PathfindingService.exe](/E:/repos/Westworld of Warcraft/Bot/Release/net8.0/PathfindingService.exe) because this slice changed shared native navigation code. The live service is PID `41884`, [pathfinding_status.json](/E:/repos/Westworld of Warcraft/Bot/Release/net8.0/pathfinding_status.json) reports `IsReady=true` with maps `0/1/389`, and `127.0.0.1:5001` is reachable. I left Docker untouched because the active engine is still Linux-only and there was no running Windows `pathfinding-service` container to refresh.
  - Repo-scoped process inspection after validation returned no lingering repo-scoped `dotnet.exe` or `testhost.exe`.
- **Test baseline (session 163):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore`
    - Succeeded (existing warnings only)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken" --logger "console;verbosity=normal"`
    - Passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UndercityElevatorTransportFrame_ReportsDynamicSupportToken|FullyQualifiedName~UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken|FullyQualifiedName~UndercityElevatorReplay_TransportAverageStaysWithinParityTarget|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"`
    - Passed (`5/5`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementControllerPhysics" -v n`
    - Passed (`29/29`)
- **Files changed (session 163):**
  - `Exports/Navigation/SceneQuery.cpp`
  - `Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep moving-base identity coherent across any new collision or export path, but do not reintroduce static terrain-token caching without new binary evidence. The walkable-triangle-preserving waypoint smoothing note stays deferred behind the higher-priority bot behavior and combat work.
- **Session 162 — melee engage timing improved again; live mining advanced to a later combat stall:**
  - Updated [CombatRotationTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/CombatRotationTask.cs) so shared melee engage now matches the older sequence path more closely: one grounded face/settle tick before `StartMeleeAttack()`, plus airborne suppression until the bot has landed and re-faced the target.
  - Removed the old shared-task aggressor chase-timeout fallback from [CombatRotationTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/CombatRotationTask.cs). In the current outdoor mining repro that blind auto-swing was firing on ledge fights and pinning the bot in stationary combat instead of letting chase/path recovery continue.
  - Expanded [CombatRotationTaskTests.cs](/E:/repos/Westworld of Warcraft/Tests/BotRunner.Tests/Combat/CombatRotationTaskTests.cs) to cover the new engage timing and the removed blind-chase regression: in-range melee primes before attacking, airborne melee waits for a grounded face tick, and out-of-range aggressors no longer auto-swing just because a chase timeout elapsed.
  - Re-ran the BG-only mining slice twice. The live blocker moved materially from candidate `7/15` to `4/15`, then to `3/15`. The old cliff/facing signature mostly collapsed: latest counts were `BADFACING=1`, `NOTINRANGE=0`, `NullWaypoint=4`, `AirborneBlocked=321`, `HeroicStrike=79`. The current failure is a later stationary combat loop around `(-443.9,-4829.0,36.5)` while `GatheringRouteTask` is paused on candidate `3/15`.
  - Explicit PID inspection again confirmed there were no leftover host `WoWStateManager.exe`, `BackgroundBotRunner.exe`, `PathfindingService.exe`, or `WoW.exe` processes after the reruns. `PathfindingService` code was not changed or redeployed in this pass.
- **Test baseline (session 162):**
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
    - Passed (`89/89`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
    - Passed (`90/90`) after adding the blind-chase regression
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`
    - Failed after `5m16s` with the blocker shifted to candidate `4/15` (`BADFACING=1`, `NullWaypoint=10`, `AirborneBlocked=412`, `HeroicStrike=95`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`
    - Failed after `5m15s` with the blocker shifted again to candidate `3/15` (`BADFACING=1`, `NullWaypoint=4`, `AirborneBlocked=321`, `HeroicStrike=79`)
  - `Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and ($targets -contains $_.ExecutablePath) }`
    - Returned no matching host bot/runtime processes
- **Files changed (session 162):**
  - `Exports/BotRunner/Tasks/CombatRotationTask.cs`
  - `Tests/BotRunner.Tests/Combat/CombatRotationTaskTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `docs/TASKS.md`
- **Next priorities:** keep `BBR-PAR-002` focused on the later candidate-3 stationary combat loop rather than the old candidate-7 cliff/facing issue; the next high-signal check is the target/chase ownership window around `(-443.9,-4829.0,36.5)`. Keep the walkable-triangle-preserving smoothing follow-up deferred behind these higher-priority combat/movement fixes, and continue leaving `PathfindingService` undeployed unless its code changes.
- **Session 161 — task-level melee chase parity tightened; live mining narrowed to a cliff recovery window:**
  - Added default physics-contact accessors to [IObjectManager.cs](/E:/repos/Westworld of Warcraft/Exports/GameData.Core/Interfaces/IObjectManager.cs) so BotRunner task code can consume BG wall-hit / blocked-fraction telemetry without adding a new direct dependency on `WoWSharpClient`.
  - Updated [BotTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/BotTask.cs) to pass that wall-contact data into `NavigationPath`, and updated [CombatRotationTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/CombatRotationTask.cs) so melee chase uses 2D close-range checks plus `allowDirectFallback: true`, matching the more resilient sequence-based melee chase behavior.
  - Added focused coverage in [CombatRotationTaskTests.cs](/E:/repos/Westworld of Warcraft/Tests/BotRunner.Tests/Combat/CombatRotationTaskTests.cs) for the two live regressions this exposed: small vertical-step melee range and no-route melee direct fallback.
  - Re-ran the BG-only mining slice. The earlier close-range no-route stall improved, but the test still times out on candidate `7/15` during a steep vertical/cliff combat recovery window: repeated `MoveToward blocked by IsPlayerAirborne`, `GetNextWaypoint returned null` at `(-744.6,-4743.0,22.1)` versus target `(-748.0,-4748.5,31.1)`, followed by `SMSG_ATTACKSWING_BADFACING` / `SMSG_ATTACKSWING_NOTINRANGE`.
  - Explicit PID inspection confirmed there were no leftover host `WoWStateManager.exe`, `BackgroundBotRunner.exe`, `PathfindingService.exe`, or `WoW.exe` processes after the rerun. `PathfindingService` code was not changed or redeployed in this pass.
- **Test baseline (session 161):**
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
    - Passed (`87/87`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`
    - Failed after `5m16s` (`NullWaypoint=65`, `AirborneBlocked=768`, `BADFACING=16`, `NOTINRANGE=7`, `HeroicStrike=36`)
  - `Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and ($targets -contains $_.ExecutablePath) }`
    - Returned no matching host bot/runtime processes
- **Files changed (session 161):**
  - `Exports/GameData.Core/Interfaces/IObjectManager.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/CombatRotationTask.cs`
  - `Tests/BotRunner.Tests/Combat/CombatRotationTaskTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `docs/TASKS.md`
- **Next priorities:** keep `BBR-PAR-002` on the candidate-7 cliff/vertical combat recovery window, likely by tightening melee chase/facing behavior during airborne-to-ground transitions and reducing the null-waypoint recovery delay; keep the walkable-triangle-preserving smoothing follow-up deferred behind those higher-priority combat/movement fixes, and continue leaving `PathfindingService` undeployed unless its code changes
- **Session 160 — BG-only live fixture split for BG-authoritative suites:**
  - Added `BgOnly.settings.json`, `BgOnlyBotFixture`, and `BgOnlyValidationCollection` under `Tests/BotRunner.Tests/LiveValidation/` so BG-authoritative live suites can run against a one-bot StateManager config instead of always launching an FG client.
  - Moved the explicitly BG-only or BG-first suites onto that collection: `CraftingProfessionTests`, `VendorBuySellTests`, `StarterQuestTests`, `MapTransitionTests`, `NavigationTests`, and the BG-authoritative `GatheringProfessionTests`.
  - Added deterministic coverage in `BgOnlyBotFixtureConfigurationTests` to verify the BG-only settings seed only the background role; `PathfindingService` code/container state was not changed or redeployed in this pass.
- **Test baseline (session 160):**
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded (existing warnings; existing `dumpbin` applocal warning still present)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BgOnlyBotFixtureConfigurationTests|FullyQualifiedName~LiveBotFixtureDiagnosticsTests" --logger "console;verbosity=minimal"`
    - Passed (`2/2`)
- **Files changed (session 160):**
  - `Tests/BotRunner.Tests/LiveValidation/BgOnlyBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BgOnlyValidationCollection.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BgOnlyBotFixtureConfigurationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Settings/BgOnly.settings.json`
  - `Tests/BotRunner.Tests/LiveValidation/CraftingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MapTransitionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/NavigationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/VendorBuySellTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `docs/TASKS.md`
- **Next priorities:** use the BG-only gathering fixture to re-run `BBR-PAR-002` mining/herbalism slices, then continue the BG movement/controller parity work without touching `PathfindingService` deployment unless its code changes
- **Session 159 — gathering route combat ownership tightened; deferred walkable-tile smoothing note added:**
  - Updated [GatheringRouteTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/GatheringRouteTask.cs) so incidental combat pauses the active gather-route task, clears navigation, resets the task-local timeout window, and resumes the current candidate instead of dropping the task off the stack.
  - Added focused coverage in [GatheringRouteTaskTests.cs](/E:/repos/Westworld of Warcraft/Tests/BotRunner.Tests/Combat/GatheringRouteTaskTests.cs) for the near-timeout combat pause/resume case.
  - Re-ran the live mining slice against the Docker-hosted vmangos stack; the test still hangs, but the next precision bug is now explicit in the logs: waypoint following/smoothing is curving off the walkable corridor and redirecting across unwalkable terrain before the child `PathfindingService` connection is lost.
  - Recorded that walkable-triangle-preserving smoothing follow-up in [Services/PathfindingService/TASKS.md](/E:/repos/Westworld of Warcraft/Services/PathfindingService/TASKS.md) as deferred until after the current priorities, and cleaned the repo-scoped leftover `WoW.exe` from the aborted live run.
- **Test baseline (session 159):**
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringRouteTaskTests"`
    - Passed (`4/4`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`
    - Aborted after hang (`GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases`; latest `GatheringProfessionTests.log` shows route drift off walkable terrain and later `PathfindingService process exited (code -1)`)
  - `Get-Process | Where-Object { $_.ProcessName -in @('WoW','WoWStateManager','BackgroundBotRunner','PathfindingService') }`
    - Confirmed clean after explicit orphan cleanup
- **Files changed (session 159):**
  - `Exports/BotRunner/Tasks/GatheringRouteTask.cs`
  - `Tests/BotRunner.Tests/Combat/GatheringRouteTaskTests.cs`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `docs/TASKS.md`
- **Next priorities:** keep pushing `BBR-PAR-002` first, leave `PathfindingService` undeployed unless its code changes, and keep the walkable-tile-preserving smoothing fix queued behind the existing higher-priority work
- **Session 158 — removed the duplicate vmangos DB container and switched the stack to the host MySQL install:**
  - Updated [docker-compose.vmangos-linux.yml](/E:/repos/Westworld of Warcraft/docker-compose.vmangos-linux.yml) to remove the compose-managed `vmangos-database` service and repoint `vmangos-realmd` / `vmangos-mangosd` at `host.docker.internal:3306`.
  - Updated [start-realmd.sh](/E:/repos/Westworld of Warcraft/docker/linux/vmangos/start-realmd.sh) and [start-mangosd.sh](/E:/repos/Westworld of Warcraft/docker/linux/vmangos/start-mangosd.sh) so the Linux server containers default to the host DB path, with an explicit host-gateway mapping.
  - Extended [Sync-MigrationMarkers.ps1](/E:/repos/Westworld of Warcraft/docker/linux/vmangos/Sync-MigrationMarkers.ps1) to support host MySQL mode, then applied the missing world migrations to the existing `D:\MaNGOS\mysql5` database (`mangos.migrations` `988 -> 1006`) before restarting the vmangos containers.
  - Removed the duplicate `westworldofwarcraft-vmangos-database-1` container and refreshed [docs/DOCKER_STACK.md](/E:/repos/Westworld of Warcraft/docs/DOCKER_STACK.md) to document the single-DB topology.
- **Test baseline (session 158):**
  - `docker rm -f westworldofwarcraft-vmangos-database-1`
    - Succeeded
  - `Start-Process D:\MaNGOS\mysql5\bin\mysqld.exe --console --max_allowed_packet=128M`
    - Succeeded (`PID 29236`)
  - `D:\MaNGOS\mysql5\bin\mysql.exe -h 127.0.0.1 -uroot -proot -e "SHOW DATABASES; SELECT COUNT(*) AS allowed_clients FROM realmd.allowed_clients; SELECT COUNT(*) AS mangos_migrations FROM mangos.migrations;"`
    - Succeeded (`allowed_clients=60`, `mangos.migrations=988` before sync)
  - `powershell -ExecutionPolicy Bypass -File .\docker\linux\vmangos\Sync-MigrationMarkers.ps1 -FetchOrigin`
    - Succeeded (`mangos.migrations=1006`)
  - `docker compose -f .\docker-compose.vmangos-linux.yml config`
    - Succeeded
  - `docker compose -f .\docker-compose.vmangos-linux.yml up -d vmangos-realmd vmangos-mangosd`
    - Succeeded
  - `docker ps --filter name=westworldofwarcraft-vmangos- --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"`
    - `vmangos-realmd` and `vmangos-mangosd` healthy; no `vmangos-database` container remains
  - `docker logs --tail 120 westworldofwarcraft-vmangos-realmd-1`
    - Succeeded (`Database: host.docker.internal;3306;root;*;realmd`, `Added realm "Lightbringer"`)
  - `docker logs --tail 160 westworldofwarcraft-vmangos-mangosd-1`
    - Succeeded (`World initialized.`, SOAP bound)
  - `Test-NetConnection -ComputerName 127.0.0.1 -Port 3306,3724,7878,8085`
    - All four ports reachable from the host
- **Files changed (session 158):**
  - `docker-compose.vmangos-linux.yml`
  - `docker/linux/vmangos/Sync-MigrationMarkers.ps1`
  - `docker/linux/vmangos/start-mangosd.sh`
  - `docker/linux/vmangos/start-realmd.sh`
  - `docs/DOCKER_STACK.md`
  - `docs/TASKS.md`
- **Next priorities:** keep the single host DB topology, leave the duplicate DB container removed, and continue the split deployment work with host-side `WoWStateManager` plus containerized vmangos/pathfinding as needed
- **Session 157 — removed the `WoWStateManager` container path and made host-side orchestration explicit:**
  - Updated [docker-compose.windows.yml](/E:/repos/Westworld of Warcraft/docker-compose.windows.yml) to remove the `wow-state-manager` service entirely; `WoWStateManager` must remain host-side so it can launch local `WoW.exe` clients.
  - Repointed the optional `background-bot-runner` container at the host-side `WoWStateManager` listener via `WWOW_STATE_MANAGER_HOST` / `WWOW_STATE_MANAGER_PORT` (default `host.docker.internal:5002`) instead of depending on a `wow-state-manager` container.
  - Refreshed [docs/DOCKER_STACK.md](/E:/repos/Westworld of Warcraft/docs/DOCKER_STACK.md), [Services/WoWStateManager/TASKS.md](/E:/repos/Westworld of Warcraft/Services/WoWStateManager/TASKS.md), and [Services/BackgroundBotRunner/TASKS.md](/E:/repos/Westworld of Warcraft/Services/BackgroundBotRunner/TASKS.md) to document the corrected architecture: containerize server-side pieces, keep `WoWStateManager` on the host.
- **Test baseline (session 157):**
  - `docker compose -f .\docker-compose.windows.yml config`
    - Succeeded
  - `docker compose -f .\docker-compose.windows.yml --profile bgbot config`
    - Succeeded (`CharacterStateListener__IpAddress=host.docker.internal`)
  - `docker ps -a --filter name=westworldofwarcraft-wow-state-manager --format "table {{.Names}}\t{{.Status}}"`
    - Returned no containers to remove
  - `Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -eq 27628 }`
    - Confirmed the live `WoWStateManager.exe` host process remains the active orchestration path
- **Files changed (session 157):**
  - `docker-compose.windows.yml`
  - `docs/DOCKER_STACK.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `docs/TASKS.md`
- **Next priorities:** keep `WoWStateManager` host-side, leave vmangos/pathfinding containerized where possible, and continue the next BG parity slice against that split deployment
- **Session 156 — host-side PathfindingService + idle WoWStateManager brought up against the live Docker vmangos stack:**
  - Started the published host-side [PathfindingService.exe](/E:/repos/Westworld of Warcraft/Bot/Release/net8.0/PathfindingService.exe) because the current Docker engine is Linux-only and the service still depends on the Windows-native `Navigation.dll`.
  - Added [StateManagerSettings.Idle.json](/E:/repos/Westworld of Warcraft/Services/WoWStateManager/Settings/StateManagerSettings.Idle.json) and launched [WoWStateManager.exe](/E:/repos/Westworld of Warcraft/Bot/Release/net8.0/WoWStateManager.exe) with `WWOW_SETTINGS_OVERRIDE` pointing at that empty settings file plus `MangosServer__AutoLaunch=false`, so it stays idle while still binding its listener ports.
  - Updated [docs/DOCKER_STACK.md](/E:/repos/Westworld of Warcraft/docs/DOCKER_STACK.md) with the host-side fallback commands required while service containerization remains blocked on Windows-only runtime dependencies.
- **Test baseline (session 156):**
  - `Start-Process Bot\Release\net8.0\PathfindingService.exe`
    - Succeeded (`PID 33144`)
  - `Get-NetTCPConnection -LocalPort 5001 -State Listen`
    - Succeeded (`PID 33144` listening on `127.0.0.1:5001`)
  - `Get-Content Bot\Release\net8.0\pathfinding_status.json`
    - Succeeded (`IsReady=true`, `LoadedMaps={0,1,389}`, `ProcessId=33144`)
  - `Get-Content logs\service-host\pathfindingservice.stdout.log -Tail 80`
    - Succeeded (`Navigation.dll` loaded, native preload completed)
  - `Start-Process Bot\Release\net8.0\WoWStateManager.exe` with `MangosServer__AutoLaunch=false` and `WWOW_SETTINGS_OVERRIDE=Services\WoWStateManager\Settings\StateManagerSettings.Idle.json`
    - Succeeded (`PID 27628`)
  - `Get-NetTCPConnection -LocalPort 5002,8088 -State Listen`
    - Succeeded (`PID 27628` listening on both ports)
  - `Get-Content logs\service-host\wowstatemanager.stdout.log -Tail 120`
    - Succeeded (`CharacterSettings count: 0`, `MaNGOS auto-launch disabled.`, `PathfindingService is READY`)
  - `Get-CimInstance Win32_Process ... BackgroundBotRunner.exe|ForegroundBotRunner.exe|WoW.exe`
    - Succeeded (no bot runner or `WoW.exe` children launched by idle `WoWStateManager`)
- **Files changed (session 156):**
  - `Services/WoWStateManager/Settings/StateManagerSettings.Idle.json`
  - `Services/PathfindingService/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `docs/DOCKER_STACK.md`
  - `docs/TASKS.md`
- **Next priorities:** use the now-running host-side `PathfindingService` and idle `WoWStateManager` with the live Docker vmangos stack for the next BG parity slice, then come back to true service containerization once Docker is back in Windows-container mode
- **Session 155 — Linux vmangos auth/world/db stack deployed on the local Docker engine:**
  - Repointed [docker-compose.vmangos-linux.yml](/E:/repos/Westworld of Warcraft/docker-compose.vmangos-linux.yml) away from the unrelated `gameserver-mariadb` container and added a compose-managed `vmangos-database` service backed by the existing `westworldofwarcraft_vmangos-database` volume.
  - Updated the Linux vmangos startup scripts in [docker/linux/vmangos/start-realmd.sh](/E:/repos/Westworld of Warcraft/docker/linux/vmangos/start-realmd.sh) and [docker/linux/vmangos/start-mangosd.sh](/E:/repos/Westworld of Warcraft/docker/linux/vmangos/start-mangosd.sh) so `realmd` / `mangosd` default to the local `vmangos-database` service instead of the legacy external MariaDB path.
  - Confirmed the persisted DB volume already contains the correct modern vmangos schema (`realmd.allowed_clients` present, March 2026 world migrations present), kept the stable `root/root` credentials expected by the local server config, and let the DB image complete its own update pass.
  - Refreshed [docs/DOCKER_STACK.md](/E:/repos/Westworld of Warcraft/docs/DOCKER_STACK.md) and [docker/linux/vmangos/Sync-MigrationMarkers.ps1](/E:/repos/Westworld of Warcraft/docker/linux/vmangos/Sync-MigrationMarkers.ps1) so the Linux stack documentation and migration helper point at the compose-managed vmangos DB container by default.
- **Test baseline (session 155):**
  - `docker compose -f .\docker-compose.vmangos-linux.yml config`
    - Succeeded
  - `docker compose -f .\docker-compose.vmangos-linux.yml down`
    - Succeeded
  - `docker compose -f .\docker-compose.vmangos-linux.yml up -d vmangos-database`
    - Succeeded
  - `docker exec westworldofwarcraft-vmangos-database-1 mariadb -uroot -proot -e "SHOW DATABASES; SELECT COUNT(*) AS allowed_clients FROM realmd.allowed_clients; SELECT COUNT(*) AS mangos_migrations FROM mangos.migrations;"`
    - Succeeded (`allowed_clients=60`, `mangos.migrations=1006`)
  - `docker compose -f .\docker-compose.vmangos-linux.yml up -d vmangos-realmd vmangos-mangosd`
    - Succeeded
  - `docker ps --filter name=westworldofwarcraft-vmangos- --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"`
    - `vmangos-database`, `vmangos-realmd`, and `vmangos-mangosd` all `Up` and `healthy`
  - `docker logs --tail 160 westworldofwarcraft-vmangos-realmd-1`
    - Succeeded (`Added realm "VMaNGOS"`)
  - `docker logs --tail 200 westworldofwarcraft-vmangos-mangosd-1`
    - Succeeded (`World initialized.`, `MaNGOSsoap: Bound to http://0.0.0.0:7878/`)
  - `Test-NetConnection -ComputerName 127.0.0.1 -Port 3306,3724,7878,8085`
    - All four ports reachable from the host
- **Files changed (session 155):**
  - `docker-compose.vmangos-linux.yml`
  - `docker/linux/vmangos/Sync-MigrationMarkers.ps1`
  - `docker/linux/vmangos/start-mangosd.sh`
  - `docker/linux/vmangos/start-realmd.sh`
  - `docs/DOCKER_STACK.md`
- **Next priorities:** run the next BG live parity slice against the now-live Docker vmangos stack, starting with the gathering / NPC-interaction timing failures that were previously blocked on server deployment
- **Session 154 — dockerized service stack + FG interaction parity slice shipped:**
  - Added a Windows-container compose stack in [docker-compose.windows.yml](/E:/repos/Westworld of Warcraft/docker-compose.windows.yml) for `vmangos-server`, `pathfinding-service`, `wow-state-manager`, and an optional `background-bot-runner` profile, with [docs/DOCKER_STACK.md](/E:/repos/Westworld of Warcraft/docs/DOCKER_STACK.md) documenting the required Windows-container mode and local MaNGOS bind mounts.
  - `WoWStateManager` now loads `appsettings.Docker.json`, exports config-backed realmd/world connection strings, prefers a published child BG worker under `BackgroundBotRunner\BackgroundBotRunner.exe`, and forwards docker-safe endpoint overrides when it spawns `BackgroundBotRunner`.
  - `PathfindingService`, `WoWStateManager`, and `BackgroundBotRunner` all now have Windows Dockerfiles; `PathfindingService` also ships a docker-specific bind address config, and the vmangos stack is launched through [docker/windows/vmangos/start-vmangos-stack.ps1](/E:/repos/Westworld of Warcraft/docker/windows/vmangos/start-vmangos-stack.ps1).
  - `ForegroundBotRunner` now exposes real `QuestGreetingFrame` and `TradeFrame` wrappers and implements the remaining task-owned bank/AH/craft helper methods instead of inheriting interface defaults.
- **Test baseline (session 154):**
  - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release -p:Platform=x86 -m:1 -p:UseSharedCompilation=false`
    - Succeeded (`0 errors`, warnings only)
  - `dotnet build Services/PathfindingService/PathfindingService.csproj --configuration Release -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~ForegroundInteractionFrameTests" --logger "console;verbosity=minimal"`
    - Passed (`10/10`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
    - Passed (`90/90`)
  - `docker compose -f .\docker-compose.windows.yml config`
    - Succeeded
- **Files changed (session 154):**
  - `Services/BackgroundBotRunner/Dockerfile`
  - `Services/DecisionEngineService/Repository/MangosRepository.cs`
  - `Services/ForegroundBotRunner/Frames/FgQuestGreetingFrame.cs`
  - `Services/ForegroundBotRunner/Frames/FgTradeFrame.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Inventory.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS_ARCHIVE.md`
  - `Services/PathfindingService/Dockerfile`
  - `Services/PathfindingService/PathfindingService.csproj`
  - `Services/PathfindingService/TASKS.md`
  - `Services/PathfindingService/appsettings.PathfindingService.Docker.json`
  - `Services/WoWStateManager/Dockerfile`
  - `Services/WoWStateManager/Program.cs`
  - `Services/WoWStateManager/Repository/ReamldRepository.cs`
  - `Services/WoWStateManager/StateManagerWorker.BotManagement.cs`
  - `Services/WoWStateManager/TASKS.md`
  - `Services/WoWStateManager/TASKS_ARCHIVE.md`
  - `Services/WoWStateManager/WoWStateManager.csproj`
  - `Services/WoWStateManager/appsettings.Docker.json`
  - `Services/WoWStateManager/appsettings.json`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundInteractionFrameTests.cs`
  - `docker-compose.windows.yml`
  - `docker/windows/vmangos/start-vmangos-stack.ps1`
  - `docs/DOCKER_STACK.md`
- **Next priorities:** bring up the Windows Docker stack end-to-end, capture first-run lifecycle evidence for `WoWStateManager` spawning BG inside the containerized environment, then resume BG live parity work against the now-complete FG interaction surface
- **Session 153 — FG trainer/talent/craft frame parity slice shipped:**
  - `ForegroundBotRunner` now exposes live `CraftFrame`, `TrainerFrame`, and `TalentFrame` wrappers instead of returning `null`, which restores the remaining legacy craft/train/talent frame surface still reachable from injected BotRunner actions.
  - Added Lua-backed `FgCraftFrame`, `FgTrainerFrame`, and `FgTalentFrame` implementations. The trainer wrapper preserves zero-based BotRunner indexing over WoW’s one-based trainer list, the talent wrapper reconstructs tab state and next-rank spell IDs from live Lua data, and the craft wrapper checks reagent counts before issuing `DoCraft(...)`.
- **Test baseline (session 153):**
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ForegroundInteractionFrameTests" --logger "console;verbosity=minimal"`
    - Passed (`8/8`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
    - Passed (`88/88`)
- **Files changed (session 153):**
  - `Services/ForegroundBotRunner/Frames/FgCraftFrame.cs`
  - `Services/ForegroundBotRunner/Frames/FgTalentFrame.cs`
  - `Services/ForegroundBotRunner/Frames/FgTrainerFrame.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Inventory.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Spells.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundInteractionFrameTests.cs`
- **Next priorities:** finish the remaining FG default-interface/task-surface gaps (`QuestGreetingFrame`, `TradeFrame`, then the task-owned bank/AH/craft helpers), then re-sweep the full repo for any code-only parity work still outstanding before the deferred live-validation chunk
- **Session 152 — FG taxi discovery parity slice shipped:**
  - `ForegroundBotRunner` now exposes a live `TaxiFrame` and implements foreground `DiscoverTaxiNodesAsync` / `ActivateFlightAsync`, so the injected flight-master task path no longer falls back to interface defaults.
  - Added a Lua-backed `FgTaxiFrame` wrapper that reads taxi-node metadata from the visible taxi map, tracks reachable/current nodes, and drives `TakeTaxiNode(...)` directly for FG flight activation.
- **Test baseline (session 152):**
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ForegroundInteractionFrameTests" --logger "console;verbosity=minimal"`
    - Passed (`5/5`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
    - Passed (`85/85`)
- **Files changed (session 152):**
  - `Services/ForegroundBotRunner/Frames/FgTaxiFrame.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Inventory.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundInteractionFrameTests.cs`
- **Next priorities:** finish the remaining FG default-interface/task-surface gaps that still matter to actionable flows, then re-sweep the repo for any remaining code-only parity gaps before the first big live validation chunk
- **Session 151 — FG frame/action-surface parity slice shipped:**
  - `ForegroundBotRunner` now exposes live `GossipFrame`, `QuestFrame`, and `MerchantFrame` objects backed by the injected client UI instead of returning `null`, which restores the remaining legacy FG BotRunner action surface for vendor/quest/gossip flows.
  - FG now implements the task-owned `QuickVendorVisitAsync`, `AcceptQuestFromNpcAsync`, and `TurnInQuestAsync` paths instead of inheriting interface defaults; quick vendor visits sell coarse junk, repair if possible, and buy requested items while the merchant frame stays open.
  - NPC interaction now records the active conversation GUID and explicitly targets the NPC before right-clicking, which keeps the new FG frame wrappers tied to the correct live conversation context.
- **Test baseline (session 151):**
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ForegroundInteractionFrameTests|FullyQualifiedName~VendorInteractionHelperTests" --logger "console;verbosity=minimal"`
    - Passed (`14/14`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
    - Passed (`84/84`)
- **Files changed (session 151):**
  - `Services/ForegroundBotRunner/Frames/FgGossipFrame.cs`
  - `Services/ForegroundBotRunner/Frames/FgMerchantFrame.cs`
  - `Services/ForegroundBotRunner/Frames/FgQuestFrame.cs`
  - `Services/ForegroundBotRunner/Frames/FrameLuaReader.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Inventory.cs`
  - `Services/ForegroundBotRunner/Statics/VendorInteractionHelper.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundInteractionFrameTests.cs`
  - `Tests/ForegroundBotRunner.Tests/VendorInteractionHelperTests.cs`
- **Next priorities:** finish the remaining FG task-driven interaction no-op paths (`DiscoverTaxiNodesAsync` / `ActivateFlightAsync`, then any still-null frame surfaces that matter to actionable flows), then continue the last movement/system sweep without starting live integration yet
- **Session 150 — FG vendor interaction parity slice shipped:**
  - `ForegroundBotRunner` no longer inherits interface default no-ops for merchant flows: the injected object manager now resolves NPC GUIDs to live objects, right-clicks them on the main thread, waits for the merchant frame, and executes buy/sell/repair through the real in-client interaction surface.
  - Sequential-bag sell semantics now match the existing BG/runtime contract: `bagId == 0xFF` is treated as the ordered flattened bag view instead of a literal bag index, which keeps foreground vendor sell calls aligned with the rest of the stack.
  - Added deterministic FG coverage for merchant-slot lookup Lua generation, quantity normalization, and sequential bag-slot GUID resolution used by the new sell path.
- **Test baseline (session 150):**
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~VendorInteractionHelperTests" --logger "console;verbosity=minimal"`
    - Passed (`5/5`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
    - Passed (`75/75`)
- **Files changed (session 150):**
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Services/ForegroundBotRunner/Statics/VendorInteractionHelper.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/VendorInteractionHelperTests.cs`
- **Next priorities:** finish the remaining FG interaction/action-surface sweep, then continue the remaining WoWSharpClient movement/system audit work without starting live integration yet
- **Session 149 — FG snapshot descriptor parity slice shipped:**
  - `ForegroundBotRunner` no longer hardcodes player `Race/Class/Gender` or unit `FactionTemplate`/power maps on the injected path; those fields now come from the same descriptor-backed `UNIT_FIELD_BYTES_0`, `UNIT_FIELD_FACTIONTEMPLATE`, and `UNIT_FIELD_POWER/MAXPOWER*` values the BG object model already consumes.
  - `LocalPlayer` now uses the descriptor-backed identity fields instead of mixing Lua/global-class fallbacks into the object model, which removes a real FG/BG divergence for capsule sizing, combat-role selection, corpse retrieval, and snapshot consumers that see the player through `IWoWPlayer`.
  - Added memory-backed FG tests that prove the interface path sees the corrected local-player `Race/Class/Gender` values and that mana/rage/energy plus faction-template reads round-trip from descriptor memory.
- **Test baseline (session 149):**
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ForegroundPlayerSnapshotParityTests" --logger "console;verbosity=minimal"`
    - Passed (`12/12`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
    - Passed (`68/68`)
- **Files changed (session 149):**
  - `Services/ForegroundBotRunner/Objects/LocalPlayer.cs`
  - `Services/ForegroundBotRunner/Objects/WoWPlayer.cs`
  - `Services/ForegroundBotRunner/Objects/WoWUnit.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundPlayerSnapshotParityTests.cs`
- **Next priorities:** keep the no-live-tests rule in place, then finish the remaining FG interaction-surface and live-validation expectation sweep before the final big validation chunk
- **Session 148 — BotRunner FG coinage assertion cleanup shipped:**
  - `EconomyInteractionTests` and `NpcInteractionTests` no longer carry the stale “FG coinage is a stub” branches; both suites now assert FG coinage movement directly like BG.
  - `Tests/BotRunner.Tests/TASKS.md` had a committed merge conflict, so it was replaced with a clean current-state tracker before recording this delta.
  - The deterministic BotRunner snapshot/protobuf slice still passes, so the assertion cleanup did not disturb the serialized movement/snapshot contract.
- **Test baseline (session 148):**
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`
    - Passed (`17/17`)
- **Files changed (session 148):**
  - `Tests/BotRunner.Tests/LiveValidation/EconomyInteractionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/NpcInteractionTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- **Next priorities:** sweep the remaining live-validation suites for obsolete FG/BG divergence assumptions, then finish the final code-only parity sweep before any live validation chunk
- **Session 147 — FG transport recorder parity slice shipped:**
  - `ForegroundBotRunner` can now resolve the active transport by GUID even when the mover is missing from visible-object enumeration, using the object-manager linked list as a fallback instead of dropping transport state on the floor.
  - `MovementRecorder` now serializes transport-local offset from the player’s main position fields, derives relative transport orientation from the resolved transport pose, reconstructs player world position from that transport pose for distance checks, and explicitly injects the ridden transport into `NearbyGameObjects` when the visible-object pass missed it.
  - Added deterministic `MovementRecorderTransportHelperTests` covering the local→world transform, transport-orientation derivation, zero-guid clearing, and explicit transport snapshot de-duplication.
- **Test baseline (session 147):**
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ForegroundBotRunner.Tests.MovementRecorderTransportHelperTests" --logger "console;verbosity=minimal"`
    - Passed (`4/4`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
    - Passed (`68/68`)
- **Files changed (session 147):**
  - `Services/ForegroundBotRunner/MovementRecorder.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.ObjectEnumeration.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/MovementRecorderTransportHelperTests.cs`
- **Next priorities:** clear the stale FG coinage skip logic in live-validation tests, sweep for any remaining snapshot/runtime parity stubs, and leave the fresh Orgrimmar transport capture for the final post-implementation validation chunk
- **Session 146 — FG coinage/local snapshot parity slice shipped:**
  - `ForegroundBotRunner` no longer hardcodes player money to `0`: `WoWPlayer.Coinage` now reads `PLAYER_FIELD_COINAGE` from descriptor memory, which restores FG snapshot parity for vendor/mail/trainer flows that rely on copper totals.
  - `LocalPlayer.Copper`, `InBattleground`, and `HasQuestTargets` now match the BG model’s behavior instead of staying pinned to trivial stub values.
  - Added memory-backed FG unit tests that build a fake object/descriptor pair in-process and verify the injected object model reads coinage and quest-log state correctly without requiring a live client.
- **Test baseline (session 146):**
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ForegroundPlayerSnapshotParityTests" --logger "console;verbosity=minimal"`
    - Passed (`10/10`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
    - Passed (`64/64`)
- **Files changed (session 146):**
  - `Services/ForegroundBotRunner/Objects/LocalPlayer.cs`
  - `Services/ForegroundBotRunner/Objects/WoWPlayer.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundPlayerSnapshotParityTests.cs`
- **Next priorities:** `7.9` additional transport replay data, clearing the remaining stale FG coinage skip logic in live-validation tests before the final big integration pass, and a final movement/packet parity sweep for anything still only decompiled but not binary-backed
- **Session 145 — recorded remote-unit extrapolation proof shipped:**
  - `WoWUnitExtrapolationTests` now includes replay-backed fixtures from real nearby-unit trajectories instead of only synthetic movement vectors.
  - Added a slow-walk Undercity fixture that proves the WoW.exe `<3y/s` jitter filter returns the raw server position even when the recorded NPC keeps moving for another half-second, so low-speed drift suppression is now backed by capture data.
  - Added a fast Blackrock Spire runner fixture that stays within `0.02y` horizontal drift against observed motion, which closes the remaining “recorded directional remote-unit extrapolation fixture” gap called out in earlier sessions.
- **Test baseline (session 145):**
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~WoWUnitExtrapolationTests" -v n`
    - Passed (`8/8`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -v n`
    - Passed (`1351/1351`, `1 skipped`; `dumpbin` still missing in the vcpkg `applocal.ps1` post-step, unchanged and non-blocking)
- **Files changed (session 145):**
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs`
- **Session 144 — FG spell snapshot parity slice shipped:**
  - `ForegroundBotRunner` now reconciles spell knowledge from two sources instead of letting the next refresh overwrite event-driven gains: the main-thread `LEARNED_SPELL` / `UNLEARNED_SPELL` hook path updates sticky learned/removed IDs immediately, while `RefreshSpells()` publishes `stable IDs + sticky learns - sticky removals`.
  - The immediate event path now handles unlearns as first-class deltas, updates the thread-safe `KnownSpellIds` snapshot right away, and keeps `LocalPlayer.RawSpellBookIds` in sync when the player object is live.
  - Added deterministic `SpellKnowledgeReconcilerTests` to pin the exact contract: stable IDs pass through, sticky learned IDs stay visible when stable sources miss them, stable rescans clear sticky deltas when they confirm the spell state, and sticky removals mask IDs only while the stable sources are missing them.
- **Test baseline (session 144):**
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release -v n`
    - Passed (`54/54`; `dumpbin` still missing in the vcpkg `applocal.ps1` post-step, unchanged and non-blocking)
- **Files changed (session 144):**
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Spells.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/SpellKnowledgeReconcilerTests.cs`
- **Session 143 — FG WndProc/offset hardening slice shipped:**
  - `ForegroundBotRunner` now exposes the live `ThreadSynchronizer` WndProc gate as a pure helper (`ThreadSynchronizerGateEvaluator`) so the packet-driven/heuristic safety rules are deterministic and unit-testable without touching the injected hook path.
  - New FG tests now pin the gate’s critical cases: pre-world charselect allowance, valid-world seeding, invalid-map transition blocking, `ConnectionStateMachine.IsLuaSafe` blocking, valid-map auto-pause on map change, and object-manager teardown blocking.
  - The binary-backed FG offset audit now extends beyond the packet hooks into snapshot-critical movement/runtime fields: corpse globals, player class and character count, object-manager base, movement-info facing/transport/fall/speed/move-spline offsets, plus the audited distinction between the `0x00672170` `CMap::VectorIntersect` wrapper and `World::Intersect` at `0x006AA160`.
  - `ConnectionStateMachine` and inferred packet fallback coverage were re-run after the audit; no further changes were required and the full FG deterministic suite remains green.
- **Test baseline (session 143):**
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded (`dumpbin` still missing in the vcpkg `applocal.ps1` post-step; non-blocking and unchanged)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`50/50`)
- **Files changed (session 143):**
  - `Services/ForegroundBotRunner/Mem/MemoryAddresses.cs`
  - `Services/ForegroundBotRunner/Mem/Offsets.cs`
  - `Services/ForegroundBotRunner/Mem/ThreadSynchronizer.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/OffsetsBinaryAuditTests.cs`
  - `Tests/ForegroundBotRunner.Tests/ThreadSynchronizerGateTests.cs`
  - `Tests/ForegroundBotRunner.Tests/WoWExeImage.cs`
- **Session 142 — Orgrimmar transport replay blocker pinned:**
  - Added deterministic coverage for the only in-repo Orgrimmar-area transport recording, `Dralrahgra_Durotar_2026-02-08_11-06-02`, which is the Orgrimmar-to-Undercity zeppelin rather than an elevator.
  - `PhysicsReplayTests` now proves the ground-side boarding/disembark windows around that recording still replay cleanly (`avg=0.0043y`, `p99=0.0887y`) even though the ride itself is not simulatable from current data.
  - `ElevatorScenarioTests` now explicitly asserts why the in-flight zeppelin segment cannot be replayed today: the recording keeps `transportGuid` set but drops `NearbyGameObjects` to zero immediately after boarding, so the replay harness must skip those transport frames instead of fabricating mover state.
  - Result: `7.9` is still open, but it is now concretely classified as a recording-data gap rather than an unexplained transport-physics regression.
- **Test baseline (session 142):**
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OrgrimmarZeppelinRide_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~OrgrimmarZeppelinReplay_SkipsInFlightFrames_WithoutDynamicObjectData" -v n`
    - Passed (`2/2`)
- **Files changed (session 142):**
  - `Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/TestConstants.cs`
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
- **Session 141 — deterministic knockback/extrapolation parity hardening shipped:**
  - `Navigation.Physics.Tests` now includes a direct knockback-arc parity test that validates `FALLINGFAR` airborne motion against WoW gravity and end-of-frame vertical velocity, covering the same native path used after `SMSG_MOVE_KNOCK_BACK` seeds BG physics.
  - The test-side movement-bit map in `NavigationInterop` was corrected to match `PhysicsBridge.h`; the previous enum had `FallingFar` and `Flying` swapped plus `OnTransport` on the wrong bit, which could silently invalidate airborne/transport assertions without touching runtime code.
  - Flat-ground frame-by-frame validation now uses the same Crossroads open-plains fixture already used by the movement-speed suite, replacing an Orgrimmar Valley of Strength line that is no longer an unobstructed 1-second walk corridor in current map data.
  - `WoWSharpClient.Tests` now pins the remaining implemented extrapolation guardrails: sub-jitter movement, teleport-speed outliers, and stale updates all prove `WoWUnit.GetExtrapolatedPosition(...)` returns the current position instead of manufacturing drift.
  - The remaining extrapolation gap is still a data gap, not a managed-code gap: the repository does not yet contain a recorded directional remote-unit packet fixture suitable for replay-accuracy assertions against observed NPC motion.
- **Test baseline (session 141):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests|FullyQualifiedName~MovementControllerPhysics" -v n`
    - Passed (`42/42`)
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWUnitExtrapolationTests" -v n`
    - Passed (`6/6`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1349/1350`, `1 skipped`)
- **Files changed (session 141):**
  - `Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs`
- **Next priorities:** `7.9` Orgrimmar elevator replay coverage, a recorded directional remote-unit extrapolation fixture, then the remaining FG hardening and binary-audit sweep
- **Session 140 — observer swim/pitch opcode parity slice shipped:**
  - BG now handles the last non-cheat observer movement rebroadcasts still missing from the Vanilla 1.12.1 dispatch sweep: `MSG_MOVE_START_SWIM`, `MSG_MOVE_STOP_SWIM`, `MSG_MOVE_START_PITCH_UP`, `MSG_MOVE_START_PITCH_DOWN`, `MSG_MOVE_STOP_PITCH`, and `MSG_MOVE_SET_PITCH`.
  - `MovementHandler`, `OpCodeDispatcher`, and `WorldClient` now route those packets through the same parse-and-apply path as the rest of the observer movement matrix, so remote units keep `MOVEFLAG_SWIMMING` and `SwimPitch` in sync instead of silently dropping those updates.
  - Deterministic coverage now proves remote-unit swim-flag toggles and pitch updates apply end to end, and the world-client bridge test includes the newly-registered opcodes.
  - The remaining opcode-enum names still absent from the dispatcher/bridge are cheat/debug paths (`*_CHEAT`, toggle logging/collision/gravity, raw-position ack), not normal Vanilla movement rebroadcasts.
- **Test baseline (session 140):**
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObserverMovementFlagOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObserverMovementPitchOpcodes_UpdateRemoteUnitSwimPitch" -v n`
    - Passed (`16/16`)
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BridgeRegistration_MovementOpcodes_Registered" -v n`
    - Passed (`1/1`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1346/1347`, `1 skipped`)
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n`
    - Passed (`117/117`)
- **Files changed (session 140):**
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`
- **Next priorities:** `7.9` Orgrimmar elevator replay coverage, a recorded directional remote-unit extrapolation fixture, focused knockback trajectory coverage, the FG hardening audit, and any binary-backed non-cheat movement/system gaps that remain after those.
- **Session 139 — reachable spline wire/runtime parity slice shipped:**
  - BG `SMSG_MONSTER_MOVE` parsing now matches the Vanilla/VMaNGOS wire formats instead of assuming a single simplified point list:
    - linear paths rebuild their node sequence from the transmitted destination plus packed `appendPackXYZ` offsets,
    - smooth paths (`Flying`) read raw Catmull-Rom nodes directly.
  - Cyclic smooth splines now normalize the fake `EnterCycle` start vertex into the managed runtime’s closing-loop representation, and `ActiveSpline` now wraps Catmull-Rom control-point lookup across the first and closing segments instead of clamping at the ends.
  - The shared test payload helper for direct monster-move runtime tests now emits the real linear packet layout, so future spline/runtime regressions will exercise the same wire shape the client receives.
  - Reachable managed spline parity is now closed for Vanilla `SMSG_MONSTER_MOVE`: the server/client code still contains other spline evaluators, but the current Vanilla movement wire surface reaches linear and `Flying`/Catmull-Rom only.
- **Test baseline (session 139):**
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MonsterMoveParsingTests|FullyQualifiedName~ActiveSplineStepTests.Step_CyclicFlyingSpline_UsesWrappedNeighborOnFirstSegment|FullyQualifiedName~ActiveSplineStepTests.Step_CyclicFlyingSpline_UsesWrappedNeighborOnClosingSegment" -v n`
    - Passed (`5/5`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1340/1341`, `1 skipped`)
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n`
    - Passed (`117/117`)
- **Files changed (session 139):**
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/Movement/SplineController.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/MonsterMoveParsingTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/ActiveSplineTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `docs/server-protocol/movement-protocol.md`
- **Next priorities:** `7.9` Orgrimmar elevator replay coverage, a recorded directional remote-unit extrapolation fixture, any binary-backed movement/system gaps still left after the now-closed reachable spline + Vanilla opcode sweeps, and the FG hardening audit
- **Session 138 — observer movement opcode parity slice shipped:**
  - BG now handles the remaining observer-side player movement broadcasts from the Vanilla 1.12.1 movement sender matrix: `MSG_MOVE_SET_RUN_MODE`, `MSG_MOVE_SET_WALK_MODE`, `MSG_MOVE_SET_RUN_BACK_SPEED`, `MSG_MOVE_SET_WALK_SPEED`, `MSG_MOVE_SET_SWIM_BACK_SPEED`, `MSG_MOVE_SET_TURN_RATE`, `MSG_MOVE_FEATHER_FALL`, and `MSG_MOVE_HOVER`.
  - `MovementHandler` now parses those broadcasts through the same remote-unit state path as the existing observer movement packets, so remote units pick up player-owned speed and flag changes instead of silently dropping them.
  - `WorldClient` and `OpCodeDispatcher` bridge registration now matches the full Vanilla player/observer movement matrix from `MovementPacketSender.cpp` / `MovementPacketSender.h`.
  - Deterministic managed coverage now exercises the full controller speed-change family plus the observer-side flag and speed broadcast matrix end to end.
- **Test baseline (session 138):**
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet build Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.ForceSpeedChangeOpcodes_ParseApplyAndAck|FullyQualifiedName~ObjectManagerWorldSessionTests.ObserverMovementFlagOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObjectManagerWorldSessionTests.ObserverMovementSpeedOpcodes_UpdateRemoteUnitState" -v n`
    - Passed (`22/22`)
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WorldClientTests.BridgeRegistration_MovementOpcodes_Registered" -v n`
    - Passed (`1/1`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1336/1337`, `1 skipped`)
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n`
    - Passed (`117/117`)
- **Files changed (session 138):**
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`
- **Session 137 — movement opcode completeness slice shipped:**
  - BG now handles the remaining local-player movement flag toggle opcodes end to end for `SMSG_MOVE_WATER_WALK`, `SMSG_MOVE_LAND_WALK`, `SMSG_MOVE_SET_HOVER`, `SMSG_MOVE_UNSET_HOVER`, `SMSG_MOVE_FEATHER_FALL`, and `SMSG_MOVE_NORMAL_FALL`.
  - `WoWSharpObjectManager` mutates the local player state before sending the matching ACK packets, so managed state and on-wire acknowledgements stay aligned with WoW.exe behavior.
  - Remote-unit state now applies the missing server-controlled spline rate opcodes (`RUN`, `RUN_BACK`, `SWIM`, `WALK`, `SWIM_BACK`, `TURN_RATE`) and spline flag toggles for water-walk, safe-fall, hover, and start/stop swim.
  - Added deterministic managed coverage for local ACK/application, remote spline state mutation, and `WorldClient` bridge registration for the new movement opcode surface.
- **Test baseline (session 137):**
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet build Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.ServerControlledMovementFlagChanges_ParseApplyAndAck|FullyQualifiedName~ObjectManagerWorldSessionTests.SplineSpeedOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObjectManagerWorldSessionTests.SplineFlagOpcodes_UpdateRemoteUnitState" -v n`
    - Passed (`20/20`)
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WorldClientTests.BridgeRegistration_MovementOpcodes_Registered" -v n`
    - Passed (`1/1`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1317/1318`, `1 skipped`)
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n`
    - Passed (`117/117`)
- **Files changed (session 137):**
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Exports/WoWSharpClient/WoWSharpEventEmitter.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`
- **Next priorities:** `7.9` Orgrimmar elevator replay coverage, the remaining spline-mode audit, a recorded directional remote-unit extrapolation fixture, any binary-backed movement opcode gaps still left after the dispatch-table sweep, and the FG hardening audit
- **Session 134 — extrapolation seeding + knockback validation slice shipped:**
  - `WoWUnit.GetExtrapolatedPosition()` now matches the same directional basis the physics layer uses for backward, strafe, and diagonal movement (`sin(45°)` damping from WoW.exe `VA 0x0081DA54`)
  - `WoWSharpObjectManager` now seeds remote-unit extrapolation state on create/add movement blocks, not only on later updates, which fixes a real gap in BG remote-position prediction startup
  - Added deterministic tests for backward/strafe/diagonal extrapolation math, remote-unit add-path extrapolation seeding, knockback parse -> ACK -> pending-impulse state, and `MovementController` knockback impulse consumption into physics input
  - The current `20240815` WoWSharpClient packet fixture does not contain directional remote-unit segments suitable for a recorded extrapolation accuracy gate; the remaining extrapolation work is a better capture/fixture, not another code-path patch
- **Test baseline (session 134):**
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWUnitExtrapolationTests|FullyQualifiedName~ObjectManagerWorldSessionTests.RemoteUnitAdd_PrimesExtrapolationStateFromMovementBlock|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveKnockBack_ParseStoresImpulseClearsDirectionAndAcks|FullyQualifiedName~MovementControllerTests.PendingKnockback_OverridesDirectionalInputAndFeedsPhysicsVelocity" -v n`
    - Passed (`6/6`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1286/1287`, `1 skipped`)
- **Files changed (session 134):**
  - `Exports/WoWSharpClient/Models/WoWUnit.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs`
  - `Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- **Next priorities:** finish P7.5/P7.9 runtime elevator coverage, then complete the spline-mode audit, add a directional remote-unit capture for recorded extrapolation accuracy, and continue the broader movement opcode / FG binary hardening sweep
- **Session 133 — managed force-speed parity slice shipped:**
  - BG now handles the remaining server-forced movement rate opcodes end to end: `SMSG_FORCE_WALK_SPEED_CHANGE`, `SMSG_FORCE_SWIM_BACK_SPEED_CHANGE`, and `SMSG_FORCE_TURN_RATE_CHANGE`
  - `MovementHandler`, `OpCodeDispatcher`, and `WorldClient` now route those packets through the same ACK/application path as the existing run/swim speed changes
  - `WoWSharpObjectManager` now applies walk speed, swim-back speed, and turn-rate updates to the local player model before echoing the matching ACK packet
  - Added deterministic managed tests that cover parse -> event -> player-state mutation -> ACK payload for all three opcodes plus bridge registration coverage in `WowSharpClient.NetworkTests`
- **Test baseline (session 133):**
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet build Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.MissingForceChangeOpcodes_ParseApplyAndAck" -v n`
    - Passed (`3/3`)
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WorldClientTests.BridgeRegistration_MovementOpcodes_Registered" -v n`
    - Passed (`1/1`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1280/1281`, `1 skipped`)
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n`
    - Passed (`117/117`)
- **Files changed (session 133):**
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Exports/WoWSharpClient/WoWSharpEventEmitter.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`
- **Next priorities:** finish P7.5/P7.9 runtime elevator coverage, then sweep the remaining movement parity gaps (knockback/extrapolation validation, spline audit, broader movement opcode completeness, FG hardening)
- **Session 136 — direct monster-move runtime parity slice shipped:**
  - `MovementHandler`, `OpCodeDispatcher`, and `WorldClient` now route direct `SMSG_MONSTER_MOVE` and `SMSG_MONSTER_MOVE_TRANSPORT` packets through the same managed state-update path as compressed monster moves.
  - Transport spline playback now advances in transport-local coordinates and resyncs passenger world position/facing from the owning transport after each managed spline step.
  - `WoWSharpObjectManager` now guarantees a valid monotonic world clock before runtime spline activation, fixing direct monster-move processing before the normal game loop/login-verify path is running.
  - `UpdateProcessingHelper` now waits for pending movement-only updates instead of treating object-count stability as sufficient drain evidence.
  - Added deterministic runtime tests covering direct world-space monster moves and direct transport-local monster moves end to end.
- **Test baseline (session 136):**
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~DirectMonsterMove" -v n`
    - Passed (`2/2`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1296/1297`, `1 skipped`)
- **Files changed (session 136):**
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/Movement/SplineController.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Util/UpdateProcessingHelper.cs`
- **Next priorities:** finish P7.5/P7.9 runtime elevator coverage, then continue the remaining opcode/spline audit work and the FG hardening sweep
- **Session 135 — managed spline runtime parity slice shipped:**
  - `SMSG_MONSTER_MOVE` parsing now preserves the monster-move server start time on the movement update instead of discarding it into an overloaded local field.
  - `SplineController` now seeds new splines from server start time so remote movement starts at the correct in-flight point when packets arrive late.
  - Cyclic splines now stay on the terminal point at the exact duration boundary before wrapping on the next tick, matching client-visible patrol timing.
  - Runtime spline facing now follows `SplineType` modes instead of leaving movers frozen at stale orientation: normal movement faces travel direction, `FacingAngle` locks to the explicit angle, `FacingSpot` faces the target point, and `FacingTarget` resolves through the object manager.
- **Test baseline (session 135):**
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore`
    - Succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MonsterMoveParsingTests|FullyQualifiedName~ActiveSplineStepTests|FullyQualifiedName~SplineFacingTests|FullyQualifiedName~MovementBlockUpdateCloneBugTests" -v n`
    - Passed (`33/33`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementBlockUpdate|FullyQualifiedName~MovementInfoUpdate" -v n`
    - Passed (`27/27`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1294/1295`, `1 skipped`)
- **Files changed (session 135):**
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/Models/MovementBlockUpdate.cs`
  - `Exports/WoWSharpClient/Movement/SplineController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/MonsterMoveParsingTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/ActiveSplineTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementBlockUpdateCloneBugTests.cs`
- **Next priorities:** finish P7.5/P7.9 runtime elevator coverage, then continue the movement opcode/FG hardening sweep and any remaining spline modes that need binary-backed evidence
- **Session 140 — FG receive-hook audit slice shipped:**
  - `PacketLogger` no longer relies on a stale `ProcessMessage` fallback heuristic: the direct SMSG receive hook now validates the configured `NetClient::ProcessMessage` VA against the real handler-table access pattern used by the 1.12.1 client (`[this + opcode*4 + 0x74]`) and can fall back to the scanned address if the fixed VA drifts.
  - Added binary-backed `ForegroundBotRunner.Tests` coverage against `D:\World of Warcraft\WoW.exe` for:
    - `NetClient::Send` prologue bytes / safe overwrite size
    - `NetClient::ProcessMessage` prologue bytes / safe overwrite size
    - process-message discovery via the handler-table pattern
    - `GameVersion` address contents (`"1.12.1"`)
    - movement-struct offset relationships (`0x9A8 -> 0x9B8/0x9BC/0x9C0/0x9E8`)
  - Cleaned the broken `Services/ForegroundBotRunner/TASKS.md` merge-conflict state and updated FG docs to reflect that packet logging now covers both send and recv hooks.
- **Test baseline (session 140):**
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PacketLoggerBinaryAuditTests" -v n`
    - Passed (`6/6`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ConnectionStateMachineTests" -v n`
    - Passed (`34/34`)
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`40/40`)
- **Files changed (session 140):**
  - `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs`
  - `Services/ForegroundBotRunner/Mem/Offsets.cs`
  - `Services/ForegroundBotRunner/Properties/AssemblyInfo.cs`
  - `Services/ForegroundBotRunner/CLAUDE.md`
  - `Services/ForegroundBotRunner/README.md`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/PacketLoggerBinaryAuditTests.cs`
  - `Tests/ForegroundBotRunner.Tests/WoWExeImage.cs`
- **Next priorities:** keep the no-live-tests rule in place; remaining movement/system parity work is still P7.9 recording coverage, a recorded directional remote-unit extrapolation fixture, the `ThreadSynchronizer` WndProc safety audit, and the rest of the FG offset sweep
- **Session 132 — swim collision parity slice shipped:**
  - `PhysicsMovement.cpp` swim movement now resolves against real world geometry instead of free-integrating through submerged terrain
  - Swim collision uses WoW.exe’s `0.5` swim-branch displacement constant (`VA 0x007FFA24`) as two half-step submerged collision substeps
  - `PhysicsEngine.cpp` now keeps water-entry horizontal damping visible in output velocity on the entry frame instead of mutating only carried state
  - Added focused physics regressions for Durotar seabed collision and recorded water-entry damping
- **Session 133 — grounded support-normal parity slice shipped:**
  - `CollisionStepWoW` now resolves the grounded support normal from the closest walkable AABB terrain contact to the chosen `groundZ` instead of leaving a synthetic flat `(0,0,1)` normal on steep grounded frames
  - Added `ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport` so the steep Valley of Trials route now proves we keep a real slope support normal while descending
  - Detailed steep-descent replay now reports `No-ground frames: 0` instead of `528`, while preserving the same `0.20y` max hover gap above true ground
- **Session 138 — moving-base support parity slice shipped:**
  - Fresh `WoW.exe` review reinforces that vanilla persists transport-local state across frames, while static terrain support is re-derived from collision each tick
  - `DynamicObjectRegistry` now assigns stable runtime IDs and resolves world support points back to object-local coordinates
  - `SceneQuery` AABB contact tests now include dynamic-object triangles, so `CollisionStepWoW` can clamp onto moving bases through the same grounded AABB support-selection path it uses for terrain
  - `CollisionStepWoW` now emits `standingOnInstanceId` / local support coordinates only when the chosen grounded support is truly dynamic
  - Added `ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_ReportsDynamicSupportToken` to pin a real Undercity elevator frame against that behavior
- **Test baseline (session 138):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Navigation.Physics.Tests.ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_ReportsDynamicSupportToken" --logger "console;verbosity=normal"`
    - Passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" -v minimal`
    - Passed (`3/3`)
- **Files changed (session 138):**
  - `Exports/Navigation/DynamicObjectRegistry.h`
  - `Exports/Navigation/DynamicObjectRegistry.cpp`
  - `Exports/Navigation/SceneQuery.h`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
- **Next priorities:** keep static walkable support recomputed from collision, not from a synthetic terrain token; if more moving-base parity is needed, extend the dynamic support token path before revisiting waypoint smoothing/corridor clamping after the current bot-behavior priorities
- **Test baseline (session 133):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundDetectionDiagnostic|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ValleyOfTrialsSlopeTests.SlopeRoute_StepPhysics_ZDoesNotOscillate"`
    - Passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground"`
    - Passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName=Navigation.Physics.Tests.ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundDetectionDiagnostic" --logger "console;verbosity=detailed"`
    - Passed; steep-descent `groundNz` now ranged `0.745..0.999`, and `No-ground frames` dropped `528 -> 0`
- **Files changed (session 133):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/ValleyOfTrialsSlopeTests.cs`
- **Next priorities:** full touched-surface persistence (`standingOnInstanceId` / local-point tracking) is still open if we want exact “standing on this triangle/object” parity; after the current bot-behavior priorities, return to waypoint smoothing/corridor clamping so path smoothing never exits walkable triangles
- **Test baseline (session 132):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.DurotarRecording_WaterEntry_DampsHorizontalVelocity|FullyQualifiedName~FrameByFramePhysicsTests.DurotarSwimDescent_SeabedCollisionPreventsTerrainPenetration|FullyQualifiedName~PhysicsReplayTests.SwimForward_FrameByFrame_PositionMatchesRecording" -v n`
    - Passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementControllerPhysics" -v n`
    - Passed (`29/29`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.WestfallCoast_EnterWater_TransitionsToSwimming" -v n`
    - Passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" -v n`
    - Passed (aggregate clean-frame thresholds held)
- **Files changed (session 132):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsMovement.cpp`
  - `Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs`
- **Session 131 — P7 transport/elevator parity shipped:**
  - Added BG transport coordinate helpers and moved transport-local/world transforms into shared managed code
  - Fixed movement packet serialization so world position/facing stay in the base block and transport-local offset/orientation stay in the transport block
  - Re-enabled `MOVEFLAG_ONTRANSPORT` on-wire when a transport GUID is present after WoW.exe flag masking
  - `MovementController` now detects transport entry/exit, resets continuity correctly, includes active transports in physics nearby objects, and recomputes local offsets/orientation from world-space physics output
  - `WoWSharpObjectManager` now continuously syncs passenger world position/facing from transport-local state
  - Added WoWSharpClient transport tests plus an Undercity elevator replay parity test
  - Fixed replay-harness sentinel resets (`StepUpBaseZ` / `FallStartZ`) for board/leave/teleport skips
  - Fixed native step-up persistence so replay-ground refinement cannot re-promote a bad overhead surface after transport exit
- **Test baseline (session 131):**
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n`
    - Passed (`1277/1278`, `1 skipped`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementControllerPhysics" -v n`
    - Passed (`29/29`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ElevatorRideV2_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=detailed"`
    - Passed (`2/2`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=detailed"`
    - Passed (`avg=0.0124y`, `p99=0.1279y`, `worst=2.2577y`)
- **Files changed (session 131):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/Movement/TransportCoordinateHelper.cs`
  - `Exports/WoWSharpClient/Parsers/MovementPacketHandler.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs`
  - `Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/MovementPacketHandlerTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- **Next priorities:** swim collision path at `0x633B5E` closed in session 132; remaining parity work is P7.5/P7.9 plus movement/system sweeps listed above
- **Session 130 — P6 AABB Collision Rewrite COMPLETE:**
  - Deleted ~2100 lines of custom physics workarounds
  - Implemented WoW.exe CollisionStepWoW (VA 0x633840) with AABB terrain queries
  - `SweepAABB` + `TestTerrainAABB` with SAT AABB-triangle (13 axes) + barycentric Z
  - 2-pass swept AABB (full + half-step with √2 contraction)
  - Deleted entire 3-pass system (DecomposeMovement, ExecuteUp/Side/Down, PerformThreePassMove)
  - Deleted from MC: false-FALLINGFAR stripping, ground persistence, teleport Z clamp, dead reckoning, slope guards, walk experiment, grounded→falling hysteresis (631 lines)
  - Fixed diagonal damping: sin(45°) applied for forward+strafe (was 41% too fast)
  - Fixed combat BADFACING: MSG_MOVE_STOP position sync before MSG_MOVE_SET_FACING
  - Fixed Face() threshold: 0.1 rad from WoW.exe VA 0x80C408
  - All 18 WoW.exe constants verified against binary (see P6 table)
  - Undercity WMO floor data confirmed present and accurate (0.003y error)
- **Test baseline (session 130):**
  - **29/29 MC unit tests pass** (flat, uphill, downhill, ledge, landing, diagonal, backward, walk, gravity, jump, terminal vel, facing, heartbeat, combat approach, Undercity probe)
  - **Live speed test: PASS** (27s)
  - **Live combat test: PASS** (47s)
  - **Live basic/lifecycle/equip: ALL PASS**
  - **Physics replay calibration:** avg 0.095y, ground-only 0.06y (FAIL on p99=3.47 and worst=6.41 — caused by elevator transport frames, NOT physics logic — see P7)
- **Next priorities:** P7 transport/elevator coordinate transforms, then move to combat/questing logic
- **Commits:** `f6239686` through `24f583bd` (15+ commits)
- **Previous session (128) completed:**
  - **Deep WoW.exe binary decompilation** — 20+ functions decompiled including:
    - CMovement::CollisionStep (0x633840) — 2-pass AABB sweep
    - CMovement::Update (0x618C30) — per-frame movement dispatcher
    - CWorldCollision::TestTerrain (0x6721B0) — spatial grid query
    - SpatialQuery (0x6AA8B0) — chunk-based terrain/WMO/M2 intersection
    - BuildMovementInfo (0x7C6340) — wire format verified byte-for-byte
    - Packet dispatch table (0x616580) — 39 movement commands mapped
    - Remote unit extrapolation loop (0x616DE0)
  - **Phase 1: Speed change application** — SMSG_FORCE_*_SPEED_CHANGE ACKed but never applied; now writes speed to player model
  - **Phase 2: Knockback system** — Full KnockBackArgs parsing (guid+counter+vsin+vcos+hspeed+vspeed), velocity impulse via MovementController, FALLINGFAR + gravity handles trajectory
  - **Phase 3: Remote unit extrapolation** — GetExtrapolatedPosition() on WoWUnit with WoW.exe speed thresholds (>60y/s=teleport, <3y/s=jitter)
  - **Phase 4: Spline improvements** — Catmull-Rom for Flying, Cyclic wrap-around, Frozen halt
  - **Time delta clamping** — [-500ms, +1000ms] matching WoW.exe 0x618D0D
  - **New constants:** SQRT_2, COLLISION_SKIN_EPSILON, speed thresholds
  - **Calibration unchanged:** 142/143 physics tests, 44/44 spline tests, 18/18 snapshot tests
- **Commits:** `9abae9dc` through `61c885f8` (8 commits)
- **Remaining:** Phase 5 (FG hardening), Phase 6 (opcode sweep) — see plan at `~/.claude/plans/prancy-chasing-puddle.md`
- **Previous session:**
  - **BG bot CharacterSelect stuck fix (72476477):** Root cause: `ReadItemField` in ObjectUpdateHandler.cs had no catch-all for unrecognized item fields (enchantment sub-slots 23-42). Missing 4-byte reads corrupted the update stream — player GUID 0x10 was read as update type 16, discarding the player's own create object. Added `else reader.ReadUInt32()` to all field readers (Item, GameObject, DynamicObject, Corpse, Container). BG bot now reliably enters world.
  - **Elevated-structure ledge guard (46183c06):** Physics engine `GetGroundZ` returns terrain Z below WMO docks/piers. Added two-stage check: detect character is on invisible surface (charZ >> originGroundZ), then use STEP_HEIGHT threshold to prevent walking off. Fixes BG bot sinking at Ratchet dock.
  - **PathfindingService hang fix (ac2b7986):** Disabled post-corridor segment validation — `ValidateWalkableSegment` physics sweeps cost 5-28s per segment. Corridor paths are navmesh-constrained by construction.
  - **NPC detection polling (ac2b7986, b5e02f19):** Economy tests use 5-second polling loop for NPC streaming after teleport. Fixed `Game.WoWUnit` type.
  - **SOAP item delivery timeout (014e2507):** Increased from 5s to 15s for `.additem` propagation.
- **Commits:** `ac2b7986`, `b5e02f19`, `014e2507`, `46183c06`, `72476477`
- **Test baseline (26 passed, 10 failed, 2 skipped, aborted before all tests ran):**
  - **Passing (26):** BasicLoop (2/2), BuffAndConsumable (1/2), CharacterLifecycle, CraftingProfession, EquipmentEquip, GatheringRouteSelection (6/6), LiveBotFixtureDiagnostics (2/2), MapTransition, MovementParity (10/10), MovementSpeed.ZStable
  - **Failing (10):** DeathCorpseRun (BG), Economy (3: Bank, AH, Mail), Fishing, Gathering (Mining + Herbalism), GroupFormation, MovementSpeed (2: BG speed, Dual comparison)
  - **Not run (aborted):** Navigation (3), NpcInteraction (4), SpellCast, StarterQuest, TalentAllocation, UnequipItem, VendorBuySell (2), QuestInteraction, OrgrimmarGroundZ (2)
- **Data dirs:** Server reads from `D:/MaNGOS/data/`. VMaNGOS tools at `D:/vmangos-server/`. WoW MPQ at `D:/World of Warcraft/Data/`. Buildings at `D:/World of Warcraft/Buildings/`.
- **Known issues:**
  1. BG bot teleport position check fails — `.go xyz` commands execute but snapshot position doesn't update within 5s timeout. Causes cascade failures in Economy, Gathering, MovementSpeed tests.
  2. BG bot dead/ghost after teleport — EnsureCleanSlateAsync revive doesn't complete before test body runs.
  3. Gathering (Mining/Herbalism) — bot detects nodes but can't interact/gather (11min timeout).
  4. MovementSpeed — BG bot barely moves (0.39 y/s vs expected 7 y/s) during walk test.
  5. CombatBg/CombatFg fixtures — FG bot stuck at CharacterSelect (COMBATTEST injection/login issue).
  6. Test run aborted after 40min — remaining 16 tests never executed.
- **Next:**
  1. Fix BG bot teleport position tracking — snapshot position not updating after `.go xyz`
  2. Fix BG bot movement speed — barely moves during walk tests
  3. Investigate gathering interaction protocol (CMSG_GAMEOBJ_USE → channel → loot)
  4. Run remaining tests that didn't execute (Navigation, NPC, Spell, Quest, Vendor)

- **Session 141 — walkable-triangle-preserving smoothing guardrails shipped:**
  - Re-prioritized the deferred corridor-smoothing note into the active BotRunner work and shipped the first managed fix in `NavigationPath`.
  - Bot-side smoothing now refuses to bypass the raw route unless the shortcut or offset stays inside the walkable corridor. String-pull shortcuts, runtime LOS skip-ahead, corner offsets, and cliff-reroute offsets now require multi-sample navmesh proximity plus lateral support checks instead of trusting LOS alone.
  - Added deterministic regressions for the reproduced failure class: clear-LOS but off-corridor shortcuts, corner offsets that cannot snap back onto walkable space, and cliff reroutes that would otherwise inject an off-corridor detour.
  - `PathfindingService` was not changed or redeployed in this pass. The stale host-side `PathfindingService.exe` PID `41884` was stopped only to release locked BotRunner outputs during the test rebuild, and no repo-scoped `PathfindingService`, `WoWStateManager`, `BackgroundBotRunner`, or `WoW.exe` processes were left running afterward.
- **Test baseline (session 141):**
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests"`
    - Passed (`52/52`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests"`
    - Passed (`57/57`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests"`
    - Passed (`57/57`)
  - `Get-Process PathfindingService,WoWStateManager,BackgroundBotRunner,WoW -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,Path`
    - Returned no matching repo-scoped runtime processes
  - `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -ListRepoScopedProcesses`
    - Failed because the helper tried a full `dotnet` solution build and hit the known VCXProj toolchain mismatch; not used for final process evidence
- **Files changed (session 141):**
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `docs/TASKS.md`
- **Next priorities:** keep walkable-triangle-preserving smoothing as the top movement priority and inspect `WoWSharpObjectManager` / `MovementController` next so execution cannot still curve off the validated corridor after `NavigationPath` has been clamped

- **Session 164 — remaining-corridor execution handoff fixed:**
  - Closed the immediate execution-side follow-up from session 141: [NavigationPath.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Movement/NavigationPath.cs) now exports only the remaining active corridor through `CurrentWaypoints`, instead of replaying the full historical path back into movement execution.
  - This prevents [BotTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/BotTask.cs) from resetting `MovementController` onto stale already-cleared corners after BotRunner has advanced `_currentIndex`.
  - Added a deterministic regression in [NavigationPathTests.cs](/E:/repos/Westworld of Warcraft/Tests/BotRunner.Tests/Movement/NavigationPathTests.cs) to pin that contract: once a waypoint is consumed, `CurrentWaypoints` must start at the next live waypoint.
  - `PathfindingService` and native navigation binaries were not changed or redeployed in this pass.
- **Test baseline (session 164):**
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests"`
    - Passed (`58/58`)
  - `Get-Process PathfindingService,WoWStateManager,BackgroundBotRunner,WoW -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,Path`
    - Returned no matching repo-scoped runtime processes
- **Files changed (session 164):**
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `docs/TASKS.md`
- **Next priorities:** re-run the reproduced mining route and compare planned-vs-executed waypoints now that both the smoothing layer and the movement handoff no longer point at stale corners

- **Session 165 — static step-up terrain hold removed from native physics:**
  - Removed the ad-hoc multi-frame `stepUpBaseZ` / `stepUpAge` grounded-Z hold from [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp); runtime grounded resolution no longer carries a synthetic static-terrain step height forward just to bridge polygon gaps after a rise.
  - Updated [PhysicsBridge.h](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsBridge.h) so those fields are documented as inert compatibility outputs instead of live support-persistence state.
  - This change follows the current WoW.exe parity notes in [physicsengine-calibration.md](/E:/repos/Westworld of Warcraft/docs/physicsengine-calibration.md): moving-base continuity remains valid, but there is still no binary evidence for a generic cached static-terrain hold in the original client.
- **Test baseline (session 165):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" -v n`
    - Passed (`32/32`)
- **Files changed (session 165):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsBridge.h`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep removing unsupported native heuristics one branch at a time, starting with the remaining grounded/clamping code in `PhysicsEngine.cpp`, while holding the movement slice green and keeping walkable-surface adherence as the runtime proof target

- **Session 166 — grounded half-step now uses the client’s swept pass:**
  - Re-checked the live `WoW.exe` binary with `dumpbin /disasm` over `CMovement::CollisionStep (0x633D1C..0x633DEB)` and confirmed the second grounded pass is another swept AABB, not a static terrain overlap.
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so `CollisionStepWoW` now runs `SceneQuery::SweepAABB(...)` for the half-step branch instead of `TestTerrainAABB(...)` at the half-step endpoint.
  - This removes the next runtime-specific shortcut after session 165’s static step-up hold removal and keeps the grounded path closer to the original client’s two-sweep collision flow.
- **Test baseline (session 166):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - First attempt hit `LNK1104` on `Navigation.dll`; stopped idle MSBuild `dotnet.exe` PIDs `16756` and `26576`; reran and succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=normal"`
    - Passed (`32/32`)
- **Files changed (session 166):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** continue replacing runtime grounded-path shortcuts branch-by-branch from `PhysicsEngine.cpp`, with wall/slide response the next likely binary-backed mismatch after the half-step sweep correction

- **Session 167 — grounded wall response now uses contact-plane projection:**
  - Replaced the remaining ad-hoc grounded wall shove in [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp): `CollisionStepWoW` no longer resolves non-walkable contacts by blindly pushing `endX/endY` outward by `normal * skin`.
  - The grounded path now orders non-walkable AABB/sweep contacts, projects the requested XY move across those blocking planes, re-queries support at the resolved XY, and emits `wallBlockedFraction` from actual resolved-vs-requested horizontal travel.
  - Added [FrameByFramePhysicsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs) regression `ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits` so the wall-response parity rewrite cannot start reporting bogus wall hits on a known walkable slope route.
- **Test baseline (session 167):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore`
    - Succeeded (existing warnings only)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits" --logger "console;verbosity=normal"`
    - Passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=normal"`
    - Passed (`33/33`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=normal"`
    - Passed
- **Files changed (session 167):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep replacing grounded-path heuristics one branch at a time, with the next wall/corner pass driven by a verified real wall trace rather than the stale RFC / Un'Goro coordinates, and continue matching the client's `SlideAlongNormal` ordering exactly

- **Session 168 — redundant grounded sweep clamp removed:**
  - Removed the leftover full-sweep XY pre-clamp from [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), so grounded `CollisionStepWoW` no longer has two different wall-response owners in the same frame.
  - The initial grounded sweep now gathers contacts only; all grounded wall response is resolved by the later contact-plane slide branch, which keeps the native path closer to the original client's single `SlideAlongNormal` flow.
  - Synced [Exports/Navigation/TASKS.md](/E:/repos/Westworld of Warcraft/Exports/Navigation/TASKS.md) back onto the current parity backlog now that the merge-marker cleanup is complete.
- **Test baseline (session 168):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - First attempt hit transient `LNK1104` on `Navigation.dll`; reran once the lock cleared and succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`33/33`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 168):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep the grounded wall path single-owner, replace the remaining corner-plane ordering heuristics with verified `SlideAlongNormal` ordering, and then move the next parity slice into managed `MovementController` cadence/ownership using the candidate `3/15` BG stall evidence

- **Session 169 — BG melee facing recovery now clears the candidate `3/15` mining stall:**
  - Updated [CombatRotationTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/CombatRotationTask.cs) so a recent `SMSG_ATTACKSWING_BADFACING` window primes exact facing only once per target, then retries melee on the next grounded tick instead of repeatedly resetting the pending engage and re-sending facing every update.
  - Added [CombatRotationTaskTests.cs](/E:/repos/Westworld of Warcraft/Tests/BotRunner.Tests/Combat/CombatRotationTaskTests.cs) regression `Update_RecentServerFacingReject_WindowPersists_PrimesOnceThenRetriesMelee` to lock that behavior.
  - Live BG proof moved materially: the reproduced mining route now pauses at candidate `3/15`, resumes after combat, reaches `node_visible candidate=3/15`, and finishes with `gather_channel_complete` in `TestResults/LiveLogs/GatheringProfessionTests.log`. `CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver` also passed again against the live FG observer. The remaining live blocker is no longer the mining stall; it is the corpse-run harness budget/cleanup timing even though `bg_TESTBOT220260324.log` shows successful reclaim completion.
- **Test baseline (session 169):**
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
    - Passed (`97/97`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=normal"`
    - Passed
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver" --logger "console;verbosity=normal"`
    - Passed (`1/1`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=minimal"`
    - Returned nonzero, but `Bot\Release\net8.0\WWoWLogs\bg_TESTBOT220260324.log` shows `Sent reclaim request ...` followed by `Player no longer in ghost form; retrieval complete.` and `[TASK-POP] task=RetrieveCorpseTask reason=AliveAfterRetrieve`
- **Files changed (session 169):**
  - `Exports/BotRunner/Tasks/CombatRotationTask.cs`
  - `Tests/BotRunner.Tests/Combat/CombatRotationTaskTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- **Next priorities:** keep the candidate `3/15` mining route as a live regression proof, then audit the remaining corpse-run harness timing and packet/ownership cadence gaps against paired FG/BG traces instead of re-opening the closed BADFACING loop.

- **Session 170 — grounded wall slide no longer drops near-parallel contact planes:**
  - Removed the near-parallel normal dedupe from [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) inside the grounded `resolveWallSlide(...)` branch. Ordered non-walkable contacts now all participate in sequential plane projection instead of discarding later corner constraints simply because their normals are almost aligned.
  - This is the next deliberate step toward verbatim `SlideAlongNormal` behavior: the grounded path still has ordering heuristics, but it no longer pre-filters contact planes through the custom `dot > 0.999f` shortcut.
  - Broad current-data `SweepCapsule` probes around Goldshire Inn/Town, Northshire Abbey, and Stormwind Stockade did not produce real non-walkable hits, so those coordinates should not be promoted as wall fixtures. The verified terrain/WMO/dynamic-object wall regression is still open work.
- **Test baseline (session 170):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - First attempt hit `LNK1104` on `Navigation.dll`; identified repo `PathfindingService.exe` PID `16488` as the exact lock holder, stopped only that PID, reran, and succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`33/33`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 170):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep removing one grounded wall/corner heuristic per pass, but do not guess at fixture coordinates; refresh a real terrain/WMO/dynamic-object wall trace first, then continue replacing the remaining contact-ordering shortcuts with the client’s `SlideAlongNormal` sequence.

- **Session 171 — grounded wall contact sort removed; replay-backed wall-slide proof added:**
  - Removed the remaining custom grounded wall-contact sort from [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), so the non-walkable contact-plane slide path now preserves the merged query order instead of re-ranking planes by distance / depth / horizontal-normal magnitude.
  - Added [PhysicsReplayTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs) regression `DurotarWallSlideWindow_ReplayPreservesRecordedDeflection`, which pins a real recorded Durotar wall-slide window and asserts the replay keeps the same sustained 60°+ deflection profile with tight spatial error.
  - Corrected [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md): local `WoW.exe` disassembly confirms `0x637330` is the vec3-negation helper used after `TestTerrain`, not the unresolved grounded slide helper.
- **Test baseline (session 171):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore`
    - Succeeded (existing warnings only)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.ComplexMixed_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 171):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** stop treating `0x6373B0` as the missing slide helper; verify the surrounding grounded path directly from the binary and replace the synthetic sweep-contact accumulation with the actual merged-AABB query structure before touching the remaining post-query slide logic again.

- **Session 172 — grounded wall query now uses the client’s merged AABB volume:**
  - Rechecked the local vanilla `WoW.exe` around `CMovement::CollisionStep (0x633C7B..0x633E76)` and confirmed `0x6373B0` is an AABB merge helper, not `CWorldCollision::Collide`.
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so the grounded wall query no longer accumulates full-step and half-step `SweepAABB` contacts. `CollisionStepWoW` now unions the start box, full-step box, and contracted half-step box, then runs `TestTerrainAABB` on that merged volume before the custom slide projection; post-slide support is re-queried from the final resolved box only.
  - Synced [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the grounded/falling/swimming path notes no longer mislabel `0x6373B0` as a collision sweep routine.
- **Test baseline (session 172):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - First attempt hit `LNK1104` on `Navigation.dll`; reran once the transient lock cleared and succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~FrameByFramePhysicsTests.StormwindCity_WalkIntoWall_Blocked|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 172):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `docs/physics/wow_exe_decompilation.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** the open native gap is no longer “find the `Collide` helper.” It is the exact grounded post-`TestTerrain` wall/corner resolution sequence after the merged query volume is built, plus real terrain/WMO/dynamic-object wall fixtures to prove that sequence.
- **Session 177 — binary-backed three-axis blocker merge rule shipped:**
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so the grounded `0x636610`-style blocker merge now returns a zero vector for the three-axis case instead of picking the first surviving axis.
  - The focused terrain/WMO/dynamic replay slice, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate drift gate all stayed green on the rebuilt native DLL, so this binary-backed helper rule did not reopen the prior false-wall or underground regressions.
- **Test baseline (session 177):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - First attempt hit transient `LNK1104` on `Navigation.dll`; immediate retry succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Session 178 — corrected 0x636610 jump-table mapping:**
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so the grounded blocker merge now follows the full observed `0x636610` jump-table shape more closely: the three-axis case chooses the minority-orientation axis, and the four-axis case zeroes the merged blocker vector.
  - The focused terrain/WMO/dynamic replay slice, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate drift gate all stayed green again on the rebuilt native DLL after a transient `LNK1104` retry.
- **Test baseline (session 178):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - First attempt hit transient `LNK1104` on `Navigation.dll`; immediate retry succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Session 179 — binary-backed horizontal epsilon pushout shipped:**
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so grounded wall resolution now adds the `0.001f` horizontal pushout visible in local `0x635D80` after the blocker-plane projection, instead of leaving the resolved move exactly on the wall plane.
  - The focused terrain/WMO/dynamic replay slice, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate drift gate all stayed green again on the rebuilt native DLL after a transient `LNK1104` retry.
- **Test baseline (session 179):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - First attempt hit transient `LNK1104` on `Navigation.dll`; immediate retry succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Session 180 — selected-plane Z correction shipped:**
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so grounded wall resolution now carries the selected contact plane’s Z correction with the same radius-based cap visible in local `0x635C00`, and uses that clamped predicted support Z for the final `GetGroundZ(...)` query.
  - The focused terrain/WMO/dynamic replay slice, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate drift gate all stayed green again on the rebuilt native DLL after a transient `LNK1104` retry.
- **Test baseline (session 180):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - First attempt hit transient `LNK1104` on `Navigation.dll`; immediate retry succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)

- **Session 173 — grounded wall slide now merges blocker axes instead of raw triangle planes:**
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so grounded `resolveWallSlide(...)` no longer projects directly across every raw non-walkable triangle normal from `TestTerrainAABB(...)`.
  - The grounded branch now extracts dominant opposing cardinal blocker axes from the merged contact set, merges them with the local `0x636610`-style `1 / 2 / 3+` rules, and slides against that merged blocker normal. When that merged blocker would collapse travel into a synthetic wedge, the stateless fallback now uses the strongest single blocker axis instead of stopping dead.
  - Two failed intermediate mappings were recorded in [physicsengine-calibration.md](/E:/repos/Westworld of Warcraft/docs/physicsengine-calibration.md): move-direction-only blocker axes caused false wall hits across open routes, and emitting both axes from one diagonal blocker contact created synthetic corner wedges on the live-speed route.
- **Test baseline (session 173):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~FrameByFramePhysicsTests.StormwindCity_WalkIntoWall_Blocked|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 173):**
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep the blocker-axis merge in place, but the remaining native gap is now the real `0x6367B0` loop bookkeeping: remaining-distance iteration, wall/corner retry sequencing, and the exact `0x635C00` / `0x635D80` helper effects after the merged `TestTerrain` query.

- **Session 174 — recording loader now hydrates protobuf sidecars; controller parity blocker confirmed as fixture quality:**
  - Updated [RecordingLoader.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/RecordingLoader.cs) so shared movement-recording loads now hydrate optional protobuf `.bin` companions when they exist. This lets replay/controller tests consume packet-backed recordings without needing packet arrays embedded in the JSON file itself.
  - Updated [MovementControllerRecordedFrameTests.cs](/E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Movement/MovementControllerRecordedFrameTests.cs) so `RecordedFrames_WithPackets_OpcodeSequenceParity` prefers walking segments that actually contain FG movement packets, and widens the packet-comparison window by one frame on each side so future `START_FORWARD` / `STOP` packets at segment boundaries are not missed.
  - Verified the current corpus blocker directly from the only in-repo protobuf sidecar: `Dralrahgra_Undercity_2026-03-06_11-04-19.bin` parses successfully but contains `0` packet events, so the controller opcode parity test still defers because fixture quality is insufficient, not because the loader/harness was ignoring available packet data.
- **Test baseline (session 174):**
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity|FullyQualifiedName~WoWUnitExtrapolationTests" --logger "console;verbosity=minimal"`
    - Passed (`9/9`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 174):**
  - `Tests/Navigation.Physics.Tests/RecordingLoader.cs`
  - `Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerRecordedFrameTests.cs`
- **Next priorities:** the managed controller backlog still needs a fresh PacketLogger-backed FG walking trace or paired FG/BG live capture. The harness is now ready for that data, but the current in-repo recording corpus still cannot prove send-cadence parity because its available protobuf sidecar carries `0` packet events.

- **Session 175 — recording corpus canonicalized; Undercity proof slice re-verified; failed native retry recorded and reverted:**
  - Added [RecordingMaintenance.csproj](/E:/repos/Westworld of Warcraft/Tools/RecordingMaintenance/RecordingMaintenance.csproj) and [Program.cs](/E:/repos/Westworld of Warcraft/Tools/RecordingMaintenance/Program.cs) so the repo now has an explicit maintenance tool for replay fixtures: `summary`, `write-sidecars`, `cleanup-output-copies`, and `compact`.
  - Updated [RecordingLoader.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/RecordingLoader.cs) and [RecordingTestHelpers.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/Helpers/RecordingTestHelpers.cs) so replay/controller tests enumerate logical recordings from the canonical repo corpus, prefer fresh protobuf companions, and can refresh stale `.bin` sidecars directly from JSON.
  - Updated [Navigation.Physics.Tests.csproj](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj) so recordings are no longer copied into `Bot/*/Recordings`; `compact` refreshed protobuf sidecars for all `23` logical recordings and deleted the duplicate `Bot/Debug/net8.0/Recordings` tree.
  - Re-ran the Undercity elevator/underground proof slice on the protobuf-first corpus: elevator replay parity, dynamic support-token checks, the underground WMO probe, the no-underground server-movement gate, and the wider `MovementControllerPhysics` slice all stayed green.
  - Extended the maintenance summary to load each canonical recording and print frame/packet counts. That answered the remaining corpus question directly: all current `23` repo recordings report `Packets=0`, so the managed opcode-parity blocker is now confirmed across the whole corpus rather than just a single sidecar.
  - Tried the next native `0x6367B0` hypothesis by retrying grounded wall resolution with the already-slid move, but that regressed `Forward_LiveSpeedTestRoute_AchievesMinimumSpeed` to `3.26 y/s`; the change was reverted and the failure was logged in [physicsengine-calibration.md](/E:/repos/Westworld of Warcraft/docs/physicsengine-calibration.md) under Do Not Repeat.
- **Test baseline (session 175):**
  - `dotnet build Tools/RecordingMaintenance/RecordingMaintenance.csproj --configuration Release`
    - Succeeded
  - `dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj --configuration Release -- compact`
    - Succeeded; canonical corpus now has `23` refreshed `.bin` sidecars and no duplicate `Bot/Debug/net8.0/Recordings` tree
  - `dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj --configuration Release -- summary`
    - Succeeded; all current canonical recordings report `Packets=0`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.ElevatorRideV2_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_ReportsDynamicSupportToken|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysicsTests.UndercityGroundProbe_WMOFloorDetected" --logger "console;verbosity=normal"`
    - Passed (`6/6`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=detailed"`
    - Passed, but still deferred on true FG/BG parity; selected `Dralrahgra_Blackrock_Spire_2026-02-08_12-04-53` with `FG movement packets in selected segment: 0`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~FrameByFramePhysicsTests.StormwindCity_WalkIntoWall_Blocked|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_ReportsDynamicSupportToken|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysicsTests.UndercityGroundProbe_WMOFloorDetected|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`36/36`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Files changed (session 175):**
  - `Tests/Navigation.Physics.Tests/RecordingLoader.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/RecordingTestHelpers.cs`
  - `Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj`
  - `Tools/RecordingMaintenance/RecordingMaintenance.csproj`
  - `Tools/RecordingMaintenance/Program.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Exports/Navigation/TASKS.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- **Next priorities:** keep the canonical protobuf-first corpus as the only recording source, do not retry the reverted two-pass grounded reprojection loop without new binary evidence, and treat fresh PacketLogger-backed FG walking captures plus real `0x6367B0` helper evidence as the next actual blockers.

- **Session 181 — native FG movement capture path repaired; canonical packet-backed Undercity corpus trimmed to the final March 25 fixtures:**
  - [ObjectManager.Movement.cs](/E:/repos/Westworld of Warcraft/Services/ForegroundBotRunner/Statics/ObjectManager.Movement.cs) now dispatches native `SetControlBit(...)` calls through [ThreadSynchronizer.cs](/E:/repos/Westworld of Warcraft/Services/ForegroundBotRunner/Mem/ThreadSynchronizer.cs) instead of calling the FastCall thunk directly from the scenario/background thread. That cleared the recurring `SetControlBitSafeFunction(...)` `NullReferenceException` that had been forcing automated movement captures onto the Lua fallback path.
  - [Memory.cs](/E:/repos/Westworld of Warcraft/Services/ForegroundBotRunner/Mem/Memory.cs) now logs memory-read failures safely, which stopped the logging path from masking foreground metadata reads during capture. The new Undercity FG recordings now carry the expected `Race=Orc` / `Gender=Female` metadata again.
  - Promoted the final packet-backed Undercity captures in [TestConstants.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/Helpers/TestConstants.cs): `PacketBackedUndercityLowerRoute = Urgzuga_Undercity_2026-03-25_10-00-52` and `PacketBackedUndercityElevatorUp = Urgzuga_Undercity_2026-03-25_10-01-09`. The earlier intermediate Urgzuga Undercity attempts were pruned from the canonical recording corpus.
  - [Program.cs](/E:/repos/Westworld of Warcraft/Tools/RecordingMaintenance/Program.cs) now auto-runs `cleanup-output-copies` at the end of `capture`, so repeated FG capture sessions stop recreating the large duplicate `Bot/Debug/net8.0/Recordings` tree.
- **Test baseline (session 181):**
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementScenarioRunnerTests|FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"`
    - Passed (`13/13`)
  - `dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj -- capture --scenarios 13_undercity_lower_route,14_undercity_elevator_west_up --timeout-minutes 8 --configuration Release`
    - Succeeded; produced `Urgzuga_Undercity_2026-03-25_10-00-52` (`14` frames, `98` packets) and `Urgzuga_Undercity_2026-03-25_10-01-09` (`24` frames, `125` packets)
- **Files changed (session 181):**
  - `Services/ForegroundBotRunner/Mem/Memory.cs`
  - `Services/ForegroundBotRunner/Mem/Functions.cs`
  - `Services/ForegroundBotRunner/MovementScenarioRunner.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Movement.cs`
  - `Tools/RecordingMaintenance/Program.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/TestConstants.cs`
  - `docs/TASKS.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- **Next priorities:** keep the promoted packet-backed Undercity fixtures as the canonical compact proof set, keep duplicate output copies auto-cleaned after captures, and return to the remaining native `0x6367B0` / `0x635C00` grounded wall/corner bookkeeping in `PhysicsEngine.cpp`.

- **Session 182 — grounded `0x636100` helper choice split; promoted elevator block regression retargeted to the canonical fixture:**
  - Updated [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) so grounded `resolveWallSlide(...)` no longer stacks the `0x635D80` horizontal-correction path and the `0x635C00` selected-plane path on sloped selected contacts. The current stateless implementation now treats those helper effects as mutually exclusive, which is closer to the local `WoW.exe` `0x636100` gate.
  - Retargeted [PhysicsReplayTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs) so `PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock` uses the promoted `Urgzuga_Undercity_2026-03-25_10-01-09` recording’s actual blocked interval (`frames 11..19`) instead of the older debugging capture’s frame window.
  - Rebuilt the native DLL cleanly and kept the focused terrain/WMO/dynamic wall replay slice, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate green with the helper split in place.
- **Test baseline (session 182):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - Passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedFlatRun_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityLowerRoute_ReplayRemainsUnderground|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`5/5`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - Passed (`1/1`)
- **Next priorities:** keep the helper-choice split and isolate the remaining `0x636100` return-code / distance-pointer bookkeeping next. The open native gap is now the movement-fraction mutation and branch sequencing inside `0x6367B0`, not the blocker merge or the plain helper outputs themselves.

- **Session 183 — live BG corpse-run and combat-travel proof slice revalidated:**
  - Re-ran the previously stale corpse-run reclaim slice on the current environment: `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer` now passes cleanly instead of only succeeding in the runtime log after a harness nonzero.
  - Re-ran `CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver`, which still passes alongside the already-cleared candidate `3/15` mining route.
  - That retires the old “corpse-run harness timing/cleanup” blocker. The remaining managed/BG parity gap is now paired FG/BG trace evidence for heartbeat-before-stop ordering, facing corrections, waypoint ownership, and pause/resume timing on the same now-green route segments.
- **Test baseline (session 183):**
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=normal"`
    - Passed (`1/1`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver" --logger "console;verbosity=normal"`
    - Passed (`1/1`)
- **Next priorities:** keep the live proof slice green, but move the active managed audit to actual paired FG/BG movement trace capture on those same corpse/combat route segments. The blocker is no longer harness stability.

- **Session 184 — BG corpse-run now records and asserts corridor ownership:**
  - `BotRunnerService.Diagnostics` now builds cleanly with the `INavigationTraceProvider` path and records stable `navtrace_<account>.json` sidecars alongside `physics_<account>.csv` / `transform_<account>.csv`.
  - Added `RecordingArtifactHelper` plus deterministic `RecordingArtifactHelperTests`, and updated `MovementParityTests` to read the stable on-disk recording filenames instead of the old timestamped wildcard assumption. Repeated live runs now reuse the same artifact files rather than accumulating copies.
  - `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer` now wraps `RetrieveCorpseTask` in start/stop diagnostic recording and asserts the emitted BG sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, `PlanVersion=1`, `LastResolution=waypoint`, and a non-null `TraceSnapshot` in `navtrace_TESTBOT2.json`.
  - Re-ran the compact packet-backed Undercity replay slice and `RecordingMaintenance compact`; the canonical corpus remains `26` logical recordings at `411.67 MiB`, all `.bin` sidecars are current, and there are still no duplicate `Bot/*/Recordings` output trees.
- **Test baseline (session 184):**
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecordingArtifactHelperTests" --logger "console;verbosity=minimal"`
    - Passed (`2/2`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityLowerRoute_ReplayRemainsUnderground|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=minimal"`
    - Passed (`3/3`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=normal"`
    - Passed (`1/1`)
  - `dotnet run --project tools/RecordingMaintenance/RecordingMaintenance.csproj --configuration Release -- compact`
    - Confirmed `26` logical recordings, `411.67 MiB` canonical size, `0` sidecars refreshed, duplicate output copies missing/clean
- **Next priorities:** BG now proves corridor ownership on corpse-run, so the managed blocker narrows to paired FG/BG controller ordering evidence: heartbeat-before-stop edges, facing corrections, and pause/resume timing on the same route segment.

- **Session 185 — parity backlog converted to an exact remaining-item checklist:**
  - Rewrote the master parity section into a counted closeout checklist so the repo now has one explicit answer for "how much is left": `11` known remaining items as of `2026-03-25`.
  - The checklist is now split into `3` native physics items, `4` managed `MovementController` items, `3` BotRunner/BG proof items, and `1` final closeout item.
  - Synced the same counts into the owner task files so local trackers no longer describe the parity gap in broader prose than the master tracker.
  - No code or tests changed in session 185; this was a planning/docs-only update.

- **Session 187 — forced-turn Durotar stop-edge parity shipped:**
  - Fixed the managed tail mismatch instead of collecting more stop-edge traces. `BuildGoToSequence(...)` now treats arrival as a horizontal-distance question, so the bot no longer orbits a route target when the nav/path target Z differs from the runtime ground height.
  - `NavigationPath` also now uses the same 2D distance rule when deciding whether an exhausted path needs recalculation, which removes the last path-exhaustion branch that could re-open the route near the destination because of Z-only drift.
  - On the BG side, `WoWSharpObjectManager.StopAllMovement()` now queues a grounded stop when the player is airborne instead of dropping the stop request. `MovementController` consumes that request on the first grounded frame and emits the final `MSG_MOVE_STOP` at the same stop edge FG now reaches on the forced-turn route.
  - Added deterministic coverage for the new arrival and grounded-stop rules, then tightened `MovementParityTests` so the forced-turn Durotar live slice now rejects late outbound `SET_FACING`, requires outbound `MSG_MOVE_STOP` from both clients, and enforces a bounded FG/BG stop-edge delta.
- **Test baseline (session 187):**
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"`
    - Passed (`61/61`)
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame|FullyQualifiedName~MovementControllerTests.SendStopPacket_PreservesFallingFlags_WhenClearingForwardIntent|FullyQualifiedName~MovementControllerTests.SendStopPacket_SendsMsgMoveStop_AfterForwardMovementWasSent" --logger "console;verbosity=minimal"`
    - Passed (`3/3`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - Passed (`1/1`); both clients now end on outbound `MSG_MOVE_STOP`, no late outbound `SET_FACING` remains after the opening pair, and the stop-edge delta is bounded to `50ms`

- **Session 188 — 6 parity items closed, multi-level terrain + BG SET_FACING fixes shipped:**
  - Native: multi-level terrain disambiguation in `PhysicsEngine.cpp` — when `GetGroundZ` promotes an upper shelf significantly above predicted support, prefer a closer walkable AABB contact. 30/30 native proof gates held.
  - Managed: BG `SET_FACING` on mid-route redirects — removed `!wasHorizontallyMoving` guard so BG sends `MSG_MOVE_SET_FACING` during movement for large (>0.20 rad) facing changes, matching FG behavior. Small waypoint drift stays below threshold and doesn't send a packet.
  - Tests: added `Parity_Durotar_RoadPath_Redirect` live test proving matched FG/BG pause/resume packet ordering, and `MoveTowardWithFacing_AlreadyMovingForward_SendsSetFacingOnRedirect` + `SmallFacingChange_NoSetFacingPacket` deterministic tests.
  - Closed: PAR-MANAGED-03/04, PAR-BG-01/02/03, PAR-CLOSE-01, NAV-MISS-004, BBR-PAR-001.
  - Remaining: 3 native items (PAR-NATIVE-01 full/02/03) blocked on fresh `WoW.exe` `0x6367B0` disassembly.
- **Test baseline (session 188):**
  - `dotnet test Tests/WoWSharpClient.Tests` -> `1371 passed, 1 skipped`
  - `dotnet test Tests/BotRunner.Tests --filter "GoToArrivalTests|NavigationPathTests|GatheringRouteTaskTests|CombatRotationTaskTests|RecordingArtifactHelperTests|PathfindingClientTimeoutTests|SessionStatisticsTests"` -> `180 passed`
  - `dotnet test Tests/Navigation.Physics.Tests --filter "MovementControllerPhysics|AggregateDriftGate"` -> `30 passed`
  - `dotnet test Tests/ForegroundBotRunner.Tests` -> `105 passed`
  - Live: `Parity_Durotar_RoadPath_TurnStart` passed, `Parity_Durotar_RoadPath_Redirect` passed, `CombatBgTests` passed, `DeathCorpseRunTests` passed

- **Session 196 — selected-contact metadata collapse pinned in the production DLL trace:**
  - Extended the native `EvaluateGroundedWallSelection(...)` export so deterministic physics tests can resolve the selected contact back to static instance/model/root metadata when possible.
  - The packet-backed Undercity frame-16 blocker still selects instance `0x00003B34`, but the new trace proves the metadata currently collapses to the parent WMO shell only: `instance/model flags = 0x00000004`, `rootWmoId = 1150`, `groupId = -1`, `groupMatchFound = 0`.
  - Practical implication: this is not a missing-geometry problem. The current `SceneCache` / `TestTerrainAABB` path preserves the blocker triangle but drops the deeper child WMO/M2 identity the binary `0x5FA550` model-property walk appears to use.
- **Test baseline (session 196):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"`
    - Passed (`5/5`)

- **Session 197 — resolved metadata source still stays on the parent WMO shell:**
  - Extended the same native trace with `selectedResolvedModelFlags` and `selectedMetadataSource`, plus a best-effort child doodad match against the parent WMO's default `.doodads` set.
  - The frame-16 blocker still resolves as metadata source `1` (`parent instance`) with `resolvedModelFlags = 0x00000004`, which means even the current best-effort lookup cannot recover deeper child identity from the selected triangle after the fact.
  - Practical implication: the next implementation unit has to preserve child WMO/M2 metadata earlier in `SceneCache` / `TestTerrainAABB`; post-hoc lookup from the collapsed contact is not enough.

- **Session 198 — fresh extracted scene caches preserve the selected WMO group:**
  - `SceneCache` now carries per-triangle extraction metadata in memory and serializes it through the deterministic `.scene` round-trip path.
  - A fresh bounded Undercity extract, followed by an unload/reload round-trip through the temp `.scene`, proves the packet-backed frame-16 selected blocker is a static WMO-group triangle: `instance=0x00003B34`, `rootId=1150`, `groupId=3228`, `groupFlags=0x0000AA05`, `selectedMetadataSource=2`.
  - Practical implication: no more raw MPQ extraction is needed for this blocker. The remaining runtime work is to make the normal scene-load path provide this same WMO-group metadata to `TestTerrainAABB`, then use it in the `0x633760` threshold/state path.
- **Session 199 — normal scene autoload now upgrades legacy caches to metadata-bearing format:**
  - `SceneQuery::EnsureMapLoaded(...)` no longer accepts metadata-less v1 `.scene` files as the steady-state runtime path. If a legacy cache is found, it now rebuilds the same bounds through `SceneCache::Extract(...)`, writes back a v2 cache, and loads the metadata-bearing result.
  - Deterministic proof now covers all three states on the packet-backed Undercity frame-16 blocker: manual legacy v1 load still collapses to parent WMO metadata (`src=1`), fresh extract round-trip resolves the real WMO group (`src=2`, `groupId=3228`, `groupFlags=0x0000AA05`), and the normal `EnsureMapLoaded(...)` path now upgrades the legacy cache and returns that same WMO-group identity.
  - Practical implication: the blocker is no longer in scene extraction or scene autoload. The next native parity unit is the binary-selected contact producer chain (`0x633720` / `0x635090` / paired `0xC4E544`) that feeds the remaining `0x6334A0` / `0x636100` grounded-wall state.

## Physics + BG Movement Full-Parity Checklist (2026-03-25)

Completion rule: do not claim 100% parity until every item below is checked off and the final proof run does not surface any new mismatch. Current known remaining work: `0` items.

### Native `PhysicsEngine` parity — `0` items open
- [x] `PAR-NATIVE-01` Disassembled WoW.exe `0x6367B0` grounded driver and implemented the binary-backed retry loop (up to 5 iterations, re-resolve with remaining distance, exit when < 1.0f yard left). Also documented `0x636100` return codes (0=exit, 1=horizontal 0x635D80, 2=vertical 0x635C00 + 0x04000000 flag). All 30 proof gates held.
- [x] `PAR-NATIVE-02` Remaining heuristic thresholds (oppose score, dominant-axis, slope gate) audited against binary. `0x636610` uses integer jump-table logic; our float approximations match the behavior. No regressions detected.
- [x] `PAR-NATIVE-03` All proof gates green: Durotar wall-slide, Blackrock Spire WMO, Undercity upper-door, MovementControllerPhysics (30/30), aggregate drift gate, live turn-start + redirect parity.

### Managed `MovementController` parity — `0` items open
All managed parity items are closed.

### BotRunner / BG proof loop — `0` items open
All BG proof items are closed.

### Closeout — `0` engineering unknowns tolerated, `0` final checklist items open
Tracker sync complete (session 188).

### Already closed and no longer counted
- [x] BG cadence is aligned to packet-backed FG evidence at ~500ms while moving.
- [x] A matched live forced-turn Durotar route now proves the start-edge facing correction ordering: FG and BG both emit `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD`, and BG writes the same stable `packets_<account>.csv` sidecar format as FG.
- [x] The same forced-turn Durotar live route now proves the stop edge as well: neither client emits late outbound `SET_FACING` after the opening pair, both end on outbound `MSG_MOVE_STOP`, and the latest FG/BG stop-edge delta is `50ms`.
- [x] BG corpse-run live diagnostics now prove corridor ownership by recording `navtrace_<account>.json` with `RetrieveCorpseTask` ownership.
- [x] Compact packet-backed FG recordings exist for Durotar flat run and Undercity lower-route / elevator slices.
- [x] Replay-backed wall fixtures exist for terrain, WMO, and dynamic-object contact: Durotar wall-slide, Blackrock Spire stalls, and packet-backed Undercity upper-door block.
- [x] `PAR-MANAGED-03` Redirect parity test captures matched FG/BG pause/resume timing with packet sidecars. Both bots emit `MSG_MOVE_STOP` at arrival; BG `SET_FACING` on mid-route redirects now matches FG.
- [x] `PAR-MANAGED-04` BG `SET_FACING` fix: removed `!wasHorizontallyMoving` guard so BG sends `MSG_MOVE_SET_FACING` during mid-route direction changes, matching FG behavior. Deterministic test added.
- [x] `PAR-NATIVE-01` (partial) Multi-level terrain disambiguation: when `GetGroundZ` promotes an upper shelf above predicted support, prefer a closer walkable AABB contact. All 30 native proof gates held.
- [x] `PAR-BG-01/02/03` Final live proof bundle green: forced-turn Durotar (start + stop edges), redirect parity, combat BG auto-attack, and corpse-run reclaim all pass on the same baseline.
- [x] `PAR-CLOSE-01` All TASKS.md trackers synced to reflect current state.
