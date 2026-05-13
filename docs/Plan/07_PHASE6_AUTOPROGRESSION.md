# Plan 07 — Phase 6: Automated Progression

## Goal

Production bots are always on, progressing toward roster goals
without intervention. `RosterPlanner` decides account-level character
composition. `ProgressionPlanner` picks per-character next objectives
from priority bands. Groups form organically when level/role-compatible
bots converge on the same objective (no scheduler, no leases per the
2026-05-12 design refinement).

## Entry pre-requisite

Phase 1 (task families) + Phase 4 (registry + legality + LockoutVerifier)
+ Phase 5 (observability so we can see what's happening).

## Exit criteria

- [ ] `RosterPlanner` enforces faction-side, class, profession, spec,
      and PvP coverage rules.
- [ ] `ProgressionPlanner` returns a `ProgressionObjective` for every
      catalog activity family and respects lockouts (via `LockoutVerifier`)
      and server capabilities.
- [ ] Pre-built `CharacterBuildConfig` templates ship for at least
      one rep spec per class (9 templates) and 6 archetypes
      (FuryWarriorPreRaid, HolyPriestMCReady, FrostMageAoEFarmer,
      ProtPaladinTank, RestoDruidHealer, SubtletyRogueFarmer).
- [ ] `AccountRoster` persists between StateManager restarts.
- [ ] A 24-hour staged automated-progression test:
  - 20 bots, mixed levels 1-60.
  - Bots level (where below 60).
  - Bots farm gold (where below target).
  - Bots train spells (where catalog has training slots).
  - Bots equip gear (where slots are missing or upgrades available).
  - Bots run dungeons (when groups form).
  - Bots queue BGs (when bracket fills).
  - Bots post on AH, deposit at bank, send mail.
  - Dashboard shows continuous progression metrics with zero
    idle-bot warnings.

## Slots

### S6.1 — `RosterPlanner`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.3 (catalog), S16 (named accounts/characters)
- **Owned paths:**
  - `Services/WoWStateManager/Progression/RosterPlanner.cs`
  - `Services/WoWStateManager/Progression/AccountRoster.cs`
- **Spec contracts:** [`Spec/05_PROGRESSION.md#rosterplanner`](../Spec/05_PROGRESSION.md#rosterplanner)

### S6.2a — `RewardSelector` upgrade (progression-aware)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S2.9 (trivial selector from Phase 2), S6.1 (RosterPlanner)
- **Owned paths:**
  - `Exports/BotRunner/Activities/RewardSelector.cs` (rewrite)
  - `Tests/BotRunner.Tests/Activities/RewardSelectorProgressionTests.cs`
- **Spec contracts:** [`Spec/03_BOTRUNNER.md#reward-selection`](../Spec/03_BOTRUNNER.md#reward-selection)
- **Goal:** Replace the trivial selector with progression-aware logic.
  Reads `CharacterBuildConfig.TargetGearSet` and picks rewards that
  advance BiS; falls back to highest vendor value for non-gear rewards.
  Always non-null (invariant preserved).
- **Future:** Phase 11 (parallel) introduces an ML-trained selector
  once min/max progression data is available.

### S6.2 — `ProgressionPlanner` priority bands

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S6.1
- **Owned paths:**
  - `Services/WoWStateManager/Progression/ProgressionPlanner.cs`
  - `Services/WoWStateManager/Progression/PriorityBands/**`
- **Spec contracts:** [`Spec/05_PROGRESSION.md#progressionplanner`](../Spec/05_PROGRESSION.md#progressionplanner)

### S6.3 — `CharacterBuildConfig` templates

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S6.2
- **Owned paths:**
  - `Services/WoWStateManager/Progression/Templates/**`
  - `Tests/BotRunner.Tests/Progression/TemplateCatalogTests.cs`
- **Goal:** 15 templates covering all roles. Each template has gear
  list, rep goals, mount target, gold target, spec, talent build.

### S6.4 — `AccountRoster` persistence

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S6.1
- **Owned paths:**
  - `Services/WoWStateManager/Progression/AccountRosterStore.cs`

### S6.5 — Idle-bot watchdog

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S6.2
- **Goal:** Any production bot idle > 60 s without an assigned
  objective is flagged via `wwow.statemanager.bot.idle_total`.
  ProgressionPlanner must pick a next objective for it; idle bots
  are bugs. (Reserved-pool bots between OnDemand instances are not
  counted; they're properly idle by design.)

### S6.6 — 24-hour automated-progression test

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S6.1, S6.2, S6.3, S6.4, S6.5
- **Goal:** Long-running test (24h) measures:
  - Median XP/hour gain per leveling bot.
  - Median gold/hour gain.
  - Group form count (dungeon parties, BG queues, raid forms).
  - Idle-bot incident count (target: 0).
  - Pathfinding queue depth + latency.
- **Success criteria:** Smoke pass + JSON report archived; UI history
  panel shows continuous progression curves.
