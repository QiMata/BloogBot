---
name: mode-handler-implementation
description: Add a StateManager mode handler (Test / Automated / OnDemandActivities / new) that decides what bots do at world-entry, per snapshot, and on external requests. Use when adding or changing a top-level StateManager operating mode.
trigger: add a StateManager mode, mode handler, IStateManagerModeHandler, OnWorldEntry, automated mode, on-demand activities mode, dispatch loadout at world entry
---

# Mode Handler Implementation

## Goal

Add a StateManager operating mode by implementing `IStateManagerModeHandler` and
registering it so `StateManagerWorker` dispatches to it based on the configured
`StateManagerMode`. The mode decides high-level behavior: what happens when a bot
enters the world, on each snapshot, and on external activity requests.

## Inputs

- The mode's behavior at the three lifecycle points: world-entry, per-snapshot,
  external request.
- Key files (verified):
  - Mode enum: `Services/WoWStateManager/Settings/StateManagerSettings.cs`
    (`enum StateManagerMode` — `Test`, `Automated`, `OnDemandActivities`).
  - Interface: `Services/WoWStateManager/Modes/IStateManagerModeHandler.cs`
    (`OnWorldEntryAsync`, `OnSnapshotAsync`, `OnExternalActivityRequestAsync`).
  - Examples: `Services/WoWStateManager/Modes/TestModeHandler.cs` (no-op,
    fixture-driven), `AutomatedModeHandler.cs` (auto-dispatch loadout at entry).
  - Dispatch + DI: `Services/WoWStateManager/StateManagerWorker.cs`,
    `Services/WoWStateManager/Program.cs`.
- Spec: `docs/Spec/02_STATEMANAGER.md` (mode definitions + handler contract).
- Area rules: `.github/instructions/services.instructions.md`.

## Preconditions

- You can articulate why an existing mode (`Test`/`Automated`/`OnDemandActivities`)
  doesn't already cover the need.
- `dotnet build Services/WoWStateManager/WoWStateManager.csproj` is green.

## Procedure

1. Add a value to `StateManagerMode` in `StateManagerSettings.cs`.
2. Create `Services/WoWStateManager/Modes/<Mode>ModeHandler.cs` implementing
   `IStateManagerModeHandler`; set `Mode` to the new enum value.
3. Implement `OnWorldEntryAsync` (one-shot setup, e.g. dispatch
   `APPLY_LOADOUT`/activity), `OnSnapshotAsync` (per-tick decisions, often via a
   coordinator — see [[coordinator-implementation]]), and
   `OnExternalActivityRequestAsync` (Shodan whisper / WPF request; throw if the
   mode doesn't support external requests).
4. Register in DI: `services.AddSingleton<IStateManagerModeHandler, <Mode>ModeHandler>()`
   in `Program.cs`.
5. Ensure `StateManagerWorker` selects the handler by the configured
   `StateManagerSettings.Mode`.

## Verification

- Build: `dotnet build Services/WoWStateManager/WoWStateManager.csproj`.
- Drive the mode in a LiveValidation test and assert on snapshots
  (see [[live-validation-test-authoring]]).
- `.\scripts\test-integration.ps1` for the end-to-end loop.

## Outputs

- New `Modes/<Mode>ModeHandler.cs` + enum value + DI registration.
- Worker dispatch wiring; doc update in `docs/Spec/02_STATEMANAGER.md` if the mode
  set changes (AGENTS.md §9).

## Failure modes and recovery

- **Heavy work in `OnSnapshotAsync`** — it runs every tick; delegate to a
  coordinator and keep per-tick cost low.
- **Unhandled external requests** — implement or explicitly reject
  `OnExternalActivityRequestAsync` so the GM Liaison path fails clearly.
- **Forgetting DI registration** — the worker won't find the handler.

## Related skills

- [[coordinator-implementation]] — the per-snapshot orchestration the mode invokes.
- [[live-validation-test-authoring]] — exercise the mode end-to-end.
- [[loadout-template-authoring]] — what `Automated` dispatches at world-entry.
- Reference: `docs/Spec/02_STATEMANAGER.md`.
