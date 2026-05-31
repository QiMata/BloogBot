# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-05-30) - PHS-STORY-MGR-TST-001 Storyline management tests

- [x] `PHS-STORY-MGR-TST-001` Added deterministic storyline management coverage.
- Completed:
  - Covered draft create/update/publish for persona profiles.
  - Covered invalid graph publish rejection for missing transition endpoints.
  - Covered invalid gameplay arc publish rejection for unknown ActivityCatalog ids.
  - Covered graph publish snapshot replacement and deletion of removed transitions.
  - Covered character binding publish updates to authoring/runtime storyline records.
  - Covered memory candidate approve/reject status changes and approved memory creation.
  - Covered graph layout round-trip separate from runtime graph semantics.
- Validation:
  - `dotnet restore Tests\PromptHandlingService.Tests\PromptHandlingService.Tests.csproj --verbosity minimal` -> passed.
  - `dotnet build Tests\PromptHandlingService.Tests\PromptHandlingService.Tests.csproj --configuration Debug --no-restore -v:minimal -m:1` -> passed.
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests\PromptHandlingService.Tests\PromptHandlingService.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Storyline" --logger "console;verbosity=minimal"` -> passed (21 passed, 1 skipped).
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests\PromptHandlingService.Tests\PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (36 passed, 3 skipped).

## Archived Snapshot (2026-05-24) - PHS-FDRY-LIVE-001 Foundry live smoke verification

- [x] `PHS-FDRY-LIVE-001` Verified the deployed Foundry persona runtime and storyline runtime against the configured dev project.
- Completed:
  - Temporarily enabled the statically skipped direct Foundry and storyline live smoke tests.
  - Verified the direct Foundry runtime returns the required minified JSON output contract from the configured project.
  - Verified the seeded storyline runtime can call the configured prompt agent and produce persona dialogue.
  - Restored both live smoke tests to static skip gates after validation.
  - Aligned live smoke timeout settings to 30 seconds.
- Validation:
  - `$env:DOTNET_ROLL_FORWARD='Major'; $env:WWOW_FOUNDRY_LIVE='1'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~StorylinePersonaLiveSmokeTests.SeededStorylineRuntimeSmoke_UsesConfiguredFoundryProject|FullyQualifiedName~FoundryPersonaLiveSmokeTests.DirectModelResponseSmoke_UsesConfiguredFoundryProject" --logger "console;verbosity=minimal"` -> passed after temporarily enabling the two statically skipped live tests (2 passed, 0 failed).
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (29 passed, 3 skipped).

## Archived Snapshot (2026-05-24) - PHS-STORY-TST-001 Foundry storyline graph runtime tests

- [x] `PHS-STORY-TST-001` Added deterministic storyline graph runtime tests.
- Completed:
  - Covered SQLite schema creation and seed import idempotency.
  - Covered repository round-trips for personas, narrative graph/node/transition, approved memory, pending memory candidates, agent binding, and conversation binding.
  - Covered context resolver prompt assembly, outbound transition loading, missing character state/persona version/narrative node/agent binding failures, and memory summary ordering/size limits.
  - Covered runtime pending-memory-candidate storage only, disallowed Foundry intent fallback, per-persona binding propagation, config/seed secret scan, and a skipped live storyline smoke gate.
- Validation:
  - `dotnet build Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore` -> passed (0 warnings, 0 errors).
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (29 passed, 3 skipped on final run).

## Archived Snapshot (2026-05-24) - PHS-FDRY-TST-001 Foundry persona runtime adapter tests

- [x] `PHS-FDRY-TST-001` Added deterministic Foundry persona runtime adapter tests.
- Completed:
  - Covered Foundry runtime option validation for project endpoint, HTTPS enforcement, model, agent name, timeout, and output token bounds.
  - Covered deterministic persona prompt assembly ordering, advisory boundary text, and missing persona/node/input validation.
  - Covered fake-client runtime behavior for normal JSON response parsing, timeout, auth failure propagation, invalid JSON, and missing JSON fields.
  - Added secret-leakage checks for non-secret Foundry config/metadata and skipped live smoke tests for direct model response and prompt-agent version creation.
- Validation:
  - `dotnet restore Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj` -> passed.
  - `dotnet restore Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --runtime win-x86` -> passed.
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> failed before test execution because x64 .NET 8 runtime is not installed on this host.
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (14 passed, 2 skipped).
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --arch x86 --filter "FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (14 passed, 2 skipped).

## Archived Snapshot (2026-04-15) - DecisionEngine runtime test host coverage

- [x] Added DecisionEngine runtime startup coverage in this project because it already references `Services/DecisionEngineService`.
- Completed:
  - Added `DecisionEngineRuntimeTests` for config defaults, config overrides, data/processed directory write-probe behavior, SQLite schema creation, and runtime creation of `CombatPredictionService` plus `CombatModelServiceListener`.
- Validation:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"` -> `passed (31 passed, 161 skipped, 0 failed, 192 total)`

## Archived Snapshot (2026-04-15) - PHS-TST-002 deterministic prompt split

- [x] `PHS-TST-002` Split deterministic unit prompt tests from live-model integration path.
- Completed:
  - Added `ScriptedPromptRunner`, a local `IPromptRunner` test double that records chat-history calls and returns scripted responses.
  - Converted `IntentionParserFunctionTests`, `GMCommandGeneratorFunctionTests`, and `CharacterSkillPrioritizationFunctionTests` to deterministic, offline `[Fact]` tests.
  - Isolated live Ollama coverage into `Category=Integration` test classes:
    - `IntentionParserOllamaIntegrationTests` -> `http://localhost:11434`, model `deepseek-r1`
    - `GMCommandGeneratorOllamaIntegrationTests` -> `http://localhost:11434`, model `llama3`
    - `CharacterSkillPrioritizationOllamaIntegrationTests` -> `http://localhost:11434`, model `deepseekr1:14b`
  - Updated `README.md` and `TASKS.md` with deterministic and integration commands.
- Validation:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"` -> `passed (27 passed, 161 skipped, 0 failed, 188 total)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~IntentionParserFunctionTests|FullyQualifiedName~GMCommandGeneratorFunctionTests|FullyQualifiedName~CharacterSkillPrioritizationFunctionTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
