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

## Session Handoff
- **Last updated:** 2026-03-22 (session 129)
- **Branch:** `main` (merged from `cpp_physics_system`)
- **Session 129 completed:**
  - Merged `cpp_physics_system` → `main` (131+ conflict resolutions)
  - Phases 5-6: FG hardening + opcode coverage sweep
  - FG auto-attack toggle spam fix (`IsAutoAttacking` tracks state)
  - LedgeGuard removed (WoW.exe 0x633840 doesn't have one)
  - Uphill ground detection: DOWN pass accepts walkable surfaces above originalZ
  - Slope climbing: SIDE pass preserves walkable slope normals for 3D slide
  - **Terrain-following SIDE pass**: `GetGroundZ` after each advance keeps capsule above terrain
  - **Ground contact persistence**: ray probe rescues false-airborne on capsule sweep miss
  - **Face() BADFACING bug**: negative facing caused early return without targeting mob
  - **Combat test PASSES**: BG bot approaches, faces, kills Vile Familiar
  - Speed test flaky (passes sometimes, fails on steep routes — FALLINGFAR oscillation)
  - Bot still partially underground on slopes (GetGroundZ data vs WoW.exe heightmap mismatch)
- **Test baseline (session 129):** BasicLoop 2/2, CharacterLifecycle 1/1, EquipmentEquip 1/1, CombatBg 1/1, RFC 1/2, GatheringRoute 6/6, MovementSpeed FLAKY
- **Next priorities:** Fix underground Z, eliminate FALLINGFAR oscillation, run full test sweep
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
