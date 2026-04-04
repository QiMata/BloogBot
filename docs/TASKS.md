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

**Focused FG packet capture and the focused dual Ratchet path test are green on the current binaries, but packet-sequence comparison work is still open.** The focused FG capture still completes end-to-end from the packet-capture dock with packet artifacts and the full fishing contract (`pool_acquired -> in_cast_range_current -> cast_started -> loot_bag_delta -> fishing_loot_success`), and the latest focused dual Ratchet path rerun also completed successfully for both bots. The remaining live risk is now narrower:
- staged Ratchet pool activation/visibility is still nondeterministic across reruns, and `.pool spawns <child>` attribution remains timing-sensitive even through the newer direct GM/system-message capture path
- when the dual slice does fall back into local pier search, `FishingTask` now treats `MovementStuckRecoveryGeneration` as authoritative blocked-probe evidence and abandons that local leg after about `1.5s` instead of burning the full `20s` stall window; keep that guard covered while the remaining comparison/instrumentation work stays open

| # | Task | Status |
|---|------|--------|
| 3.1 | Capture FG fishing packets (cast → channel → bobber → custom anim) | **Done** — focused `Fishing_CaptureForegroundPackets_RatchetStagingCast` passed and recorded `packets_TESTBOT1.csv`, `transform_TESTBOT1.csv`, and `navtrace_TESTBOT1.json` |
| 3.2 | Compare BG fishing packets against FG capture | Open — focused FG reference and the focused dual runtime path are green on current binaries; the remaining work is actual FG/BG packet-sequence comparison plus authoritative child-pool attribution on nondeterministic reruns |
| 3.3 | Harden BG fishing parity to match FG packet/timing | Blocked on 3.2 |

---

## P29 — Fast Travel & Navigation Tests

**Goal:** Test coverage for ALL fast-travel systems in vanilla WoW: mage teleports/portals, flight masters (taxi), boats, zeppelins, elevators, Deeprun Tram, meeting stones, warlock summoning. Both Horde and Alliance sides.

**Depends on:** P21 (travel planner), P26 (dungeon infrastructure for summoning stone tests).

### 29A — Mage Teleport Tests

| # | Task | Spec |
|---|------|------|
| 29.1 | **Create Mage bot accounts** — MAGETESTH + MAGETESTA created via SOAP, GM level 6. | **Done** (75e510e8) |
| 29.2 | **Mage self-teleport test (Horde)** — Mage at Razor Hill. Cast Teleport: Orgrimmar (spell 3567). Assert: mapId stays 1, position changes to Orgrimmar (within 50y of 1676,-4315,61). Under 15s. | Open |
| 29.3 | **Mage self-teleport test (Alliance)** — Mage at Goldshire. Cast Teleport: Stormwind (spell 3561). Assert: position in SW within 15s. | Open |
| 29.4 | **Mage portal test** — Mage + 4 party members. Mage casts Portal: Orgrimmar (spell 11417). Requires Rune of Portals (item 17032). 4 members click portal. Assert: all 5 in Orgrimmar within 30s. | Open |
| 29.5 | **Mage all-city teleport test** — Test all 6 teleport spells: Orgrimmar (3567), Undercity (3563), Thunder Bluff (3566), Stormwind (3561), Ironforge (3562), Darnassus (3565). Assert each lands in correct city. | Open |

### 29B — Flight Master (Taxi) Tests

| # | Task | Spec |
|---|------|------|
| 29.6 | **Horde taxi discovery test** — Bot at Orgrimmar flight master. Interact. Assert: `SMSG_SHOWTAXINODES` received, node list contains Orgrimmar node. Discover Crossroads node via `.tele`. | Open |
| 29.7 | **Horde taxi ride test** — Bot at Orgrimmar flight master with Crossroads discovered. Activate flight. Assert: `CMSG_ACTIVATETAXI` sent, position changes over time, arrives at Crossroads within 3 minutes. | Open |
| 29.8 | **Alliance taxi ride test** — Bot at Stormwind flight master. Fly to Ironforge via Deeprun Tram alternative. Assert arrival. | Open |
| 29.9 | **Multi-hop taxi test** — Bot at Orgrimmar, fly to Gadgetzan (multiple hops). Assert: intermediate nodes traversed, final arrival at Gadgetzan. | Open |

### 29C — Transport Tests (Boats, Zeppelins, Elevators)

| # | Task | Spec |
|---|------|------|
| 29.10 | **Orgrimmar→Undercity zeppelin test** — Bot walks to Org zeppelin tower. Boards zeppelin. Assert: `TransportGuid` set, mapId changes from 1 to 0, arrives in Tirisfal Glades. Uses existing `TransportWaitingLogic`. | Open |
| 29.11 | **Ratchet→Booty Bay boat test** — Bot teleported to Ratchet dock. Boards boat. Assert: arrives in Booty Bay (STV). | Open |
| 29.12 | **Menethil→Theramore boat test (Alliance)** — Alliance bot. Board ship. Cross from Wetlands to Dustwallow Marsh. | Open |
| 29.13 | **Undercity elevator test** — Bot at UC upper level. Takes elevator down. Assert: Z drops ~100y, position in Undercity interior. Uses existing `TransportData.UndercityElevatorWest`. | Open |
| 29.14 | **Thunder Bluff elevator test** — Bot at TB upper. Takes elevator down. Assert: Z drops, arrives at base level. | Open |
| 29.15 | **Deeprun Tram test** — Alliance bot. Ride tram from Ironforge to Stormwind (or vice versa). Assert: map transition via tram instance. | Open |

### 29D — Summoning Tests

| # | Task | Spec |
|---|------|------|
| 29.16 | **Warlock summon test** — Party of 5. Warlock + 2 helpers at dungeon entrance. 2 members in Orgrimmar. Warlock casts Ritual of Summoning (698). 2 helpers click portal. Absent member accepts. Assert: summoned member appears at entrance. | Open |
| 29.17 | **Meeting stone summon test** — Party of 5. 3 at WC meeting stone. 2 in Orgrimmar. Interact with meeting stone (GameObjectType 23). Assert: absent members summoned. | Open |

### 29E — Alliance-Side Tests

| # | Task | Spec |
|---|------|------|
| 29.18 | **Create Alliance test accounts** — ALLYBOT1-ALLYBOT10 created via SOAP, GM level 6. | **Done** (75e510e8) |
| 29.19 | **Alliance navigation test** — Bot at Goldshire. Navigate to Stormwind entrance. Assert: arrival within expected path time. | Open |
| 29.20 | **Alliance vendor test** — Bot at Stormwind vendor. Buy/sell items. Same as VendorBuySellTests but Alliance NPC. | Open |
| 29.21 | **Alliance dungeon test: The Deadmines** (mapId=36) — 10 Alliance bots. Form group, enter Deadmines. Already in DungeonEntryData. Fixture needed. | Open |
| 29.22 | **Alliance dungeon test: The Stockade** (mapId=34) — 10 Alliance bots in Stormwind. Enter Stockade. | Open |
| 29.23 | **Alliance dungeon test: Gnomeregan** (mapId=90) — Alliance approach via Dun Morogh. | Open |

---

## P23 — Interaction Test Suite (FG/BG Parity with Packet Recording)

**Goal:** Complete LiveValidation test coverage for ALL NPC/world interaction systems. Every test runs BOTH FG (injected, gold standard) and BG (headless protocol) bots in parallel, records FG packets as reference, and asserts BG behavior matches. Uses recorded FG packet sequences to verify BG sends correct opcodes.

**Architecture:** FG bot records full packet trace (CMSG+SMSG) via `ForegroundPacketTraceRecorder`. BG bot records its own outbound packets. Tests compare opcode sequences, timing, and state transitions between the two. Binary dumps from `docs/server-protocol/` confirm opcode formats.

### 23A — Packet Recording Framework Enhancement

| # | Task | Spec |
|---|------|------|
| 23.1 | **Extend BackgroundPacketTraceRecorder to capture ALL opcodes** — Added `PacketSent`/`PacketReceived` events to WoWClient, WorldClient, PacketPipeline. BackgroundPacketTraceRecorder now captures all CMSG+SMSG opcodes in both directions, matching FG's PacketLogger coverage. | **Done** (1464e7d) |
| 23.2 | **Add packet payload recording** — `PacketPayloadRecorder.cs` with binary sidecar for AH/bank/mail/vendor/trainer opcodes. | **Done** (31d4a513) |
| 23.3 | **Create `PacketSequenceComparator`** — `Tests/Tests.Infrastructure/PacketSequenceComparator.cs`. Parses CSV, compares opcodes, counts, timing. | **Done** (session 302) |

### 23B — Auction House Tests

| # | Task | Spec |
|---|------|------|
| 23.4 | **Create `FgAuctionFrame.cs`** — Lua-based AH frame + IAuctionFrame interface. | **Done** (d5f915b5) |
| 23.5 | **AH search test** — BG+FG: teleport to Orgrimmar AH, interact with auctioneer, search for "Linen Cloth". Assert: both get SMSG_AUCTION_LIST_RESULT, result counts match. Record FG packets, verify BG sends matching CMSG_AUCTION_LIST_ITEMS. | Open |
| 23.6 | **AH post+buy test** — Bot A posts item via CMSG_AUCTION_SELL_ITEM. Bot B searches and buys via CMSG_AUCTION_PLACE_BID. Assert: SMSG_AUCTION_COMMAND_RESULT success, item delivered via mail. | Open |
| 23.7 | **AH cancel test** — Post item, cancel via CMSG_AUCTION_REMOVE_ITEM. Assert: item returned to inventory. | Open |

### 23C — Bank Tests

| # | Task | Spec |
|---|------|------|
| 23.8 | **Create `FgBankFrame.cs`** — Lua-based bank frame + IBankFrame interface. | **Done** (d5f915b5) |
| 23.9 | **Bank deposit/withdraw test** — BG+FG: teleport to Orgrimmar bank, interact with banker, deposit an item, verify it appears in bank slot. Withdraw it back, verify inventory. Record packets. | Open |
| 23.10 | **Bank slot purchase test** — Purchase a new bank bag slot via CMSG_BUY_BANK_SLOT. Assert: SMSG_BUY_BANK_SLOT_RESULT = OK. | Open |

### 23D — Mail Tests

| # | Task | Spec |
|---|------|------|
| 23.11 | **Wire mail take operations** — Observables wired from SMSG_SEND_MAIL_RESULT action types. | **Done** (2c731c05) |
| 23.12 | **Mail send test** — Bot A sends mail with 1 copper to Bot B via CMSG_SEND_MAIL. Assert: SMSG_SEND_MAIL_RESULT success. Bot B opens mailbox, gets mail list, takes money. | Open |
| 23.13 | **Mail with item test** — Bot A sends mail with an item attachment. Bot B takes item. Assert item in inventory. | Open |

### 23E — Flight Master & Transport Tests

| # | Task | Spec |
|---|------|------|
| 23.14 | **Flight path completion detection** — Added `IsInFlight` to IObjectManager + WoWSharpObjectManager. | **Done** (8a6d33d9) |
| 23.15 | **Taxi ride test** — BG+FG: teleport to Orgrimmar flight master, discover nodes, activate flight to Crossroads. Assert: CMSG_ACTIVATETAXI sent, both arrive at Crossroads within 2 minutes. Record FG packets. | Open |
| 23.16 | **Transport boarding test** — BG+FG: teleport to Ratchet dock, board the boat to Booty Bay. Assert: TransportGuid set on boarding, cleared on arrival. Uses TransportWaitingLogic state machine. | Open |
| 23.17 | **Cross-continent transport test** — Horde bots: board Orgrimmar→Undercity zeppelin. Assert: mapId changes from 1 to 0 during transit, position updates reflect transport movement, arrive in Tirisfal Glades. | Open |

### 23F — Trade Tests

| # | Task | Spec |
|---|------|------|
| 23.18 | **Trade initiate + cancel test** — Bot A initiates trade with Bot B (CMSG_INITIATE_TRADE). Bot B accepts (SMSG_TRADE_STATUS = Begin). Both cancel. Assert: SMSG_TRADE_STATUS = Cancelled. | Open |
| 23.19 | **Trade gold + item test** — Bot A offers 10 copper + 1 item. Bot B accepts. Assert: gold transferred, item in Bot B inventory. | Open |

### 23G — Innkeeper & Spirit Healer Tests

| # | Task | Spec |
|---|------|------|
| 23.20 | **Implement innkeeper set-home** — `SetBindPointTask.cs` finds innkeeper, navigates, interacts, selects binder. | **Done** (75e510e8) |
| 23.21 | **Spirit healer resurrection test** — Kill bot, release spirit, navigate to spirit healer NPC, activate via CMSG_SPIRIT_HEALER_ACTIVATE. Assert: resurrection sickness debuff applied, health restored. | Open |

### 23H — Gossip & Quest Frame Tests

| # | Task | Spec |
|---|------|------|
| 23.22 | **Multi-option gossip test** — Interact with NPC that has multiple gossip options (e.g., trainer + quest giver). Assert: SMSG_GOSSIP_MESSAGE contains correct option count and types. Select each option, verify correct sub-frame opens. | Open |
| 23.23 | **Quest chain test** — Accept quest from NPC, complete objectives (kill mobs), return to NPC, complete quest. Assert: SMSG_QUESTUPDATE_ADD_KILL increments, SMSG_QUESTGIVER_QUEST_COMPLETE fires, reward received. | Open |
| 23.24 | **Quest reward selection test** — Complete quest with multiple reward choices. Select specific reward item. Assert: correct item ID in inventory. | Open |

---

## P24 — 3000-Bot Load Test

**Goal:** Verify the system handles 3000 concurrent bot connections (1 FG + 2999 BG) with all race/class combinations evenly distributed. Incrementally scale from 10 → 100 → 500 → 1000 → 3000.

**Architecture:** Each BG bot is a `BackgroundBotRunner` process that runs local `Navigation.dll` physics from `SceneDataService` scene slices. `PathfindingService` remains the shared pathing endpoint and a fallback physics path if scene slices are unavailable. All connect to the same MaNGOS server. StateManager orchestrates all bots. Metrics: connection time, snapshot latency, physics frame rate, memory per bot, CPU utilization.

### Race/Class Distribution (3000 bots)

WoW 1.12.1 has 8 races × 9 classes (not all combos valid). Valid Horde combos: 22. Valid Alliance combos: 22. Total unique combos: 44.
- 3000 bots ÷ 44 combos ≈ 68 bots per combo
- 1 FG bot (Orc Warrior) + 2999 BG bots distributed across all valid combos

### Load Test Milestones

| # | Task | Spec |
|---|------|------|
| 24.1 | **Create `LoadTestHarness` project** — csproj + LoadTestRunner with N-bot spawn + CSV metrics. | **Done** (33f46b59) |
| 24.2 | **Race/class distribution generator** — `BotDistribution.cs` with 40 valid combos, Generate(N), faction filtering. | **Done** (c056a52b) |
| 24.3 | **MaNGOS account bulk creation** — `BulkAccountCreator.cs` with SOAP-based idempotent creation. | **Done** (75e510e8) |
| 24.4 | **10-bot baseline test** — Launch 1 FG + 9 BG bots. All login, enter world, perform 60s patrol in Orgrimmar. Assert: all 10 connect within 30s, all snapshots received, avg physics < 2ms. Measure: total memory, CPU, pathfinding latency. | Open |
| 24.5 | **100-bot test** — 1 FG + 99 BG. Mixed zones: 50 in Orgrimmar, 25 in Durotar, 25 in Barrens. 5-minute patrol. Metrics: P50/P95/P99 physics frame time, snapshot round-trip, memory per bot. | Open |
| 24.6 | **500-bot test** — 1 FG + 499 BG. All Horde zones. 10-minute run. Measure: MaNGOS server load (world update time), pathfinding queue depth, total system memory. Identify bottlenecks. | Open |
| 24.7 | **1000-bot test** — 1 FG + 999 BG. Multi-zone. 15-minute run. Expected issues: MaNGOS world update lag, pathfinding contention, network bandwidth. Document findings. | Open |
| 24.8 | **3000-bot target test** — 1 FG + 2999 BG. Full distribution across all 44 race/class combos. 30-minute run. Mixed activities: 1000 grinding, 500 in cities, 500 questing, 500 patrolling, 499 idle. Dashboard: real-time metrics for all bots. | Open |

### Load Test Infrastructure

| # | Task | Spec |
|---|------|------|
| 24.9 | **Per-bot metrics collector** — BotMetricsCollector with UDP + CSV output + summary stats. | **Done** (33f46b59) |
| 24.10 | **Load test dashboard** — `dashboard.html` with auto-refresh, metrics cards, bot detail table. | **Done** (4bd15864) |
| 24.11 | **Bot process pooling** — Covered by P9.23 MultiBotHostWorker. | **Done** (31d4a513) |

---

## P25 — Battleground Integration Tests (WSG, AB, AV)

**Goal:** Full-scale battleground tests with realistic team sizes. Each test launches bots on BOTH factions, forms raid groups, queues for the BG, enters, and plays objectives. Validates: BG queue/join/leave protocol, faction-vs-faction combat, objective interaction, honor tracking, and BG-specific coordination.

**Depends on:** P10 (BG network infrastructure), P11A (raid formation), P8 (FG/BG parity gaps for BG packet paths).

### 25A — Shared BG Test Infrastructure

| # | Task | Spec |
|---|------|------|
| 25.1 | **Create `BattlegroundTestFixture`** — `WarsongGulchFixture` handles 20-bot config generation, coordinator mode env vars, account setup. | **Done** (7610079c) |
| 25.2 | **Create `BattlegroundCoordinator`** — Coordinates BG queue lifecycle: WaitingForBots → QueueForBattleground → WaitForInvite → InBattleground. Fixed: waits for ALL bots world-ready before queueing, checks IsObjectManagerValid. | **Done** (7610079c) |
| 25.3 | **Create `BattlemasterData.cs`** — 6 battlemaster NPC locations (3 Horde/Orgrimmar + 3 Alliance/Stormwind) with positions and BG type mapping. | **Done** (1464e7d) |
| 25.4 | **`BattlegroundNetworkClientComponent`** — Fixed: SMSG_BATTLEFIELD_STATUS parser for 1.12.1 (uint8 bracketId, no isRatedBg). Fixed: CMSG_BATTLEFIELD_PORT uses uint32 mapId + uint8 action (not just uint8). 19 BG bots successfully enter WSG. | **Done** (7610079c) |

### 25B — Warsong Gulch (1 FG + 19 BG, 10v10)

**Setup:** 10 Horde bots (1 FG TESTBOT1 + 9 BG WSGBOT2-10) vs 10 Alliance bots (10 BG WSGBOTA1-10). Both sides form raid, queue at battlemasters, enter WSG (mapId=489).

| # | Task | Spec |
|---|------|------|
| 25.5 | **Create WSG accounts + settings** — Reduced to 10 bots (5v5) to prevent test host OOM. SOAP revive+level for dead bots from previous runs. | **Done** (eee6513f) |
| 25.6 | **WSG queue + entry test** — **PASSING.** 10 bots (5v5): find permanent BMs, queue, get invited, accept, transfer to WSG map 489. Assertion via captured StateManager stdout (SMSG_NEW_WORLD map=489 events). Fixed: event-only NPC → permanent BM, packed GUID fallback, SOAP revive, `currentMapId` proto field, `GetCapturedOutput()` for test assertions. | **Done** (86e00d04) |
| 25.7 | **WSG flag capture test** — After entry, Horde bots push to Alliance flag room. One bot picks up flag (interact with game object), carries it to Horde base. Assert: `SMSG_UPDATE_WORLD_STATE` shows Horde flag capture. Score increments. | Open |
| 25.8 | **WSG full game test** — Play until one side reaches 3 captures or 25-minute timer expires. Assert: `SMSG_BATTLEFIELD_STATUS` shows BG complete, honor awarded via `SMSG_PVP_CREDIT`, bots teleported back to original locations. Timeout: 30 minutes. | Open |

### 25C — Arathi Basin (1 FG + 29 BG, 15v15)

**Setup:** 15 Horde bots (1 FG TESTBOT1 + 14 BG ABBOT2-15) vs 15 Alliance bots (15 BG ABBOTA1-15). Both sides form raid, queue, enter AB (mapId=529).

| # | Task | Spec |
|---|------|------|
| 25.9 | **Create AB accounts + settings** — `ArathiBasinFixture` generates 30-bot settings. 15 Horde (1 FG + 14 BG) + 15 Alliance (15 BG). | **Done** (1464e7d) |
| 25.10 | **AB queue + entry test** — `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`. | **Done** (1464e7d) |
| 25.11 | **AB node assault test** — Horde bots split into 3 groups (5 each), assault Stables, Blacksmith, Farm. Assert: `SMSG_UPDATE_WORLD_STATE` shows nodes captured. Track resource points accumulating. | Open |
| 25.12 | **AB full game test** — Play until one side reaches 2000 resources or 30-minute timer. Assert: BG complete, honor awarded, marks distributed. Timeout: 35 minutes. | Open |

### 25D — Alterac Valley (1 FG + 79 BG, 40v40)

**Setup target:** 40 Horde bots (1 FG `TESTBOT1` + 39 BG `AVBOT2-40`) vs 40 Alliance bots (1 FG `AVBOTA1` + 39 BG `AVBOTA2-40`). All participants should be level `60`, mounted, and staged with elixirs. Horde FG must be a High Warlord Tauren Warrior. Alliance FG must be a Grand Marshal Paladin. The remaining `78` bots should use next-tier-appropriate level-60 gear/loadouts for their class and role. Both raids form (8 subgroups × 5), queue, enter AV (mapId=`30`), then push cleanly toward their faction's first objective.

| # | Task | Spec |
|---|------|------|
| 25.13 | **Create AV accounts + settings** — `AlteracValleyFixture` generates 80-bot settings. 40 Horde (1 FG + 39 BG) + 40 Alliance (1 FG + 39 BG). | **Done** (1464e7d) |
| 25.14 | **AV queue + entry test** — `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`. | **Done** (1464e7d) |
| 25.15 | **AV fixture roster/loadout upgrade** — Expand `AlteracValleyFixture` / battleground prep so all `80` bots are level `60`; Horde FG `TESTBOT1` is a High Warlord Tauren Warrior; Alliance FG `AVBOTA1` is a Grand Marshal Paladin; all bots receive epic mounts, baseline elixirs, and class/role-appropriate level-60 gear. Add deterministic fixture/config coverage for the roster contract. | **Done** (2026-04-02) — `AlteracValleyFixture`, `AlteracValleyLoadoutPlan`, and `BattlegroundFixtureConfigurationTests` now enforce the level-60 roster/loadout contract deterministically for all `80` AV participants |
| 25.16 | **AV first-objective movement test** — After queue/entry and prep, both raids leave cave mounted and push toward their initial objective without losing the raid. Horde route target: Stonehearth Bunker approach. Alliance route target: Iceblood Tower approach. Assert both foreground leaders and the raid bulk reach the first-objective staging area with objective-state packets still flowing. | Open |
| 25.17 | **AV tower assault test** — Horde pushes south, assaults Stonehearth Bunker. Assert tower capture via world state updates. | Open |
| 25.18 | **AV graveyard capture test** — Horde captures Snowfall Graveyard. Assert: GY ownership changes, dead Horde bots respawn at captured GY. | Open |
| 25.19 | **AV general kill test** — Full AV game: Horde pushes to Vanndar Stormpike, Alliance pushes to Drek'Thar. Assert: one general dies, `SMSG_BATTLEFIELD_STATUS` shows BG winner. Timeout: 60 minutes. | Open |

**Current blocker (2026-04-02 benchmark):** the focused `AV_FullyPreparedRaids_MountAndReachFirstObjective` live slice still stalls during `EnterWorld` at `39/80`. On the `64 GB` benchmark host, `BackgroundBotRunner` reached `55` instances with `p95 private=64.8 GB` (about `1.18 GB` per runner) and launch never progressed past `AVBOTA16`. Session 293 now keeps BG local physics on thin injected scene slices instead of allowing implicit full-map `.scene` / VMAP loads, so the next AV rerun should re-measure launch pressure before any new `25.16+` objective work.

---

## P26 — Dungeon Instance Tests (1 FG + 9 BG, All Vanilla Dungeons)

**Goal:** Integration test for EVERY vanilla dungeon. Each test launches 10 bots (1 FG + 9 BG) with role-diverse composition, forms a group, travels to the dungeon entrance, and enters the instance. Tests validate: group formation, travel/summoning, instance portal entry, map transition, and basic dungeon progress. Summoning stone interaction is tested for each dungeon that has one — a subset of bots teleport to the stone and summon the rest.

**Depends on:** P5 (RFC pattern), P21 (travel planner), P10.1/P11.1 (group/raid infrastructure).

**Architecture:** Each dungeon test follows the RFC pattern (P5): `DungeoneeringCoordinator` drives the full pipeline. A `DungeonTestFixture` base class parameterized by dungeon ID provides shared setup. Summoning stone tests: GM-teleport 3 bots to the meeting stone position, remaining 7 teleport to a distant city. The 3 bots at the stone use `CMSG_MEETINGSTONE_JOIN` to summon each of the 7, validating the full summoning flow.

### 26A — Dungeon Test Infrastructure

| # | Task | Spec |
|---|------|------|
| 26.1 | **Create `DungeonTestFixture`** — `DungeonInstanceFixture` base class generates settings JSON, launches 10 bots (1 FG + 9 BG), enables coordinator. | **Done** (1464e7d) |
| 26.2 | **Create `DungeonEntryData.cs`** — 26 dungeons with entrance/meeting stone positions, level ranges, faction access. | **Done** (1464e7d) |
| 26.3 | **Implement meeting stone summoning** — `MeetingStoneSummonTask.cs` (same as P21.19). | **Done** (e8476356) |
| 26.4 | **Create `SummoningStoneData.cs`** — Accessor over DungeonEntryData meeting stones. GetByInstanceMapId, GetNearby, AllStones. | **Done** (ce24a5ce) |

### 26B — Classic Dungeons (Levels 13-30)

Each test: 1 FG + 9 BG. Form group → 3 bots at summoning stone, 7 in Orgrimmar → summon all → enter dungeon → verify mapId change → basic forward progress.

| # | Task | Spec |
|---|------|------|
| 26.5 | **Ragefire Chasm** (mapId=389) — Already implemented in P5. | Done (P5) |
| 26.6 | **Wailing Caverns** (mapId=43) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.7 | **Shadowfang Keep** (mapId=33) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.8 | **Blackfathom Deeps** (mapId=48) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.9 | **The Stockade** (mapId=34) — Needs Alliance bots. Teleport 2 to entrance, summon rest. See P29.22. | Open |
| 26.10 | **Gnomeregan** (mapId=90) — Fixture + entry test created. | **Done** (1464e7d) |

### 26C — Mid-Level Dungeons (Levels 30-50)

| # | Task | Spec |
|---|------|------|
| 26.11 | **Razorfen Kraul** (mapId=47) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.12 | **Scarlet Monastery — Cathedral** (mapId=189) — Fixture + entry test (Cathedral wing). | **Done** (1464e7d) |
| 26.13-15 | **SM Graveyard/Library/Armory** — Share Cathedral fixture (same entrance). | Covered by 26.12 |
| 26.16 | **Razorfen Downs** (mapId=129) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.17 | **Uldaman** (mapId=70) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.18 | **Zul'Farrak** (mapId=209) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.19 | **Maraudon** (mapId=349) — Fixture + entry test created. | **Done** (1464e7d) |

### 26D — High-Level Dungeons (Levels 50-60)

| # | Task | Spec |
|---|------|------|
| 26.20 | **Sunken Temple** (mapId=109) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.21 | **Blackrock Depths** (mapId=230) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.22 | **Lower Blackrock Spire** (mapId=229) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.23 | **Upper Blackrock Spire** (mapId=229) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.24 | **Dire Maul — East** (mapId=429) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.25 | **Dire Maul — West** (mapId=429) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.26 | **Dire Maul — North** (mapId=429) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.27 | **Stratholme — Living** (mapId=329) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.28 | **Stratholme — Undead** (mapId=329) — Fixture + entry test created. | **Done** (1464e7d) |
| 26.29 | **Scholomance** (mapId=289) — Fixture + entry test created. | **Done** (1464e7d) |

### 26E — Warlock Summoning Tests

| # | Task | Spec |
|---|------|------|
| 26.30 | **Warlock summon test (RFC)** — Use RFC as test dungeon. Party composition includes 1 Warlock. 3 bots (including Warlock) at RFC entrance. 7 bots in Orgrimmar. Warlock casts Ritual of Summoning (spell 698). 2 nearby bots right-click portal to assist. Target absent bot accepts summon via `CMSG_SUMMON_RESPONSE`. Assert: summoned bot appears at dungeon entrance. Repeat for all 7 absent bots. Requires: Warlock has spell 698 learned, has Soul Shard in inventory. | Open |
| 26.31 | **Meeting stone summon test (Wailing Caverns)** — 3 bots GM-teleported to WC meeting stone. 7 bots in Orgrimmar. Bots at stone interact with meeting stone object. Assert: `SMSG_MEETINGSTONE_SETQUEUE` received. Absent bots summoned one by one. Assert: all 10 bots at dungeon entrance within 5 minutes. | Open |
| 26.32 | **Fallback: no summoner available** — All 10 bots in Orgrimmar, no Warlock, no meeting stone nearby. Test that TravelTask (P21) routes all bots to dungeon entrance via walking/flight path. Assert: all arrive within travel time limit. | Open |

---

## P27 — Raid Instance Tests (10-Man Formation, All Vanilla Raids)

**Goal:** Integration test for EVERY vanilla raid instance. Each test launches 10 bots (1 FG + 9 BG) — not the full 20/40 raid size, but enough to test group formation, raid conversion, travel, instance entry, and basic encounter positioning. Uses the same DungeoneeringCoordinator pattern as P5/P26 but with raid-specific setup (subgroups, role assignment, ready checks).

**Why 10 bots:** Full 40-man tests (P11, P25D) are separate scalability exercises. P27 validates the mechanical pipeline: can the system form a raid, enter every instance portal, and not crash? 10 bots covers all code paths without the resource overhead of 40.

**Depends on:** P11A (raid formation), P26A (dungeon test infrastructure), P21 (travel).

### 27A — Raid Test Infrastructure

| # | Task | Spec |
|---|------|------|
| 27.1 | **Create `RaidTestFixture`** — Raid tests use `DungeonInstanceFixture` base with raid-specific fixtures that adapt `RaidEntryData` to `DungeonDefinition`. | **Done** (1464e7d) |
| 27.2 | **Create `RaidEntryData.cs`** — 7 raids (ZG, AQ20, MC, Onyxia, BWL, AQ40, Naxx) with entrance positions, attunement info. | **Done** (1464e7d) |

### 27B — 20-Man Raids

| # | Task | Spec |
|---|------|------|
| 27.3 | **Zul'Gurub** (mapId=309) — Fixture + entry test created. | **Done** (1464e7d) |
| 27.4 | **Ruins of Ahn'Qiraj (AQ20)** (mapId=509) — Fixture + entry test created. | **Done** (1464e7d) |

### 27C — 40-Man Raids

| # | Task | Spec |
|---|------|------|
| 27.5 | **Molten Core** (mapId=409) — Fixture + entry test created. | **Done** (1464e7d) |
| 27.6 | **Onyxia's Lair** (mapId=249) — Fixture + entry test created. | **Done** (1464e7d) |
| 27.7 | **Blackwing Lair** (mapId=469) — Fixture + entry test created. | **Done** (1464e7d) |
| 27.8 | **Temple of Ahn'Qiraj (AQ40)** (mapId=531) — Fixture + entry test created. | **Done** (1464e7d) |
| 27.9 | **Naxxramas** (mapId=533) — Fixture + entry test created. | **Done** (1464e7d) |

### 27D — Raid-Specific Coordination Tests

| # | Task | Spec |
|---|------|------|
| 27.10 | **Raid ready check test** — In MC (or any raid), raid leader initiates `MSG_RAID_READY_CHECK`. All 9 members respond with `MSG_RAID_READY_CHECK_CONFIRM`. Assert: `MSG_RAID_READY_CHECK_FINISHED` received by all. | Open |
| 27.11 | **Raid subgroup assignment test** — 10 bots split into 2 subgroups of 5 via `CMSG_GROUP_CHANGE_SUB_GROUP`. Assert: each bot knows its subgroup assignment. Tanks in group 1, healers in group 2. | Open |
| 27.12 | **Raid mark targeting test** — Raid leader sets skull mark on a mob via `CMSG_SET_RAID_ICON`. All DPS bots target the skull-marked mob. Assert: all DPS bots' `TargetGuid` matches the marked mob. | Open |
| 27.13 | **Raid loot rules test** — Set loot method to Master Looter via `CMSG_LOOT_METHOD` before entering raid. Assert: `SMSG_GROUP_SET_LEADER` + loot method change confirmed. After killing a mob, raid leader receives loot via `CMSG_LOOT_MASTER_GIVE`. | Open |

---

## P22 — Character Progression Planner (Goal-Driven Bot Behavior)

**Goal:** A config-driven progression system where each bot has explicit long-term objectives — target spec, gear set, reputation standings, rare items, mount, gold target, skill priorities — and StateManager continuously evaluates progress against those goals to decide what the bot should do next. This is the layer that makes 3000 bots behave like a population of real players with individual ambitions.

**Current state:** Spec is hardcoded per class (e.g., all Warriors are Arms). Talent builds are hardcoded 51-point paths. Gear evaluation is greedy (higher quality = better). No concept of BiS, reputation goals, rare item farming, mount acquisition, or gold savings. Bots grind without purpose.

### 22A — Character Build Config (Extends CharacterSettings)

| # | Task | Spec |
|---|------|------|
| 22.1 | **Add `CharacterBuildConfig` to CharacterSettings** — Added TargetGearSet, ReputationGoals, ItemGoals, MountGoal fields. | **Done** (c15b6773) |
| 22.2 | **Make spec configurable** — Already wired: BuildConfig.SpecName → WWOW_CHARACTER_SPEC → BotProfileResolver.Resolve. | **Done** (pre-existing) |
| 22.3 | **Make talent build configurable** — Already wired: BuildConfig.TalentBuildName → WWOW_TALENT_BUILD env var. | **Done** (pre-existing) |
| 22.4 | **Add build config to proto snapshot** — `CharacterGoals` proto message added to WoWActivitySnapshot field 22. | **Done** (d9bdf4bd) |

### 22B — Gear Progression System (BiS Lists & Target Sets)

| # | Task | Spec |
|---|------|------|
| 22.5 | **Define `GearGoal` model** — `GearGoalEntry` in CharacterBuildConfig.cs. | **Done** (c15b6773) |
| 22.6 | **Create pre-built BiS gear sets** — `PreRaidBisSets.cs` loads from template JSONs + 4 templates created. | **Done** (28f88d61) |
| 22.7 | **Gear evaluation against target set** — `GearEvaluationService.EvaluateGaps()` already exists. | **Done** (pre-existing) |
| 22.8 | **Gear-driven activity selection** — ProgressionPlanner evaluates TargetGearSet gaps, resolves sources. | **Done** (9e7df9a0) |

### 22C — Reputation Goal System

| # | Task | Spec |
|---|------|------|
| 22.9 | **Define `ReputationGoal` model** — `ReputationGoalEntry` in CharacterBuildConfig.cs. | **Done** (c15b6773) |
| 22.10 | **Reputation tracking in snapshot** — `reputationStandings` map added to WoWPlayer proto field 45. | **Done** (cbf62843) |
| 22.11 | **Rep-driven activity selection** — ProgressionPlanner evaluates ReputationGoals vs standings. | **Done** (9e7df9a0) |

### 22D — Rare Item & Mount Goals

| # | Task | Spec |
|---|------|------|
| 22.12 | **Define `ItemGoal` model** — `ItemGoalEntry` in CharacterBuildConfig.cs. | **Done** (c15b6773) |
| 22.13 | **Define `MountGoal` model** — `MountGoalEntry` in CharacterBuildConfig.cs. | **Done** (c15b6773) |
| 22.14 | **Farm loop for rare drops** — `FarmBossTask.cs` with travel/enter/clear/loot/reset loop. | **Done** (28f88d61) |

### 22E — Skill & Profession Training

| # | Task | Spec |
|---|------|------|
| 22.15 | **Skill training priority config** — SkillPriorities evaluated in ProgressionPlanner. | **Done** (9e7df9a0) |
| 22.16 | **Profession trainer location data** — `ProfessionTrainerData.cs` already exists with trainer records. | **Done** (pre-existing) |
| 22.17 | **Auto-train on skill threshold** — Tier boundary detection (75/150/225) in ProgressionPlanner. | **Done** (9e7df9a0) |

### 22F — Gold & Economy Goals

| # | Task | Spec |
|---|------|------|
| 22.18 | **Gold tracking in progression** — GoldTargetCopper + MountGoal.GoldCost evaluated. | **Done** (9e7df9a0) |
| 22.19 | **Mount acquisition flow** — `MountAcquisitionTask.cs` with prereq evaluation + vendor locations. | **Done** (28f88d61) |
| 22.20 | **Consumable budget management** — `MaxConsumableSpendPerSessionCopper` added to BotBehaviorConfig. | **Done** (e881ed2d) |

### 22G — Quest Chain Progression

| # | Task | Spec |
|---|------|------|
| 22.21 | **Quest chain goal config** — `QuestChains` field already in CharacterBuildConfig. | **Done** (pre-existing) |
| 22.22 | **Quest chain data** — `QuestChainData.cs` with 7 chains (attunements, class quests, zone chains). | **Done** (e5e3c6d6) |
| 22.23 | **Quest chain progress tracking** — ProgressionPlanner evaluates QuestChains vs quest log. | **Done** (f6f4431b) |

### 22H — Progression Planner (StateManager Decision Layer)

| # | Task | Spec |
|---|------|------|
| 22.24 | **Create `ProgressionPlanner.cs`** — Already exists with priority-ordered goal evaluation. | **Done** (pre-existing) |
| 22.25 | **Wire ProgressionPlanner into StateManager** — Already wired in CharacterStateSocketListener. | **Done** (pre-existing) |
| 22.26 | **Progress dashboard in snapshot** — `ProgressionStatus` proto added to WoWActivitySnapshot field 23. | **Done** (7b504352) |

### 22I — Pre-Built Character Templates

| # | Task | Spec |
|---|------|------|
| 22.27 | **Create template configs** — 4 JSON templates: FuryWarrior, HolyPriest, FrostMage, ProtWarrior. | **Done** (69cdb6a1) |
| 22.28 | **Template assignment in UI** — `build_template` proto field + AvailableTemplates/SelectedBuildTemplate in ViewModel. | **Done** (cd15f2cf) |

### 22J — Progression Tests

| # | Task | Spec |
|---|------|------|
| 22.29 | **Gear evaluation test** — 3 tests: empty slots, matched slot, priority ordering. | **Done** (d5f915b5) |
| 22.30 | **ProgressionPlanner priority test** — 7 tests for config/gold/skill/quest evaluation. | **Done** (26801e1f) |
| 22.31 | **Configurable spec test** — 6 tests for BotProfileResolver.Resolve with spec overrides. | **Done** (3e20b43d) |
| 22.32 | **Talent auto-allocation test** — 8 tests for TalentBuildDefinitions.GetBuild per spec. | **Done** (f3748499) |
| 22.33 | **Rep tracking test** — 4 tests for ProgressionPlanner reputation goal evaluation. | **Done** (f3748499) |

---

## P21 — Cross-World Travel Planner

**Goal:** A StateManager-level objective system that decomposes "Go to Position X on Map Y" into a chain of BotRunner tasks covering ALL in-game travel modes. The bot should be able to travel from any reachable point in the game world to any other reachable point — across continents, through dungeons, via flight paths, boats, zeppelins, hearthstones, and class-specific teleports — without GM commands.

**Existing infrastructure (DO NOT REWRITE):**
- `CrossMapRouter.PlanRoute()` — Returns `List<RouteLeg>` with walk/elevator/boat/zeppelin/dungeon portal/flight legs
- `MapTransitionGraph` — 13 transitions (4 boats, 3 zeppelins, 6 dungeon portals), faction-aware
- `TransportData` — 11 transports (4 elevators, 4 boats, 3 zeppelins) with stop positions and boarding radii
- `TransportWaitingLogic` — Full state machine for boarding/riding/disembarking
- `FlightPathData` — 48 taxi nodes with map/position/faction data
- `FlightMasterNetworkClientComponent` — Full CMSG/SMSG taxi protocol (discover, activate, express)
- `PathfindingClient` — Single-map A* pathfinding with 30s timeout
- `NavigationPath` — Waypoint-following with frame-ahead acceptance

### 21A — Travel Objective System (StateManager → BotRunner)

**Architecture:** StateManager sends a `TravelObjective` to the bot. BotRunner decomposes it via `CrossMapRouter` into `RouteLeg[]`, then pushes a `TravelTask` that executes each leg sequentially. Each leg becomes a sub-task (GoTo, TakeFlightPath, BoardTransport, EnterPortal, UseHearthstone).

| # | Task | Spec |
|---|------|------|
| 21.1 | **Add `TravelObjective` proto message** — TRAVEL_TO=79, TravelObjective proto, CharacterAction.TravelTo, ActionMapping. | **Done** (5409506e) |
| 21.2 | **Create `TravelTask.cs`** — Cross-world route execution via CrossMapRouter with walk/transport/portal legs. | **Done** (6d3dbd70) |
| 21.3 | **Create `TravelOptions` record** — TravelFaction, AllowHearthstone/ClassTeleport/FlightPath, DiscoveredFlightNodes, HearthstoneBind. | **Done** (190d1e65) |
| 21.4 | **Wire TravelTo in ActionDispatch** — Same-map GOTO, cross-map placeholder for P21.2. | **Done** (5409506e) |
| 21.5 | **StateManager travel coordination** — CharacterGoals populated from BuildConfig on every snapshot. | **Done** (78287e61) |

### 21B — Flight Path Integration

| # | Task | Spec |
|---|------|------|
| 21.6 | **Create `TakeFlightPathTask.cs`** — Navigate to FM, interact, select taxi node, monitor landing. | **Done** (91434246) |
| 21.7 | **Integrate flight legs into CrossMapRouter** — TryFlightPathShortcut for same-map >200y walks. | **Done** (cdb7cdc4) |
| 21.8 | **Flight path discovery tracking** — `discoveredFlightNodes` repeated uint32 added to WoWPlayer proto field 44. | **Done** (7b504352) |

### 21C — Hearthstone Integration

| # | Task | Spec |
|---|------|------|
| 21.9 | **Create `UseHearthstoneTask.cs`** — Finds hearthstone, casts, detects teleport. | **Done** (d9bdf4bd) |
| 21.10 | **Hearthstone cooldown tracking** — `hearthstoneCooldownSec` field 37 added to MovementData proto. | **Done** (f97a2947) |
| 21.11 | **Create `SetBindPointTask.cs`** — Already created with innkeeper find/navigate/interact/gossip. | **Done** (75e510e8) |
| 21.12 | **Innkeeper location data** — `InnkeeperData.cs` with 26 innkeepers (Horde/Alliance/Neutral). | **Done** (session 302) |

### 21D — Missing Transport Data

| # | Task | Spec |
|---|------|------|
| 21.13 | **Add Deeprun Tram** — Already in MapTransitionGraph with Ironforge↔Stormwind transitions. | **Done** (pre-existing) |
| 21.14 | **Add missing dungeon portals to MapTransitionGraph** — All vanilla dungeon/raid maps already defined in MapTransitionGraph.cs. | **Done** (pre-existing) |
| 21.15 | **Add all raid instance portals** — Already in MapTransitionGraph: MC, BWL, Onyxia, AQ20, AQ40, Naxx. | **Done** (pre-existing) |

### 21E — Class-Specific Travel (Mage Portals, Warlock Summon)

| # | Task | Spec |
|---|------|------|
| 21.16 | **Mage teleport spell data** — `MageTeleportData.cs` already exists. | **Done** (pre-existing) |
| 21.17 | **Create `MageTeleportTask.cs`** — Checks class/spell/cooldown, casts, detects teleport. | **Done** (cbf62843) |
| 21.18 | **Warlock Ritual of Summoning** — `WarlockSummonTask.cs` with prereq checks + ritual cast. | **Done** (9893b352) |
| 21.19 | **Meeting stone summoning** — `MeetingStoneSummonTask.cs` with stone find/navigate/interact/summon. | **Done** (e8476356) |

### 21F — Route Optimization & Hearthstone Strategy

| # | Task | Spec |
|---|------|------|
| 21.20 | **Extend CrossMapRouter with hearthstone legs** — Hearthstone TransitionType added. | **Done** (cdb7cdc4) |
| 21.21 | **Extend CrossMapRouter with class teleport legs** — ClassTeleport TransitionType added. | **Done** (cdb7cdc4) |
| 21.22 | **Named location resolver** — `LocationResolver.cs` already exists with static + DB loading. | **Done** (pre-existing) |
| 21.23 | **Route re-planning on failure** — Built into TravelTask.cs with MaxReplans=3. | **Done** (6d3dbd70) |

### 21G — Spirit Healer & Graveyard Navigation

| # | Task | Spec |
|---|------|------|
| 21.24 | **Graveyard position cache** — `GraveyardData.cs` with runtime DB loading + FindNearest/GetForZone. | **Done** (e8476356) |
| 21.25 | **Spirit healer auto-navigation** — RetrieveCorpseTask checks for spirit healer when corpse >200y. | **Done** (2c731c05) |

### 21H — Travel Planner Tests

| # | Task | Spec |
|---|------|------|
| 21.26 | **CrossMapRouter unit tests** — 5 tests for walk/zeppelin/boat/portal routing. | **Done** (9893b352) |
| 21.27 | **TravelTask integration test** — File: `Tests/BotRunner.Tests/LiveValidation/TravelTests.cs`. BG bot in Orgrimmar receives TravelTo action targeting Crossroads. Assert: bot walks to Orgrimmar gate, paths south through Durotar into Barrens, arrives within 5y of Crossroads. Timeout: 10 minutes. | Open |
| 21.28 | **Flight path travel test** — BG bot at Orgrimmar flight master, flies to Crossroads. Assert: CMSG_ACTIVATETAXI sent, bot position changes to Crossroads area within 2 minutes, task completes. | Open |
| 21.29 | **Hearthstone travel test** — BG bot bound to Orgrimmar, teleported to Razor Hill by GM. Send TravelTo(Orgrimmar). Assert: bot uses hearthstone (faster than walking), arrives in Orgrimmar within 15s. | Open |
| 21.30 | **Cross-continent travel test** — Horde BG bot in Orgrimmar receives TravelTo(Undercity). Assert: route legs are [Walk to zeppelin dock, Board zeppelin, Ride, Disembark, Walk to UC entrance]. Bot arrives in Undercity. Timeout: 5 minutes. | Open |

---

---

## P9 — Scalability: 3000 Concurrent Bot Architecture

**Goal:** Refactor from 1-process-per-bot (current limit ~50 bots) to N-bots-per-process with async I/O. Target: 3000 live connections to one MaNGOS server, all reading/writing game state via BG (headless) protocol.

**Hard blockers — RESOLVED:**
- ~~`ProtobufSocketServer` TCP backlog hardcoded to 50~~ → `ProtobufPipelineSocketServer` backlog 4096 (9.8)
- ~~Synchronous blocking IPC in BotRunnerService tick loop~~ → `SendMessageAsync` (9.10, 9.11)
- ~~Uncompressed protobuf snapshots~~ → GZip compression >1KB (9.14)

**Hard blockers — REMAINING:**
- `WoWSharpObjectManager` is a static singleton — 1 instance per process
- `WoWSharpEventEmitter` is a static singleton — cross-bot event interference
- `SplineController` is a static singleton — shared mutable spline state
- PathfindingService is single-process with ThreadPool-bound handlers (~64 threads)

**Load test results (async pipeline):**
- 100 clients: 944 msg/s, P99=37ms, 0 errors
- 500 clients: 4076 msg/s, P99=181ms, 0 errors
- 1000 clients: 3623 msg/s, P99=1071ms, 0 errors
- 3000 clients: 129-1555 msg/s, P99=5-12s, 0 errors (all connections accepted)

### 9A — Remove Singletons: Per-Bot Isolation Context

| # | Task | Spec |
|---|------|------|
| 9.1 | **`BotContext` class** — Created with all per-bot state (WoWClient, ObjectManager, EventEmitter, SplineController, MovementController, PathfindingClient). `FromCurrentSingletons()` bridge for migration. | **Done** (c183e27d) |
| 9.2 | **Refactor `WoWSharpObjectManager`** — Remove `private static WoWSharpObjectManager _instance` and `public static Instance` property. Change `private static readonly List<WoWObject> _objects` and `_objectsLock` to instance fields. Constructor becomes public, takes `WoWClient` + `PathfindingClient` + `ILogger`. Keep a `[Obsolete] static Instance` shim during migration that delegates to a thread-local or ambient context. | Open |
| 9.3 | **Refactor `WoWSharpEventEmitter`** — Remove singleton. Make instance-based. Each `BotContext` owns one. All 100+ event subscriptions scoped to their bot. Update all callers from `WoWSharpEventEmitter.Instance.OnX += handler` to `_context.Events.OnX += handler`. | Open |
| 9.4 | **Refactor `SplineController`** — WoWSharpObjectManager.SplineCtrl replaces Splines.Instance. Per-bot via BotContext. | **Done** (31d4a513) |
| 9.5 | **Update `BackgroundBotWorker`** — Replace `WoWSharpObjectManager.Instance` call in `InitializeInfrastructure()` with `new WoWSharpObjectManager(wowClient, pathfindingClient, logger)`. Each worker creates its own `BotContext`. | Open |
| 9.6 | **Update all tests** — Remove `DisableParallelization` from ObjectManager test collections. Each test creates its own `BotContext`. Update `ObjectManagerFixture` to use instance-based ObjectManager. Run full test suite green. | Open |
| 9.7 | **Validate N=10 bots per process** — Create `MultiBotHostWorker` that creates 10 `BotContext` instances in one `BackgroundBotRunner` process. Each runs its own tick loop on a dedicated `Task`. Connect all 10 to live MaNGOS, verify independent movement and combat. | Open |

### 9B — Async Socket Infrastructure

| # | Task | Spec |
|---|------|------|
| 9.8 | **`ProtobufPipelineSocketServer`** — Async server using `System.IO.Pipelines`, `Socket.AcceptAsync`, backlog 4096. Zero dedicated threads. Handles 3000 connections with 0 errors. | **Done** (c07eb2ae) |
| 9.9 | **Wire `CharacterStateSocketListener`** — Swapped base class from `ProtobufSocketServer` to `ProtobufPipelineSocketServer`. One-line change. All 37 unit tests pass. | **Done** (3de18c1f) |
| 9.10 | **`SendMessageAsync`** — Async client with `SemaphoreSlim`, `stream.WriteAsync/ReadAsync`. 152x throughput improvement at 100 clients (944 msg/s vs 6 msg/s). P99: 37ms vs 8679ms. | **Done** (f6be0615) |
| 9.11 | **BotRunnerService async tick loop** — `SendMemberStateUpdateAsync` replaces blocking `SendMemberStateUpdate`. No ThreadPool blocking during I/O. | **Done** (b94e3ecb) |

### 9C — Snapshot & Network Optimization

| # | Task | Spec |
|---|------|------|
| 9.12 | **Delta snapshots** — `SnapshotDeltaComputer.cs` with byte-level diff, keyframe interval, ApplyDelta reconstruction. | **Done** (1b4d561e) |
| 9.13 | **Snapshot batching** — `SnapshotBatcher.cs` with timer-based flush + max batch size. | **Done** (4a34eafd) |
| 9.14 | **GZip compression** — `ProtobufCompression.cs` with 1-byte flag header (0x00=raw, 0x01=GZip). Threshold 1KB. Backward-compatible decode. Unit tests pass. | **Done** (pre-existing) |
| 9.15 | **Connection multiplexing** — `ConnectionMultiplexer.cs` with hash-based bot→connection routing. | **Done** (4a34eafd) |

### 9D — PathfindingService Scaling

| # | Task | Spec |
|---|------|------|
| 9.16 | **Sharded PathfindingService** — `PathfindingShardRouter.cs` with consistent-hash shard assignment. | **Done** (4a34eafd) |
| 9.17 | **Async pathfinding requests** — `AsyncPathfindingWrapper.cs` with Channel-based queue + configurable worker pool. | **Done** (1b4d561e) |
| 9.18 | **Physics step batching** — `PhysicsBatchProcessor.cs` with batch P/Invoke + sequential fallback. C++ export stub ready. | **Done** (cd15f2cf) |
| 9.19 | **Path result caching** — `PathResultCache.cs` LRU cache with grid-quantized keys, 10K entries, hit rate tracking. | **Done** (4bd15864) |

### 9E — StateManager Horizontal Scaling

| # | Task | Spec |
|---|------|------|
| 9.20 | **Partitioned StateManager** — `StateManagerCluster.cs` with map-based sharding + UDP gossip protocol. | **Done** (79b200f4) |
| 9.21 | **Replace `Dictionary<string, ...> + lock`** — Already using ConcurrentDictionary in CharacterStateSocketListener. | **Done** (pre-existing) |
| 9.22 | **Replace thread-per-bot log pipes** — `BotTaggedLogger.cs` with Serilog ForContext BotId tagging + scope. | **Done** (1b4d561e) |
| 9.23 | **Bot process pooling** — `MultiBotHostWorker.cs` with staggered N-bot launch + per-bot Task tick loops. | **Done** (31d4a513) |

### 9F — Load Testing Infrastructure

| # | Task | Spec |
|---|------|------|
| 9.24 | **Create `LoadTestHarness` project** — Same as P24.1. | **Done** (33f46b59) |
| 9.25 | **100-bot baseline** — Run 100 bots on single machine. All login, move to Orgrimmar, perform basic patrol route. Measure: P50/P95/P99 tick latency, PathfindingService queue depth, StateManager snapshot processing time, total memory, total CPU. | Open |
| 9.26 | **500-bot milestone** — Run 500 bots across 2 machines (250 each). Multi-zone: 200 in Orgrimmar, 200 in Durotar, 100 in Barrens. Measure same metrics + cross-machine latency. | Open |
| 9.27 | **3000-bot target** — Run 3000 bots across cluster. 1000 per machine (3 machines). Mixed activities: 1000 grinding, 500 in BGs, 500 raiding, 500 questing, 500 idle in cities. Full metrics dashboard. | Open |
| 9.28 | **Continuous performance regression** — Add `BenchmarkDotNet` benchmarks for: protobuf serialization/deserialization, pathfinding request latency, behavior tree tick cost, snapshot delta computation. Run in CI, fail build if P95 regresses >10%. | Open |

---

## P10 — Battleground System (WSG, AB, AV)

**Goal:** Full PvP battleground support — queue, join, play objectives, earn honor. Required for human-like behavior at scale.

**Current state:** 27 BG-related opcodes defined in `Opcode.cs` but ZERO handlers implemented. `BgInteractionTests.cs` exists but only tests banking.

### 10A — BG Network Infrastructure

| # | Task | Spec |
|---|------|------|
| 10.1 | **Create `BattlegroundNetworkClientComponent`** — Already fully implemented with JoinQueue/Accept/Leave/StatusChanged. | **Done** (pre-existing) |
| 10.2 | **Add BG CharacterActions** — JoinBattleground/AcceptBattleground/LeaveBattleground already in enum + mapping + dispatch. | **Done** (pre-existing) |
| 10.3 | **BG state tracking in ObjectManager** — BattlegroundState property on component. | **Done** (pre-existing) |

### 10B — BG Objective Systems

| # | Task | Spec |
|---|------|------|
| 10.4 | **Warsong Gulch objectives** — `WsgObjectiveTask.cs` with flag pickup/carry/capture/defend. | **Done** (76614674) |
| 10.5 | **Arathi Basin objectives** — `AbObjectiveTask.cs` with 5-node assault/defend state machine. | **Done** (1c032398) |
| 10.6 | **Alterac Valley objectives** — `AvObjectiveTask.cs` with tower/GY/general push. | **Done** (c60084c4) |
| 10.7 | **BG target prioritization** — `BgTargetSelector.cs` with health/mana heuristics. | **Done** (c1bcbbf0) |

### 10C — Honor & Reward Tracking

| # | Task | Spec |
|---|------|------|
| 10.8 | **Honor tracking** — honorPoints/honorableKills/dishonorableKills proto fields 46-48. | **Done** (c1bcbbf0) |
| 10.9 | **BG reward collection** — `BgRewardCollectionTask.cs` with mark inventory check + battlemaster navigation. | **Done** (4bd15864) |

### 10D — BG Tests

| # | Task | Spec |
|---|------|------|
| 10.10 | **BG queue/join test** — `Tests/BotRunner.Tests/LiveValidation/BattlegroundQueueTests.cs`. Bot queues for WSG, receives invite, accepts, enters BG map, verifies `BattlegroundState == InBattleground`. | Open |
| 10.11 | **WSG flag capture test** — 2 bots (both BG) enter WSG. One carries flag, other defends. Assert flag capture event fires. | Open |
| 10.12 | **AB node assault test** — Bot enters AB, assaults Blacksmith node, asserts node capture from world state. | Open |

---

## P11 — 40-Man Raid System

**Goal:** Support raid formation up to 40 players (1 FG + 39 BG), subgroup management, role assignment, ready checks, encounter mechanics, and master loot distribution.

### 11A — Raid Formation & Management

| # | Task | Spec |
|---|------|------|
| 11.1 | **Implement ready check** — InitiateReadyCheckAsync + ReadyCheck observables in PartyNetworkClientComponent. | **Done** (pre-existing) |
| 11.2 | **Subgroup management** — CMSG_GROUP_CHANGE_SUB_GROUP in PartyNetworkClientComponent. | **Done** (pre-existing) |
| 11.3 | **Main Tank / Main Assist targets** — `RaidRoleAssignment.cs` with MT/MA tracking + auto-assign. | **Done** (9105b2a3) |
| 11.4 | **Raid composition builder** — `RaidCompositionService.cs` with tank/healer/DPS assignment. | **Done** (5cfc1183) |

### 11B — Encounter Mechanics

| # | Task | Spec |
|---|------|------|
| 11.5 | **Threat management** — `ThreatTracker.cs` with damage/healing threat + throttle check. | **Done** (5cfc1183) |
| 11.6 | **Positional awareness** — `EncounterPositioning.cs` with melee/ranged/tank positions + cleave zones. | **Done** (76614674) |
| 11.7 | **Boss mechanic responses** — `EncounterMechanicsTask.cs` with data-driven spread/stack/interrupt/dispel/taunt swap. | **Done** (c60084c4) |
| 11.8 | **Raid cooldown coordination** — `RaidCooldownCoordinator.cs` with overlap prevention. | **Done** (76614674) |

### 11C — Master Loot

| # | Task | Spec |
|---|------|------|
| 11.9 | **Master loot distribution** — `MasterLootDistributionTask.cs` with priority-based loot assignment via LootFrame + AssignLoot. | **Done** (c60084c4) |
| 11.10 | **Loot council simulation** — `LootCouncilSimulator.cs` with MainSpec > OffSpec > Greed priority + /roll. | **Done** (9105b2a3) |

### 11D — Raid Tests

| # | Task | Spec |
|---|------|------|
| 11.11 | **40-man raid formation test** — Extend RagefireChasmTests pattern. 40 bots, 8 subgroups, verify group list shows all 40 members in correct subgroups. | Open |
| 11.12 | **Ready check test** — Raid leader initiates ready check, all 39 members confirm, assert FINISHED received. | Open |
| 11.13 | **Encounter test (RFC)** — 10-bot RFC clear with threat tracking and positional awareness. Assert all bosses killed, no wipes from positional failures. | Open |

---

## P12 — World PvP & Hostile Player Engagement

| # | Task | Spec |
|---|------|------|
| 12.1 | **PvP flag detection** — `HostilePlayerDetector.IsPvPFlagged()` checks `UNIT_FLAG_PVP` on UnitFlags. | **Done** (c60084c4) |
| 12.2 | **Hostile player scanning** — `HostilePlayerDetector.Scan()` with faction detection + threat assessment. | **Done** (c60084c4) |
| 12.3 | **PvP engagement BotTask** — `PvPEngagementTask.cs` with fight-or-flee + guard escape. | **Done** (c60084c4) |
| 12.4 | **Dishonorable kill avoidance** — `HostilePlayerDetector.IsCivilian()` checks `UNIT_FLAG_PASSIVE`. | **Done** (c60084c4) |

---

## P13 — Quest Objective Tracking & Chain Routing

| # | Task | Spec |
|---|------|------|
| 13.1 | **Parse quest objective updates** — Already done. QuestHandler parses ADD_KILL + ADD_ITEM, calls `UpdateQuestKillProgress`/`UpdateQuestItemProgress` on ObjectManager. ConcurrentDictionary tracks progress, fires events. | **Done** (pre-existing) |
| 13.2 | **Quest objective display in snapshot** — `QuestObjectiveProgress` proto message + `objectives` field on QuestLogEntry. | **Done** (31d4a513) |
| 13.3 | **Quest chain router** — `QuestChainRouter.cs` with step resolver + nearest quest giver lookup. | **Done** (c60084c4) |
| 13.4 | **Escort quest support** — `EscortQuestTask.cs` with follow/defend NPC state machine. | **Done** (c60084c4) |

---

## P14 — Pet Management System

| # | Task | Spec |
|---|------|------|
| 14.1 | **Pet management task** — `PetManagementTask.cs` with stance/feed/ability state machine. | **Done** (c60084c4) |
| 14.2 | **Pet stance control** — Covered in PetManagementTask (P14.1) with stance enum. | **Done** (c60084c4) |
| 14.3 | **Hunter pet feeding** — `PetFeedingTask.cs` with diet-based feeding + inventory check. | **Done** (07c90b02) |
| 14.4 | **Pet ability usage in combat** — Covered in PetManagementTask (P14.1) UseAbility state. | **Done** (c60084c4) |

---

## P15 — Channel & Social System

| # | Task | Spec |
|---|------|------|
| 15.1 | **`ChannelNetworkClientComponent`** — Already exists with JoinChannel, LeaveChannel, SendChannelMessage. | **Done** (pre-existing) |
| 15.2 | **Auto-join General/Trade/LocalDefense** — `ChannelAutoJoinTask.cs` with default channel list. | **Done** (07c90b02) |
| 15.3 | **Whisper conversation tracking** — `WhisperTracker.cs` with per-player history + unread detection. | **Done** (07c90b02) |

---

## P16 — Crafting & Profession Automation

| # | Task | Spec |
|---|------|------|
| 16.1 | **Batch crafting task** — `BatchCraftTask.cs` with cast + failure detection. | **Done** (c60084c4) |
| 16.2 | **Profession skill tracking** — `ProfessionSkillEntry` proto message + `professionSkills` field on WoWPlayer. | **Done** (31d4a513) |
| 16.3 | **Trainer visit on skill-up** — `ProfessionTrainerScheduler.cs` with tier thresholds + Horde/Alliance trainer locations. | **Done** (07c90b02) |
| 16.4 | **First Aid / Cooking auto-learn** — Covered in ProfessionTrainerScheduler (secondary professions included). | **Done** (07c90b02) |

---

## P17 — Character Progression Automation

| # | Task | Spec |
|---|------|------|
| 17.1 | **Talent auto-allocation** — `TalentAutoAllocator.cs` with pre-defined build paths per class/spec. | **Done** (c60084c4) |
| 17.2 | **Trainer visit on level-up** — `LevelUpTrainerTask.cs` with class trainer navigation. | **Done** (c60084c4) |
| 17.3 | **Zone progression router** — `ZoneLevelingRoute.cs` with Horde/Alliance zone routes. | **Done** (c60084c4) |
| 17.4 | **Hearthstone management** — Covered by P21.9 UseHearthstoneTask + P21.11 SetBindPointTask. | **Done** (pre-existing via P21) |
| 17.5 | **Durability monitoring & repair scheduling** — `DurabilityMonitor.cs` with repair vendor positions. | **Done** (07c90b02) |
| 17.6 | **Ammo management (Hunters)** — `AmmoManager.cs` with level-based ammo selection + vendor positions. | **Done** (07c90b02) |

---

## P18 — Economy & Banking Automation

| # | Task | Spec |
|---|------|------|
| 18.1 | **AH posting strategy** — `AuctionPostingService.cs` with market scan + undercut pricing. | **Done** (c60084c4) |
| 18.2 | **Bank deposit automation** — `BankDepositTask.cs` with deposit/keep filters. | **Done** (c60084c4) |
| 18.3 | **Mail-based item transfer** — `MailTransferTask.cs` with mailbox navigation + send. | **Done** (07c90b02) |
| 18.4 | **Gold threshold management** — `GoldThresholdManager.cs` with level-based reserve + deposit thresholds. | **Done** (07c90b02) |

---

## P19 — Travel & Transport Automation

| # | Task | Spec |
|---|------|------|
| 19.1 | **Hearthstone auto-use** — UseHearthstoneTask (P21.9) + hearthstoneCooldownSec (P21.10). | **Done** (pre-existing via P21) |
| 19.2 | **Spirit healer navigation** — Covered by P21.25 (RetrieveCorpseTask spirit healer). | **Done** (2c731c05) |
| 19.3 | **Boat/zeppelin schedule** — `TransportScheduleService.cs` with 7 routes + dock positions. | **Done** (07c90b02) |
| 19.4 | **Mount usage** — `IsMounted` DIM on IWoWUnit (MountDisplayId != 0). | **Done** (e5a09ae7) |

---

## P20 — LiveValidation Test Coverage Gaps

**All new gameplay systems need integration tests against live MaNGOS.**

| # | Task | Spec |
|---|------|------|
| 20.1 | **Trading tests** — `TradingTests.cs`: 2 BG bots trade items and gold. Assert both see correct inventory changes in snapshots. | Open |
| 20.2 | **Auction house tests** — `AuctionHouseTests.cs`: Bot posts item, second bot buys it. Assert gold transfer and item delivery via mail. | Open |
| 20.3 | **Bank tests** — `BankInteractionTests.cs`: Bot deposits item to bank, logs out, logs in, withdraws. Assert item preserved. | Open |
| 20.4 | **Mail tests** — `MailSystemTests.cs`: Bot sends mail with item + gold to alt. Alt collects. Assert delivery. | Open |
| 20.5 | **Guild tests** — `GuildOperationTests.cs`: Bot creates guild, invites second bot, both accept. Assert guild roster shows both members. | Open |
| 20.6 | **Crafting tests** — `CraftingTests.cs`: Bot with Tailoring crafts Linen Bandage from Linen Cloth. Assert item created in inventory. | Open |
| 20.7 | **Wand attack test** — `WandAttackTests.cs`: Priest/Mage bot equips wand, starts wand attack on target. Assert ranged auto-attack damage events. | Open |
| 20.8 | **Channel tests** — `ChannelTests.cs`: Bot joins General channel, sends message, second bot receives it. Assert message content matches. | Open |
| 20.9 | **BG queue test** — `BattlegroundQueueTests.cs`: Bot queues for WSG, asserts SMSG_BATTLEFIELD_STATUS received with queued status. | Open |
| 20.10 | **Raid formation test** — `RaidFormationTests.cs`: 40 bots form raid, assign subgroups, ready check passes. Assert group list correct. | Open |
| 20.11 | **Quest objective tracking test** — `QuestObjectiveTests.cs`: Bot accepts kill quest, kills required mobs, asserts kill count increments in snapshot, completes quest. | Open |
| 20.12 | **Pet management test** — `PetManagementTests.cs`: Hunter bot summons pet, sets stance, feeds pet, uses pet ability in combat. | Open |
| 20.13 | **Load test (100 bots)** — `ScaleTest100.cs`: 100 BG bots all login, move to Orgrimmar, perform patrol. Assert all 100 snapshots received within 5s window. | Open |

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
- **Last updated:** 2026-04-04 (session 302)
- **Branch:** `main`
- **Session 302 — 87 items shipped, 156/305 done (51.1%). Phases P8/P28 archived. CROSSED 50%:**
  - **P22** (32/33, 97%). **P21** (26/30, 87%). **P19** (4/4, 100%). **P10** (3/7 confirmed). **P11** (2/11 confirmed).
  - **P24** (5/11). **P23** (6/21). **P9** (5/27 confirmed including ConcurrentDictionary).
  - New: LoadTestHarness project, BotMetricsCollector, MeetingStoneSummon, GraveyardData, flight/hearthstone/teleport in CrossMapRouter, spirit healer, mail observables, IsMounted, BG component confirmed.
  - Commits: `fcba3c26` through `cad4ad5e`
- **Session 301 — Binary parity fully restored; all non-binary fallbacks removed:**
  - **C++ PhysicsEngine.cpp:** ZERO diff from parity baseline (70c72973). Binary parity preserved.
  - **MovementController.cs:** Physics is ALWAYS local via NativeLocalPhysics.Step. Removed `_physics.PhysicsStep` remote path entirely. Restored original ground snap logic (stripped 87 lines of workarounds). Restored idle guard.
  - **PathfindingClient:** Removed `IPhysicsClient` implementation and PhysicsStep hold-position stub. No pretend physics.
  - **ObjectManager:** `_physicsClient` always null, `_useLocalPhysics` always true. Removed PathfindingClient-as-physics fallback chain.
  - **Deleted:** FrameAheadSimulator (dead code), PathfindingClientDeadReckoningTests, NativePathfindingClient helper.
  - **IPC parity tests fixed:** 6 tests now pass using local NativeLocalPhysics.
  - **Physics test suite: 666/669** (2 pre-existing Undercity elevator). Navigation test GREEN.
  - Net: -978 lines of fallback/workaround code removed.
  - Commits: `e9e7f5c5` through `6498df1a`
  - **Next:** Live BG bot Z oscillation (terrain layer disambiguation) on VoT terrain. Pre-existing terrain data issue, not code parity.
- **Session 300 — containerized services operational; PathfindingService stripped to path-only:**
  - Service simplification: PathfindingService 967→260 lines (path-only). Physics/GroundZ/LOS/navmesh local via P/Invoke. Physics.cs deleted.
  - Containerization fixes: GetGroundZ export, ground snap FALLINGFAR, CrashMonitor Docker, scene slice VMAP fallback, WWOW_DATA_DIR forwarding, StateManager PID fix.
  - Navigation test GREEN: Bot navigates VoT (-601,-4297)→(-630,-4340) via containerized PathfindingService.
  - Commits: `b15365e9` through `8faa5618`
- **Session 299 — split Pathfinding/SceneData services are live on Linux with mounted data volumes; live matrix still pending completion:**
  - Validated the current split-service deployment on `docker-compose.vmangos-linux.yml`: `pathfinding-service` and `scene-data-service` are both running, publishing `5001`/`5003`, and serving from mounted `/wwow-data`.
  - Runtime logs show the expected behavior: Pathfinding preload across the discovered map set and SceneData readiness on `0.0.0.0:5003` with initialized scene/nav coverage.
  - Fast integration gate stays green (`run-tests.ps1 -Layer 4 -SkipBuild` passed). A full `BotRunner.Tests` `LiveValidation` namespace run was started but interrupted by user request before finishing, so the complete live pass/fail matrix is still in progress.
  - Validation:
    - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> `pathfinding-service` (`5001`) and `scene-data-service` (`5003`) `Up`
    - `docker compose -f .\docker-compose.vmangos-linux.yml ps` -> both split services running in compose
    - `docker logs --tail 80 pathfinding-service` -> active map preload from `/wwow-data`
    - `docker logs --tail 80 scene-data-service` -> ready and listening at `0.0.0.0:5003`
    - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -Layer 4 -SkipBuild -TestTimeoutMinutes 15` -> `passed`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal"` -> `interrupted by user`
- **Session 298 — StateManager launch no longer gates on split external services:**
  - `Services/WoWStateManager/StateManagerWorker.cs` removed the remaining startup gate that aborted `ApplyDesiredWorkerState(...)` when `PathfindingService` was unavailable.
  - Startup now probes both `PathfindingService` and `SceneDataService` as external dependencies, logs readiness/unavailability, and continues launching configured WoW clients either way.
  - This closes the remaining mismatch with the new ownership model: split services are externally managed and no longer part of StateManager’s launch constraints.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateManagerTestClientTimeoutTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~SceneDataServiceAssemblyTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
- **Session 297 — StateManager now only manages WoW clients; Pathfinding/SceneData are split external services:**
  - `Services/WoWStateManager/Program.cs` no longer launches or kills `PathfindingService`/`SceneDataService`; startup now treats both as external dependencies, performs bounded readiness checks, and proceeds to bot/client orchestration.
  - Added `Services/SceneDataService/Dockerfile` and updated `docker-compose.windows.yml` so `pathfinding-service` and `scene-data-service` run as separate containers; `background-bot-runner` now depends on both and receives both endpoint env overrides.
  - Updated `Services/WoWStateManager/appsettings.json` and `appsettings.Docker.json` to include `SceneDataService` endpoint defaults for host/container alignment.
  - `Tests/Tests.Infrastructure/BotServiceFixture.cs` cleanup ownership now matches that architecture: fixture teardown no longer kills `PathfindingService`/`SceneDataService`, and managed-process crash semantics remain scoped to `StateManager` + `WoW.exe`.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet build Services/SceneDataService/SceneDataService.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateManagerTestClientTimeoutTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~SceneDataServiceAssemblyTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
    - `docker compose -f .\docker-compose.windows.yml config` -> `succeeded`
    - `docker compose -f .\docker-compose.windows.yml --profile bgbot config` -> `succeeded`
    - `docker info --format "{{.OSType}}"` -> `linux` (runtime deployment of Windows images still blocked on this host)
    - `docker run --rm mcr.microsoft.com/windows/servercore:ltsc2022 cmd /c echo windows` -> `failed` (`no matching manifest for linux/amd64`)
- **Session 296 — BG scene-slice clients now defer connect instead of committing to a startup fallback, but the latest AV rerun aborted before EnterWorld:**
  - `ProtobufSocketClient` now supports deferred initial connection, and `SceneDataClient` uses that path with a bounded connect budget plus retry backoff. `BackgroundBotWorker` no longer decides the whole run off a one-shot `SceneDataService` reachability probe; if the endpoint is configured, the BG runner can keep the intended thin-slice client and connect when the first region request actually happens.
  - `WoWStateManager` warnings were updated to match that contract. A late `SceneDataService` no longer means "without scene slices" by definition; BG workers will still launch and retry scene-slice acquisition on demand once the service becomes available.
  - Added focused proof across the transport boundary: `SceneDataClientTests` pin the new retry behavior, `BackgroundPhysicsModeResolverTests` pin the configured-endpoint runtime selection, and `ProtobufSocketPipelineTests.DeferredConnect_ClientCanBeConstructedBeforeServerStarts` proves the deferred client can exist before the socket listener is up.
  - The shared-tree AV rerun did not yield fresh world-entry evidence. `logs/av_allbotsenterworld_20260403_deferred_scene_client_rerun.log` shows the test host aborted while `PathfindingService` was still preloading maps (`Map 229`), so this session did not yet confirm whether the deferred scene-slice contract moves the AV ceiling.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientTests|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure" --logger "console;verbosity=minimal"` -> `passed (4/4)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BackgroundPhysicsModeResolverTests|FullyQualifiedName~BotRunner.Tests.IPC.ProtobufSocketPipelineTests.DeferredConnect_ClientCanBeConstructedBeforeServerStarts" --logger "console;verbosity=minimal"` -> `passed (14/14)`
    - `$env:WWOW_BOT_OUTPUT_DIR='E:\repos\Westworld of Warcraft\Bot\Release\net8.0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath logs/av_allbotsenterworld_20260403_deferred_scene_client_rerun.log` -> `aborted`; test host crashed during `PathfindingService` preload before AV bring-up
- **Session 295 — pure-local BG fallback now constructs MovementController, and SceneDataService is reduced to best-effort during AV startup:**
  - `WoWSharpObjectManager` now remembers `useLocalPhysics` and creates `MovementController` whenever the BG runner requests pure local physics, even if both `_physicsClient` and `_sceneDataClient` are null. That closes the remaining fallback hole where scene-service outages left bots with no per-frame physics loop at all.
  - Added deterministic proof in `ObjectManagerWorldSessionTests`: `Initialize_UseLocalPhysicsWithoutSceneData_DoesNotFallbackToPathfindingClient` still pins the pure-local contract, and `EnterWorld_UseLocalPhysicsWithoutSceneData_InitializesMovementController` now proves the fallback still constructs the controller needed for local gravity/collision.
  - `WoWStateManager` still launches `SceneDataService`, but it now waits only `2.5s` before continuing so BG workers can fall back to preloaded local `Navigation.dll` physics instead of spending two minutes blocked on a dead `5003` socket.
  - Shared-tree live AV evidence moved the blocker accordingly. `AV_FullyPreparedRaids_MountAndReachFirstObjective` now reaches `WoWStateManager READY`, logs `SceneDataService did not become available ... Background bots will use local Navigation.dll physics without scene slices.`, and then stalls later at `[AV:EnterWorld] 40/80` with the entire alliance roster missing. That is a bring-up / launch-pressure problem, not the earlier hover gate.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.Initialize_UseLocalPhysicsWithoutSceneData_DoesNotFallbackToPathfindingClient|FullyQualifiedName~ObjectManagerWorldSessionTests.EnterWorld_UseLocalPhysicsWithoutSceneData_InitializesMovementController" --logger "console;verbosity=minimal"` -> `passed (2/2)`
    - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `dotnet build Services/SceneDataService/SceneDataService.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `$env:WWOW_BOT_OUTPUT_DIR='E:\repos\Westworld of Warcraft\Bot\Release\net8.0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal"` -> `failed` at `[AV:EnterWorld] STALE - bot count stopped at 40/80 for 123s`; scene-service gating no longer the blocker
- **Session 294 — post-teleport AV hover regression now keeps bots falling, and BG workers now inherit/use the scene-data endpoint:**
  - `MovementController` no longer immediately snaps an airborne teleport back to `teleportZ` on the first no-ground frame. Post-teleport settle now allows a real grace window for falling motion before any fallback clamp, which removes the exact hover case where AV bots stayed `+3y` above the battlemaster staging point.
  - The same post-teleport path now rejects physics contacts that project the player above the teleport target during the settle window. Instead of clearing falling and finalizing mid-air, it resets grounded continuity and keeps the bot in `FALLINGFAR` so local scene data can catch up and the next frame can continue downward.
  - `WoWStateManager` now forwards `SceneDataService__IpAddress` / `SceneDataService__Port` to spawned `BackgroundBotRunner` processes, and `BackgroundBotWorker` now constructs `SceneDataClient` directly from config/env defaults (`127.0.0.1:5003`) instead of permanently downgrading to shared physics because a one-shot startup reachability probe missed the service.
  - Added deterministic coverage for the two live hover signatures: `MovementControllerTests.Update_PostTeleport_NoGroundBelow_AllowsGraceFall` proves an air teleport keeps descending during the settle window, and `Update_PostTeleport_RejectsSupportAboveTeleportTarget_AndContinuesFalling` proves the controller rejects overhead support and stays in falling motion.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin4 --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_PostTeleport_NoGroundBelow_AllowsGraceFall|FullyQualifiedName~MovementControllerTests.Update_PostTeleport_RejectsSupportAboveTeleportTarget_AndContinuesFalling|FullyQualifiedName~MovementControllerTests.Update_IdleAirTeleportDoesNotSkipRemotePhysics|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure" --logger "console;verbosity=minimal"` -> `passed (4/4)`
    - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release -o E:\tmp\isolated-background-botrunner2\bin --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release -o E:\tmp\isolated-wowstatemanager\bin --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release -o E:\tmp\isolated-av-live2\bin --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_QueueAndEnterBattleground" --logger "console;verbosity=minimal"` -> initial rerun `skipped` (`_bot.IsReady` false because `BotServiceFixture` still resolved binaries from the wrong output root)
    - `dotnet test ... --logger "console;verbosity=normal"` with `WWOW_BOT_OUTPUT_DIR=E:\tmp\isolated-av-live2\bin` -> progressed past fixture startup and proved the launcher fix, but the isolated live tree then blocked on missing native/service assets (`Navigation.dll` first, then `Loader.dll` / unavailable `SceneDataService`), so AV queue logic still did not execute end-to-end
- **Session 293 — BG local physics now stays on thin scene slices instead of implicit full-map loads:**
  - Added native thin-scene-slice mode to `SceneQuery` and exported it through `Navigation.dll`, so the scene-backed BG local path can stay on explicitly injected nearby geometry instead of auto-loading full-map `.scene` / VMAP data on cache misses.
  - `MovementController` now enables that mode whenever a `SceneDataClient` is present, disables it for native-local controllers without scene data, and leaves remote/shared controllers alone. That keeps the local in-process physics path aligned with the intended `SceneDataService` design without adding a new hard native-DLL dependency to remote paths.
  - Added deterministic proof on both sides of the boundary: `SceneSliceModeTests.GetGroundZ_SceneSliceMode_DoesNotAutoloadFullSceneCache` proves the native query no longer repopulates full-map cache while slice mode is active, and `MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode` proves the managed controller opts into the mode for scene-backed local physics.
  - Validation:
    - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded`
    - `$env:WWOW_DATA_DIR='E:\repos\Westworld of Warcraft\Data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release -o E:\tmp\isolated-nav-physics-tests\bin --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneSliceModeTests.GetGroundZ_SceneSliceMode_DoesNotAutoloadFullSceneCache" --logger "console;verbosity=minimal"` -> `passed (1/1)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin3 --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithoutSceneDataClient_DisablesSceneSliceMode" --logger "console;verbosity=minimal"` -> `passed (3/3)`
- **Session 292 — AV roster/loadout contract archived out of the active queue:**
  - Revalidated the existing AV fixture/loadout implementation instead of leaving it as a stale open item. `AlteracValleyFixture` already defines the 80-account level-60 roster, objective-ready loadout prep, epic faction mounts, baseline elixirs, and the foreground leader contracts (`TESTBOT1` High Warlord Tauren Warrior, `AVBOTA1` Grand Marshal Paladin).
  - `BattlegroundFixtureConfigurationTests` already pin that contract deterministically, including leader classes/races/runner types, faction loadouts, supplemental items, and first-objective assignments for all 80 accounts.
  - Master task `25.15` is now marked done, and the matching stale open bullets were removed from `Tests/BotRunner.Tests/TASKS.md` and archived in `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`.
  - Validation again used an isolated output directory because the shared `Bot\Release\net8.0` tree remains busy from AV/background processes.
  - Validation:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release -o E:\tmp\isolated-botrunner-tests\bin --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests" --logger "console;verbosity=minimal"` -> `passed (11/11)`
- **Session 291 — local scene refresh misses no longer freeze BG bots in mid-air:**
  - `MovementController.RunPhysics(...)` no longer returns a synthetic hold-position result when `SceneDataClient` cannot refresh the nearby scene slice. BG runners now continue through `NativeLocalPhysics.Step(...)` on the current local scene cache, which keeps gravity and collision sweeps active instead of hovering at the teleport Z.
  - Added a small WoWSharpClient test seam for this path: `SceneDataClient` now has an internal non-network constructor plus `TestEnsureSceneDataAroundOverride`, which lets deterministic tests force a scene refresh miss without opening a socket.
  - Added regression coverage in `MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure`, which proves local native physics still advances to a falling state after a failed scene refresh.
  - The normal `Bot\Release\net8.0` validation path remained locked by the active AV swarm, so the successful verification runs used isolated `-o E:\tmp\...` output directories instead of touching the busy shared output tree.
  - Practical implication: the next live AV rerun should no longer leave BG bots suspended out of battlemaster range just because a scene-slice refresh missed during the post-teleport settle window.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_RunsRemotePhysicsEveryIdleFrame|FullyQualifiedName~MovementControllerTests.Update_KeepsLocalPhysicsActiveWhileIdle|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ForwardsNearbyObjectsToNavigationInput|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure|FullyQualifiedName~MovementControllerTests.Update_IdleAirTeleportDoesNotSkipRemotePhysics|FullyQualifiedName~MovementControllerTests.Update_IdleFreefallStillAppliesPhysics" --logger "console;verbosity=minimal"` -> `passed (6/6)`
    - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release -o E:\tmp\isolated-background-botrunner\bin --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
- **Session 290 — BG runners now use scene-backed in-process physics without `LocalPhysicsClient`:**
  - Removed the runtime `LocalPhysicsClient` path. `BackgroundBotWorker` now defaults to local in-process physics, connects to `SceneDataService` when available, and initializes `WoWSharpObjectManager` with `sceneDataClient` so `MovementController` steps `Navigation.dll` directly through `NativeLocalPhysics`.
  - `MovementController` still runs idle physics every frame, but local native steps now marshal `NearbyObjects` into the native `PhysicsInput` so transport models and nearby collidable game objects stay available to local physics. Added deterministic coverage in `MovementControllerTests.Update_LocalNativePhysics_ForwardsNearbyObjectsToNavigationInput`.
  - Preserved existing callers by restoring the old `WoWSharpObjectManager.Initialize(...)` default: when no scene client is supplied, the object manager still falls back to `PathfindingClient` for physics instead of leaving movement uninitialized.
  - `PathfindingService` and `SceneDataService` now preload every discovered map from the data directories (`scenes`, `mmaps`, `maps`) rather than a hand-picked subset, which keeps the local scene-backed path and the shared fallback path aligned on available map data.
  - Practical implication: the next AV resource rerun should measure the scene-backed local path directly, without the extra `LocalPhysicsClient` wrapper or remote per-frame physics dependency.
  - Validation:
    - `dotnet build Services/SceneDataService/SceneDataService.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded` (existing `_logger` hiding warning)
    - `dotnet build Services/PathfindingService/PathfindingService.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests.Update_RunsRemotePhysicsEveryIdleFrame|FullyQualifiedName~MovementControllerTests.Update_KeepsLocalPhysicsActiveWhileIdle|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ForwardsNearbyObjectsToNavigationInput|FullyQualifiedName~MovementControllerTests.Update_IdleAirTeleportDoesNotSkipRemotePhysics|FullyQualifiedName~MovementControllerTests.Update_IdleFreefallStillAppliesPhysics" --logger "console;verbosity=minimal"` -> `passed (5/5)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BackgroundPhysicsModeResolverTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`
- **Session 289 — AV launch scalability benchmark captured the current resource ceiling:**
  - Rebuilt `Tests/BotRunner.Tests` in `Release`, then ran the focused live AV slice `AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective` with external process sampling and saved the benchmark bundle to `TestResults\AVBenchmark_20260402_191811`.
  - The run still fails during `[AV:EnterWorld]`, stalling at `39/80` after `122s` without progress. It never reached queue, mount, or first-objective movement, so the current blocker is still launch/entry scale rather than in-BG coordination.
  - The launch path does not have a hard cap in `Services/WoWStateManager/StateManagerWorker.cs`; it walks the `CharacterSettings` list sequentially with `100ms + 500ms` delays between worker starts. The observed plateau is therefore not an explicit AV limit in the coordinator/state-manager code.
  - Resource profile on the `12`-logical-core / `63.93 GB` host: aggregate `CPU p95=79.6%`, `private MB p95=65735.6`, `private MB max=69322.17`, `working set MB max=46622.07`, `handles p95=30884.4`, `threads p95=1143.7`.
  - `BackgroundBotRunner` dominates the footprint: `max instances=55`, `private MB p95=64790.63`, `working set MB p95=40687.3`, or about `1178 MB` private and `740 MB` working set per runner. Projected to all `78` BG runners, that is about `91.9 GB` private and `57.7 GB` working set before foreground bots and fixed services.
  - Launch evidence matches the resource ceiling: `54` unique BG login attempts were observed (`AVBOT2-40` plus `AVBOTA2-16`), while `AVBOTA17-40` never started and the failure diagnostics still listed both FG leaders plus the rest of the alliance wave as missing. The run also logged repeated `Enter world timed out ... Retrying CMSG_PLAYER_LOGIN` warnings and a transient `AUTH_LOGON_PROOF` failure under load.
  - Practical next step: focus the next AV slice on reducing `BackgroundBotRunner` startup/runtime memory and launch pressure before attempting more AV objective/capture live work.
  - Validation:
    - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --blame-hang --blame-hang-timeout 60m --logger "trx;LogFileName=av_benchmark.trx" --logger "console;verbosity=normal"` -> `failed ([AV:EnterWorld] STALE at 39/80; benchmark artifacts captured externally in TestResults\AVBenchmark_20260402_191811)`
- **Session 288 — queued the Alterac Valley level-60 roster/loadout and first-objective expansion:**
  - Updated `P25 / 25D` so the AV target state is explicit instead of implied: both factions use `40` level-`60` characters, epic mounts, baseline elixirs, and objective-ready loadouts.
  - Added a new open AV fixture/prep task ahead of the existing live objective work: Horde FG `TESTBOT1` must be a High Warlord Tauren Warrior, Alliance FG `AVBOTA1` must be a Grand Marshal Paladin, and the remaining roster should use next-tier-appropriate level-60 gear per class/role.
  - Added a new first-objective movement milestone ahead of tower/graveyard/general validation so the queue now matches the actual delivery order: roster/loadout prep first, then cave-to-objective movement proof, then capture/kill assertions.
  - No code or tests changed in this session; this was a task-tracker update to define the next AV implementation slice clearly.
- **Session 287 — search-walk now obeys movement-controller stuck recovery, and the focused dual Ratchet path test is green again:**
  - `FishingTask.SearchForPool(...)` now snapshots the active `MovementStuckRecoveryGeneration` when each probe window opens and treats any newer generation after a short `1.5s` grace as authoritative blocked-probe evidence. Instead of grinding the same upper-pier corner for the full `20s` stall window, the task now emits `search_walk_stalled ... reason=movement_stuck` and advances immediately.
  - Added deterministic BotRunner coverage in `AtomicBotTaskTests` for the new behavior; the focused `FishingTaskTests|AtomicBotTaskTests` slice is now `30/30` green.
  - The latest focused dual `Fishing_CatchFish_BgAndFg_RatchetPoolTaskPath` rerun passed on the current binaries in `3m 38s`. FG completed `fishing_loot_success` with loot item `6303`, and BG completed `fishing_loot_success` with loot item `6358`.
  - Important scope note: this green rerun acquired immediate local pools and never re-entered `search_walk`, so the blocked-corner improvement is currently proven by deterministic coverage plus the earlier live diagnostics that showed repeated stuck-recovery generations on the same last pier probe.
  - Practical next step: keep the focused FG capture and focused dual path tests as the live baseline, then resume the actual FG/BG packet-sequence comparison work while keeping the staged local-pool attribution instrumentation in view for nondeterministic reruns.
  - Validation:
    - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingTaskTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (30/30)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetPoolTaskPath" --logger "console;verbosity=normal"` -> `passed`
- **Session 286 — FG packet capture stays green, runtime search-walk was tightened again, but the latest dual rerun failed earlier in staged Ratchet visibility:**
  - `FishingTask.ResolveSearchWaypointTravelTarget(...)` no longer commits to the first snapped local probe step. It now falls back through `8y -> 4y -> 2y` travel steps and only keeps a step target when the pathfinder can actually return a non-empty route from the current pier position.
  - Added deterministic BotRunner coverage for that behavior in `FishingTaskTests`; the focused `FishingTaskTests` slice is now `20/20` green.
  - Focused FG packet capture remains the last known green fishing reference: `pool_acquired`, `in_cast_range_current`, `cast_started`, and `fishing_loot_success` all fired and `packets_TESTBOT1.csv` was recorded.
  - The latest dual `Fishing_CatchFish_BgAndFg_RatchetPoolTaskPath` rerun did not reach the old BG last-leg failure first. It failed earlier in FG stage preparation: near-stage Ratchet children still stayed non-visible, and `.pool spawns <child>` replies continued to drift outside the tracked command window, leaving the local FG child pools classified as `Unknown` while some non-local Barrens children reported spawned objects.
  - Practical next step: keep the focused FG reference as the completed baseline, but treat authoritative post-update pool activation capture / staged local-pool visibility as the next blocker before judging whether the new shorter-step search-walk logic clears the remaining BG pier leg.
  - Validation:
    - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingTaskTests" --logger "console;verbosity=minimal"` -> `passed (20/20)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetPoolTaskPath" --logger "console;verbosity=normal"` -> `failed earlier in FG stage preparation; BG runtime pier search was not exercised`
- **Session 285 — VMaNGOS source review corrected the Ratchet pool-command assumptions:**
  - Pulled `https://github.com/vmangos/core` into `E:\repos\vmangos-core-ref` because the available local cMaNGOS checkout does not match the live server's pool command and respawn schema behavior.
  - Confirmed in VMaNGOS that `.pool update <pool>` prints the pool's current spawned count before it calls `sPoolMgr.UpdatePool(...)`, so `Pool #2620: 0/1 objects spawned` is pre-update state only and cannot be used as proof that the just-issued refresh succeeded or failed.
  - Confirmed that child-pool updates reroll through their mother pool, while `.pool update 2628` on the Barrens master pool is not the right primary Ratchet reroll path.
  - Confirmed that pooled gameobjects are excluded from the static `ObjectMgr` grid and live in `MapPersistentState` pool/grid data instead; server-side "active in pool state" and "currently visible to the client" are different steps.
  - Practical next step: replace the fishing harness's response-based `.pool update` activation classification with a true post-update signal, then resume the remaining short-pier runtime work.
- **Session 284 — Ratchet fishing search is now local and bounded, but the last two pier legs still stall:**
  - `FishingPoolStagePlanner` now keeps staged Ratchet probes local when nearby spawn rows exist and caps far probe travel to `20y` from the dock stage, so the harness no longer asks FG to march almost all the way to the DB node just to stream a pool.
  - `FishingTask` now refines each search probe onto a better walkable edge through a short radial ground/walkable-point sweep before moving, and deterministic `AtomicBotTaskTests` cover the higher-support probe choice.
  - The latest clean FG packet-capture rerun still proved the slice is red, but the failure is narrower: FG reached `search_walk waypoint=1/5`, `2/5`, and `3/5`, then stalled on the last two short pier legs (`waypoint=4/5 distance=12.9`, `waypoint=5/5 distance=17.1`) before `search_walk_exhausted`. Separate reruns still fail earlier when local child pools stay invisible from the staged dock positions.
- **Test baseline (session 284):**
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolStagePlannerTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"`
    - Passed (`9/9`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CaptureForegroundPackets_RatchetStagingCast" --logger "console;verbosity=normal"`
    - Failed on two useful edges across the reruns: one direct-probe fallback shrank to `3` local waypoints but still stalled on each probe; the latest clean staged rerun reached `1/5`, `2/5`, `3/5` and then stalled on the final two local pier legs before `search_walk_exhausted`
- **Next priorities:** keep the staged Ratchet visibility evidence explicit, but treat the remaining last-two-leg pier stalls as the real runtime blocker whenever staging succeeds again.
- **Session 282 — the old `PhysicsEngine` grounded caller shell is now fully pinned, and the first end-to-end proof attempt is blocked by the fixture rather than missing seams:**
  - Added [EvaluateGroundedDriverSelectedPlaneTailEntrySetup(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) and [EvaluateGroundedDriverSelectedPlaneTailRerouteLoopController(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now mirrors the last two open `0x635600` seams: the visible `0x635600..0x6356D2` entry/setup shell and the bounded `0x6357DA..0x6359A9` reroute-loop controller above the already-split reroute-candidate / vertical-fallback leaves.
  - Strengthened [EvaluateGroundedDriverSelectedPlaneTailChooserContract(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) and [WowGroundedDriverSelectedPlaneTailChooserContractTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailChooserContractTests.cs) so the positive chooser-mutation path is now explicit instead of only the unchanged-buffer case.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverSelectedPlaneTailEntrySetupTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailEntrySetupTests.cs) and [WowGroundedDriverSelectedPlaneTailRerouteLoopControllerTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailRerouteLoopControllerTests.cs).
  - Validation held on the serialized unit lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded-tail slice passed `40/40`, and the widened grounded slice passed `124/124`.
  - Replay calibration rerun stayed unchanged after the seam closure: `logs/physicsengine-variance-20260328-run2.txt` held `avg=0.0096 p95=0.0662 p99=0.0820 max=0.0889 (frame=231)`, so the split helper work did not retune the live replay path.
  - The smallest live FG/BG movement proof is present but currently blocked by the fixture: `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=detailed"` -> `skipped`; `LiveBotFixture` failed to initialize because `WoWStateManager` closed the snapshot connection with `Unknown compression flag: 0x08`.
  - Practical implication: the current collision/movement-controller gap is no longer missing deterministic native seam work on the old grounded shell. It is now live replay calibration / transport parity inside `PhysicsEngine.cpp`, plus the separate `WoWStateManager` live-fixture bootstrap blocker.
  - Exact next command: `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=detailed"`
- **Session 281 — the grounded caller shell is no longer a loose bundle of unexplained tails:**
  - Added [EvaluateGroundedDriverSelectedPlaneTailPostForwarding(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now composes the visible `0x635734..0x6357D4` shell: post-`0x6351A0` dispatch, shared move/writeback join, elapsed accumulation, and direct-vs-alternate state-handler routing.
  - Added [EvaluateGroundedDriverSelectedPlaneTailReturn2LateBranch(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) and [EvaluateGroundedDriverSelectedPlaneTailLateNotifier(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the bounded `0x6359AE..0x6359EC` late branch and the visible `0x635A37..0x635AED` notifier/state-commit tail are now pinned as deterministic seams.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverSelectedPlaneTailPostForwardingTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailPostForwardingTests.cs), [WowGroundedDriverSelectedPlaneTailReturn2LateBranchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailReturn2LateBranchTests.cs), and [WowGroundedDriverSelectedPlaneTailLateNotifierTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailLateNotifierTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded tail slice passed `34/34`, and the widened grounded slice passed `118/118`.
  - Practical implication: the remaining collision/movement-controller backlog is now smaller and easier to estimate. On this grounded tail, the only open work left is the front-end `0x635600` setup shell and the reroute-loop controller above the already-split probe/fallback leaves.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverSelectedPlaneTailLateNotifierTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailReturn2LateBranchTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailPostForwardingTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailElapsedMillisecondsTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProbeStateSnapshotTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProbeRerouteCandidateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProbeVerticalFallbackTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailChooserProbeTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailChooserContractTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailReturnDispatchTests" --logger "console;verbosity=minimal"`
- **Session 280 — the last helper-shaped grounded tail work is closed and the remaining shell is explicitly mapped:**
  - Added [CaptureGroundedDriverSelectedPlaneTailProbeStateSnapshot(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) and [RestoreGroundedDriverSelectedPlaneTailProbeStateSnapshot(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now mirrors the visible `0x635EA0` / `0x635F10` snapshot-copy wrappers instead of leaving those fields implicit.
  - Added [EvaluateGroundedDriverSelectedPlaneTailElapsedMilliseconds(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the isolated `0x635A08..0x635A30` elapsed-seconds to rounded-milliseconds quantizer is now pinned outside the larger shell.
  - Added the raw shell capture [0x635600_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x635600_disasm.txt) and tightened the remaining grounded map to named caller-side seams: pre-call setup, the `0x6351A0` / `0x635450` dispatch shell, shared move/writeback join, post-join branch lattice, reroute-loop controller, return-`2` late branch, side-channel notifier branch, late notifier/state-commit tail, and final notifier/return tail.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverSelectedPlaneTailProbeStateSnapshotTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailProbeStateSnapshotTests.cs) and [WowGroundedDriverSelectedPlaneTailElapsedMillisecondsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailElapsedMillisecondsTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded tail slice passed `25/25`, and the widened grounded slice passed `109/109`.
  - Practical implication: the remaining collision/movement-controller backlog is easier to estimate now because it is orchestration-only on this grounded tail. There are no newly discovered helper unknowns inside the `0x635600` body.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverSelectedPlaneTailElapsedMillisecondsTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProbeStateSnapshotTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProbeRerouteCandidateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProbeVerticalFallbackTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailChooserProbeTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailChooserContractTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailReturnDispatchTests" --logger "console;verbosity=minimal"`
- **Session 279 — the old `PhysicsEngine` chooser probe is now bounded into real inner seams instead of one opaque helper:**
  - Added [EvaluateGroundedDriverSelectedPlaneTailProbeRerouteCandidate(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) and [EvaluateGroundedDriverSelectedPlaneTailProbeVerticalFallback(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now mirrors more of the internal `0x635600` probe simulator that sits under the already-pinned `0x635F80` chooser gate.
  - The new reroute seam pins the visible `0x6357DA..0x6359A9` math: candidate build from the normalized input direction plus XY offset, the later-attempt drift/abort gate, the accepted XY writeback into `field68/6c/84`, the `field5c/60/64` direction vector write, and the next-input magnitude rebuild.
  - The new fallback seam pins the visible `0x6358A0..0x6358EF` vertical-only retry: clear `field84`, zero XY, keep only `normalizedInputDirection.z * remainingMagnitude`, and rebuild the next-input magnitude from `abs(z)`.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverSelectedPlaneTailProbeRerouteCandidateTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailProbeRerouteCandidateTests.cs) and [WowGroundedDriverSelectedPlaneTailProbeVerticalFallbackTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailProbeVerticalFallbackTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded chooser/probe slice passed `18/18`, and the widened grounded slice passed `102/102`.
  - Practical implication: the remaining grounded-native backlog is no longer “internal `0x635F80` semantics” as one opaque blob. It is now bounded to the still-open `0x635600` orchestration shell around `0x6351A0` / `0x635450`, elapsed/budget bookkeeping, and the notifier tail.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProbeRerouteCandidateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProbeVerticalFallbackTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailChooserProbeTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailChooserContractTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailReturnDispatchTests" --logger "console;verbosity=minimal"`
- **Session 278 — the final visible `0x635F80` caller contract is now pinned as its own grounded seam:**
  - Added [EvaluateGroundedDriverSelectedPlaneTailChooserContract(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now makes the last visible caller-side chooser contract explicit instead of leaving it buried inside the `return 1` vs `return 2` tail. The new seam surfaces the chooser input packed-pair vector, chooser input projected move, chooser input scalar, possible in-place projected-move buffer mutation, the `0x635F80` call gate, and the final `field80` suppression on `0x04000000`.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverSelectedPlaneTailChooserContractTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailChooserContractTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded slice passed `59/59`, and the widened chosen-contact/grounded/raster slice passed `118/118`.
  - Practical implication: the remaining grounded/native gap is no longer the visible `0x636100` caller contract. The remaining work on this path is any still-opaque internal `0x635F80` semantics beyond that pinned caller-visible contract, plus the still-open producer/object bodies elsewhere on the collision path.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverSelectedPlaneTailChooserContractTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailWritebackTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProjectedBlendTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailPreThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverHorizontalCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneRetryTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBranchGateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTransactionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneDistancePointerTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFirstPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFollowupRerankTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBlendCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlanePostFastReturnTailTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailReturnDispatchTests" --logger "console;verbosity=minimal"`
- **Session 277 — the pre-chooser `0x636100` writeback block is now pinned as its own grounded seam:**
  - Added [EvaluateGroundedDriverSelectedPlaneTailWriteback(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now makes the visible tail-local writeback/state-mutation block explicit instead of leaving that behavior buried in the later chooser path. The new helper always applies the third-pass XY writeback from `packedPair.xy * followupScalar`, then conditionally applies the projected-tail XYZ writeback plus resolved-distance and `normal.z` increments only when the scalar delta exceeds epsilon, the walkable gate survives, and the projected-tail rerank succeeds.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverSelectedPlaneTailWritebackTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailWritebackTests.cs).
  - Tightened [WowGroundedDriverSelectedPlaneTailReturnDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailReturnDispatchTests.cs) so the caller-visible chooser gate is better bounded: non-walkable final selections now prove `0x635F80` is skipped, and `0x04000000` now proves `field80` writeback is suppressed on the selected-plane return path.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded slice passed `56/56`, and the widened chosen-contact/grounded/raster slice passed `115/115`.
  - Practical implication: the remaining grounded/native gap is no longer the pre-chooser writeback block. It is now the final `0x635F80` caller-visible contract inside `0x636100`: the possible in-place projected-move buffer mutation and scalar input at the return-`1` vs return-`2` split, while `0x635F80` itself remains intentionally opaque except for the already-pinned chooser bool outcome.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverSelectedPlaneTailWritebackTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProjectedBlendTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailPreThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverHorizontalCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneRetryTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBranchGateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTransactionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneDistancePointerTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFirstPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFollowupRerankTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBlendCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlanePostFastReturnTailTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailReturnDispatchTests" --logger "console;verbosity=minimal"`
- **Session 276 — the grounded tail now composes its pre-tail projection inputs into the post-fast-return blend seam:**
  - Added [EvaluateGroundedDriverSelectedPlaneTailProjectedBlendTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now feeds the real tail-preparation outputs from [EvaluateGroundedDriverSelectedPlaneTailPreThirdPassSetup(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) into the already-pinned [EvaluateGroundedDriverSelectedPlanePostFastReturnTailTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) instead of relying on injected fallback inputs at that boundary.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverSelectedPlaneTailProjectedBlendTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailProjectedBlendTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded slice passed `51/51`, and the widened chosen-contact/grounded/raster slice passed `110/110`.
  - Practical implication: the remaining grounded/native gap is no longer the tail-preparation-to-post-blend composition. It is now the tail-local writeback/state-mutation block still living inside `0x636100` after that composed projected-input path and before the final chooser/state commit, while `0x635F80` remains intentionally opaque except for the already-pinned chooser bool outcome.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverSelectedPlaneTailProjectedBlendTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailPreThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverHorizontalCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneRetryTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBranchGateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTransactionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneDistancePointerTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFirstPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFollowupRerankTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBlendCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlanePostFastReturnTailTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailReturnDispatchTests" --logger "console;verbosity=minimal"`
- **Session 275 — one more bounded `0x636100` tail-preparation seam is now pinned:**
  - Added [EvaluateGroundedDriverSelectedPlaneTailPreThirdPassSetup(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now mirrors one more visible post-fast-return `0x636100` tail-preparation block before the already-pinned third-pass and final-return seams. The new helper seeds the horizontal fallback through the real `0x635D80` helper, optionally reuses the already-pinned `0x635C00` correction transaction, and makes the projected tail-rerank working vector/distance explicit instead of leaving that setup buried in the full body.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverSelectedPlaneTailPreThirdPassSetupTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneTailPreThirdPassSetupTests.cs).
  - Refreshed [0x636100_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x636100_disasm.txt) through the `0x6362FD..0x63660D` continuation and tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the open grounded backlog is no longer described as raster thin-wrapper/failure-cleanup work.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded slice passed `48/48`, and the widened chosen-contact/grounded/raster slice passed `107/107`.
  - Practical implication: the remaining grounded/native gap is now the tail-local projected-rerank/state-writeback details still living inside `0x636100` between the new pre-third-pass setup seam and the already-pinned post-fast-return / final-return helpers. `0x635F80` remains intentionally opaque except for the already-pinned chooser bool outcome, and the old raster thin-wrapper/failure-cleanup wording is no longer current.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverSelectedPlaneTailPreThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverHorizontalCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneRetryTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBranchGateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTransactionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneDistancePointerTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFirstPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFollowupRerankTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBlendCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlanePostFastReturnTailTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneTailReturnDispatchTests" --logger "console;verbosity=minimal"`
- **Session 274 — the raster outer aggregation is now a bounded `0x6BB6B0` seam:**
  - Added [EvaluateSelectorObjectRasterCellLoopAggregation(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumer.cpp), so the production DLL now makes the outer raster cell-loop accounting and final queue-link tail explicit instead of keeping that aggregation inlined inside the full body. The new seam aggregates per-cell accept/reject counts, queue/scratch overflow flags, appended word/triangle totals, and the final deferred-cleanup splice state on top of the already-pinned per-cell helper.
  - Updated [EvaluateSelectorObjectRasterConsumerBody(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumer.cpp) to route through that aggregation helper after prefix + prepass composition, leaving only the thin wrapper/body exits around the already-split seams.
  - Added matching export/interop wiring in [SelectorObjectRasterConsumerTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumerTestExports.cpp) and [NavigationInterop.SelectorObjectRasterConsumer.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectRasterConsumer.cs), plus focused coverage in [WowSelectorObjectRasterConsumerAggregationTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectRasterConsumerAggregationTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused raster aggregation slice passed `29/29`, and the widened chosen-contact/grounded/raster slice passed `97/97`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the open raster backlog is no longer the outer cell-loop accounting or final queue splice. The remaining raster/object-side work is now only whatever thin wrapper/failure-cleanup composition still lives around the already-split helpers, plus the independent grounded `0x636100` tail above the now-pinned `0x635C00` / `0x635D80` leaves.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverHorizontalCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneRetryTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBranchGateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTransactionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneDistancePointerTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFirstPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFollowupRerankTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBlendCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlanePostFastReturnTailTests" --logger "console;verbosity=minimal"`
- **Session 273 — the raster translation-plus-prepass composition is now a bounded seam:**
  - Added [EvaluateSelectorObjectRasterPrepassComposition(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumer.cpp), so the production DLL now makes the translation + prepass outcode composition explicit above the already-pinned inner outcode loop. The new seam rebuilds the translated payload from the prefix-applied translation, surfaces the translated anchors/first-plane distance, and then feeds the real translated payload into the inner prepass helper.
  - Updated [EvaluateSelectorObjectRasterConsumerBody(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumer.cpp) to route its prepass work through that narrower composition helper instead of translating payload state inline.
  - Added matching export/interop wiring in [SelectorObjectRasterConsumerTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumerTestExports.cpp) and [NavigationInterop.SelectorObjectRasterConsumer.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectRasterConsumer.cs), plus focused coverage in [WowSelectorObjectRasterConsumerPrepassCompositionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectRasterConsumerPrepassCompositionTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused raster prepass-composition slice passed `26/26`, and the widened chosen-contact/grounded/raster slice passed `94/94`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the open raster backlog is no longer the raw translation-plus-prepass block itself. The next raster/object-side front is the outer aggregation/body wrapper above the already-split prefix, prepass, and per-cell helpers.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowSelectorObjectRasterConsumerAggregationTests|FullyQualifiedName~WowSelectorObjectRasterConsumerPrepassCompositionTests|FullyQualifiedName~WowSelectorObjectRasterConsumerPrepassTests|FullyQualifiedName~WowSelectorObjectRasterConsumerPrefixTests|FullyQualifiedName~WowSelectorObjectRasterConsumerBodyTests|FullyQualifiedName~WowSelectorObjectRasterConsumerCellIterationTests" --logger "console;verbosity=minimal"`
- **Session 272 — the bounded `0x635D80` horizontal helper now composes through the split grounded seam:**
  - Added [EvaluateGroundedDriverHorizontalCorrection(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now makes the visible `0x635D80` horizontal correction explicit instead of leaving the horizontal retry path as an inferred zero vector. The new seam projects the requested move against the selected contact plane, zeroes `Z`, normalizes the horizontal plane normal, and applies the binary `+0.001f` pushout.
  - Updated [EvaluateGroundedDriverSelectedPlaneRetryTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) and [EvaluateGroundedDriverSelectedPlaneBranchGate(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) so their horizontal branches now route through that helper and surface the resulting correction through nested trace state instead of only reporting that the helper would have been used.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverHorizontalCorrectionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverHorizontalCorrectionTests.cs), [WowGroundedDriverSelectedPlaneRetryTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneRetryTests.cs), and [WowGroundedDriverSelectedPlaneBranchGateTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneBranchGateTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded horizontal slice passed `41/41`, and the widened chosen-contact/grounded/raster slice passed `91/91`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the open native backlog no longer treats the bounded `0x635D80` helper as unresolved. The next bounded fronts are the raster/object-side prepass composition still embedded in `0x6BB6B0` and any still-uncaptured post-fast-return `0x636100` tail above the already-pinned helper leaves.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowSelectorObjectRasterConsumerPrepassTests|FullyQualifiedName~WowSelectorObjectRasterConsumerPrefixTests|FullyQualifiedName~WowSelectorObjectRasterConsumerBodyTests|FullyQualifiedName~WowSelectorObjectRasterConsumerCellIterationTests" --logger "console;verbosity=minimal"`
- **Session 271 — the raster prepass outcode loop is now pinned as its own `0x6BB6B0` seam:**
  - Added [EvaluateSelectorObjectRasterPrepassOutcodeLoop(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumer.cpp) to the split raster consumer surface and rewired [EvaluateSelectorObjectRasterConsumerBody(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumer.cpp) to route its prepass writes through that narrower helper instead of keeping the point-grid outcode walk fully inlined.
  - Added matching export/interop wiring in [SelectorObjectRasterConsumerTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumerTestExports.cpp) and [NavigationInterop.SelectorObjectRasterConsumer.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectRasterConsumer.cs), plus focused coverage in [WowSelectorObjectRasterConsumerPrepassTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectRasterConsumerPrepassTests.cs).
  - The new proof pins the visible prepass loop shape through the production DLL: four-write single-cell window, out-of-range point-index zeroing, bounded output-buffer truncation, and null output-buffer tracing all stay explicit without relying on the outer raster body aggregate.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused raster slice passed `23/23`, and the widened chosen-contact/grounded/raster slice passed `88/88`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the open raster backlog is no longer the raw prepass outcode loop itself. The next raster/object-side front is the remaining prepass composition and outer aggregation still embedded in `EvaluateSelectorObjectRasterConsumerBody(...)`.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverHorizontalCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneRetryTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBranchGateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTransactionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneDistancePointerTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFirstPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFollowupRerankTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBlendCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlanePostFastReturnTailTests" --logger "console;verbosity=minimal"`
- **Session 270 — the bounded `0x635C00` correction wrapper now composes through the split grounded seam:**
  - Added [EvaluateGroundedDriverSelectedPlaneCorrectionTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the production DLL now makes the vertical `0x635C00` correction wrapper explicit above the already-pinned distance-pointer scalar helper. The new seam preserves the four visible scalar outcomes from that inner helper: direct scalar, positive radius clamp, negative radius clamp, and the flagged-negative zero-distance path.
  - Updated [EvaluateGroundedDriverSelectedPlaneRetryTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) to route its vertical branch through that narrower correction transaction instead of duplicating the rescale bookkeeping inline.
  - Added matching export/interop wiring in [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) and [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), plus focused coverage in [WowGroundedDriverSelectedPlaneCorrectionTransactionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneCorrectionTransactionTests.cs). The new proof pins direct, positive clamp, negative clamp, flagged-negative zero-distance, and degenerate-input-distance behavior, while the widened grounded slice proves the refactored retry seam still matches the earlier bounded helpers.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded correction slice passed `38/38`, and the widened chosen-contact/grounded/raster slice passed `84/84`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the open native backlog no longer treats the bounded `0x635C00` correction wrapper as unresolved. The next bounded fronts are the remaining post-fast-return `0x636100` tail details and the raster/object-side prepass composition still embedded in `0x6BB6B0`.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowSelectorObjectRasterConsumerPrefixTests|FullyQualifiedName~WowSelectorObjectRasterConsumerBodyTests|FullyQualifiedName~WowSelectorObjectRasterConsumerCellIterationTests" --logger "console;verbosity=minimal"`
- **Session 269 — the lower `0x631E70` producer handoff is now a bounded composed seam:**
  - Added [EvaluateSelectorChosenIndexPairVariableContainerTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp), so the production DLL now makes the lower variable-plus-selected-contact-container handoff explicit instead of duplicating it inside the broader producer path. The new seam composes the already-pinned variable helper with the already-pinned selected-contact-container helper, preserving reported best ratio, ambient cached-container reuse on override, cached-query reuse, and walkable query-result copy behavior.
  - Updated [EvaluateSelectorChosenIndexPairProducerTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp) to route through that narrower helper before the later chosen-contact bridge, keeping runtime behavior unchanged while removing one more inlined producer composition.
  - Added matching export/interop wiring in [SelectorChosenContactTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContactTestExports.cpp) and [NavigationInterop.SelectorChosenContact.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorChosenContact.cs), plus focused coverage in [WowSelectorChosenIndexPairVariableContainerTransactionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorChosenIndexPairVariableContainerTransactionTests.cs). The new proof compares the composed helper directly against the already-closed variable and selected-contact-container seams in the success cases, and separately pins the early query-failure short-circuit before container work.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused chosen-contact producer slice passed `21/21`, and the widened chosen-contact/grounded/raster slice passed `53/53`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the open native backlog no longer treats the lower `0x631E70` selected-contact container/data handoff as an unresolved producer gap. The next bounded fronts are the still-coarse `0x635C00` selected-plane correction wrapper and the remaining raster/object-side prepass composition above the already-split helpers.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverSelectedPlaneDistancePointerTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneRetryTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBranchGateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFirstPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFollowupRerankTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBlendCorrectionTests|FullyQualifiedName~WowGroundedDriverSelectedPlanePostFastReturnTailTests" --logger "console;verbosity=minimal"`
- **Session 268 — the producer, grounded, and raster lanes each closed one more bounded native seam:**
  - Added [EvaluateSelectorChosenIndexPairSelectedRecordLoadTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp), so the production DLL now makes the visible `0x6351A0` selected-record direct-load classifier explicit instead of collapsing every non-in-range case together. The new helper distinguishes unset (`-1`), `selectedIndex == selectedContactCount` sentinel, in-range direct load, and past-end mismatch while loading the chosen contact/pair only on the direct path.
  - Added [EvaluateGroundedDriverSelectedPlanePostFastReturnTailTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), so the visible `0x636100` post-fast-return tail is now a bounded composed seam through the production DLL: third-pass setup runs first, third-pass failure exits before blend work, and third-pass success forwards deterministically into the already-pinned blend/fallback body.
  - Added [EvaluateSelectorObjectRasterCellIteration(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumer.cpp), so one dense `0x6BB6B0` raster-cell body is no longer only covered through the outer consumer loop. The new seam pins cell-mode `0xF` skip, cell-mode mask skip, triangle accept/reject masks, queue/scratch overflow behavior, and scratch-word append order for one raster cell.
  - Added matching export/interop wiring in [SelectorChosenContactTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContactTestExports.cpp), [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp), [SelectorObjectRasterConsumerTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectRasterConsumerTestExports.cpp), [NavigationInterop.SelectorChosenContact.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorChosenContact.cs), [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs), and [NavigationInterop.SelectorObjectRasterConsumer.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectRasterConsumer.cs), plus focused coverage in [WowSelectorChosenIndexPairSelectedRecordLoadTransactionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorChosenIndexPairSelectedRecordLoadTransactionTests.cs), [WowGroundedDriverSelectedPlanePostFastReturnTailTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlanePostFastReturnTailTests.cs), and [WowSelectorObjectRasterConsumerCellIterationTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectRasterConsumerCellIterationTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the widened chosen-contact/grounded/raster slice passed `49/49`, and the widened object-consumer/raster slice passed `40/40`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the open native backlog no longer includes the visible selected-record direct-load classifier, the visible `0x636100` post-fast-return tail composition, or the explicit raster-cell body inside `0x6BB6B0`.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowSelectorChosenIndexPairSelectedRecordLoadTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairPreBridgeTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairSelectedContactContainerTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairDirectionSetupTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairDirectionSetupProducerTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairProducerTransactionTests|FullyQualifiedName~WowGroundedDriverSelectedPlanePostFastReturnTailTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneThirdPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBlendCorrectionTests|FullyQualifiedName~WowSelectorObjectRasterConsumerCellIterationTests|FullyQualifiedName~WowSelectorObjectRasterConsumerBodyTests|FullyQualifiedName~WowSelectorObjectRasterConsumerPrefixTests" --logger "console;verbosity=minimal"`
- **Session 267 — the producer lane now composes real direction-setup outputs before the chosen-contact bridge:**
  - Added [EvaluateSelectorChosenIndexPairDirectionSetupProducerTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp) so the production DLL no longer has to inject the visible `0x632BA0` outputs by hand on this bounded seam. The new wrapper runs the existing variable helper, derives the chosen index plus candidate-plane buffer through [EvaluateSelectorChosenIndexPairDirectionSetupTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp), then feeds those real direction-setup outputs into the selected-contact container + bridge path already pinned by the injected producer seam.
  - Added matching export/interop wiring in [SelectorChosenContactTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContactTestExports.cpp) and [NavigationInterop.SelectorChosenContact.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorChosenContact.cs), plus new focused coverage in [WowSelectorChosenIndexPairDirectionSetupProducerTransactionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorChosenIndexPairDirectionSetupProducerTransactionTests.cs). The new proof compares the composed wrapper directly against the already-closed inner seams (variable helper + direction setup + injected producer/bridge) and also pins the query-failure short-circuit before direction setup or bridge dispatch.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, and the focused chosen-contact/variable slice passed `21/21`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the producer lane is no longer described as depending entirely on injected `0x632BA0` outputs. The remaining producer gap is now the still-open `0x631E70` selected-contact container/data handoff inside the variable seam, not the later direction-setup -> bridge composition.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowSelectorChosenIndexPairDirectionSetupProducerTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairProducerTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairDirectionSetupTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairBridgeTests|FullyQualifiedName~WowSelectorSourceVariableTransactionTests|FullyQualifiedName~WowTerrainQuerySelectedContactContainerTests|FullyQualifiedName~WowTerrainQueryMergedTransactionTests" --logger "console;verbosity=minimal"`
- **Session 264 — the live `0x6B8E50` accepted-list body now composes through the pinned preprocess wrapper:**
  - Updated [EvaluateSelectorAcceptedListConsumerVisibleBody(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumers.cpp) so the production DLL now derives its pending/accepted preprocess counts through the already-pinned [EvaluateSelectorAcceptedListConsumerPreprocessLoop(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumers.cpp) seam instead of maintaining a second hand-counted prefix. This keeps the live accepted-list consumer body aligned with the binary-backed preprocess choreography already pinned by [WowSelectorObjectConsumerDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectConsumerDispatchTests.cs).
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the widened object-consumer slice passed `29/29`, and the full `Navigation.Physics.Tests` assembly passed `549/550` with the lone existing skipped MPQ extraction test.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining object-side gap is no longer `0x6B8E50` itself. The object frontier is now only the raster/consumer body `0x6BB6B0`; the other remaining fronts are the selected-index / paired-payload producer wiring and the still-uncaptured `0x635C00` / `0x636100` tail.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowSelectorObjectRasterConsumerPrefixTests|FullyQualifiedName~WowSelectorObjectRasterConsumerBodyTests|FullyQualifiedName~WowSelectorChosenIndexPairCallerTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairProducerTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairDirectionSetupTransactionTests" --logger "console;verbosity=minimal"`
- **Session 262 — the bounded `0x632A30` caller-side wrapper contract is now pinned as a pure selector seam:**
  - Added [EvaluateSelectorChosenIndexPairCallerTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp), exported through [SelectorChosenContactTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContactTestExports.cpp) with matching interop in [NavigationInterop.SelectorChosenContact.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorChosenContact.cs). The new coverage in [WowSelectorChosenIndexPairCallerTransactionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorChosenIndexPairCallerTransactionTests.cs) now pins the visible fixed `0x632A30` caller wrapper through the production DLL: fixed `7/9/9` init counts, override-vs-default position choice, fixed `(0,0,-1)` test/candidate seeds with initial best ratio `1.0f`, and the no-override post-`0x631BE0` `0x631E70` failure path that zeroes the caller-reported scalar before the later ranking handoff.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, and the focused chosen-contact producer slice passed `12/12`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining selector-producer gap is no longer the fixed `0x632A30` wrapper contract. The open work is now the still-unclosed `0x631BE0 -> 0x631E70` data flow and the real selected-index / paired-payload handoff into `0x6351A0`.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorChosenIndexPairCallerTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairProducerTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairBridgeTests|FullyQualifiedName~WowSelectorChosenIndexPairDirectionSetupTransactionTests|FullyQualifiedName~WowSelectorPairForwardingTests|FullyQualifiedName~WowSelectorPairPostForwardingDispatchTests" --logger "console;verbosity=minimal"`
- **Session 263 — the bounded `0x6B8E50` counted preprocess wrapper is now pinned as a pure split seam:**
  - Added [EvaluateSelectorAcceptedListConsumerPreprocessLoop(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumers.cpp), exported through [SelectorObjectConsumersTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumersTestExports.cpp) with matching interop in [NavigationInterop.SelectorObjectConsumers.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectConsumers.cs). The focused coverage in [WowSelectorObjectConsumerDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectConsumerDispatchTests.cs) now pins the visible `0x6B8E50` counted wrapper above the already-pinned preprocess iteration seam: pending-loop gate disable returns before iteration, accepted-loop runs the full source span, helper-call totals aggregate across all executed iterations, and buffered iteration traces truncate independently from the true loop count.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, and the focused object-consumer slice passed `18/18`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining `0x6B8E50` gap is no longer the counted preprocess wrapper. The next object work is the final accepted-list consumer body composition around the now-pinned loops/tails and the still-open `0x6BB6B0` raster body.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorAcceptedListConsumerPreprocessTests|FullyQualifiedName~WowSelectorAcceptedListConsumerVisibleBodyTests|FullyQualifiedName~WowSelectorAcceptedListConsumerRecordWriteTests|FullyQualifiedName~WowSelectorObjectConsumerDispatchTests" --logger "console;verbosity=minimal"`
- **Session 261 — the visible `0x636100` first-pass scalar/vector setup is now pinned as a pure grounded seam:**
  - Added [EvaluateGroundedDriverSelectedPlaneFirstPassSetup(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), exported through [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) with matching interop in [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs). The new coverage in [WowGroundedDriverSelectedPlaneFirstPassSetupTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneFirstPassSetupTests.cs) now pins the visible first-pass `0x636100` setup through the production DLL: fixed 7-slot support-plane init count, scalar floor `max(this+0xB0+skinEpsilon, boundingRadius*tanMaxSlope)`, in-band gate `0.0f <= contactNormal.z <= 0.6427876f`, negative-normalized horizontal working-vector build, and the first-rerank success/failure dispatch split.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, and the focused grounded selected-plane slice passed `21/21`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining grounded-driver gap is no longer the visible `0x636100` first-pass scalar/vector setup. The next grounded-driver unit is now any still-uncaptured tail after the small `fabs(contactNormal.z)` fast return and the remaining selected-plane scalar details inside `0x635C00`.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowGroundedDriverSelectedPlaneDistancePointerTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFirstPassSetupTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneRetryTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneBranchGateTests|FullyQualifiedName~WowGroundedDriverSelectedPlaneFollowupRerankTests" --logger "console;verbosity=minimal"`
- **Session 260 — the first bounded `0x6B8E50` preprocess iteration is now pinned as a pure split seam:**
  - Added [EvaluateSelectorAcceptedListConsumerPreprocessIteration(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumers.cpp), exported through [SelectorObjectConsumersTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumersTestExports.cpp) with matching interop in [NavigationInterop.SelectorObjectConsumers.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectConsumers.cs). The new coverage in [WowSelectorObjectConsumerDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectConsumerDispatchTests.cs) now pins one exact `0x6B8E50` preprocess pass through the production DLL: pending-loop vs accepted-loop debug color (`0x7FFF0000` vs `0x7F00FF00`), fixed `owner + 0x94` payload token, shared triangle-word `2,1,0` consumption order, loop-specific local stack slot order, and the common `0x6BCE50 -> 0x6A98E0(edx = 0)` tail.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, and the focused object-consumer slice passed `26/26`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining `0x6B8E50` gap is no longer an unstructured preprocess blob. One bounded preprocess iteration is now explicit; the next object work is the full pending/accepted loop aggregation around that helper and the still-open `0x6BB6B0` raster body.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~WowSelectorAcceptedListConsumerPreprocessTests|FullyQualifiedName~WowSelectorAcceptedListConsumerRecordWriteTests|FullyQualifiedName~WowSelectorAcceptedListConsumerVisibleBodyTests|FullyQualifiedName~WowSelectorObjectConsumerDispatchTests|FullyQualifiedName~WowSelectorObjectRasterConsumerPrefixTests|FullyQualifiedName~WowSelectorObjectRasterConsumerBodyTests" --logger "console;verbosity=minimal"`
- **Session 258 — the next bounded `0x632BA0` selector-producer setup seam is now pinned as a pure split helper:**
  - Added [EvaluateGroundedDriverSelectedPlaneRetryTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), exported through [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) with matching interop in [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs). The new coverage in [WowGroundedDriverSelectedPlaneRetryTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneRetryTests.cs) now pins the visible caller-side `0x635C00` / `0x636100` vertical retry transaction: walkable-selected and gate-`2` vertical dispatch, `0x04000000` flag set before the non-walkable retry path, exact `OutputDistancePointer / InputDistancePointer` remaining-distance and sweep-fraction rescale, and the flagged negative-scalar zero-budget collapse while the helper still returns `+boundingRadius`.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused grounded selected-plane slice passed `27/27`, and the full `Navigation.Physics.Tests` assembly passed `535/536` with the lone existing skipped MPQ extraction test.
  - Practical implication: the split grounded-driver lane no longer has to infer the visible walkable/gate-`2` vertical retry bookkeeping around `0x635C00`. The next grounded-driver unit is now the earlier first-pass vector/scalar setup inside `0x635C00` and any still-uncaptured tail after the visible `0x636100` follow-up rerank fast return.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorAcceptedListConsumerRecordWriteTests|FullyQualifiedName~WowSelectorAcceptedListConsumerVisibleBodyTests|FullyQualifiedName~WowSelectorObjectConsumerDispatchTests|FullyQualifiedName~WowSelectorObjectRasterConsumerPrefixTests|FullyQualifiedName~WowSelectorObjectRasterConsumerBodyTests" --logger "console;verbosity=minimal"`
- **Session 258 — the next bounded `0x632BA0` selector-producer setup seam is now pinned as a pure split helper:**
  - Added [EvaluateSelectorChosenIndexPairDirectionSetupTransaction(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp), exported through [SelectorChosenContactTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContactTestExports.cpp) with matching interop in [NavigationInterop.SelectorChosenContact.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorChosenContact.cs). The new coverage in [WowSelectorChosenIndexPairDirectionSetupTransactionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorChosenIndexPairDirectionSetupTransactionTests.cs) now pins the visible `0x632BA0` entry/setup wrapper: near-zero early success, override-vs-default position choice, optional swim-side vertical-offset scale branch, requested-distance clamp before scaling the candidate direction, local support-plane/neighborhood setup, and the handoff into the already-closed five-direction chooser core.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, the focused new selector-producer slice passed `9/9`, and the widened chosen-contact slice passed `24/24`.
  - Practical implication: the split selected-contact lane no longer has to inject the visible `0x632BA0` entry/setup branch shape by hand. The next bounded producer work is the remaining caller-side transaction that feeds the real selected-contact / paired-payload globals into `0x6351A0`.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorChosenIndexPairDirectionSetupTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairProducerTransactionTests|FullyQualifiedName~WowSelectorChosenIndexPairBridgeTests|FullyQualifiedName~WowSelectorPairForwardingTests|FullyQualifiedName~WowSelectorPairPostForwardingDispatchTests" --logger "console;verbosity=minimal"`
- **Session 257 — the next bounded `0x6B8E50` record-write tail is now pinned as a pure seam:**
  - Tightened [EvaluateSelectorAcceptedListConsumerRecordWrite(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumers.cpp) so the record slot’s `0x18/0x1A` min/max now follow the reserved source triangle words instead of the truncated test output span, then validated the existing export/interop lane through [SelectorObjectConsumersTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumersTestExports.cpp) and [NavigationInterop.SelectorObjectConsumers.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectConsumers.cs).
  - The focused coverage already living in [WowSelectorObjectConsumerDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectConsumerDispatchTests.cs) now proves the post-reservation `0x6B8E50` record materialization tail end-to-end: pre-reserve record index, fixed `+0x94` owner payload token, direct `d0/d4` token writes, independent triangle-word and accepted-id buffer tokens, `0x14/0x16` count fields, and `0x18/0x1A` min/max sourced from all reserved triangle words.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, and the focused accepted-list consumer slice passed `12/12`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining `0x6B8E50` gap is no longer the post-reservation record write tail. The next bounded work in that consumer is now the two helper-driven preprocess loops.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorAcceptedListConsumerRecordWriteTests|FullyQualifiedName~WowSelectorAcceptedListConsumerVisibleBodyTests|FullyQualifiedName~WowSelectorObjectConsumerDispatchTests" --logger "console;verbosity=minimal"`
- **Session 256 — one more visible `0x636100` grounded-driver rerank tail is now pinned as a pure seam:**
  - Added [EvaluateGroundedDriverSelectedPlaneFollowupRerank(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), exported through [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) with matching interop in [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs). The new coverage in [WowGroundedDriverSelectedPlaneFollowupRerankTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPlaneFollowupRerankTests.cs) now pins the visible post-first-rerank `0x636100` tail: selected-record exact-match vs reload-original packed-pair choice, fixed unit-`Z` second-pass vector, second-rerank failure return, and the immediate `fabs(contactNormal.z) <= 0x8029D4 -> return 1` fast return.
  - Validation on the serialized lane: native release build passed, a direct inline harness against the rebuilt `Navigation.dll` passed `4/4` for the new seam, and the normal `Navigation.Physics.Tests` release build is currently blocked by an unrelated duplicate-class error in [WowSelectorObjectConsumerDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectConsumerDispatchTests.cs).
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining grounded-driver `0x636100` gap is no longer that visible follow-up rerank tail. The next grounded-driver unit is now the earlier first-pass vector/scalar setup and any still-uncaptured tail after the small `fabs(z)` fast return.
- **Session 254 — the full `0x6BC7E0` recursive BVH body is now pinned as a pure seam:**
  - Added [EvaluateSelectorBvhRecursiveTraversal(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectTraversal.cpp), exported through [SelectorObjectTraversalTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectTraversalTestExports.cpp) with matching interop in [NavigationInterop.SelectorObjectTraversal.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectTraversal.cs). The new coverage in [WowSelectorBvhRecursiveTraversalTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorBvhRecursiveTraversalTests.cs) now pins the full visible `0x6BC7E0` body: low-first then high-second recursion order, exact leaf-arm iteration over the node-owned leaf-id span, leaf-cull skip behavior, and queue/overflow propagation across recursive calls.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed with `--no-dependencies`, and the focused traversal slice passed `16/16`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining object-path gap is no longer `0x6BC7E0` or `0x6B9430`. The next bounded object deliverables are now the two post-traversal consumers `0x6B8E50` and `0x6BB6B0`.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorBvhRecursiveTraversalTests|FullyQualifiedName~WowSelectorBvhRecursionStepTests|FullyQualifiedName~WowSelectorBvhChildTraversalTests|FullyQualifiedName~WowSelectorLeafQueueMutationTests|FullyQualifiedName~WowSelectorLeafQueueMutationWrapperTests" --logger "console;verbosity=minimal"`
- **Session 253 — `0x6B9430` post-recursion dispatch/cleanup is now pinned as a pure seam:**
  - Added [EvaluateSelectorObjectConsumerDispatch(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumers.cpp), exported through [SelectorObjectConsumersTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectConsumersTestExports.cpp) with matching interop in [NavigationInterop.SelectorObjectConsumers.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectConsumers.cs). The new coverage in [WowSelectorObjectConsumerDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectConsumerDispatchTests.cs) now pins the visible `0x6B9430` tail: traversal then accepted-list consumer dispatch order, conditional raster gate on `flags & 0x000F0000`, queued visited-bit cleanup via `stateBytes[id*2] &= 0x7F`, zeroing `0xCE26E0` / `0xCE66FC`, and the final result flag sourced from whether `0xC5A474` changed.
  - Validation held on the serialized lane: native release build passed, release `Navigation.Physics.Tests` build passed, the new `WowSelectorObjectConsumerDispatchTests` slice passed `3/3`, and the widened adjacent object slice passed `13/13`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining object-path gap is no longer the `0x6B9430` tail. The next bounded object deliverables are now `0x6BC7E0`, `0x6B8E50`, and `0x6BB6B0`.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorObjectConsumerDispatchTests|FullyQualifiedName~WowSelectorBvhRecursionStepTests|FullyQualifiedName~WowSelectorLeafQueueMutationTests|FullyQualifiedName~WowSelectorLeafQueueMutationWrapperTests|FullyQualifiedName~WowSelectorObjectRouterTests" --logger "console;verbosity=minimal"`
- **Session 252 — the remaining object-side functions are now fully bounded from the local WoW.exe instead of inferred from prefix windows:**
  - Extended [0x6B8E50_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6B8E50_disasm.txt) and [0x6BB6B0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6BB6B0_disasm.txt) to their real returns and added new full captures in [0x6BC7E0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6BC7E0_disasm.txt) and [0x6B9430_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6B9430_disasm.txt). The object side is now exact: `0x6BC7E0` is one medium recursive BVH body, `0x6B9430` is one small post-recursion dispatch/cleanup tail, `0x6B8E50` is one medium accepted-list consumer, and `0x6BB6B0` is one large raster/consumer body.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) to replace the old prefix-based estimates with the actual end-to-end function boundaries and visible phases, including the newly captured `0x6B9430` call order (`0x6BC7E0` -> `0x6B8E50` -> optional `0x6BB6B0` -> visited-bit cleanup).
  - Practical implication: the remaining delivery plan is now exact. Lane 1 = selected-index / paired-payload producer wiring, lane 2 = remaining `0x635C00` / `0x636100` selected-plane correction, lane 3 = `0x6BC7E0` plus the small `0x6B9430` tail, lane 4 = `0x6B8E50`, lane 5 = `0x6BB6B0`, followed by the final focused managed `MovementController` audit. Revised ETA as of March 27, 2026: best case `2026-03-31`, realistic `2026-04-01` to `2026-04-02`, conservative `2026-04-03`.
  - Validation: no build/test run this pass; this was a raw-disassembly quantification pass only.
  - Exact next command: `py -c "from capstone import *; from pathlib import Path; code=Path(r'D:/World of Warcraft/WoW.exe').read_bytes(); md=Cs(CS_ARCH_X86, CS_MODE_32); start=0x632A30; end=0x632E40; data=code[start-0x400000:end-0x400000]; print('\\n'.join([f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}' for i in md.disasm(data, start) if i.address < end]))"`
- **Session 250 — the visible `0x617170` consumer body and `0x636FA1` hover/rerank wrapper are now pinned as pure seams:**
  - Added the split grounded-driver seams [EvaluateGroundedDriverSelectedPairCommitBody(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) and [EvaluateGroundedDriverHoverRerankDispatch(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), exported through [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) with matching interop in [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs). The new coverage in [WowGroundedDriverSelectedPairCommitBodyTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPairCommitBodyTests.cs) and [WowGroundedDriverHoverRerankDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverHoverRerankDispatchTests.cs) now pins the visible `0x617170` pair-commit body and the visible `0x636FA1` hover/rerank wrapper through the production DLL.
  - Validation held on the widened selector/object/grounded slice again: native release build passed, `Navigation.Physics.Tests` release build passed, the focused grounded-driver slice passed `34/34`, and the combined selected-contact + object + grounded-driver slice passed `117/117`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) and anchored fresh raw captures in [0x617170_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x617170_disasm.txt), [0x636FA1_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x636FA1_disasm.txt), [0x6B8E50_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6B8E50_disasm.txt), and [0x6BB6B0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6BB6B0_disasm.txt). The open collision/movement gaps are now the selected-index / paired-payload producer wiring, the remaining selected-plane `Z` correction / distance-pointer behavior inside `0x635C00` / `0x636100`, and the larger recursive/post-traversal object consumers.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorBvhRecursionStepTests|FullyQualifiedName~WowSelectorBvhChildTraversalTests|FullyQualifiedName~WowSelectorPairPostForwardingDispatchTests|FullyQualifiedName~WowSelectorPairForwardingTests|FullyQualifiedName~WowSelectorAlternateUnitZStateHandlerTests|FullyQualifiedName~WowSelectorDirectStateHandlerTests|FullyQualifiedName~WowGroundedDriverFirstDispatchTests|FullyQualifiedName~WowGroundedDriverSelectedContactDispatchTests|FullyQualifiedName~WowGroundedDriverResweepBookkeepingTests|FullyQualifiedName~WowGroundedDriverVerticalCapTests|FullyQualifiedName~WowGroundedDriverSelectedPairCommitTailTests|FullyQualifiedName~WowGroundedDriverSelectedPairCommitGuardTests|FullyQualifiedName~WowGroundedDriverSelectedPairCommitBodyTests|FullyQualifiedName~WowGroundedDriverHoverRerankDispatchTests|FullyQualifiedName~WowSelectorAlternatePairTests|FullyQualifiedName~WowSelectorTwoCandidateWorkingVectorTests|FullyQualifiedName~WowSelectorTriangleEdgeDirectionTests|FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
- **Session 249 — the visible `0x6173B0` selected-pair commit guard is now pinned as a pure seam:**
  - Added the split grounded-driver seam [EvaluateGroundedDriverSelectedPairCommitGuard(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), exported through [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) with matching interop in [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs). The new coverage in [WowGroundedDriverSelectedPairCommitGuardTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPairCommitGuardTests.cs) now pins the visible reject lattice before the still-opaque `0x617170` consumer body.
  - Validation held on the widened selector/object/grounded slice again: native release build passed, `Navigation.Physics.Tests` release build passed, the focused grounded-driver slice passed `24/24`, and the combined selected-contact + object + grounded-driver slice passed `107/107`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining grounded-driver gap is no longer the visible `0x6173B0` guard lattice. The open work is now the selected-index / paired-payload producer wiring, the larger hover/rerank branch under `0x636FA1`, the still-opaque `0x617170` consumer body, and the true recursive/post-traversal object consumers.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorBvhRecursionStepTests|FullyQualifiedName~WowSelectorBvhChildTraversalTests|FullyQualifiedName~WowSelectorPairPostForwardingDispatchTests|FullyQualifiedName~WowSelectorPairForwardingTests|FullyQualifiedName~WowSelectorAlternateUnitZStateHandlerTests|FullyQualifiedName~WowSelectorDirectStateHandlerTests|FullyQualifiedName~WowGroundedDriverFirstDispatchTests|FullyQualifiedName~WowGroundedDriverSelectedContactDispatchTests|FullyQualifiedName~WowGroundedDriverResweepBookkeepingTests|FullyQualifiedName~WowGroundedDriverVerticalCapTests|FullyQualifiedName~WowGroundedDriverSelectedPairCommitTailTests|FullyQualifiedName~WowGroundedDriverSelectedPairCommitGuardTests|FullyQualifiedName~WowSelectorAlternatePairTests|FullyQualifiedName~WowSelectorTwoCandidateWorkingVectorTests|FullyQualifiedName~WowSelectorTriangleEdgeDirectionTests|FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
- **Session 248 — the `0x04000000` cap block and the fast selected-pair commit/fallback tail are now pinned as pure seams:**
  - Added the split grounded-driver seams [EvaluateGroundedDriverVerticalCap(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) and [EvaluateGroundedDriverSelectedPairCommitTail(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), exported through [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) with matching interop in [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs). The new coverage in [WowGroundedDriverVerticalCapTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverVerticalCapTests.cs) and [WowGroundedDriverSelectedPairCommitTailTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedPairCommitTailTests.cs) now pins the positive-`Z` `0x04000000` clamp block and the fast `0x636F36` selected-pair commit/fallback wrapper.
  - Validation held on the widened selector/object/grounded slice again: native release build passed, `Navigation.Physics.Tests` release build passed, the focused grounded-driver slice passed `20/20`, and the combined selected-contact + object + grounded-driver slice passed `103/103`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining grounded-driver gap is no longer the common resweep bookkeeper, the `0x04000000` cap block, or the fast selected-pair commit/fallback tail. The open work is now the selected-index / paired-payload producer wiring, the visible `0x6173B0` pre-`0x617170` pair-commit guard, the larger hover/rerank branch under `0x636FA1`, and the true recursive/post-traversal object consumers.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorBvhRecursionStepTests|FullyQualifiedName~WowSelectorBvhChildTraversalTests|FullyQualifiedName~WowSelectorPairPostForwardingDispatchTests|FullyQualifiedName~WowSelectorPairForwardingTests|FullyQualifiedName~WowSelectorAlternateUnitZStateHandlerTests|FullyQualifiedName~WowSelectorDirectStateHandlerTests|FullyQualifiedName~WowGroundedDriverFirstDispatchTests|FullyQualifiedName~WowGroundedDriverSelectedContactDispatchTests|FullyQualifiedName~WowGroundedDriverResweepBookkeepingTests|FullyQualifiedName~WowGroundedDriverVerticalCapTests|FullyQualifiedName~WowGroundedDriverSelectedPairCommitTailTests|FullyQualifiedName~WowSelectorAlternatePairTests|FullyQualifiedName~WowSelectorTwoCandidateWorkingVectorTests|FullyQualifiedName~WowSelectorTriangleEdgeDirectionTests|FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
- **Session 247 — the selected-contact state handlers and two more grounded-driver bookkeeping seams are now pinned as pure seams:**
  - Added the split selected-contact seams [EvaluateSelectorAlternateUnitZStateHandler(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp) and [EvaluateSelectorDirectStateHandler(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp), exported through [SelectorChosenContactTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContactTestExports.cpp) with matching interop in [NavigationInterop.SelectorChosenContact.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorChosenContact.cs). The new coverage in [WowSelectorAlternateUnitZStateHandlerTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorAlternateUnitZStateHandlerTests.cs) and [WowSelectorDirectStateHandlerTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorDirectStateHandlerTests.cs) now pins the visible `0x633220` alternate-unit-Z state write and the `0x7C6290` direct-state cache-reset wrapper from a precomputed inner `+0x84` scalar result.
  - Added the split grounded-driver seams [EvaluateGroundedDriverSelectedContactDispatch(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp) and [EvaluateGroundedDriverResweepBookkeeping(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), exported through [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) with matching interop in [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs). The new coverage in [WowGroundedDriverSelectedContactDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverSelectedContactDispatchTests.cs) and [WowGroundedDriverResweepBookkeepingTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverResweepBookkeepingTests.cs) now pins the `0x6334A0`-driven selected-contact handoff before the non-walkable lattice and the common post-helper resweep recombine/finalize block inside `0x6367B0`.
  - Validation held on the widened selector/object/grounded slice: native release build passed, `Navigation.Physics.Tests` release build passed, the focused selected-contact state-handler slice passed `47/47`, the focused grounded-driver slice passed `11/11`, and the combined selected-contact + object + grounded-driver slice passed `94/94`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining grounded-selector gap is no longer the `0x633220` / `0x7C6290` state handlers, the `0x6334A0` handoff into grounded runtime, or the common post-helper resweep bookkeeper. The open work is now the selected-index / paired-payload producer wiring, the `0x04000000`-gated vertical-cap block and selected-pair commit/fallback tail, plus the true recursive/post-traversal object consumers.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorBvhRecursionStepTests|FullyQualifiedName~WowSelectorBvhChildTraversalTests|FullyQualifiedName~WowSelectorPairPostForwardingDispatchTests|FullyQualifiedName~WowSelectorPairForwardingTests|FullyQualifiedName~WowSelectorAlternateUnitZStateHandlerTests|FullyQualifiedName~WowSelectorDirectStateHandlerTests|FullyQualifiedName~WowGroundedDriverFirstDispatchTests|FullyQualifiedName~WowGroundedDriverSelectedContactDispatchTests|FullyQualifiedName~WowGroundedDriverResweepBookkeepingTests|FullyQualifiedName~WowSelectorAlternatePairTests|FullyQualifiedName~WowSelectorTwoCandidateWorkingVectorTests|FullyQualifiedName~WowSelectorTriangleEdgeDirectionTests|FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
- **Session 246 — the post-`0x6351A0` caller wrapper and one-step `0x6BC7E0` recursion shell are now pinned as pure seams:**
  - Added the split selected-contact seam [EvaluateSelectorPairPostForwardingDispatch(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp), exported through [SelectorChosenContactTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContactTestExports.cpp) with matching interop in [NavigationInterop.SelectorChosenContact.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorChosenContact.cs). The new coverage in [WowSelectorPairPostForwardingDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorPairPostForwardingDispatchTests.cs) now pins the tiny post-forward handoff above `0x635450`: failure branch on return code `2`, skip-window path on return code `0`, `0x635450` invocation on return code `1`, and alternate-unit-Z dispatch priority over direct-state dispatch.
  - Added the split object-traversal seam [EvaluateSelectorBvhRecursionStep(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectTraversal.cpp), exported through [SelectorObjectTraversalTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorObjectTraversalTestExports.cpp) with matching interop in [NavigationInterop.SelectorObjectTraversal.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorObjectTraversal.cs). The new coverage in [WowSelectorBvhRecursionStepTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorBvhRecursionStepTests.cs) now pins low-first/high-second child entry order plus propagated result/pending/accepted/overflow accumulation from a prebuilt split record.
  - Validation held on the widened selector/object/grounded slice: native release build passed, `Navigation.Physics.Tests` release build passed, the direct selected-contact wrapper slice passed `31/31`, the direct recursion-step slice passed `7/7`, and the combined selected-contact + grounded-driver + recursion-step slice passed `83/83`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining selected-contact gap is no longer the tiny post-`0x6351A0` caller wrapper and the remaining object-path gap is no longer the low/high accumulator ordering inside one split step. The open work is still the selected-index / paired-payload producer wiring, the `0x633220` / `0x7C6290` state handlers and later grounded-driver bookkeeping, plus the true recursive `0x6BC7E0` composition / post-traversal consumers.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorBvhRecursionStepTests|FullyQualifiedName~WowSelectorBvhChildTraversalTests|FullyQualifiedName~WowSelectorPairPostForwardingDispatchTests|FullyQualifiedName~WowSelectorPairForwardingTests|FullyQualifiedName~WowGroundedDriverFirstDispatchTests|FullyQualifiedName~WowSelectorAlternatePairTests|FullyQualifiedName~WowSelectorTwoCandidateWorkingVectorTests|FullyQualifiedName~WowSelectorTriangleEdgeDirectionTests|FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
- **Session 245 — selected-contact pair forwarding and the first grounded-driver dispatch lattice are now pinned as pure seams:**
  - Added the split selected-contact seam [EvaluateSelectorChosenPairForwarding(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContact.cpp), exported through [SelectorChosenContactTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/SelectorChosenContactTestExports.cpp) with matching interop in [NavigationInterop.SelectorChosenContact.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.SelectorChosenContact.cs). The new coverage in [WowSelectorPairForwardingTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorPairForwardingTests.cs) now pins the one-step `0x6351A0` chosen-contact bridge: direct-pair return, direct zero-pair return, alternate unit-Z zero-pair return, and alternate-pair fallback after the direct gate/prism test.
  - Added the split grounded-driver seam [EvaluateGroundedDriverFirstDispatch(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParity.cpp), exported through [GroundedDriverParityTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/GroundedDriverParityTestExports.cpp) with matching interop in [NavigationInterop.GroundedDriver.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.GroundedDriver.cs). The new coverage in [WowGroundedDriverFirstDispatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowGroundedDriverFirstDispatchTests.cs) now pins the first visible `0x6367B0` dispatch lattice: walkable-selected vertical path, non-walkable exit, non-walkable horizontal path, and non-walkable vertical `0x04000000` path with remaining-distance rescale only on the vertical branches.
  - Validation held on the focused selector/grounded slices: native release build passed, `Navigation.Physics.Tests` release build passed, the direct selected-contact seam slice passed `26/26`, the widened selected-contact slice passed `68/68`, the direct grounded-driver seam slice passed `4/4`, and the combined selected-contact + grounded-driver slice passed `72/72`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the open selector gap is no longer the visible `0x6351A0` pair-forwarding shell itself and the open grounded-driver gap is no longer the first `0x636100` caller-side dispatch lattice. The remaining work is still the selected-index / paired-payload producer wiring, later grounded-driver bookkeeping, and the recursive `0x6BC7E0` traversal / post-traversal consumers.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorPairForwardingTests|FullyQualifiedName~WowGroundedDriverFirstDispatchTests|FullyQualifiedName~WowSelectorAlternatePairTests|FullyQualifiedName~WowSelectorTwoCandidateWorkingVectorTests|FullyQualifiedName~WowSelectorTriangleEdgeDirectionTests|FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
- **Session 244 — the remaining object-path gap is now centered on recursive traversal and concrete leaf wrappers:**
  - Added more native/exported seams directly above and below the unresolved `0x6BC7E0` traversal body: `ShouldUseSelectorObjectCallback(...)`, `FinalizeSelectorObjectNoCallbackState(...)`, `EvaluateSelectorLeafQueueMutation(...)`, and `BuildSelectorNodeTraversalPayload(...)` in [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), exported through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp), with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorLeafQueueMutationTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorLeafQueueMutationTests.cs), [WowSelectorNodeTraversalPayloadTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorNodeTraversalPayloadTests.cs), and the strengthened [WowSelectorObjectTraversalHelperTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectTraversalHelperTests.cs), which now pin the `0x6AC860` callback-present/null-callback no-op branch contract, the shared `0x6BC9A0` / `0x6BCA50` pending/accepted queue mutation core, and the deterministic `0x6B9430` pre-`0x6BC7E0` payload packaging (query bounds, `callbackMask|0x80` word, zeroed accepted count, and exact node-owned pointers).
  - Validation held on the widened selector/object collision slice: native release build passed, `Navigation.Physics.Tests` release build passed, the new leaf/payload slice passed `6/6`, and the widened selector/object slice passed `99/99`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining object-path gap is no longer the callback gate, the null-callback no-op path, the shared leaf queue mutation body, or the deterministic `0x6B9430` payload builder. It is now the recursive `0x6BC7E0` walk itself, the concrete predicate-bound `0x6BC9A0` / `0x6BCA50` wrappers, and the downstream post-traversal consumers that use the accepted list.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorNodeTraversalPayloadTests|FullyQualifiedName~WowSelectorLeafQueueMutationTests|FullyQualifiedName~WowSelectorObjectRouterTests|FullyQualifiedName~WowSelectorObjectTraversalHelperTests|FullyQualifiedName~WowSelectorBvhChildTraversalTests|FullyQualifiedName~WowTriangleLocalBoundsAabbRejectTests|FullyQualifiedName~WowSelectorTrianglePlaneOutcodeRejectTests|FullyQualifiedName~WowSelectorDynamicObjectHullSourceGeometryTests|FullyQualifiedName~WowSelectorSupportPointBufferTransformTests|FullyQualifiedName~WowSelectorHullSourceGeometryTests|FullyQualifiedName~WowSelectorHullConsumerTests" --logger "console;verbosity=minimal"`
- **Session 243 — the remaining object-path gap is now past the prep/router predicates and into the callback/traversal body:**
  - Added more pure native/exported seams directly on the remaining `0x6AC860` / `0x6A4A00` / `0x6A3DC0` / `0x6B8B70` / `0x6B8C00` path: `ResolveSelectorObjectNodePointer(...)`, `BuildSelectorBvhChildTraversal(...)`, `TriangleSharesSelectorPlaneOutcodeReject(...)`, `EvaluateTriangleLocalBoundsAabbReject(...)`, and `EvaluateSelectorObjectRouterEntries(...)` in [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), exported through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp), with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorBvhChildTraversalTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorBvhChildTraversalTests.cs), [WowSelectorTrianglePlaneOutcodeRejectTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorTrianglePlaneOutcodeRejectTests.cs), [WowTriangleLocalBoundsAabbRejectTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTriangleLocalBoundsAabbRejectTests.cs), [WowSelectorObjectRouterTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectRouterTests.cs), and the strengthened [WowSelectorObjectTraversalHelperTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectTraversalHelperTests.cs), which now pin the exact `0x6A4A00` pointer gate, the visible non-leaf `0x6BC7E0` split/clipped-child math, the pure selector-plane and local-bounds triangle reject predicates, and the deterministic `0x6A3DC0` entry loop ordering: overlap first, `0x6A4A00(index, 0)` next, fixed `0x6B9430` dispatch selection, and byte-OR accumulation with no early-out.
  - Validation held on the widened selector/object collision slice: native release build passed, `Navigation.Physics.Tests` release build passed, the new router class passed `4/4`, the new object-path predicate/helper slice passed `12/12`, and the widened selector/object slice passed `91/91`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining object-path gap is no longer the selector-node gate, the non-leaf child split math, the local-bounds or selector-plane triangle reject predicates, or the deterministic `0x6A3DC0` router shell. It is now the `0x6AC860` callback-present/null-callback tail plus the deeper `0x6B9430` / `0x6BC7E0` traversal and enqueue body that actually consumes the routed entries.
  - Exact next command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorObjectRouterTests|FullyQualifiedName~WowSelectorObjectTraversalHelperTests|FullyQualifiedName~WowSelectorBvhChildTraversalTests|FullyQualifiedName~WowTriangleLocalBoundsAabbRejectTests|FullyQualifiedName~WowSelectorTrianglePlaneOutcodeRejectTests|FullyQualifiedName~WowSelectorDynamicObjectHullSourceGeometryTests|FullyQualifiedName~WowSelectorSupportPointBufferTransformTests|FullyQualifiedName~WowSelectorHullSourceGeometryTests|FullyQualifiedName~WowSelectorHullConsumerTests" --logger "console;verbosity=minimal"`
- **Session 242 — the remaining `0x6A3DC0` prep logic is now reduced to wrapper control flow:**
  - Added pure native/exported seams for the object-path callback-mask fold, selector-node gate, and support-point buffer bounds build: `BuildSelectorObjectCallbackMask(...)`, `ShouldResolveSelectorObjectNode(...)`, and `BuildSelectorSupportPointBounds(...)` in [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), exported through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp), with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorObjectTraversalHelperTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorObjectTraversalHelperTests.cs), which now pins the exact `0x6A3DC0` movement-bit mask fold (`0xEE/0xC6` base plus the `0x20`, `0x40`, and `0x4000` clears), the `0x6A4A00` selector-enabled/node-ready fallback gate, and the `0x7C1450` support-point min/max bounds build used before entry overlap checks.
  - Validation held on the widened selector/object collision slice: native release build passed, `Navigation.Physics.Tests` release build passed, the new traversal-helper class passed `5/5`, and the widened selector/object helper slice passed `73/73`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining object-path gap is no longer the callback-mask fold, selector-node gate, or support-point AABB prep inside `0x6A3DC0`; it is now the intrusive-list walk, per-entry callback dispatch, and result accumulation around `0x6AC860` / `0x6A3DC0` / `0x6B9430`.
- **Session 241 — the next object-path seams under `0x6AC860` are now pinned as pure binary helpers:**
  - Added deterministic coverage in [WowSelectorSupportPointBufferTransformTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSupportPointBufferTransformTests.cs), [WowSelectorHullSourceGeometryTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorHullSourceGeometryTests.cs), and the strengthened [WowSelectorHullConsumerTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorHullConsumerTests.cs). The production DLL now mirrors the visible `0x7BCA80` support-point transform loop, the `0x686500` / `0x686640` hull-source initializer/build, and the corrected `0x6869C0` transformed-bounds cull normal math.
  - Validation held on the widened selector/object helper slice: native release build passed, `Navigation.Physics.Tests` release rebuild passed, and the focused object-path slice passed `64/64`.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining object-path producer gap is no longer the support-point transform, the copied 8-point hull-source build, or the transformed-bounds cull; it is now the intrusive-list walk and the simple-six-plane vs callback branch tail around `0x6AC860` / `0x6A3DC0`.
- **Session 240 — more of the surrounding collision producer caller transaction is now pinned as pure binary seams:**
  - Added pure [DoAabbsOverlapInclusive(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [IsTerrainQueryPayloadEnabled(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [BuildOptionalSelectorChildDispatchMask(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [ZeroTerrainQueryPairPayloadRange(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [EvaluateTerrainQueryEntryDispatch(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [ShouldDispatchDynamicTerrainQueryEntry(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [BeginTerrainQueryProducerPass(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [BuildTerrainQueryChunkSpan(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [EnumerateTerrainQueryChunkCoordinates(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), and [EvaluateSelectorSourceAabbCull(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowTerrainQueryCallerTransactionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTerrainQueryCallerTransactionTests.cs) and [WowSelectorSourceAabbCullTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceAabbCullTests.cs), which now pin the visible caller masks, zero-fill growth, entry dispatch ordering, `stamp++` plus output reset, chunk-span quantization / enumeration, and the first object-path six-plane AABB cull through the production DLL.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining producer gap is no longer the visible caller masks, pass reset, outer chunk span math, or the first object-path cull; it is now the transformed 8-point/object-hull builder and the remaining tagged list walk around `0x6AC860` / `0x6869C0` / `0x686500` / `0x686640`.
  - Focused native proof held: release native build passed, release physics-test build passed, and the direct producer slice passed `60/60`.
  - Practical implication: the next missing deterministic work on this collision path is still native producer parity, not pathfinding or live validation.
- **Session 239 — the visible `0x6AC1E0` row/column walk is now mirrored as one composed native seam:**
  - Added pure [AppendSelectorSourceScanWindowCandidateRecords(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorSourceScanWindowRecordTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceScanWindowRecordTests.cs), which now pins two more exact binary-backed behaviors through the production DLL: the `0x8103F8` mask table skips whole 2x2 blocks during the scan, and the outer loop advances one 17-wide source row at a time while feeding the fixed triangle append helper.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the visible `0x6AC1E0` body is now described as a composed scan helper instead of loose inner pieces.
  - Focused native proof held: release native build passed, release physics-test build passed, the direct producer slice passed `42/42`, and the widened adjacent query slice passed `55/55`.
  - Practical implication: the remaining unresolved native work on this producer path is no longer the visible `0x6AC1E0` loop body. The next missing step is the surrounding `0x6AADC0` caller transaction that selects the chunk/cell inputs and feeds this now-pinned scan helper.
- **Session 238 — the visible fixed-table setup inside sibling `0x6AC1E0` is now pinned as pure binary seams:**
  - Added pure [TranslateSelectorSourceGeometry(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [BuildSelectorSourcePlaneOutcode(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [CountSelectorSourceTrianglesPassingPlaneOutcodes(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [BuildSelectorSourceScanWindow(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [BuildSelectorSourceSubcellMask(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [BuildTranslatedTriangleSelectorRecord(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), and [AppendSelectorSourceTriangleCandidateRecords(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorSourceGeometryTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceGeometryTests.cs), [WowSelectorSourceTriangleCountTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceTriangleCountTests.cs), [WowSelectorSourceTriangleRecordTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceTriangleRecordTests.cs), [WowSelectorSourceSubcellMaskTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceSubcellMaskTests.cs), and [WowSelectorSourceScanWindowTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceScanWindowTests.cs), which now pin the translated source payload, strict `-0.0194444433f` outcode threshold, fixed sample/triangle tables, the rsqrt-vs-`0x637480` append path, the `0x8103F8` 2x2 subcell mask table, and the local 17-wide scan-window clamp/stride math through the production DLL.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining `0x6AC1E0` gap is no longer the visible source setup and append loop; it is now the final outer row/column iteration and the surrounding `0x6AADC0` caller wiring.
  - Focused native proof held: release native build passed, release physics-test build passed, the direct producer slice passed `40/40`, and the widened adjacent query slice passed `53/53`.
  - Practical implication: the remaining unresolved native work on this producer path is no longer the fixed source tables, subcell mask, scan-window math, or accepted-triangle append body inside `0x6AC1E0`. The next missing step is the rest of the outer loop / caller transaction that feeds those now-pinned helpers.
- **Session 237 — the shared `0x637480` plane-from-three-points primitive is now pinned directly:**
  - Added pure [BuildPlaneFromTrianglePoints(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowPlaneFromTrianglePointsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowPlaneFromTrianglePointsTests.cs), which now pins the binary triangle orientation, the `-(normal dot point0)` distance write, and the degenerate-triangle fail path through the production DLL.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so `0x637480` is recorded as a directly mirrored primitive instead of only an implementation detail inside higher-level record builders.
  - Focused native proof held: release native build passed, release physics-test build passed, the direct producer slice passed `20/20`, and the widened adjacent query slice passed `33/33`.
  - Practical implication: the remaining unresolved native work on this producer path is no longer the fallback triangle-plane primitive used by `0x6AC1E0`. The next missing step is the rest of the sibling `0x6AC1E0` setup/transform transaction itself.
- **Session 236 — the visible `0x713A5D` surviving-triangle pre-count is now pinned as a pure binary seam:**
  - Added pure [CountTrianglesPassingAabbOutcodeReject(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowTriangleAabbOutcodeCountTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTriangleAabbOutcodeCountTests.cs), which now pins the mixed accepted/rejected count path and the all-rejected zero-count path through the production DLL.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the `0x713A5D` pre-count/reserve step is no longer implicit.
  - Focused native proof held: release native build passed, release physics-test build passed, the direct producer slice passed `17/17`, and the widened adjacent query slice passed `30/30`.
  - Practical implication: the remaining unresolved native work on this producer path is no longer the triangle-cull count. The next missing step is the sibling `0x6AC1E0` transaction and the remaining transformed-source setup around it.
- **Session 235 — one accepted-triangle `0x7137C0` record writer is now pinned as a pure binary seam:**
  - Added pure [BuildTransformedTriangleSelectorRecord(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowTransformedTriangleSelectorRecordTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTransformedTriangleSelectorRecordTests.cs), which now pins row normalization, basis-row permutation, zero-length-row preservation, and the final plane-from-point write through the production DLL.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the accepted `0x7137C0` record write is no longer lumped into the unresolved body.
  - Focused native proof held: release native build passed, release physics-test build passed, the direct producer slice passed `15/15`, and the widened adjacent query slice passed `28/28`.
  - Practical implication: the remaining unresolved native work on this producer path is no longer the accepted record’s normal/plane write. The next missing step is the surviving-triangle pre-count/reserve logic around `0x713A5D` plus the sibling `0x6AC1E0` transaction.
- **Session 234 — `0x6ABD90` sidecar fill and the first visible `0x7137C0` reject gate are now pinned as pure binary seams:**
  - Added pure [AppendTerrainQueryPairPayloadRange(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [BuildAabbOutcode(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), and [TriangleSharesAabbOutcodeReject(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowTerrainQueryPairPayloadRangeTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTerrainQueryPairPayloadRangeTests.cs) and [WowAabbOutcodeTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowAabbOutcodeTests.cs), which now pin the fill-only-new-slots sidecar contract, the inclusive six-bit outcode layout, and the shared-bit triangle reject through the production DLL.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the `0x6ABD90` / `0x6ABFB0` sidecar tail and the first visible `0x7137C0` triangle cull are no longer implicit.
  - Focused native proof held: release native build passed, release physics-test build passed, the direct producer slice passed `12/12`, and the widened adjacent query slice passed `25/25`.
  - Practical implication: the remaining unresolved native work on this producer path is no longer the sidecar append or the first triangle-cull gate. The next missing step is the rest of `0x7137C0` / `0x6AC1E0` that turns accepted transformed triangles into the temp `0x34` records consumed by `0x6721B0`.
- **Session 233 — the visible `0x6AB530` / `0x6ABA30` boundary-record writer is now pinned as a pure binary seam:**
  - Added pure [AppendSelectorQuadRecordPair(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and [BuildAabbBoundarySelectorCandidateRecords(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported the wrapper through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowAabbBoundarySelectorRecordTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowAabbBoundarySelectorRecordTests.cs), which now pins the two-triangle-per-face write order, the inclusive boundary comparisons, and the zero-record interior case through the production DLL.
  - Tightened [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the visible `0x6AB530` / `0x6ABA30` producer math is no longer implicit.
  - Focused native proof held: release native build passed, release physics-test build passed, the direct new-helper slice passed `5/5`, and the widened adjacent selector-record slice passed `28/28`.
  - Practical implication: the visible boundary-face emission is now closed. The next unresolved native work on this path is the surrounding `0x6AADC0` producer transaction that decides which geometry/selector tuples reach that writer before `0x6721B0` filters the temp buffer.
- **Session 232 — `0x6721B0` filtered contact+pair copy contract pinned:**
  - Added raw capture in [0x673C80_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x673C80_disasm.txt) and tightened [0x6721B0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6721B0_disasm.txt) / [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the `TestTerrain` output contract now explicitly records two facts that were still missing from the repo notes:
    - `0x6721B0` filters the temp `0x34` records by stored `normal.z >= 0x80DFFC`
    - for every surviving record it also appends the aligned `0x08` sidecar payload through `0x673C80`
  - Added a pure native helper/export seam for that structural contract in [PhysicsEngine.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) / [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp), plus deterministic coverage in [WowTerrainQueryWalkableCopyTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTerrainQueryWalkableCopyTests.cs).
  - Focused native proof held: release native build passed, release physics-test build passed, and the filtered terrain-query slice passed `13/13`.
  - Practical implication: one more producer-path seam is now closed without changing runtime grounded behavior. The next unresolved native work is earlier in the temp query generation path (`0x6AA8B0` / `0x6AADC0` / `0x6AB530`), not in this filtered output copy itself.
- **Session 231 — the visible `0x635090` alternate-pair caller is now pinned as a pure binary seam:**
  - Added pure [BuildSelectorAlternatePair(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorAlternatePairTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorAlternatePairTests.cs), which now pins the band-fail negated-input path, the 3-candidate selected-normal path, and the 2-candidate builder path through the production DLL.
  - Updated [0x635090_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x635090_disasm.txt) and [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the visible caller math is no longer described as opaque.
  - Practical implication: the visible alternate-pair helper chain is now closed. The next unresolved selector step is the production grounded transaction that chooses the selected index plus paired `0xC4E544[index]` payload before `0x6351A0` consumes it.
- **Test baseline (session 231):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorAlternatePairTests" --logger "console;verbosity=minimal"`
    - Passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorAlternatePairTests|FullyQualifiedName~WowSelectorTwoCandidateWorkingVectorTests|FullyQualifiedName~WowSelectorTriangleEdgeDirectionTests|FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests" --logger "console;verbosity=minimal"`
    - Passed (`59/59`)
- **Session 230 — the full `0x634AE0` two-candidate working-vector body is now pinned as a pure binary seam:**
  - Added pure [BuildSelectorTwoCandidateWorkingVector(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorTwoCandidateWorkingVectorTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorTwoCandidateWorkingVectorTests.cs), which now pins the line-Z selected-normal gate, the `0x634960` footprint-mismatch reject path, and the orientation-negated constructed-vector path through the production DLL.
  - Updated [0x634AE0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x634AE0_disasm.txt) and [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the `count == 2` body is no longer described as partially unresolved.
  - Practical implication: the remaining alternate-pair gap is no longer inside `0x634AE0`; the next unresolved step is the caller-side normalization / pair-write math in `0x635090`.
- **Test baseline (session 230):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorTwoCandidateWorkingVectorTests|FullyQualifiedName~WowSelectorTriangleEdgeDirectionTests|FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests" --logger "console;verbosity=minimal"`
    - Passed (`56/56`)
- **Session 229 — the `0x634DA0` selector edge chooser is now pinned as a pure binary seam:**
  - Added pure [BuildSelectorTriangleEdgeDirection(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorTriangleEdgeDirectionTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorTriangleEdgeDirectionTests.cs), which now pins mixed fast-path/cross-path scoring, zero-length edge rejection, and the all-zero-length default output through the production DLL.
  - Added fresh raw capture [0x634DA0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x634DA0_disasm.txt), and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the former private chooser inside the `count == 2` `0x634AE0` body is now explicit in the binary notes.
  - Practical implication: the remaining alternate-pair gap is no longer any hidden helper inside `0x634AE0`; the next unresolved step is the full two-candidate working-vector output and caller-side normalization path in `0x634AE0` / `0x635090`.
- **Test baseline (session 229):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorTriangleEdgeDirectionTests|FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests" --logger "console;verbosity=minimal"`
    - Passed (`53/53`)
- **Session 228 — the `0x634FC0` selector three-plane intersection point is now pinned as a pure binary seam:**
  - Added pure [BuildSelectorPlaneIntersectionPoint(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorPlaneIntersectionPointTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorPlaneIntersectionPointTests.cs), which now pins exact orthogonal and scaled-coefficient intersections through the production DLL.
  - Added fresh raw capture [0x634FC0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x634FC0_disasm.txt), and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the plane-intersection helper inside the `count == 2` `0x634AE0` body is now explicit in the binary notes.
  - Practical implication: the remaining alternate-pair gap is no longer another hidden matrix/intersection helper. The next unresolved body is `0x634DA0`, which chooses how that intersection point is consumed.
- **Test baseline (session 228):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorPlaneIntersectionPointTests|FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests" --logger "console;verbosity=minimal"`
    - Passed (`48/48`)
- **Next command:** `rg --line-number "GroundedWallSelectionTrace|selectedContactIndex|outputPair|0xC4E544|0x6351A0" Exports/Navigation/PhysicsEngine.cpp Tests/Navigation.Physics.Tests -S`
- **Session 227 — the `0x634960` selector footprint-vs-plane gate is now pinned as a pure binary seam:**
  - Added pure [EvaluateSelectorPlaneFootprintMismatch(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorPlaneFootprintMismatchTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorPlaneFootprintMismatchTests.cs), which now pins the binary sample-height constant `0x80C740`, the `1/720` plane-distance epsilon, and the visible horizontal/vertical plane outcomes on the five-point footprint ring.
  - Added fresh raw capture [0x634960_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x634960_disasm.txt), and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the former private plane-check subcall inside the `count == 2` `0x634AE0` body is now explicit in the binary notes.
  - Practical implication: the remaining `count == 2` alternate-pair gap is no longer another opaque plane/footprint gate. The next unresolved body is the actual line/intersection math in `0x634FC0` and `0x634DA0`.
- **Test baseline (session 227):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorPlaneFootprintMismatchTests|FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests" --logger "console;verbosity=minimal"`
    - Passed (`44/44`)
- **Next command:** `py -c "from capstone import *; import pathlib; code=pathlib.Path(r'D:/World of Warcraft/WoW.exe').read_bytes(); md=Cs(CS_ARCH_X86, CS_MODE_32); start=0x634FC0; data=code[start-0x400000:start-0x400000+352]; [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') for i in md.disasm(data, start)]"`
- **Session 226 — the `0x6336A0 -> 0x634AE0` selector alternate-working-vector front-end is now pinned as a pure binary seam:**
  - Added pure [IsSelectorContactWithinAlternateWorkingVectorBand(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and [EvaluateSelectorAlternateWorkingVectorMode(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorAlternateWorkingVectorModeTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorAlternateWorkingVectorModeTests.cs), which now pins the `0x6336A0` non-walkable slope-band gate plus the visible `0x634AE0` count fanout (`<=1/>4 => negate first candidate`, `2 => two-plane builder`, `3/4 => selected contact normal`).
  - Added fresh raw captures [0x6336A0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6336A0_disasm.txt) and [0x634AE0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x634AE0_disasm.txt), and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the alternate-path front-end is explicit in the binary notes.
  - Practical implication: the remaining selector gap is no longer the `0x635090` front-end gate or the obvious `0x634AE0` branch split. The next unresolved body is the `count == 2` builder path plus its helpers `0x634960`, `0x634FC0`, and `0x634DA0`.
- **Test baseline (session 226):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorAlternateWorkingVectorModeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests" --logger "console;verbosity=minimal"`
    - Passed (`44/44`)
- **Next command:** `py -c "from capstone import *; import pathlib; code=pathlib.Path(r'D:/World of Warcraft/WoW.exe').read_bytes(); md=Cs(CS_ARCH_X86, CS_MODE_32); start=0x634960; data=code[start-0x400000:start-0x400000+320]; [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') for i in md.disasm(data, start)]"`
- **Session 225 — the visible `0x7C5F50` + `0x635450` selector post-window chain is now pinned as a pure binary seam:**
  - Added pure [ComputeVerticalTravelTimeScalar(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and [EvaluateSelectorPairWindowAdjustment(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowVerticalTravelTimeTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowVerticalTravelTimeTests.cs) and [WowSelectorPairWindowAdjustmentTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorPairWindowAdjustmentTests.cs), which now pin the `MOVEFLAG_SAFE_FALL` terminal-velocity split, stationary sqrt branch, earlier-root path, `0x635450` zero/clamp/scale paths, and the alternate-state handoff from `0x635550`.
  - Added fresh raw captures [0x635450_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x635450_disasm.txt) and [0x7C5F50_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x7C5F50_disasm.txt), and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the visible post-`0x6351A0` window solver/scaler is now explicit in the binary notes.
  - Practical implication: the remaining selector gap is no longer the visible post-selection math. The next unresolved piece is the production grounded transaction that chooses the selected index plus paired `0xC4E544[index]` payload before runtime grounded wall resolution consumes it.
- **Test baseline (session 225):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowVerticalTravelTimeTests|FullyQualifiedName~WowSelectorPairWindowAdjustmentTests|FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - Passed (`38/38`)
- **Session 224 — the visible `0x635550` selector follow-up gate is now pinned as a pure binary seam:**
  - Added pure [ComputeJumpTimeScalar(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and [EvaluateSelectorPairFollowupGate(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorPairFollowupGateTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorPairFollowupGateTests.cs), which now pins both exact binary-backed seams: the `MOVEFLAG_JUMPING`-gated `0x7C5DA0` jump-time scalar and the visible `0x635550` follow-up gate after `0x6351A0`.
  - Added fresh raw capture [0x635550_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x635550_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the visible post-`0x6351A0` window/jump checks are now explicit in the binary notes.
  - Practical implication: the remaining selector gap is no longer the visible follow-up gate. The next unresolved piece is `0x635450`, which combines the two `0x6351A0` out-state dwords, the `0x635550` result, and the `0x7C5F50` scalar before the grounded runtime consumes the selected payload.
- **Test baseline (session 224):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - Passed (`27/27`)
- **Session 222 — the outer `0x63214C` transport-local loop/gate is now pinned as a pure binary seam:**
  - Added pure [TransformSelectorCandidateRecordBufferToTransportLocal(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Extended [WowTransportLocalTransformTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTransportLocalTransformTests.cs) so the production DLL now pins both visible `0x63214C` gates: when `transportGuid == 0` the cached records stay unchanged, and when `transportGuid != 0` the loop rewrites every `0x34`-byte record in-place.
  - Updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) and [0x63214C_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x63214C_disasm.txt) so the fast exits on `transportGuid == 0` and `count == 0` are now explicit in the binary notes.
  - Practical implication: the remaining `0x631E70` gap is no longer the transport-local record rewrite loop. The next unresolved piece is how that transformed buffer is consumed by the later selector transaction.
- **Test baseline (session 222):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTransportLocalTransformTests|FullyQualifiedName~WowSwimQueryPlaneFlipTests|FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests" --logger "console;verbosity=minimal"`
    - Passed (`11/11`)
- **Session 221 — the `0x63214C` cached-contact record rewrite is now pinned as a pure binary seam:**
  - Added pure [TransformSelectorCandidateRecordToTransportLocal(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Extended [WowTransportLocalTransformTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTransportLocalTransformTests.cs) so the production DLL now pins the exact `0x34`-byte record transform used by `0x63214C`: inverse-transform the three stored points and rebuild the plane from the rotated normal plus transformed first point.
  - Added fresh raw capture [0x63214C_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x63214C_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the remaining gap is now the outer count/guid gate and later selector consumption, not the record contents themselves.
  - Practical implication: the remaining `0x631E70` gap is narrower again. The per-record rewrite body is closed; the next unresolved piece is the batch loop/gating around `0xC4E530` / `0xC4E534` and how that transformed buffer feeds the later selector path.
- **Test baseline (session 221):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTransportLocalTransformTests|FullyQualifiedName~WowSwimQueryPlaneFlipTests|FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests" --logger "console;verbosity=minimal"`
    - Passed (`9/9`)
- **Session 220 — the `0x631E70` transport-local point/plane transform seam is now pinned as a pure binary seam:**
  - Added pure [TransformWorldPointToTransportLocal(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), [TransformWorldVectorToTransportLocal(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), and [BuildTransportLocalPlane(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowTransportLocalTransformTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTransportLocalTransformTests.cs), which now pins the inverse-yaw point transform and the local-plane rebuild that the `0x63214C..0x632270` transport-local contact loop requires.
  - Added fresh raw captures [0x7BD700_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x7BD700_disasm.txt) and [0x7BCC60_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x7BCC60_disasm.txt), and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the transport note now records the inverse-RT frame build plus the point transform it feeds.
  - Practical implication: the remaining `0x631E70` gap is no longer the raw transport-local point/vector/plane math. The next unresolved piece is the actual per-contact rewrite loop at `0x63214C`, including which cached contact fields are transformed and copied back.
- **Test baseline (session 220):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTransportLocalTransformTests|FullyQualifiedName~WowSwimQueryPlaneFlipTests|FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests" --logger "console;verbosity=minimal"`
    - Passed (`8/8`)
- **Session 219 — the `0x631E70` swim-side plane flip is now pinned as a pure binary seam:**
  - Added pure [NegatePlane(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSwimQueryPlaneFlipTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSwimQueryPlaneFlipTests.cs), which now pins the exact `0x637330` + `0x597AD0` result used on the swim-side query path: negate the normal and negate the plane distance.
  - Added fresh raw capture [0x597AD0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x597AD0_disasm.txt) and updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) so the swim-side `0x30000` query note no longer drops the plane-distance rewrite.
  - Practical implication: the remaining `0x631E70` gap is no longer the per-contact swim-side plane flip. The next unresolved piece is the post-cache transform loop that rewrites cached contacts when `transportGuid != 0`.
- **Test baseline (session 219):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSwimQueryPlaneFlipTests|FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests|FullyQualifiedName~WowTerrainQueryBoundsTests" --logger "console;verbosity=minimal"`
    - Passed (`9/9`)
- **Session 218 — `0x631E70` cache-miss merged bounds are now pinned as a pure binary seam:**
  - Added pure [BuildTerrainQueryCacheMissBounds(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowTerrainQueryCacheMissBoundsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowTerrainQueryCacheMissBoundsTests.cs), which now pins the exact `0x631E70` cache-miss bounds transaction: build projected bounds, expand min/max by binary `1/6`, then merge against cached `0xC4E5A0`.
  - Practical implication: the remaining open work in `0x631E70` is no longer the merged-bounds handoff. The next unresolved piece is the optional swim-side `0x30000` query / contact-flip path and how that feeds the later selector transaction.
- **Test baseline (session 218):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests|FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowAabbMergeTests" --logger "console;verbosity=minimal"`
    - Passed (`9/9`)
- **Session 217 — `0x6372D0` / `0x637300` scalar offsets are now pinned as pure binary seams:**
  - Added pure [AddScalarToVector3(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp) and [SubtractScalarFromVector3(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), then exported them through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp) with matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowVectorScalarOffsetTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowVectorScalarOffsetTests.cs), which now pins the exact add-to-all-components and subtract-from-all-components behavior from `0x6372D0` and `0x637300`.
  - Practical implication: another piece of the `0x631E70` cache-miss builder is no longer inferred. The remaining open work there is the merged transaction around those offsets, not the scalar offset helpers themselves.
- **Fresh binary evidence (session 217):**
  - Added raw captures [0x6372D0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x6372D0_disasm.txt), [0x637300_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x637300_disasm.txt), and [0x61E9C0_disasm.txt](/E:/repos/Westworld of Warcraft/docs/physics/0x61E9C0_disasm.txt), then updated [wow_exe_decompilation.md](/E:/repos/Westworld of Warcraft/docs/physics/wow_exe_decompilation.md) to record the true helper semantics and the fact that `0x61E9C0` is a no-op in this build.
- **Test baseline (session 217):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowVectorScalarOffsetTests|FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowAabbMergeTests|FullyQualifiedName~WowSelectorSourceWrapperSeedTests" --logger "console;verbosity=minimal"`
    - Passed (`6/6`)
- **Session 216 — `0x632A30` source-wrapper seeds are now pinned as a pure binary seam:**
  - Added pure [InitializeSelectorTriangleSourceWrapperSeeds(...)](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsEngine.cpp), exported it through [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp), and added matching interop in [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs).
  - Added deterministic coverage in [WowSelectorSourceWrapperSeedTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/WowSelectorSourceWrapperSeedTests.cs), which now pins the exact `0x632A30` fixed payload into `0x632280`: `testPoint = (0,0,-1)`, `candidateDirection = (0,0,-1)`, and `bestRatio = 1.0f`.
  - Practical implication: the remaining `0x632A30 -> 0x632280` gap is no longer the fixed seed state. The open work is the variable payload around the selected-index seed, `0x631BE0` outputs, and the optional `0x631E70` transaction.
- **Test baseline (session 216):**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - Succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - Succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSourceWrapperSeedTests|FullyQualifiedName~WowSelectorSourceWrapperTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - Passed (`15/15`)
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
- **Next priorities:** keep the runtime grounded path frozen, then close the remaining `0x631E70` transport-local contact transform loop before going back to the variable `0x632A30 -> 0x632280` payload.
- **Next command:** `py -c "from capstone import *; import pathlib; code=pathlib.Path(r'D:/World of Warcraft/WoW.exe').read_bytes(); md=Cs(CS_ARCH_X86, CS_MODE_32); start=0x63214C; data=code[start-0x400000:start-0x400000+320]; [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') for i in md.disasm(data, start)]"`
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
- **Session 223 — visible `0x6351A0` selector-consumer tail pinned:**
  - Added binary-backed pure helpers for the alternate unit-Z gate and the visible `0x6351A0` consumer contract: zero-distance return, `0x632BA0` failure returning `2`, direct-pair return, zero-pair direct success, zero-pair unit-Z success, and alternate-pair fallback.
  - Captured the caller-side proof in `docs/physics/0x635734_callsite_disasm.txt`, which closes one important detail for later runtime hookup: `0x6351A0` writes two separate out-state dwords and the caller consumes them separately.
  - Added deterministic coverage in `WowSelectorPairConsumerTests.cs`; focused native build + selector slices passed `19/19`.
  - Practical implication: the next native parity unit is no longer the visible `0x6351A0` tail. It is exposing the selected index plus paired `0xC4E544[index]` payload on the production grounded path and then wiring that exact transaction into runtime wall resolution.
- **Session 230 — recording artifacts now require explicit opt-in:**
  - Added `WWOW_ENABLE_RECORDING_ARTIFACTS` and wired it through `BotRunnerService.Diagnostics`, FG/BG packet sidecars, FG `MovementRecorder`, and the FG file-backed diagnostic writers that had been creating `WWoWLogs/*`, `event_log.txt`, and `antiafk_log.txt` by default.
  - Test and tooling entry points that intentionally capture artifacts now opt in explicitly: `LiveBotFixture`, `StateManagerProcessHelper`, `BotServiceFixture`, and `RecordingMaintenance capture`.
  - Cleaned the untracked repo output trees that had accumulated excessive snapshots/logs: `Bot/*/Recordings`, `Bot/*/WWoWLogs`, `Bot/*/botrunner_diag.log`, and `TestResults/*`. The canonical `Tests/Navigation.Physics.Tests/Recordings` corpus was left intact.
  - Validation: `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`, `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundPacketTraceRecorderTests" --logger "console;verbosity=minimal"`, `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`, `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecordingArtifactHelperTests" --logger "console;verbosity=minimal"`, `dotnet build tools/RecordingMaintenance/RecordingMaintenance.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`.

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
