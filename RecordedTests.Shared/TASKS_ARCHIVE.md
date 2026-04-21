# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - Real cloud storage provider parity

- [x] `RTS-MISS-001` Implement real S3 storage operations.
  - Added `AWSSDK.S3` package wiring.
  - Replaced upload/download/list/delete TODO stubs with an SDK-backed `AwsS3StorageBackend`.
  - `StoreAsync` now stores run metadata and available test-run/recording artifacts instead of warning/no-op.
  - Missing S3 downloads map to `FileNotFoundException`; deletes remain idempotent.
- [x] `RTS-MISS-002` Implement real Azure Blob storage operations.
  - Added `Azure.Storage.Blobs` package wiring.
  - Replaced upload/download/list/delete TODO stubs with an SDK-backed `AzureSdkBlobStorageBackend`.
  - `StoreAsync` now stores run metadata and available test-run/recording artifacts instead of warning/no-op.
  - Missing Azure downloads map to `FileNotFoundException`; deletes remain idempotent.
- [x] `RTS-MISS-003` Normalize cancellation and failure semantics across providers.
  - Filesystem, S3, and Azure providers now check cancellation before backend work.
  - Cloud providers enforce configured bucket/container URI ownership and deterministic missing-artifact behavior.
- [x] `RTS-MISS-004` Add provider contract parity tests.
  - Added deterministic in-memory S3/Azure backends for credential-free unit tests.
  - Added `RecordedTestStorageProviderParityTests` covering upload/download/list/delete, missing artifacts, cancellation, and `StoreAsync` metadata across filesystem/S3/Azure.
- Validation:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests|FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests|FullyQualifiedName~RecordedTestStorageProviderParityTests" --logger "console;verbosity=minimal"` -> `passed (125/125)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (382/382)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (56/56)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (53/53)`
  - `rg -n "TODO: Implement actual S3|S3 listing not yet implemented|TODO: Implement actual Azure Blob|Azure Blob listing not yet implemented|StoreAsync is not directly implemented" RecordedTests.Shared/Storage -S` -> no matches
  - `rg -n "stubbed|download stub|not yet implemented|requires AWSSDK|requires Azure.Storage.Blobs" Tests/RecordedTests.Shared.Tests/Storage RecordedTests.Shared/Storage -S` -> no matches
