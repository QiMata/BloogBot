# QuestInteractionTests

Snapshot-plumbing coverage for quest state changes staged by the Shodan
test director.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONFG1`, `ECONBG1`,
and SHODAN. SHODAN is the director-only staging account, `ECONBG1` is the
quest-state assertion target, and `ECONFG1` stays idle for topology parity.

This suite currently exercises:
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- `Exports/BotRunner/BotRunnerService.Snapshot.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
- `Exports/WoWSharpClient/Client/WorldClient.cs`

## Test Methods

### Quest_AddCompleteAndRemove_AreReflectedInSnapshots

**Bots:** BG action/assertion target (`ECONBG1`) plus idle FG topology
participant (`ECONFG1`) and SHODAN director.

## Test Flow

1. Assert the configured `Economy.config.json` roster matches live
   characters and resolves SHODAN as director-only.
2. Stage `ECONBG1` through `StageBotRunnerLoadoutAsync(..., cleanSlate: true)`.
3. Remove any stale quest `786` with `StageBotRunnerQuestAbsentAsync`.
4. Add quest `786` with `StageBotRunnerQuestAddedAsync` and verify it appears
   in `ActivitySnapshot.Player.QuestLogEntries`.
5. Completion-stage quest `786` with `StageBotRunnerQuestCompletedAsync` and
   assert the quest-log snapshot changes or the quest is removed.
6. Remove quest `786` and verify the quest disappears from the snapshot.

## Runtime Linkage

- Test-owned setup/teardown: none; quest setup is behind fixture helpers.
- Staging path: `LiveBotFixture.TestDirector` quest-state helpers through the
  normal bot chat/action forwarding path for MaNGOS self-targeted `.quest`
  commands.
- Snapshot projection path: `BotRunnerService.Snapshot` populates
  `Player.QuestLogEntries`.
- This is not the final task-driven questing path. It verifies that live quest
  state reaches snapshots correctly so later `AcceptQuestTask` /
  `CompleteQuestTask` work has a reliable observation surface.

## Metrics

The live assertions require:
- quest `786` absent before staging
- quest `786` present after add
- quest state changed or removed after complete
- quest absent after remove

## Current Status

`2026-04-25`: migrated to the Shodan director pattern. Live artifact
`quest_group_shodan_rerun.trx` passed as part of the four-class quest group
run (`6/6` total). The suite remains snapshot-plumbing coverage rather than
the final task-driven questing suite planned in the overhaul.
