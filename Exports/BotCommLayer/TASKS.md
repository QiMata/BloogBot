# BotCommLayer Tasks

## Scope
- Project: `Exports/BotCommLayer`
- Owns protobuf contracts and socket transport used by FG/BG snapshot and command exchange.
- This file tracks direct contract/interop tasks tied to concrete files and tests.
- Master tracker: `MASTER-SUB-003` in `docs/TASKS.md`.

## Execution Rules
1. Work this file only until the current top unchecked task is completed or blocked.
2. Limit scans to `Exports/BotCommLayer` plus directly referenced tests.
3. Keep generated artifacts (`Models/*.cs`) aligned with `.proto` sources.
4. Preserve corpse-run canonical fields used by live tests (`dead/ghost/reclaim/movement`).
5. Record `Last delta`, `Pass result`, and `Next command` in `Session Handoff` after each pass.
6. Move completed items to `Exports/BotCommLayer/TASKS_ARCHIVE.md` in the same session.
7. Keep commands single-line and runnable as-is from repo root.
8. Resume-first guard: start each pass by running the prior `Session Handoff -> Next command` verbatim before new scans.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same pass.

## Environment Checklist
- [x] `Exports/BotCommLayer/BotCommLayer.csproj` builds in `Release`.
- [x] Snapshot serialization tests are runnable from `Tests/BotRunner.Tests` (`WoWActivitySnapshotMovementTests`).
- [x] Proto regeneration scripts and commands are documented and reproducible (`README.md`, `protocsharp.bat`, `protocpp.bat`).

## Evidence Snapshot (2026-02-25)
- [x] Contract fields used by corpse lifecycle are present in proto contracts.
  - `game.proto`: `WoWPlayer.corpseRecoveryDelaySeconds` at line 123.
  - `communication.proto`: `WoWActivitySnapshot.player`, `movementData`, `recentChatMessages`, `recentErrors` at lines 161/168/170/171.
  - `DeathCorpseRunTests.cs`: lifecycle extraction uses `Health`, `Bytes1`, `CorpseRecoveryDelaySeconds`, `MovementFlags` (lines 61, 92, 117, 120).
- [x] `WoWActivitySnapshotExtensions.cs` is interface-only (`WoWActivitySnapshot : IWoWActivitySnapshot`) and contains no mapping logic.
- [x] BotCommLayer build passes.
  - Command: `dotnet build Exports/BotCommLayer/BotCommLayer.csproj --configuration Release --no-restore`
  - Result: success, 0 errors.
- [x] Snapshot movement tests pass.
  - Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`
  - Result: passed 14 tests.
- [x] Live corpse-run validation fails on BG runback stall (reproduced).
  - Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
  - Result: failed in ~1m47s with `[BG] scenario failed: corpse run stalled with minimal movement (travel=0.0y, moveFlags=0x0)`.
- [x] Repo-scoped cleanup command executes.
  - Command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`
  - Result: exit code 0.
- [x] Teardown hardening gap remains in socket server/client code.
  - `ProtobufSocketServer.cs`: has `Stop()` and `_isRunning`, no `IDisposable` contract.
  - `ProtobufAsyncSocketServer.cs`: `while (true)` client loop and direct `client.Close()` without cancellation token flow.
  - `ProtobufSocketClient.cs`: `Close()` exists but no `IDisposable` contract for deterministic ownership patterns.

## P0 Active Tasks (Ordered)

### BCL-MISS-001 Snapshot contract parity audit for corpse lifecycle fields
- [x] **Done (batch 12).** Corpse lifecycle field map audited and documented below. All fields present in proto, populated by BotRunnerService.Snapshot.cs, and consumed by DeathCorpseRunTests.
- [x] **Canonical corpse lifecycle field map (proto → snapshot → consumer):**
  | Proto field | game.proto location | Populated by | Consumer |
  |---|---|---|---|
  | `WoWUnit.health` | field 11 | `BuildUnitProtobuf`: `unit.Health = u.Health` | `GetLifeState()`: `unit.Health == 0` = dead |
  | `WoWUnit.bytes1` | field 18 | `BuildUnitProtobuf`: `unit.Bytes1 = u.Bytes1` | `GetLifeState()`: `bytes1 & 0xFF == 7` = stand-dead |
  | `WoWUnit.movementFlags` | field 22 | `BuildUnitProtobuf`: `unit.MovementFlags = u.MovementFlags` | Movement stall detection |
  | `WoWPlayer.playerFlags` | field 5 | `BuildPlayerProtobuf`: `player.PlayerFlags = lp.PlayerFlags` | `GetLifeState()`: `flags & 0x10` = ghost |
  | `WoWPlayer.corpseRecoveryDelaySeconds` | field 43 | `BuildPlayerProtobuf`: `player.CorpseRecoveryDelaySeconds = lp.CorpseRecoveryDelaySeconds` | Reclaim gating |
  | `MovementData.movementFlags` | communication.proto field | `PopulateSnapshotFromObjectManager` | Fallback movement flag source |
- [x] **Intentional omissions:**
  - `CorpsePosition` is NOT in the proto. Ghost runback uses `IWoWLocalPlayer.CorpsePosition` locally in BotRunner, not via IPC snapshot. If remote decision-making needs it, add to `WoWPlayer` in game.proto.
  - `DeathState` enum is NOT in the proto. Consumers derive it from health/bytes1/playerFlags per `GetLifeState()` pattern.
- [x] Validation: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Debug --no-build --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests"` — 17/17 pass including 3 new death state round-trip tests.
- [x] Acceptance: all required fields are explicitly accounted for with concrete proto field numbers, population call sites, and consumer patterns.

### BCL-MISS-002 Add regression coverage for snapshot serialization of death/runback fields
- [x] **Done (batch 12).** Added 3 death/runback serialization tests to `ActivitySnapshotMovementTests.cs`:
  - `DeathState_GhostForm_ShouldRoundTrip` — health=0, bytes1=DEAD, playerFlags=GHOST, corpseRecoveryDelay round-trip
  - `DeathState_AliveAfterResurrect_ShouldClearDeathFields` — health>0, no ghost flag, stand state normal
  - `DeathState_CorpseRunMovement_ShouldPreserveRunbackFields` — StateChangeResponse wrapping with dual movementFlags + speeds
- [x] Target files:
  - `Tests/BotRunner.Tests/ActivitySnapshotMovementTests.cs`
  - `Tests/BotRunner.Tests/BotRunner.Tests.csproj`
- [x] Evidence gap closed: `DeathState_GhostForm_ShouldRoundTrip`, `DeathState_AliveAfterResurrect_ShouldClearDeathFields`, and `DeathState_CorpseRunMovement_ShouldPreserveRunbackFields` all assert `CorpseRecoveryDelaySeconds`, `Health`, `Bytes1`, and `PlayerFlags` round-trip.
- [x] Required change: done — 17/17 snapshot tests pass.
- [x] Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`.
- [x] Validation: 17/17 snapshot tests pass including new death state assertions.
- [x] Acceptance: tests fail if corpse-run critical snapshot fields (health, bytes1, playerFlags, corpseRecoveryDelaySeconds, movementFlags) are dropped or remapped incorrectly.

### BCL-MISS-003 Harden socket teardown and cancellation paths
- [x] Problem: lingering test processes are expensive; socket layers need deterministic stop semantics under timeout/cancel.
- [x] Target files:
  - `Exports/BotCommLayer/ProtobufSocketServer.cs`
  - `Exports/BotCommLayer/ProtobufAsyncSocketServer.cs`
  - `Exports/BotCommLayer/ProtobufSocketClient.cs`
- [x] Changes applied (2026-02-27):
  - Added `IDisposable` to all three types (`ProtobufSocketServer`, `ProtobufAsyncSocketServer`, `ProtobufSocketClient`).
  - Fixed `while(true)` → `while(_isRunning)` in `ProtobufAsyncSocketServer.HandleClient`.
  - Added client dictionary cleanup on disconnect and bulk close in `Stop()`.
  - Added guarded `_server.Stop()` exception handling in both server types.
  - `ProtobufSocketClient.Dispose()` calls `Close()` + disposes stream/client.
- [x] Validation command: `dotnet build Exports/BotCommLayer/BotCommLayer.csproj --configuration Release --no-restore`.
- [x] Acceptance: timeout/cancel paths do not leave active listeners/clients for this repo scope.

### BCL-MISS-004 Keep proto regeneration workflow explicit and low-friction
- [x] Problem: schema edits can drift from generated C# when regeneration workflow is unclear.
- [x] Target files:
  - `Exports/BotCommLayer/README.md`
  - `Exports/BotCommLayer/Models/ProtoDef/protocsharp.bat`
  - `Exports/BotCommLayer/Models/ProtoDef/protocpp.bat`
- [x] Changes applied (2026-02-28):
  - README updated: canonical single-command for C# regen from repo root, batch script auto-resolves `tools/protoc/bin/protoc.exe`, C++ section documents external `ActivityManager` target, "never manually edit" warning.
  - `protocsharp.bat`: default protoc path changed from `C:\protoc\bin\protoc.exe` to repo-local `tools/protoc/bin/protoc.exe` via `%~dp0` relative path.
  - `protocpp.bat`: unchanged (external target, no repo-local consumers).
- [x] Acceptance: another agent can regenerate models without scanning unrelated directories.

## Simple Command Set
1. `dotnet build Exports/BotCommLayer/BotCommLayer.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
### 2026-04-28 (Jump action contract)
- Last delta:
  - Added protobuf `ActionType.JUMP = 81` and regenerated
    `Exports/BotCommLayer/Models/Communication.cs`.
  - This supports direct FG/BG running-jump movement parity without routing
    through ad hoc chat/script actions.
- Pass result: `delta shipped`
- Validation/tests run:
  - `.\protocsharp.bat "." ".."` from `Exports/BotCommLayer/Models/ProtoDef` -> `succeeded`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors after regeneration)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_direct_actions_full_04.trx"` -> `passed (5/5; duration 2m41s)`.
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
  - `Exports/BotCommLayer/Models/Communication.cs`
  - `Exports/BotCommLayer/TASKS.md`
- Next command: `git status --short --branch`

### 2026-04-21 (P4.4)
- Pass result: `ActionMessage correlation ids and CommandAckEvent are part of the canonical contract`
- Last delta:
  - `communication.proto` now adds `ActionMessage.correlation_id`, new `CommandAckEvent`, and `WoWActivitySnapshot.recent_command_acks` with the cap-10 ring documented next to the field.
  - Regenerated `Exports/BotCommLayer/Models/Communication.cs` with `protocsharp.bat` so the new contract ships without any compatibility shim or feature flag.
- Validation/tests run:
  - `& .\protocsharp.bat "." ".."` (from `Exports/BotCommLayer/Models/ProtoDef`) -> `succeeded`
  - `dotnet build Exports/BotCommLayer/BotCommLayer.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (33 warnings, 0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~LoadoutSpecConverterTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
  - `Exports/BotCommLayer/Models/Communication.cs`
  - `Exports/BotCommLayer/TASKS.md`
- Commits:
  - `9232c83f` `feat(comm): P4.4 add command ack proto schema`
  - `4d1b7489` `feat(botrunner): P4.4 plumb correlated command acks`
  - `3f800ed9` `test(botrunner): P4.4 cover command ack round-trips`
- Next command: `rg -n "LastAckStatus|SendGmChatCommandTrackedAsync|RecentCommandAcks|ContainsCommandRejection" Services/WoWStateManager Tests/BotRunner.Tests docs/TASKS.md`

- Last updated: 2026-04-20
- Master tracker: `MASTER-SUB-003`
- Active task: `none`
- Last delta:
  - Session 301 extended `communication.proto` / generated `Communication.cs` so `WoWActivitySnapshot` now carries `desired_party_leader_name`, `desired_party_members`, and `desired_party_is_raid`.
  - This contract is now used by StateManager/BotRunner battleground coordination so group-queue battlegrounds can form parties/raids from a single high-level desired-state payload instead of fixture-owned invite choreography.
  - Validation:
    - `cmd /c protocsharp.bat . ..` (from `Exports/BotCommLayer/Models/ProtoDef`) -> regenerated `Exports/BotCommLayer/Models/Communication.cs`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BotRunnerServiceDesiredPartyTests|FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~CoordinatorStrictCountTests" --logger "console;verbosity=minimal"` -> `passed (55/55)`
- Pass result: `desired battleground party-state fields are now part of the canonical snapshot contract and covered by deterministic tests`
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
  - `Exports/BotCommLayer/Models/Communication.cs`
  - `Exports/BotCommLayer/TASKS.md`
- Next command: `dotnet build Exports/BotCommLayer/BotCommLayer.csproj --configuration Release --no-restore`
- Previous handoff notes:
  - Extended `pathfinding.proto` again so `CalculatePathResponse` carries jump-gap, safe-drop, unsafe-drop, and blocked counts plus max climb height, max gap distance, and max drop height.
  - Regenerated `Exports/BotCommLayer/Models/Pathfinding.cs` with `protocsharp.bat`.
  - Updated README contract notes so callers know the expanded route-affordance metadata is part of the supported wire contract.
- Pass result: `delta shipped`
- Validation/tests run:
  - `E:\repos\Westworld of Warcraft\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat . .\..` (run from `Exports/BotCommLayer/Models/ProtoDef`) -> `succeeded`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathAffordanceClassifierTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`
  - `Exports/BotCommLayer/Models/Pathfinding.cs`
  - `Exports/BotCommLayer/README.md`
  - `Exports/BotCommLayer/TASKS.md`
- Blockers: none
- Next task: none in this owner; scan master/local trackers for any remaining unchecked work.
- Next command: `rg -n "^- \\[ \\]|\\[ \\] Problem|Active task:" docs/TASKS.md Exports/Navigation/TASKS.md Services/PathfindingService/TASKS.md Tests/PathfindingService.Tests/TASKS.md Tests/Navigation.Physics.Tests/TASKS.md Exports/BotRunner/TASKS.md Tests/BotRunner.Tests/TASKS.md`
- Loop Break: if no delta after two passes, record blocker with exact missing field/test and move to next queued file.
