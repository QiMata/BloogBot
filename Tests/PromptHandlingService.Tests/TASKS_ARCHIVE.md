# Task Archive

Completed items moved from TASKS.md.

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
