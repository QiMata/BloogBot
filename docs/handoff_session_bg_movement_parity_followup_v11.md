# Handoff (followup v11): BG bot teleport double-fall - Stream 2E.3 closed; next is Stream 4 BG cross-map baseline

> **How to use this:** copy/paste this file top to bottom into a fresh
> coding session in `e:\repos\Westworld of Warcraft`. Read it in full before
> touching anything. Older context lives in
> `handoff_session_bg_movement_parity.md` and followups v1 through v10.

---

## Why this work exists (restated)

A third-party WoW client observing a BackgroundBotRunner (BG) bot after a
teleport reported the falling animation playing twice: once from local
prediction immediately after teleport, then again when authoritative state
arrived while the bot was still airborne.

Binary parity is the authority. WoW.exe (FG) emits `MSG_MOVE_FALL_LAND` after
a Durotar same-map vertical-drop teleport; BG must match that packet shape
without timing hacks, ACK gates tied to local physics state, or synthetic
server-state shortcuts.

**Current parity state (post-v11):**

- Streams 2A through 2D and Stream 3 are closed.
- Stream 2E.1 is closed: same-map BG teleport no longer emits spurious
  `CMSG_SET_ACTIVE_MOVER`.
- Stream 2E.2 is superseded: the old 100y "no FALL_LAND" fixture was a
  current-bug oracle, not the desired state.
- **Stream 2E.3 is closed:** live BG now emits `MSG_MOVE_FALL_LAND` for the
  standard 10y drop and for a 100y high drop when the recorder window is long
  enough.
- Stream 4 is still partial: FG cross-map baseline is pinned; BG cross-map
  baseline is still TODO.

---

## Done since v10

Recent commits on `main`:

1. `6942e624` - `diag(bg-movement-parity): log post-teleport physics snap frames`
   - Added first-frame diagnostic logging in
     `Exports/WoWSharpClient/Movement/MovementController.cs` around
     `RunPhysics`, `ApplyPhysicsResult`, and the `_needsGroundSnap` block.
   - The intended `Information` lines did not land in
     `Bot/Release/net8.0/logs/botrunner_ECONBG1.diag.log` because the live
     worker inherited warning-level file logging. Warning diagnostics in
     `Bot/Release/net8.0/WWoWLogs/bg_ECONBG120260428.log` were still enough.

2. `f7c1337a` - `fix(bg-movement-parity): prime airborne teleports as falling`
   - Root cause: pre-fix live BG completed the post-teleport ground snap in
     one frame with `moveFlags=0x0` at the teleport Z. The controller never
     observed a `FALLINGFAR -> grounded` transition, so it could not emit
     `MSG_MOVE_FALL_LAND`.
   - Fix shape: on the first post-reset ground-snap frame, probe downward
     with `NativeLocalPhysics.GetGroundZ(..., maxSearchDist: 150f)`. If support
     is more than the nearby-support tolerance below the teleport Z, set
     `MOVEFLAG_FALLINGFAR`, reset fall timing/vertical velocity for a clean
     fall, and seed `_prevGroundZ` to the probed ground.
   - Nearby support still snaps normally; no teleport ACK gate was changed.
   - Added deterministic coverage:
     `Update_PostTeleport_AirborneDestinationPrimesFallingBeforeFirstPhysicsStep`.

3. `af778d24` - `test(bg-movement-parity): refresh BG FALL_LAND baselines after airborne teleport fix`
   - Refreshed `background_durotar_vertical_drop_baseline.json`: standard 10y
     live BG drop now contains `MSG_MOVE_FALL_LAND` at 1253ms.
   - Refreshed `background_durotar_high_drop_baseline.json`: 100y live BG drop
     captured with `WWOW_BG_POST_TELEPORT_WINDOW_MS=10000` contains
     `MSG_MOVE_FALL_LAND` at 8357ms. The default 2500ms window is too short
     for a 100y fall.
   - Updated `PostTeleportPacketWindowParityTests` so the BG live baselines
     require `MSG_MOVE_FALL_LAND` and still reject `CMSG_SET_ACTIVE_MOVER`.

4. This docs commit - v11 handoff, task trackers, and parity audit update.

---

## Evidence and commands run

Session-start sanity:

- `git status && git fetch origin && git log --oneline -10` -> tip was
  `9c2ef1e6 docs(bg-movement-parity): v10 handoff`; three unrelated untracked
  ACK corpus JSON files were already present and were left untouched.
- `docker ps` -> `mangosd`, `realmd`, `pathfinding-service`, and `maria-db`
  were running/healthy. Do not keep re-checking in the next session; check once
  at session start.
- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests"` -> passed 6/6 before changes.

Validation after the fix:

- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Update_PostTeleport_AirborneDestinationPrimesFallingBeforeFirstPhysicsStep|FullyQualifiedName~Update_PostTeleport_NearbySupportBelowTeleportTarget_SnapsToNearbyGround|FullyQualifiedName~Update_PostTeleport_NoGroundBelow_AllowsGraceFall|FullyQualifiedName~Update_TeleportWithGroundSnap_RunsPhysics" --logger "console;verbosity=minimal"` -> passed 4/4.
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> passed, existing warnings only.
- 10y live BG capture command:
  `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW='1'; $env:WWOW_REPO_ROOT='e:/repos/Westworld of Warcraft'; $env:WWOW_LOG_LEVEL='Information'; $env:WWOW_FILE_LOG_LEVEL='Information'; $env:WWOW_CONSOLE_LOG_LEVEL='Warning'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Background_VerticalDropTeleport_CapturesPostTeleportWindow"` -> passed 1/1; captured `background_20260428_131256_436.json` with FALL_LAND at 1253ms.
- 100y live BG capture used a temporary local flip in
  `AckCaptureTests.cs` (`DurotarTeleportZ = DurotarGroundZ + 100f`) and
  `WWOW_BG_POST_TELEPORT_WINDOW_MS='10000'`; the command passed 1/1 and
  captured `background_20260428_132614_836.json` with FALL_LAND at 8357ms.
  The temporary source flip was reverted before committing.
- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> passed 6/6 after fixture/test refresh.

Notes:

- The 100y default 2500ms capture did not contain FALL_LAND because the fall
  had not landed yet. Diagnostics showed the bot still falling at about
  Z=129 after the 2.5s window.
- The 10s high-drop window opens on the staging teleport and includes the
  high-drop trigger about 5s later. This is why the high-drop FALL_LAND appears
  late in the window.

---

## Stream 2E.3 - closed details

### Root cause

Live BG reset movement state to `MOVEFLAG_NONE` before the first native
physics step after teleport. For airborne teleport destinations, the first
`NativeLocalPhysics` result could look like a completed ground snap rather
than a falling frame. The managed side then had no previous airborne state and
no future `FALLINGFAR -> grounded` edge to convert into `MSG_MOVE_FALL_LAND`.

### Fix

`MovementController.PrimeAirborneTeleportFallIfNeeded()` now runs once per
post-teleport reset before `RunPhysics(deltaSec)`:

- Refresh local scene data for the current position.
- Probe downward up to 150y.
- If ground exists and is more than the nearby-support tolerance below the
  teleport Z, set `_player.MovementFlags = MOVEFLAG_FALLINGFAR`, reset fall
  time and vertical velocity, and seed `_prevGroundZ`.
- If no ground is found or support is nearby, leave the existing snap/grace
  behavior alone.

Do not move this logic into teleport ACK readiness. Per
`docs/physics/state_teleport.md`, the binary ACK readiness gate is `0x468570`;
local physics tick state must not gate the ACK.

---

## Stream 4 - remaining work

**Done:** FG cross-map baseline exists at
`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_kalimdor_to_ek_cross_map_baseline.json`
and is pinned by
`ForegroundCrossMapBaseline_PinsTransferPendingNewWorldShape`.

**Remaining primary task:** BG cross-map baseline.

Suggested path:

1. Add a `Background_CrossMapTeleport_CapturesPostTeleportWindow` live test in
   `Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`, or extend
   `Background_VerticalDropTeleport_CapturesPostTeleportWindow` with a genuine
   cross-map hop before returning to Orgrimmar.
2. Use a real Kalimdor -> Eastern Kingdoms hop; Org -> Org will not exercise
   the cross-map recorder path.
3. Capture with:
   - `WWOW_ENABLE_RECORDING_ARTIFACTS=1`
   - `WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1`
   - `WWOW_REPO_ROOT="e:/repos/Westworld of Warcraft"`
4. Promote the fixture as
   `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_kalimdor_to_ek_cross_map_baseline.json`.
5. Add a `BackgroundCrossMapBaseline_*` parity test mirroring the foreground
   cross-map oracle but asserting the BG-today shape.

Lower-priority Stream 4 followups:

- Transport/zeppelin baseline. This probably needs recorder trigger research;
  normal transport movement may not use teleport opcodes.
- Knockback baseline. Likely add `SMSG_MOVE_KNOCK_BACK` to recorder trigger
  set, then capture with a GM knockback command.
- WORLDPORT_ACK followup. Current FG cross-map window starts on
  `SMSG_TRANSFER_PENDING`; `MSG_MOVE_WORLDPORT_ACK` can be outside 2.5s because
  WoW.exe pauses packet processing during map load. Options: longer recorder
  window or a second window triggered by outbound `MSG_MOVE_WORLDPORT_ACK`.

Exact next command:

```powershell
rg -n "Foreground_CrossMapTeleport|Background_VerticalDropTeleport|ForegroundCrossMapBaseline|IsInboundTeleportTrigger" Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs
```

---

## Hard rules (DO NOT VIOLATE)

- No background agents or `run_in_background:true`. Single session only.
- Commit and push after each logical unit of work.
- Binary parity is the movement/physics authority. Use WoW.exe behavior,
  decompiled VAs, packet captures, or committed fixture JSON as evidence.
- Live MaNGOS via Docker is always running. Run `docker ps` once at session
  start to confirm `mangosd`, `realmd`, `pathfinding-service`, and `maria-db`;
  do not keep re-checking.
- Use SOAP / bot chat for mutable MaNGOS operations. No direct MySQL writes
  except documented bootstrap exceptions.
- Do not use `Thread.Sleep` or bare `Task.Delay` in tests; use the snapshot
  polling helpers.
- Never blanket-kill processes. Kill only explicit PIDs you started. If
  `WoW.exe` may lock foreground binaries before a build, run
  `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST`, then kill only your PID if
  needed.
- When locally flipping a live test for verification, revert it before commit
  unless the flip is itself the intended change.
- Never add `_needsGroundSnap` or any physics-tick state to teleport ACK gates.
- Never re-introduce `&& !_isBeingTeleported` in
  `RestoreLocalPlayerControlFromHydratedUpdate`.
- Never re-introduce the `!_needsGroundSnap` movement-packet suppression guard.
- Never reorder `DetermineOpcode` so MOVE_STOP wins over FALL_LAND on a
  `FALLINGFAR/JUMPING -> grounded` transition.
- Never remove or weaken `NotifyExternalPacketSent` cadence suppression after
  `TryFlushPendingTeleportAck`.
- Never drop `BackgroundPostTeleportWindowRecorder.Start()` from
  `BackgroundBotWorker` when `WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1`.
- Shodan is GM-liaison/setup only; do not dispatch behavior actions to Shodan.
- No `.gobject add`, no synthetic node spawns, no `.learn all_myclass`, and no
  `.learn all_myspells`.
- Use positive test filters or per-project commands; do not use
  `dotnet test --filter "Category!=RequiresInfrastructure"` as a broad unit
  suite.
- Read `snapshot.RecentChatMessages` before diagnosing live GM/test failures.
- `bash` calls do not persist `cd`; use absolute paths or chain
  `cd "e:/repos/Westworld of Warcraft" && ...`.

### Live capture env vars

For BG post-teleport packet windows:

- `WWOW_ENABLE_RECORDING_ARTIFACTS=1`
- `WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1`
- `WWOW_REPO_ROOT="e:/repos/Westworld of Warcraft"`
- Optional for long falls: `WWOW_BG_POST_TELEPORT_WINDOW_MS=10000`

For FG post-teleport packet windows:

- `WWOW_ENABLE_RECORDING_ARTIFACTS=1`
- `WWOW_CAPTURE_POST_TELEPORT_WINDOW=1`
- `WWOW_REPO_ROOT="e:/repos/Westworld of Warcraft"`

For FG ACK corpus captures:

- `WWOW_CAPTURE_ACK_CORPUS=1`

---

## Starter checklist for the next session

1. `git status && git fetch origin && git log --oneline -10`
   - Confirm the docs v11 handoff commit is at tip or near tip.
   - Preserve unrelated untracked ACK corpus JSONs if still present.
2. `docker ps`
   - Confirm `mangosd`, `realmd`, `pathfinding-service`, and `maria-db`.
3. Read:
   - This file top to bottom.
   - `CLAUDE.md` and `AGENTS.md`.
   - `docs/physics/state_teleport.md`.
   - `docs/physics/bg_movement_parity_audit.md`.
   - `Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`.
   - The four committed post-teleport fixtures:
     - `foreground_durotar_vertical_drop_baseline.json`
     - `background_durotar_vertical_drop_baseline.json`
     - `background_durotar_high_drop_baseline.json`
     - `foreground_kalimdor_to_ek_cross_map_baseline.json`
4. Run the current parity sanity:

```powershell
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"
```

Expected: 6 passed / 0 failed.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v12.md`, self-contained as
this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what has been done since v11.
3. Remaining tasks, with completed work moved to a Done section or referenced
   via prior handoff.
4. Any blockers, surprises, or design pivots.
5. Repeat all hard rules above.
6. Repeat this "When you near context exhaustion" instruction so the next
   session does the same.

The user copies/pastes the latest handoff into a fresh session. Make it
self-contained; assume the next agent has zero memory.
