---
title: "Recovery — Reconnect (disconnect → SRP6 → realm-list → world-enter → activity resume)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - D:/MaNGOS/source/src/realmd/AuthSocket.cpp
  - D:/MaNGOS/source/src/game/Protocol/WorldSocket.cpp
  - D:/MaNGOS/source/src/game/WorldSession.cpp
  - D:/MaNGOS/source/src/game/WorldSession.h
  - D:/MaNGOS/source/src/game/Handlers/MiscHandler.cpp
  - D:/MaNGOS/source/src/game/Objects/Player.cpp
  - D:/MaNGOS/source/src/game/World.cpp
  - Services/WoWStateManager/StateManagerWorker.BotManagement.cs
  - Exports/BotRunner/WorldEntryHydration.cs
  - Exports/BotRunner/BotRunnerService.cs
  - Exports/WoWSharpClient/Client/AuthClient.cs
  - Exports/WoWSharpClient/Client/WorldClient.cs
  - Tests/WoWSharpClient.NetworkTests/AuthClientTests.cs
  - Tests/WoWSharpClient.NetworkTests/ReconnectPoliciesTests.cs
crawl_date: 2026-05-19
---

# Reconnect — Disconnect → SRP6 Auth → Realm-List → World-Enter → Activity Resume

Foundational recovery cycle. Every other Activity assumes the bot can survive a transient socket close (network blip, world-server restart, kick) and return to the same `AssignedActivity` without operator intervention. Live integration tests rely on this for R8 — a clean cross-test state cannot be re-established without first proving the bot can re-up. The shipped surface is split between StateManager process-supervision (`Services/WoWStateManager/StateManagerWorker.BotManagement.cs` with `MinRelaunchInterval = TimeSpan.FromMinutes(1)` backoff at line 35) and the WoWSharpClient SRP6 + realm-list + world-enter state machine in `Exports/WoWSharpClient/Client/AuthClient.cs` + `WorldClient.cs`. The corresponding `ReconnectTask` is `partial` per [Plan/Activities/recovery.md](../../Plan/Activities/recovery.md) — there is no `ReconnectTask.cs` today; the bot survives a disconnect by being relaunched, not by an in-process reconnect task.

---

## Overview

A disconnect is detected at one of three layers:

1. **Transport** — `WoWSocket::DoRecvIncomingData` (`D:/MaNGOS/source/src/game/Protocol/WorldSocket.cpp:91-94`) calls `CloseSocket()` on any `IO::NetworkError` other than `SocketClosed` against a closing socket. The client-side mirror is `PacketPipeline.WhenDisconnected` (`Exports/WoWSharpClient/Client/AuthClient.cs:146`) which surfaces socket-fault events.
2. **Auth ping timeout** — `_loginHandshakeTimeout` (`AuthClient.cs:231`, default 30 s via `WWOW_AUTH_REALM_LIST_TIMEOUT_SECONDS`; constant declared at `AuthClient.cs:27-29`) raises `OperationCanceledException` when SRP6 stalls.
3. **Server-driven kick** — `WorldSession::LogoutPlayer` (`D:/MaNGOS/source/src/game/WorldSession.cpp:626`) executes server-side `m_playerLogout = true` and runs the same teardown path as a clean logout (death-state preservation, BG flag drop, instance-validity check at line 681); the client sees a `SMSG_LOGOUT_COMPLETE` plus socket close.

The realm-side state machine cycles through five commands declared in the `AuthHandler table` at `D:/MaNGOS/source/src/realmd/AuthSocket.cpp:126-136`:

- `CMD_AUTH_LOGON_CHALLENGE` (opcode `0x00`) — `STATUS_CHALLENGE` (`AuthSocket.cpp:128`)
- `CMD_AUTH_LOGON_PROOF` (SRP6 M1/M2 exchange) — `STATUS_LOGON_PROOF` (`AuthSocket.cpp:129`)
- `CMD_AUTH_RECONNECT_CHALLENGE` (fast-path for session-key re-use) — `STATUS_CHALLENGE` (`AuthSocket.cpp:130`)
- `CMD_AUTH_RECONNECT_PROOF` — `STATUS_RECON_PROOF` (`AuthSocket.cpp:131`)
- `CMD_REALM_LIST` (opcode `0x10`) — `STATUS_AUTHED` (`AuthSocket.cpp:132`)

After realm-list resolves, the bot drives `WorldClient.ConnectAsync` (`Exports/WoWSharpClient/Client/WorldClient.cs:107`) with the SRP-derived session key, then `SendAuthSessionAsync` (`WorldClient.cs:463`), then `SendCharEnumAsync` (`WorldClient.cs:140`), then `SendPlayerLoginAsync(guid)` (`WorldClient.cs:162`). World-entry hydration completes when `Exports/BotRunner/WorldEntryHydration.cs:7` returns `true` — gated on `Guid != 0 AND Position != null AND (InGhostForm OR MaxHealth > 0)`. The `MinRelaunchInterval = 1 min` (StateManagerWorker.BotManagement.cs:35) gates whole-process relaunch as a worst-case fallback when in-process reconnect cannot recover.

---

## Pre-conditions

| Condition | Source citation |
|---|---|
| Client has cached SRP6 session key (allows fast-path `CMD_AUTH_RECONNECT_CHALLENGE` instead of full logon) | `D:/MaNGOS/source/src/realmd/AuthSocket.cpp:130-131` (handler table entries `STATUS_CHALLENGE → STATUS_RECON_PROOF`); WoWSharpClient currently always uses the full SRP6 path (`AuthClient.cs:185` `LoginAsync`) |
| Bot child process is alive OR can be re-spawned within backoff window | `Services/WoWStateManager/StateManagerWorker.BotManagement.cs:35` (`MinRelaunchInterval = TimeSpan.FromMinutes(1)`); `StateManagerWorker.BotManagement.cs:174-179` (skip-launch guard) |
| Realm-list query is not rate-limited (server logs `LOG_LVL_ERROR` "sending CMD_REALM_LIST too frequently") | `D:/MaNGOS/source/src/realmd/AuthSocket.cpp:1012` |
| Account is not banned (`account_banned` table lookup returns null OR ban window expired) | `D:/MaNGOS/source/src/realmd/AuthSocket.cpp:394-409` — ban check, returns `WOW_FAIL_BANNED` (perma) or `WOW_FAIL_SUSPENDED` (temp) |
| Account is not IP-locked OR client IP matches `last_ip` field | `D:/MaNGOS/source/src/realmd/AuthSocket.cpp:353-372` — `IP_LOCK` lockFlag check, returns `WOW_FAIL_SUSPENDED` (line 364) when locked and IP differs |
| World-server reports account `current_realm` is settable (no concurrent session) | `D:/MaNGOS/source/src/game/WorldSession.cpp:728` — `UPDATE account SET current_realm = ?, online = 0` runs on logout; concurrent-session collisions surface as `WOW_FAIL_ALREADY_ONLINE` |
| For in-instance disconnect: `m_instanceValid` still true OR bot can be teleported to homebind | `D:/MaNGOS/source/src/game/WorldSession.cpp:681-687` — `if (!_player->m_instanceValid && !_player->IsGameMaster()) _player->TeleportToHomebind()` |
| For mid-combat disconnect: `CONFIG_BOOL_FORCE_LOGOUT_DELAY = true` (default per `D:/MaNGOS/source/src/game/World.cpp:802`) keeps char in-world for 120 s | `D:/MaNGOS/source/src/game/WorldSession.cpp:387-394` — `SetDisconnectedSession(); m_disconnectTimer = 120000` |

---

## Decision-Engine Rules

| # | Predicate | Action | Threshold / Constant | Source citation |
|---|---|---|---|---|
| R1 | `IObjectManager.IsConnected == false` (target — not yet on the interface; current proxy: WoWSharpClient `PacketPipeline.WhenDisconnected` observable fires) | Push `ReconnectTask` (when shipped) OR escalate to StateManager process-relaunch path | Detection latency = transport-layer socket-close event (sub-second) | `Exports/WoWSharpClient/Client/AuthClient.cs:146` (`WhenDisconnected`); planned task at `Exports/BotRunner/Tasks/Recovery/ReconnectTask.cs` per [Plan/Activities/recovery.md](../../Plan/Activities/recovery.md) §`ReconnectTask` |
| R2 | Disconnect classified as **server-initiated** (`SMSG_LOGOUT_COMPLETE` received before socket close) AND `IsInCombat == false` AND `IsTaxiFlying == false` AND `PLAYER_FLAGS_RESTING` set OR `GetSecurity() >= CONFIG_UINT32_INSTANT_LOGOUT` | Instant logout occurred — server already processed clean teardown. Re-up via full SRP6 + realm-list + char-enum + player-login | Server-side gate at `WorldSession::HandleLogoutRequestOpcode` runs instant path when resting / taxi / GM-tier | `D:/MaNGOS/source/src/game/Handlers/MiscHandler.cpp:328-339` |
| R3 | Disconnect classified as **network-fault** (no `SMSG_LOGOUT_COMPLETE`, raw socket close) AND `CONFIG_BOOL_FORCE_LOGOUT_DELAY == true` | Server holds the WorldSession in `SetDisconnectedSession` state for `m_disconnectTimer = 120 000 ms`. Engine MAY attempt fast-path `CMD_AUTH_RECONNECT_CHALLENGE` within this window (preserves session key + player position); falls back to full SRP6 after 120 s | `m_disconnectTimer = 120000` ms (line `WorldSession.cpp:391`); `CONFIG_BOOL_FORCE_LOGOUT_DELAY` defaults `true` per `World.cpp:802` | `D:/MaNGOS/source/src/game/WorldSession.cpp:387-394`; `D:/MaNGOS/source/src/game/World.cpp:802`; `D:/MaNGOS/source/src/game/WorldSession.h:341` (`IsLogingOut`) |
| R4 | Disconnect classified as **world-restart** (server-side `sWorld.IsStopped() == true` OR all sessions dropped simultaneously) | Wait `MinRelaunchInterval = 1 min` per-account backoff before retrying. Realm-list refresh required (the world server's listener port may have changed) | `MinRelaunchInterval = TimeSpan.FromMinutes(1)`; skip-launch guard logs `LogWarning("Skipping launch ... last attempt {N} ago (< {MinRelaunchInterval})")` | `Services/WoWStateManager/StateManagerWorker.BotManagement.cs:35`; `StateManagerWorker.BotManagement.cs:174-179` |
| R5 | Disconnect classified as **auth-revocation** (logon-challenge returns `WOW_FAIL_BANNED` OR `WOW_FAIL_SUSPENDED` after the previously-successful session) | TERMINAL — do not retry. Pop `ReconnectTask` with reason `LoginFailed` → StateManager flags the bot as `login_failed` cluster per [Plan/Activities/recovery.md](../../Plan/Activities/recovery.md) §"Failure recovery (meta)" | `WOW_FAIL_BANNED` returned for `bandate == unbandate` (perma); `WOW_FAIL_SUSPENDED` returned for `unbandate > UNIX_TIMESTAMP()` (temp) OR IP-lock mismatch | `D:/MaNGOS/source/src/realmd/AuthSocket.cpp:394-409` (ban-table check); `AuthSocket.cpp:353-372` (IP-lock check); `AuthSocket.cpp:364` (`WOW_FAIL_SUSPENDED` for IP-lock); `AuthSocket.cpp:401` (`WOW_FAIL_BANNED` perma) |
| R6 | Auth-server handshake stalls > `_loginHandshakeTimeout` seconds | Throw `OperationCanceledException`, increment `_loginAttempts` counter, retry per the configured `ExponentialBackoffPolicy` (default initial delay 1 s, doubles per attempt, max 10 attempts) | Default `_loginHandshakeTimeout = 30 s` (configurable via `WWOW_AUTH_REALM_LIST_TIMEOUT_SECONDS`); `ExponentialBackoffPolicy(maxAttempts: 10, initialDelay: 1 s, backoffMultiplier: 2.0)` | `Exports/WoWSharpClient/Client/AuthClient.cs:27-29`; `AuthClient.cs:230-244`; `Tests/WoWSharpClient.NetworkTests/ReconnectPoliciesTests.cs:5-126` (policy behavior) |
| R7 | Logon-challenge succeeded (`WOW_SUCCESS = 0x00`) — drive SRP6 proof | Send `CMD_AUTH_LOGON_PROOF` (opcode `0x01`) with computed `M1` digest; await `M2` proof in response. Server side decides at `AuthSocket.cpp:572-804` | `WOW_SUCCESS` constant emitted at `AuthSocket.cpp:420`, `AuthSocket.cpp:972` | `D:/MaNGOS/source/src/realmd/AuthSocket.cpp:175-244` (`GenerateLogonProofResponse`); `AuthSocket.cpp:420` (`WOW_SUCCESS`) |
| R8 | Logon-proof succeeded → request realm list | Send `CMD_REALM_LIST` (opcode `0x10`) within `_realmListTimeout` (default 30 s); on timeout, retry with backoff per R6. Re-query is required after any > 5 min idle gap (server may have rotated realm-server endpoints) | Realm-list rate-limit logs at `AuthSocket.cpp:1012`: "sending CMD_REALM_LIST too frequently. Delay = N seconds" | `Exports/WoWSharpClient/Client/AuthClient.cs:256-283`; `D:/MaNGOS/source/src/realmd/AuthSocket.cpp:1006-1029` |
| R9 | Realm-list response delivered → connect to world server | `WorldClient.ConnectAsync(username, host, sessionKey, port = 8085)` with 10 s connect timeout; on success, send `CMSG_AUTH_SESSION` (in `SendAuthSessionAsync` post-`SMSG_AUTH_CHALLENGE`) | World-connect timeout 10 s (hard-coded at `WorldClient.cs:115`); world-server default port `8085`; server-side handler `WorldSocket::_HandleAuthSession` at `WorldSocket.cpp:223` | `Exports/WoWSharpClient/Client/WorldClient.cs:107-128`; `D:/MaNGOS/source/src/game/Protocol/WorldSocket.cpp:223-382` (auth-session handler) |
| R10 | `SMSG_AUTH_RESPONSE` arrives with non-success code (server rejects world auth) | Classify: `WOW_FAIL_UNKNOWN_ACCOUNT` (line 334), `WOW_FAIL_BANNED` (`WorldSocket.cpp:340`), or IP-mismatch (`WorldSocket.cpp:310`) → terminal `LoginFailed` per R5. Version mismatch (`WorldSocket.cpp:259`) → flag as `client_version_mismatch` cluster, do not retry | Same `WOW_FAIL_*` enum surface as realmd | `D:/MaNGOS/source/src/game/Protocol/WorldSocket.cpp:254-376` (auth-response branches) |
| R11 | World-auth succeeded → drive char-enum + player-login | Send `CMSG_CHAR_ENUM` (`WorldClient.cs:148`); parse `SMSG_CHAR_ENUM` response; for the target character GUID, send `CMSG_PLAYER_LOGIN` (`WorldClient.cs:162`). Then await world-enter handshake (`SMSG_LOGIN_VERIFY_WORLD`, `SMSG_ACCOUNT_DATA_TIMES`, `SMSG_INITIAL_SPELLS`, `SMSG_INITIAL_FACTIONS`) | No client-side timeout on player-login (the server should respond within 1-2 s after instance hydration) | `Exports/WoWSharpClient/Client/WorldClient.cs:140-173`; opcode declarations in `Exports/GameData.Core/Enums/Opcode.cs` |
| R12 | Post-world-enter hydration: poll `WorldEntryHydration.IsReadyForWorldInteraction(Player)` | Returns `true` when `Guid != 0 AND Position != null AND (InGhostForm OR MaxHealth > 0)`. Until `true`, do NOT push any task that reads player state (race against FG memory hydration) | Ghost form is a valid hydrated state for `RetrieveCorpseTask` resume | `Exports/BotRunner/WorldEntryHydration.cs:7-22`; consumers at `Exports/BotRunner/BotRunnerService.cs:393, 780, 879` |
| R13 | Hydration complete + last persisted `AssignedActivity` exists | Restore `BotRunnerService.AssignedActivity` from `StateManagerSettings.AssignedActivity` / `CharacterSettings.AssignedActivity` (persisted JSON snapshot). Resumable Activity types: `Questing`, `Combat`, `Gathering`, `Travel`, `Economy/AH`. RESET-only types: `Dungeoneering`, `Raid`, `BG` (instance-id likely invalidated mid-disconnect — see R14) | Activity-resume contract per [Spec/03_BOTRUNNER.md#configuration](../../Spec/03_BOTRUNNER.md#configuration) | `Exports/BotRunner/BotRunnerService.cs:879` (hydration gate before resume); planned `ReconnectTask` ctor `public ReconnectTask(IBotContext botContext, string lastActivitySnapshotJson)` per [Plan/Activities/recovery.md](../../Plan/Activities/recovery.md) §`ReconnectTask` |
| R14 | Crash-mid-instance: bot was in a dungeon/raid map at disconnect AND `_player->m_instanceValid == false` at server-side LogoutPlayer | Server teleports to homebind (line 683); bot wakes up in capital city — do NOT resume the in-instance Activity. Rejoin group via party-invite acceptance flow (separate Activity); push `LeaveInstanceAndReturn` recovery instead | `if (!_player->m_instanceValid && !_player->IsGameMaster()) _player->TeleportToHomebind()` | `D:/MaNGOS/source/src/game/WorldSession.cpp:681-687` |
| R15 | Combat-mid-disconnect: server keeps char in-world for `m_disconnectTimer = 120 000 ms` (R3) AND `_player->IsInCombat() == true` at disconnect time | If reconnect completes within 120 s, the player resumes in the SAME spatial+combat state. After 120 s the session is terminated and `_player->CombatStop()` runs on the deferred logout path (`WorldSession.cpp:654-658`) — bot wakes up either dead OR fled-to-graveyard. Engine MUST query post-reconnect snapshot rather than trusting pre-disconnect state | `m_disconnectTimer = 120 000 ms`; CombatStop gate at `WorldSession.cpp:654-658`; Spirit-of-Redemption KillPlayer branch at `WorldSession.cpp:659-667` | `D:/MaNGOS/source/src/game/WorldSession.cpp:387-394` (disconnect-timer set); `WorldSession.cpp:654-658` (deferred CombatStop); `D:/MaNGOS/source/src/game/Objects/Player.cpp:1761-1800` (`OnDisconnected` body) |
| R16 | Three consecutive `login_failed` returns from R5/R6/R10 | Flag bot as `login_failed` cluster; do NOT relaunch until cool-down. StateManager surface: `_lastLaunchTimes.TryRemove(accountName, out _)` at `StateManagerWorker.BotManagement.cs:997` + `:1063` (failure clear paths) ensures the next relaunch attempt is gated by the cool-down rather than the per-account backoff | `_loginAttempts` counter; planned ReconnectTask state `ReconnectState.Failed` per [Plan/Activities/recovery.md](../../Plan/Activities/recovery.md) §`ReconnectTask` | `Services/WoWStateManager/StateManagerWorker.BotManagement.cs:997, 1063`; [Plan/Activities/recovery.md](../../Plan/Activities/recovery.md) "Failure recovery (meta)" |

---

## Failure modes

| Failure | Detection | Recovery |
|---|---|---|
| **Realm-list rate-limit** (`AuthSocket.cpp:1006-1029`) | Server log entry `"user %s IP %s is sending CMD_REALM_LIST too frequently. Delay = %d seconds"` AND realm-list TCS times out client-side | Honour the server delay (parse from server response if surfaced, else default 60 s); back off R8 with `ExponentialBackoffPolicy` |
| **Concurrent-session collision** (`WOW_FAIL_ALREADY_ONLINE` from `SMSG_AUTH_RESPONSE`) | World-auth rejected with the already-online code | Wait 60 s (server-side session drop is asynchronous), then retry R9. If still rejected after 3× retries, treat as `login_failed` (R16) — likely another bot or operator session holds the lock |
| **Stale session key during fast-path reconnect** | `CMD_AUTH_RECONNECT_PROOF` returns non-success (server's M1 check failed because cached key drifted) | Drop the cached key, restart at R7 (full `CMD_AUTH_LOGON_CHALLENGE`). Fast-path is purely an optimization — the full SRP6 path always works |
| **World-server unreachable** (post-realm-list, `TimeoutException` from `WorldClient.ConnectAsync`) | 10 s connect timeout (`WorldClient.cs:115`) fires | Treat as R4 world-restart; refresh realm-list (the server's listener may have rotated) before re-attempting world-connect |
| **Hydration never completes** (post-`CMSG_PLAYER_LOGIN`, `IsReadyForWorldInteraction` returns `false` for > 30 s) | `Player.Guid == 0` OR `Position == null` OR (`!InGhostForm && MaxHealth == 0`) after the world-enter handshake completes | FG memory reads may be stale (`WorldEntryHydration.cs:16-18` documents the `InGhostForm` short-circuit). Force a `WoWSharpObjectManager.RefreshFromMemory` cycle; if still no hydration after 60 s, push `Reconnect` again (idempotent; the world server will accept the second `CMSG_PLAYER_LOGIN` if the first session timed out server-side) |
| **Disconnect during AH session** (Slot SRec.8 acceptance per [Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md:216](../../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md) bullet 4) | `WhenDisconnected` fires while `BotRunnerService.AssignedActivity == Economy/AH` | Resume the AH activity post-reconnect per R13. The S1.20 one-hour shake-out test explicitly asserts AH-session-disconnect-during-Economy survives |
| **MinRelaunchInterval skip-loop** (StateManager keeps trying to relaunch a perma-failing bot within the 60 s window) | `LogWarning("Skipping launch for {acct} - last attempt {N} ago (< 00:01:00)")` repeated in StateManager logs | Inspect the bot child-process exit code; if recurring, escalate to `login_failed` cluster (R16) and stop the StateManager-side relaunch loop for that account |

---

## Live-test acceptance

A live integration test exercising this Activity must satisfy R8 (clean cross-test state) by:

1. **Server-up assertion** — `ServerHealthcheck` returns 200 (realmd + world) before the test starts; if not, FAIL FAST (root CLAUDE.md R4). This is the prerequisite for any live test: the canonical "disconnect → re-up" path tested here is what makes server-up assertions meaningful for the rest of the test suite.
2. **Pre-test snapshot predicate** — bot is connected (`Player.Guid != 0 AND IsConnected == true AND WorldEntryHydration.IsReadyForWorldInteraction(Player) == true`) before triggering the disconnect.
3. **Disconnect trigger** — issue `.kick` (server-side disconnect, classification R2/R3 path) OR kill the TCP socket (network-fault, R3 path); poll for `IsConnected == false` with timeout ≤ 2 s.
4. **Backoff observation** — first relaunch attempt within `MinRelaunchInterval = 1 min` window should be skipped (assert StateManager logs the `"Skipping launch ... < 00:01:00"` warning). After 60 s, the next relaunch should proceed.
5. **SRP6 handshake predicate** — poll for `SMSG_AUTH_CHALLENGE` reception, then `WOW_SUCCESS` response on logon-proof, then `CMD_REALM_LIST` response. Each leg ≤ `_loginHandshakeTimeout = 30 s`.
6. **World-enter predicate** — after `CMSG_PLAYER_LOGIN`, poll `WorldEntryHydration.IsReadyForWorldInteraction(Player) == true` with timeout ≤ 30 s.
7. **Activity-resume predicate** — assert `BotRunnerService.AssignedActivity` is restored to the pre-disconnect value (R13). For Questing/Combat/Gathering/Travel: same activity. For Dungeoneering/Raid/BG: assert RESET (R14 path).
8. **Combat-survival predicate** (R15) — if the disconnect was triggered mid-combat, after reconnect within 120 s assert `Player.IsInCombat == true` AND `Player.Position` near pre-disconnect position. If reconnect > 120 s, assert one of: dead/at-graveyard OR fled-to-safety (server-side deferred `CombatStop`).
9. **Auth-revocation fail-fast** — separate test variant injects a `WOW_FAIL_BANNED` response after the first successful session; assert the test reports `LoginFailed` after a single retry attempt rather than burning the full `ExponentialBackoffPolicy` budget. Three consecutive failures must flag `login_failed` cluster per R16.
10. **Disconnect guard** — second-tier disconnect during the reconnect test (e.g., world-server dies mid-handshake) must surface as `LoginFailed` after the 3-attempt budget; do not infinite-loop. Per R8 in root CLAUDE.md, the test fails fast.
11. **Screenshot capture** — per R11 in root CLAUDE.md, capture FG client screenshot at: (a) pre-disconnect, (b) login-screen visible, (c) character-select, (d) world-load-complete. Overwrite `Tests/artifacts/recovery/reconnect/latest-{predisconnect,login,charselect,world}.png`.
12. **Final state dump** — write `Snapshot.player` JSON + last 20 `WoWActivitySnapshot` deltas + last `AssignedActivity` JSON to `Tests/artifacts/recovery/reconnect/snapshot-final.json`.

Existing test anchors: `Tests/WoWSharpClient.NetworkTests/AuthClientTests.cs:17` exercises the `CMD_AUTH_LOGON_CHALLENGE` send and SRP6 entry; `Tests/WoWSharpClient.NetworkTests/ReconnectPoliciesTests.cs:5-200+` exercises `ExponentialBackoffPolicy` + `FixedDelayPolicy` at the transport layer (no task-push coverage). Live `ReconnectTests` is **planned** at `Tests/BotRunner.Tests/LiveValidation/ReconnectTests.cs::Reconnect_AfterMidActivityDisconnect_ResumesAssignedActivity` — `not-started` per [Plan/Activities/recovery.md](../../Plan/Activities/recovery.md) §`ReconnectTask`. Slot SRec.8 (LiveValidation recovery, bullet 4 "Bot disconnect during AH session → assert reconnect + resume") is the canonical acceptance target.

---

## Cross-References

- Plan: [../../Plan/Activities/recovery.md](../../Plan/Activities/recovery.md) — `ReconnectTask` block (current shipped surface = StateManager relaunch loop in `StateManagerWorker.BotManagement.cs` + WoWSharpClient SRP6/realm-list state machine; planned task = `Exports/BotRunner/Tasks/Recovery/ReconnectTask.cs`)
- Plan slots: SRec.3 — `ReconnectTask` (open); SRec.8 bullet 4 — LiveValidation `bot disconnect during AH session`
- Phase-1 acceptance: [../../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md:216](../../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md) — S1.20 one-hour shake-out explicitly asserts mid-Economy disconnect+resume
- Spec: [../../Spec/03_BOTRUNNER.md#catalog-of-task-families](../../Spec/03_BOTRUNNER.md) row "Recovery"; [Spec/03_BOTRUNNER.md#configuration](../../Spec/03_BOTRUNNER.md#configuration) for the `AssignedActivity` persistence contract
- Sibling Activities: [corpse-run.md](./corpse-run.md) — R6 disconnect-guard explicitly excludes `ReconnectTask` from the corpse-run flow (separate Activity, separate test); [stuck-recovery.md](./stuck-recovery.md) — R7 disconnect-guard same exclusion. Both sibling docs explicitly say mid-test disconnects must fail the test rather than paper over via reconnect.
- Decision-Engine state flags: [../decision-engine/state-flags.md](../decision-engine/state-flags.md) — `Snapshot.player.Guid`, `Snapshot.player.Position`, `Snapshot.player.InGhostForm`, `Snapshot.player.MaxHealth`, `Snapshot.connected` (target — wire up under S1.0)
- Decision-Engine priority: [../decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — Reconnect preempts ALL other Activities while `IsConnected == false`; this is the canonical "disconnect → re-up" path on which R8 server-up assertion across the entire live-test suite depends
- Server-protocol references: `Exports/GameData.Core/Enums/Opcode.cs` for `CMSG_AUTH_SESSION`, `CMSG_CHAR_ENUM`, `CMSG_PLAYER_LOGIN`, `SMSG_LOGIN_VERIFY_WORLD`, `SMSG_ACCOUNT_DATA_TIMES`, `SMSG_INITIAL_SPELLS`, `SMSG_INITIAL_FACTIONS` opcode numerics
- Realmd state-machine: `D:/MaNGOS/source/src/realmd/AuthSocket.cpp:126-136` (the canonical `AuthHandler table` cited by every R7/R8 entry above)
