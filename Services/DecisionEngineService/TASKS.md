# DecisionEngineService Tasks

## Scope
- Directory: `Services/DecisionEngineService`
- Project: `DecisionEngineService.csproj`
- Master tracker: `MASTER-SUB-015`
- Focus: deterministic runtime startup for combat prediction service wiring, SQLite model storage, and socket listener ownership.
- Keep only unresolved work here; move completed items to `Services/DecisionEngineService/TASKS_ARCHIVE.md` in the same session.

## Execution Rules
1. Keep scans source-scoped to `Services/DecisionEngineService` plus direct host/test callers only.
2. Preserve deterministic behavior and explicit failure paths; no silent fallbacks.
3. Never blanket-kill `dotnet`; cleanup must be repo-scoped and evidenced.
4. Every pass must end with one-line `Pass result` and one executable `Next command` in `Session Handoff`.

## Current Status (2026-04-15)
- Known remaining owner-local items: `0`.
- `DecisionEngineWorker` now composes the runtime from configuration when enabled.
- Runtime startup creates and verifies the data and processed directories, initializes SQLite native provider support, creates `TrainedModel` and `ModelWeights` when missing, then starts `CombatPredictionService` and `CombatModelServiceListener`.
- The prior checklist reference to a concrete `DecisionEngine` object is legacy wording; no such implementation type exists in the current codebase, and the active prediction path is `CombatPredictionService -> CombatModelServiceListener`.
- Completed item details are archived in `Services/DecisionEngineService/TASKS_ARCHIVE.md`.

## Open Tasks
- None.

## Simple Command Set
1. `dotnet build Services/DecisionEngineService/DecisionEngineService.csproj --configuration Release --no-restore`
2. `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"`
3. `rg -n "return base.HandleRequest|while \(!stoppingToken.IsCancellationRequested\)|FileSystemWatcher watcher = new|CreatePredictionEngine<WoWActivitySnapshot, WoWActivitySnapshot>\(_trainedModel\)" Services/DecisionEngineService`
4. `rg -n "new CombatPredictionService\(|new CombatModelServiceListener\(|new DecisionEngineRuntime\(" Services/DecisionEngineService Services/WoWStateManager -g "*.cs"`

## Session Handoff
- Last updated: 2026-04-15
- Active task: none. `DES-MISS-001` through `DES-MISS-006` are complete.
- Last delta: `DES-MISS-006` runtime composition, preflight, and SQLite schema startup.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet build Services/DecisionEngineService/DecisionEngineService.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Services/DecisionEngineService/CombatPredictionService.cs`
  - `Services/DecisionEngineService/DecisionEngineRuntime.cs`
  - `Services/DecisionEngineService/DecisionEngineRuntimeInitializer.cs`
  - `Services/DecisionEngineService/DecisionEngineRuntimeOptions.cs`
  - `Services/DecisionEngineService/DecisionEngineService.csproj`
  - `Services/DecisionEngineService/DecisionEngineWorker.cs`
  - `Services/DecisionEngineService/Listeners/CombatModelServiceListener.cs`
  - `Services/DecisionEngineService/Properties/AssemblyInfo.cs`
  - `Services/DecisionEngineService/README.md`
  - `Services/DecisionEngineService/SqliteProvider.cs`
  - `Tests/PromptHandlingService.Tests/DecisionEngineRuntimeTests.cs`
- Blockers: none
- Next command: `rg -n "^- \[ \]" --glob TASKS.md`
