---
name: coordinator-implementation
description: Author a new activity coordinator (Dungeon/Raid/BG/Quest/Economy/etc.) in WoWStateManager that orchestrates multiple bots through an Activity by emitting Objectives from snapshot state. Use when a multi-character Activity needs server-side coordination.
trigger: new coordinator, activity coordinator, dungeon raid bg coordinator, orchestrate multiple bots, StateManager coordination, emit objectives for an activity
---

# Coordinator Implementation

## Goal

Add a StateManager-side coordinator that drives one Activity for a set of bots: it
reads the latest snapshots, decides the next Objective per participant, and emits
`ObjectiveMessage`s that BotRunner decomposes into Tasks. Coordinators are the
legal producer of Objectives (the wire `ObjectiveMessage`); tests still drive the
Activity and assert on behavior, not on raw Objectives.

## Inputs

- The Activity family (Dungeon/Raid/Battleground/Quest/Economy/Combat/…), its
  participants/roles, and the state-machine phases.
- Key files (verified):
  - Examples: `Services/WoWStateManager/Coordination/DungeoneeringCoordinator.cs`,
    `BattlegroundCoordinator.cs`, `CombatCoordinator.cs` — concrete classes
    (no shared interface today) with a nested `CoordState` enum, a
    `switch(_state)` machine, `TransitionTo(next)`, and a
    `GetAction(requestingAccount, snapshots)` → `ObjectiveMessage?` entry point.
  - Test base: `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBase.cs`.
- Spec: `docs/Spec/02_STATEMANAGER.md` (coordinator role),
  `docs/Spec/04_ACTIVITIES.md` (Activity catalog + families).
- Area rules: `.github/instructions/services.instructions.md`.

## Preconditions

- The Activity exists in the catalog (see [[activity-catalog-bootstrap]]) and the
  needed StateManager mode dispatches to coordinators (see
  [[mode-handler-implementation]]).
- `dotnet build Services/WoWStateManager/WoWStateManager.csproj` is green.

## Procedure

1. Copy the closest coordinator (e.g. `DungeoneeringCoordinator`) to
   `Services/WoWStateManager/Coordination/<Domain>Coordinator.cs`.
2. Define a nested `CoordState` enum for the phases (prep → travel → execute →
   complete/abort).
3. Implement `GetAction(string requestingAccount, ConcurrentDictionary<string,
   WoWActivitySnapshot> snapshots)`: per state, read participant snapshots, return
   the next `ObjectiveMessage` (or `null` to wait), and call `TransitionTo(next)`.
4. Resolve runtime parameters from env/config where applicable (mirror
   `DungeoneeringCoordinator.ResolveTargetFromEnvironment()`).
5. Wire the coordinator into the mode handler that should invoke it.
6. Add a LiveValidation collection/fixture extending `CoordinatorFixtureBase` and
   assert on participant snapshots (see [[live-validation-test-authoring]]).

## Verification

- Build: `dotnet build Services/WoWStateManager/WoWStateManager.csproj`.
- Targeted live test:
  `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~<Domain>Coordinator"`.
- Full integration: `.\scripts\test-integration.ps1` (Layer 4).
- Confirm the coordinator never targets Shodan as a participant.

## Outputs

- `Services/WoWStateManager/Coordination/<Domain>Coordinator.cs` + mode wiring.
- A coordinator LiveValidation fixture/test.
- `docs/TASKS.md` / `docs/Plan/Activities/` slot update.

## Failure modes and recovery

- **Stalling with no `null`/timeout path** — always provide a wait or abort
  transition so a stuck participant doesn't hang the Activity.
- **Trusting stale snapshots** — read the latest from the snapshot map each tick.
- **Targeting Shodan** as a participant — director only.
- **Decomposing Objectives into Tasks inside the coordinator** — that's BotRunner's
  job; the coordinator emits Objectives only.

## Related skills

- [[mode-handler-implementation]] — the mode that invokes the coordinator.
- [[activity-catalog-bootstrap]] — define the Activity it drives.
- [[botrunner-task-implementation]] — the Tasks each Objective decomposes into.
- [[live-validation-test-authoring]] — verify multi-bot behavior.
- Reference: `docs/Spec/02_STATEMANAGER.md`, `docs/Spec/04_ACTIVITIES.md`.
