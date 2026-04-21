# RecordedTests.Shared Tasks

## Scope
- Directory: `RecordedTests.Shared`
- Project: `RecordedTests.Shared.csproj`
- Focus: shared recorded-test storage provider parity and deterministic artifact semantics across filesystem/S3/Azure.
- Master tracker: `MASTER-SUB-011`.
- Keep only unresolved work here; move completed items to `RecordedTests.Shared/TASKS_ARCHIVE.md` in the same session.

## Execution Rules
1. Work only the top unchecked task ID unless blocked.
2. Keep scans source-scoped to `RecordedTests.Shared` and `Tests/RecordedTests.Shared.Tests` while this file is active.
3. Preserve identical behavior contracts across providers (list/filter/upload/download/delete/cancellation/error semantics).
4. Keep commands simple and one-line.
5. Never blanket-kill `dotnet`; cleanup must be repo-scoped and evidenced.
6. Move completed tasks to `RecordedTests.Shared/TASKS_ARCHIVE.md` in the same session.
7. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
8. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [x] Provider configuration inputs are explicit (filesystem root, S3 bucket/region, Azure container/account).
- [x] Integration tests use deterministic in-memory backend paths when cloud credentials are absent.
- [x] Artifact naming conventions are shared across all providers by the provider parity test matrix.
- [x] No S3/Azure operation-stub markers remain in `RecordedTests.Shared/Storage`.

## Current Status (2026-04-15)
- Known remaining owner-local items: `0`.
- Completed item details are archived in `RecordedTests.Shared/TASKS_ARCHIVE.md`.
- `RTS-MISS-001` through `RTS-MISS-004` are complete.

## P0 Active Tasks (Ordered)
None.

## Simple Command Set
1. `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"`
2. `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests|FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests|FullyQualifiedName~RecordedTestStorageProviderParityTests" --logger "console;verbosity=minimal"`
4. `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
5. `rg -n "TODO: Implement actual S3|S3 listing not yet implemented|TODO: Implement actual Azure Blob|Azure Blob listing not yet implemented|StoreAsync is not directly implemented|stubbed|download stub|not yet implemented" RecordedTests.Shared/Storage Tests/RecordedTests.Shared.Tests/Storage -S`

## Session Handoff
- Last updated: 2026-04-15
- Active task: none. `RTS-MISS-001` through `RTS-MISS-004` are complete.
- Last delta: replaced S3/Azure storage TODO stubs with real SDK-backed providers, added deterministic in-memory test backends, normalized cancellation/missing-artifact behavior, and added a shared provider parity matrix.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests|FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests|FullyQualifiedName~RecordedTestStorageProviderParityTests" --logger "console;verbosity=minimal"` -> `passed (125/125)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (382/382)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (56/56)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (53/53)`
  - `rg -n "TODO: Implement actual S3|S3 listing not yet implemented|TODO: Implement actual Azure Blob|Azure Blob listing not yet implemented|StoreAsync is not directly implemented" RecordedTests.Shared/Storage -S` -> no matches
  - `rg -n "stubbed|download stub|not yet implemented|requires AWSSDK|requires Azure.Storage.Blobs" Tests/RecordedTests.Shared.Tests/Storage RecordedTests.Shared/Storage -S` -> no matches
- Files changed:
  - `RecordedTests.Shared/RecordedTests.Shared.csproj`
  - `RecordedTests.Shared/Properties/AssemblyInfo.cs`
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/FileSystemRecordedTestStorage.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/InMemoryCloudStorageBackends.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/RecordedTestStorageProviderParityTests.cs`
  - `RecordedTests.Shared/TASKS.md`
  - `RecordedTests.Shared/TASKS_ARCHIVE.md`
- Blockers: none.
- Next command: `Get-Content -Path 'Services/TASKS.md' -TotalCount 360`
