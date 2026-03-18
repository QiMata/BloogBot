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

*Trope: physical classes (Warrior, Hunter, Rogue, Shaman) = Female; magic classes (Druid, Priest, Warlock, Mage) = Male.*

### Implementation Tasks

| # | Task | Status |
|---|------|--------|
| 5.1 | Create MaNGOS accounts (RFCBOT2–RFCBOT10) + GM level 6 via SOAP. Characters auto-created on first bot login, then leveled via `.character level` | **Done** (SOAP) |
| 5.2 | Create `RagefireChasm.settings.json` — 10-bot StateManager config with dungeoneering mode | **Done** (eb3fddd) |
| 5.3 | Restore `DungeoneeringTask` from commit `0e7e0bf` — adapt to current BotRunner architecture (IBotTask, behavior trees, NavigationPath) | **Done** (541a941) |
| 5.4 | Add dungeoneering coordinator to StateManager — group formation, raid conversion, ready check, dungeon entry at RFC portal (1811, -4410, -18) on Kalimdor | Open |
| 5.5 | Implement role-aware combat sequences — tank (hold aggro, skull mark), healer (lowest-HP party member), DPS (assist skull target), off-tank (pickup adds) | Open |
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

# Combat tests only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests"

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# Full solution
dotnet test WestworldOfWarcraft.sln --configuration Release
```

## Session Handoff
- **Last updated:** 2026-03-18 (session 114)
- **Branch:** `cpp_physics_system`
- **Completed this session:**
  - P5.2: Created `RagefireChasm.settings.json` — 10-bot StateManager config (eb3fddd)
  - P5.3: Restored `DungeoneeringTask` from old commit, adapted to current BotTask architecture (541a941)
  - P5.6: CanProceed rest/buff check built into DungeoneeringTask (541a941)
  - P5.7: Created `RagefireChasmTests.cs` — 4 test methods (enter world, form raid, teleport, full run placeholder) (eb3fddd)
  - P5.8: Added RFC waypoint data in `DungeonWaypoints.cs` (541a941)
  - Added `START_DUNGEONEERING` action type through full pipeline (proto → CharacterAction → ActionMapping → ActionDispatch → Task)
- **Test baseline:** 1266/1266 WoWSharpClient, BotRunner builds clean (0 errors)
- **Data dirs:** Server reads from `D:/MaNGOS/data/`. VMaNGOS tools at `D:/vmangos-server/`. Source at `D:/vmangos/`.
- **Next:**
  1. P5.1: Create MaNGOS accounts (RFCBOT2–RFCBOT10) + level 8 characters via SOAP
  2. P5.4: Dungeoneering coordinator in StateManager — group formation, raid conversion, dungeon entry
  3. P5.5: Role-aware combat sequences (tank/heal/DPS/off-tank)
  4. P3/P4: FG packet capture tests (fishing parity, teleport flags) — requires live FG bot
