# WWoW.RecordedTests.Shared.Tests Tasks

## Scope
- Directory: `Tests/WWoW.RecordedTests.Shared.Tests`
- Project: `Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj`
- Master tracker: `MASTER-SUB-033`
- Primary test surfaces:
- `Tests/WWoW.RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`
- `Tests/WWoW.RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`
- `Tests/WWoW.RecordedTests.Shared.Tests/RecordedTestRunnerTests.cs`
- Primary implementation surfaces:
- `WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`
- `WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`
- `WWoW.RecordedTests.Shared/Storage/FileSystemRecordedTestStorage.cs`

## Execution Rules
1. Execute tasks in this file top-down and keep scans limited to the files listed in `Scope`.
2. Keep commands simple and one-line; run narrow `--filter` commands before full-project runs.
3. Do not switch to another local `TASKS.md` until this file has concrete IDs, acceptance, and handoff metadata.
4. Never blanket-kill `dotnet`; if cleanup is needed, use repo-scoped process matching and record PID-level evidence.
5. Move completed items to `Tests/WWoW.RecordedTests.Shared.Tests/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `WWoW.RecordedTests.Shared.Tests.csproj:24` sets `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] `WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:60-188` still contains S3 TODO stubs and a private `ParseS3Uri`.
- [x] `WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:55-157` still contains Azure TODO stubs and a private instance `ParseAzureBlobUri`.
- [x] Storage tests currently call static helper APIs not exposed by implementation (`S3RecordedTestStorageTests.cs`, `AzureBlobRecordedTestStorageTests.cs`).
- [x] `RecordedTestRunnerTests` currently validates happy-path persistence only; cancellation/failure teardown coverage is still thin.

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj` succeeded (`All projects are up-to-date for restore`).
- `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` failed at compile stage before test execution.
- Compile-blocker groups captured from that run:
- Helper API drift: missing `S3RecordedTestStorage.ParseS3Uri/GenerateS3Key/GenerateS3Uri` and missing `AzureBlobRecordedTestStorage.ParseAzureBlobUri/GenerateBlobName/GenerateAzureBlobUri` (`CS0117`).
- Signature drift: missing `cancellationToken` args and outdated ctor/signature usage in multiple tests (`CS7036`, `CS1729`, `CS1739`, `CS1503`).
- Docker CLI contract drift: missing `IDockerCli.ExecuteAsync` in tests (`CS1061`).
- Runsettings and timeout wiring confirmed at `WWoW.RecordedTests.Shared.Tests.csproj:24` and `Tests/test.runsettings:5`.
- S3 helper/API drift is explicit:
- Tests call `S3RecordedTestStorage.ParseS3Uri`, `GenerateS3Key`, `GenerateS3Uri` (`Tests/WWoW.RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs:49`, `:121`, `:157`).
- Implementation only exposes private `ParseS3Uri` and TODO storage stubs (`WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:60`, `:96`, `:122`, `:155`, `:188`).
- Azure helper/API drift is explicit:
- Tests call `AzureBlobRecordedTestStorage.ParseAzureBlobUri`, `GenerateBlobName`, `GenerateAzureBlobUri` (`Tests/WWoW.RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs:32`, `:89`, `:125`).
- Implementation exposes private instance parser + TODO stubs (`WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:55`, `:88`, `:109`, `:137`, `:157`).
- `RecordedTestRunner` currently persists artifacts/metadata in happy path (`WWoW.RecordedTests.Shared/RecordedTestRunner.cs:67-80`), and test coverage currently centers on `RunAsync_PersistsArtifactsAndMetadata` (`Tests/WWoW.RecordedTests.Shared.Tests/RecordedTestRunnerTests.cs:19`).

## P0 Active Tasks (Ordered)
1. [ ] `WRTS-TST-000` Restore compile baseline for `WWoW.RecordedTests.Shared.Tests` so filtered suites can execute.
- Evidence: current filtered run fails before test discovery with widespread API/signature drift errors (`CS0117`, `CS7036`, `CS1729`, `CS1739`, `CS1503`, `CS1061`).
- Files: `Tests/WWoW.RecordedTests.Shared.Tests/**/*.cs`, `WWoW.RecordedTests.Shared/**/*.cs`.
- Required breakdown: reconcile test code to current runtime contracts (constructor/signature/cancellation token updates), then rerun filtered S3/Azure commands and capture first runtime failures after compile is green.
- Validation: `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`.

2. [ ] `WRTS-TST-001` Resolve S3 helper API drift between tests and implementation.
- Evidence: S3 tests invoke `ParseS3Uri`, `GenerateS3Key`, and `GenerateS3Uri`, but implementation currently exposes only a private parser.
- Files: `Tests/WWoW.RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`, `WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`.
- Required breakdown: decide target API contract (public static helpers vs instance-only behavior), align tests and implementation to the same contract, and enforce exact URI/key format expectations.
- Validation: `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"`.

3. [ ] `WRTS-TST-002` Resolve Azure helper API drift between tests and implementation.
- Evidence: Azure tests invoke `ParseAzureBlobUri`, `GenerateBlobName`, and `GenerateAzureBlobUri`, but implementation currently has only an instance-private parser.
- Files: `Tests/WWoW.RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`, `WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`.
- Required breakdown: define one parsing/generation contract (account/container/blob extraction + URI generation), align method visibility and signatures, and lock in valid/invalid URI behaviors.
- Validation: `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`.

4. [ ] `WRTS-TST-003` Add deterministic tests for S3 storage operation semantics while provider implementation is stubbed.
- Evidence: upload/download/list/delete methods return success paths but are not backed by SDK calls yet.
- Files: `Tests/WWoW.RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`, `WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`.
- Required breakdown: assert deterministic return URI format, path sanitization behavior, input validation exceptions, and cancellation token forwarding for future SDK integration.
- Validation: `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"`.

5. [ ] `WRTS-TST-004` Add deterministic tests for Azure Blob storage operation semantics while provider implementation is stubbed.
- Evidence: upload/download/list/delete methods are placeholders and need contract-level tests before SDK wiring.
- Files: `Tests/WWoW.RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`, `WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`.
- Required breakdown: assert generated blob URI shape, blob-name sanitization and hierarchy behavior, container mismatch failures, and deterministic empty-list behavior until provider integration lands.
- Validation: `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`.

6. [ ] `WRTS-TST-005` Expand `RecordedTestRunner` coverage for cancellation/failure teardown and metadata persistence.
- Evidence: current runner test verifies artifact/metadata persistence but not timeout/cancellation/failure paths.
- Files: `Tests/WWoW.RecordedTests.Shared.Tests/RecordedTestRunnerTests.cs`, `WWoW.RecordedTests.Shared/RecordedTestRunner.cs`.
- Required breakdown: add tests for canceled token propagation, storage failure behavior, and deterministic metadata keys/values on partial failures.
- Validation: `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~RecordedTestRunnerTests" --logger "console;verbosity=minimal"`.

7. [ ] `WRTS-TST-006` Add cross-backend storage contract tests to keep filesystem/S3/Azure naming behavior aligned.
- Evidence: sanitization logic differs by backend and currently has no shared contract gate in this test project.
- Files: `Tests/WWoW.RecordedTests.Shared.Tests/Storage/*.cs`, `WWoW.RecordedTests.Shared/Storage/*.cs`.
- Required breakdown: introduce shared test vectors for test-name/artifact-name inputs and assert expected path/key/blob outputs per backend so drift is caught early.
- Validation: `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storage" --logger "console;verbosity=minimal"`.

## Simple Command Set
- `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~RecordedTestRunnerTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `WRTS-TST-000` (restore compile baseline before filtered storage tests can run).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` -> blocked by compile errors (no tests executed).
- Files changed: `Tests/WWoW.RecordedTests.Shared.Tests/TASKS.md`.
- Blockers: Test project does not currently compile due cross-suite API/signature drift.
- Next task: `WRTS-TST-000`.
- Next command: `Get-Content -Path 'Tests/WWoW.Tests.Infrastructure/TASKS.md' -TotalCount 360`.
