# Master Tasks — Feature Validation & Test Coverage

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order within each phase.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live.** Never defer live validation tests.
5. Every fix must include or update a focused test.
6. After each shipped delta, commit and push before ending the pass.
7. **Previous implementation phases (P3-P29, V1-V4) are archived** — see `docs/ARCHIVE.md`.

---

## Test Baseline (2026-04-04, post T1+T2)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1410 | 0 | 1 | Green |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing Undercity elevator |
| BotRunner.Tests (non-LV, non-infra) | 1624 | 4 | 1 | 4 infra-dependent (PathPerf 2, StateLoad 2) |

---

## T1 — Unit Tests for New BotRunner Tasks (Priority: Critical)

**Goal:** Every new BotRunner task class gets at least 2-3 unit tests covering state transitions, edge cases, and key behavior. These are fast tests using mocked IObjectManager/IBotContext — no live server needed.

**62 new files, 0 unit tests.** This is the biggest gap.

### T1A — Combat & Raid Systems

| # | Task | Status |
|---|------|--------|
| 1.1 | **ThreatTracker tests** — RecordDamage accumulates, RecordHealing at 0.5x, ShouldThrottle at 90%, GetHighestThreat returns tank, Reset clears. File: `Tests/BotRunner.Tests/Combat/ThreatTrackerTests.cs` | **Done** (4bed6f4c) |
| 1.2 | **RaidCooldownCoordinator tests** — RecordUsage logs entry, IsSafeToUse respects gap, GetNextAvailableOwner returns least-recent. File: `Tests/BotRunner.Tests/Combat/RaidCooldownCoordinatorTests.cs` | **Done** (4bed6f4c) |
| 1.3 | **EncounterPositioning tests** — GetMeleePosition behind boss, GetTankPosition in front, IsInFrontCleaveZone cone math, IsInTailSwipeZone. File: `Tests/BotRunner.Tests/Combat/EncounterPositioningTests.cs` | **Done** (4bed6f4c) |
| 1.4 | **BgTargetSelector tests** — Prioritizes low-HP targets, deprioritizes full-HP, handles empty list. File: `Tests/BotRunner.Tests/Combat/BgTargetSelectorTests.cs` | **Done** (4bed6f4c) |
| 1.5 | **HostilePlayerDetector tests** — IsPvPFlagged checks UnitFlags, IsCivilian checks passive flag, AssessThreat levels correct by level diff, GetFactionSide maps correctly. File: `Tests/BotRunner.Tests/Combat/HostilePlayerDetectorTests.cs` | **Done** (4bed6f4c) |
| 1.6 | **RaidRoleAssignment tests** — SetMainTank/Assist, GetRole defaults DPS, AutoAssignMainTank picks first tank, GetPlayersWithRole filters. File: `Tests/BotRunner.Tests/Combat/RaidRoleAssignmentTests.cs` | **Done** (4bed6f4c) |
| 1.7 | **LootCouncilSimulator tests** — RecordRoll generates 1-100, GetWinner prioritizes MainSpec>OffSpec>Greed then highest roll, AllRollsIn counts, ClearItem removes. File: `Tests/BotRunner.Tests/Combat/LootCouncilSimulatorTests.cs` | **Done** (c590bbdd) |

### T1B — Economy & Services

| # | Task | Status |
|---|------|--------|
| 1.8 | **AuctionPostingService tests** — RecordPrice stores, GetMarketPrice returns cached, EvaluatePosting undercuts 5%, rejects below vendor price, stale price purge. File: `Tests/BotRunner.Tests/Economy/AuctionPostingServiceTests.cs` | **Done** (c590bbdd) |
| 1.9 | **GoldThresholdManager tests** — Evaluate returns SellVendorTrash below min, DepositExcess above threshold, None in range. CalculateDepositAmount subtracts reserve. GetDefaultReserve scales by level. File: `Tests/BotRunner.Tests/Economy/GoldThresholdManagerTests.cs` | **Done** (c590bbdd) |
| 1.10 | **WhisperTracker tests** — RecordIncoming/Outgoing stores, GetHistory returns ordered, HasUnreadWhispers detects, max messages evicts oldest. File: `Tests/BotRunner.Tests/Social/WhisperTrackerTests.cs` | **Done** (c590bbdd) |

### T1C — Progression & Routing

| # | Task | Status |
|---|------|--------|
| 1.11 | **TalentAutoAllocator tests** — GetNextAllocation returns correct talent for level/points, returns null when all spent, GetPendingAllocations returns burst list. File: `Tests/BotRunner.Tests/Progression/TalentAutoAllocatorTests.cs` | **Done** (c590bbdd) |
| 1.12 | **ZoneLevelingRoute tests** — GetZoneForLevel returns correct zone, GetNextZone advances, Horde/Alliance routes are different, level 60 returns endgame zones. File: `Tests/BotRunner.Tests/Progression/ZoneLevelingRouteTests.cs` | **Done** (c590bbdd) |
| 1.13 | **QuestChainRouter tests** — GetNextStep skips completed quests, returns null for complete chain, GetNearestQuestGiver finds closest. File: `Tests/BotRunner.Tests/Questing/QuestChainRouterTests.cs` | **Done** (c590bbdd) |
| 1.14 | **ProfessionTrainerScheduler tests** — NeedsTraining detects tier boundaries (75/150/225), GetTrainer returns correct position for faction. File: `Tests/BotRunner.Tests/Crafting/ProfessionTrainerSchedulerTests.cs` | **Done** (c590bbdd) |
| 1.15 | **AmmoManager tests** — NeedsAmmo true when below 200, GetBestAmmoForLevel returns correct tier, GetNearestAmmoVendor returns closest. File: `Tests/BotRunner.Tests/Progression/AmmoManagerTests.cs` | **Done** (c590bbdd) |

### T1D — Scalability Infrastructure

| # | Task | Status |
|---|------|--------|
| 1.16 | **PathResultCache tests** — Store/TryGet round-trip, grid quantization groups nearby positions, Evict removes oldest, InvalidateMap clears map entries, HitRate calculates correctly. File: `Tests/BotRunner.Tests/Clients/PathResultCacheTests.cs` | **Done** (c590bbdd) |
| 1.17 | **SnapshotBatcher tests** — Enqueue buffers, FlushAsync processes batch, timer triggers flush, max batch size caps. File: `Tests/BotRunner.Tests/Clients/SnapshotBatcherTests.cs` | **Done** (c590bbdd) |
| 1.18 | **ConnectionMultiplexer tests** — GetConnectionAsync routes by hash, same bot always gets same connection, InvalidateConnection forces re-create. File: `Tests/BotRunner.Tests/Clients/ConnectionMultiplexerTests.cs` | **Done** (c590bbdd) |
| 1.19 | **PathfindingShardRouter tests** — GetShard consistent hash, CreateLocal generates N shards on sequential ports. File: `Tests/BotRunner.Tests/Clients/PathfindingShardRouterTests.cs` | **Done** (c590bbdd) |
| 1.20 | **SnapshotDeltaComputer tests** — First call returns full snapshot, subsequent returns delta when changed, unchanged returns small delta, ApplyDelta reconstructs original. File: `Tests/BotRunner.Tests/IPC/SnapshotDeltaComputerTests.cs` | **Done** (c590bbdd) |

### T1E — Travel & Transport Data

| # | Task | Status |
|---|------|--------|
| 1.21 | **TransportScheduleService tests** — FindRoute matches by mapId, GetBoardingDock returns correct side, GetRoutesFromMap lists departures. 7 routes defined. File: `Tests/BotRunner.Tests/Travel/TransportScheduleServiceTests.cs` | **Done** (c590bbdd) |
| 1.22 | **InnkeeperData tests** — FindNearest returns closest innkeeper, GetByFaction filters, all 26 entries have valid positions. File: `Tests/BotRunner.Tests/Travel/InnkeeperDataTests.cs` | **Done** (c590bbdd) |
| 1.23 | **GraveyardData tests** — FindNearest, GetForZone, data loading. File: `Tests/BotRunner.Tests/Travel/GraveyardDataTests.cs` | **Done** (c590bbdd) |
| 1.24 | **SummoningStoneData tests** — GetByInstanceMapId, GetNearby, AllStones count. File: `Tests/BotRunner.Tests/Travel/SummoningStoneDataTests.cs` | **Done** (c590bbdd) |

---

## T2 — Fix Existing Test Failures (Priority: High)

| # | Task | Status |
|---|------|--------|
| 2.1 | **Fix BotRunner.Tests failures** — Fixed 7 TalentAllocationTests (wrong name format). Remaining 4: PathfindingPerformance (2, needs native DLL), StateManagerLoad (2, needs infra). | **Done** (cdaab68b) |
| 2.2 | **Ensure new unit tests pass** — 149 T1 tests + 8 TalentAllocation = all green. ScalabilityUnitTests included in T1.16-T1.20. | **Done** (c590bbdd) |
| 2.3 | **WoWSharpClient.Tests regression** — 1410/0/1 confirmed. | **Done** |
| 2.4 | **Physics tests regression** — 666/2/1 confirmed (2 pre-existing elevator). | **Done** |

---

## T3 — LiveValidation Against Running Server (Priority: High)

**Goal:** Run the wired LiveValidation tests against the live MaNGOS server and fix failures.

| # | Task | Status |
|---|------|--------|
| 3.1 | **Run BasicLoopTests + CharacterLifecycleTests** — Confirm bots login, enter world, move. | In progress — fixture launches StateManager+WoW.exe, 120s bot connect timeout |
| 3.2 | **Run CombatBgTests + CombatFgTests** — Confirm combat works for both bot types. | Pending T3.9 |
| 3.3 | **Run VendorBuySellTests** — Confirm buy/sell at vendor works. | Pending T3.9 |
| 3.4 | **Run EconomyInteractionTests** — Confirm bank/AH/mail interactions work. | Pending T3.9 |
| 3.5 | **Run TradingTests** — Confirm dual-bot trade flow works. | Pending T3.9 |
| 3.6 | **Run GroupFormationTests** — Confirm party/raid formation works. | Pending T3.9 |
| 3.7 | **Run NavigationTests** — Confirm pathfinding-based movement works. | Pending T3.9 |
| 3.8 | **Run TaxiTests against live flight master** — Confirm CMSG_ACTIVATETAXI flow. | Pending T3.9 |
| 3.9 | **Run all LiveValidation suite** — Full sweep running in background. Awaiting results. | In progress |

---

## T4 — Proto Snapshot Pipeline Validation (Priority: Medium)

**Goal:** Verify new proto fields (QuestObjectiveProgress, ProfessionSkillEntry, build_template) flow end-to-end through the snapshot pipeline.

| # | Task | Status |
|---|------|--------|
| 4.1 | **Quest objective proto round-trip test** — QuestLogEntry.objectives serialize/deserialize verified (7 tests). | **Done** (aaafc820) |
| 4.2 | **Profession skill proto round-trip test** — ProfessionSkillEntry serialize/deserialize verified. | **Done** (aaafc820) |
| 4.3 | **Build template proto round-trip test** — CharacterDefinition.build_template survives round-trip. | **Done** (aaafc820) |

---

## Canonical Commands

```bash
# Unit tests only (fast, no server needed)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "Category!=RequiresInfrastructure&Category!=RequiresService" --no-build

# WoWSharpClient tests
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "Category!=RequiresInfrastructure" --no-build

# Physics tests
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build

# Full LiveValidation suite (needs MaNGOS + StateManager)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Full solution
dotnet test WestworldOfWarcraft.sln --configuration Release
```

---

## Blocked - Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in `RecordedTests.Shared` | Requires `AWSSDK.S3` |
| `RTS-MISS-002` | Azure ops in `RecordedTests.Shared` | Requires `Azure.Storage.Blobs` |
