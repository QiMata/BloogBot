# RecordedTests.Shared.Tests Tasks

## Scope
- Project: `Tests/RecordedTests.Shared.Tests`
- Master tracker: `MASTER-SUB-027`
- Directory: `Tests/RecordedTests.Shared.Tests`
- Runtime surfaces under test:
- `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`
- `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`
- Local test targets:
- `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`
- `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`

## Execution Rules
1. Work tasks in this file top-down; do not branch to another project until this list is complete or blocked.
2. Keep commands one-line and timeout-bounded.
3. Never blanket-kill `dotnet`; cleanup must be repo-scoped and logged with process name/PID/result.
4. When a missing behavior is found, add a paired research + implementation task ID immediately.
5. Move completed items to `TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and a single `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj:22` uses `..\test.runsettings`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj` succeeded (`Restored ...RecordedTests.Shared.Tests.csproj`, 9/10 up-to-date).
- Runsettings wiring confirmed: `RecordedTests.Shared.Tests.csproj:22` sets `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`.
- Timeout confirmed: `Tests/test.runsettings:5` sets `<TestSessionTimeout>600000</TestSessionTimeout>`.
- Stub behavior remains and is directly evidenced:
- `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:49` has `TODO` upload stub.
- `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:130` logs S3 listing not implemented.
- `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:115` logs Azure listing not implemented.
- Baseline command validation:
- `dotnet test ... --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests"` -> `Passed: 25, Failed: 0, Skipped: 0`.

## P0 Active Tasks (Ordered)
1. [ ] `RTS-TST-001` Add direct tests for S3 `StoreAsync` warning/no-op semantics.
- Evidence: `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:27` through `:30` only warns and returns completed task.
- Gap: current S3 tests cover URI/key helpers and config only (`Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs:10` through `:167`), with explicit stub note at `:167`.
- Files: `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`, `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`.
- Validation: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"`.

2. [ ] `RTS-TST-002` Add method-contract tests for S3 `UploadArtifactAsync`, `DownloadArtifactAsync`, `ListArtifactsAsync`, and `DeleteArtifactAsync` current stub behavior.
- Evidence: TODO operation stubs at `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:49`, `:85`, `:111`, `:144`; list path currently logs not implemented and returns empty at `:130`.
- Gap: no assertions currently verify deterministic returned URI format, argument validation, directory creation side effects, and empty-list fallback semantics.
- Files: `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`, `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`.
- Validation: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"`.

3. [ ] `RTS-TST-003` Add direct tests for Azure Blob `StoreAsync` warning/no-op semantics.
- Evidence: `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:25` through `:28` only warns and returns completed task.
- Gap: current Azure tests cover URI/blob helper methods and config only (`Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs:10` through `:149`), with explicit stub note at `:149`.
- Files: `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`, `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`.
- Validation: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`.

4. [ ] `RTS-TST-004` Add method-contract tests for Azure `UploadArtifactAsync`, `DownloadArtifactAsync`, `ListArtifactsAsync`, and `DeleteArtifactAsync` current stub behavior.
- Evidence: TODO operation stubs at `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:47`, `:80`, `:101`, `:129`; list path currently logs not implemented and returns empty at `:115`.
- Gap: no assertions currently verify deterministic returned URI format, argument validation, directory creation side effects, and empty-list fallback semantics.
- Files: `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`, `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`.
- Validation: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`.

5. [ ] `RTS-TST-005` Add container/URI guard tests for Azure instance parse path used by download/delete operations.
- Evidence: download/delete resolve via `ParseAzureBlobUriInstance` at `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:70` and `:125`; configured container mismatch throws at `:204` through `:208`.
- Gap: current tests cover static URI parsing only, not instance-level container mismatch enforcement.
- Files: `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`, `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`.
- Validation: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`.

6. [ ] `RTS-TST-006` Simplify project command surface to fast storage-only checks and one bounded full-suite command.
- Evidence: command usage is currently implicit across files; no local canonical command block focused on storage regressions.
- Files: `Tests/RecordedTests.Shared.Tests/TASKS.md`.
- Validation: run the `Simple Command Set` below unchanged.

## Simple Command Set
- `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `RTS-TST-001` (S3 `StoreAsync` warning/no-op semantics).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `Passed: 25, Failed: 0, Skipped: 0`.
- Files changed: `Tests/RecordedTests.Shared.Tests/TASKS.md`.
- Blockers: None.
- Next task: `RTS-TST-001`.
- Next command: `Get-Content -Path 'Tests/Tests.Infrastructure/TASKS.md' -TotalCount 360`.
