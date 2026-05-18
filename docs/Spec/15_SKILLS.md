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

## Skill applicability scoring (cross-game auto-bootstrap)

When the autonomous loop arrives at a target game repo (FF XI, WAR,
UO, EQ, EQ2, PSO, Ragnarok, SWG, ...), the **first** question is
"which of the WWoW-proven skills apply here, and in what order should
the lead agent pick them up?" Answering that by hand for every new
game repo defeats the cross-game scaling story.

The **skill auto-bootstrap** mechanism is an off-line tool that
scores each skill against a target game repo and produces a ranked
applicability list. The tool consumes:

| Input | Source |
|---|---|
| Skill descriptors | `.claude/skills/<skill>/SKILL.md` frontmatter (`name`, `description`, `trigger`) |
| Target repo CLAUDE.md and AGENTS.md | the entry-point docs the lead agent already reads |
| Target repo `docs/TASKS.md` open items | per-repo task board |
| Target repo file inventory | `git ls-files` rooted at the target repo |
| Spec/20 §6 traces | per-skill-invocation outcomes from WWoW (`tmp/test-runtime/traces/Skill_*/` — see §Trace capture) |

Per-skill output is a `SkillApplicability { skill_name, score [0..1],
rationale, suggested_task_ids[] }` record. The lead agent's task is
then to dispatch the top-N skills in score order.

### Trace capture for skills

Skill invocations produce a JSONL trace per
[`Spec/13 §Training-trace capture`](13_TESTING.md#training-trace-capture)
at `tmp/test-runtime/traces/Skill_<skill_name>_<test_method>/<ts>.jsonl`.
The `outcome` line carries a skill-specific delta: number of
target-repo TASKS.md items closed, number of new tests passing, lines
of code changed, and the standard `roster_distance_delta` (which for
cross-game work is the "tasks remaining to make this repo
WWoW-parity" axis — see [`Spec/05 §RosterPlanner.Distance`](05_PROGRESSION.md#rosterplannerdistance--the-canonical-progression-metric)
for the metric's general shape; the cross-game axis is added on top of
the WWoW-internal axes as `DistanceAxis.CrossGameParity` in a future
Spec/05 amendment).

## Failure-reason mapping

Skill-invocation failures map onto [`Spec/12`](12_ERROR_TAXONOMY.md):

| Failure | Spec/12 reason | Notes |
|---|---|---|
| `SKILL.md` declares a precondition that doesn't hold in the target repo | `task_precondition_failed` | skill skips with a logged reason; auto-bootstrap downranks |
| Skill's procedure step throws (e.g. a referenced file isn't there) | `task_unrecoverable` | skill abandons; auto-bootstrap captures the step + line for triage |
| Cross-game smoke test fails | `task_unrecoverable` | skill is marked **not portable**; auto-bootstrap excludes from future ranking until a fix lands |
| Skill not found by name in the registry | `catalog_invalid` | auto-bootstrap logs the typo + suggests the nearest match |
| `~/.claude/skills/mmo-*` shared skills folder absent on the workstation | `server_unavailable` | local-only repo skills still usable; cross-game skills temporarily unavailable |

No new Spec/12 values needed.

## ML integration — Skill auto-bootstrap scoring

**Surface.** Skill auto-bootstrap is **off-line tooling**, not a
runtime advisor. It produces a `SkillApplicability` ranked list at
the start of a cross-game work session; the lead agent reads the
list once and dispatches tasks. No per-tick RPC.

**Why no Spec/20 RPC.** A new game repo gets a skill ranking maybe
once per week (when the autonomous loop migrates). The Spec/20
advisor surface is for per-decision RPCs; this is the wrong shape.
The off-line tool consumes Spec/20 §6 traces as labeled-data input —
that consumption is sufficient for the "ML must consume Spec/20
surface" rule.

**Three maturity phases** per the Spec/20 §5 pattern:

| Phase | Source | Skill ranking algorithm |
|---|---|---|
| 1 — Heuristic | Hand-authored `.claude/skills/<skill>/triggers.json` regex over target repo CLAUDE.md + open TASKS.md items | If any trigger matches the repo's text, score = 1.0; else 0. Binary. |
| 2 — Rules + lookup | Per-game `Config/skill-applicability-rules.json` declares overrides: e.g. `{ "ffxi": { "wpf-dashboard-panel": 1.0, "pathfinding-bake-iteration": 0.4 } }` | Override beats Phase-1 trigger match |
| 3 — ONNX | `Services/DecisionEngineService/Models/skill_applicability/v1.onnx` — embedding-similarity model trained on (skill description, target repo CLAUDE.md + first 100 TASKS.md items) → success-probability label | Cosine-similarity ranking with per-skill confidence |

**Input feature vector** (Phase 3):

| Feature | Source |
|---|---|
| skill_name one-hot[64] | from the §Required new skills + §Existing skills tables |
| target_repo embedding[384] | sentence-transformer embed of target repo CLAUDE.md first 4 KB |
| open_task_text_embedding[384] | embed of the top-10 open TASKS.md items |
| prior_invocation_success_rate[1] | from `tmp/test-runtime/traces/Skill_<skill_name>_*/outcome.jsonl` aggregation across all prior target repos |
| file_inventory_hash_distance[1] | cosine similarity between WWoW file inventory and target repo file inventory |

**Output shape.** `SkillApplicability { skill_name, score [0..1],
rationale, suggested_task_ids[] }` per skill. Tool emits a JSON file
at `<target-repo>/.claude/skill-applicability-<ts>.json` for human
review. NOT auto-merged into the lead agent's dispatch queue.

**Fail-soft fallback.** No model → Phase-1 trigger regex. No regex
match → score 0 (skill not surfaced; lead agent must add it
manually). Lead agent can always override the ranking; the tool is
advisory.

**Live-validation guard.** Replaying the WWoW skill catalog through
the auto-bootstrap tool with the *WWoW repo itself* as the target
MUST produce score = 1.0 for every existing-and-proven WWoW skill.
This is the canary: a regression that breaks the scorer's ability to
match WWoW skills to WWoW is a hard failure. Asserted by
`SkillAutoBootstrap_WwowSelfTargetEveryProvenSkillScoresOneTest` in
§Test surface.

## Dynamic-progressive invariant

Skill auto-bootstrap MUST satisfy both properties:

1. **Dynamic.** Different target repos (FF XI vs UO vs SWG) MUST
   produce different skill rankings — game repos have different
   shapes (UO is C# but no MaNGOS, SWG is `python-server-emu` not
   C-based, PSO is much smaller). Identical target repos produce
   identical rankings (deterministic given fixed `Mode` and feature
   inputs).
2. **Progressive.** Every dispatched skill MUST close at least one
   item from the target repo's `docs/TASKS.md` (or a clearly
   documented "this skill produced infrastructure, no task closed
   yet" outcome). The aggregate `roster_distance_delta_cross_game`
   across a target-repo work session must be ≤ 0.

Asserted by
`Skills_DynamicProgressive_DispatchedSkillClosesTaskItemTest` in
§Test surface.

## Plan-slot cross-reference

| Slot | Owns | Section here |
|---|---|---|
| [`Plan/11_PARALLEL_SKILL_REFINEMENT.md`](../Plan/11_PARALLEL_SKILL_REFINEMENT.md) | The 18-skill backlog itself (existing parallel track) | §Required new skills |
| **(no slot yet — Plan follow-up)** | `Tools/SkillAutoBootstrap/` (Python off-line tool); `Config/skill-applicability-rules.json` | §Skill applicability scoring |
| [`Plan/14/S10.7`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | Trace pipeline that produces `Skill_*/outcome.jsonl` lines | §Trace capture for skills |
| [`Plan/13/S9.8`](../Plan/13_PHASE9_CATALOG_FILL.md) (catalog tests) | Drift-test pattern reused for `Tests/BotRunner.Tests/Spec/SkillsContractTests.cs` | §Test surface |

The "no slot yet" row joins the Plan-follow-up roster (10th orphan
through this pass: 6 from pass 11 + AnomalyDetector pass 14 +
FailureClusterer pass 15 + WoWStateManagerUIFixture pass 16 +
SkillAutoBootstrap here).

## Test surface

Contract tests live at
`Tests/BotRunner.Tests/Spec/SkillsContractTests.cs`. Tests assert
against the `.claude/skills/<name>/SKILL.md` files on disk and the
auto-bootstrap tool's JSON output; they do NOT invoke skills (skills
are agent-loop concepts, not runtime code).

- **`SkillsCatalog_EverySpec15ListedSkillHasSkillMd`** — every entry
  in the §Required new skills table AND §Existing skills section has
  a corresponding `.claude/skills/<name>/SKILL.md` file present in
  the repo, with required frontmatter (`name`, `description`,
  `trigger`).
- **`SkillsCatalog_SkillMdFrontmatterValidYaml`** — every
  `SKILL.md` parses as valid YAML frontmatter; `name` matches the
  containing folder name.
- **`SkillsCatalog_SkillMdHasRequiredSections`** — every `SKILL.md`
  has the 7 required sections from §Skill registry: Goal, Inputs,
  Preconditions, Procedure, Verification, Outputs, Failure modes and
  recovery, Related skills.
- **`SkillAutoBootstrap_WwowSelfTargetEveryProvenSkillScoresOneTest`** —
  the auto-bootstrap tool, run with the WWoW repo as the target,
  produces score ≥ 0.95 for every existing-and-proven skill (the
  scorer's calibration canary).
- **`SkillAutoBootstrap_CrossGameSmokeTestPasses`** — for each
  monorepo-shared skill, the smoke test in the target repo (FF XI
  by default) exits 0.
- **`Skills_DynamicProgressive_DispatchedSkillClosesTaskItemTest`** —
  the dynamic-progressive invariant. For ≥2 prior trace runs of the
  same skill against different target repos, the skill closed ≥1
  `docs/TASKS.md` item per repo (allowing infrastructure-only
  outcomes with explicit "no task closed yet" annotation). The
  aggregate `roster_distance_delta_cross_game` is ≤ 0.

## Existing code anchors

| Concept | File |
|---|---|
| Repo-local skills (WWoW) | `.claude/skills/bot-profile/SKILL.md`, `.claude/skills/debugging/SKILL.md` |
| Monorepo shared skills | (workstation) `~/.claude/skills/mmo-*` (per `CLAUDE.md` system prompt) |
| Skill plan (monorepo) | [`../../docs/SKILL_DEVELOPMENT_PLAN.md`](../../docs/SKILL_DEVELOPMENT_PLAN.md) |
| Repo-agent doc propagation | [`../../docs/REPO_AGENT_DOCS_PROPAGATION.md`](../../docs/REPO_AGENT_DOCS_PROPAGATION.md) |
