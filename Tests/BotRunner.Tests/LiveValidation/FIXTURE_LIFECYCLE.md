# LiveBotFixture Lifecycle

All 25 live validation tests share a single `LiveBotFixture` instance (xUnit collection fixture via `LiveValidationCollection`). Tests run sequentially within the collection. The fixture initializes once before any test and disposes after all tests complete.

## Components

| Component | Role |
|-----------|------|
| **LiveBotFixture** | xUnit `IAsyncLifetime` fixture — orchestrates setup, exposes helpers |
| **BotServiceFixture** | Starts StateManager process, tracks PIDs, handles cleanup |
| **StateManager** | Launches PathfindingService, BackgroundBotRunner(s), ForegroundBotRunner (WoW.exe injection) |
| **PathfindingService** | A* pathfinding on port 5001, launched by StateManager |
| **BackgroundBotRunner** | Headless WoW protocol client (TESTBOT2, COMBATTEST) |
| **ForegroundBotRunner** | DLL-injected into WoW.exe (TESTBOT1) |
| **MaNGOS** | Game server (auth 3724, world 8085, SOAP 7878, MySQL 3306) — assumed already running |

## InitializeAsync — Full Sequence

### Step 0: Environment Variables
Sets env vars to disable automated corpse/release tasks and the combat coordinator during tests:
- `WWOW_DISABLE_AUTORELEASE_CORPSE_TASK=1`
- `WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK=1`
- `WWOW_TEST_DISABLE_COORDINATOR=1`

### Step 1: BotServiceFixture.InitializeAsync()

| # | Action | Component | Details |
|---|--------|-----------|---------|
| 1.1 | Acquire mutex | BotServiceFixture | Machine-wide `Global\WWoW_BotServiceFixture_Mutex` (2min timeout). Prevents concurrent test processes. |
| 1.2 | Kill stale processes | BotServiceFixture | Kills WoWStateManager.exe (from this repo only), ALL orphaned WoW.exe, orphaned PathfindingService. Waits 3-5s for MaNGOS session cleanup. |
| 1.3 | Verify FastCall.dll | BotServiceFixture | Checks Bot output dir for stale version (>20KB = old). Replaces from ForegroundBotRunner/Resources if needed. |
| 1.4 | Check MaNGOS | MangosServerFixture | Verifies auth (3724), world (8085), MySQL (3306) are reachable via TCP. |
| 1.5 | Verify port 8088 free | BotServiceFixture | Waits if still occupied after cleanup. |
| 1.6 | Start StateManager | BotServiceFixture | Launches `WoWStateManager.exe` as child process. Captures stdout for WoW.exe PID tracking. Polls port 8088 up to 90s. |
| 1.7 | StateManager internally | StateManager | Launches PathfindingService (port 5001), reads `StateManagerSettings.json`, launches bots per config (TESTBOT1=FG, TESTBOT2=BG, COMBATTEST=BG). Sets GM level 6 for each account via SOAP. |
| 1.8 | Check PathfindingService | BotServiceFixture | Polls port 5001 up to 30s. Sets `PathfindingServiceReady`. |
| 1.9 | Start crash monitor | BotServiceFixture | Background task polling every 2s — checks if StateManager or tracked WoW.exe PIDs died. |

### Step 2: SOAP & MySQL Setup

| # | Action | Component | Details |
|---|--------|-----------|---------|
| 2.1 | Check SOAP | LiveBotFixture | Verifies TCP to SOAP port 7878. |
| 2.2 | EnsureGmCommandsEnabledAsync | LiveBotFixture (MySQL) | Sets GM level 6 for ADMINISTRATOR, TESTBOT1, TESTBOT2, COMBATTEST in `realmd.account` + `account_access`. Sanitizes stale fixture rows from `mangos.command` table. Sends `.reload command` via SOAP. |
| 2.3 | CleanupZombieGameObjectsAsync | LiveBotFixture (MySQL) | Deletes gameobjects with test ore/herb entries near known test locations (Orgrimmar, Valley of Trials, Durotar coast). |

### Step 3: Connect to StateManager
- Creates `StateManagerTestClient` connecting to `127.0.0.1:8088` (Protobuf socket, 10s timeout).

### Step 4: Wait for Bots to Enter World (up to 120s)

| # | Action | Details |
|---|--------|---------|
| 4.1 | Poll snapshots | Queries `QuerySnapshotsAsync(null)` every 500ms. |
| 4.2 | Track InWorld bots | Records "ever seen InWorld" per account (handles BG snapshot flickering). |
| 4.3 | IdentifyBots() | Account `"COMBATTEST"` -> CombatTestBot. Account ending in `"1"` -> ForegroundBot (TESTBOT1). Others -> BackgroundBot (TESTBOT2). |
| 4.4 | Timeout behavior | If 2+ bots seen: proceed. If only 1 after 60s: log stuck bots, proceed with available. If 0 after 120s: fail. |

### Step 5: Verify SOAP Player Resolution
For each known character (BG, FG, CombatTest):
- Sends `.revive {name}` via SOAP as a harmless probe.
- Polls up to 15s until response doesn't contain "not found".

### Step 6: Clean Character State

| # | Action | Bots | Details |
|---|--------|------|---------|
| 6.1 | EnsureAliveForSetupAsync | BG, FG, Combat | Checks `IsStrictAlive()` (health > 0, no ghost, standState != dead). If not alive, sends SOAP `.revive`, polls up to 12s. |
| 6.2 | EnsureNotGroupedAsync | BG, FG, Combat | If `PartyLeaderGuid != 0`, sends DisbandGroup (if leader) or LeaveGroup action. Up to 3 retries. |

### Step 7: Enable GM Mode (FG only)
- Sends `.gm on` via bot chat to TESTBOT1 (FG) ONLY.
- **BG (TESTBOT2):** Never receives `.gm on` — MaNGOS responds with a packet that disconnects headless clients.
- **COMBATTEST:** Never receives `.gm on` — corrupts factionTemplate, causing mob evade in combat tests.

### Step 8: Stage Bots at Orgrimmar
- SOAP `.tele name {charName} Orgrimmar` for BG and FG bots.
- **COMBATTEST excluded** — rapid back-to-back SOAP teleports disconnect BG clients.
- Waits 1.5s for teleports to settle.

### Step 9: Stabilize (up to 15s)
- `WaitForBotsStabilizedAsync()`: polls snapshots every 1s, requires 2 consecutive polls with all known bots InWorld.
- Updates `AllBots`, calls `IdentifyBots()` with stable data.

### Step 10: Ready
- Sets `IsReady = true`.
- Logs: `"Ready. BG='Lokgaka', FG='N/A', Combat='Shanaka'"` (example).

---

## DisposeAsync — Full Sequence

| # | Action | Component |
|---|--------|-----------|
| 1 | Dispose StateManagerTestClient | LiveBotFixture |
| 2 | Cancel crash monitor | BotServiceFixture |
| 3 | Kill StateManager FIRST | BotServiceFixture (prevents WoW.exe relaunch) |
| 4 | Wait 1s | — |
| 5 | Kill all tracked WoW.exe PIDs | BotServiceFixture (`taskkill /F /PID`, fallback `Process.Kill()`) |
| 6 | Kill orphaned PathfindingService | BotServiceFixture |
| 7 | Kill orphaned BackgroundBotRunner | BotServiceFixture |
| 8 | Orphan check (10s wait) | BotServiceFixture — force-kills any tracked PIDs still alive |
| 9 | Release mutex | BotServiceFixture |
| 10 | Dispose LoggerFactory | LiveBotFixture |

---

## Bot Identification

| Account | EndsWith("1") | Equals("COMBATTEST") | Role | RunnerType | Gets .gm on |
|---------|---------------|----------------------|------|-----------|-------------|
| TESTBOT1 | Yes | No | ForegroundBot | Foreground (DLL injection) | Yes |
| TESTBOT2 | No | No | BackgroundBot | Background (headless) | Never |
| COMBATTEST | No | Yes | CombatTestBot | Background (headless) | Never |

---

## Key Helper Methods

### Snapshot/Query
| Method | What it does |
|--------|-------------|
| `RefreshSnapshotsAsync()` | Queries all snapshots from StateManager, filters InWorld, calls IdentifyBots(), logs chat/error deltas. Retries 3x if InWorld count drops. |
| `GetSnapshotAsync(account)` | Queries snapshot for a specific account. |
| `WaitForSnapshotConditionAsync(account, predicate, timeout, pollMs)` | Generic poll-until-true on snapshot. |

### Action Dispatch
| Method | What it does |
|--------|-------------|
| `SendActionAsync(account, action)` | Forwards ActionMessage to bot via StateManager IPC. |
| `SendActionAndWaitAsync(account, action, delayMs)` | Same + delay after. |

### Bot Chat (.gm commands via in-game chat)
| Method | What it does |
|--------|-------------|
| `SendGmChatCommandAsync(account, cmd)` | Bot types GM command in chat. Dead-state guard blocks when bot is dead. |
| `SendGmChatCommandTrackedAsync(account, cmd, ...)` | Full tracked version: captures baseline/delta chat, duplicate tracking. |
| `BotLearnSpellAsync(account, spellId)` | `.targetself` + `.learn {id}` + snapshot verify. |
| `BotSetSkillAsync(account, skillId, cur, max)` | `.targetself` + `.setskill`. |
| `BotAddItemAsync(account, itemId, count)` | `.additem` + snapshot verify. |
| `BotClearInventoryAsync(account, extraBags)` | Sends DestroyItem for all 16 backpack slots + 4 bag slots. |

### SOAP GM Commands
| Method | What it does |
|--------|-------------|
| `ExecuteGMCommandAsync(cmd)` | SOAP HTTP POST to port 7878. Tracks duplicates, parses XML, throws on faults. |
| `ExecuteGMCommandWithRetryAsync(cmd, retries)` | Retries up to 3x with 1s delay. |

### Teleport
| Method | What it does |
|--------|-------------|
| `BotTeleportAsync(account, map, x, y, z)` | Bot chat `.go xyz` with map, fallback to map-less, polls position within 80y, retries once. |
| `TeleportToNamedAsync(charName, location)` | SOAP `.tele name`. Works for offline characters. |
| `TeleportAndVerifyAsync(...)` | Teleport + poll for position + retry with map-less syntax. |

### Death/Revive
| Method | What it does |
|--------|-------------|
| `InduceDeathForTestAsync(account, charName, ...)` | Tries `.die`, `.kill`, `.damage 5000` via chat; fallback to SOAP. |
| `RevivePlayerAsync(charName)` | SOAP `.revive` with retry. |
| `EnsureStrictAliveAsync(account, label)` | Revive + poll, skip test if fails. |
| `EnsureCleanSlateAsync(account, label)` | Revive if dead, teleport to Orgrimmar safe zone, `.gm on`. |

### State Checks
| Method | What it does |
|--------|-------------|
| `IsStrictAlive(snap)` | health > 0, no ghost flag, standState != dead. |
| `IsDeadOrGhostState(snap)` | Any of health=0, ghost flag, standState=dead. |
| `IsFgActionable` | FG snapshot exists AND strict-alive. |
| `ContainsCommandRejection(text)` | Checks for "no such command", "unknown command", etc. |
