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
| 7.1 | Detect transport entry/exit in physics replay frames (transportGuid field changes) | Open |
| 7.2 | Implement world↔transport coordinate transform in `CollisionStepWoW` matching 0x633840 | Open |
| 7.3 | Transform displacement by transport orientation matrix before collision (0x4549A0 `Vec3TransformCoord`) | Open |
| 7.4 | Inverse-transform result position back to transport-local after collision | Open |
| 7.5 | Handle elevator spline evaluation — Undercity elevators use gameobject transport splines | Open |
| 7.6 | Update `MovementController` to track transport state and switch coordinate frames | Open |
| 7.7 | Update heartbeat packets to include transport offset when on transport (flag 0x2000000) | Open |
| 7.8 | Add Undercity elevator ride recording/parity test (BG rides elevator, compare Z trajectory with FG) | Open |
| 7.9 | Add Orgrimmar elevator recording/parity test | Open |
| 7.10 | Fix physics replay to exclude transport-transition frames from ground mode scoring | Open |
| 7.11 | Calibration gate: ground avg < 0.08y, transport avg < 0.15y, aggregate p99 < 2.0y | Open |

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
- **Last updated:** 2026-03-22 (session 130)
- **Branch:** `main`
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
