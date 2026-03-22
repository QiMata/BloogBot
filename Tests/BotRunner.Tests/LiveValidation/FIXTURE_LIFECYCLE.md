# LiveBotFixture Lifecycle

All live-validation classes in `Tests/BotRunner.Tests/LiveValidation/` share one `LiveBotFixture` instance through `LiveValidationCollection`. Tests run sequentially inside that collection.

## Core Rules

1. No bot receives `.gm on`. Setup relies on account-level GM access only.
2. `EnsureCleanSlateAsync(account, label)` means revive-if-needed plus safe-zone teleport, not GM flag mutation.
3. `WWOW_DISABLE_AUTORELEASE_CORPSE_TASK=1` and `WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK=1` stay enabled for the general live suite. Corpse-recovery coverage must override them explicitly.

## InitializeAsync Sequence

### Step 0: Environment variables

- `WWOW_DISABLE_AUTORELEASE_CORPSE_TASK=1`
- `WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK=1`
- `WWOW_TEST_DISABLE_COORDINATOR=1`

### Step 1: Start infrastructure

`BotServiceFixture.InitializeAsync()`:
- acquires the repo-scoped mutex
- cleans up repo-scoped stale processes
- verifies `FastCall.dll`
- verifies MaNGOS auth/world/MySQL availability
- starts `WoWStateManager`
- waits for `PathfindingService` on port `5001`

### Step 2: Enable command surface

`LiveBotFixture`:
- verifies SOAP on port `7878`
- ensures GM commands are enabled for `ADMINISTRATOR`, `TESTBOT1`, `TESTBOT2`, and `COMBATTEST`
- removes zombie test gameobjects from prior runs

### Step 3: Connect to StateManager

- creates `StateManagerTestClient`
- connects to `127.0.0.1:8088`

### Step 4: Wait for bots to enter world

- polls snapshots every `500ms`
- keeps the most recent `InWorld` snapshot per account
- identifies bots by account:
  - `TESTBOT1` => FG
  - `TESTBOT2` => BG
  - `COMBATTEST` => dedicated BG combat bot

### Step 5: Verify SOAP player resolution

For each known character, the fixture probes `.revive {name}` until SOAP stops returning "player not found".

### Step 6: Clean character state

For BG, FG, and COMBAT when present:
- revive to strict-alive if needed
- leave/disband stale groups if needed

### Step 7: Stage bots at Orgrimmar

- BG is teleported with `TeleportToNamedAsync(characterName, "Orgrimmar")`
- FG is not fixture-teleported with SOAP `.tele name`; FG-sensitive files must own their own `BotTeleportAsync(...)` / `CheckFgActionableAsync()` staging because remote named teleports are still the tracked `FG-CRASH-TELE` trigger
- COMBATTEST is left alone because rapid back-to-back SOAP teleports have disconnected BG clients in prior runs

### Step 8: Stabilize snapshots

- requires two consecutive `InWorld` polls for all known bots
- refreshes `BackgroundBot`, `ForegroundBot`, `CombatTestBot`, and the account/name caches

## DisposeAsync Sequence

1. Dispose `StateManagerTestClient`
2. Dispose `BotServiceFixture`
3. Release logger resources

`BotServiceFixture.DisposeAsync()` is responsible for:
- killing StateManager first
- killing tracked WoW.exe PIDs
- cleaning repo-scoped PathfindingService and BackgroundBotRunner processes
- releasing the fixture mutex

## Key Helpers

### Snapshot/query

- `RefreshSnapshotsAsync()`
- `GetSnapshotAsync(account)`
- `WaitForSnapshotConditionAsync(account, predicate, timeout, pollIntervalMs)`

### Setup and teardown

- `EnsureStrictAliveAsync(account, label)`
- `EnsureCleanSlateAsync(account, label)`
- `RevivePlayerAsync(characterName)`
- `TeleportToNamedAsync(characterName, location)`
- `BotTeleportAsync(account, map, x, y, z)`

### Command paths

- `ExecuteGMCommandAsync(cmd)` for SOAP
- `SendGmChatCommandAsync(account, cmd)` for in-world GM chat
- `SendActionAsync(account, action)` for BotRunner action forwarding

## Bot Identity Table

| Account | Role | RunnerType | `.gm on` |
|---|---|---|---|
| `TESTBOT1` | ForegroundBot | Foreground | Never |
| `TESTBOT2` | BackgroundBot | Background | Never |
| `COMBATTEST` | CombatTestBot | Background | Never |
