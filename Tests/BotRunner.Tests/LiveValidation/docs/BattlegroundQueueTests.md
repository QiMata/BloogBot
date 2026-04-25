# BattlegroundQueueTests

Shodan-directed Warsong Gulch queue smoke coverage.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONBG1` as the BG
action target, `ECONFG1` idle for topology parity, and SHODAN as the director.
SHODAN performs setup through fixture helpers; only `ECONBG1` receives
BotRunner battleground actions.

## Active Test

### BG_QueueForWSG_ReceivesQueuedStatus

**Purpose:** Prove the BG `ActionType.JoinBattleground` dispatch path can
queue from the Orgrimmar Warsong Gulch battlemaster after Shodan-owned staging.

**Setup path:**
- `StageBotRunnerLoadoutAsync(...)` sets the BG target to the Warsong Gulch
  minimum level.
- `StageBotRunnerAtOrgrimmarWarsongBattlemasterAsync(...)` stages the BG target
  near Brakgul Deathbringer.
- `QuiesceAccountsAsync(...)` drains setup actions before queue dispatch.

**Action path:**
- `ActionType.JoinBattleground` with Warsong Gulch type id `2` and expected map
  id `489`.
- `ActionType.LeaveBattleground` is sent as cleanup after the queue evidence is
  captured.

**Assertion surface:**
- Snapshot ObjectManager validity.
- Visible Warsong Gulch battlemaster entry `3890`.
- `CurrentAction`, `PreviousAction`, command ACKs, or recent battleground chat
  markers proving the queue action was accepted by the BotRunner path.

**Current live result:**
- `battleground_queue_shodan.trx` passed `1/1`.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed.
- Setup grep -> no inline GM setup calls.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `battleground_queue_shodan.trx` -> passed `1/1`.
