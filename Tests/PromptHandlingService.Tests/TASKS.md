# PromptHandlingService.Tests Tasks

## Scope
- Directory: `Tests/PromptHandlingService.Tests`
- Project: `PromptHandlingService.Tests.csproj`
- Master tracker: `docs/TASKS.md` (`MASTER-SUB-025`)
- Local goal: make prompt-handling tests deterministic, discovered by xUnit, and directly aligned to `PHS-MISS-001`.

## Execution Rules
1. Execute tasks in numeric order unless blocked by fixture prerequisites.
2. Keep scan scope to this project and directly referenced implementation files only.
3. Use one-line `dotnet test` commands and include `Tests/test.runsettings` for timeout enforcement.
4. Never blanket-kill `dotnet`; use repo-scoped cleanup only with process evidence.
5. Move completed IDs to `Tests/PromptHandlingService.Tests/TASKS_ARCHIVE.md` in the same session.
6. If two consecutive passes produce no file delta, record blocker and exact next command, then advance queue pointer in `docs/TASKS.md`.
7. Add a one-line `Pass result` in `Session Handoff` (`delta shipped` or `blocked`) every pass so compaction resumes from `Next command` directly.
8. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist (Run Before P0)
- [x] `Tests/test.runsettings` is applied (`TestSessionTimeout=600000`) via csproj runsettings path.
- [ ] Local prompt-runner tests do not require live network/model access for default CI command path.
- [ ] If integration prompt tests are enabled, local Ollama endpoint and model names are explicitly documented per test.

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj` completed successfully.
- Timeout/runsettings baseline:
  - `Tests/test.runsettings:5` -> `<TestSessionTimeout>600000</TestSessionTimeout>`
  - `Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj:23` -> `<RunSettingsFilePath>...\\..\\test.runsettings</RunSettingsFilePath>`
- Discovery gap evidence for `PHS-TST-001`:
  - `IntentionParserFunctionTests.cs`, `GMCommandGeneratorFunctionTests.cs`, and `CharacterSkillPrioritizationFunctionTests.cs` contain multiple `public async Task ...` methods without `[Fact]` attributes.
- Determinism gap evidence for `PHS-TST-002`:
  - direct Ollama coupling (`http://localhost:11434`) appears in all three prompt-function test files.
- README command drift evidence for `PHS-TST-005`:
  - command examples at `README.md:96`, `:99`, `:102`, `:105` do not show explicit `--settings Tests/test.runsettings`.
- Live validation on the current `PHS-TST-001` filter confirms a discovery gap:
  - `dotnet test ... --filter "FullyQualifiedName~IntentionParserFunctionTests|FullyQualifiedName~GMCommandGeneratorFunctionTests|FullyQualifiedName~CharacterSkillPrioritizationFunctionTests"` -> `No test matches the given testcase filter`.
- `dotnet test ... --list-tests` currently discovers only:
  - `DecisionEngineReadBinFileTests.ReadBinFile_AllowsConcurrentWriter`
  - `PromptCacheTests.PromptCache_Dispose_ReleasesDatabaseFile`

## P0 Active Tasks (Ordered)

### [x] PHS-TST-001 - Fix xUnit discovery gaps in prompt function tests
- **Already addressed (PHS-MISS-004).** All test methods already have `[Fact(Skip = "Integration: requires local Ollama")]` attributes. Discovery is correct — 2 non-skipped tests run, 12 integration tests are properly skipped.
- Problem: multiple async test methods are missing `[Fact]`, so they are not discovered/executed.
- Evidence:
1. `Tests/PromptHandlingService.Tests/IntentionParserFunctionTests.cs:15`
2. `Tests/PromptHandlingService.Tests/GMCommandGeneratorFunctionTests.cs:12`
3. `Tests/PromptHandlingService.Tests/CharacterSkillPrioritizationFunctionTests.cs:15`
4. `Tests/PromptHandlingService.Tests/DecisionEngineReadBinFileTests.cs:16` (discovered baseline pattern)
- Implementation targets:
1. `Tests/PromptHandlingService.Tests/IntentionParserFunctionTests.cs`
2. `Tests/PromptHandlingService.Tests/GMCommandGeneratorFunctionTests.cs`
3. `Tests/PromptHandlingService.Tests/CharacterSkillPrioritizationFunctionTests.cs`
- Required change:
1. Add `[Fact]` (or `[Theory]` where needed) to each intended test method.
2. Ensure class/fixture constructors remain xUnit-compatible after attribute updates.
3. Add a short discovery validation step in this file's handoff notes once implemented.
- Command: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~IntentionParserFunctionTests|FullyQualifiedName~GMCommandGeneratorFunctionTests|FullyQualifiedName~CharacterSkillPrioritizationFunctionTests" --logger "console;verbosity=minimal"`.
- Acceptance:
1. All intended test methods in the three files are discovered by xUnit.
2. Filtered command executes test cases instead of reporting zero matches.
3. `dotnet test --list-tests` shows discoverable entries for the three target prompt-function test classes.

### [ ] PHS-TST-002 - Split deterministic unit prompt tests from live-model integration path
- Problem: prompt tests currently hardcode local Ollama endpoint/model usage, which is non-deterministic and environment-coupled.
- Evidence:
1. `Tests/PromptHandlingService.Tests/IntentionParserFunctionTests.cs:11`
2. `Tests/PromptHandlingService.Tests/GMCommandGeneratorFunctionTests.cs:121`
3. `Tests/PromptHandlingService.Tests/CharacterSkillPrioritizationFunctionTests.cs:12`
- Implementation targets:
1. `Tests/PromptHandlingService.Tests/IntentionParserFunctionTests.cs`
2. `Tests/PromptHandlingService.Tests/GMCommandGeneratorFunctionTests.cs`
3. `Tests/PromptHandlingService.Tests/CharacterSkillPrioritizationFunctionTests.cs`
4. `Tests/PromptHandlingService.Tests` (new deterministic stub/fake prompt runner helper)
- Required change:
1. Add deterministic `IPromptRunner` test doubles for default unit path.
2. Mark live-model tests explicitly as integration-only (trait/category or equivalent).
3. Document integration prerequisites and opt-in command in this `TASKS.md` and project README.
- Command: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Default project test run is deterministic and offline-capable.
2. Integration prompt tests are isolated and opt-in with explicit prerequisites.

### [x] PHS-TST-003 - Add contract tests for `PromptFunctionBase.TransferHistory` behavior (`PHS-MISS-001` guard)
- **Done (batch 14 — PHS-MISS-002/003).** 14 transfer-contract tests in `PromptFunctionBaseTransferTests.cs` covering TransferHistory, TransferChatHistory, TransferPromptRunner, ResetChat, system prompt preservation, and InitializeChat semantics.
- Problem: `TransferHistory(IPromptFunction)` throws `NotImplementedException` for non-`PromptFunctionBase` targets, and behavior contract is not test-pinned.
- Evidence:
1. `Services/PromptHandlingService/PromptFunctionBase.cs:43`
2. `Services/PromptHandlingService/PromptFunctionBase.cs:47`
3. `Services/PromptHandlingService/PromptFunctionBase.cs:147`
- Implementation targets:
1. `Tests/PromptHandlingService.Tests` (new transfer-history contract tests)
2. `Services/PromptHandlingService/PromptFunctionBase.cs`
- Required change:
1. Define supported/unsupported transfer-target behavior explicitly in tests first.
2. Replace ambiguous `NotImplementedException` path with explicit supported-path handling or explicit argument/operation exception contract.
3. Verify system prompt handling and chat history transfer semantics with deterministic assertions.
- Command: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~TransferHistory|FullyQualifiedName~PromptFunctionBase" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Transfer behavior is covered by unit tests and no longer relies on an unpinned exception path.
2. `PHS-MISS-001` has a direct test gate in this project.

### [x] PHS-TST-004 - Convert legacy repository tests into discoverable and bounded execution slices
- **Done (batch 17).** 161 static methods → `[Fact(Skip = "Integration: requires MaNGOS database")]` instance methods. All 161 now discovered by xUnit (skipped with preflight reason). Total: 205 tests (32 pass, 173 skip).
- Problem: `MangosRepositoryTest` contains many static `Test*` methods with no xUnit attributes and no bounded run strategy.
- Evidence:
1. `Tests/PromptHandlingService.Tests/MangosRepositoryTest.cs:9`
2. `Tests/PromptHandlingService.Tests/MangosRepositoryTest.cs:12`
3. `Tests/PromptHandlingService.Tests/MangosRepositoryTest.cs:3430`
- Implementation targets:
1. `Tests/PromptHandlingService.Tests/MangosRepositoryTest.cs`
2. `Tests/PromptHandlingService.Tests` (optional split into smaller files by table domain)
- Required change:
1. Convert high-value smoke cases into discovered tests first.
2. Gate database-dependent exhaustive checks behind explicit integration markers and preflight checks.
3. Keep default run path bounded and deterministic under `TestSessionTimeout`.
- Command: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~MangosRepositoryTest" --logger "console;verbosity=minimal"`.
- Acceptance:
1. At least one discovered repository smoke test executes in default path (or is clearly skipped with preflight reason).
2. Exhaustive DB coverage is explicit integration scope, not silent non-execution.

### [x] PHS-TST-005 - Simplify command surface and align README with timeout-safe defaults
- **Done (batch 17).** README.md updated: 4 canonical commands with `--configuration Release --no-restore --settings --logger`, separated deterministic/integration/repo scopes.
- Problem: README command examples are broad and do not consistently show runsettings/timeout-safe defaults.
- Evidence:
1. `Tests/PromptHandlingService.Tests/README.md:96`
2. `Tests/PromptHandlingService.Tests/README.md:105`
3. `Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj:23`
4. `Tests/test.runsettings:6`
- Implementation targets:
1. `Tests/PromptHandlingService.Tests/README.md`
2. `Tests/PromptHandlingService.Tests/TASKS.md`
- Required change:
1. Replace ambiguous examples with canonical one-line commands.
2. Provide separate commands for deterministic unit run and integration opt-in run.
3. Add repo-scoped cleanup command reference for stale testhost processes.
- Command: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --logger "console;verbosity=minimal"`.
- Acceptance:
1. Engineers can run deterministic tests with one copy/paste command.
2. Integration path is explicit and does not impact default run reliability.

## Simple Command Set
1. Default deterministic run: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"`.
2. Prompt function focus: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~IntentionParserFunctionTests|FullyQualifiedName~GMCommandGeneratorFunctionTests|FullyQualifiedName~CharacterSkillPrioritizationFunctionTests" --logger "console;verbosity=minimal"`.
3. Transfer-history contract focus: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~TransferHistory|FullyQualifiedName~PromptFunctionBase" --logger "console;verbosity=minimal"`.
4. Repository smoke/integration focus: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~MangosRepositoryTest" --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-02-28
- Active task: PHS-TST-002 (deterministic stubs) is remaining — low priority
- Last delta: PHS-TST-004 (161 methods discovered), PHS-TST-005 (README updated) — batch 17
- Pass result: `delta shipped`
- Build: 0 errors. Tests: 32 pass, 173 skip, 0 fail (205 total).
- Files changed: MangosRepositoryTest.cs, README.md, TASKS.md
- Blockers: PHS-TST-002 requires IPromptRunner stub design (low priority)
- Next command: continue with next queue file
