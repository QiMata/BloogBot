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
6. Every behavior task must include enough detail that another agent can continue without asking for new requirements.

## Global P0: FG/BG Indistinguishable Parity Program (Current Priority)
1. Missing implementation inventory
- [ ] Track each behavior as `Not Started`, `Researching`, `Implementing`, `Verifying`, or `Archived`.

2. Behavior categories that must be fully mirrored
- [ ] Core movement states: walk/run/turn/strafe/jump/fall/swim and terrain transitions.
- [ ] Death lifecycle: kill/release/ghost runback/reclaim delay/retrieve/resurrect.
- [ ] Combat lifecycle: target acquisition, ability sequencing, GCD timing, interrupts, and stop/start attack transitions.
- [ ] Gathering lifecycle: node detection/approach/interact/loot/retry/failover.
- [ ] World interactions: NPC gossip/vendor/trainer/mail/quest/object interaction.
- [ ] Inventory and aura impacts: item use, cooldowns, buffs/debuffs, and cast constraints.

3. Per-behavior parity acceptance
- [ ] FG is efficient and player-like for the scenario.
- [ ] BG mirrors FG movement, spells, packets, and timing closely enough to be indistinguishable.
- [ ] Physics calibration evidence is attached for any movement-affecting behavior.
- [ ] Timeout/cancel path confirms repo-scoped teardown with PID evidence.

## Global P0: Scheduled PhysicsEngine + MovementController Refinement Round
1. Round objective (new mandatory round)
- [ ] Fix air-teleport mismatch where FG falls but BG hovers.
- [ ] Keep this round active until BG vertical movement matches FG gravity/fall behavior.

2. Reproduction and regression gates
- [ ] Add or update a deterministic air-teleport scenario that proves fall-state behavior for FG and BG.
- [ ] Verify corpse-run still teleports to Orgrimmar before kill and performs real ghost runback.
- [ ] Run calibration before and after changes to prove frame-by-frame improvement.

3. Implementation focus
- [ ] Refine `Exports/Navigation/PhysicsEngine.cpp` frame integration for airborne spawn/teleport states.
- [ ] Refine `Exports/WoWSharpClient/Movement/MovementController.cs` cadence/state transitions for gravity-consistent packets.
- [ ] Add tests that fail on hover/no-fall regressions after airborne teleport.

4. Completion evidence
- [ ] Capture FG/BG trace comparison for vertical position and velocity.
- [ ] Archive commands used, test duration, and teardown outcome.
- [ ] Move closed refinement tasks to `TASKS_ARCHIVE.md` in each owning project.

## Global P0: Corpse-Run Stabilization
1. Setup command path
- [ ] Capture fresh live evidence for both BG and FG under current setup.

2. Runtime guard and teardown safety
- [ ] Keep corpse-run runtime window at up to 10 minutes.
- [ ] On timeout/failure/cancel, stop only repo-scoped lingering clients and managers within 30 seconds.
- [ ] Persist teardown evidence with process name, PID, and stop outcome.

3. Behavior verification
- [ ] BG and FG both complete `alive -> dead -> ghost -> runback -> reclaim-ready -> retrieve -> alive`.
- [ ] Resurrection retrieval occurs only after reclaim delay reaches zero.
- [ ] Verify both clients can run back from Orgrimmar graveyard pathing (no teleport shortcuts).

## Global P1: Iterative Scenario Parity (Combat + Gathering + World Interactions)
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

4. World interaction parity
- [ ] Run paired FG/BG scenarios for vendor, trainer, quest, and object interaction behaviors.
- [ ] Add immediate research + implementation tasks for interaction mismatches.

## Global P1: Sub-TASKS.md Completion Pass
1. Populate every local `TASKS.md` with behavior-ready detail
- [ ] For each behavior card, include: scope, owning files, simple command, acceptance signals, teardown rule, and evidence path.

2. Required behavior card format
- [ ] `Behavior`: one specific player-observable behavior (for example, `AirTeleportFallRecovery`).
- [ ] `FG Baseline`: what FG does and how it is measured.
- [ ] `BG Target`: exact mirrored behavior expected from BG.
- [ ] `Implementation Targets`: concrete project/file paths likely to change.
- [ ] `Simple Command`: one command to run/verify the behavior.
- [ ] `Acceptance`: objective pass/fail checks.
- [ ] `If Fails`: immediate research task + implementation task to add.

3. Archive and handoff discipline
- [ ] Move completed cards/tasks to local `TASKS_ARCHIVE.md` in the same session.
- [ ] Leave a short handoff note with next command and next unchecked task ID.

## Canonical Commands
1. Corpse-run validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

2. Combat validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

3. Gathering/mining validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

4. Repo-scoped lingering process cleanup:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

5. Physics calibration:
- `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

## Pending Command Simplification Work
1. Thin scenario wrappers
- [ ] Add one-command wrappers for corpse/combat/gathering/physics scenarios in `run-tests.ps1`.
- [ ] Keep wrapper flags minimal and consistent across scenarios.
- [ ] Update local `TASKS.md` files to reference wrapper commands once implemented.

## Cross-Project Ownership for Current Priority
- `Tests/TASKS.md`: cross-suite sequencing and parity gates.
- `Tests/BotRunner.Tests/TASKS.md`: corpse/combat/gathering test behaviors and simple run commands.
- `Tests/Tests.Infrastructure/TASKS.md`: timeout and teardown lifecycle guardrails.
- `Exports/BotRunner/TASKS.md`: retrieve/combat/gathering task behavior and stall prevention.
- `Tests/Navigation.Physics.Tests/TASKS.md`: calibration evidence and interpolation tests.
- `Exports/Navigation/TASKS.md`: `PhysicsEngine` frame-by-frame interpolation and movement parity.
- `Services/ForegroundBotRunner/TASKS.md`: FG baseline capture and behavior efficiency checks.
- `Services/BackgroundBotRunner/TASKS.md`: BG behavior mirroring execution and packet/state parity.
- `Services/WoWStateManager/TASKS.md`: observable state parity instrumentation and lifecycle safety.

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
- Current top priority: FG/BG indistinguishable parity backlog with air-teleport fall refinement and corpse/combat/gathering validation.
- Next sync target: complete scheduled PhysicsEngine + MovementController refinement round and push behavior-card rollout into local `TASKS.md` files.

## Behavior Matrix
- `docs/BEHAVIOR_MATRIX.md` is the ordered queue for all local `TASKS.md` files.
- Update matrix status and next action each time a local behavior card changes state.
