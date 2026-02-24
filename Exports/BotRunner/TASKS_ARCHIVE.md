# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-02-23 09:27:22) - Exports/BotRunner/TASKS.md

# BotRunner Tasks

## Scope
Shared bot action sequencing, behavior trees, task semantics, and snapshot mapping.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep task behavior deterministic and snapshot-verifiable.

## Active Priorities
1. Death/corpse behavior
- [x] Keep `RetrieveCorpseTask` strictly pathfinding-driven (no direct fallback).
- [x] Ensure reclaim-delay waiting/retry behavior matches live server timing.
- [x] Align release/retrieve transitions with strict life-state checks.
- [x] Add explicit task-stack diagnostics for push/pop reason codes (`ReleaseCorpseTask`, `RetrieveCorpseTask`) to speed future parity triage.
- [x] Use descriptor-backed ghost/dead state (player flags + stand state) for death-recovery decisions so task scheduling is not blocked by `InGhostForm` drift.
- [x] Make corpse-retrieval movement/reclaim gating horizontal-distance based (`DistanceTo2D`) and clamp corpse navigation Z when corpse Z is implausible vs ghost Z.
- [ ] Add explicit `RetrieveCorpseTask` diagnostics in FG-friendly output (not only Serilog) so reclaim/send cadence is visible in passing test logs.

2. Action sequencing and guards
- [x] Keep dead-state send-chat guards consistent with test setup requirements.
- [x] Simplify death-command sequencing to a deterministic `.kill` -> `.die` fallback path without capability-probe spam.
- [ ] Ensure movement/action tasks do not overwrite long-running gather/corpse actions.
- [x] Move `Goto` behavior to pathfinding-driven waypoint movement (no direct steering by default), with explicit no-route stop/wait handling to avoid stuck-forward loops.
- [ ] Tune `Goto` no-route retry/log behavior (`[GOTO] No route ...`) so BG follow loops remain observable without log spam.

3. Snapshot field completeness
- [x] Removed Lua ghost-bit OR fallback in `BuildPlayerProtobuf` so life-state assertions come from descriptor-backed snapshot fields.
- [x] Serialize quest-log snapshot data (`WoWPlayer.QuestLogEntries`) from `IWoWPlayer.QuestLog` for live quest-state assertions.
- [x] Serialize nearby-unit identity fields (`GameObject.Entry`, `GameObject.Name`) in `BuildUnitProtobuf` for deterministic live target classification.
- [ ] Keep all parity-critical fields mapped and serialized consistently for FG and BG.
- [ ] Fix live combat target-visibility parity: `Player.Unit.TargetGuid` can remain unset/stale during successful `StartMeleeAttack` engage (BG/FG), causing snapshot-observability drift from real in-game target state.
- [ ] Audit FG nearby-unit name completeness (`<unknown>` names in live scans) and either fix source mapping or ensure fallback identity fields are always populated for target classification.
- [ ] Add targeted diagnostics when ghost corpse-run movement stalls (e.g., movement flags stuck at non-moving values like `0x10000000`) to distinguish pathfinding no-route vs controller/root-state issues.
- [ ] Add corpse-position snapshot serialization (`WoWPlayer.corpsePosition`) so death tests/tasks do not rely on last-alive fallback when server transitions directly to ghost.

## Session Handoff
- Last bug/task closed:
  - `RetrieveCorpseTask` now uses horizontal corpse distance for run/reclaim gating and clamps corpse-nav Z when corpse Z delta is extreme.
  - Fixed FG corpse-retrieval churn by hardening `RetrieveCorpseTask` against transient ghost-state flicker and ensuring FG always provides a non-null `PathfindingClient` to `ClassContainer`.
  - Simplified death-command dispatch in live fixture to avoid dead-state chat spam and removed capability-probe dead code.
  - Snapshot mapping now relies on descriptor `PlayerFlags` only (removed local Lua ghost-bit injection path).
  - Death-recovery scheduling now uses descriptor-backed ghost/corpse detection (`PLAYER_FLAGS_GHOST`, stand-state dead) instead of relying solely on `InGhostForm`.
  - Added explicit task push/pop diagnostics (`[TASK-PUSH]`, `[TASK-POP]`) for release/retrieve/teleport task transitions.
  - `SendChat` dead/ghost guard and resurrect sequence checks now use descriptor-backed death-state helper.
  - Added quest-log serialization in `BuildPlayerProtobuf` (`QuestLogEntries`) so LiveValidation can assert quest add/remove transitions from snapshots.
  - Added nearby-unit `Entry`/`Name` snapshot serialization so combat tests can force boar-only targeting using snapshot state.
- Validation tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal"` (latest: `Passed`, ~2m15s)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal"`
  - Result set in this session:
    - `Passed` (~2m10s) after descriptor/death-recovery updates.
    - one rerun `Skipped` due live fixture precondition state.
    - one rerun `Failed` (intermittent FG corpse-run stall persisted: `dist=127.8`, `step=0.0`, `moveFlags=0x10000000`).
    - subsequent rerun `Passed` (~2m10s).
- Files changed:
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/ReleaseCorpseTask.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Exports/BotRunner/Tasks/TeleportTask.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
- Next task:
  - Continue isolating intermittent corpse-run stalls with new task-stack diagnostics and movement-flag context (`moveFlags`, reclaim delay, path/no-path) across repeated live runs.
  - Correlate FG `RetrieveCorpseTask` route availability with the pathfinding service when stall signature appears (`dist=127.8`, no movement ticks).
  - Close combat snapshot parity gap where live targeting succeeds but `Player.Unit.TargetGuid`/unit identity can be stale in snapshots during melee engage.

## Archive
Move completed items to `Exports/BotRunner/TASKS_ARCHIVE.md`.


