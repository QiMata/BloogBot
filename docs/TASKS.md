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
| 7.9 | Add second Orgrimmar transport recording/parity test | Open — repo only has an older zeppelin capture with no in-flight `NearbyGameObjects`, so full transport replay remains blocked on better recording data |
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
  - `7.9` additional Orgrimmar transport replay coverage: the repo contains `Dralrahgra_Durotar_2026-02-08_11-06-02` (Orgrimmar zeppelin) but it loses dynamic object snapshots as soon as boarding starts, so only the ground-side transition windows can be replayed today.

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
- **Last updated:** 2026-03-23 (session 145)
- **Branch:** `main`
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
- **Next priorities:** `7.9` additional transport replay data and a final movement/packet parity sweep for anything still only decompiled but not binary-backed
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
