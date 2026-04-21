# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - DES-MISS-006 runtime startup closeout

- [x] `DES-MISS-006` Wire service runtime composition, startup preflight, and schema verification.
- Completed:
  - Added config-backed `DecisionEngineRuntimeOptions` with runnable defaults and overrides for enablement, data/processed directories, SQLite connection string, listener IP, and listener port.
  - Added `DecisionEngineRuntimeInitializer` to create and write-probe data/processed directories, initialize SQLite native support, create SQLite parent directories, and create `TrainedModel`/`ModelWeights` tables when missing.
  - Added `DecisionEngineRuntime` to own `CombatPredictionService` plus `CombatModelServiceListener` lifetime and clean up prediction service if listener startup fails.
  - Updated `DecisionEngineWorker` to compose the runtime from configuration instead of idling with no prediction/listener wiring.
  - Hardened `CombatPredictionService.UpdateModel()` so an empty model table leaves prediction unavailable explicitly instead of creating a prediction engine from `null`.
  - Reworked `CombatModelServiceListener` to avoid primary-constructor logger capture warnings and keep explicit prediction-service ownership.
  - Added direct runtime tests for defaults, overrides, writable directory/schema preflight, and prediction-service/listener startup.
  - Updated the README runtime configuration section.
- Validation:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet build Services/DecisionEngineService/DecisionEngineService.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`

## Archived Snapshot (2026-04-15) - DES-MISS-001 through DES-MISS-005 historical closeout

- [x] `DES-MISS-001` Replace pass-through socket handling with prediction-backed response.
- [x] `DES-MISS-002` Wire service lifecycle away from heartbeat spam toward deterministic hosted lifetime.
- [x] `DES-MISS-003` Fix watcher lifetime and disposal in combat prediction service.
- [x] `DES-MISS-004` Harden null/empty model paths for startup and reload.
- [x] `DES-MISS-005` Add direct regression tests for decision service contract.
- Note:
  - These items were already marked complete in `TASKS.md`; this archive entry moves their completed state out of the active tracker so the local task file contains only unresolved work.
