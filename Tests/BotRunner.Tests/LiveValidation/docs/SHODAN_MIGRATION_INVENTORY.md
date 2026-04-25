# Shodan Test-Director Migration Inventory (2026-04-24)

Scope: top-level `Tests/BotRunner.Tests/LiveValidation/*.cs` test classes.
Goal: move per-test GM setup out of FG/BG bot bodies and onto the Shodan
test-director role. FG (TESTBOT1) and BG (TESTBOT2) stay idle until the test
explicitly dispatches a BotRunner action (e.g. `ActionType.StartFishing`).
Shodan is responsible for world/loadout staging and state repair.

Reference implementation: [FishingProfessionTests.cs](../FishingProfessionTests.cs)
with [Fishing.config.json](../../../../../Services/WoWStateManager/Settings/Configs/Fishing.config.json).

## Categories

- **ALREADY-SHODAN** - test already follows the Ratchet pattern. No action.
- **SHODAN-CANDIDATE** - test issues GM setup commands on FG/BG in the test
  body. Migrate setup to a Shodan-directed fixture helper, leave the action
  dispatch and snapshot assertions in the test body.
- **ACTIVITY-OWNED** - the setup is part of a BG / dungeon / raid activity
  that BotRunner itself owns in production. Keep as-is; the activity is what
  we are validating.
- **NO-GM-USAGE** - test has no GM setup on FG/BG. No action.
- **FIXTURE-INFRASTRUCTURE** - partial fixtures, collections, utilities. Not
  migrated individually; helpers they expose are migrated in-place.

Counts reflect the first-pass audit of 70 top-level files under
`LiveValidation/*.cs`. Sub-directories (`Battlegrounds/`, `Dungeons/`,
`Raids/`, `Scenarios/`) are deferred to the activity-owned pass.

## ALREADY-SHODAN (reference)

| File | Notes |
|------|-------|
| `FishingProfessionTests.cs` | Single-launch FG+BG+Shodan; Shodan stages pool; FG/BG dispatch `ActionType.StartFishing` only. |
| `UnequipItemTests.cs` | Migrated pilot: shared `Equipment.config.json`; `StageBotRunnerLoadoutAsync`; test body dispatches `EquipItem` / `UnequipItem` only. |
| `EquipmentEquipTests.cs` | Migrated: `Equipment.config.json` launches `EQUIPFG1`/`EQUIPBG1` warriors + SHODAN; `StageBotRunnerLoadoutAsync`; test body dispatches `EquipItem` only. |
| `WandAttackTests.cs` | Migrated: `Wand.config.json` launches `TRMAF5`/`TRMAB5` mages + SHODAN; `StageBotRunnerLoadoutAsync` for wand loadout; fixture-contained Durotar mob staging; test body dispatches `EquipItem` / `StartWandAttack` / `StopAttack` only. |
| `MageTeleportTests.cs` | Migrated: `MageTeleport.config.json` launches `TRMAF5`/`TRMAB5` mages + SHODAN; `StageBotRunnerLoadoutAsync` learns the city-teleport spell + adds Rune of Teleportation; fixture-contained `StageBotRunnerAtRazorHillAsync`; test body dispatches `CastSpell` only. |

## SHODAN-CANDIDATE (migrate setup to Shodan)

Each of these issues at least one of `BotLearnSpellAsync`,
`BotSetSkillAsync`, `BotAddItemAsync`, `BotTeleportAsync`,
`SendGmChatCommand*`, or raw `.additem/.learn/.tele` to FG/BG in the test
body. The setup is not part of the real BotRunner behavior under test;
it exists only because the test is priming world state.

Profession / loadout tests (migrate first - they resemble the Ratchet flow):

| File | Typical per-test setup | Notes |
|------|------------------------|-------|
| `GatheringProfessionTests.cs` | `.learn mining/herb`, `.additem pick`, Valley stage | Task-owned gathering + GM prep. |
| `CraftingProfessionTests.cs` | Recipe + reagent add, skill set | Crafting task prep. |
| `PetManagementTests.cs` | Pet spells, taming setup | Hunter-only. |

Economy / NPC-interaction tests:

| File | Typical per-test setup |
|------|------------------------|
| `AuctionHouseTests.cs`, `AuctionHouseParityTests.cs` | `.tele` to AH, item add |
| `BankInteractionTests.cs`, `BankParityTests.cs` | `.tele` to bank, item add |
| `VendorBuySellTests.cs` | `.tele` to vendor, gold/item add |
| `EconomyInteractionTests.cs` | Gold, item prep, location stage |
| `MailSystemTests.cs`, `MailParityTests.cs` | `.send items`, `.tele` to mailbox |
| `TradingTests.cs`, `TradeParityTests.cs` | Item add, partner positioning |
| `GossipQuestTests.cs`, `QuestObjectiveTests.cs`, `QuestInteractionTests.cs`, `StarterQuestTests.cs` | `.tele` to NPC, item add |
| `NpcInteractionTests.cs` | `.tele` to NPC, loadout prep |
| `SpiritHealerTests.cs` | `.die` + `.tele` to graveyard |

Movement / navigation tests:

| File | Typical per-test setup |
|------|------------------------|
| `MapTransitionTests.cs` | Inter-map `.go xyz`, loadout prep |
| `MountEnvironmentTests.cs` | Mount spell add, stage teleport |
| `TravelPlannerTests.cs` | Multi-leg staging teleports |
| `CornerNavigationTests.cs`, `TileBoundaryCrossingTests.cs` | Edge-case staging teleports |
| `MovementSpeedTests.cs` | Arena teleport, buff prep |
| `NavigationTests.cs` | Staging teleport + navmesh probe |
| `AllianceNavigationTests.cs` | Alliance-side staging teleport |

Combat / death / buffs / misc:

| File | Typical per-test setup |
|------|------------------------|
| `LootCorpseTests.cs` | `.tele` to boar area, pre-kill stage |
| `DeathCorpseRunTests.cs` | `.damage` + ghost run stage |
| `BuffAndConsumableTests.cs`, `ConsumableUsageTests.cs` | `.additem` consumable, `.unaura` buff reset |
| `BgInteractionTests.cs` | BG UI setup, buff prep |
| `BattlegroundQueueTests.cs` | Queue staging teleport |
| `SpellCastOnTargetTests.cs` | `.learn` spell, target mob staging |
| `TaxiTests.cs`, `TaxiTransportParityTests.cs`, `TransportTests.cs` | `.tele` to taxi/transport, gold add |
| `DualClientParityTests.cs`, `MovementParityTests.cs` | Dual-client position/gear staging |
| `IntegrationValidationTests.cs` | Cross-cutting GM validation (subset) |
| `AckCaptureTests.cs` | Capture-triggering teleports/actions |

Total: ~44 SHODAN-CANDIDATE files (after `MageTeleportTests.cs` moved to ALREADY-SHODAN).

## ACTIVITY-OWNED (keep as-is; part of the activity under test)

These live under `Battlegrounds/`, `Dungeons/`, `Raids/`, `Scenarios/`, or
their supporting fixtures. The loadout / world prep is part of the
production activity (BattlegroundCoordinator, dungeon entry, raid form-up)
and is therefore valid BotRunner behavior. They stay untouched in this
first pass and will be revisited when we rework activity-owned loadout.

- `Battlegrounds/AlteracValleyFixture.cs`, `AlteracValleyObjectiveFixture.cs`,
  `AvObjectiveTests.cs`, `AbObjectiveTests.cs`, `ArathiBasinFixture.cs`,
  `WarsongGulchFixture.cs`, `WsgObjectiveTests.cs`, `WarsongGulchTests.cs`,
  `BattlegroundEntryTests.cs`, `ClassLoadoutSpells.cs`,
  `AlteracValleyLoadoutPlan.cs`, `BgTestHelperTests.cs`,
  `WarsongGulchCollection.cs`, `WarsongGulchObjectiveCollection.cs`.
- `Dungeons/WailingCavernsFixture.cs`, `WailingCavernsTests.cs`,
  `DungeonEntryTests.cs`, `DungeonCollections.cs`, `SummoningStoneTests.cs`.
- `Raids/RaidCoordinationTests.cs`, `RaidEntryTests.cs`, `RaidCollections.cs`.
- `RagefireChasmTests.cs`, `RfcBotFixture.cs`, `RfcValidationCollection.cs`.
- `Scenarios/TestScenario.cs`, `TestScenarioRunner.cs`.
- `CombatArenaFixture.cs`, `CombatBgArenaFixture.cs`, `CombatFgArenaFixture.cs`,
  `CombatBgTests.cs`, `CombatFgTests.cs`.
- `GroupFormationTests.cs`, `RaidFormationTests.cs`, `SummoningTests.cs`.
- `ForegroundNewAccountFlowTests.cs` (activity-owned new-account loadout).

## NO-GM-USAGE (already compliant)

- `BasicLoopTests.cs`, `CharacterLifecycleTests.cs`, `ChannelTests.cs`,
  `CombatLoopTests.cs`, `GuildOperationTests.cs`, `LoadTestMilestoneTests.cs`,
  `ScalabilityTests.cs`, `ScalabilityValidationTests.cs`, `ScaleTest100.cs`,
  `TalentAllocationTests.cs`, `LiveBotFixtureBotChatTests.cs`,
  `LiveBotFixtureDiagnosticsTests.cs`, `LiveBotFixtureIdentityTests.cs`,
  `LiveBotFixtureQuiesceTests.cs`, `CoordinatorFixtureBaseTests.cs`,
  `CoordinatorStrictCountTests.cs`, `DungeonFixtureConfigurationTests.cs`,
  `BattlegroundFixtureConfigurationTests.cs`,
  `BgOnlyBotFixtureConfigurationTests.cs`,
  `FishingPoolActivationAnalyzerTests.cs`,
  `FishingPoolStagePlannerTests.cs`,
  `GatheringRouteSelectionTests.cs`,
  `RatchetFishingStageAttributionTests.cs`,
  `RecordingArtifactHelperTests.cs`,
  `PacketTraceArtifactHelperTests.cs`,
  `PacketSequenceComparatorTests.cs`.

## FIXTURE-INFRASTRUCTURE

These expose GM helpers consumed by tests. Migration of their GM helper
surface happens in-place as tests are migrated. They are not migrated as
a standalone slice.

- `LiveBotFixture.cs`, `LiveBotFixture.Assertions.cs`,
  `LiveBotFixture.BotChat.cs`, `LiveBotFixture.Diagnostics.cs`,
  `LiveBotFixture.GmCommands.cs`, `LiveBotFixture.ServerManagement.cs`,
  `LiveBotFixture.ShodanLoadout.cs`, `LiveBotFixture.Snapshots.cs`.
- Collection markers: `LiveValidationCollection.cs`,
  `BgOnlyValidationCollection.cs`, `SingleBotValidationCollection.cs`.
- `BgOnlyBotFixture.cs`, `CoordinatorFixtureBase.cs`,
  `SingleBotFixture.cs`, `DungeonInstanceFixture.cs`.
- Utilities: `FishingPoolActivationAnalyzer.cs`, `FishingPoolStagePlanner.cs`,
  `GatheringRouteSelection.cs`, `OrgrimmarServiceLocations.cs`,
  `RatchetFishingStageAttribution.cs`, `RecordingArtifactHelper.cs`,
  `PacketTraceArtifactHelper.cs`.

## Migration pattern (to apply per SHODAN-CANDIDATE file)

1. Ensure the test roster includes Shodan alongside FG + BG (reuse
   `Fishing.config.json` shape). Prefer one shared `Equipment.config.json`
   / `Profession.config.json` / etc. per category rather than a file per
   test.
2. Pre-test body: `EnsureSettingsAsync(...)`, then one shared Shodan-director
   helper call (e.g. `await _bot.StageTargetLoadoutAsync(...)`). No
   `.learn` / `.additem` / `.setskill` / `.tele` in the test body.
3. Action: dispatch the `ActionType.*` under test against FG (then BG).
4. Assert: observe via snapshot / task markers. No side-channel GM reads.
5. Restage with Shodan between roles if the action mutates world state
   (as Ratchet does between FG and BG fishing).

## Migration slices

`UnequipItemTests.cs` was the pilot (smallest representative). Its old setup
(`BotLearnSpellAsync` + `BotSetSkillAsync` + `BotAddItemAsync` +
`BotClearInventoryAsync` + `EnsureCleanSlateAsync`) is now represented by
`StageBotRunnerLoadoutAsync`.

`EquipmentEquipTests.cs` now uses `Equipment.config.json`: SHODAN is the
director, while `EQUIPFG1` and `EQUIPBG1` are the only BotRunner action
targets. Both targets receive Worn Mace staging and only `ActionType.EquipItem`
dispatches from the test body.

`WandAttackTests.cs` uses the separate `Wand.config.json` because wand actions
must run on mage characters. SHODAN is the director, while `TRMAF5` and
`TRMAB5` are the only BotRunner action targets. The slice also adds a
fixture-contained `StageBotRunnerAtDurotarMobAreaAsync` helper for the
target-bot `.go xyz` constraint; the test body remains GM-free.

`MageTeleportTests.cs` uses `MageTeleport.config.json` (same
`TRMAF5`/`TRMAB5` mage roster as `Wand.config.json`, but kept as a
distinct file so each Shodan slice is independently revertable). SHODAN
is the director. `TRMAB5` is the only BotRunner action target for
spell-casting tests because `ActionType.CastSpell` resolves to
`_objectManager.CastSpell(int)`, which is a documented no-op on the
Foreground runner (only the `CastSpellByName(string)` Lua overload casts
there). FG is launched for Shodan-topology parity but stays idle in
this slice. The slice adds a fixture-contained
`StageBotRunnerAtRazorHillAsync` helper for the same `.go xyz`
constraint as the wand staging helper, and an optional `levelTo`
parameter on `StageBotRunnerLoadoutAsync` so spell-casting tests can
seed a sufficient level via SOAP `.character level`. The test body
dispatches only `ActionType.CastSpell`.

Migration result on this slice: `MagePortal_PartyTeleported` and
`MageAllCityTeleports` pass. `MageTeleport_Alliance_StormwindArrival`
correctly skips against the Horde-only roster.
`MageTeleport_Horde_OrgrimmarArrival` is a documented pre-existing
failure: MaNGOS returns `SMSG_SPELL_FAILURE` for spell 3567 even after
the bot is leveled to 20 with the Rune of Teleportation reagent staged.
The Shodan/FG/BG shape is correct; the underlying cast rejection
(initially `NO_POWER`, then a short-payload failure after the level bump)
is tracked as a follow-up rather than reverted in this slice.

Known migration constraint: `StageBotRunnerLoadoutAsync` still routes `.learn`,
`.setskill`, and `.additem` through the target bot's chat layer because the
current MaNGOS command forms resolve against the sender's own character. This
keeps the test body GM-free while preserving behavior. A later helper pass can
prove and adopt SOAP or Shodan cross-target command variants.
