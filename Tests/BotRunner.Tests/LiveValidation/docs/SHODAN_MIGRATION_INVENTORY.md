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
| `GatheringProfessionTests.cs` | Migrated: `Gathering.config.json` launches `GATHFG1`/`GATHBG1` warriors + SHODAN; fixture-contained loadout, pool refresh, and route staging; test body dispatches `StartGatheringRoute` only. |
| `CraftingProfessionTests.cs` | Migrated: `Crafting.config.json` launches `CRAFTFG1`/`CRAFTBG1` warriors + SHODAN; `StageBotRunnerLoadoutAsync` stages First Aid recipe/skill/reagent; test body dispatches BG `CastSpell` only. |
| `PetManagementTests.cs` | Migrated: `PetManagement.config.json` launches idle `PETFG1`, hunter `PETBG1`, and SHODAN; `StageBotRunnerLoadoutAsync` stages hunter level/pet spells; test body dispatches BG `CastSpell` only. |
| `AuctionHouseTests.cs` | Migrated: `Economy.config.json` launches `ECONFG1`/`ECONBG1` warriors + SHODAN; fixture-contained AH staging; test body dispatches `InteractWith` only. |
| `AuctionHouseParityTests.cs` | Migrated: `Economy.config.json`; AH search parity stages FG/BG at auctioneer, while post/buy and cancel are explicit missing-action skips after Shodan staging. |
| `BankInteractionTests.cs` | Migrated: `Economy.config.json`; fixture-contained Orgrimmar bank staging; banker detection and `InteractWith` dispatch are Shodan-shaped, while deposit/withdraw is an explicit missing-action skip. |
| `BankParityTests.cs` | Migrated: `Economy.config.json`; FG/BG bank staging and item setup are Shodan-shaped, while deposit/withdraw and bank-slot purchase are explicit missing-action skips. |
| `VendorBuySellTests.cs` | Migrated: `Economy.config.json`; fixture-contained Razor Hill vendor and coinage staging; BG dispatches `BuyItem` / `SellItem` only while FG stays idle for topology parity. |
| `EconomyInteractionTests.cs` | Migrated: `Economy.config.json`; fixture-contained bank/AH/mailbox/mail-money staging; FG/BG dispatch only `InteractWith` or `CheckMail`. |

## SHODAN-CANDIDATE (migrate setup to Shodan)

Each of these issues at least one of `BotLearnSpellAsync`,
`BotSetSkillAsync`, `BotAddItemAsync`, `BotTeleportAsync`,
`SendGmChatCommand*`, or raw `.additem/.learn/.tele` to FG/BG in the test
body. The setup is not part of the real BotRunner behavior under test;
it exists only because the test is priming world state.

Profession / loadout tests (migrate first - they resemble the Ratchet flow):

None currently. The remaining candidates start with the economy /
NPC-interaction group.

Economy / NPC-interaction tests:

| File | Typical per-test setup |
|------|------------------------|
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

Total: ~35 SHODAN-CANDIDATE files (after `EconomyInteractionTests.cs` moved to ALREADY-SHODAN).

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

`GatheringProfessionTests.cs` uses `Gathering.config.json` with
`GATHFG1`/`GATHBG1` Orc Warrior action targets and SHODAN as the
Background Gnome Mage director. The slice moves profession spell/skill/item
staging into `StageBotRunnerLoadoutAsync`, route staging into
`StageBotRunnerAtValleyCopperRouteStartAsync` /
`StageBotRunnerAtDurotarHerbRouteStartAsync`, and pool refresh/selection
into `RefreshAndPrioritizeGatheringPoolsWithShodanAsync`. The test body
dispatches only `ActionType.StartGatheringRoute`.

Migration result on this slice: `Mining_BG_GatherCopperVein` and
`Herbalism_BG_GatherHerb` pass. `Herbalism_FG_GatherHerb` skipped because
the foreground bot was no longer actionable after the preceding foreground
mining failure. `Mining_FG_GatherCopperVein` remains a documented functional
gap: the FG action target is level 20, has Mining, has a Mining Pick, receives
the `StartGatheringRoute` action, and moves around active copper candidates,
but no gather success, skill delta, or bag delta is observed before timeout.
The slice also corrects the Valley copper route center from
`(-800,-4500,31)` to `(-1000,-4500,28.5)` after native `GetGroundZ` proved
the old point sits on a high terrain layer.

`CraftingProfessionTests.cs` uses `Crafting.config.json` with `CRAFTFG1` and
`CRAFTBG1` Orc Warrior action targets and SHODAN as the Background Gnome Mage
director. The slice moves First Aid Apprentice (`3273`), Linen Bandage recipe
(`3275`), First Aid skill `129=1/75`, and Linen Cloth (`2589`) setup into
`StageBotRunnerLoadoutAsync`. The test body dispatches only
`ActionType.CastSpell` to `CRAFTBG1`; `CRAFTFG1` stays idle because
foreground spell-id casting is not the validated crafting path.

Migration result on this slice: `FirstAid_LearnAndCraft_ProducesLinenBandage`
passes. The live artifact `crafting_shodan.trx` shows the Shodan topology, BG
loadout staging, and the single BG craft action producing Linen Bandage `1251`.

`PetManagementTests.cs` uses `PetManagement.config.json` with `PETBG1` as the
Background Orc Hunter action target, `PETFG1` as an idle Foreground Orc Rogue
topology participant, and SHODAN as the Background Gnome Mage director. The
foreground account is intentionally class-matched to the existing live
character because this slice validates BG spell-id pet management; the action
requirement is carried by the BG hunter. The slice moves hunter level `10`,
Call Pet (`883`), Dismiss Pet (`2641`), and Tame Animal (`1515`) setup into
`StageBotRunnerLoadoutAsync`. The test body dispatches only
`ActionType.CastSpell` to `PETBG1` for Call Pet and Dismiss Pet.

Migration result on this slice: `Pet_SummonAndManage_StanceFeedAbility`
passes. The live artifact `pet_management_shodan.trx` shows Shodan topology,
BG hunter staging, and the two BG pet-management casts returning success.

`AuctionHouseTests.cs` and `AuctionHouseParityTests.cs` use
`Economy.config.json` with `ECONFG1` / `ECONBG1` Orc Warrior action targets
and SHODAN as the Background Gnome Mage director. The slice adds
`StageBotRunnerAtOrgrimmarAuctionHouseAsync` so AH coordinate staging lives in
the fixture. `AuctionHouseTests` dispatches only `ActionType.InteractWith`
against detected auctioneer GUIDs. `AuctionHouseParityTests` verifies FG/BG
auctioneer staging/detection; post/buy and cancel remain explicit skips after
Shodan setup because no auction post/buy/cancel `ActionType` surface exists
yet.

Migration result on this slice: live artifact `auction_house_shodan.trx`
passed `3` tests and skipped `2` with tracked missing-action reasons.

`BankInteractionTests.cs` and `BankParityTests.cs` reuse
`Economy.config.json` with `ECONFG1` / `ECONBG1` Orc Warrior action targets
and SHODAN as the Background Gnome Mage director. The slice adds
`StageBotRunnerAtOrgrimmarBankAsync` so bank coordinate staging lives in the
fixture. `BankInteractionTests` now validates FG/BG banker detection and
dispatches only `ActionType.InteractWith` against detected banker GUIDs.
`BankParityTests` stages Linen Cloth through `StageBotRunnerLoadoutAsync` and
verifies FG/BG bank/item setup before skipping the unimplemented action
surfaces.

Migration result on this slice: live artifact `bank_shodan.trx` passed `1`
test and skipped `3` with tracked missing-action reasons. Deposit/withdraw
and bank-slot purchase still need BotRunner `ActionType` support before those
assertions can become behavioral proofs.

`VendorBuySellTests.cs` reuses `Economy.config.json` with `ECONBG1` as the BG
packet action target, `ECONFG1` launched idle for Shodan topology parity, and
SHODAN as the Background Gnome Mage director. The slice adds
`StageBotRunnerAtRazorHillVendorAsync` and `StageBotRunnerCoinageAsync` so
vendor location, item, and money setup live behind fixture helpers. The test
body dispatches only `ActionType.BuyItem`, `ActionType.SellItem`, and the
post-buy cleanup `ActionType.DestroyItem`.

Migration result on this slice: live artifact `vendor_buy_sell_shodan.trx`
passed `2/2`. This remains a BG packet baseline by design; FG vendor buy/sell
coverage is left to a future parity-specific slice.

`EconomyInteractionTests.cs` reuses `Economy.config.json` with `ECONFG1` and
`ECONBG1` action targets plus SHODAN as director. The slice moves bank, AH,
mailbox, and SOAP mail-money setup into fixture helpers
(`StageBotRunnerAtOrgrimmarBankAsync`,
`StageBotRunnerAtOrgrimmarAuctionHouseAsync`,
`StageBotRunnerAtOrgrimmarMailboxAsync`, and
`StageBotRunnerMailboxMoneyAsync`). The test body dispatches only
`ActionType.InteractWith` for banker/auctioneer and `ActionType.CheckMail` for
mailbox collection.

Migration result on this slice: live artifact `economy_interaction_shodan.trx`
passed `3/3` across FG and BG.

Known migration constraint: `StageBotRunnerLoadoutAsync` still routes `.learn`,
`.setskill`, and `.additem` through the target bot's chat layer because the
current MaNGOS command forms resolve against the sender's own character. This
keeps the test body GM-free while preserving behavior. A later helper pass can
prove and adopt SOAP or Shodan cross-target command variants.
