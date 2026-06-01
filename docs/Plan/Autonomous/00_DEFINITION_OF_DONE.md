# WWoW Autonomous — Definition of Done ("everything works")

> **Purpose.** The machine-checkable target the autonomous system is
> built to reach, so an autonomous agent (Codex, or a Claude `/loop`)
> has an unambiguous answer to *"is it done yet?"* for every
> capability — and so "everything works" is a predicate, not a vibe.
>
> **Companion docs (this folder):**
> - [`CHURN_TRACKER.md`](CHURN_TRACKER.md) — the dependency-ordered
>   backlog of work items that close the gap to this end-state.
> - [`CODEX_LOOP_PROMPT.md`](CODEX_LOOP_PROMPT.md) — the per-iteration
>   cadence an autonomous agent follows against the tracker.
> - [`LIVE_RUNBOOK.md`](LIVE_RUNBOOK.md) — how to run the live suite
>   headless against the up VMaNGOS stack.
> - [`PROGRESSION_LAYER_PLAN.md`](PROGRESSION_LAYER_PLAN.md) — the
>   "what already exists vs. what's missing" map for the autonomous
>   planner (Phase C).
> - [`S20_COMPOSER_PLAN.md`](S20_COMPOSER_PLAN.md) — the load-bearing
>   `IActivity`/`IObjective` runtime + `IActivityComposer` plan (the
>   single biggest unblocker, Phase A → B).
>
> **Source-of-truth docs this derives from (do not duplicate them):**
> - [`../../Spec/00_VISION.md`](../../Spec/00_VISION.md) — the 3,000-bot
>   north star + the original acceptance criteria.
> - [`../../Spec/05_PROGRESSION.md`](../../Spec/05_PROGRESSION.md) +
>   [`../07_PHASE6_AUTOPROGRESSION.md`](../07_PHASE6_AUTOPROGRESSION.md)
>   — the RosterPlanner / ProgressionPlanner / organic-group design.
> - [`../../leveling-guide/decision-engine/`](../../leveling-guide/decision-engine/)
>   — the domain knowledge base (the *how*); this doc is the *acceptance*.
> - [`../Activities/00_INDEX.md`](../Activities/00_INDEX.md) — the 86-row
>   activity catalog + the per-row training-trace + dynamic-progressive
>   contract.
>
> **This layout layers on top of the existing phased Plan; it does not
> replace it.** `Plan/00_OVERVIEW.md` + the phase docs (`01`…`16`) +
> `Spec/` remain the detailed "how." This `Autonomous/` set is the
> dependency-ordered, self-verifying "what to churn next" view, with an
> `Accept` gate on every row. Where a tracker row maps to an existing
> phase slot (e.g. `S2.0`, `S1.x`, `Plan/14/S10.x`) it cites it.
>
> **Last updated:** 2026-05-28 (layout session, WWoW `main` @ `be6c32af`).

---

## 0. The north star (one paragraph)

A population of bot characters runs Vanilla WoW 1.12.1 on the local
VMaNGOS server continuously, **with no human present**, such that:

1. **Any single fresh character autonomously progresses from level 1 to
   60** with a viable talent build, a credible per-spec gear set
   (dungeon/pre-raid floor → raid tier as stretch), at least one
   gathering + one crafting profession maxed, the attunements its
   chosen content needs, riding mounts, and a working economy footprint.
2. **A fresh human walking onto the server cannot tell, from gameplay
   observation alone, that the world is machine-driven** — the
   population is faction-balanced, covers all class/spec/profession
   combos, the economy is alive (AH/vendor/mail/trade-chat), and **no
   bot is ever idle** (idle is a bug, per `Spec/00_VISION.md §2`).

"Everything works" = the autonomous loop drives a character's
`CharacterProgression` from the **initial state** to the **terminal
state** in §2, and every capability the loop depends on has a
**reliably green live integration test** (§1 acceptance ladder), and a
small roster runs that loop unattended over a soak (§2.3).

This is deliberately ambitious. It is reached **incrementally** —
[`CHURN_TRACKER.md`](CHURN_TRACKER.md) decomposes it into rows, each
with its own `Accept` gate, so progress is always measurable and the
loop never has to "boil the ocean" in one step.

---

## 1. The three meanings of "works" (the acceptance ladder)

Every capability passes through three rungs. A tracker row may target
any rung, but **only rung 2+ counts as "done" for a gameplay
capability**, and the autonomous goal requires rung 3.

| Rung | Name | What it means | How it's proven |
|---|---|---|---|
| **R-Unit** | Logic green | The decomposition / predicate / planner / composer logic is correct in isolation. | A unit slice in `Tests/BotRunner.Tests` (or the relevant `*.Tests`) passes; per-row shape pins. |
| **R-Live** | Behavior green | The bot actually performs the capability against the live VMaNGOS stack — FG (injected) and/or BG (headless) — via a `LiveValidation` test driving an **Activity** (not a hand-built `ObjectiveMessage`). | A named `Category=Integration` test passes in a clean run **executed this iteration** (not a stale status claim), and its trace satisfies the **dynamic-progressive invariant** (`roster_distance_delta ≤ 0` on completion — `Activities/00_INDEX.md §Dynamic-progressive invariant`). |
| **R-Auto** | Sustained green | The autonomous planner *chooses* and *chains* the capability unattended, recovering from failure, for the target duration. | The roster loop (Phase D) drives it with no human input; observed via StateManagerUI + telemetry. |

> **Verification-trust rule (load-bearing).** A capability is **never**
> marked done on the strength of a status table alone. The
> `Activities/00_INDEX.md` board carries `done`/`partial` claims (e.g.
> the gathering family and the dungeon task-family are marked `done`)
> that predate a reliable headless harness. **A row is `done` only when
> its `Accept` gate passed in a run the current iteration executed and
> observed.** Stale green is treated as red. (This is not hypothetical:
> the sibling FFXI project marked a 6-bot party Activity "Live + tested"
> and it timed out when actually re-run — the same trap exists here.)

---

## 2. The initial state and the terminal state (the checkable bookends)

The autonomous system must read/write a **`CharacterProgression`**
model — a per-character completion-state projected onto
`WoWActivitySnapshot`. **This model does not exist yet as a runtime
type** (only the static `CharacterBuildConfig` does — see
[`PROGRESSION_LAYER_PLAN.md`](PROGRESSION_LAYER_PLAN.md)); building it is
CHURN row `C.statemodel`. "Done for one character" is a predicate over
that model. The planner's job is to drive `initial → terminal`.

### 2.1 Initial state (a freshly-provisioned character)

```
character.level                = 1
character.talents_spent        = 0
character.gear                 = starter gear only
professions                    = {} (none learned, or 1 at skill 1)
attunements.*                  = {}        (no MC/Ony/BWL/Naxx)
reputations.*                  = baseline  (neutral/at-war defaults)
mounts                         = {}        (no riding skill)
gold                           = a few silver
```

### 2.2 Terminal state ("everything works" for one character)

This is the **enumerated done-predicate**. Each line is a checkable
fact in `CharacterProgression`. The autonomous loop is "done for this
character" when **all** are true.

#### Level & build
- `character.level == 60`
- `character.talents_spent == 51` in a single viable tree (a complete
  spec build for the character's role; the build target comes from the
  class guide under `leveling-guide/classes/`, consumed as data — not
  hardcoded).

#### Gear (the credible-set floor)
- `gear.all_slots_filled == true` (no empty equipment slot).
- `gear.tier >= PreRaid` per spec — the dungeon-blue / pre-raid-BiS set
  the class guide names. **Raid tier sets (Tier 1/2/3) are a stretch
  dimension** (§5), gated on the raid Activities + attunements.
- A defined accessory floor: trinkets/rings/neck/cloak from the
  dungeon + quest-reward pipeline, not empty.

> The **canonical per-spec gear target** is enumerated in
> `docs/Plan/Autonomous/ITEM_TARGETS.md` (a tracker deliverable — CHURN
> row `C.itemset`). Until that file exists, the floor above is the
> acceptance bar. "All gear" is scoped to the **per-spec pre-raid
> progression set + dungeon-set/quest accessories**, NOT literally every
> item in the game (explicitly out of scope — §5).

#### Professions
- At least **one gathering profession at 300** and **one crafting
  profession at 300** (proves the gather→craft→economy capability
  end-to-end). All-professions-to-300 across the roster is the north
  star, not the per-character bar (§5).

#### Attunements & content access
- `attunements.mc == true` (Molten Core attuned — the floor raid
  attunement; the chain is the `attune.mc` Activity).
- Riding: `mounts.apprentice == true` (level 40) and
  `mounts.journeyman == true` (level 60), with the gold + faction-rep
  prerequisites met.
- BWL / Onyxia / Naxx attunements are **stretch** (§5).

#### Reputations
- The reputations the chosen build/content **needs** are at their gate
  standing (e.g. the faction rep for the riding mount; Argent Dawn for
  Naxx is stretch). "All reputations to Exalted" is **stretch** (§5).

#### Economy / currency
- The bot can earn and hold gold, run the `econ.vendor-loop`
  (vendor → repair → bank → mail) and `econ.ah-restock` cycles, and
  reach a steady-state gold floor without hand-seeding.

### 2.3 "Done for the population" (the autonomous north star)
- A configurable roster of **N characters** (the churnable bar is a
  small roster, e.g. 20 bots — `Plan/07` Phase 6's 24h-20-bot test;
  3,000-bot scale is **stretch**, §5) each holds a long-term goal and
  is **never idle** (idle is a bug — `Spec/00_VISION.md §2`,
  `Plan/07` exit criterion).
- Organic group formation: groups form when level/role-compatible bots
  converge on the same Objective — no scheduler, no leases
  (`Spec/05_PROGRESSION.md`).
- The population maintains steady-state economy seeding
  (AH/bazaar/mail/trade-chat).
- StateManagerUI shows population distribution, activity load,
  progression velocity, error rate.
- The whole thing runs unattended for a **multi-hour soak** with no
  human intervention and no unrecovered failure (`Plan/07` 24h-20-bot
  test, generalized).

---

## 3. The done-predicate as code (what the planner checks)

The autonomous planner must be able to evaluate the terminal state
programmatically. Target shape (to be implemented — CHURN row
`C.progression`; this type does NOT exist today):

```csharp
// Exports/GameData.Core/Progression/CharacterCompletionPredicate.cs (TARGET)
public static bool IsCharacterComplete(CharacterProgression p) =>
       p.Level == 60
    && p.TalentsSpent == 51
    && p.AllGearSlotsFilled
    && p.GearTier >= GearTier.PreRaid
    && p.GatheringProfessionAt300
    && p.CraftingProfessionAt300
    && p.HasAttunement(Attunement.MoltenCore)
    && p.HasRiding(RidingTier.Journeyman)
    && p.NeededReputationsAtGate;
// Stretch dimensions (raid tier sets, all profs, all attunements, all
// reps to exalted, PvP rank, 3000-bot scale) are evaluated by a
// separate IsCharacterFullyMaxed() — see §5.
```

The **per-tier sub-predicates** (`IsLevel60`, `IsSpecComplete`,
`IsPreRaidGeared(spec)`, `IsProfessionMaxed(prof)`,
`IsAttuned(raid)`, `HasRiding(tier)`) are what individual Activities
assert against. Some exist piecemeal today (e.g. the
`ProgressionPlanner` reads gear/rep gaps off the snapshot but the
resolvers are stubbed); CHURN row `C.progression` consolidates them
under one model. See [`PROGRESSION_LAYER_PLAN.md`](PROGRESSION_LAYER_PLAN.md).

---

## 4. Acceptance is tiered by the unlock graph (the order things become "done")

The planner advances a character through the
[`unlock-graph.md`](../../leveling-guide/decision-engine/unlock-graph.md)
in dependency order, picking the highest-priority eligible action via
the `PickNextAction` pseudocode in
[`decision-engine/README.md`](../../leveling-guide/decision-engine/README.md)
+ the five priority bands in
[`leveling-priority.md`](../../leveling-guide/decision-engine/leveling-priority.md)
(Survival 1000-1999 · Class-identity 800-999 · Critical-path
progression 600-799 · Optimal questing 400-599 · Background 100-399).
The bracket menus live in
[`per-bracket-actions/`](../../leveling-guide/decision-engine/per-bracket-actions/)
(`01-l1-l10.md` … `06-l55-l60.md`).

| Stage | Gate (entry) | Completion (exit) | Primary Activities needed |
|---|---|---|---|
| Leveling 1-60 | `level >= 1` | `level == 60` | `quest.starter.*`, `quest.zone.*`, `combat` (grind), `dungeon.*` |
| Build | `level` thresholds | `talents_spent == 51` | auto-train + auto-talent (a `recovery`/`equipment`-adjacent capability) |
| Professions | `level >= 5` | 1 gather + 1 craft @ 300 | `prof.*-route`, `prof.city-trainer-loop`, `econ.*` |
| Travel / mounts | `level >= 40` | riding tiers | `travel` (taxi/run), `econ.*` (gold), faction-rep prereq |
| Attunement (floor) | `level >= 55` | MC attuned | `attune.mc` (chain of quests + a dungeon clear) |
| Gear (floor) | per bracket | pre-raid set | `dungeon.*` + `quest.*` reward pipeline |
| Economy | always | gold steady-state | `econ.vendor-loop`, `econ.ah-restock` |
| Endgame (stretch) | `level == 60` | raid tiers / reps | `raid.*`, `rep.*`, `attune.{bwl,ony,naxx}`, `bg.*`, `boss.*` |

> The **two most load-bearing missing primitives** are:
> 1. **The `IActivity`/`IObjective` runtime + `IActivityComposer`**
>    (`Plan/03 S2.0`) — the runtime that *walks a catalog row into
>    Objectives→Tasks*. Today tests drive Activities via an
>    `AssignedActivity` string; **nothing composes a catalog row into a
>    live Objective graph.** Without it, no Activity reaches R-Live
>    through the real decision path. See
>    [`S20_COMPOSER_PLAN.md`](S20_COMPOSER_PLAN.md).
> 2. **The progression-layer runtime consumer** of
>    `decision-engine/` (`C.*`). The markdown is complete; **no code
>    reads it.** `ProgressionPlanner` is a hardcoded 8-priority stub
>    that returns `null` ~95% of the time (every resolver is a `TODO`).
>    Without it the Activities exist but nothing *chooses* which to run.
>    See [`PROGRESSION_LAYER_PLAN.md`](PROGRESSION_LAYER_PLAN.md).

---

## 5. Explicitly OUT of scope (so the loop doesn't chase infinity)

To keep "everything" finite and churnable, the following are
**stretch** tiers — tracked separately, never blocking the core
done-predicate (§3):

- **3,000-bot scale-load** (`Plan/08`, `Plan/07` scale). The churnable
  bar is a small roster soak (§2.3); the population scale + sharding +
  P99 latency targets are a load-test phase gated on dedicated hardware
  the loop cannot self-provision. In scope: the loop *works* at small
  scale and is *architected* not to preclude scale.
- **Full ML-advisor maturity** (`Plan/10`/`Plan/14`, `Plan/16`). The
  seven Spec/20 advisors (rotation / threat / reward / objective /
  chat_template / activity_request / personality_cluster) and the ONNX
  trainer are an *extension*. In scope: the planner makes correct
  deterministic (rules/trivial) choices and the advisor wire is stubbed
  with `NoAdvice` such that "advisors off" still completes within budget
  (`Plan/14` correctness invariant). The ML phases ride on top.
- **Raid tier sets / BiS / all-attunements / all-reps-to-Exalted / PvP
  rank grind.** A credible pre-raid set + MC attunement + the build's
  needed reps is the bar (§2.2); the full endgame grind is stretch.
- **All professions to 300 on one character.** One gather + one craft
  proves the capability; the rest are stretch.
- **Behavioral-variation personality system** (`Plan/16`) beyond a
  default profile — an indistinguishability *polish* layer, not the
  core loop.

When the core done-predicate (§3) is green for a fresh character via
R-Auto **and** a small roster soaks unattended (§2.3), **"everything
works" is achieved** for the primary goal. The stretch tiers extend it
toward the full `Spec/00_VISION.md` 3,000-bot living server but are not
the bar for this loop.

---

## 6. How to know the WHOLE repo is "churnable" (meta-acceptance)

This doc, plus the tracker and loop prompt, make the repo churnable
when **all** of these hold (the exit criteria for the "make it
churnable" handoff — see
[`NEW_SESSION_HANDOFF.md`](NEW_SESSION_HANDOFF.md)):

1. **A reliable verification harness.** A `tools/run-live.ps1` wrapper +
   `LIVE_RUNBOOK.md` let any agent run a single live test headless
   against the up VMaNGOS stack (docker preflight, the skip-restart +
   disable-UI gates, a `SetupTimedOut`-only retry), and **every
   Activity currently marked `done`/`partial` in `Activities/00_INDEX.md`
   either passes a clean run or is honestly downgraded** (the trust gap
   is closed). CHURN rows `A.3` + `A.4`.
2. **A populated `CHURN_TRACKER.md`** where every row has: an ID,
   explicit `Deps` (real IDs), an `Accept` column naming the exact
   test/predicate/doc that flips it to `done`, and an honest `Status`.
3. **The `CODEX_LOOP_PROMPT.md` is self-contained** — an agent can
   start from it with zero prior context and make correct progress.
4. **No human-only step is on the critical path — full autonomy.** The
   one prior decision gate (`Q-D5-1` pathfinding data-dir) is
   pre-resolved (Option A — repoint the test runner to `prod-data`); no
   live-RE rows are planned (WWoW's RE is mature). The loop makes +
   documents an autonomous-default for every choice and only stops on a
   failure STOP CONDITION or the docker stack being down.

When 1–4 hold, the operator can hand `CODEX_LOOP_PROMPT.md` to Codex and
walk away; Codex churns the tracker toward this Definition of Done with
no decision gates, surfacing the operator only on a hard failure.

---

## 7. Scope decisions made by the layout session (for human review)

These shape the DoD and are documented here so the operator can
redirect cheaply:

- **Layered, not replacing.** The `Autonomous/` set references the
  existing `Plan/` + `Spec/` + `leveling-guide/` as the domain "how";
  it adds the dependency-ordered, `Accept`-gated "what." The 16 phase
  docs are NOT superseded.
- **Core vs north star.** The *churnable core* is one character to the
  §2.2 terminal state + a small-roster soak (§2.3). The full
  `Spec/00_VISION.md` 3,000-bot living server is the north star, with
  scale-load + ML maturity + full endgame as explicit stretch (§5). If
  you want the 3,000-bot scale itself to be the churn target, say so —
  it changes Phase D/E sizing.
- **WoW-version scope.** This DoD targets **1.12.1 (Vanilla)** only.
  TBC/WotLK support (`CLAUDE.md` lists 2.4.3 / 3.3.5a) is out of scope
  for this loop.
- **Full autonomy — `A.6` pre-resolved.** The operator pre-approved
  Option A for the `Q-D5-1` pathfinding data-dir question (repoint the
  test runner to `prod-data` — non-destructive; never sync to
  `MaNGOS\data`). There are no remaining decision gates and no planned
  live-RE rows; the loop runs unattended and only surfaces the operator
  on a hard failure. If you would rather the loop pause on a given class
  of decision, add a `blocked:*` row in `CHURN_TRACKER.md`.
