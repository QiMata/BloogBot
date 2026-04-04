# Master Tasks — Validation Phase

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order within each phase.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live.** Never defer live validation tests.
5. Every fix must include or update a focused test.
6. After each shipped delta, commit and push before ending the pass.
7. **Previous implementation phases (P3-P29) are archived** — see `docs/ARCHIVE.md`.

---

## Current Test Baseline (2026-04-04, post V1.1 fix)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1410 | 0 | 1 | All green after V1.1 reflection fix |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing Undercity elevator Z sync (known) |
| BotRunner.Tests (unit) | TBD | TBD | ~80 | LiveValidation tests skip without infra |

---

## V1 — Fix Existing Test Failures (Priority: Critical)

**Goal:** Green test suite. Fix the 5 known failures before adding new validation.

| # | Task | Status |
|---|------|--------|
| 1.1 | **Fix MovementController NearbyObjects failures (3 tests)** — Reflection used `BindingFlags.Static` but P9.2 changed fields to instance. Fixed to `BindingFlags.Instance`. 3/3 pass. | **Done** (810196d8) |
| 1.2 | **Investigate Undercity elevator failures (2 tests)** — Pre-existing P7 transport Z sync issue. SimZ=39.8 vs RecZ=42.6 at elevator exit (2.84y Z error, 0.017y XY error). Root cause: elevator spline evaluation doesn't fully resolve Z at transport exit transition. Horizontal parity is excellent. Not a regression. | **Known** — pre-existing transport physics gap |
| 1.3 | **Run full solution test suite and document baseline** — WoWSharpClient: 1410/0/1. Physics: 666/2/1. Baseline recorded above. | **Done** (810196d8) |

---

## V2 — Flesh Out Stub Test Bodies (Priority: High)

**Goal:** Replace snapshot-only stubs with real test logic. ~80 LiveValidation test files currently have placeholder bodies that just assert snapshot != null. Each needs real setup/action/assert logic wired against live MaNGOS.

### V2A — Economy & Interaction Tests (highest value — most protocol coverage)

| # | Task | Status |
|---|------|--------|
| 2.1 | **TradingTests** — OFFER_TRADE/DECLINE_TRADE/ACCEPT_TRADE flow with gold transfer. | **Done** (c5207f42) |
| 2.2 | **AuctionHouseTests** — Navigate to AH, find auctioneer NPC, interact. | **Done** (c5207f42) |
| 2.3 | **BankInteractionTests** — Find banker NPC, interact, deposit item. | **Done** (c5207f42) |
| 2.4 | **MailSystemTests** — Send mail with gold via CMSG_SEND_MAIL, verify SMSG_SEND_MAIL_RESULT, recipient opens mailbox and takes gold. | Open |
| 2.5 | **GuildOperationTests** — Create guild via `.guild create`, invite second bot, accept, assert both in roster. | Open |

### V2B — Combat & BG Tests

| # | Task | Status |
|---|------|--------|
| 2.6 | **WandAttackTests** — Equip wand via GM, target mob, CastSpellByName('Shoot'), assert SMSG_ATTACKERSTATUPDATE with ranged damage. | Open |
| 2.7 | **BattlegroundQueueTests** — Navigate to WSG battlemaster, send CMSG_BATTLEMASTER_JOIN, assert SMSG_BATTLEFIELD_STATUS with queued status. | Open |
| 2.8 | **WsgObjectiveTests** — After BG entry, locate flag game object, interact, carry to base. Assert SMSG_UPDATE_WORLD_STATE for capture. | Open |

### V2C — Travel & Navigation Tests

| # | Task | Status |
|---|------|--------|
| 2.9 | **TravelPlannerTests** — BG bot in Orgrimmar, send TravelTo(Crossroads). Assert bot walks south, arrives within 10 minutes. | Open |
| 2.10 | **MageTeleportTests** — Mage bot casts Teleport: Orgrimmar (3567), assert position changes to Org within 15s. | Open |
| 2.11 | **TaxiTests** — Bot at Org flight master, activate flight to Crossroads, assert CMSG_ACTIVATETAXI sent, arrival within 3 min. | Open |
| 2.12 | **TransportTests** — Bot at Org zeppelin tower, board zeppelin, assert TransportGuid set, mapId changes to 0. | Open |

### V2D — Raid & Dungeon Tests

| # | Task | Status |
|---|------|--------|
| 2.13 | **RaidFormationTests** — Form 10-man raid, assign subgroups via CMSG_GROUP_CHANGE_SUB_GROUP, verify group list. | Open |
| 2.14 | **RaidCoordinationTests** — Ready check (MSG_RAID_READY_CHECK), raid marks (CMSG_SET_RAID_ICON), loot rules. | Open |
| 2.15 | **SummoningStoneTests** — 3 bots at WC meeting stone, summon 2 from Org, assert arrival. | Open |

### V2E — Remaining Stubs

| # | Task | Status |
|---|------|--------|
| 2.16 | **ChannelTests** — Join General, send message, second bot receives via SMSG_MESSAGECHAT. | Open |
| 2.17 | **QuestObjectiveTests** — Accept kill quest, kill mobs, assert SMSG_QUESTUPDATE_ADD_KILL increments. | Open |
| 2.18 | **PetManagementTests** — Hunter summons pet, set Defensive stance, feed, use Growl in combat. | Open |
| 2.19 | **SpiritHealerTests** — Kill bot, release, navigate to spirit healer, activate, assert res sickness. | Open |
| 2.20 | **GossipQuestTests** — Interact with multi-option NPC, select options, verify sub-frames open. | Open |

---

## V3 — Integration Validation (Priority: Medium)

**Goal:** End-to-end validation of new BotRunner task implementations against live MaNGOS.

| # | Task | Status |
|---|------|--------|
| 3.1 | **Validate EncounterMechanicsTask** — 10-bot RFC clear, verify spread/stack/interrupt triggers fire during boss fights. | Open |
| 3.2 | **Validate PvPEngagementTask** — Flag PvP on two bots, verify HostilePlayerDetector scans correctly, engagement/flee decisions execute. | Open |
| 3.3 | **Validate EscortQuestTask** — Accept an escort quest, verify NPC following and defender behavior. | Open |
| 3.4 | **Validate TalentAutoAllocator** — Level-up bot, verify talents allocated per build path. | Open |
| 3.5 | **Validate LevelUpTrainerTask** — Bot levels up, navigates to trainer, trains spells. | Open |
| 3.6 | **Validate AuctionPostingService** — Scan AH, verify undercut pricing logic against real market data. | Open |
| 3.7 | **Validate BgRewardCollectionTask** — After BG win, verify mark turn-in at battlemaster. | Open |
| 3.8 | **Validate MasterLootDistributionTask** — 10-bot RFC, kill boss, verify loot window opens, items assigned. | Open |

---

## V4 — Scalability Validation (Priority: Low)

**Goal:** Validate P9 scalability infrastructure with increasing bot counts.

| # | Task | Status |
|---|------|--------|
| 4.1 | **10-bot MultiBotHostWorker test** — Launch MultiBotHostWorker with WWOW_MULTI_BOT_COUNT=10. All 10 login and enter world. | Open |
| 4.2 | **Validate PathResultCache hit rate** — 10 bots patrolling same zone, measure cache hit rate (target: >50%). | Open |
| 4.3 | **Validate SnapshotDeltaComputer compression** — Compare full vs delta snapshot sizes for idle/active bots. | Open |
| 4.4 | **Validate AsyncPathfindingWrapper** — Queue 100 concurrent path requests, verify all complete without deadlock. | Open |
| 4.5 | **100-bot baseline** — 100 bots login, move to Orgrimmar, patrol 60s. Measure P50/P95/P99 tick latency. | Open |
| 4.6 | **P9.2 singleton migration audit** — Grep for remaining `WoWSharpObjectManager.Instance` calls outside tests. Migrate to instance-based. | Open |

---

## Canonical Commands

```bash
# Full LiveValidation suite
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# WoWSharpClient unit tests (fast, no infra needed)
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "Category!=RequiresInfrastructure" --no-build

# Physics tests
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build

# Full solution (excludes C++ vcxproj)
dotnet test WestworldOfWarcraft.sln --configuration Release --filter "Category!=RequiresInfrastructure"
```

---

## Blocked - Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in `RecordedTests.Shared` | Requires `AWSSDK.S3` |
| `RTS-MISS-002` | Azure ops in `RecordedTests.Shared` | Requires `Azure.Storage.Blobs` |
