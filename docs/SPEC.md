# Westworld of Warcraft — SPEC

> **This is the single entry point.** Every other doc is reached from here.
> If an autonomous agent reads only this file plus the linked subdocs, it has
> enough to pick up work without further human input.

## What we are building

A self-running Vanilla WoW 1.12.1 server population: 3,000+ machine-controlled
characters that quest, group, dungeon, raid, PvP, gather, craft, trade, and
participate in a live economy — indistinguishable at the protocol level from a
busy human server. Human players can join at any time and request **On-Demand
Activities** (any legal activity at any location/level range), and the system
forms the group around them.

Two execution paths live in the same codebase and must stay parity-equivalent:

- **ForegroundBotRunner (FG)** — DLL-injected into `WoW.exe`, reads memory,
  calls native functions. The ground-truth reference for behavior and physics.
- **BackgroundBotRunner (BG)** — headless C# protocol emulation (no game
  client), validated against FG packet/event recordings. Scale path.

A WPF operator console (`UI/WoWStateManagerUI/`) is the human surface for
configuration, monitoring, and on-demand requests. There is **no HTTP API
layer**; the UI talks directly to StateManager over the existing protobuf/TCP
contracts.

## How to read this spec

The spec is split into **stable contracts** (`Spec/`) and **implementation
phases** (`Plan/`). Contracts change rarely and describe *what must be true*.
Plans change as work proceeds and describe *what to do next, in what order, by
which owner*.

| Path | Purpose |
|---|---|
| [`Spec/`](Spec/) | Stable contracts — vision, architecture, components, telemetry, testing. |
| [`Plan/`](Plan/) | Phased implementation roadmap with dispatch-ready task slots. |
| [`Plan/Activities/`](Plan/Activities/) | Per-activity-family implementation detail (one file per family). |
| [`Reference/`](Reference/) → see [`physics/`](physics/), [`server-protocol/`](server-protocol/), [`leveling-guide/`](leveling-guide/) | Reverse-engineered + game-knowledge reference. Read-only authority. |
| [`Audits/`](Audits/) | Point-in-time captures (capability, livevalidation, dll-separation). |
| [`Archive/`](Archive/) | Completed handoffs and superseded specs. Do not delete; do not edit. |

Companion top-level docs:

- [`../CLAUDE.md`](../CLAUDE.md) — repo-local rules for Claude agents
- [`../AGENTS.md`](../AGENTS.md) — repo-local rules for Codex agents
- [`../../CLAUDE.md`](../../CLAUDE.md) — monorepo-wide rules (R1–R13)

## Stable contracts (Spec/)

| File | Topic |
|---|---|
| [`Spec/00_VISION.md`](Spec/00_VISION.md) | Final-state living-server requirements + acceptance criteria |
| [`Spec/01_ARCHITECTURE.md`](Spec/01_ARCHITECTURE.md) | Component graph, ownership boundaries, data flow |
| [`Spec/02_STATEMANAGER.md`](Spec/02_STATEMANAGER.md) | Modes, OnDemand launcher, hot reload, iterative scale |
| [`Spec/03_BOTRUNNER.md`](Spec/03_BOTRUNNER.md) | `IBotRunner`/`IBotTask` contract, FG/BG parity, behavior trees, reward selection |
| [`Spec/04_ACTIVITIES.md`](Spec/04_ACTIVITIES.md) | `ActivityDefinition`, hard-coded catalog, OnDemand siloed flow, legality |
| [`Spec/05_PROGRESSION.md`](Spec/05_PROGRESSION.md) | `RosterPlanner` + `ProgressionPlanner` (no scheduler; organic groups) |
| [`Spec/06_PATHFINDING.md`](Spec/06_PATHFINDING.md) | `PathfindingService` + `SceneDataService` contracts; route packs; scale |
| [`Spec/07_PHYSICS.md`](Spec/07_PHYSICS.md) | FG/BG physics parity contract; ground/walkable rules; validation harness |
| [`Spec/08_PROTOCOLS.md`](Spec/08_PROTOCOLS.md) | WoW 1.12.1 protocol surface; protobuf/TCP IPC framing |
| [`Spec/09_UI.md`](Spec/09_UI.md) | WPF Dashboard is the default app + test host; long-term history panel |
| [`Spec/10_METRICS.md`](Spec/10_METRICS.md) | Metric names, labels, cardinality budget, derived counters |
| [`Spec/11_LOGGING.md`](Spec/11_LOGGING.md) | Logging profile schema, suppression, Docker driver |
| [`Spec/12_ERROR_TAXONOMY.md`](Spec/12_ERROR_TAXONOMY.md) | Normalized failure enum (single source of truth) |
| [`Spec/13_TESTING.md`](Spec/13_TESTING.md) | Tests launch the UI; UI hosts StateManager; shared protobuf stream |
| [`Spec/14_CONFIG.md`](Spec/14_CONFIG.md) | Config schema; hot-reload protocol |
| [`Spec/15_SKILLS.md`](Spec/15_SKILLS.md) | Agent skill registry; cross-game replication contract |
| [`Spec/16_REALMS_AND_ACCOUNTS.md`](Spec/16_REALMS_AND_ACCOUNTS.md) | Two realms (Westworld-Test + Westworld), explicit account/character lists, OnDemand reserved pool |
| [`Spec/17_LOADOUT.md`](Spec/17_LOADOUT.md) | LoadoutSpec field reference, GM-command translation, proto mapping |
| [`Spec/18_TERMINOLOGY.md`](Spec/18_TERMINOLOGY.md) | Activity/Objective/Task/Action four-layer hierarchy (canonical glossary; adopted from D2Bot) |

## Implementation plan (Plan/)

| File | Phase | Substance |
|---|---|---|
| [`Plan/00_OVERVIEW.md`](Plan/00_OVERVIEW.md) | — | Phase map, dispatch rules, decisions of record |
| [`Plan/01_PHASE0_SPEC_HARDENING.md`](Plan/01_PHASE0_SPEC_HARDENING.md) | 0 | Catalog generation, schema tests, FailureReason enum |
| [`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`](Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md) | **1** | Physics, IBotTask gap closure, MovementController, path execution, FG-only gap closures |
| [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](Plan/03_PHASE2_ONDEMAND_ENGINE.md) | **2** | Spawn-gear-party-teleport-handoff to human; per-activity configs; reserved pool |
| [`Plan/04_PHASE3_UI_DEFAULT.md`](Plan/04_PHASE3_UI_DEFAULT.md) | **3** | UI is default app + test host; single-connection enforcement; failure screenshots |
| [`Plan/05_PHASE4_ACTIVITY_REGISTRY.md`](Plan/05_PHASE4_ACTIVITY_REGISTRY.md) | 4 | Catalog lookups, autonomous-mode legality, LockoutVerifier |
| [`Plan/06_PHASE5_OBSERVABILITY.md`](Plan/06_PHASE5_OBSERVABILITY.md) | 5 | Meters, Grafana, derived counters, long-term history API |
| [`Plan/07_PHASE6_AUTOPROGRESSION.md`](Plan/07_PHASE6_AUTOPROGRESSION.md) | 6 | RosterPlanner + ProgressionPlanner; organic group formation |
| [`Plan/08_PHASE7_PATHFINDING_SCALE.md`](Plan/08_PHASE7_PATHFINDING_SCALE.md) | 7 | Route packs, batch queries, sharding |
| [`Plan/09_PHASE8_LOAD.md`](Plan/09_PHASE8_LOAD.md) | 8 | Iterative scale: 80 → measure → optimize → hardware decision |
| [`Plan/10_PARALLEL_BRM_BAKE.md`](Plan/10_PARALLEL_BRM_BAKE.md) | parallel | Blackrock bake-fidelity (multi-cycle MmapGen) |
| [`Plan/11_PARALLEL_SKILL_REFINEMENT.md`](Plan/11_PARALLEL_SKILL_REFINEMENT.md) | parallel | Package WWoW patterns as portable skills so other games auto-bootstrap |
| [`Plan/Crashes/`](Plan/Crashes/) | research | WoW.exe crash clusters + WER capture infrastructure |

Activity detail (consumed by Phases 2–4):

| File | Activity family |
|---|---|
| [`Plan/Activities/00_INDEX.md`](Plan/Activities/00_INDEX.md) | Activity catalog status board |
| [`Plan/Activities/travel.md`](Plan/Activities/travel.md) | Multi-modal travel (walk, mount, flight, transport, hearthstone, portal) |
| [`Plan/Activities/quests.md`](Plan/Activities/quests.md) | Questing (accept, objective tracking, turn-in, chain selection) |
| [`Plan/Activities/dungeons.md`](Plan/Activities/dungeons.md) | All 26 dungeons (group formation, run plan, loot policy) |
| [`Plan/Activities/raids.md`](Plan/Activities/raids.md) | 10/20/40-man raids (formation, attunement gate, encounter scripts) |
| [`Plan/Activities/battlegrounds.md`](Plan/Activities/battlegrounds.md) | WSG/AB/AV (queue, objectives, faction balance) |
| [`Plan/Activities/professions-gathering.md`](Plan/Activities/professions-gathering.md) | Mining/Herbalism/Skinning/Fishing (route graph, node detection) |
| [`Plan/Activities/professions-crafting.md`](Plan/Activities/professions-crafting.md) | Alchemy/BS/Eng/Ench/LW/Tailor/Cook/FA (recipe walk, mat sourcing) |
| [`Plan/Activities/economy.md`](Plan/Activities/economy.md) | AH/bank/vendor/mail/repair loops |
| [`Plan/Activities/social.md`](Plan/Activities/social.md) | Group/raid/trade/whisper/channels/guild |
| [`Plan/Activities/combat.md`](Plan/Activities/combat.md) | 27 class/spec rotations; pull, rest, heal, buff |
| [`Plan/Activities/pvp.md`](Plan/Activities/pvp.md) | World PvP detection + engagement |
| [`Plan/Activities/reputations.md`](Plan/Activities/reputations.md) | 9 grindable factions |
| [`Plan/Activities/attunements.md`](Plan/Activities/attunements.md) | MC/Ony/BWL/Naxx/UBRS/ST/dungeon keys |
| [`Plan/Activities/world-events.md`](Plan/Activities/world-events.md) | STV Fishing Extravaganza, holidays |
| [`Plan/Activities/world-bosses.md`](Plan/Activities/world-bosses.md) | Azuregos, Kazzak, Emerald Dragons |
| [`Plan/Activities/recovery.md`](Plan/Activities/recovery.md) | Death/corpse run, stuck recovery, disconnect/reconnect, lease return |

## Decisions of record

Captured from the 2026-05-11 and 2026-05-12 design sessions. These are
commitments — they should only be revisited if the spec proves wrong
under load.

1. **ActivityCatalog is hard-coded**, not generated from `leveling-guide/`.
2. **Dashboard reads StateManager summary APIs only.** No direct Prometheus
   queries from the UI.
3. **Prometheus + Grafana surface in WPF** via summary APIs.
4. **All On-Demand Activities are legal for any human caller.** The
   OnDemand launcher forms the group around the human, circumventing
   normal gameplay restrictions per the activity config.
5. **WPF stays.** No HTTP API.
6. **Hot reload is required** for all reloadable sections.
7. **Iterative scaling.** Start at 80-bot capacity (AV). Measure, optimize,
   buy hardware as the dust settles. No fixed 3000-bot deadline.
8. **Tests assert via StateManager APIs.** Screenshots + JSON reports are
   dev aids only.
9. **WoW.exe crashes are bugs.** Each cluster gets a research file under
   [`Plan/Crashes/`](Plan/Crashes/).
10. **WWoW is the gold standard.** Every pattern is packaged as a portable
    skill before other game repos take the autonomous loop.
11. **Lease model dropped (2026-05-12).** No `BotLease`, no scheduler.
    Bots always on; OnDemand uses a siloed 80-bot reserved pool.
12. **Two realms (2026-05-12).** `Westworld-Test` (accelerated lockouts,
    GM enabled) and `Westworld` (vanilla defaults). Shared DB; separate
    mangosd configs. Every account/character explicitly declared. See
    [`Spec/16_REALMS_AND_ACCOUNTS.md`](Spec/16_REALMS_AND_ACCOUNTS.md).
13. **WPF UI is the solution's default startup project AND the test
    fixture host (2026-05-12).** Tests subscribe to the same protobuf
    stream the UI renders. See
    [`Plan/04_PHASE3_UI_DEFAULT.md`](Plan/04_PHASE3_UI_DEFAULT.md).
14. **OnDemand activities circumvent normal restrictions (2026-05-12).**
    StateManager applies GM-command fixes (level, spells, gear, lockout
    reset, rep) before the activity starts. Loot is ephemeral.
15. **Action/task is the foundation (2026-05-12).** Decision-making
    (priority bands, LLM personality, ML rewards) layers on top once
    the substrate works. See
    [`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`](Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md).
16. **Failure-event screenshots only (2026-05-12).** FG screenshots are
    captured on specific failure events (stuck, parity break, crash),
    overwritten per category, for agent triage. Not continuous streaming.

## Open questions (require human input)

These are unresolved as of 2026-05-11. Each blocks at least one downstream
task. Add to this list — never silently assume around them.

- See [`Plan/QUESTIONS.md`](Plan/QUESTIONS.md) for the rolling backlog.

## How an autonomous agent uses this spec

1. Read this file end-to-end.
2. Read [`Plan/00_OVERVIEW.md`](Plan/00_OVERVIEW.md) to see the current
   active phase and the dispatch rules.
3. Read the linked phase doc to see open task slots.
4. Claim a task slot per the ownership rules in
   [`Plan/00_OVERVIEW.md#dispatch`](Plan/00_OVERVIEW.md#dispatch).
5. Read the Spec contract(s) the slot touches.
6. Implement. Write tests. Run validation. Update the slot status.
7. If you discover a new task, add a slot. If you discover an ambiguity,
   add to `Plan/QUESTIONS.md` and proceed with a documented assumption.
8. Loop until the phase exits.

Single hard rule: **no work outside of a claimed slot**. The lead agent owns
slot creation; workers own slot execution; reviewers own slot acceptance.

## Top-level task board

[`TASKS.md`](TASKS.md) is the thin dispatcher — it shows which slots are
in-flight today, which agent owns each, and where to find the next slot to
claim. Everything else lives in the relevant `Plan/` file.
