# ForegroundBotRunner Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Injected client behavior, memory reads/writes, FG object manager parity, and stability.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Prioritize crash prevention and deterministic state exposure.

## Active Priorities
1. Stability guards
- [ ] Maintain AV guards for target setting and login snapshot capture paths.
- [ ] Keep pointer validation and main-thread execution constraints enforced.

2. FG parity exposure
- [x] Ensure FG death/ghost state detection is stable enough for corpse-run parity.
- [ ] Ensure FG snapshot data remains complete and comparable with BG path.
- [x] Implement missing descriptor-backed life-state fields (`WoWPlayer.PlayerFlags`, `WoWPlayer.Bytes/Bytes3`, `WoWUnit.Bytes0/1/2`) used by `ActivitySnapshot`.
- [x] Reduce remaining Lua-only FG life-state paths (`LocalPlayer.InGhostForm`, reclaim-delay fallbacks) now that descriptor fields are available.
- [x] Implement descriptor-backed FG `WoWPlayer.QuestLog` reads so quest log slots flow into snapshots.
- [ ] Fix FG `SpellList` parity for learned/already-known talent spells (e.g. `.learn 16462` acknowledged but missing from FG snapshot spell list).

3. Pathfinding wiring
- [x] Guarantee non-null `PathfindingClient` injection into FG `ClassContainer`.
- [ ] Add startup diagnostic line that captures configured PF endpoint and connection success/failure for faster live triage.

## Session Handoff
- Last crash/parity fix:
  - Implemented descriptor-backed FG snapshot life fields:
    - `Services/ForegroundBotRunner/Objects/WoWPlayer.cs`: `PlayerFlags`, `Bytes`, `Bytes3`.
    - `Services/ForegroundBotRunner/Objects/WoWUnit.cs`: `Bytes0`, `Bytes1`, `Bytes2`.
  - Ensured `ForegroundBotWorker` still supplies non-null `PathfindingClient` into `CreateClassContainer`.
  - Updated `LocalPlayer.InGhostForm` to descriptor-first detection (`PLAYER_FLAGS_GHOST` + stand-state dead guard), with memory/Lua fallback only when descriptor state is inconclusive.
  - Implemented descriptor-backed `WoWPlayer.QuestLog` reads (20 slots x 3 fields) to support quest snapshot parity.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal"` (pass in latest run).
- Files changed:
  - `Services/ForegroundBotRunner/Objects/WoWPlayer.cs`
  - `Services/ForegroundBotRunner/Objects/WoWUnit.cs`
  - `Services/ForegroundBotRunner/Objects/LocalPlayer.cs`
  - `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
- Next task:
  - Validate repeated corpse/ghost transitions over multiple live runs to confirm descriptor-first `InGhostForm` no longer drops death-recovery scheduling.

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
Move completed items to `Services/ForegroundBotRunner/TASKS_ARCHIVE.md`.


