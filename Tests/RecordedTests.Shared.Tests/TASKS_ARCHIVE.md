# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - Azure current-stub contract coverage and command closeout

- [x] `RTS-TST-003` Add direct tests for Azure Blob `StoreAsync` warning/no-op semantics.
- [x] `RTS-TST-004` Add method-contract tests for Azure `UploadArtifactAsync`, `DownloadArtifactAsync`, `ListArtifactsAsync`, and `DeleteArtifactAsync` current stub behavior.
- [x] `RTS-TST-005` Add container/URI guard tests for Azure instance parse path used by download/delete operations.
- [x] `RTS-TST-006` Simplify project command surface to fast storage-only checks and one bounded full-suite command.
- Completed:
  - Added Azure Blob tests for constructor validation, upload URI construction/logging, download/delete argument validation, invalid URI parsing, configured-container mismatch, stubbed download directory side effect, list fallback logging, `StoreAsync` warning/no-op behavior, and dispose idempotence.
  - Replaced the local task file with a current command-first closeout that reports no remaining owner-local test tasks.
- Validation:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (98/98)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (367/367)`

## Archived Snapshot (2026-04-15) - S3 current-stub contract coverage

- [x] `RTS-TST-001` Add direct tests for S3 `StoreAsync` warning/no-op semantics.
  - Existing direct tests now cover no-throw behavior and warning emission:
    - `StoreAsync_DoesNotThrow`
    - `StoreAsync_WithLogger_LogsWarning`
- [x] `RTS-TST-002` Add method-contract tests for S3 `UploadArtifactAsync`, `DownloadArtifactAsync`, `ListArtifactsAsync`, and `DeleteArtifactAsync` current stub behavior.
  - Added direct coverage for invalid S3 URI parsing in download/delete paths.
  - Added coverage that valid download creates the destination directory but does not create a file while the provider remains stubbed.
  - Added logger assertions for download, list fallback, and delete stub paths.
- Validation:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (50/50)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (62/62)`
