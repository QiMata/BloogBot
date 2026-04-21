# DeathCorpseRunTests

BG corpse recovery baseline plus opt-in foreground revalidation for the injected client.

## Bot Execution Mode

**BG default + FG opt-in** - BG always runs. FG runs only when `WWOW_RETRY_FG_CRASH001=1` is set. The historical WoW.exe access violation (`CRASH-001`) was not reproduced on 2026-04-15, and the follow-up opt-in FG corpse-run validation now passes. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

This suite links directly to the production logic it validates:
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
- `Exports/WoWSharpClient/Handlers/DeathHandler.cs`

## Test Methods

### Death_ReleaseAndRetrieve_ResurrectsPlayer

**Bot:** BG default (`TESTBOT2`); FG opt-in (`TESTBOT1`)

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
- on failure, the test appends recent snapshot errors/chats plus recent BotRunner diag lines from `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Diagnostics.cs`

**Current shape under the overhaul plan:**
- BG default, FG opt-in for foreground regression proof
- passing FG opt-in proof exists in `fg_corpse_run_after_corpse_probe_policy.trx`
- no manual second reclaim dispatch
- `RetrieveCorpseTask` owns runback, cooldown, and reclaim after the single action enqueue

## Current boundary

- `RetrieveCorpseTask` mirrors stall/no-path/timeout trace summaries into the BotRunner diag file, and the live test failure text includes those recent diag lines.
- The latest FG opt-in live rerun released and queued `RetrieveCorpseTask`, did not crash WoW.exe, reached best 34y from corpse, restored strict-alive state after 30s, and popped with `AliveAfterRetrieve`.
