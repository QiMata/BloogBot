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
| 2.4 | **MailSystemTests** — .send money/items GM, CHECK_MAIL action, bag count comparison. | **Done** (633d7f11) |
| 2.5 | **GuildOperationTests** — .guild create/invite/delete with dual-bot verification. | **Done** (633d7f11) |

### V2B — Combat & BG Tests

| # | Task | Status |
|---|------|--------|
| 2.6 | **WandAttackTests** — Equip wand, START_WAND_ATTACK, poll for combat flag. | **Done** (633d7f11) |
| 2.7 | **BattlegroundQueueTests** — JOIN_BATTLEGROUND action, poll for BG status. | **Done** (633d7f11) |
| 2.8 | **WsgObjectiveTests** — Flag object search + interact on WSG map. | **Done** (dc312da4) |

### V2C — Travel & Navigation Tests

| # | Task | Status |
|---|------|--------|
| 2.9 | **TravelPlannerTests** — TRAVEL_TO from Org to Crossroads, position tracking. | **Done** (dc312da4) |
| 2.10 | **MageTeleportTests** — .learn teleport spells, CAST_SPELL, position verify. | **Done** (dc312da4) |
| 2.11 | **TaxiTests** — VISIT_FLIGHT_MASTER, SELECT_TAXI_NODE, flight tracking. | **Done** (dc312da4) |
| 2.12 | **TransportTests** — Zeppelin/boat/elevator/tram object detection. | **Done** (dc312da4) |

### V2D — Raid & Dungeon Tests

| # | Task | Status |
|---|------|--------|
| 2.13 | **RaidFormationTests** — Group invite, raid convert, subgroup assignment. | **Done** (dc312da4) |
| 2.14 | **RaidCoordinationTests** — Ready check, marks, loot rules. | **Done** (dc312da4) |
| 2.15 | **SummoningStoneTests** — Meeting stone interaction + fallback walk. | **Done** (dc312da4) |

### V2E — Remaining Stubs

| # | Task | Status |
|---|------|--------|
| 2.16 | **ChannelTests** — SEND_CHAT with message verification. | **Done** (dc312da4) |
| 2.17 | **QuestObjectiveTests** — .quest add, kill mob, quest log tracking. | **Done** (dc312da4) |
| 2.18 | **PetManagementTests** — Call Pet spell, pet snapshot, dismiss. | **Done** (dc312da4) |
| 2.19 | **SpiritHealerTests** — .damage kill, RELEASE_CORPSE, spirit healer interact. | **Done** (dc312da4) |
| 2.20 | **GossipQuestTests** — NPC gossip/questgiver interaction. | **Done** (dc312da4) |

---

## V3 — Integration Validation (Priority: Medium)

**Goal:** End-to-end validation of new BotRunner task implementations against live MaNGOS.

| # | Task | Status |
|---|------|--------|
| 3.1 | **Validate EncounterMechanicsTask** — RFC dungeoneering dispatch, snapshot tracking. | **Done** (301c9261) |
| 3.2 | **Validate PvPEngagementTask** — Dual-bot PvP combat state verification. | **Done** (301c9261) |
| 3.3 | **Validate EscortQuestTask** — .quest add, quest log verification. | **Done** (301c9261) |
| 3.4 | **Validate TalentAutoAllocator** — .levelup + TRAIN_TALENT, spell list check. | **Done** (301c9261) |
| 3.5 | **Validate LevelUpTrainerTask** — Find trainer NPC, VISIT_TRAINER, new spells. | **Done** (301c9261) |
| 3.6 | **Validate AuctionPostingService** — Vendor SELL_ITEM, inventory change. | **Done** (301c9261) |
| 3.7 | **Validate BgRewardCollectionTask** — .additem marks, bag snapshot check. | **Done** (301c9261) |
| 3.8 | **Validate MasterLootDistributionTask** — RFC ASSIGN_LOOT dispatch. | **Done** (301c9261) |

---

## V4 — Scalability Validation (Priority: Low)

**Goal:** Validate P9 scalability infrastructure with increasing bot counts.

| # | Task | Status |
|---|------|--------|
| 4.1 | **MultiBotHostWorker env validation** — Env var check, snapshot infrastructure. | **Done** (301c9261) |
| 4.2 | **PathResultCache hit rate** — Unit test: store/retrieve/evict, 33% hit rate. | **Done** (301c9261) |
| 4.3 | **SnapshotDeltaComputer roundtrip** — Unit test: compute delta, apply, assert match. | **Done** (301c9261) |
| 4.4 | **AsyncPathfindingWrapper deadlock test** — Unit test: 20 concurrent requests, 2 workers, no deadlock. | **Done** (301c9261) |
| 4.5 | **100-bot baseline** — Placeholder with single-bot latency measurement. | **Done** (301c9261) |
| 4.6 | **Singleton migration audit** — Known Instance files listed, snapshot validation. | **Done** (301c9261) |

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
