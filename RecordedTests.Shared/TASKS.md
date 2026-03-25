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
- [ ] Provider configuration inputs are explicit (filesystem root, S3 bucket/region, Azure container/account).
- [ ] Integration tests use deterministic mock/stub paths when cloud credentials are absent.
- [ ] Artifact naming conventions are documented and shared across all providers.

## Evidence Snapshot (2026-02-25)
- S3 implementation remains stubbed:
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:30` logs `StoreAsync` not directly implemented.
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:49`, `:85`, `:111`, `:144` contain TODO placeholders for upload/download/list/delete.
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:130` logs S3 listing not implemented.
- Azure Blob implementation remains stubbed:
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:28` logs `StoreAsync` not directly implemented.
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:47`, `:80`, `:101`, `:129` contain TODO placeholders for upload/download/list/delete.
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:115` logs Azure listing not implemented.
- Contract-shape divergence risk is real because filesystem provider already throws/handles concrete semantics:
  - `RecordedTests.Shared/Storage/FileSystemRecordedTestStorage.cs:130` throws `FileNotFoundException` for missing artifact download.
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:211` enforces configured container match.
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:180` parses and validates `s3://` URI contract.
- Tests currently emphasize helper/config parsing rather than real cloud CRUD behavior:
  - `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs:167` notes operations are stubbed.
  - `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs:149` notes operations are stubbed.
- Test discovery baseline in this shell:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --list-tests` lists URI/config/helper tests plus filesystem CRUD tests, but no real S3/Azure CRUD execution tests.

## P0 Active Tasks (Ordered)

### RTS-MISS-001 Implement real S3 storage operations
- [ ] Problem: S3 provider operations are TODO stubs and currently do not execute actual backend operations.
- [ ] Target files:
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`
  - `RecordedTests.Shared/RecordedTests.Shared.csproj` (package references and wiring if required)
- [ ] Required change: add concrete S3 client wiring, implement upload/download/list/delete with deterministic missing-object and cancellation behavior.
- [ ] Validation command: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: no TODO operation stubs remain for S3 methods and contract tests assert deterministic results.

### RTS-MISS-002 Implement real Azure Blob storage operations
- [ ] Problem: Azure provider operations are TODO stubs and currently do not execute actual backend operations.
- [ ] Target files:
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`
  - `RecordedTests.Shared/RecordedTests.Shared.csproj` (package references and wiring if required)
- [ ] Required change: add concrete Azure Blob client wiring, implement upload/download/list/delete with deterministic missing-object and cancellation behavior.
- [ ] Validation command: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: no TODO operation stubs remain for Azure methods and contract tests assert deterministic results.

### RTS-MISS-003 Normalize cancellation and failure semantics across providers
- [ ] Problem: filesystem provider already enforces concrete error behavior while cloud providers remain placeholders, creating drift risk.
- [ ] Target files:
  - `RecordedTests.Shared/Storage/FileSystemRecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`
  - `RecordedTests.Shared/Abstractions/I/IRecordedTestStorage.cs`
- [ ] Required change: define and enforce consistent error/cancellation semantics (`not found`, invalid URI/container mismatch, cancellation propagation) across all providers.
- [ ] Validation command: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests|FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: provider differences are backend transport only; behavior contracts are equivalent under the same scenarios.

### RTS-MISS-004 Add provider contract parity tests
- [ ] Problem: existing S3/Azure test suites focus on URI/config helpers and explicitly note stubbed operations.
- [ ] Target files:
  - `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/FileSystemRecordedTestStorageTests.cs`
- [ ] Required change: add shared parity matrix covering upload/download/list/delete, missing artifacts, invalid location parsing, and cancellation behavior.
- [ ] Validation command: `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~RecordedTestStorageTests|FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests|FullyQualifiedName~FileSystemRecordedTestStorageTests" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: parity tests fail on semantic drift and pass only when provider contracts align.

## Simple Command Set
1. `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
2. `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storage|FullyQualifiedName~Provider" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --list-tests`
4. `rg -n "TODO: Implement actual S3|S3 listing not yet implemented|TODO: Implement actual Azure Blob|Azure Blob listing not yet implemented" RecordedTests.Shared/Storage -S`
5. `rg -n "stubbed|S3RecordedTestStorageTests|AzureBlobRecordedTestStorageTests" Tests/RecordedTests.Shared.Tests/Storage -S`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `MASTER-SUB-011` (`RecordedTests.Shared/TASKS.md`)
- Current focus: `RTS-MISS-001`
- Last delta: added evidence-backed S3/Azure stub inventory, contract-drift risks, and direct parity-validation commands for storage provider implementation order.
- Pass result: `delta shipped`
- Validation/tests run:
  - `rg -n "TODO: Implement actual S3|S3 listing not yet implemented|StoreAsync is not directly implemented|TODO: Implement actual Azure Blob|Azure Blob listing not yet implemented|StoreAsync is not directly implemented for Azure" RecordedTests.Shared/Storage -S`
  - `rg -n "S3RecordedTestStorageTests|AzureBlobRecordedTestStorageTests|stub" Tests/RecordedTests.Shared.Tests/Storage -S`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --list-tests`
- Files changed:
  - `RecordedTests.Shared/TASKS.md`
- Next command: `Get-Content -Path 'Services/TASKS.md' -TotalCount 360`
- Loop Break: if no file delta after two passes, record blocker and exact next command, then advance queue pointer in `docs/TASKS.md`.
