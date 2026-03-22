<<<<<<< HEAD
﻿# GameData.Core Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Shared interfaces/models used by both FG and BG object managers and bot logic.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep interface changes tightly coupled to actual parity requirements.

## Active Priorities
1. Life-state and corpse model parity
- [ ] Keep dead/ghost/reclaim fields aligned with server semantics and snapshot usage.

2. Object/unit/player model consistency
- [ ] Ensure model fields required by tests and BotRunner tasks are explicit and stable.

## Session Handoff
- Last interface/model update:
- Downstream impact validated in:
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
Move completed items to `Exports/GameData.Core/TASKS_ARCHIVE.md`.


=======
# GameData.Core Tasks

## Scope
- Project: `Exports/GameData.Core`
- Owns shared enums/interfaces/models consumed by FG, BG, BotRunner, and snapshot layers.
- This file tracks direct implementation tasks bound to concrete files/symbols.
- Master tracker: `MASTER-SUB-005`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep corpse lifecycle contract aligned to canonical flow: `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
3. Keep validation commands simple and one-line, with `--no-restore` where possible.
4. Record `Last delta` and `Next command` in `Session Handoff` every pass.
5. Move completed tasks to `Exports/GameData.Core/TASKS_ARCHIVE.md` in the same session.
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
8. Resume-first guard: start each pass by running the prior `Session Handoff -> Next command` verbatim before new scans.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same pass.

## Environment Checklist
- [x] `Exports/GameData.Core/GameData.Core.csproj` builds in `Release`.
- [x] Corpse lifecycle consumer checks are discoverable from `Tests/BotRunner.Tests`.
- [x] No stale `FIXME`/ambiguous death-state comments remain in contract files (GDC-MISS-001 done).

## Evidence Snapshot (2026-02-25)
- `dotnet build Exports/GameData.Core/GameData.Core.csproj --configuration Release --no-restore` passes.
- `Exports/GameData.Core/Enums/DeathState.cs` still contains an ambiguity marker/FIXME at `DeathState.DEAD` for player `CORPSE/DEAD` switching semantics.
- Corpse lifecycle interface contract fields are present for normalization:
  - `IWoWLocalPlayer`: `CorpsePosition`, `InGhostForm`, `CanResurrect`, `CorpseRecoveryDelaySeconds`.
  - `IWoWCorpse`: `OwnerGuid`, `Type`, `CorpseFlags`.
  - `IObjectManager`: `RetrieveCorpse()`.
- BotRunner corpse lifecycle test set is discoverable via list-tests filter:
  - `ReleaseCorpseTaskTests`
  - `RetrieveCorpseTaskTests`
  - `DeathCorpseRunTests`
- Snapshot interface drift evidence:
  - `IActivitySnapshot` currently exposes only `Timestamp` and `AccountName`.
  - `IWoWActivitySnapshot` adds `CharacterName` and `ScreenState`.

## P0 Active Tasks (Ordered)

### GDC-MISS-001 Resolve ambiguous player death-state contract in `DeathState`
- [x] **Done (batch 1).** FIXME replaced with clear XML docs covering player vs creature semantics.
- [x] Acceptance: no ambiguity marker remains and consumers can interpret player death states deterministically.

### GDC-MISS-002 Lock corpse lifecycle interface contract across player/object/corpse
- [x] **Done (batch 9).** Added XML docs to `IWoWLocalPlayer` (CorpsePosition, InGhostForm, CanResurrect, CorpseRecoveryDelaySeconds) and `IWoWCorpse` (OwnerGuid, Type, CorpseFlags) with corpse lifecycle contract summary.
- [x] Acceptance: corpse lifecycle decisions in BotRunner no longer need implementation-specific drift guards for these contract members.

### GDC-MISS-003 Remove snapshot contract drift between `IActivitySnapshot` and `IWoWActivitySnapshot`
- [x] **Done (batch 9).** Made `IWoWActivitySnapshot : IActivitySnapshot`. Removed duplicated `Timestamp`/`AccountName` from `IWoWActivitySnapshot`. Updated XML docs on both interfaces to document hierarchy boundary. Build verified (0 CS errors).
- [x] Acceptance: snapshot interface ownership is explicit and there is one deterministic contract path per consumer use-case.

## Simple Command Set
1. `dotnet build Exports/GameData.Core/GameData.Core.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ReleaseCorpseTaskTests|FullyQualifiedName~RetrieveCorpseTaskTests|FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=minimal"`
3. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Current focus: `GDC-MISS-001`
- Last delta: executed prior handoff command (`Get-Content Exports/Loader/TASKS.md`) and added resume-first/next-file continuity guards for one-by-one queue traversal.
- Pass result: `delta shipped`
- Validation/tests run:
  - `Get-Content -Path 'Exports/Loader/TASKS.md' -TotalCount 380`
- Files changed:
  - `Exports/GameData.Core/TASKS.md`
- Next queue file: `Exports/Loader/TASKS.md`
- Next command: `Get-Content -Path 'Exports/Loader/TASKS.md' -TotalCount 380`
- Loop Break: if two passes produce no delta, record blocker + exact next command and move to next queued file.
>>>>>>> cpp_physics_system
