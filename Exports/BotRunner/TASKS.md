# BotRunner Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Local scope: bot task execution behavior for corpse/combat/gathering scenarios.
- Priority is deterministic behavior with no stall loops and FG/BG parity support.

## Active Blockers
1. Fresh live corpse-run evidence is still needed after Orgrimmar setup switch.
2. Retrieve behavior can still regress into low-progress loops under movement drift.
3. Combat and gathering parity loops need structured evidence + task expansion.

## Active Work
- [ ] Re-run live corpse retrieval using Orgrimmar setup + 10-minute timeout + teardown guard.

## Iterative Parity Backlog
1. Corpse-run
- [ ] Validate BG and FG both complete full corpse lifecycle without teleport-like shortcuts.
- [ ] Ensure retrieve action is delayed until reclaim timer reaches zero.

2. Combat
- [ ] Compare FG/BG combat action sequencing and movement approach behavior.
- [ ] Create paired `research + implementation` tasks for each mismatch.

3. Gathering/mining
- [ ] Compare FG/BG node approach, interaction timing, and interruption recovery.
- [ ] Create paired `research + implementation` tasks for each mismatch.

4. Physics-gated movement parity
- [ ] Run physics calibration checks when BotRunner movement stalls or diverges.
- [ ] Convert calibration findings into `BotRunner` task fixes plus linked `Navigation` tasks.

## Canonical Commands
1. Atomic/unit focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~RetrieveCorpseTaskTests|FullyQualifiedName~TeleportTaskTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

2. Live corpse-run focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

3. Combat focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

4. Gathering focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Evidence Logs
- `tmp/deathcorpse_run_current.log`
- `tmp/botrunnertasks_atomic_current.log`

## Session Handoff
- Last updated: 2026-02-24
- Highest-priority unresolved issue: capture fresh live corpse-run evidence under Orgrimmar setup and guarded runtime.

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
Move completed items to `Exports/BotRunner/TASKS_ARCHIVE.md`.

## Behavior Cards
1. CorpseRunOrgrimmarReleaseRecovery
- [ ] Behavior: after `.tele name {NAME} Orgrimmar` and kill, FG/BG both release spirit, run back from graveyard, wait reclaim timer, retrieve corpse, and return alive.
- [ ] FG Baseline: FG completes dead -> ghost -> runback -> reclaim-ready -> retrieve -> alive sequence without teleport shortcuts.
- [ ] BG Target: BG completes the same sequence with comparable route progress, reclaim timing, and retrieve success.
- [ ] Implementation Targets: `Exports/BotRunner/Tasks/ReleaseCorpseTask.cs`, `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`, `Exports/BotRunner/Tasks/CombatRotationTask.cs`, `Exports/BotRunner/Tasks/GatherNodeTask.cs`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: both clients complete full lifecycle, retrieve only after reclaim delay reaches zero, and timeout/failure path stops repo-scoped lingering clients/managers within 30 seconds with PID evidence.
- [ ] If Fails: add `Research:CorpseRunFailure::<phase>` and `Implement:CorpseRunFix::<task>` entries with linked logs and teardown records.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
