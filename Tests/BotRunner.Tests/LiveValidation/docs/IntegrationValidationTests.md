# IntegrationValidationTests

Shodan-directed cross-cutting integration smoke coverage for the remaining V3
validation subset.

## Bot Execution Mode

**Shodan BG-action / tracked skip** - `Economy.config.json` launches `ECONBG1`
as the Background Orc Warrior action target, `ECONFG1` as an idle foreground
topology peer, and SHODAN as the Background Gnome Mage director. SHODAN stages
world, quest, and inventory state through fixture helpers; SHODAN is never
resolved as an action target.

## Test Methods

- `V3_1_EncounterMechanics_StartDungeoneering_SnapshotsUpdate`: stages `ECONBG1`
  at the Ragefire Chasm entrance and dispatches `ActionType.StartDungeoneering`.
- `V3_2_PvPEngagement_TwoBotsAttack_CombatStateReflected`: tracked skip because
  the economy roster is same-faction and the lane also needs fixture-owned PvP
  flag staging before a clean action dispatch is possible.
- `V3_3_EscortQuest_AddQuest_AppearsInQuestLog`: stages the escort quest area
  and quest state behind fixture helpers, then asserts snapshot quest-log
  projection.
- `V3_4_TalentAutoAllocator_LevelUp_TrainTalent_PointSpent`: tracked skip
  because the legacy talent probe is snapshot/progression staging and has no
  production BotRunner action surface yet.
- `V3_5_LevelUpTrainer_VisitTrainer_TrainSkill_NewSpellsLearned`: tracked skip
  behind the existing live trainer funding/staging gap documented by
  `NpcInteractionTests`.
- `V3_6_AuctionPostingService_BuySell_InventoryChanges`: stages Linen Cloth and
  the Razor Hill vendor, then dispatches `ActionType.SellItem`.
- `V3_7_BgRewardCollection_HonorMarks_VisibleInSnapshot`: stages Orgrimmar and
  Warsong mark inventory through fixture helpers, then asserts bag snapshot
  projection.
- `V3_8_MasterLootDistribution_AssignLoot_ActionDispatches`: stages RFC and
  dispatches `ActionType.AssignLoot` to prove action routing.

## Shodan Staging

The test body calls only fixture helpers and BotRunner action dispatches. The
fixture owns:

- `AssertConfiguredCharactersMatchAsync(...)` for the `Economy.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only `ECONBG1`/`ECONFG1` can receive
  actions.
- `StageBotRunnerAtNavigationPointAsync(...)` for RFC, escort quest, and reward
  snapshot positions.
- `StageBotRunnerAtRazorHillVendorAsync(...)` and
  `StageBotRunnerLoadoutAsync(...)` for vendor/reward inventory staging.
- `StageBotRunnerQuestAbsentAsync(...)` and `StageBotRunnerQuestAddedAsync(...)`
  for quest snapshot-state setup.

## Validation

- Setup grep on `IntegrationValidationTests.cs` -> no inline FG/BG setup command
  matches.
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> passed with existing warnings.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `integration_validation_shodan.trx` -> passed overall with `5` passed and `3`
  tracked skips.
