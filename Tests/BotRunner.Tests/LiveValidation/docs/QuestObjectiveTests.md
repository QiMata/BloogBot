# QuestObjectiveTests

Shodan-staged live validation that a quest objective setup can drive a real
BotRunner combat action and surface quest-log state through snapshots.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONFG1`, `ECONBG1`,
and SHODAN. SHODAN is the director-only staging account, `ECONBG1` receives
the BotRunner action, and `ECONFG1` stays idle for topology parity.

## Test Method

### Quest_KillObjective_CountIncrementsAndCompletes

1. Stage `ECONBG1` at the Durotar quest-objective area through the fixture.
2. Remove any stale quest `790`, then add quest `790` through the
   fixture-contained quest-state helper.
3. Resolve a nearby attackable unit from the live snapshot.
4. Dispatch `ActionType.StartMeleeAttack` to BG with the target GUID.
5. Log the post-combat quest entries and remove quest `790` in cleanup.

## Runtime Linkage

- Settings: `Services/WoWStateManager/Settings/Configs/Economy.config.json`
- Staging: `StageBotRunnerAtDurotarQuestObjectiveAreaAsync`,
  `StageBotRunnerQuestAbsentAsync`, and `StageBotRunnerQuestAddedAsync`
- Shared support: `QuestTestSupport`
- Action path: `BotRunnerService.ActionDispatch` to `StartMeleeAttack`
- Assertion surface: `ActivitySnapshot.Player.QuestLogEntries` and nearby units

## Current Status

`2026-04-25`: migrated to the Shodan director pattern. Live artifact
`quest_group_shodan_rerun.trx` passed as part of the four-class quest group
run (`6/6` total). The final staging point is a Durotar mob cluster near
`(-620, -4385, 44)` so the target lookup finds an attackable unit reliably.
