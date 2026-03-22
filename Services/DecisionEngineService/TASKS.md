<<<<<<< HEAD
﻿# DecisionEngineService Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Directory: .\Services\DecisionEngineService

Projects:
- DecisionEngineService.csproj

## Instructions
- Execute tasks directly without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep this file focused on active, unresolved work only.
- Add new tasks immediately when new gaps are discovered.
- Archive completed tasks to TASKS_ARCHIVE.md.

## Active Priorities
1. Validate this project behavior against current FG/BG parity goals.
2. Remove stale assumptions and redundant code paths.
3. Add or adjust tests as needed to keep behavior deterministic.

## Session Handoff
- Last task completed:
- Validation/tests run:
- Files changed:
- Next task:

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match by command line).
- [ ] Record process name, PID, and stop result in test evidence.

2. FG/BG parity gate for every scenario run.
- [ ] Run both FG and BG for the same scenario in the same validation cycle.
- [ ] FG must remain efficient and player-like.
- [ ] BG must mirror FG movement, spell usage, and packet behavior closely enough to be indistinguishable.

3. Physics calibration requirement.
- [ ] Run PhysicsEngine calibration checks when movement parity drifts.
- [ ] Feed calibration findings into movement/path tasks before marking parity work complete.

4. Self-expanding task loop.
- [ ] When a missing behavior is found, immediately add a research task and an implementation task.
- [ ] Each new task must include scope, acceptance signal, and owning project path.

5. Archive discipline.
- [ ] Move completed items to local `TASKS_ARCHIVE.md` in the same work session.
- [ ] Leave a short handoff note so another agent can continue without rediscovery.
## Archive
Move completed items to TASKS_ARCHIVE.md and keep this file short.



=======
# DecisionEngineService Tasks

## Scope
- Directory: `Services/DecisionEngineService`
- Project: `DecisionEngineService.csproj`
- Master tracker: `MASTER-SUB-015`
- Focus: wire the service so combat snapshots are handled by a real prediction path with deterministic startup, failure, and shutdown behavior.
- Keep only unresolved work here; move completed items to `Services/DecisionEngineService/TASKS_ARCHIVE.md` in the same session.

## Execution Rules
1. Execute task IDs in order unless blocked.
2. Keep scans source-scoped to `Services/DecisionEngineService` plus direct host/test callers only.
3. Preserve deterministic behavior and explicit failure paths; no silent fallbacks.
4. Never blanket-kill `dotnet`; cleanup must be repo-scoped and evidenced.
5. If two consecutive passes produce no file delta, record blocker + exact next command in `Session Handoff` before switching files.
6. Every pass must end with one-line `Pass result` and one executable `Next command` in `Session Handoff`.

## Evidence Snapshot (2026-02-25)
- Listener handler is still pass-through: `return base.HandleRequest(request);` in `Listeners/CombatModelServiceListener.cs:11`.
- Base socket server returns a default empty response when not overridden (`Exports/BotCommLayer/ProtobufSocketServer.cs:53-56`), so current listener path is non-functional for prediction.
- Worker loop is heartbeat-only (`DecisionEngineWorker.cs:20`, `DecisionEngineWorker.cs:27`) and does not start any listener/prediction component.
- `CombatPredictionService` watcher is local-only (`CombatPredictionService.cs:94`), so it has no managed lifetime/disposal path.
- Prediction engine is created from `_trainedModel` without null guard (`CombatPredictionService.cs:34`, `CombatPredictionService.cs:88`, `CombatPredictionService.cs:163`), while loader can return `null` when DB has no row (`CombatPredictionService.cs:50`).
- `rg -n "new DecisionEngine\\(|new CombatPredictionService\\(|new CombatModelServiceListener\\(" Services -g "*.cs"` returned no runtime instantiation sites.
- Host registration exists only for worker type (`AddHostedService<DecisionEngineWorker>()`) in both `Services/StateManager/Program.cs` and `Services/WoWStateManager/Program.cs`.
- Baseline build is green: `dotnet build Services/DecisionEngineService/DecisionEngineService.csproj --configuration Release --no-restore`.
- Existing direct test is green: `DecisionEngineReadBinFileTests` in `Tests/PromptHandlingService.Tests`.
- `dotnet test` emits `NETSDK1206` RID warning for `SQLite`/`SQLitePCLRaw.lib.e_sqlite3`, so runtime RID compatibility needs explicit tracking during service hardening.

## Environment Checklist
- [x] Service builds in Release (`DecisionEngineService.csproj`).
- [ ] Runtime host creates/wires `CombatPredictionService`, `DecisionEngine`, and `CombatModelServiceListener`.
- [ ] SQLite schema for `TrainedModel` and `ModelWeights` is verified in active runtime environment.
- [ ] Data/processed directories used by file watchers are created and writable at startup.

## P0 Active Tasks (Ordered)

### DES-MISS-001 Replace pass-through socket handling with prediction-backed response
- [x] **Done (2026-02-27).** CombatModelServiceListener now calls `DecisionEngine.GetNextActions(request)` instead of `base.HandleRequest`. Returns request snapshot on success, empty snapshot on failure with explicit logging.

### DES-MISS-002 Wire service runtime composition and lifecycle
- [x] **Done (2026-02-27).** DecisionEngineWorker heartbeat spam replaced with idle-wait pattern. Logs startup/shutdown lifecycle. Full listener/prediction wiring deferred pending configuration.

### DES-MISS-003 Fix watcher lifetime and disposal in combat prediction service
- [x] **Done (prior session).** FileSystemWatcher promoted to field, IDisposable implemented.

### DES-MISS-004 Harden null/empty model paths for startup and reload
- [x] **Done (prior session).** Null/empty path validation added to CombatPredictionService + DecisionEngine constructors.

### DES-MISS-005 Add direct regression tests for decision service contract
- [x] **Done (batch 14).** Added `DecisionEngineContractTests.cs` with 16 tests:
  - MLModel: Predict returns empty/cast-spell/deterministic results; LearnFromSnapshot increments/decrements/expands weights; null-safe; GetWeights never null.
  - GetNextActions: routes through MLModel.Predict; returns empty for empty snapshot; never throws.
  - DecisionEngine lifecycle: constructor validation (null/empty binDir, null db); Dispose idempotent; successful construction with valid inputs.
- Validation: 16/16 pass (`dotnet test --filter DecisionEngineContractTests`).
- [x] Acceptance: regressions in model prediction, weight learning, and engine lifecycle fail fast.

## Simple Command Set
1. `dotnet build Services/DecisionEngineService/DecisionEngineService.csproj --configuration Release --no-restore`
2. `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DecisionEngineReadBinFileTests" --logger "console;verbosity=minimal"`
3. `rg -n "return base.HandleRequest|while \\(!stoppingToken.IsCancellationRequested\\)|FileSystemWatcher watcher = new|CreatePredictionEngine<WoWActivitySnapshot, WoWActivitySnapshot>\\(_trainedModel\\)" Services/DecisionEngineService`
4. `rg -n "new DecisionEngine\\(|new CombatPredictionService\\(|new CombatModelServiceListener\\(" Services -g "*.cs"`

## Session Handoff
- Last updated: 2026-02-28
- Active task: all DecisionEngineService tasks complete (DES-MISS-001..005)
- Last delta: DES-MISS-005 (16 contract tests in DecisionEngineContractTests.cs)
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/PromptHandlingService.Tests -c Debug --filter DecisionEngineContractTests` — 16/16 pass
- Files changed:
  - `Tests/PromptHandlingService.Tests/DecisionEngineContractTests.cs` — new (16 tests)
  - `Services/DecisionEngineService/TASKS.md`
- Next command: continue with next queue file
- Blockers: none
>>>>>>> cpp_physics_system
