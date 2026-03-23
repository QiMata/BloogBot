# WowSharpClient.NetworkTests Tasks

## Scope
- Directory: `Tests/WowSharpClient.NetworkTests`
- Project: `Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj`
- Primary implementation surfaces:
- `Exports/WoWSharpClient/Networking/Implementation/PacketPipeline.cs`
- `Exports/WoWSharpClient/Networking/Implementation/ConnectionManager.cs`
- `Exports/WoWSharpClient/Networking/Implementation/TcpConnection.cs`
- `Exports/WoWSharpClient/Client/AuthClient.cs`
- `Exports/WoWSharpClient/Client/WorldClient.cs`

## Execution Rules
1. Work tasks in this file top-down; do not switch to another local `TASKS.md` until this list is complete or blocked.
2. Keep commands simple, one-line, and timeout-bounded.
3. Use scan-budget discipline: read this file plus only directly referenced implementation/test files for the active task.
4. If two passes produce no file delta, record blocker plus exact next command in `Session Handoff`, then move to the next queue file.
5. Add newly discovered gaps immediately as `research + implementation` task pairs with file/symbol evidence.
6. Move completed items to `Tests/WowSharpClient.NetworkTests/TASKS_ARCHIVE.md` in the same session.
7. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
8. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `RunSettingsFilePath` is wired in `WowSharpClient.NetworkTests.csproj:21` to `..\test.runsettings`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] `PacketPipeline` encrypted receive state uses `_pendingDecryptedHeader` at `PacketPipeline.cs:49` and `:195-258`; tests now use `XorHeaderEncryptor` at `PacketPipelineTests.cs`.
- [x] `PacketPipeline` send serialization lock is `_sendLock` at `PacketPipeline.cs:45` and `:119-142`; concurrent send tests exercise this at `PacketPipelineTests.cs`.
- [x] `ConnectionManager` reconnect/cancel flow uses `TaskCompletionSource` deterministic waits at `ConnectionManagerTests.cs` (no more coarse `Task.Delay` waits).
- [x] `WorldClient` bridge registration and exception-swallow path tested at `WorldClientTests.cs`.

## P0 Active Tasks (Ordered)

All P0 tasks completed. See `TASKS_ARCHIVE.md`.

## Simple Command Set
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PacketPipelineTests|FullyQualifiedName~ConnectionManagerTests|FullyQualifiedName~TcpConnectionReactiveTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AuthClientTests|FullyQualifiedName~WorldClientTests" --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-03-23
- Active task: Keep bridge-registration coverage aligned with movement parity work from `Exports/WoWSharpClient`.
- Last delta: Added bridge-registration coverage for the observer-side Vanilla movement broadcasts so `WorldClient` now routes the full player/controller/observer movement matrix through the legacy movement handler map.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WorldClientTests.BridgeRegistration_MovementOpcodes_Registered" -v n` -> `1 passed`
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n` -> `117 passed`
- Files changed:
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`
- Blockers: None.
- Next task: Extend bridge coverage again if the remaining dispatch-table audit finds more movement/control opcodes.
- Next command: `Get-Content Exports/WoWSharpClient/Movement/SplineController.cs | Select-Object -First 260`
- Discovery: `WorldClient.RegisterWorldHandlers()` still registers `HandleAttackStart` before `BridgeToLegacy(SMSG_ATTACKSTART, SpellHandler.HandleAttackStart)`, so the reactive `AttackStateChanged` subject is likely shadowed by the legacy bridge registration. This remains a separate latent bug, not part of the current movement parity slice.
