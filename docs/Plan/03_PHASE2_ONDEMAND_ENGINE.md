# Plan 03 — Phase 2: OnDemand Engine

## Goal

The OnDemand Activity launcher is alive. Operator clicks an activity in
the UI → StateManager spawns pool bots, gears them, parties them,
teleports them to staging, and hands the human party leader. Any
catalog activity becomes "I want to do this NOW" via one button.

This is the headline user-facing capability. Everything else (autonomous
progression, scaling) is in service of this.

## Entry pre-requisite

Phase 1 complete (task families implementable).

## Exit criteria

- [ ] **Reserved pool** sized to 80 bots managed by
      `ReservedPoolManager`. Pool accounts declared in
      `Config/accounts/<realm>-ondemand-pool.json` per
      [`Spec/16_REALMS_AND_ACCOUNTS.md`](../Spec/16_REALMS_AND_ACCOUNTS.md).
- [ ] **`OnDemandActivityLauncher`** drives the 6-stage pipeline
      (Spawning, Outfitting, Partying, Travelling, Engaged, TearDown)
      for every catalog activity family.
- [ ] **Per-activity config files** at `Config/activities/<id>.json`
      exist for every catalog row, with explicit GearProfile,
      LoadoutSpec, LockoutSkip, RolePolicy, HumanJoinPolicy.
- [ ] **`AccountProvisioner`** creates new accounts via realmd SOAP
      when the pool needs them, hot-reloads config.
- [ ] **`CharacterProvisioner`** creates/recycles characters per
      activity's role template; `.character delete` for ephemeral
      tear-down.
- [ ] **LegalityValidator's fixup mode** — for OnDemand requests,
      validator returns a *list of GM-command fixes* the launcher
      applies before the activity starts (skip lockouts, set level,
      learn spells, set rep, etc.).
- [ ] **OnDemandActivitiesModeHandler** lives in
      `Services/WoWStateManager/Modes/` and is the default mode for
      production.
- [ ] **LiveValidation** per family: at least one dungeon, one raid
      family (formation+stage only), one BG, one quest, one
      profession (gather + craft), and one economy activity, each
      launched via OnDemand and asserted complete.
- [ ] **TearDown** correctly recycles or deletes characters and
      restores pool slots.
- [ ] **OnDemand metrics** populate the UI's Activities panel.

## Slots

### S2.0 — `IActivity` + `IObjective` runtime contracts

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Activities/IActivity.cs` (new)
  - `Exports/BotRunner/Activities/IObjective.cs` (new)
  - `Exports/BotRunner/Activities/ActivityResolver.cs` (modify — wrap, do not break callers)
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto` (add `current_activity_id`, `current_objective_id`, `current_objective_type` to `WoWActivitySnapshot`)
  - `Tests/BotRunner.Tests/Activities/IActivityContractTests.cs`
- **Spec contracts:** [`Spec/18_TERMINOLOGY.md`](../Spec/18_TERMINOLOGY.md), [`Spec/03_BOTRUNNER.md`](../Spec/03_BOTRUNNER.md), [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md)
- **Reference:** D2Bot's [`D2Orchestrator/Orchestration/Activities/IActivity.cs`](../../../D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs) and [`ObjectiveRuntimeContracts.cs`](../../../D2Bot/D2Orchestrator/Orchestration/ObjectiveRuntimeContracts.cs).
- **Goal:** Port D2Bot's runtime Activity/Objective contracts to WWoW so
  the OnDemand launcher (S2.1–S2.x) can dispatch activities through an
  `IActivity.NextAction(snapshot)` pump and assert on `IObjective` state
  transitions. Three sub-deliverables:
  1. `IActivity`: `string Id`, `IActivityParameters Parameters`,
     `ObjectiveMessage? NextAction(WoWActivitySnapshot snapshot)`,
     `ActivityCompletion CheckCompletion(WoWActivitySnapshot snapshot)`.
     Mirror D2's contract.
  2. `IObjective`: `string Id`, `ObjectiveType Type`,
     `ObjectiveEndState EndState`, `IReadOnlyList<ObjectiveGate> Gates`.
     Wire-shape mirrors D2's `BotObjectiveContract`.
  3. Snapshot extension: add `current_activity_id`,
     `current_objective_id`, `current_objective_type` fields to
     `WoWActivitySnapshot`. Backward compatible (proto3 additive). Tests
     drive `Activity × Objective` permutations and assert these new
     fields propagate end-to-end.
- **Non-goals (out of scope for S2.0):** Renaming `ActivityResolver`;
  porting D2's `AutonomousObjectivePicker` (that's S6.x autonomous-
  progression scope); refactoring the 117 LiveValidation violations
  catalogued in [`Audits/test-isolation-audit.md`](Audits/test-isolation-audit.md)
  (that's S5.x test-rewrite scope).

### S2.1 — `ReservedPoolManager`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/OnDemand/ReservedPoolManager.cs`
  - `Services/WoWStateManager/OnDemand/PoolBot.cs`
  - `Tests/BotRunner.Tests/OnDemand/ReservedPoolManagerTests.cs`
- **Spec contracts:** [`Spec/16_REALMS_AND_ACCOUNTS.md`](../Spec/16_REALMS_AND_ACCOUNTS.md)
- **Goal:** Track which pool accounts are idle vs reserved-for-instance.
  `TryReserve(role, faction, count)` atomically reserves pool slots.
  Release returns them.

### S2.2 — `AccountProvisioner`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/OnDemand/AccountProvisioner.cs`
- **Goal:** When pool short, creates accounts via realmd SOAP, writes
  to `Config/accounts/<realm>-ondemand-pool.json`, broadcasts
  ConfigChanged event.

### S2.3 — `CharacterProvisioner`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/OnDemand/CharacterProvisioner.cs`
- **Goal:** Create characters on pool accounts to match activity role
  template (faction/class/spec/race/gender). Recycle existing chars
  when possible. `.character delete` on teardown when config says
  ephemeral.

### S2.4 — `LegalityValidator` fixup mode

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Legality/LegalityValidator.cs`
  - `Services/WoWStateManager/Activities/Legality/LegalityFixupPlan.cs`
- **Goal:** For OnDemand caller, validator returns a `LegalityFixupPlan`
  with the ordered GM commands to apply (level up, learn spells, clear
  lockout, set rep, equip item). Launcher executes the plan in
  Outfitting stage.

### S2.5 — `OnDemandActivityLauncher`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S2.1, S2.2, S2.3, S2.4
- **Owned paths:**
  - `Services/WoWStateManager/OnDemand/OnDemandActivityLauncher.cs`
- **Spec contracts:** [`Spec/02_STATEMANAGER.md#ondemand-activity-launcher`](../Spec/02_STATEMANAGER.md#ondemand-activity-launcher)
- **Goal:** Drive the 6-stage state machine. Emit stage-transition
  events. Cancellable. Reusable across all activity families.

### S2.6 — `OnDemandActivitiesModeHandler`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S2.5
- **Owned paths:**
  - `Services/WoWStateManager/Modes/OnDemandActivitiesModeHandler.cs`
- **Goal:** Routes UI requests + Shodan whispers to the launcher.
  Coexists with `AutomatedModeHandler` (autonomous bots) on the same
  StateManager instance.

### S2.7 — Per-activity config files

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Config/activities/*.json` (one per catalog row, ~88 files)
- **Goal:** Each catalog activity has a config with explicit
  `LoadoutSpec`, `GearProfile`, `LockoutsToSkip`, `RolePolicy`,
  `HumanJoinPolicy`, `StagingCoord`, `BotRaidLeader` flag,
  `GearHuman` flag. Defaults ship reasonable per family; operator
  edits via UI.

### S2.8 — Activity coordinator upgrades

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S2.5
- **Owned paths:**
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Services/WoWStateManager/Coordination/DungeoneeringCoordinator.cs`
  - `Services/WoWStateManager/Coordination/RaidCoordinator.cs` (new)
  - `Services/WoWStateManager/Coordination/QuestCoordinator.cs` (new)
- **Goal:** Each coordinator implements `IActivityCoordinator` per the
  Spec/02 interface. Launcher dispatches to the matching coordinator
  by `ActivityDefinition.Family`.

### S2.9 — RewardSelector (trivial, Phase 4 has the upgrade)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Activities/RewardSelector.cs`
- **Goal:** Per
  [`Spec/03_BOTRUNNER.md#reward-selection`](../Spec/03_BOTRUNNER.md#reward-selection):
  trivial "first valid" selector. Never null. Phase 6 upgrade.

### S2.10 — LiveValidation per family (OnDemand)

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S2.5..S2.9
- **Goal:** One LiveValidation test per family:
  - `OnDemand_RagefireChasm_Dungeon` — 5 bots, full clear, human as
    leader.
  - `OnDemand_ZulGurub_RaidFormation` — 20 bots formed and ready-
    checked (no encounter scripts yet).
  - `OnDemand_WSG_Battleground` — 10v10 with bot pool fill, one cap.
  - `OnDemand_DefiasBrotherhoodQuest_Group` — 5 bots in Westfall
    objectives.
  - `OnDemand_MiningRoute_Profession` — 1 bot on a Wetlands mining
    circuit.
  - `OnDemand_AhRestock_Economy` — 1 bot posts 10 items to AH.

### S2.11 — UI panel hookup

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S2.5, Phase 3 (UI host alive)
- **Owned paths:**
  - `UI/WoWStateManagerUI/Views/ActivityRequestView.xaml`
  - `UI/WoWStateManagerUI/ViewModels/ActivityRequestViewModel.cs`
- **Goal:** UI "Launch OnDemand Activity" button opens the request
  form; operator picks activity + parameters; UI sends the launch
  via IPC. UI subscribes to stage-transition events and renders the
  launcher's live state.

## Out of scope for Phase 2

- Autonomous progression's decision engine (Phase 6).
- Long-term performance history rendering — basic Activities panel
  only (Phase 5 hits the full history view).
- Pathfinding scale + route packs (Phase 7).
- ML-driven reward selection (later).
