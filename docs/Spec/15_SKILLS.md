# Spec 15 — Agent Skills

## Purpose

Skills are the unit of cross-game knowledge transfer. A skill is a
self-contained, executable playbook for one task category that an
autonomous agent can invoke without further human instruction.

WWoW is the gold standard. Every pattern that lands here must be
packaged as a skill before the autonomous loop moves on to FF XI, WAR,
UO, EQ, EQ2, PSO, Ragnarok, SWG. The skill catalog lets a fresh agent
in a new game repo bootstrap from "no idea where to start" to
"working from a slot in the local TASKS.md" without human help.

## Skill registry

Two registries:

1. **Repo-local skills** at `.claude/skills/<skill-name>/SKILL.md` —
   carry WWoW-specific implementation detail.
2. **Monorepo-shared skills** at `e:/repos/.claude/skills/<skill-name>/`
   (referenced from `~/.claude/skills/` on the workstation) — carry
   game-agnostic patterns reused across all game repos.

Each `SKILL.md` follows the structure:

```markdown
---
name: <slug>
description: <when to use>
trigger: <semantic trigger phrases>
---

## Goal
## Inputs
## Preconditions
## Procedure
## Verification
## Outputs
## Failure modes and recovery
## Related skills
```

## Existing skills (in WWoW today)

- `bot-profile` — create/modify class/spec combat profiles.
- `debugging` — investigate bugs, errors, unexpected behavior.

## Required new skills (Phase 10 parallel track)

These are the patterns WWoW has proven and that must be portable
before any other game repo starts autonomous work.

| Skill | Description |
|---|---|
| `activity-catalog-bootstrap` | Generate the compiled activity catalog from a game's leveling-guide and validate against MaNGOS equivalents. |
| `mode-handler-implementation` | Add a StateManager mode (`Test`/`Automated`/`OnDemandActivities`) for a game. |
| `coordinator-implementation` | Author a new activity coordinator (Dungeon/Raid/BG/Quest/Economy/etc.). |
| `botrunner-task-implementation` | Add an `IBotTask` family with FG+BG parity, tests, and live-validation. |
| `fg-bg-physics-parity` | Drive the FG→BG physics parity loop using the validator + bake pipeline. |
| `pathfinding-bake-iteration` | Iterate `tools/MmapGen/` per-tile config to close a navmesh fidelity gap. |
| `route-pack-generation` | Generate and validate route packs for a static long leg. |
| `protocol-handler-implementation` | Add a `CMSG`/`SMSG` handler with packet capture tests. |
| `loadout-template-authoring` | Convert a leveling-guide spec into a `CharacterBuildConfig` template. |
| `wpf-dashboard-panel` | Extend the operator console with a new panel + StateManager summary API. |
| `metrics-instrumentation` | Add new meters / labels per the metric naming spec. |
| `logging-noise-reduction` | Identify noisy categories + apply burst suppression / level adjustment. |
| `failure-reason-mapping` | Map a new failure path to the `FailureReason` enum. |
| `live-validation-test-authoring` | Author a LiveValidation test that exercises the full StateManager loop. |
| `config-hot-reload-subscriber` | Add an `IConfigSubscriber` for a new reloadable section. |
| `docker-stack-extension` | Add a new service container with metrics + log rotation. |
| `crash-cluster-triage` | Capture WER dump, root-cause WoW.exe crash, ship hardening patch. |
| `gold-standard-export` | Package a WWoW pattern as a portable skill, with verification cross-game. |

Each skill ships with:

1. The `SKILL.md` playbook.
2. At least one reference invocation in WWoW that exercised it.
3. A cross-game smoke test (run against FF XI repo as the validation
   target).

## Cross-game replication contract

When the autonomous loop moves from WWoW to another game (FF XI first),
the entry steps are:

1. Read the target repo's `CLAUDE.md` and `AGENTS.md`.
2. Read this monorepo's [`../../docs/MONOREPO_OVERVIEW.md`](../../docs/MONOREPO_OVERVIEW.md).
3. List skills relevant to the target repo's current open tasks.
4. For any task with no matching skill, the lead agent either:
   - Adapts an existing WWoW skill (and updates the skill to be
     game-agnostic), or
   - Creates a new skill, anchored to a WWoW reference invocation.
5. Workers claim slots in the target repo's TASKS.md and execute via
   skills.

The bar for "skill is ready for cross-game use" is:

- It works in WWoW (proven).
- Its `SKILL.md` references no WWoW-specific path or symbol without
  generalization.
- A smoke test exists in another game repo that exercises the skill
  end-to-end.

## Game-specific extensions

Each game repo carries `.claude/skills/<game>-*` for game-specific
overrides (e.g. `wwow-loadout-template`, `ffxi-job-rotation`). These
extend a shared base skill with the game's specifics. The lead agent
picks the most-specific applicable skill at task dispatch time.

## Existing code anchors

| Concept | File |
|---|---|
| Repo-local skills (WWoW) | `.claude/skills/bot-profile/SKILL.md`, `.claude/skills/debugging/SKILL.md` |
| Monorepo shared skills | (workstation) `~/.claude/skills/mmo-*` (per `CLAUDE.md` system prompt) |
| Skill plan (monorepo) | [`../../docs/SKILL_DEVELOPMENT_PLAN.md`](../../docs/SKILL_DEVELOPMENT_PLAN.md) |
| Repo-agent doc propagation | [`../../docs/REPO_AGENT_DOCS_PROPAGATION.md`](../../docs/REPO_AGENT_DOCS_PROPAGATION.md) |
