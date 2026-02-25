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
- [x] `PacketPipeline` encrypted receive state uses `_pendingDecryptedHeader` at `PacketPipeline.cs:49` and `:195-258`; current tests use `NoEncryption` only at `PacketPipelineTests.cs:17`, `:59`, `:90`, `:133`, `:183`, `:226`.
- [x] `PacketPipeline` send serialization lock is `_sendLock` at `PacketPipeline.cs:45` and `:119-142`.
- [x] `ConnectionManager` reconnect/cancel flow is in `OnDisconnected` at `ConnectionManager.cs:79-124`; current tests rely on coarse `Task.Delay` waits at `ConnectionManagerTests.cs:36`, `:66`, `:99`, `:130`, `:182`, `:263`.
- [x] `WorldClient` bridge registration and exception-swallow path are in `WorldClient.cs:180-329` including `BridgeToLegacy` catch at `:324`.

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj` succeeded (`Restored ...WowSharpClient.NetworkTests.csproj`, 3/4 up-to-date).
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PacketPipelineTests" --logger "console;verbosity=minimal"` passed (`Passed: 6`, `Failed: 0`, `Skipped: 0`, duration `997 ms`).
- Runsettings and timeout are active: `WowSharpClient.NetworkTests.csproj:21` (`RunSettingsFilePath`) and `Tests/test.runsettings:5` (`TestSessionTimeout=600000`).
- Encrypted receive-state gap remains explicit:
- `_pendingDecryptedHeader` lifecycle in `PacketPipeline.cs` (`49`, `195-258`).
- tests still instantiate `NoEncryption` only (`PacketPipelineTests.cs:17/59/90/133/183/226`).
- Reconnect determinism gap remains explicit:
- `ConnectionManagerTests.cs` still uses fixed `Task.Delay` waits (`36/66/99/130/182/263`) while reconnect cancel logic is in `ConnectionManager.cs:109-114`.
- `WorldClient` bridge risk remains explicit:
- bulk `BridgeToLegacy` registrations (`212-310`) and swallow catch at `324`.

## P0 Active Tasks (Ordered)
1. [ ] `WSCN-TST-001` Add encrypted `PacketPipeline` receive-state tests (fragmented header/body, remainder carry, and header reset).
- Evidence: encrypted parsing state machine exists at `PacketPipeline.cs:195-258`, but tests currently run only with `NoEncryption`.
- Files: `Tests/WowSharpClient.NetworkTests/PacketPipelineTests.cs`, `Exports/WoWSharpClient/Networking/Implementation/PacketPipeline.cs`.
- Required breakdown: use a deterministic test encryptor (non-`NoEncryption`), feed split buffers across multiple receives, and assert complete packet reconstruction plus `_pendingDecryptedHeader` lifecycle.
- Validation: `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PacketPipelineTests" --logger "console;verbosity=minimal"`.

2. [ ] `WSCN-TST-002` Add concurrent send serialization tests for `PacketPipeline` lock behavior.
- Evidence: `_sendLock` protects send path at `PacketPipeline.cs:119-142` but no explicit concurrent-send test exists.
- Files: `Tests/WowSharpClient.NetworkTests/PacketPipelineTests.cs`, `Exports/WoWSharpClient/Networking/Implementation/PacketPipeline.cs`.
- Required breakdown: run multi-task sends against a recording fake connection and assert serialized send ordering/no payload interleaving.
- Validation: `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PacketPipelineTests" --logger "console;verbosity=minimal"`.

3. [ ] `WSCN-TST-003` Make `ConnectionManager` reconnect cancellation/dispose tests deterministic.
- Evidence: reconnect loop cancel checks are at `ConnectionManager.cs:109-114`, while tests rely on fixed `Task.Delay` timing.
- Files: `Tests/WowSharpClient.NetworkTests/ConnectionManagerTests.cs`, `Exports/WoWSharpClient/Networking/Implementation/ConnectionManager.cs`.
- Required breakdown: add targeted tests that trigger disconnect, cancel during backoff, and assert no reconnect attempts after cancellation/dispose without long sleep windows.
- Validation: `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ConnectionManagerTests" --logger "console;verbosity=minimal"`.

4. [ ] `WSCN-TST-004` Add `TcpConnection` tests for reconnect semantics and duplicate disconnect-emit guard.
- Evidence: reconnect calls disconnect first at `TcpConnection.cs:46-47`; disconnected events can be raised from multiple paths (`:84-87`, `:134`).
- Files: `Tests/WowSharpClient.NetworkTests/TcpConnectionReactiveTests.cs`, `Exports/WoWSharpClient/Networking/Implementation/TcpConnection.cs`.
- Required breakdown: verify reconnect while connected emits exactly one transition per cycle and explicit disconnect/read-complete paths do not double-notify observers.
- Validation: `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~TcpConnectionReactiveTests" --logger "console;verbosity=minimal"`.

5. [ ] `WSCN-TST-005` Add `AuthClient` raw parser boundary tests for unknown-opcode resync and fragmented realm list frames.
- Evidence: unknown opcode byte-drop and realm list framing paths exist in `AuthClient.cs:345-360` but are not directly asserted in tests.
- Files: `Tests/WowSharpClient.NetworkTests/AuthClientTests.cs`, `Exports/WoWSharpClient/Client/AuthClient.cs`.
- Required breakdown: inject malformed/partial buffers that include unknown opcodes and split `0x10` payloads; assert parser resync and correct realm-list completion behavior.
- Validation: `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AuthClientTests" --logger "console;verbosity=minimal"`.

6. [ ] `WSCN-TST-006` Add `WorldClient` bridge coverage tests for critical opcode registration and exception swallow path.
- Evidence: bridge registration list is large (`WorldClient.cs:180-311`) and `BridgeToLegacy` swallows handler exceptions (`:324-329`) without dedicated regression tests.
- Files: `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`, `Exports/WoWSharpClient/Client/WorldClient.cs`.
- Required breakdown: assert representative movement/login/object opcodes are registered and verify handler throw does not terminate dispatch pipeline.
- Validation: `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WorldClientTests" --logger "console;verbosity=minimal"`.

## Simple Command Set
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PacketPipelineTests|FullyQualifiedName~ConnectionManagerTests|FullyQualifiedName~TcpConnectionReactiveTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AuthClientTests|FullyQualifiedName~WorldClientTests" --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `WSCN-TST-001` (encrypted `PacketPipeline` receive-state tests).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PacketPipelineTests" --logger "console;verbosity=minimal"` -> Passed `6`, Failed `0`, Skipped `0`.
- Files changed: `Tests/WowSharpClient.NetworkTests/TASKS.md`.
- Blockers: None.
- Next task: `WSCN-TST-001`.
- Next command: `Get-Content -Path 'Tests/WoWSharpClient.Tests/TASKS.md' -TotalCount 360`.
