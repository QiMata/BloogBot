# Plan 11 — Parallel: Skill Refinement

## Goal

Every pattern that lands in WWoW is packaged as a portable skill so
the same autonomous loop can be pointed at FF XI, WAR, UO, EQ, EQ2,
PSO, Ragnarok, SWG and pick up work without human intervention.

## Trigger

This phase runs **alongside** the main path. Whenever a new pattern
lands (a coordinator, a task family, a physics fix, a metric
instrumentation pattern), a slot is opened here to skill-ify it.

## Exit criteria

- [ ] Every skill listed in [`Spec/15_SKILLS.md`](../Spec/15_SKILLS.md)
      exists either repo-locally or in the monorepo-shared registry.
- [ ] Each skill has at least one reference invocation in WWoW.
- [ ] FF XI repo's `CLAUDE.md` plus this monorepo's skill registry is
      enough for a fresh agent to pick a slot from FF XI's TASKS.md
      and execute it via skills (cross-game smoke test).
- [ ] Skill catalog drift test in `Tests/Tests.Infrastructure/`
      enforces that `Spec/15_SKILLS.md` matches the on-disk skills.

## Slots

(One slot per skill from [`Spec/15_SKILLS.md#required-new-skills`](../Spec/15_SKILLS.md#required-new-skills).
Workers open and close these as patterns land.)

### S10.1 — `activity-catalog-bootstrap` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.3 (catalog must exist before skilling the pattern)
- **Owned paths:**
  - `.claude/skills/activity-catalog-bootstrap/SKILL.md`
- **Goal:** Capture how to generate an activity catalog from a game's
  leveling guide + MaNGOS-equivalent database. Reference: WWoW
  `ActivityCatalog.cs`.

### S10.2 — `mode-handler-implementation` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S2.5

### S10.3 — `coordinator-implementation` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S2.5 (any of the upgraded coordinators is reference)

### S10.4 — `botrunner-task-implementation` skill

- **Owner:** `monorepo-worker`
- **Status:** open

### S10.5 — `fg-bg-physics-parity` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `.claude/skills/fg-bg-physics-parity/SKILL.md`
- **Goal:** Capture the validator-driven physics parity loop. Reference:
  the OG cliff-fall round-4 iter-5 VICTORY (2026-05-10).

### S10.6 — `pathfinding-bake-iteration` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S9.6

### S10.7 — `route-pack-generation` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.2

### S10.8 — `protocol-handler-implementation` skill

- **Owner:** `monorepo-worker`
- **Status:** open

### S10.9 — `loadout-template-authoring` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S4.3

### S10.10 — `wpf-dashboard-panel` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S35.x (any panel slot)

### S10.11 — `metrics-instrumentation` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S1.1

### S10.12 — `logging-noise-reduction` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S1.6, S1.7

### S10.13 — `failure-reason-mapping` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.5

### S10.14 — `live-validation-test-authoring` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S2.13

### S10.15 — `config-hot-reload-subscriber` skill

- **Owner:** `monorepo-worker`
- **Status:** open

### S10.16 — `docker-stack-extension` skill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S1.5

### S10.17 — `crash-cluster-triage` skill

- **Owner:** `codex:codex-rescue`
- **Status:** open
- **Depends on:** S9.5

### S10.18 — `gold-standard-export` skill (meta-skill)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.1..S10.17
- **Goal:** Document the process of taking a WWoW pattern and turning
  it into a portable, game-agnostic skill. The meta-skill that all
  others reference.

### S10.19 — FF XI cross-game smoke test

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.18
- **Owned paths:** `Final Fantasy XI/.claude/skills/` (game-specific
  overrides only)
- **Goal:** Point a worker agent at the FF XI repo and run a smoke
  task using only the skill registry. Success: agent picks a slot
  from FF XI's TASKS.md and produces a passing PR without further
  human input.

### S10.20 — Skill catalog drift test

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Tests/Tests.Infrastructure/SkillCatalogTests.cs`
- **Goal:** Asserts every skill listed in
  [`Spec/15_SKILLS.md`](../Spec/15_SKILLS.md) exists on disk; every
  on-disk skill is listed in the spec.
