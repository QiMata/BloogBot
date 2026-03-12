# DeathCorpseRunTests

BG-only corpse recovery baseline for the headless bot.

This suite now links directly to the production logic it is validating:
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
- `Exports/WoWSharpClient/Handlers/DeathHandler.cs`

## Test Methods

### Death_ReleaseAndRetrieve_ResurrectsPlayer

**Bot:** BG only (`TESTBOT2`)

**Purpose:** Assert the live corpse lifecycle that matters to the botrunner:
1. kill transitions into corpse state
2. explicit `ReleaseCorpse` reaches ghost state
3. graveyard relocation actually occurs before retrieval starts
4. one `RetrieveCorpse` dispatch queues `RetrieveCorpseTask`
5. the task reduces corpse distance, waits out reclaim delay, and restores strict-alive state

**Observed metrics:**
- corpse position captured from `InduceDeathForTestAsync()`
- ghost flag observed after release
- graveyard distance exceeds reclaim radius before task dispatch
- best corpse distance improves during runback
- reclaim delay trends down while ghosted
- strict-alive state returns before timeout
- on failure, the test now appends recent snapshot errors/chats plus recent BotRunner diag lines from `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Diagnostics.cs`

**Current shape under the overhaul plan:**
- BG-only
- no FG parity assertion
- no manual second reclaim dispatch
- `RetrieveCorpseTask` owns runback, cooldown, and reclaim after the single action enqueue

## Current boundary

- `RetrieveCorpseTask` now mirrors stall/no-path/timeout trace summaries into the BotRunner diag file, and the live test failure text includes those recent diag lines.
- The latest focused live rerun did not reach the corpse-run assertions because BG entered setup at `health=0/0` and never returned to strict-alive within the fixture revive window, even though SOAP `.revive` reported success.
