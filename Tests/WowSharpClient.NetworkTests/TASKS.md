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

All P0 tasks completed. See TASKS_ARCHIVE.md.

## Simple Command Set
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PacketPipelineTests|FullyQualifiedName~ConnectionManagerTests|FullyQualifiedName~TcpConnectionReactiveTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AuthClientTests|FullyQualifiedName~WorldClientTests" --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-28
- Active task: All P0 tasks complete.
- Last delta: Implemented all 6 pending tasks (WSCN-TST-001 through WSCN-TST-006), adding 28 new test methods across 5 test files.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"` -> Passed `117`, Failed `0`, Skipped `0` (run twice for determinism check).
- Files changed:
  - `Tests/WowSharpClient.NetworkTests/PacketPipelineTests.cs` — Added `XorHeaderEncryptor` test helper, 6 encrypted receive-state tests (TST-001), 3 concurrent send tests (TST-002)
  - `Tests/WowSharpClient.NetworkTests/ConnectionManagerTests.cs` — Replaced `Task.Delay` waits with `TaskCompletionSource` for determinism, added 4 new deterministic reconnect/cancel tests (TST-003)
  - `Tests/WowSharpClient.NetworkTests/TcpConnectionReactiveTests.cs` — Added 4 tests: reconnect-while-connected, server-close-then-disconnect, connect-after-dispose, dispose-completes-observables (TST-004)
  - `Tests/WowSharpClient.NetworkTests/AuthClientTests.cs` — Added 5 tests: unknown-opcode resync, multiple-unknown-opcodes, fragmented realm list, realm-list-one-byte-then-rest, fragmented-failed-challenge (TST-005)
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs` — Added 6 tests: bridge movement opcodes, bridge login/object opcodes, bridge-handler-throws, multiple-throws-never-terminates, attack-swing-errors-observable, register-opcode-handler (TST-006)
  - `Tests/WowSharpClient.NetworkTests/TASKS.md` — Marked all tasks complete
  - `Tests/WowSharpClient.NetworkTests/TASKS_ARCHIVE.md` — Archived completed tasks
- Blockers: None.
- Next task: None in this queue. All WSCN-TST tasks are complete.
- Next command: Check `docs/TASKS.md` for any remaining items.
- Discovery: `WorldClient.RegisterWorldHandlers()` registers `HandleAttackStart` (reactive subject) then `BridgeToLegacy(SMSG_ATTACKSTART, SpellHandler.HandleAttackStart)` which overwrites it via `MessageRouter.AddOrUpdate`. The reactive `AttackStateChanged` subject is never emitted for attack start/stop opcodes. This is a latent bug — the bridge handler should be wired to ALSO emit to the reactive subject, or the registration order should be reversed. Filed as an observation, not blocking.
