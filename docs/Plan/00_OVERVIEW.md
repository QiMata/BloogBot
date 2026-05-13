# Plan 00 — Overview, Dispatch Rules, and Phase Map

## Goal

Drive WWoW from current state (FG/BG parity green for OG cliff-fall,
pathfinding overhaul mid-flight, Automated/Test mode handlers live, no
OnDemand mode yet, lease model dropped 2026-05-12) to the spec's
acceptance criteria in [`Spec/00_VISION.md`](../Spec/00_VISION.md) —
all without human intervention between phases.

## Dispatch model

A **lead agent** owns the rolling task board ([`../TASKS.md`](../TASKS.md)).
Workers (Claude subagents, Codex subagents) claim **slots** with
explicit ownership, dependencies, and success criteria. Slot schema is
documented below; ownership and conflict-handling rules unchanged from
the 2026-05-11 design.

### Slot schema

```markdown
### S<phase>.<number> — <short title>

- **Owner:** `monorepo-worker` | `monorepo-test-runner` | `codex:codex-rescue` | `human` | ...
- **Status:** `open` | `claimed:<agent>` | `in-progress:<agent>` | `review` | `done` | `blocked:<reason>`
- **Depends on:** `S<phase>.<number>` (zero or more)
- **Owned paths:** glob list of files/directories the slot may write
- **Read-only paths:** glob list the slot may read but not modify
- **Spec contracts:** Spec/XX section references
- **Goal:** one paragraph
- **Procedure:** ordered steps
- **Success criteria:** specific test names / metrics / contract assertions
- **Failure modes & recovery:** known dead ends + how to step out
- **Artifacts:** TRX paths / JSON report paths / screenshot paths
```

### Ownership rules (unchanged)

1. Owned paths are exclusive — one in-progress slot per glob.
2. Read-only paths are shared.
3. Dependency edges are strict.
4. Status transitions one-way until done.
5. Workers do not edit slot definitions — only the lead agent does.

### Conflict handling (unchanged)

- Two workers wanting the same slot: lead assigns deterministically.
- Slot scope creep: worker proposes split; lead accepts or rejects.
- Spec ambiguity: write `Plan/QUESTIONS.md` entry, proceed with
  `⚠ ASSUMPTION` marker.

### Stop conditions (unchanged)

Loop continues through: compile errors, test failures, slow runs,
routine WoW.exe crashes (auto-open hardening slot).

Loop halts on: unresolved ambiguity blocking >50% of in-progress
slots, hardware failure, server downtime >1h, spec PR in review,
novel crash cluster after 3 failed triage attempts.

## Phase map (revised 2026-05-12)

```
Phase 0 — Spec hardening (current; partial)
  ↓
Phase 1 — Action / Task Foundation     ← physics, IBotTask, MovementController, path execution
  ↓
Phase 2 — OnDemand Engine               ← spawn pool bots, gear, party, teleport, hand-off to human
  ↓
Phase 3 — UI Default + Test Host       ← WoWStateManagerUI is the default app; tests host through it
  ↓
Phase 4 — Activity Registry            ← catalog lookups + autonomous-mode legality validator
  ↓
Phase 5 — Observability                 ← meters, Grafana, derived counters, long-term history panel
  ↓
Phase 6 — Automated Progression        ← RosterPlanner + ProgressionPlanner; organic group formation
  ↓
Phase 7 — Pathfinding/Scene Scale      ← route packs, batch queries, sharding
  ↓
Phase 8 — Living-server Load            ← iterative: 80 → measure → optimize → buy hardware → re-measure

Parallel tracks:
  Phase 10 — Blackrock bake-fidelity (multi-cycle MmapGen)
  Phase 11 — Skill refinement (package every WWoW pattern as a portable skill)
```

The reordering: **action/task work comes before everything else.** If
bots cannot reliably travel, fight, quest, gather, craft, or recover,
no amount of scheduling sophistication helps. Once Phase 1 lands, every
downstream phase has a working substrate to build on.

OnDemand Engine (Phase 2) is the **headline user-facing capability** —
operator clicks an activity in the UI, bots spawn-gear-party-teleport,
human plays. This is the value proposition the rest of the work
supports.

UI-as-default (Phase 3) is the test infrastructure foundation: every
test runs through the same UI host the operator uses. This locks the
contract between operator-observable state and test-asserted state.

Observability and autonomous progression come *after* OnDemand because:
- Metrics without working bots aren't operator-useful (Phase 5).
- Autonomous progression needs working tasks AND legality validation
  before it can plan (Phase 6 depends on 1 + 4 + 5).
- Scale work needs measurable bottlenecks (Phase 7 depends on 5).

## Decisions of record affecting this plan

From 2026-05-12 design session (additions to the original 10):

11. **Lease model dropped.** No `BotLease`, no `LeaseLedger`, no
    `ActivityScheduler`. Bots always on. OnDemand uses a reserved
    80-bot pool, siloed from autonomous progression. See
    [`Spec/02_STATEMANAGER.md`](../Spec/02_STATEMANAGER.md).
12. **Two realms, identical content, separate mangosd configs.**
    `Westworld-Test` (accelerated lockouts) and `Westworld`
    (production). Every account/character explicitly declared.
    See [`Spec/16_REALMS_AND_ACCOUNTS.md`](../Spec/16_REALMS_AND_ACCOUNTS.md).
13. **WPF UI is the solution's default startup project AND the test
    fixture host.** Tests subscribe to the same protobuf stream the UI
    renders. See [`Plan/04_PHASE3_UI_DEFAULT.md`](04_PHASE3_UI_DEFAULT.md).
14. **OnDemand activities circumvent normal restrictions.** StateManager
    applies GM-command fixes (level, spells, gear, lockout reset, rep)
    before the activity starts. Loot is ephemeral; instances are siloed.
15. **Action/task is the foundation.** Decision-making (priority bands,
    LLM personalities, ML reward selection) is layered on top once the
    substrate works.
16. **Iterative scaling, not target-driven.** We measure at 80 bots,
    optimize, buy hardware accordingly. No fixed "3000-bot" deadline.

## Per-phase entry points

| Phase | File | Pre-req |
|---|---|---|
| 0 | [`01_PHASE0_SPEC_HARDENING.md`](01_PHASE0_SPEC_HARDENING.md) | This SPEC tree complete |
| 1 | [`02_PHASE1_ACTION_TASK_FOUNDATION.md`](02_PHASE1_ACTION_TASK_FOUNDATION.md) | Phase 0 done |
| 2 | [`03_PHASE2_ONDEMAND_ENGINE.md`](03_PHASE2_ONDEMAND_ENGINE.md) | Phase 1 done |
| 3 | [`04_PHASE3_UI_DEFAULT.md`](04_PHASE3_UI_DEFAULT.md) | Phase 1 + 2 in flight |
| 4 | [`05_PHASE4_ACTIVITY_REGISTRY.md`](05_PHASE4_ACTIVITY_REGISTRY.md) | Phase 0 + 1 + 2 |
| 5 | [`06_PHASE5_OBSERVABILITY.md`](06_PHASE5_OBSERVABILITY.md) | Phase 1 + 2 + 3 in flight |
| 6 | [`07_PHASE6_AUTOPROGRESSION.md`](07_PHASE6_AUTOPROGRESSION.md) | Phase 1 + 4 + 5 |
| 7 | [`08_PHASE7_PATHFINDING_SCALE.md`](08_PHASE7_PATHFINDING_SCALE.md) | Phase 5 (metrics for measurement) |
| 8 | [`09_PHASE8_LOAD.md`](09_PHASE8_LOAD.md) | Phase 7 |
| 10 (parallel) | [`10_PARALLEL_BRM_BAKE.md`](10_PARALLEL_BRM_BAKE.md) | None |
| 11 (parallel) | [`11_PARALLEL_SKILL_REFINEMENT.md`](11_PARALLEL_SKILL_REFINEMENT.md) | A pattern lands |

Crash research files: [`Crashes/`](Crashes/).

## Activity coverage

The per-family slots in [`Activities/`](Activities/) feed into Phases
1, 2, and 6:

- **Phase 1** picks the foundational task for each family (one
  representative task per family, FG+BG green).
- **Phase 2** wires each family's coordinator into the OnDemand
  launcher.
- **Phase 6** picks per-bracket progression objectives from each
  family.

## Rolling task board

[`../TASKS.md`](../TASKS.md) shows the **currently active slots**. The
phase files contain the full enumeration; TASKS.md is the working
subset (last-N-days).

## Open questions

[`QUESTIONS.md`](QUESTIONS.md) — the rolling backlog. The 2026-05-12
session resolved the 29-question batch; new entries will arrive as
workers hit slot-level ambiguities.
