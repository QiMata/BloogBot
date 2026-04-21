# PromptHandlingService.Tests Tasks

## Scope
- Directory: `Tests/PromptHandlingService.Tests`
- Project: `PromptHandlingService.Tests.csproj`
- Master tracker: `docs/TASKS.md`
- Local goal: keep prompt-handling tests deterministic, discovered by xUnit, and directly aligned to `PHS-MISS-001`.

## Execution Rules
1. Keep scan scope to this project and directly referenced implementation files only.
2. Use one-line `dotnet test` commands and include `Tests/test.runsettings` for timeout enforcement.
3. Never blanket-kill `dotnet`; use repo-scoped cleanup only with process evidence.
4. Move completed IDs to `Tests/PromptHandlingService.Tests/TASKS_ARCHIVE.md` in the same session.
5. Add a one-line `Pass result` in `Session Handoff` (`delta shipped` or `blocked`) every pass.
6. Keep `Session Handoff -> Next command` executable.

## Environment Checklist
- [x] `Tests/test.runsettings` is applied (`TestSessionTimeout=600000`) via csproj runsettings path.
- [x] Local prompt-runner tests do not require live network/model access for default CI command path.
- [x] If integration prompt tests are enabled, local Ollama endpoint and model names are explicitly documented per test.

## Current Status (2026-04-15)
- Known remaining owner-local items: `0`.
- Default prompt-function tests use `ScriptedPromptRunner` and are deterministic/offline.
- Ollama prompt tests are isolated behind `Category=Integration`.
- Additional DecisionEngine runtime tests live here because this test project already references `DecisionEngineService`.
- Completed item details are archived in `Tests/PromptHandlingService.Tests/TASKS_ARCHIVE.md`.

## Completed P0 Items
- [x] `PHS-TST-001` Fix xUnit discovery gaps in prompt function tests.
- [x] `PHS-TST-002` Split deterministic unit prompt tests from live-model integration path.
- [x] `PHS-TST-003` Add contract tests for `PromptFunctionBase.TransferHistory` behavior (`PHS-MISS-001` guard).
- [x] `PHS-TST-004` Convert legacy repository tests into discoverable and bounded execution slices.
- [x] `PHS-TST-005` Simplify command surface and align README with timeout-safe defaults.

## Open Tasks
- None.

## Simple Command Set
1. Default deterministic run: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"`.
2. Prompt function deterministic focus: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~IntentionParserFunctionTests|FullyQualifiedName~GMCommandGeneratorFunctionTests|FullyQualifiedName~CharacterSkillPrioritizationFunctionTests" --logger "console;verbosity=minimal"`.
3. Transfer-history contract focus: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~TransferHistory|FullyQualifiedName~PromptFunctionBase" --logger "console;verbosity=minimal"`.
4. Repository smoke/integration focus: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~MangosRepositoryTest" --logger "console;verbosity=minimal"`.
5. Prompt Ollama integration opt-in: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category=Integration" --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-04-15
- Active task: none. `PHS-TST-001` through `PHS-TST-005` are complete.
- Last delta: added `DecisionEngineRuntimeTests` coverage for `Services/DecisionEngineService` startup preflight.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"` -> `passed (27 passed, 161 skipped, 0 failed, 188 total)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~IntentionParserFunctionTests|FullyQualifiedName~GMCommandGeneratorFunctionTests|FullyQualifiedName~CharacterSkillPrioritizationFunctionTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"` -> `passed (31 passed, 161 skipped, 0 failed, 192 total)`
- Files changed:
  - `Tests/PromptHandlingService.Tests/DecisionEngineRuntimeTests.cs`
  - `Tests/PromptHandlingService.Tests/ScriptedPromptRunner.cs`
  - `Tests/PromptHandlingService.Tests/IntentionParserFunctionTests.cs`
  - `Tests/PromptHandlingService.Tests/GMCommandGeneratorFunctionTests.cs`
  - `Tests/PromptHandlingService.Tests/CharacterSkillPrioritizationFunctionTests.cs`
  - `Tests/PromptHandlingService.Tests/README.md`
  - `Tests/PromptHandlingService.Tests/TASKS.md`
  - `Tests/PromptHandlingService.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Blockers: none
- Next command: `rg -n "^- \[ \]" --glob TASKS.md`
