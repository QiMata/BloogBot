# GossipQuestTests

Shodan-staged live validation for gossip and quest-giver interaction plumbing.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONFG1`, `ECONBG1`,
and SHODAN. SHODAN is the director-only staging account, `ECONBG1` receives
the BotRunner actions, and `ECONFG1` stays idle for topology parity.

## Test Methods

### Gossip_MultiOption_SelectsCorrectOption

Stages the BG bot at the Razor Hill inn through fixture helpers, locates a
visible gossip NPC by snapshot, and dispatches `ActionType.InteractWith`.

### Quest_Chain_CompletesSequentialQuests

Stages the BG bot near the Razor Hill quest NPC, dispatches
`ActionType.InteractWith`, and records the resulting quest-log snapshot.

### Quest_RewardSelection_PicksBestReward

Uses fixture-contained quest state staging for quest `2161` and validates that
the quest can be added, completion-staged, and removed from the live snapshot
without GM commands in the test body.

## Runtime Linkage

- Settings: `Services/WoWStateManager/Settings/Configs/Economy.config.json`
- Staging: `LiveBotFixture.TestDirector` quest-location and quest-state helpers
- Shared support: `QuestTestSupport`
- Action path: `BotRunnerService.ActionDispatch` to `ActionType.InteractWith`
- Assertion surface: `ActivitySnapshot.Player.QuestLogEntries` and recent chat

## Current Status

`2026-04-25`: migrated to the Shodan director pattern. Live artifact
`quest_group_shodan_rerun.trx` passed as part of the four-class quest group
run (`6/6` total).
