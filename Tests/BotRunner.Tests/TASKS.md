# BotRunner.Tests Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Local focus: live validation behavior correctness and harness safety.
- Keep setup/run paths simple, deterministic, and easy to rerun.

## Priority Focus
1. Corpse-run deterministic setup
- [x] Use `.tele name {NAME} Orgrimmar` before kill in `DeathCorpseRunTests`.
- [x] Remove `ValleyOfTrials` setup dependency from corpse-run test flow.
- [ ] Capture fresh BG+FG live evidence with the current Orgrimmar setup path.

2. Runtime safety
- [ ] Keep corpse-run timeout window at 10 minutes.
- [ ] Guarantee teardown on timeout/failure/cancel without leaking repo-scoped processes.
- [ ] Confirm no lingering `WoWStateManager`, test-launched `WoW`, repo-scoped `dotnet`, or `testhost*` after each run.

3. Corpse lifecycle assertions
- [ ] BG and FG both prove `alive -> dead -> ghost -> runback -> reclaim-ready -> retrieve -> alive`.
- [ ] Retrieval occurs only after reclaim delay reaches zero.

## Iterative Expansion Backlog
1. Combat parity loop
- [ ] Run FG/BG `CombatLoopTests` in the same validation cycle.
- [ ] Compare movement pacing, spell sequence/timing, and packet behavior.
- [ ] Add `research + implementation` tasks when parity mismatch is found.

2. Gathering/mining parity loop
- [ ] Run FG/BG `GatheringProfessionTests` and mining-focused scenarios in the same cycle.
- [ ] Compare route choice, node approach, gather cast timing, and interruption handling.
- [ ] Add `research + implementation` tasks when parity mismatch is found.

3. Physics calibration gate
- [ ] Trigger calibration checks when movement parity drifts in combat/gathering/corpse scenarios.
- [ ] Feed findings into `Tests/Navigation.Physics.Tests/TASKS.md` and `Exports/Navigation/TASKS.md` before closing parity tasks.

## Diagnostics + Execution Simplicity
1. Visible process windows for local debug
- [ ] Add opt-in test setting to launch visible consoles/windows for state manager and helper processes.
- [ ] Keep default headless behavior for CI and unattended runs.

2. Canonical command simplicity
- [ ] Keep one command per primary scenario (corpse, combat, gathering).
- [ ] Avoid multi-step setup commands in day-to-day usage docs.

## Canonical Commands
1. Corpse-run validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

2. Combat validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

3. Gathering/mining validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

4. Repo-scoped cleanup:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match command line/process tree).
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

## Archived Completed Items (Moved 2026-02-24)
- [x] Removed reseed/variant retry death-loop path from runback setup.
- [x] Preserved strict corpse lifecycle ordering in test assertions.
- [x] Added timeout/runsettings plumbing baseline for test sessions.
- [x] Switched corpse setup teleport from `ValleyOfTrials` to Orgrimmar named teleport command path.

## Session Handoff
- Last updated: 2026-02-24
- Current directive: validate corpse-run reliability first, then iterate combat and gathering parity loops with calibration gating.
