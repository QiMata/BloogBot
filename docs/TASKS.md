# Tasks

> **Reference docs:**
> - `docs/ARCHIVE.md` — Completed task history (code-complete items, unit test inventory)
> - `CLAUDE.md` — Architecture overview, build commands, code search guide

## Goal

Two WoW 1.12.1 clients: **Injected** (ForegroundBotRunner inside WoW.exe) and **Headless** (WoWSharpClient, standalone). Both must produce identical behavior for all supported operations. Coordinate both bots to work as a group.

**Completion criteria:** Live-tested on the running Mangos server with both clients. Unit tests alone do NOT qualify as "DONE" — features must be validated end-to-end.

---

## Test Architecture Rules

**These rules govern ALL tests in the project. No exceptions without updating this list.**

1. **Live server tests MUST launch WoWStateManager.** No test should create `WoWClient`, `BackgroundBotRunner`, or `ForegroundBotRunner` directly. StateManager orchestrates everything.

2. **Live server test config MUST launch BOTH a headless client AND an injected client.** The `appsettings.test.json` must define two `CharacterSettings` entries: one `RunnerType = Background`, one `RunnerType = Foreground`. Every live test validates both paths produce identical results.

3. **Unit tests for protocol/ObjectManager only.** OpCode processing, packet parsing, how packets affect ObjectManager state — these are unit tests with mocks and recorded `.bin` packet files. No live server needed.

4. **Unit tests for physics/navigation only.** PhysicsEngine calibration through `Navigation.dll` P/Invoke — these are unit tests with the physics fixture. No live server needed.

5. **Live server fixture setup = enter world complete.** The fixture handles: start StateManager → StateManager launches both bots → both bots authenticate + enter world → fixture is ready. Tests start with characters already in-world.

6. **Assertions at each major step.** Tests must assert after every significant action (buy item, learn spell, equip gear), not just one final assert at the end.

7. **No test launches services directly.** Tests must NOT spawn `PathfindingService`, `DecisionEngineService`, `ForegroundBotRunner`, or `BackgroundBotRunner` as processes. All service lifecycle is managed by `WoWStateManager`.

---

## 0. Test Architecture Refactoring — DONE (archived to docs/ARCHIVE.md)

---

## Test Inventory

### Key infrastructure files

| File | Purpose |
|------|---------|
| `Tests/Tests.Infrastructure/StateManagerTestClient.cs` | Test client for port 8088 (snapshot query + action forward) |
| `Tests/Tests.Infrastructure/BotServiceFixture.cs` | Auto-starts StateManager if not running |
| `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs` | Dual-client fixture (BG + FG via snapshots) |
| `Services/WoWStateManager/Settings/StateManagerSettings.json` | Dual-client config: ORWR1 (FG) + ORSH1 (BG) |

### Compliant unit tests (~150 tests)

| Project | Count | What they test |
|---------|-------|---------------|
| BotRunner.Tests/Combat/ | 30 | Atomic tasks, combat rotations, services, data models |
| WoWSharpClient.Tests/ | 46 | Protocol parsing, packet handling, network agents |
| WowSharpClient.NetworkTests/ | 8 | TCP, auth, framing, reconnection |
| PromptHandlingService.Tests/ | 6 | Decision engine, prompt caching, intent parsing |
| RecordedTests.Shared.Tests/ | 38 | Test orchestration, storage, configuration |
| Navigation.Physics.Tests/ | 5 (unit) | DLL availability, physics constants |

### LiveValidation tests (14 classes, all snapshot-based)

All use `LiveBotFixture` → `BotServiceFixture` → `StateManager` → dual-client snapshots.

| File | Tests | What they validate |
|------|-------|--------------------|
| `BasicLoopTests.cs` | 6 | Login, physics, teleport, nearby units/objects, level |
| `NpcInteractionTests.cs` | 5 | Vendor sell, trainer learn, flight master, NPC flags |
| `EconomyInteractionTests.cs` | 3 | Banking, AH, mail |
| `CharacterLifecycleTests.cs` | 4 | Character info, equipment, consumable, death/revive |
| `CombatLoopTests.cs` | 1 | Target + melee attack + mob death |
| `DeathCorpseRunTests.cs` | 1 | Die → release spirit → retrieve corpse |
| `QuestInteractionTests.cs` | 1 | GM-based quest add/complete |
| `TalentAllocationTests.cs` | 1 | GM-based talent learning |
| `EquipmentEquipTests.cs` | 1 | Add weapon → equip → verify slot |
| `ConsumableUsageTests.cs` | 1 | Use elixir → verify via action forward |
| `FishingProfessionTests.cs` | 1 | Learn + cast fishing → verify catch |
| `GatheringProfessionTests.cs` | 1 | Learn mining → detect node |
| `CraftingProfessionTests.cs` | 1 | Learn recipe → craft → verify bags |
| `GroupFormationTests.cs` | 1 | Dual-client group invite/accept |

---

## 1. Live Validation Test Scenarios

### Passed (7 tests, 2 classes)

| Test Class | Tests | Date |
|-----------|-------|------|
| `BasicLoopTests.cs` | 6 (login, physics, teleport, units, GOs, level) | 2026-02-19 |
| `FishingProfessionTests.cs` | 1 (learn, equip pole, teleport, face water, cast, detect bobber — dual-client) | 2026-02-20 |

### To run next (written, untested on live server)

| # | Test Class | Tests | Known IDs |
|---|-----------|-------|-----------|
| 1.1 | `EquipmentEquipTests.cs` | 1 | Worn Shortsword item 25 |
| 1.2 | `ConsumableUsageTests.cs` | 1 | Elixir of Lion's Strength item 2454 |
| 1.3 | `CharacterLifecycleTests.cs` | 4 | Character info, equipment, consumable, death/revive |
| 1.4 | `NpcInteractionTests.cs` | 5 | Vendor sell, trainer learn, flight master, NPC flags |
| 1.5 | `CombatLoopTests.cs` | 1 | SetTarget + StartMeleeAttack, Northshire (-8949,-133,82) |
| 1.6 | `DeathCorpseRunTests.cs` | 1 | ReleaseSpirit + RetrieveCorpse |
| 1.7 | `EconomyInteractionTests.cs` | 3 | Banking, AH, mail |
| 1.8 | `QuestInteractionTests.cs` | 1 | Quest 783 "A Threat Within" |
| 1.9 | `TalentAllocationTests.cs` | 1 | Deflection spell 16462 |
| 1.10 | `GatheringProfessionTests.cs` | 1 | Mining spell 2575, Pick 2901, Vein 1731 |
| 1.11 | `CraftingProfessionTests.cs` | 1 | First Aid spell 3273, Linen Cloth 2589 |
| 1.12 | `GroupFormationTests.cs` | 1 | PartyNetworkClientComponent (needs dual-client) |

---

## 2. Game Interaction Improvements

Items discovered or remaining from the code-complete features. Only work on these after live validation passes.

### 2.1 Flight Master — Travel Activation
- FlightMasterVisitTask currently only discovers nodes — needs to actually activate flights
- `FlightMasterService.ActivateFlightAsync()` exists but isn't called from the task
- Need a TravelTask or extend FlightMasterVisitTask to call `DiscoverAndFlyToNearestAsync`
- Travel-to-zone coordination (walk to nearest flight master, fly to destination)

### 2.2 Quest System
- Quest database for starting zones (1-10) — `SqliteQuestRepository` exists
- Quest chain dependencies
- Quest objective locations (kill/gather coordinates)
- NPC locations for accept/turn-in
- Zone-based quest routing
- Reward selection (currently picks first reward — IQuestFrame only exposes RewardCount)

### 2.3 Bag Sorting
- Group items by type: consumables, equipment, quest items, junk
- Use `InventoryNetworkClientComponent` move/swap

### 2.4 Banking — Withdraw
- Withdraw specific items when needed (reagents, ammo)

### 2.5 Gathering Route Optimization
- Nearest-unvisited-node pathfinding per zone instead of random 40y detection
- Resource tracking

### 2.6 AH Price Scanning
- Pre-search market before posting items
- Use `IAuctionHouseNetworkClientComponent.SearchAsync()`
- Calculate median price, undercut strategy

### 2.7 Mail — Send Items
- Send items/gold between characters
- Use `MailNetworkClientComponent.SendMailAsync()`

### 2.8 Obstacle Avoidance (Movement)
- Collide-and-slide with physics engine results
- Last remaining item from Section 2.1

---

## 3. Combat Improvements (Lower Priority)

Combat rotations are code-complete for all 27 specs. Improvements here are polish, not prerequisites.

### 3.1 PvP Polish
- Line-of-sight awareness
- Diminishing returns tracking
- Focus target management (healer targeting)

### 3.2 Group Combat Polish
- CC target assignment (sheep Moon, sap Star)
- Dungeon boss mechanic handling
- Dungeon/group role rotations (tank, healer, DPS)

### 3.3 Equipment Stat Comparison
- Replace quality+requiredLevel heuristic with actual stat-based comparison

---

## 4. Advanced Systems (Future)

### 4.1 World Navigation
- Cross-continent travel (boat/zeppelin schedules)
- City-to-city routing (flight paths + walking)
- Transport coordinate transitions
- Dungeon waypoint data + boss positioning

### 4.2 Battlegrounds
- BG queue + entry
- WSG/AB/AV objective-based behavior

### 4.3 Character Management
- FG bot character creation
- Multi-character management
- Realm selection logic
- Talent respec

### 4.4 Professions Expansion
- Primary professions (Alchemy, Blacksmithing, Tailoring, etc.)
- Recipe list management
- Profession trainer seeking

### 4.5 Infrastructure
- Bot health dashboard
- Error alerting (stuck, disconnected)
- Hot-reload configuration
- Zone waypoint/hotspot definitions
- UI for editing config
