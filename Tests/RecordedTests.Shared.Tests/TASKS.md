# RecordedTests.Shared.Tests Tasks

## Scope
- Project: `Tests/RecordedTests.Shared.Tests`
- Directory: `Tests/RecordedTests.Shared.Tests`
- Runtime surfaces under test:
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/FileSystemRecordedTestStorage.cs`
- Local test targets:
  - `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/FileSystemRecordedTestStorageTests.cs`

## Execution Rules
1. Keep commands one-line and timeout-bounded.
2. Never blanket-kill `dotnet`; cleanup must be repo-scoped and logged with process name/PID/result.
3. Move completed items to `TASKS_ARCHIVE.md` in the same session.
4. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and a single `Next command`.

## Environment Checklist
- [x] `Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj:22` uses `..\test.runsettings`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] S3 real storage behavior is pinned by direct tests with deterministic in-memory backend coverage.
- [x] Azure Blob real storage behavior is pinned by direct tests with deterministic in-memory backend coverage.
- [x] Cross-provider parity behavior is pinned by a shared filesystem/S3/Azure matrix.
- [x] Storage-only and full-suite commands are documented and validated.

## Current Status (2026-04-15)
- Known remaining owner-local items: `0`.
- Completed item details are archived in `Tests/RecordedTests.Shared.Tests/TASKS_ARCHIVE.md`.
- Provider implementation work in `RecordedTests.Shared/TASKS.md` is complete (`RTS-MISS-001` through `RTS-MISS-004`).

## Completed P0 Items
- [x] `RTS-TST-001` Add direct tests for S3 `StoreAsync` warning/no-op semantics.
- [x] `RTS-TST-002` Add method-contract tests for S3 `UploadArtifactAsync`, `DownloadArtifactAsync`, `ListArtifactsAsync`, and `DeleteArtifactAsync` current stub behavior.
- [x] `RTS-TST-003` Add direct tests for Azure Blob `StoreAsync` warning/no-op semantics.
- [x] `RTS-TST-004` Add method-contract tests for Azure `UploadArtifactAsync`, `DownloadArtifactAsync`, `ListArtifactsAsync`, and `DeleteArtifactAsync` current stub behavior.
- [x] `RTS-TST-005` Add container/URI guard tests for Azure instance parse path used by download/delete operations.
- [x] `RTS-TST-006` Simplify project command surface to fast storage-only checks and one bounded full-suite command.

## Open Tasks
- None.

## Simple Command Set
1. S3/Azure storage checks: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`.
2. Storage parity checks: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests|FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests|FullyQualifiedName~RecordedTestStorageProviderParityTests" --logger "console;verbosity=minimal"`.
3. Bounded full suite: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-04-15
- Active task: none. `RTS-TST-001` through `RTS-TST-006` are complete; implementation-side `RTS-MISS-001` through `RTS-MISS-004` are also complete.
- Last delta: updated storage tests from S3/Azure stub contracts to real provider CRUD contracts plus shared provider parity coverage.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests|FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests|FullyQualifiedName~RecordedTestStorageProviderParityTests" --logger "console;verbosity=minimal"` -> `passed (125/125)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (382/382)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (56/56)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (53/53)`
- Files changed:
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/FileSystemRecordedTestStorage.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/InMemoryCloudStorageBackends.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/RecordedTestStorageProviderParityTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/TASKS.md`
- Blockers: none.
- Next command: `Get-Content -Path 'Services/TASKS.md' -TotalCount 360`
