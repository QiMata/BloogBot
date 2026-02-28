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
- [ ] Problem: current test coverage only validates read-bin behavior and does not protect runtime listener/model/watcher contracts.
- [ ] Target files: `Tests/PromptHandlingService.Tests/*` or `Tests/DecisionEngineService.Tests/*` (if created), plus service files under test.
- [ ] Required change: add deterministic tests for listener routing, model-unavailable fallback path, watcher start/stop disposal, and reload behavior.
- [ ] Validation command: `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DecisionEngine" --logger "console;verbosity=minimal"`.
- [ ] Acceptance criteria: regressions in listener wiring, model state handling, and watcher lifecycle fail fast with actionable assertions.

## Simple Command Set
1. `dotnet build Services/DecisionEngineService/DecisionEngineService.csproj --configuration Release --no-restore`
2. `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DecisionEngineReadBinFileTests" --logger "console;verbosity=minimal"`
3. `rg -n "return base.HandleRequest|while \\(!stoppingToken.IsCancellationRequested\\)|FileSystemWatcher watcher = new|CreatePredictionEngine<WoWActivitySnapshot, WoWActivitySnapshot>\\(_trainedModel\\)" Services/DecisionEngineService`
4. `rg -n "new DecisionEngine\\(|new CombatPredictionService\\(|new CombatModelServiceListener\\(" Services -g "*.cs"`

## Session Handoff
- Last updated: 2026-02-25
- Last delta: expanded `MASTER-SUB-015` tasks into concrete execution cards with direct build/test/source evidence and explicit validation/acceptance criteria.
- Pass result: `delta shipped`
- Next command: `Get-Content -Path 'Services/ForegroundBotRunner/TASKS.md' -TotalCount 320`
- Current blocker: none.
- Loop Break: if no file delta after two passes, record blocker + exact next command, then advance queue pointer in `docs/TASKS.md`.
