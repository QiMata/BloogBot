# WoWSharpClient.Tests Tasks

## Scope
- Directory: `Tests/WoWSharpClient.Tests`
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Master tracker: `MASTER-SUB-030`
- Primary implementation surfaces:
- `Tests/WoWSharpClient.Tests/Handlers/SMSG_UPDATE_OBJECT_Tests.cs`
- `Tests/WoWSharpClient.Tests/Handlers/OpcodeHandler_Tests.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
- `Exports/WoWSharpClient/Models/WoWUnit.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs`

## Execution Rules
1. Work tasks in this file top-down; do not switch to another local `TASKS.md` until this list is complete or blocked.
2. Keep commands simple and one-line; use test filters for smallest targeted run first.
3. Use scan-budget discipline: read this file plus only directly referenced handler/model files for each task.
4. If two passes produce no file delta, record blocker plus exact next command in `Session Handoff`, then move to the next queue file.
5. Move completed items to `Tests/WoWSharpClient.Tests/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `WoWSharpClient.Tests.csproj:823` sets `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] `SMSG_UPDATE_OBJECT_Tests.cs:16` contains a TODO marker on a currently broad replay test.
- [x] `SMSG_UPDATE_OBJECT_Tests.cs:41` and `:149` use fixed `Thread.Sleep` delays, creating nondeterministic timing sensitivity.
- [x] `OpcodeHandler_Tests.cs:44` contains a TODO marker.
- [x] `OpcodeHandler_Tests.cs:47` includes parameter `handlerType` that is not asserted or used for validation.
- [x] `WoWSharpObjectManager.cs:2000-2039` documents unimplemented `WoWPlayer` field mappings.
- [x] `WoWUnit.cs:270` has TODO for `CMSG_CANCEL_AURA` packet send path.
- [x] `GossipNetworkClientComponent.cs:249` logs "Custom navigation strategy not implemented."

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj` succeeded (all projects up-to-date).
- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SMSG_UPDATE_OBJECT|FullyQualifiedName~OpcodeHandler" --logger "console;verbosity=minimal"` passed (`Passed: 26`, `Failed: 0`, `Skipped: 0`, duration `199 ms`).
- Local tooling note: build output logs `dumpbin` missing from `vcpkg` applocal script, but test run completed and passed.
- Timeout wiring confirmed:
- `WoWSharpClient.Tests.csproj:823` includes `RunSettingsFilePath`.
- `Tests/test.runsettings:5` sets `TestSessionTimeout=600000`.
- Current test debt remains explicit:
- `SMSG_UPDATE_OBJECT_Tests.cs` TODO marker and fixed sleeps at `16`, `41`, `149`.
- `OpcodeHandler_Tests.cs` TODO marker and unused `handlerType` at `44`, `47`.
- Backlog linkage remains explicit:
- `WoWSharpObjectManager.cs:2000-2039` comments identify unimplemented player field mappings.
- `WoWUnit.cs:270` has `CMSG_CANCEL_AURA` TODO.
- `GossipNetworkClientComponent.cs:249` logs custom navigation strategy not implemented.

## P0 Active Tasks (Ordered)
1. [ ] `WSC-TST-001` Replace TODO-only object update replay test with deterministic assertions and explicit coverage intent.
- Evidence: `SMSG_UPDATE_OBJECT_Tests.cs:16` TODO marker and broad assertions in `ShouldDecompressAndParseAllCompressedUpdateObjectPackets`.
- Files: `Tests/WoWSharpClient.Tests/Handlers/SMSG_UPDATE_OBJECT_Tests.cs`.
- Required breakdown: keep or split the test into focused cases (decode, object creation, stable object count/guid invariants) with explicit assertion rationale.
- Validation: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SMSG_UPDATE_OBJECT" --logger "console;verbosity=minimal"`.

2. [ ] `WSC-TST-002` Replace TODO-only opcode dispatch replay test with observable assertions.
- Evidence: `OpcodeHandler_Tests.cs:44` TODO marker; current test dispatches packets without assertions; `handlerType` is unused (`:47`).
- Files: `Tests/WoWSharpClient.Tests/Handlers/OpcodeHandler_Tests.cs`.
- Required breakdown: assert at least one measurable outcome per opcode class (state mutation, collection delta, or expected no-throw + postcondition), or remove rows that cannot provide deterministic value.
- Validation: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~OpcodeHandler" --logger "console;verbosity=minimal"`.

3. [ ] `WSC-TST-003` Remove sleep-based flakiness from object replay tests.
- Evidence: fixed sleeps at `SMSG_UPDATE_OBJECT_Tests.cs:41` and `:149` can race with async update processing.
- Files: `Tests/WoWSharpClient.Tests/Handlers/SMSG_UPDATE_OBJECT_Tests.cs`, `Tests/WoWSharpClient.Tests/Util/*` (if helper updates are needed).
- Required breakdown: replace `Thread.Sleep` with deterministic wait/flush helper or explicit completion signal from update processing before assertions.
- Validation: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SMSG_UPDATE_OBJECT|FullyQualifiedName~SessionTimelineReplayTests" --logger "console;verbosity=minimal"`.

4. [ ] `WSC-TST-004` Add regression tests mapped to `WSC-MISS-*` implementation backlog.
- Evidence: unimplemented targets are documented at `WoWSharpObjectManager.cs:2000-2039`, `WoWUnit.cs:270`, and `GossipNetworkClientComponent.cs:249`.
- Files: `Tests/WoWSharpClient.Tests/**/*.cs`, `Exports/WoWSharpClient/**/*.cs`.
- Required breakdown: add direct tests for player-field mapping behavior, aura cancel packet path, and gossip navigation fallback/strategy behavior as each implementation lands.
- Validation: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.

## Simple Command Set
- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SMSG_UPDATE_OBJECT|FullyQualifiedName~OpcodeHandler" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `WSC-TST-001` (replace TODO-only object-update replay test with deterministic assertions).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SMSG_UPDATE_OBJECT|FullyQualifiedName~OpcodeHandler" --logger "console;verbosity=minimal"` -> Passed `26`, Failed `0`, Skipped `0`.
- Files changed: `Tests/WoWSharpClient.Tests/TASKS.md`.
- Blockers: None.
- Next task: `WSC-TST-001`.
- Next command: `Get-Content -Path 'Tests/WoWSimulation/TASKS.md' -TotalCount 360`.
