# StarterQuestTests

Shodan-staged BG live baseline for accepting and turning in quest `4641`
through real NPC interaction.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONFG1`, `ECONBG1`,
and SHODAN. SHODAN is the director-only staging account, `ECONBG1` receives
`AcceptQuest` / `CompleteQuest`, and `ECONFG1` stays idle for topology parity.

This suite currently exercises:
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/BotRunnerService.Sequences.NPC.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/QuestNetworkClientComponent.cs`
- `Exports/BotRunner/BotRunnerService.Snapshot.cs`

## Test Method

### Quest_AcceptAndTurnIn_StarterQuest

**Bots:** BG action target (`ECONBG1`) plus idle FG topology participant
(`ECONFG1`) and SHODAN director.

## Test Flow

1. Assert the configured `Economy.config.json` roster matches live
   characters and resolves SHODAN as director-only.
2. Remove stale quest `4641` through the fixture-contained quest-state helper.
3. Clean-slate BG and stage it near Kaltunk through
   `StageBotRunnerAtValleyOfTrialsQuestGiverAsync`.
4. Resolve Kaltunk from the live snapshot and dispatch `ActionType.AcceptQuest`
   with quest `4641`.
5. Poll `QuestLogEntries` until quest `4641` appears.
6. Stage BG near Gornek through
   `StageBotRunnerAtValleyOfTrialsQuestTurnInAsync`.
7. Resolve Gornek and dispatch `ActionType.CompleteQuest` with quest `4641`.
8. Poll `QuestLogEntries` until quest `4641` is gone, then remove it again in
   cleanup and return BG to the Orgrimmar trade staging point.

## Runtime Linkage

- Accept path: `BotRunnerService.ActionDispatch` -> `AcceptQuestSequence` in
  `BotRunnerService.Sequences.NPC.cs` ->
  `WoWSharpObjectManager.AcceptQuestFromNpcAsync(...)`.
- Complete path: `BotRunnerService.ActionDispatch` -> `CompleteQuestSequence`
  in `BotRunnerService.Sequences.NPC.cs` ->
  `WoWSharpObjectManager.CompleteQuestAsync(...)`.
- Snapshot verification path: `BotRunnerService.Snapshot` writes
  `Player.QuestLogEntries`.
- Fixture staging: `LiveBotFixture.TestDirector` quest-location and
  quest-state helpers.
- The suite is closer to the final behavior target than `QuestInteractionTests`,
  but it is still action-driven inside the test. The planned overhaul endpoint
  is task ownership via `AcceptQuestTask` / `CompleteQuestTask`.

## Metrics

The live assertions require:
- Kaltunk and Gornek visible through normal snapshot updates
- `AcceptQuest` dispatch succeeds and quest `4641` appears in the snapshot
- `CompleteQuest` dispatch succeeds and quest `4641` disappears from the
  snapshot

## Current Status

`2026-04-25`: migrated to the Shodan director pattern. Live artifact
`quest_group_shodan_rerun.trx` passed as part of the four-class quest group
run (`6/6` total). The suite remains BG-action-only while FG stays online for
topology parity.
