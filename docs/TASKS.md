# Master Tasks

## Role
- `docs/TASKS.md` is the master coordination list for every local `TASKS.md`.
- Local files hold project implementation details; this file holds priority, sequencing, and shared rules.
- When priorities conflict, this file wins until explicitly updated.

## Master Coordination Rules
1. Keep every local `TASKS.md` aligned with this file in the same work session.
2. Keep commands simple and one-line where possible.
3. Never blanket-kill `dotnet`; cleanup must be repo-scoped.
4. Every timeout/failure/cancel path must include deterministic teardown evidence.
5. Move completed items to the matching `TASKS_ARCHIVE.md` during the same session.

## Global P0: Branch Stabilization And Merge Readiness
1. Commit boundary and checkpoint
- [ ] Capture current working tree into a branch checkpoint commit (no broad reset/revert).
- [ ] Include updated master task priority + ownership in the same checkpoint.

2. Mainline synchronization
- [ ] Fetch latest `origin/main`.
- [ ] Merge `origin/main` into current branch and resolve conflicts.
- [ ] Push merged branch so follow-up test fixes stack on a clean, merge-ready base.

3. Post-merge smoke gate
- [ ] Confirm corpse-run setup still teleports to Orgrimmar before kill.
- [ ] Run at least one targeted bot test command to verify no immediate regression.

## Global P0: Corpse-Run Stabilization
1. Setup command path
- [x] `DeathCorpseRunTests` setup uses `.tele name {NAME} Orgrimmar` before kill.
- [x] `ValleyOfTrials` setup path removed from corpse-run flow.
- [ ] Capture fresh live evidence for both BG and FG under current setup.

2. Runtime guard and teardown safety
- [ ] Keep corpse-run runtime window at up to 10 minutes.
- [ ] On timeout/failure/cancel, stop only repo-scoped lingering clients and managers within 30 seconds.
- [ ] Persist teardown evidence with process name, PID, and stop outcome.

3. Behavior verification
- [ ] BG and FG both complete `alive -> dead -> ghost -> runback -> reclaim-ready -> retrieve -> alive`.
- [ ] Resurrection retrieval occurs only after reclaim delay reaches zero.
- [ ] Verify both clients can run back from Orgrimmar graveyard pathing (no teleport shortcuts).

## Global P1: Iterative Scenario Parity (Combat + Gathering)
1. Combat loop parity
- [ ] Run FG and BG combat scenarios side-by-side and compare movement cadence, spell timing, and packet signature.
- [ ] Add mismatch triage tasks immediately when parity diverges.

2. Gathering/mining parity
- [ ] Run FG and BG gathering/mining scenarios in the same cycle with equivalent route goals.
- [ ] Compare approach vectors, node interaction timing, and interruption handling.
- [ ] Add research + implementation tasks for each discovered divergence.

3. Physics calibration gate
- [ ] Run physics calibration checks whenever FG/BG movement diverges.
- [ ] Feed calibration outputs into `PhysicsEngine` and `MovementController` tasks before closing parity work.

## Canonical Commands
1. Corpse-run validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

2. Combat validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

3. Gathering/mining validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

4. Repo-scoped lingering process cleanup:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Cross-Project Ownership for Current Priority
- `Tests/TASKS.md`: cross-suite sequencing and parity gates.
- `Tests/BotRunner.Tests/TASKS.md`: corpse/combat/gathering test behaviors and simple run commands.
- `Tests/Tests.Infrastructure/TASKS.md`: timeout and teardown lifecycle guardrails.
- `Exports/BotRunner/TASKS.md`: retrieve/combat/gathering task behavior and stall prevention.
- `Tests/Navigation.Physics.Tests/TASKS.md`: calibration evidence and interpolation tests.
- `Exports/Navigation/TASKS.md`: `PhysicsEngine` frame-by-frame interpolation and movement parity.

## Master Index of TASKS.md Files
- `BotProfiles/TASKS.md`
- `Exports/TASKS.md`
- `Exports/BotCommLayer/TASKS.md`
- `Exports/BotRunner/TASKS.md`
- `Exports/GameData.Core/TASKS.md`
- `Exports/Loader/TASKS.md`
- `Exports/Navigation/TASKS.md`
- `Exports/WinImports/TASKS.md`
- `Exports/WoWSharpClient/TASKS.md`
- `RecordedTests.PathingTests/TASKS.md`
- `RecordedTests.Shared/TASKS.md`
- `Services/TASKS.md`
- `Services/BackgroundBotRunner/TASKS.md`
- `Services/CppCodeIntelligenceMCP/TASKS.md`
- `Services/DecisionEngineService/TASKS.md`
- `Services/ForegroundBotRunner/TASKS.md`
- `Services/LoggingMCPServer/TASKS.md`
- `Services/PathfindingService/TASKS.md`
- `Services/PromptHandlingService/TASKS.md`
- `Services/WoWStateManager/TASKS.md`
- `Tests/TASKS.md`
- `Tests/BotRunner.Tests/TASKS.md`
- `Tests/Navigation.Physics.Tests/TASKS.md`
- `Tests/PathfindingService.Tests/TASKS.md`
- `Tests/PromptHandlingService.Tests/TASKS.md`
- `Tests/RecordedTests.PathingTests.Tests/TASKS.md`
- `Tests/RecordedTests.Shared.Tests/TASKS.md`
- `Tests/Tests.Infrastructure/TASKS.md`
- `Tests/WowSharpClient.NetworkTests/TASKS.md`
- `Tests/WoWSharpClient.Tests/TASKS.md`
- `Tests/WoWSimulation/TASKS.md`
- `Tests/WWoW.RecordedTests.PathingTests.Tests/TASKS.md`
- `Tests/WWoW.RecordedTests.Shared.Tests/TASKS.md`
- `Tests/WWoW.Tests.Infrastructure/TASKS.md`
- `UI/TASKS.md`
- `UI/Systems/Systems.AppHost/TASKS.md`
- `UI/Systems/Systems.ServiceDefaults/TASKS.md`
- `UI/WoWStateManagerUI/TASKS.md`
- `WWoW.RecordedTests.PathingTests/TASKS.md`
- `WWoW.RecordedTests.Shared/TASKS.md`
- `WWoWBot.AI/TASKS.md`

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match command line/process tree).
- [ ] Record process name, PID, and stop result in test evidence.

2. FG/BG parity gate for every scenario run.
- [ ] Run FG and BG for the same scenario in the same validation cycle.
- [ ] FG must remain efficient and player-like.
- [ ] BG must mirror FG movement, spell usage, and packet behavior closely enough to be indistinguishable.

3. Physics calibration requirement.
- [ ] Run PhysicsEngine calibration checks when movement parity drifts.
- [ ] Feed calibration findings into movement/path tasks before marking parity work complete.

4. Self-expanding task loop.
- [ ] When a missing behavior is found, add one research task and one implementation task immediately.
- [ ] Each new task must include scope, acceptance signal, and owning project path.

5. Archive discipline.
- [ ] Move completed items to local `TASKS_ARCHIVE.md` in the same work session.
- [ ] Leave a short handoff note so another agent can continue without rediscovery.

## Session Handoff
- Last updated: 2026-02-24
- Current top priority: commit and merge branch to a clean baseline, then continue corpse/combat/gathering stabilization.
- Next sync target: confirm merged baseline builds and preserves Orgrimmar corpse-run setup before deeper parity iteration.
