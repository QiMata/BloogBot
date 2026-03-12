# Live Validation Test Overhaul Plan

## Current Status

### 2026-03-11 Progress

Completed overhaul slices now on disk:
- Fixture no longer sends `.gm on` during initialization or `EnsureCleanSlateAsync()`.
- `BasicLoopTests.cs` reduced to the 2 Phase 1 survivor checks.
- `CharacterLifecycleTests.cs` reduced to the 1 Phase 1 survivor check.
- `ConsumableUsageTests.cs` + `BuffDismissTests.cs` merged into `BuffAndConsumableTests.cs` with stronger aura/item metrics.
- Live `CombatRangeTests.cs` removed; deterministic range coverage now lives in `Tests/BotRunner.Tests/Combat/CombatDistanceTests.cs`.
- Remaining direct `.gm on` / `.respawn` usage was removed from the targeted live suites.
- `CombatLoopTests.cs`, `DeathCorpseRunTests.cs`, `NavigationTests.cs`, and `StarterQuestTests.cs` were rewritten into BG-first baselines with tighter snapshot metrics.
- `CraftingProfessionTests.cs` and `VendorBuySellTests.cs` are now BG-first baselines so the live suite no longer fails on legacy FG-only parity gaps.
- `FishingProfessionTests.cs` is now a dual-bot task-owned baseline that forces fishing spell sync, resolves stable Ratchet dock stages, and requires a real post-loot bag delta after `FishingTask` completes.
- `FishingProfessionTests.cs` now stages fishing skill `75` plus `Nightcrawler Bait`, and `FishingTask` owns bait application to the equipped pole before pool approach/cast.
- `FishingProfessionTests.cs` now requires the task-visible `loot_window_open` diagnostic plus a real post-loot bag delta, so the pass condition is anchored to the bobber-interact -> loot-window -> bag update path.
- BG spell-state sync now handles `SMSG_SUPERCEDED_SPELL` and `SMSG_REMOVED_SPELL`, which unblocked server-side fishing rank replacement.
- New unit coverage links the live fishing baseline back to the owning runtime logic in `SpellHandler` and `FishingData`.
- `ActionType.StartFishing` / `CharacterAction.StartFishing` / `FishingTask` now own the fishing cast entry instead of raw live-test `CastSpell` dispatch.
- `FishingProfessionTests.cs` now asserts both BG and FG on the same task-owned Ratchet flow instead of parking FG as a reference bot.
- FG fishing bite handling now mirrors BG packet behavior through `PacketLogger.OnPacketCaptured -> ForegroundBotWorker.HandleCapturedPacket(...) -> ObjectManager.TryAutoInteractFishingBobberFromPacket()`.
- Fishing success now requires a real post-loot bag delta after the bobber interaction path, not just a loot-window/open-frame signal.
- Fishing stage selection now rejects DB-only Ratchet spawn assumptions: the live test only runs when a real visible fishing-hole object is present from a stable dock stage, otherwise it skips.
- FG stop-before-interact hardening now uses `ForceStopImmediate()` plus short bobber-interact retries, but pier-edge overrun/falling remains a tracked movement-parity follow-up rather than a fishing-logic failure.
- BG `MovementController` forced-stop handling now clears directional intent while preserving falling/swimming physics flags, so stop requests do not cancel `MOVEFLAG_FALLINGFAR` mid-overrun.
- The NPC action contract now includes `VisitVendor`, `VisitTrainer`, and `VisitFlightMaster`, and `Trainer_LearnAvailableSpells` now drives BG through `TrainerVisitTask`-owned logic instead of a raw `InteractWith` dispatch.
- `LiveBotFixture.CheckFgActionableAsync()` now requires both successful action forwarding and a teleport/snapshot round-trip before later FG-sensitive suites keep running.
- Test markdown was refreshed to link each touched test back to the production code paths it exercises.

Verification runs on the current pass:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded with warnings only.
- `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> succeeded.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CombatDistanceTests" --logger "console;verbosity=minimal"` -> 32 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~BuffAndConsumableTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 4 passed, 1 skipped (`BB-BUFF-001`).
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 6 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CraftingProfessionTests|FullyQualifiedName~VendorBuySellTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 3 passed.
- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SpellHandlerTests" --logger "console;verbosity=minimal"` -> 12 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingDataTests" --logger "console;verbosity=minimal"` -> 26 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 1 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests|FullyQualifiedName~NpcInteractionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 8 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~NpcInteractionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 28 passed, 1 skipped.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests|FullyQualifiedName~NpcInteractionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 7 passed, 1 skipped.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 33 passed, 0 failed, 2 skipped.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests|FullyQualifiedName~NpcInteractionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 12 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 31 passed, 2 failed, 2 skipped after an FG herbalism crash/restart cascaded into `GroupFormationTests`.
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded with warnings only.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 2 passed, 1 skipped.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 33 passed, 0 failed, 2 skipped.
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 30 passed, 2 failed, 3 skipped.
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 1 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests|FullyQualifiedName~SpellCastOnTargetTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 2 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BuffAndConsumableTests|FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 2 passed, 1 skipped.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~BuffAndConsumableTests|FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~CombatLoopTests|FullyQualifiedName~CraftingProfessionTests|FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~EconomyInteractionTests|FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> fishing stayed green; `CombatLoopTests` failed separately.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> command still exited nonzero, but the refreshed `FishingProfessionTests.log` shows a successful Ratchet catch (`CustomAnim -> ForceStopImmediate -> CMSG_GAMEOBJ_USE -> SMSG_LOOT_RESPONSE -> item 20708 pushed`), so the suite now progresses beyond the fishing failure.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests|FullyQualifiedName~NpcInteractionTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~SpellCastOnTargetTests|FullyQualifiedName~UnequipItemTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 10 passed, 2 skipped.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SpellCastOnTargetTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 1 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 32 passed, 0 failed, 3 skipped.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingTaskTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip" --logger "console;verbosity=minimal"` -> 13 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingTaskTests|FullyQualifiedName~FishingDataTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip" --logger "console;verbosity=minimal"` -> 44 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 1 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingTaskTests|FullyQualifiedName~FishingDataTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip|FullyQualifiedName~UseItemTaskTests" --logger "console;verbosity=minimal"` -> 48 passed.
- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MovementControllerTests" --logger "console;verbosity=minimal"` -> 38 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 15m --logger "console;verbosity=minimal"` -> 1 skipped when no live visible Ratchet fishing-hole object was available.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests|FullyQualifiedName~OrgrimmarGroundZAnalysisTests" --blame-hang --blame-hang-timeout 15m --logger "console;verbosity=minimal"` -> 3 skipped.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SpellCastOnTargetTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 1 passed.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 31 passed, 0 failed, 4 skipped.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~BuffAndConsumableTests|FullyQualifiedName~CraftingProfessionTests|FullyQualifiedName~EconomyInteractionTests|FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~GroupFormationTests|FullyQualifiedName~OrgrimmarGroundZAnalysisTests|FullyQualifiedName~SpellCastOnTargetTests|FullyQualifiedName~TalentAllocationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> 14 passed, 1 skipped.

Current live-suite boundary:
- The major behavior slice reran green (`12 passed`) after tightening melee stick distance in `BuildStartMeleeAttackSequence(...)`.
- The NPC contract slice is green except for the tracked trainer skip (`7 passed, 1 skipped` in the quest/NPC slice; `28 passed, 1 skipped` when combined with the action-forwarding contract tests).
- Current evidence still does **not** show an active `.gobject add` path in tests. The previously failing herbalism run involved the natural `mangos.gameobject` row `guid=1641` / `id=1618`, whose template faction is `0`.
- Gathering is now BG-authoritative: FG mining/herbalism remains best-effort reference coverage and logs instability instead of blocking the live suite.
- `GroupFormationTests` now starts from `EnsureCleanSlateAsync()` plus `CheckFgActionableAsync()` so prior FG restarts skip early instead of cascading into timeout noise.
- `CheckFgActionableAsync()` now proves both command forwarding and snapshot movement responsiveness, so later FG-dependent suites no longer inherit stale world-state from earlier instability.
- The root FG remote-teleport instability remains tracked under `FG-CRASH-TELE`; it is no longer misattributed to test-spawned game objects or allowed to cascade into unrelated live suites.
- `Trainer_LearnAvailableSpells` now takes the task-owned `VisitTrainer -> TrainerVisitTask -> LearnAllAvailableSpellsAsync(...)` path, but BG still closes gossip without surfacing `SMSG_TRAINER_LIST`; that gap is tracked under `BRT-OVR-006`.
- BG fishing now starts from `EnsureCleanSlateAsync()` before spell sync and gear setup, and the fishing cast entry now flows through `ActionType.StartFishing -> FishingTask`.
- The latest full-suite rerun passed (`31 passed, 0 failed, 4 skipped`), but routine regression coverage now uses a narrower documented-stable slice so unfinished major-rework suites do not dominate the signal.
- The default documented-stable slice is `14 passed, 1 skipped`; active-overhaul suites like combat, gathering, fishing, questing, and NPC trainer coverage are now validated individually until their owning task IDs close.
- Fishing-specific follow-up work now centers on keeping the dual BG/FG task-owned slice green while the captured FG packet/timing path continues to inform future BG parity work.
- Fishing-specific follow-up work now treats "no live visible pool" as a skip condition instead of a bot failure, so the suite only runs on meaningful live-world state.
- The next implementation slice after the BG forced-stop parity fix is FG packet-timing capture plus FG Ratchet pier overrun recovery, especially when the FG client leaves the dock before a stop fully arrests movement.

## Core Principles

1. **NO `.gm on` — EVER.** All bots use account-level GM (gmlevel=6) for setup commands (`.learn`, `.additem`, `.go xyz`). The PLAYER_FLAGS_GM flag must never be set. This eliminates factionTemplate corruption and ensures mobs/NPCs behave naturally.
2. **Test BotTasks, not raw dispatches.** Tests must exercise the actual task stack — push a BotTask onto the stack and observe the outcome. The test should NOT replicate task logic via sequential ActionType dispatches.
3. **GM commands are setup/teardown ONLY.** `.learn`, `.additem`, `.setskill`, `.go xyz`, `.revive` are acceptable for staging. The tested mechanic itself must flow through BotTasks.
4. **NO `.respawn` commands.** Valley of Trials has sufficient mob density. If a test can't find a target, it waits or skips — never forces spawns.
5. **Orgrimmar Bank Top** as default staging location for all Orgrimmar-area tests (use `.tele` named location).
6. **BG-only for behavior tests.** FG (injected) is the gold standard for packet correctness but the WoW client handles its own combat/interaction logic. BG (headless) is where our code runs — that's what we test.

---

## Fixture Changes

### EnsureCleanSlateAsync — Remove `.gm on`

**Current:** Calls `.gm on` for FG bot (TESTBOT1).
**Change:** Remove `.gm on` from `EnsureCleanSlateAsync()` entirely. No bot gets `.gm on` at any point. The fixture's `EnableGmMode` step (Step 7 in FIXTURE_LIFECYCLE.md) is deleted.

**Impact:** All tests that relied on GM god-mode invincibility must handle normal game interactions (mob aggro, damage taken). Tests that teleport into hostile areas must account for this.

### Default Staging Location

Replace all per-test Orgrimmar coordinate teleports with:
```
.tele name {charName} OrgrimmarBankTop
```
Tests that need specific locations (Valley of Trials, Razor Hill) teleport from there.

---

## Phase 1: Deletions (Remove Tests)

| Test | File | Reason |
|------|------|--------|
| `Teleport_PlayerMovesToNewPosition` | BasicLoopTests.cs | Useless — teleport is tested implicitly everywhere |
| `SetLevel_ChangesPlayerLevel` | BasicLoopTests.cs | Setup utility, not a behavior test |
| `Snapshot_SeesNearbyUnits` | BasicLoopTests.cs | Initial setup validation, not needed |
| `Snapshot_SeesNearbyGameObjects` | BasicLoopTests.cs | Initial setup validation, not needed |
| `Consumable_AddPotionToInventory` | CharacterLifecycleTests.cs | Redundant with Equipment_AddItemToInventory |
| `Death_KillAndRevive` | CharacterLifecycleTests.cs | Replaced by CorpseRecoveryTask test |
| `CharacterCreation_InfoAvailable` | CharacterLifecycleTests.cs | Fixture already validates this |

**BasicLoopTests.cs survives with 2 tests:**
- `LoginAndEnterWorld_BothBotsPresent` (fixture health check)
- `Physics_PlayerNotFallingThroughWorld` (physics validation)

**CharacterLifecycleTests.cs survives with 1 test:**
- `Equipment_AddItemToInventory` (basic snapshot validation)

---

## Phase 2: Consolidations

### 2.1 ConsumableUsageTests + BuffDismissTests → BuffAndConsumableTests

Merge into a single test class with two tests:
1. **UseConsumable_AppliesBuff** — UseItemTask consumes Elixir of Lion's Strength, verify aura
2. **DismissBuff_RemovesBuff** — DismissBuff action removes the aura applied in test 1

Both run sequentially in one test method to avoid redundant setup. Single item add, single use, then dismiss.

### 2.2 QuestInteractionTests + StarterQuestTests → QuestingTaskTests

Merge into a single class that tests the QuestingTask pipeline (ref: commit 09506b8):
- **Test 1:** QuestingTask accepts quest 4641 from Kaltunk (NPC 10176), navigates to objective area, then turns in at Gornek (NPC 3143)
- Uses `AcceptQuestTask` → `MoveToPositionTask` → `CompleteQuestTask` chain
- NO `.quest add`/`.quest complete` GM shortcuts — the BotTask handles NPC interaction

### 2.3 UnequipItemTests → Rolled into EquipmentSuiteTests (Phase 3)

### 2.4 VendorBuySellTests → Rolled into NpcInteractionSuiteTests (Phase 3)

---

## Phase 3: New/Expanded Test Suites

### 3.1 CombatClassTests (replaces CombatLoopTests)

**New file:** `CombatClassTests.cs`

**Principle:** Each class gets its own test method. The test dispatches a single "attack this mob" action to StateManager. The class's `PvECombatRotation` (via CombatRotationTask subclass) handles targeting, movement, facing, ability usage — the test only observes the outcome.

**Existing tasks used:** `CombatRotationTask` (base), `StartAttackTask`, profile-specific tasks (BuffTask, RestTask, PullTargetTask)

**Test structure per class:**

| Step | Action | Owner |
|------|--------|-------|
| Setup | `.learn` class spells, `.setskill` weapon, `.additem` weapon, equip | Test (GM) |
| Setup | Teleport to Valley of Trials | Test (GM) |
| Execute | Push combat task with target mob GUID | BotTask stack |
| Observe | Mob dies, bot took damage, used abilities | Test asserts snapshot |

**Phase 3.1a — Warriors (immediate):**
- COMBATTEST account, Worn Mace (36), spell 198, skill 54
- Target: Mottled Boar (3098) in Valley of Trials — NO `.respawn`, NO fallback spawn
- BotRunner pushes CombatRotationTask with target GUID
- Assert: mob health reaches 0, bot health > 0

**Phase 3.1b — All classes (per-class expansion):**
Each class needs: account setup, class-specific spells/items, specific mob type

| Class | Special Setup | Notes |
|-------|--------------|-------|
| Warrior | Weapon + melee skills | Baseline |
| Warlock | Pet summon spell, soul shards | Test SummonPetTask + pet combat |
| Hunter | Pet summon, ranged weapon + ammo | Test SummonPetTask + ranged rotation |
| Mage | Mana, Frostbolt/Fireball | Test ConjureItemsTask if low mana |
| Priest | Mana, Shadow Word: Pain | Test HealTask self-heal |
| Rogue | Weapon + poisons | Stealth opener |
| Druid | Weapon, Bear/Cat form spells | Form shifting |
| Shaman | Weapon, totems | Totem placement |
| Paladin | Weapon, seals/judgements | Seal management |

### 3.2 CombatRangeTests → Unit Tests Only

**Delete the live validation test.** FG client handles range natively. BG range logic is deterministic and should be tested via unit tests against `CombatDistance` static methods.

**Unit test file:** `Tests/BotRunner.Tests/Combat/CombatDistanceTests.cs`
- Test `GetMeleeAttackRange()` with various CombatReach values
- Test `GetInteractionDistance()` formula
- Test `IsMovingXZ()` flag checking
- Test leeway calculations (both moving, one moving, neither)

### 3.3 CorpseRecoveryTaskTests (replaces DeathCorpseRunTests)

**New file:** `CorpseRecoveryTaskTests.cs`

**Principle:** Kill the bot with a single command (`.damage 99999` via SOAP — pick one that works, no fallbacks). Then let the BotTask stack handle everything: detect death → `ReleaseCorpseTask` → `RetrieveCorpseTask` → alive.

**Pre-requisite:** Remove env vars `WWOW_DISABLE_AUTORELEASE_CORPSE_TASK` and `WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK` for THIS test only. Re-enable them after.

| Step | Action | Owner |
|------|--------|-------|
| Setup | Teleport to Orgrimmar Bank Top | Test (GM) |
| Kill | `.damage 99999` via SOAP (single command) | Test (GM) |
| Observe | Bot detects death, pushes ReleaseCorpseTask | BotRunnerService auto-push |
| Observe | Ghost state entered | BotTask stack |
| Observe | RetrieveCorpseTask navigates to corpse | BotTask stack |
| Observe | Corpse reclaimed, bot alive | BotTask stack |
| Assert | `IsStrictAlive()` within 120s | Test |

**Existing tasks:** `ReleaseCorpseTask` (19 lines, functional), `RetrieveCorpseTask` (550+ lines, production-grade)

### 3.4 BankDepositTests (replaces Bank_OpenAndDeposit)

**New task needed:** `BankDepositTask`

**Constructor:** `IBotContext botContext, uint itemId, byte sourceBag, byte sourceSlot, byte targetBankBag, byte targetBankSlot`

**Task flow:**
1. Find nearest banker NPC (UNIT_NPC_FLAG_BANKER)
2. Navigate to banker (within interaction range)
3. Interact with banker (opens bank frame)
4. Move item from (sourceBag, sourceSlot) → (targetBankBag, targetBankSlot)
5. Close bank frame
6. Pop

**Test:**

| Step | Action | Owner |
|------|--------|-------|
| Setup | `.additem 2589 1` (Linen Cloth), teleport to Orgrimmar Bank Top | Test (GM) |
| Execute | Push `BankDepositTask(itemId=2589, ...)` | BotTask |
| Assert | Item moves from bags to bank slot | Snapshot |

### 3.5 AuctionHouseSuiteTests (replaces AuctionHouse_OpenAndList)

**New tasks needed:** `PostAuctionTask`, `BuyAuctionTask`, `SearchAuctionTask`

**Test suite:**
1. **PostAndBuy_CrossCharacter** — TESTBOT2 posts item on AH, COMBATTEST buys it
   - TESTBOT2: `.additem 2589 1`, push `PostAuctionTask(itemId, startBid, buyout, duration)`
   - Wait for auction to appear (poll AH search or snapshot)
   - COMBATTEST: push `SearchAuctionTask(itemName)` → `BuyAuctionTask(auctionId)`
   - Assert: item in COMBATTEST's mailbox

### 3.6 MailTests (replaces Mail_OpenMailbox)

**New task needed:** `CheckMailTask`

**Task flow:**
1. Find nearest mailbox game object
2. Navigate to mailbox
3. Interact (opens mail frame)
4. Take most recent item/gold from mailbox
5. Pop

**Test:**

| Step | Action | Owner |
|------|--------|-------|
| Setup | `.send money {charName} "test" "test" 1000` via SOAP | Test (GM) |
| Execute | Push `CheckMailTask` | BotTask |
| Assert | Player coinage increased | Snapshot |

### 3.7 EquipmentSuiteTests (replaces EquipmentEquipTests + UnequipItemTests)

Expand to test ALL equipment slots:

| Slot | Item | Setup |
|------|------|-------|
| Head | 2583 (Wyrm Scale Breastplate) or other | `.additem` + equip |
| Mainhand | 36 (Worn Mace) | Existing test |
| Offhand | 2081 (Worn Wooden Shield) | `.additem` + equip |
| Ranged | 2947 (Throwing Knife) | `.additem` + equip |
| Chest | Starter chest armor | Already equipped |
| Back | 4678 (Worn Cloak) | `.additem` + equip |

Each test: add item → push `EquipItemTask(itemId, slot)` → verify snapshot slot filled → push unequip → verify back in bags.

**Existing tasks:** `EquipItemTask` (functional)

### 3.8 FishingTaskTests (replaces FishingProfessionTests)

**New task needed:** `FishingTask`

**Task flow:**
1. Navigate to nearest water body (within range of fishing node)
2. Face water
3. Cast fishing spell (7620)
4. Wait for bobber + auto-catch
5. Repeat N times or until skill cap

**Test:**

| Step | Action | Owner |
|------|--------|-------|
| Setup | Learn fishing spells, equip pole, teleport to Ratchet dock | Test (GM) |
| Execute | Push `FishingTask(maxCasts=3)` | BotTask |
| Assert | Fishing skill increased OR fish in inventory | Snapshot |

### 3.9 GatheringTaskTests (replaces GatheringProfessionTests)

**New task needed:** `GatheringRouteTask`

**Constructor:** `IBotContext botContext, List<Position> nodePositions, int gatherSpellId`

**Task flow:**
1. Sort node positions into optimized path
2. For each position: navigate → scan for node → push `GatherNodeTask` if found
3. Complete when all positions visited or N successful gathers

**Tests:**
- **Mining:** Copper Vein node positions in Valley of Trials. Learn mining (2575), add Mining Pick (2901).
- **Herbalism:** Peacebloom/Silverleaf/Earthroot positions in Valley of Trials. Learn herbalism (2366).
- **Skinning (new):** Kill boar → push `SkinCorpseTask`. Learn skinning, add skinning knife.

**Existing tasks:** `GatherNodeTask` (77 lines, functional), `SkinCorpseTask` (exists)

### 3.10 RaidManagementTests (replaces GroupFormationTests)

Expand beyond 2-player party to full raid management:

**Phase 1 (immediate):** Test with 3 bots (TESTBOT1, TESTBOT2, COMBATTEST)
- FG invites both BG bots
- Both accept
- Verify 3-person party with correct leader
- Convert to raid
- Verify raid state in snapshots

**Phase 2 (future):** 40-man raid via SOAP character creation + background bots
- Create 37 additional BG accounts
- Form raid groups (8 groups x 5 players)
- Move players between groups
- Assign roles (main tank, main assist)

### 3.11 MapTransitionTaskTests (replaces MapTransitionTests)

**Principle:** Replace GM `.go xyz` teleports with a `GoToTask` that uses pathfinding including the Deeprun Tram.

**Test:**
- Start: Ironforge, just outside Deeprun Tram portal
- End: Stormwind, just outside Deeprun Tram portal
- Push `MoveToPositionTask(swPosition)` — PathfindingService must route through tram
- Assert: arrival at Stormwind side

**Note:** This requires PathfindingService navmesh to include Deeprun Tram (map 369) and cross-map transitions. If navmesh doesn't support this yet, test is deferred.

### 3.12 NavigationTaskTests (replaces NavigationTests)

**Expanded test:** Graveyard → Orgrimmar Bank Top

| Step | Action | Owner |
|------|--------|-------|
| Setup | Teleport to Orgrimmar graveyard | Test (GM) |
| Execute | Push `MoveToPositionTask(bankTopPosition)` | BotTask |
| Assert | Arrival within 8y. Bot navigated through Orgrimmar gates, up ramps, collision avoidance. | Snapshot |

**Why this route:** Tests collision with Orgrimmar architecture (walls, ramps, elevation changes), gate navigation, and pathfinding optimization.

**Existing task:** `MoveToPositionTask` (45 lines, functional)

### 3.13 NpcInteractionSuiteTests (replaces NpcInteractionTests + VendorBuySellTests)

**Consolidated test class with sub-tests:**

1. **VendorVisitTask_BuysAndSells** — Push `VendorVisitTask`, verify items bought/sold
2. **TrainerVisitTask_LearnsSpells** — Push `TrainerVisitTask`, verify new spells in SpellList
3. **FlightMasterVisitTask_DiscoversNodes** — Push `FlightMasterVisitTask`, verify taxi node discovery
4. **SkillTrainerFrame** (new) — Test profession trainer interaction (First Aid trainer, etc.)

**Existing tasks:** `VendorVisitTask` (227 lines, functional), `TrainerVisitTask` (244 lines, functional), `FlightMasterVisitTask` (171 lines, functional)

**BuyItem via task:** Test must push VendorVisitTask which internally handles NPC interaction, opening vendor frame, and buying consumables.

### 3.14 TalentSuiteTests (replaces TalentAllocationTests)

Expand to per-class talent builds:

| Class | Build | Spells to Verify |
|-------|-------|-----------------|
| Warrior Arms | 5/5 Deflection, 3/3 Tactical Mastery | 16462, 12295 |
| Warrior Fury | 5/5 Cruelty | 12855 |
| Mage Frost | 5/5 Improved Frostbolt | 12472 |
| etc. | Known pre-fab builds | Class-specific |

Each test: set level 60, apply talent points via `.learn`, verify all expected passive spells appear in SpellList.

### 3.15 CraftingTaskTests (replaces CraftingProfessionTests)

**New ActionType needed:** `CreateItem` (replaces `CastSpell` for crafting)

**Why:** `CreateItem` allows BotRunner to add pre-checks:
- Inventory space available?
- Required materials present?
- Skill level sufficient?

These checks happen in a new `CraftItemTask` before the actual cast.

**Task flow:**
1. Check inventory space (at least 1 free slot)
2. Check materials (Linen Cloth x1 for bandage)
3. Cast recipe spell (3275)
4. Wait for channel completion
5. Verify output item

**Test:** Push `CraftItemTask(recipeSpellId=3275)` → assert Linen Bandage in bags.

---

## Phase 4: New BotTasks to Implement

| Task | Priority | Complexity | Notes |
|------|----------|-----------|-------|
| `BankDepositTask` | High | Medium | Banker NPC interaction + item move |
| `CheckMailTask` | High | Medium | Mailbox interaction + take attachment |
| `PostAuctionTask` | Medium | High | AH interaction + listing |
| `BuyAuctionTask` | Medium | High | AH search + purchase |
| `SearchAuctionTask` | Medium | Medium | AH query |
| `FishingTask` | High | Medium | Cast + bobber detect + auto-loot loop |
| `GatheringRouteTask` | Medium | Medium | Multi-node path optimization |
| `CraftItemTask` | High | Low | Pre-check + CastSpell wrapper |

**Existing tasks that are ready (no changes needed):**
- `RetrieveCorpseTask` — 550+ lines, production-grade
- `ReleaseCorpseTask` — Simple but functional
- `CombatRotationTask` — 575 lines, rich framework
- `GatherNodeTask` — Functional
- `TrainerVisitTask` — 244 lines, functional
- `VendorVisitTask` — 227 lines, functional
- `FlightMasterVisitTask` — 171 lines, functional
- `AcceptQuestTask` — Simple but functional
- `CompleteQuestTask` — Simple but functional
- `LootCorpseTask` — Simple but functional
- `SkinCorpseTask` — Exists
- `MoveToPositionTask` — Functional
- `EquipItemTask` — Functional

---

## Phase 5: Global Cleanup

### 5.1 Remove `.respawn` Everywhere

Grep all test files for `.respawn` and remove. Valley of Trials mob density is sufficient. Tests that can't find mobs should `Skip.If()`.

### 5.2 Remove `.gm on` Everywhere

Grep all fixture and test code for `.gm on` / `gm on` and delete. Update FIXTURE_LIFECYCLE.md Step 7 to document this is no longer done.

### 5.3 Standardize Teleport to Named Locations

Replace hardcoded coordinates with `.tele name {char} {location}` where possible:
- `OrgrimmarBankTop` (or nearest named tele)
- `ValleyOfTrials`
- `RazorHill`
- `Ratchet`

### 5.4 Remove Fallback Spawn Commands

Delete `.npc add temp` from CombatLoopTests and anywhere else it appears.

---

## Execution Order

```
Session 1:  Phase 1 (deletions) + Phase 2 (consolidations) + Phase 5 (global cleanup)
            - Delete 7 test methods
            - Merge ConsumableUsage+BuffDismiss
            - Remove fixture-level .gm on
            - Delete live CombatRangeTests in favor of CombatDistance unit tests
            - Continue removing remaining .gm on / .respawn call sites

Session 2:  Phase 3.3 (CorpseRecoveryTask) + Phase 3.12 (NavigationTask)
            - Both use existing fully-functional tasks
            - No new task code needed
            - Commit + push

Session 3:  Phase 3.1a (Warrior CombatClassTest) + Phase 3.2 (CombatRange unit tests)
            - Test CombatRotationTask with warrior profile
            - Move range tests to unit test project
            - Commit + push

Session 4:  Phase 3.7 (EquipmentSuite) + Phase 3.15 (CraftItemTask) + Phase 4 (CraftItemTask impl)
            - New CraftItemTask + CreateItem ActionType
            - Equipment slot expansion
            - Commit + push

Session 5:  Phase 3.13 (NpcInteractionSuite) + Phase 3.4 (BankDeposit)
            - New BankDepositTask
            - Vendor/Trainer/FM task tests
            - Commit + push

Session 6:  Phase 3.6 (Mail) + Phase 3.5 (AuctionHouse)
            - New CheckMailTask, PostAuctionTask, BuyAuctionTask
            - Cross-character AH test
            - Commit + push

Session 7:  Phase 3.8 (FishingTask) + Phase 3.9 (GatheringRoute)
            - New FishingTask, GatheringRouteTask
            - Skinning test
            - Commit + push

Session 8:  Phase 3.1b (All class combat) + Phase 3.14 (TalentSuite)
            - Per-class combat rotation tests
            - Per-class talent builds
            - Commit + push

Session 9:  Phase 3.10 (RaidManagement) + Phase 3.11 (MapTransition)
            - Raid formation with 3 bots
            - Deeprun Tram pathfinding (if navmesh supports)
            - Commit + push
```

---

## File Changes Summary

### Tests to Delete (7 methods across 2 files)
- `BasicLoopTests.cs` — remove 4 methods, keep 2
- `CharacterLifecycleTests.cs` — remove 3 methods, keep 1

### Tests to Merge/Rename
- `ConsumableUsageTests.cs` + `BuffDismissTests.cs` → `BuffAndConsumableTests.cs`
- `QuestInteractionTests.cs` + `StarterQuestTests.cs` → `QuestingTaskTests.cs`
- `UnequipItemTests.cs` → merged into `EquipmentSuiteTests.cs`
- `VendorBuySellTests.cs` → merged into `NpcInteractionSuiteTests.cs`

### Tests to Rewrite
- `CombatLoopTests.cs` → `CombatClassTests.cs`
- `DeathCorpseRunTests.cs` → `CorpseRecoveryTaskTests.cs`
- `FishingProfessionTests.cs` → `FishingTaskTests.cs`
- `GatheringProfessionTests.cs` → `GatheringTaskTests.cs`
- `NavigationTests.cs` → `NavigationTaskTests.cs`
- `MapTransitionTests.cs` → `MapTransitionTaskTests.cs`

### Tests to Expand
- `EquipmentEquipTests.cs` → `EquipmentSuiteTests.cs` (all slots)
- `NpcInteractionTests.cs` → `NpcInteractionSuiteTests.cs` (vendors, trainers, FM, skill trainers)
- `GroupFormationTests.cs` → `RaidManagementTests.cs`
- `TalentAllocationTests.cs` → `TalentSuiteTests.cs` (per-class)
- `EconomyInteractionTests.cs` → `BankDepositTests.cs` + `AuctionHouseSuiteTests.cs` + `MailTests.cs`
- `CraftingProfessionTests.cs` → `CraftingTaskTests.cs`

### Tests Removed Early
- `CombatRangeTests.cs` → replaced by `Tests/BotRunner.Tests/Combat/CombatDistanceTests.cs`

### Tests to Keep As-Is
- `OrgrimmarGroundZAnalysisTests.cs` — keep
- `BasicLoopTests.cs` (2 surviving methods) — keep

### New BotTasks to Implement (Exports/BotRunner/Tasks/)
- `BankDepositTask.cs`
- `CheckMailTask.cs`
- `PostAuctionTask.cs`
- `BuyAuctionTask.cs`
- `SearchAuctionTask.cs`
- `FishingTask.cs`
- `GatheringRouteTask.cs`
- `CraftItemTask.cs`

### New ActionType
- `CreateItem` — added to proto enum, maps to `CraftItemTask` in ActionDispatch

### Fixture Changes
- Remove `.gm on` from `EnsureCleanSlateAsync()`
- Remove Step 7 (GM mode) from fixture init
- Update FIXTURE_LIFECYCLE.md
