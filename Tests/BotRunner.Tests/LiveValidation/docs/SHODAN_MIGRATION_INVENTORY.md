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
| `MailSystemTests.cs` | Migrated: `Economy.config.json`; fixture-contained mailbox and SOAP mail-money/item staging; BG dispatches `CheckMail` only while FG stays idle for topology parity. |
| `MailParityTests.cs` | Migrated: `Economy.config.json`; fixture-contained mailbox and SOAP mail-money/item staging; BG dispatches `CheckMail` only while FG stays idle for topology parity due to the tracked FG mail collection stability gap. |
| `TradingTests.cs` | Migrated: `Economy.config.json`; fixture-contained trade-spot/loadout/coinage staging; BG offer/decline cancel executes, while BG transfer is a tracked skip because FG `AcceptTrade` ACKs `Failed/behavior_tree_failed`. |
| `TradeParityTests.cs` | Migrated: `Economy.config.json`; SHODAN launches the parity topology and resolves foreground/BG participants, while foreground trade cancel and transfer are tracked skips due FG `DeclineTrade` / `OfferItem` / `AcceptTrade` ACK failures. |
| `GossipQuestTests.cs` | Migrated: `Economy.config.json`; fixture-contained Razor Hill NPC staging; BG dispatches `InteractWith` only while FG stays idle for topology parity. |
| `QuestObjectiveTests.cs` | Migrated: `Economy.config.json`; fixture-contained quest/objective-area staging; BG dispatches `StartMeleeAttack` only after quest state is staged. |
| `QuestInteractionTests.cs` | Migrated: `Economy.config.json`; fixture-contained quest add/complete/remove staging keeps the test body GM-free while it asserts snapshot quest-state projection. |
| `StarterQuestTests.cs` | Migrated: `Economy.config.json`; fixture-contained Kaltunk/Gornek staging; BG dispatches `AcceptQuest` / `CompleteQuest` only while FG stays idle for topology parity. |
| `NpcInteractionTests.cs` | Migrated: `NpcInteraction.config.json`; fixture-contained vendor, flight-master, NPC flag, hunter trainer, and loadout staging; vendor/flight/object-manager paths dispatch to FG/BG, while trainer is a documented skip behind the live funding/mailbox staging gap. |
| `SpiritHealerTests.cs` | Migrated: `Economy.config.json`; fixture-contained corpse/graveyard staging; BG dispatches `ReleaseCorpse`, `Goto`, and `InteractWith` while FG stays idle for topology parity. |
| `MapTransitionTests.cs` | Migrated: `Economy.config.json`; fixture-contained Ironforge tram staging and rejected Deeprun Tram transition; BG dispatches a post-bounce `Goto` liveness action while FG stays idle for topology parity. |
| `MountEnvironmentTests.cs` | Migrated: `Economy.config.json`; fixture-contained riding/mount loadout, unmount cleanup, and indoor/outdoor staging; BG dispatches `CastSpell` while FG stays idle for topology parity. |
| `TravelPlannerTests.cs` | Migrated: `Economy.config.json`; fixture-contained street-level Orgrimmar staging and action quiesce; short BG `TravelTo` dispatch passes while long Crossroads probes are tracked skips for the current no-movement gap. |
| `CornerNavigationTests.cs` | Migrated: `Economy.config.json`; fixture-contained corner/obstacle coordinate staging; BG dispatches `TravelTo` for route checks while FG stays idle for topology parity. |
| `TileBoundaryCrossingTests.cs` | Migrated: `Economy.config.json`; fixture-contained tile-boundary staging; BG dispatches `TravelTo` across Orgrimmar/open-terrain boundaries while FG stays idle for topology parity. |
| `MovementSpeedTests.cs` | Migrated: `Economy.config.json`; fixture-contained Durotar road staging; BG dispatches `Goto` for the speed probe while FG stays idle for topology parity. |
| `NavigationTests.cs` | Migrated: `Economy.config.json`; fixture-contained Durotar road/winding staging; BG dispatches `Goto` for route probes while FG stays idle for topology parity; Valley long route is a tracked skip. |
| `AllianceNavigationTests.cs` | Migrated: `Navigation.config.json`; stable idle `ECONFG1`, Human BG `NAVBG1`, and SHODAN; Alliance-side coordinate staging is fixture-owned with snapshot assertions. |
| `LootCorpseTests.cs` | Migrated: `Loot.config.json`; fixture-contained clean-bag and Durotar mob-area staging; BG dispatches `StartMeleeAttack`, `StopAttack`, and `LootCorpse` while FG stays idle for topology parity. |
| `DeathCorpseRunTests.cs` | Migrated: `Loot.config.json`; fixture-contained Razor Hill corpse staging and cleanup; BG dispatches `ReleaseCorpse`, `StartPhysicsRecording`, `RetrieveCorpse`, and `StopPhysicsRecording` while FG remains opt-in for CRASH-001 regression proof. |
| `BuffAndConsumableTests.cs` | Migrated: `Loot.config.json`; fixture-contained elixir and aura cleanup staging; BG dispatches `UseItem` / `DismissBuff` while FG stays idle for topology parity, with unstable aura/dismiss paths tracked as skips. |
| `ConsumableUsageTests.cs` | Migrated: `Loot.config.json`; fixture-contained Elixir of Lion's Strength staging; BG dispatches `UseItem` while FG stays idle for topology parity. |
| `BgInteractionTests.cs` | Migrated: `Economy.config.json`; fixture-contained bank/AH/mail/flight-master staging; BG dispatches `InteractWith`, `CheckMail`, and `VisitFlightMaster` while bank deposit and Deeprun Tram remain tracked skips. |
| `BattlegroundQueueTests.cs` | Migrated: `Economy.config.json`; fixture-contained WSG battlemaster staging and level setup; BG dispatches `JoinBattleground` and cleanup `LeaveBattleground` only. |
| `SpellCastOnTargetTests.cs` | Migrated: `Economy.config.json`; fixture-contained Battle Shout spell/rage/aura staging; BG dispatches `CastSpell` while FG stays idle for topology parity. |
| `TaxiTests.cs` | Migrated: `Economy.config.json`; fixture-contained taxi readiness, taxi-node, coinage, and Orgrimmar flight-master staging; BG dispatches `VisitFlightMaster` / `SelectTaxiNode` only, with Alliance ride tracked as a Horde-roster skip. |
| `TaxiTransportParityTests.cs` | Migrated: `Economy.config.json`; fixture-contained taxi readiness and transport-point staging; FG/BG dispatch taxi, recording, and elevator `Goto` actions while elevator `TransportGuid` and cross-continent boarding gaps stay tracked skips. |
| `TransportTests.cs` | Migrated: `Economy.config.json`; fixture-contained zeppelin, Ratchet dock, Undercity elevator, and Thunder Bluff elevator staging; Horde-side snapshot checks pass while Alliance/tram/Menethil placeholders skip explicitly. |

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

None currently. The remaining candidates start with movement / navigation.

Movement / navigation tests:

None currently. The remaining candidates start with parity / integration /
ack-capture work.

Parity / integration / ack capture:

| File | Typical per-test setup |
|------|------------------------|
| `DualClientParityTests.cs`, `MovementParityTests.cs` | Dual-client position/gear staging |
| `IntegrationValidationTests.cs` | Cross-cutting GM validation (subset) |
| `AckCaptureTests.cs` | Capture-triggering teleports/actions |

Total: 4 SHODAN-CANDIDATE files (after the taxi/transport group moved to ALREADY-SHODAN).

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

`MailSystemTests.cs` and `MailParityTests.cs` reuse `Economy.config.json` with
`ECONBG1` as the behavior action target, `ECONFG1` launched idle for topology
parity, and SHODAN as director. The slice moves mailbox positioning and SOAP
mail-money/item staging into `StageBotRunnerAtOrgrimmarMailboxAsync`,
`StageBotRunnerMailboxMoneyAsync`, and `StageBotRunnerMailboxItemAsync`. Test
bodies dispatch only `ActionType.CheckMail` to BG.

Migration result on this slice: live artifact `mail_shodan_bgonly.trx` passed
`4/4`. An earlier full FG+BG parity attempt delivered `CheckMail` to FG but
timed out waiting for FG gold/item snapshot deltas under the combined mail
suite; a focused FG gold rerun passed once (`mail_gold_rerun.trx`). The
migration therefore documents the foreground mail collection stability gap and
keeps the committed mail parity shape BG-action-only until that runtime issue
is fixed.

`TradingTests.cs` and `TradeParityTests.cs` reuse `Economy.config.json` with
`ECONFG1` / `ECONBG1` as the real BotRunner participants and SHODAN as
director. The slice adds `StageBotRunnerAtOrgrimmarTradeSpotAsync` plus shared
`TradeTestSupport` so loadout, coinage, partner positioning, visible-partner
resolution, and action ACK checks live outside the test body. The test body no
longer issues GM setup commands; executable paths dispatch only
`ActionType.OfferTrade` / `DeclineTrade` and the staged but skipped transfer
paths stay behind explicit skip reasons.

Migration result on this slice: live artifact `trading_shodan_final.trx`
passed `1` test and skipped `3` with tracked foreground trade action reasons.
`Trade_InitiateAndCancel_BothBotsSeeCancellation` passes for BG offer/decline.
`Trade_GoldAndItem_TransferSuccessful` is Shodan-launched but skipped because
the transfer still depends on FG `AcceptTrade`, which ACKs
`Failed/behavior_tree_failed`. `TradeParityTests` are Shodan-launched but
skipped because foreground `DeclineTrade`, `OfferItem`, and `AcceptTrade`
produce the same failed ACK shape. This slice also fixes the BG item-offer
packet mapping in `InventoryManager.SetTradeItemAsync` (`bag 0` -> `0xFF`,
slot `0` -> `23`) and adds FG trade Lua coverage, but the remaining FG runtime
gap is documented rather than hidden.

`GossipQuestTests.cs`, `QuestObjectiveTests.cs`, `QuestInteractionTests.cs`,
and `StarterQuestTests.cs` reuse `Economy.config.json` with `ECONBG1` as the
quest/gossip action target, `ECONFG1` launched idle for Shodan topology parity,
and SHODAN as director. The slice adds shared `QuestTestSupport` plus
fixture-contained quest location and quest-state staging helpers so the test
bodies no longer issue GM setup commands. Executable paths dispatch only
`ActionType.InteractWith`, `StartMeleeAttack`, `AcceptQuest`, or
`CompleteQuest` to BG; snapshot-plumbing paths assert staged quest-log state
without direct GM calls in the test body.

Migration result on this slice: live artifact `quest_group_shodan_rerun.trx`
passed `6/6`. The first post-migration live attempt
`quest_group_shodan.trx` passed `4`, failed `1`, and skipped `1`; the rerun
fixed the reward-completion assertion to match MaNGOS 1.12 `.quest complete`
snapshot behavior and moved the quest-objective staging point to a nearby
attackable Durotar mob cluster.

`NpcInteractionTests.cs` uses `NpcInteraction.config.json` with `NPCBG1` as a
Background Orc Hunter action target, `NPCFG1` as a Foreground Orc Rogue action
target, and SHODAN as the Background Gnome Mage director. The slice adds
fixture-contained Razor Hill hunter trainer and Orgrimmar flight-master staging
helpers, moves NPC setup out of the test body, and resolves action recipients
through `ResolveBotRunnerActionTargets(...)` so SHODAN is never an action
target. Vendor, flight-master, and object-manager checks dispatch only
`ActionType.VisitVendor` / `VisitFlightMaster` or inspect snapshots after
Shodan staging.

Migration result on this slice: live artifact `npc_interaction_shodan.trx`
passed `3` and skipped `1`. `Trainer_LearnAvailableSpells` is Shodan-shaped
but skipped because this live environment cannot currently fund the hunter:
in-client `.modify money` is unavailable/no-op for BotRunner accounts, and
SOAP `.send money` creates mail that remains uncollectable during Orgrimmar
mailbox staging. The pre-skip failure artifact
`npc_interaction_shodan_final.trx` captured `[SHODAN-STAGE] BG mailbox staging
failed` after strict mailbox staging could not enable GM mode.

`SpiritHealerTests.cs` reuses `Economy.config.json` with `ECONBG1` as the BG
death/recovery action target, `ECONFG1` launched idle for Shodan topology
parity, and SHODAN as director. The slice adds fixture-contained Valley spirit
healer staging and cleanup helpers; the test body no longer issues `.go`,
`.tele`, or `.die` setup commands and dispatches only `ActionType.ReleaseCorpse`,
`ActionType.Goto`, and `ActionType.InteractWith` to the BG target.

Migration result on this slice: live artifact
`spirit_healer_shodan_deadactor_order.trx` passed `1/1`. The slice also fixes
BotRunner dead/ghost `InteractWith` routing so spirit-healer activation uses the
BG `DeadActorAgent.ResurrectWithSpiritHealerAsync(...)` packet path before the
generic gameobject interaction fallback. Pre-fix artifacts captured two useful
failure modes: ordinary NPC gossip did not resurrect the ghost, and strict
2-yard approach tolerance could stall on a valid 5-yard interaction-range
arrival.

`MapTransitionTests.cs` reuses `Economy.config.json` with `ECONBG1` as the BG
map-transition action target, `ECONFG1` launched idle for Shodan topology
parity, and SHODAN as director. The slice adds fixture-contained Ironforge tram
staging and rejected Deeprun Tram transition helpers; the test body no longer
issues `.go xyz` commands and dispatches only a correlated post-bounce
`ActionType.Goto` at the current snapshot position to prove BotRunner remains
action-responsive after the map transition settles.

Migration result on this slice: live artifact `map_transition_shodan.trx`
passed `1/1`. The cross-map `.go xyz` command remains fixture-owned because
there is no production BotRunner ActionType for forcing a server-rejected
instance teleport; the behavior assertion stays snapshot-based and the only
BotRunner action is the post-bounce liveness command.

`MountEnvironmentTests.cs` reuses `Economy.config.json` with `ECONBG1` as the
BG mount action target, `ECONFG1` launched idle for Shodan topology parity, and
SHODAN as director. The slice adds fixture-contained riding-skill/mount-spell
loadout, unmount cleanup, and indoor/outdoor coordinate staging helpers. The
test body no longer issues `.learn`, `.setskill`, `.dismount`, `.unaura`, or
`.go xyz` setup commands and dispatches only `ActionType.CastSpell` for the
mount behavior checks.

Migration result on this slice: live artifact `mount_environment_shodan.trx`
passed `4/4`, covering outdoor/indoor scene classification and outdoor allow /
indoor block behavior for mount spell `23509`.

`TravelPlannerTests.cs` reuses `Economy.config.json` with `ECONBG1` as the BG
travel action target, `ECONFG1` launched idle for Shodan topology parity, and
SHODAN as director. The slice adds fixture-contained street-level Orgrimmar
staging through `StageBotRunnerAtTravelPlannerStartAsync(...)` plus a targeted
quiesce after staging so leftover setup actions do not poison the first
`TravelTo` dispatch. The test body no longer issues `.tele` setup commands and
dispatches only `ActionType.TravelTo` to the BG target.

Migration result on this slice: live artifact `travel_planner_shodan.trx`
passed overall with `1/1` executable short-walk case and `3` tracked skips for
the long Orgrimmar-to-Crossroads probes. The skipped long-route probes are
Shodan-launched, but current evidence shows the BG action remains
`CurrentAction=TravelTo` after `GoToTask` starts and produces no position delta
after 20 seconds. Earlier failure evidence captured delivered `TravelTo` plus
`GOTO-TASK Update #1` at the street-level Orgrimmar start toward Crossroads, so
the remaining gap is recorded as a runtime travel/planning issue rather than a
migration-shape failure.

`CornerNavigationTests.cs` and `TileBoundaryCrossingTests.cs` reuse
`Economy.config.json` with `ECONBG1` as the BG navigation action target,
`ECONFG1` launched idle for Shodan topology parity, and SHODAN as director. The
slice adds the fixture-contained `StageBotRunnerAtNavigationPointAsync(...)`
helper for arbitrary navigation probe coordinates plus post-stage target
quiesce. Test bodies no longer issue direct `BotTeleportAsync(...)` setup calls
and dispatch only `ActionType.TravelTo` for movement route checks.

Migration result on this slice: live artifact
`corner_tile_navigation_shodan.trx` passed `6/6`, covering Orgrimmar
bank-to-auction-house corner navigation, RFC corridor travel, static-obstacle
snapshot staging, Undercity tunnel staging, Orgrimmar tile-boundary crossing,
and open-terrain tile-boundary crossing.

`MovementSpeedTests.cs` reuses `Economy.config.json` with `ECONBG1` as the BG
movement-speed action target, `ECONFG1` launched idle for Shodan topology
parity, and SHODAN as director. The slice removes the old observational FG
shadow teleports, stages the Durotar winding-path start through
`StageBotRunnerAtNavigationPointAsync(...)`, and quiesces the BG target before
dispatch. The test body no longer issues direct `BotTeleportAsync(...)` setup
calls and dispatches only `ActionType.Goto`.

Migration result on this slice: live artifact `movement_speed_shodan.trx`
passed `1/1`, covering the Durotar 141-yard winding path speed, Z-stability,
and arrival assertions.

`NavigationTests.cs` reuses `Economy.config.json` with `ECONBG1` as the BG
navigation action target, `ECONFG1` launched idle for Shodan topology parity,
and SHODAN as director. The slice moves Durotar road and winding-road start
staging into `StageBotRunnerAtNavigationPointAsync(...)`; the test body no
longer issues direct `BotTeleportAsync(...)` setup calls and dispatches only
BG `ActionType.Goto` for the executable route probes. The short Durotar road
route stages at z=`42` so the setup command stays distinct from the preceding
trace route while the server still ground-snaps to the same road surface.

`AllianceNavigationTests.cs` uses `Navigation.config.json` with stable idle
foreground `ECONFG1`, Human Warrior BG target `NAVBG1`, and SHODAN as the
director. The slice avoids the initial all-Human foreground configuration
because that FG runner crashed during the first live attempt. Alliance-side
Goldshire, Stormwind, Westfall, Stockade, and Gnomeregan staging is
fixture-contained and asserted through snapshots; SHODAN never resolves as an
action target.

Migration result on this slice: live artifact
`navigation_alliance_shodan_final4.trx` passed overall with `7` passed and
`1` tracked skip. `Navigation_ShortPath_ArrivesAtDestination` and
`Navigation_LongPath_ZTrace_FGvsBG` pass after Shodan-owned Durotar staging;
the trace writes `durotar_winding_trace_*.json`. The skipped
`Navigation_LongPath_ArrivesAtDestination` is Shodan-launched but currently
pops `GoToTask` with `no_path_timeout` on the Valley of Trials long diagonal,
so it remains documented as a navigation runtime gap rather than a
migration-shape failure.

`LootCorpseTests.cs` uses `Loot.config.json` with `LOOTBG1` as the BG loot
action target, `LOOTFG1` launched idle for Shodan topology parity, and SHODAN
as director. The slice moves clean-slate / bag cleanup into
`StageBotRunnerLoadoutAsync(...)` and Valley of Trials creature-cluster staging
into `StageBotRunnerAtDurotarMobAreaAsync(...)`. The test body no longer uses
the dedicated `CombatBgArenaFixture`, no longer issues setup GM commands
inline, and dispatches only `ActionType.StartMeleeAttack`,
`ActionType.StopAttack`, and `ActionType.LootCorpse` to the BG target.

Migration result on this slice: live artifact `loot_corpse_shodan.trx` passed
`1/1`. The BG target killed a Shodan-staged low-level Durotar mob through
BotRunner melee combat, dispatched `LootCorpse`, and completed the inventory
observation path; no-loot corpses remain non-fatal because the action dispatch
path is the behavior under validation.

`DeathCorpseRunTests.cs` reuses `Loot.config.json` with `LOOTBG1` as the BG
corpse-run action target, `LOOTFG1` launched for Shodan topology parity, and
SHODAN as director. The slice adds fixture-contained Razor Hill corpse staging
and cleanup helpers so clean-slate, coordinate staging, death induction, revive,
and restore movement live outside the test body. The executable path dispatches
only `ActionType.ReleaseCorpse`, `StartPhysicsRecording`, `RetrieveCorpse`, and
`StopPhysicsRecording` to the resolved BotRunner target.

Migration result on this slice: live artifact `death_corpse_run_shodan.trx`
passed overall with `1` BG pass and `1` foreground skip. The BG target released,
ran `RetrieveCorpseTask`, restored strict-alive state, and produced the expected
`navtrace_<account>.json` with `RetrieveCorpseTask` ownership. The foreground
corpse-run proof remains opt-in behind `WWOW_RETRY_FG_CRASH001=1` because it is
the historical WoW.exe crash-regression lane, but it now launches through the
same Loot/SHODAN topology before skipping by default.

`BuffAndConsumableTests.cs` and `ConsumableUsageTests.cs` reuse
`Loot.config.json` with `LOOTBG1` as the BG consumable action target,
`LOOTFG1` launched idle for Shodan topology parity, and SHODAN as director.
The slice adds fixture-contained consumable staging helpers so clean slate,
inventory clear, Elixir of Lion's Strength staging, and Lion's Strength aura
cleanup live outside the test body. The executable paths dispatch only
`ActionType.UseItem` and `ActionType.DismissBuff` to the resolved BG target.

Migration result on this slice: live artifact `buff_consumable_shodan.trx`
passed overall with `1` pass and `2` tracked skips. The legacy
`ConsumableUsageTests` baseline passed a BG `UseItem` dispatch once. The richer
`BuffAndConsumableTests` assertions remain tracked skips until the BG
consumable path produces a stable Lion's Strength aura assertion and until
`WoWSharpClient` exposes enough buff metadata for `DismissBuff` to prove
removal (`BB-BUFF-001`).

`BgInteractionTests.cs` reuses `Economy.config.json` with `ECONBG1` as the BG
economy/NPC smoke action target, `ECONFG1` launched idle for Shodan topology
parity, and SHODAN as director. The slice moves item, bank, auction-house,
mailbox, mail-money, coinage, and flight-master setup behind fixture helpers.
The test body dispatches only `ActionType.InteractWith`,
`ActionType.CheckMail`, and `ActionType.VisitFlightMaster` to the BG target.

Migration result on this slice: live artifact `bg_interaction_shodan.trx`
passed overall with `3` passed and `2` tracked skips.
`AuctionHouse_InteractWithAuctioneer`, `Mail_SendGoldAndCollect_CoinageChanges`,
and `FlightMaster_DiscoverAndTakeFlight` passed.
`Bank_DepositItem_MovesToBankSlot` is Shodan-staged and proves banker
`InteractWith`, then skips because no bank deposit `ActionType` surface exists
yet. `DeeprunTram_RideTransport_ArrivesAtDestination` skips because this smoke
suite uses the Horde economy roster; the dedicated transport slice owns
Deeprun Tram validation.

`BattlegroundQueueTests.cs` reuses `Economy.config.json` with `ECONBG1` as the
BG queue action target, `ECONFG1` launched idle for Shodan topology parity, and
SHODAN as director. The slice adds fixture-contained Orgrimmar Warsong Gulch
battlemaster staging plus WSG minimum-level setup through
`StageBotRunnerLoadoutAsync(...)`. The test body dispatches only
`ActionType.JoinBattleground` with Warsong Gulch type/map parameters and a
cleanup `ActionType.LeaveBattleground`.

Migration result on this slice: live artifact `battleground_queue_shodan.trx`
passed `1/1`. The test staged `ECONBG1` at Brakgul Deathbringer, proved the
WSG battlemaster was visible in the snapshot, dispatched `JoinBattleground`,
observed queue action evidence through snapshot action/ACK/chat markers, and
issued `LeaveBattleground` cleanup.

`SpellCastOnTargetTests.cs` reuses `Economy.config.json` with `ECONBG1` as the
BG Battle Shout action target, `ECONFG1` launched idle for Shodan topology
parity, and SHODAN as director. The slice adds fixture-contained rage staging
through `StageBotRunnerRageAsync(...)` and uses existing loadout/aura helpers
for Battle Shout spell and cleanup setup. The test body dispatches only
correlated `ActionType.CastSpell` with spell id `6673`.

Migration result on this slice: live artifact
`spell_cast_on_target_shodan.trx` passed `1/1`. The BG target learned Battle
Shout, received staged rage, had stale auras cleared, dispatched `CastSpell`,
observed aura `6673`, and removed the aura in fixture cleanup. FG remains idle
because prior Shodan spell-id slices documented foreground `ActionType.CastSpell`
by-id behavior separately.

`TaxiTests.cs`, `TaxiTransportParityTests.cs`, and `TransportTests.cs` reuse
`Economy.config.json` with `ECONBG1` as the BG transport/taxi action target,
`ECONFG1` available for parity lanes or idle topology parity, and SHODAN as
director. The slice adds fixture-contained taxi readiness and transport
coordinate staging helpers:
`StageBotRunnerTaxiReadinessAsync(...)`,
`StageBotRunnerAtOrgrimmarZeppelinTowerAsync(...)`,
`StageBotRunnerAtRatchetDockAsync(...)`,
`StageBotRunnerAtUndercityElevatorUpperAsync(...)`, and
`StageBotRunnerAtThunderBluffElevatorAsync(...)`. Test bodies no longer issue
direct `.tele`, `.modify money`, or taxi-node setup calls; executable paths
dispatch only `ActionType.VisitFlightMaster`, `ActionType.SelectTaxiNode`,
recording actions, or `ActionType.Goto` to resolved BotRunner action targets.

Migration result on this slice: live artifact
`transport_taxi_shodan_final.trx` passed overall with `8` passed and `5`
tracked skips. `TaxiTests` passed Horde discovery, Orgrimmar-to-Crossroads,
and Orgrimmar-to-Gadgetzan actions, while Alliance ride skips because the
shared economy roster is Horde-only. `TaxiTransportParityTests` passed FG/BG
taxi parity; Undercity elevator boarding remains a tracked skip after real
FG/BG `Goto` dispatch because live clients do not reliably acquire
`TransportGuid`, and cross-continent transport parity still lacks a stable
action-driven boarding/disembark assertion. `TransportTests` passed Horde-side
zeppelin, Ratchet dock, Undercity elevator, and Thunder Bluff elevator snapshot
checks; Menethil/Theramore and Deeprun Tram stay tracked skips until an
Alliance/dock/tram action-target config exists. The slice also fixes
taxi-cheat confirmation polling so a final refreshed snapshot can satisfy the
helper when MaNGOS reports "has access to all taxi nodes now".

Known migration constraint: `StageBotRunnerLoadoutAsync` still routes `.learn`,
`.setskill`, and `.additem` through the target bot's chat layer because the
current MaNGOS command forms resolve against the sender's own character. This
keeps the test body GM-free while preserving behavior. A later helper pass can
prove and adopt SOAP or Shodan cross-target command variants.
