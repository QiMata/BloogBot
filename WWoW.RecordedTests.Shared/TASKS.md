# WWoW.RecordedTests.Shared Tasks

## Scope
- Local ownership: `WWoW.RecordedTests.Shared/*`.
- Master reference: `docs/TASKS.md` (`MASTER-SUB-040`).
- Master tracker: `MASTER-SUB-040`.
- Goal: make WWoW recorded-test storage providers deterministic, fully implemented, and contract-aligned with tests.

## Execution Rules
1. Execute task IDs in order and keep scans limited to files listed in this document.
2. Keep commands one-line and run narrow `--filter` targets before full-project runs.
3. Never blanket-kill `dotnet`; if cleanup is required, use repo-scoped process matching with PID evidence.
4. Do not switch to another local `TASKS.md` until this file has concrete IDs, acceptance criteria, and a complete handoff block.
5. If two consecutive passes produce no delta, log blocker + exact next command, then hand off.
6. Archive completed items to `WWoW.RecordedTests.Shared/TASKS_ARCHIVE.md` in the same session.
7. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
8. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `S3RecordedTestStorage` and `AzureBlobRecordedTestStorage` still contain TODO stubs for upload/download/list/delete operations (`Storage/S3RecordedTestStorage.cs:60`, `:96`, `:122`, `:155`; `Storage/AzureBlobRecordedTestStorage.cs:55`, `:88`, `:109`, `:137`).
- [x] Current WWoW storage tests call static parse/generate helpers that are not exposed by current implementations (`Tests/WWoW.RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs:49`, `:121`, `:157`; `Tests/WWoW.RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs:32`, `:89`, `:125`).
- [x] SDK package requirements are implied in code comments (`AWSSDK.S3`, `Azure.Storage.Blobs`) but are not present in `WWoW.RecordedTests.Shared.csproj` (`NO_S3_AZURE_PACKAGES` evidence command).
- [x] Contract parity with `RecordedTests.Shared` remains required for URI/key/blob semantics to avoid cross-library drift (active `WRTS-PARITY-001` gate).

## Evidence Snapshot (2026-02-25)
- `dotnet restore WWoW.RecordedTests.Shared/WWoW.RecordedTests.Shared.csproj` succeeded (`All projects are up-to-date for restore`).
- `dotnet build WWoW.RecordedTests.Shared/WWoW.RecordedTests.Shared.csproj --configuration Release --no-restore` succeeded (no warnings/errors).
- S3 helper/API drift confirmed:
- tests require `ParseS3Uri`, `GenerateS3Key`, `GenerateS3Uri` (`Tests/.../S3RecordedTestStorageTests.cs:49`, `:121`, `:157`);
- implementation exposes only private parse/sanitize helpers and TODO operation stubs (`Storage/S3RecordedTestStorage.cs:188`, `:174`, `:60`, `:96`, `:122`, `:155`).
- Azure helper/API drift confirmed:
- tests require `ParseAzureBlobUri`, `GenerateBlobName`, `GenerateAzureBlobUri` (`Tests/.../AzureBlobRecordedTestStorageTests.cs:32`, `:89`, `:125`);
- implementation keeps parse/sanitize helpers non-public and operation methods TODO (`Storage/AzureBlobRecordedTestStorage.cs:157`, `:151`, `:55`, `:88`, `:109`, `:137`).
- Dependency gap confirmed:
- code comments reference `AWSSDK.S3` and `Azure.Storage.Blobs` prerequisites (`Storage/S3RecordedTestStorage.cs:27`, `Storage/AzureBlobRecordedTestStorage.cs:27`);
- project file check returned `NO_S3_AZURE_PACKAGES`.
- Test execution blocker confirmed:
- filtered command for `S3RecordedTestStorageTests` currently fails at compile stage due broader WWoW shared test API drift (including missing storage helper symbols and unrelated constructor/signature mismatches), so storage tests are not yet isolatable with current project wiring.

## P0 Active Tasks (Ordered)
1. [ ] `WRTS-CONTRACT-001` Align S3 helper contract between tests and implementation.
- Evidence: tests call `S3RecordedTestStorage.ParseS3Uri`, `GenerateS3Key`, and `GenerateS3Uri`; implementation currently exposes only a private parser and no generator helpers.
- Files: `WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`, `Tests/WWoW.RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`.
- Implementation: expose one explicit helper surface (public or internal static) for S3 URI/key generation and parsing, then enforce consistent validation semantics.
- Acceptance: S3 helper tests pass with deterministic parsing of bucket/key and deterministic key/URI generation.

2. [ ] `WRTS-CONTRACT-002` Align Azure helper contract between tests and implementation.
- Evidence: tests call `AzureBlobRecordedTestStorage.ParseAzureBlobUri`, `GenerateBlobName`, and `GenerateAzureBlobUri`; implementation currently has only an instance-private parser.
- Files: `WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`, `Tests/WWoW.RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`.
- Implementation: expose one explicit helper surface for account/container/blob parsing and URI/blob-name generation with deterministic invalid-input behavior.
- Acceptance: Azure helper tests pass and container/account/blob extraction behavior is stable across valid and invalid URI cases.

3. [ ] `WRTS-MISS-001` Implement S3 provider operations (upload/download/list/delete) and remove TODO-only behavior.
- Evidence: all S3 methods currently log success but are stubbed.
- Files: `WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`, `WWoW.RecordedTests.Shared/WWoW.RecordedTests.Shared.csproj`.
- Implementation: finalize client initialization strategy, add real operation calls (or explicit feature gating if SDK intentionally excluded), and enforce cancellation/input-validation behavior.
- Acceptance: S3 methods execute concrete storage logic and no longer silently return placeholder success paths.

4. [ ] `WRTS-MISS-002` Implement Azure Blob provider operations (upload/download/list/delete) and remove TODO-only behavior.
- Evidence: all Azure methods currently log success but are stubbed.
- Files: `WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`, `WWoW.RecordedTests.Shared/WWoW.RecordedTests.Shared.csproj`.
- Implementation: finalize client initialization strategy, add real blob operations (or explicit feature gating if SDK intentionally excluded), and enforce cancellation/input-validation behavior.
- Acceptance: Azure methods execute concrete storage logic and no longer silently return placeholder success paths.

5. [ ] `WRTS-PARITY-001` Add storage-contract parity coverage against `RecordedTests.Shared` behavior.
- Evidence: naming and URI generation logic can drift across WWoW and non-WWoW shared libraries without a direct test gate.
- Files: `WWoW.RecordedTests.Shared/Storage/*.cs`, `RecordedTests.Shared/Storage/*.cs`, `Tests/WWoW.RecordedTests.Shared.Tests/Storage/*.cs`.
- Implementation: add shared test vectors for sanitized test name, artifact name, timestamps, and expected storage URIs across filesystem/S3/Azure surfaces.
- Acceptance: parity tests fail on format drift and pass when both libraries produce expected deterministic outputs.

6. [ ] `WRTS-DOC-001` Update local storage documentation after contract and provider implementation changes.
- Evidence: README and comments currently describe SDK prerequisites but not the final implemented/feature-gated behavior.
- Files: `WWoW.RecordedTests.Shared/README.md`, `WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`, `WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`.
- Implementation: document final dependency model, supported URI formats, and simple validation commands.
- Acceptance: docs match live code and can be used as low-context handoff references.

## Simple Command Set
1. Build project:
- `dotnet build WWoW.RecordedTests.Shared/WWoW.RecordedTests.Shared.csproj --configuration Release --no-restore`
2. Run S3 storage tests:
- `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"`
3. Run Azure storage tests:
- `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`
4. Run all shared-storage tests:
- `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storage" --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `WRTS-CONTRACT-001` (align S3 helper contract between tests and implementation).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet build WWoW.RecordedTests.Shared/WWoW.RecordedTests.Shared.csproj --configuration Release --no-restore` -> succeeded (`0 warnings`, `0 errors`).
- Files changed: `WWoW.RecordedTests.Shared/TASKS.md`
- Blockers: None.
- Next task: `WRTS-CONTRACT-001`.
- Next command: `Get-Content -Path 'WWoWBot.AI/TASKS.md' -TotalCount 360`
