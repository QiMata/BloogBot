---
name: botrunner-task-implementation
description: Add an IBotTask behavior-tree task that orchestrates Actions to drive one minute state change, with FG+BG parity, tests, and live-validation. Use when implementing a new bot behavior (move/loot/interact/cast/gather/etc.) as a Task on the LIFO task stack.
trigger: new IBotTask, add a bot task, behavior-tree node, Task stack node, GoToTask child, ObjectiveType decomposition, drive a new bot behavior, push a child task
---

# BotRunner Task Implementation

## Goal

Add one new **Task** (`IBotTask`) that drives a single minute state change by
composing **Actions** over many ticks, with verification and failure handling.
A Task is a behavior-tree node on the LIFO task stack — not an atomic Action and
not an Objective. Read [`docs/Spec/18_TERMINOLOGY.md`](../../../docs/Spec/18_TERMINOLOGY.md)
first: `Activity → Objective → Task → Action`. `MoveToCoord`, `CastSpell`,
`LootCorpse` are **Tasks**; one memory read / one opcode send / one key press are
**Actions**. If your unit of work is a single atomic primitive, you do not need a
new Task — see `Tests/BotRunner.Tests/ActionDispatch/`.

## Inputs

- The Objective (`ObjectiveType`) the Task serves and the single state change it
  must produce (e.g. "player is at coord X", "corpse is looted").
- Success/verification criteria readable from game state, and a failure signal.
- Whether the Task needs movement (almost always pushes `GoToTask` as the
  universal child).
- Key files:
  - Contract: `Exports/BotRunner/Interfaces/IBotTask.cs`
    (`TickAsync`, `OnPushedAsync`, `OnPoppedAsync`, `OnChildFailedAsync`,
    `Status`, `Name`).
  - Status enum: `Exports/BotRunner/Interfaces/BotTaskStatus.cs`
    (`Running` / `Complete` / `Failed`).
  - Base class: `Exports/BotRunner/Tasks/BotTask.cs` — inherit this. Provides
    `ObjectManager`, `BotTasks` (the stack), `Container`, `Config`,
    `EventHandler`, `Logger`, and `NavigateToward` / `TryNavigateToward` /
    `NavPath`. Implements the Phase-1 async shim: `TickAsync` → `OnTick` →
    legacy `void Update()` (reflection). New tasks may either implement the
    legacy `Update()` shim or override `TickAsync` directly.
  - Per-tick context: `Exports/BotRunner/Tasks/BotTaskContext.cs`.
  - Stack runner: `Exports/BotRunner/Tasks/TaskStackDriver.cs`,
    `Exports/BotRunner/BotRunnerService.cs`.
  - Worked examples: `Exports/BotRunner/Tasks/IdleTask.cs` (minimal),
    `GoToTask.cs` (universal navigation child), `CastSpellTask.cs`,
    `InteractWithUnitTask.cs`, `LootCorpseTask.cs`.
  - Objective → Task wiring: `Exports/BotRunner/ActionDispatcher.cs` and
    `Exports/BotRunner/BotRunnerService.ActionMapping.cs`.
- Area rules: `.github/instructions/shared-libraries.instructions.md`.

## Preconditions

- You have read `docs/Spec/18_TERMINOLOGY.md` and confirmed the unit of work is a
  Task, not an Action or an Objective.
- `BotRunner` is a **shared library** consumed by both `Services/ForegroundBotRunner/`
  and `Services/BackgroundBotRunner/` (see `Exports/BotRunner/CLAUDE.md`); the Task
  must work in both modes (FG memory access + BG protocol emulation).
- `WoW.exe` is killed before any build (FG injection locks the output DLLs).
- The build is green: `dotnet build Exports/BotRunner/BotRunner.csproj`.

## Procedure

1. **Pick the closest existing task** as a template (e.g. `InteractWithUnitTask`
   for "go to a unit and do something", `GatherNodeTask` for node interaction).
   Copy its structure rather than starting blank.
2. **Create** `Exports/BotRunner/Tasks/<Name>Task.cs` inheriting `BotTask`
   (`public class <Name>Task(IBotContext botContext, /*params*/) : BotTask(botContext), IBotTask`).
   Hold a private `BotTaskStatus _status = BotTaskStatus.Running;` and expose it via
   `public BotTaskStatus Status => _status;`.
3. **Drive one tick.** In `Update()` (or `override TickAsync`): read state via
   `ObjectManager`; if the destination is reachable, push `GoToTask` as a child
   (`BotTasks.Push(new GoToTask(...))`) and return — let the universal child move,
   then resume when it pops. Never busy-loop a move inside one Task.
4. **Verify before completing.** Re-read game state to confirm the state change
   actually happened, then set `_status = BotTaskStatus.Complete` and
   `BotTasks.Pop()`. Set `BotTaskStatus.Failed` on an unrecoverable condition;
   map the cause to a `FailureReason` (see [[failure-reason-mapping]]).
5. **Wire the Objective → Task mapping.** In
   `Exports/BotRunner/BotRunnerService.ActionMapping.cs` (and/or
   `ActionDispatcher.cs`), route the relevant `ObjectiveType` to push your Task,
   following the existing `case ObjectiveType.X:` pattern.
6. **Keep ticks cheap and idempotent.** Each tick is one pass of the loop; store
   progress in fields, honor the `CancellationToken`, and call `ClearNavigation()`
   when switching targets.
7. **Add tests** (see Verification).

## Verification

- Build the layer: `dotnet build Exports/BotRunner/BotRunner.csproj`.
- Fast unit suite: `.\scripts\test-fast.ps1` (Layer 3 — no server).
- Targeted: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -v n`.
- **Unit test** the Task against a mocked `IObjectManager` (xUnit + Moq) — the
  legitimate isolated pattern (Category B in the test-isolation audit).
- **Single-Action shape** assertions (one opcode / read / write the Task emits)
  go in `Tests/BotRunner.Tests/ActionDispatch/`, never in `LiveValidation/`.
- **Behavior** is validated by declaring the Activity and letting
  `DecisionEngine`/`ActivityResolver` pick the Task — author that via
  [[live-validation-test-authoring]]; do not construct raw `ObjectiveMessage`
  objects in a new behavior test.
- Confirm both execution modes: BG (headless) regression + FG (live client)
  spot-check.

## Outputs

- New `Exports/BotRunner/Tasks/<Name>Task.cs`.
- Objective → Task mapping edit in `BotRunnerService.ActionMapping.cs` /
  `ActionDispatcher.cs`.
- A unit test (mocked `IObjectManager`) and/or an `ActionDispatch/` test, plus a
  LiveValidation behavior test where the bot must choose the Task naturally.
- Updated `docs/TASKS.md` slot if this work is task-tracked.

## Failure modes and recovery

- **Confusing Task with Action.** Putting compound, multi-tick logic where an
  atomic primitive belongs (or vice-versa) drifts the spec. Re-check Spec/18.
- **Blocking the tick.** Looping/sleeping inside `Update()`/`TickAsync` stalls the
  whole bot. Return after pushing a child or after one unit of progress.
- **Skipping verification.** Marking `Complete` without re-reading state produces
  silent regressions. Always confirm the state change.
- **Forgetting BG parity.** A Task that only works via FG memory access breaks the
  headless runner. Exercise both modes.
- **Building with WoW.exe running** → MSB3027 DLL copy lock. Kill the specific
  WoW.exe PID first (never blanket-kill).
- **Inventing a `FailureReason`** in code — the enum mirrors `docs/Spec/12`; use
  [[failure-reason-mapping]].

## Related skills

- [[live-validation-test-authoring]] — exercise the Task end-to-end through the
  StateManager loop.
- [[failure-reason-mapping]] — classify a Task's failure path.
- [[coordinator-implementation]] — when many bots' Tasks must be orchestrated for
  an Activity.
- [[bot-profile]] — class/spec combat Tasks (rotation/buff/rest/pull).
- Spec: `docs/Spec/18_TERMINOLOGY.md`, `docs/architecture/aota/`.
