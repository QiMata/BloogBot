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

## Current Status (2026-05-24)
- Known remaining owner-local items: `0`.
- Default prompt-function tests use `ScriptedPromptRunner` and are deterministic/offline.
- Ollama prompt tests are isolated behind `Category=Integration`.
- Additional DecisionEngine runtime tests live here because this test project already references `DecisionEngineService`.
- Foundry persona runtime tests use a fake Foundry client by default; live smoke coverage is statically skipped and opt-in via environment variables.
- Completed item details are archived in `Tests/PromptHandlingService.Tests/TASKS_ARCHIVE.md`.

## Completed P0 Items
- [x] `PHS-TST-001` Fix xUnit discovery gaps in prompt function tests.
- [x] `PHS-TST-002` Split deterministic unit prompt tests from live-model integration path.
- [x] `PHS-TST-003` Add contract tests for `PromptFunctionBase.TransferHistory` behavior (`PHS-MISS-001` guard).
- [x] `PHS-TST-004` Convert legacy repository tests into discoverable and bounded execution slices.
- [x] `PHS-TST-005` Simplify command surface and align README with timeout-safe defaults.
- [x] `PHS-FDRY-TST-001` Add deterministic Foundry persona runtime adapter tests for options validation, prompt assembly, fake-client runtime parsing/error paths, secret leakage, and skipped live smoke gates.
- [x] `PHS-STORY-TST-001` Add deterministic Foundry storyline graph runtime tests for schema, seed, repository, resolver, runtime guardrails, binding propagation, and secret scanning.
- [x] `PHS-STORY-MGR-TST-001` Add deterministic storyline management tests for drafts, publish validation, graph snapshot replacement, gameplay arcs, character bindings, memory review, and graph layout.

## Open Tasks
- None.

## Simple Command Set
1. Default deterministic run: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"`.
2. Prompt function deterministic focus: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~IntentionParserFunctionTests|FullyQualifiedName~GMCommandGeneratorFunctionTests|FullyQualifiedName~CharacterSkillPrioritizationFunctionTests" --logger "console;verbosity=minimal"`.
3. Transfer-history contract focus: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~TransferHistory|FullyQualifiedName~PromptFunctionBase" --logger "console;verbosity=minimal"`.
4. Repository smoke/integration focus: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~MangosRepositoryTest" --logger "console;verbosity=minimal"`.
5. Prompt Ollama integration opt-in: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category=Integration" --logger "console;verbosity=minimal"`.
6. Foundry deterministic focus: `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-05-31
- Active task: none. Foundry metadata and seed-dataset contract checks are in place.
- Last delta: added deterministic checks that Foundry metadata uses `evaluationSuites[]`, accepts quoted YAML scalar values, and that the v1 seed dataset contains `query` plus `expected_behavior` rows.
- Pass result: `delta shipped`
- Validation/tests run:
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests\PromptHandlingService.Tests\PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (38 passed, 3 skipped)
- Files changed:
  - `Tests/PromptHandlingService.Tests/FoundryPersonaRuntimeTests.cs`
  - `Tests/PromptHandlingService.Tests/TASKS.md`
- Blockers: none
- Next command: `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"`
