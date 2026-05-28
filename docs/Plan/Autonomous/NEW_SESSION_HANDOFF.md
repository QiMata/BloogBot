# WWoW Autonomous — New-Session Handoff ("make the repo churnable")

> **Purpose.** A complete, cold-start brief + paste-able prompt for a
> **new Claude session** whose single goal is to make this repo
> *churnable* — i.e., get it into a state where Codex can run
> [`CODEX_LOOP_PROMPT.md`](CODEX_LOOP_PROMPT.md) against
> [`CHURN_TRACKER.md`](CHURN_TRACKER.md) and make real, verified
> progress toward [`00_DEFINITION_OF_DONE.md`](00_DEFINITION_OF_DONE.md)
> with minimal human interaction.
>
> **Last updated:** 2026-05-28 (layout session, WWoW `main` @ `be6c32af`).

---

## Context the new session needs (what's already true)

- **Goal:** a fresh character autonomously progresses 1 → 60 with a
  viable build, a credible per-spec gear set, ≥1 gather + ≥1 craft
  profession maxed, the attunements its content needs, riding mounts,
  and a working economy footprint; and a small roster runs that loop
  unattended such that a human can't tell it's bot-driven. The full,
  checkable definition is
  [`00_DEFINITION_OF_DONE.md`](00_DEFINITION_OF_DONE.md). The 3,000-bot
  living-server (`Spec/00_VISION.md`) is the north star, with scale-load
  + ML maturity + the endgame raid grind as explicit **stretch** (DoD §5).
- **This is a layered view.** The `Autonomous/` set sits on top of the
  existing `Plan/` (16 phase docs) + `Spec/` + `leveling-guide/` +
  `Activities/` — it does not replace them. It is the
  dependency-ordered, `Accept`-gated "what to churn next" view.
- **The domain knowledge already exists** but has **no runtime
  consumer** — the gap. `leveling-guide/decision-engine/` has the
  complete `PickNextAction` pseudocode + state-flags + unlock-graph +
  priority bands + per-bracket menus; `Activities/00_INDEX.md` has 86
  catalog rows. **Nothing reads the decision-engine markdown, and nothing
  composes a catalog row into a live Objective graph.**
- **The harness is NOT yet turnkey.** `run-tests.ps1 -Layer 4` runs the
  whole `Category=Integration` layer against the up VMaNGOS stack, but
  there is no single-filter headless wrapper with docker preflight +
  skip-restart/disable-UI gates + setup-timeout retry. A
  **`tools/run-live.ps1` skeleton** was created this session (tracker
  `A.2`); the env gates it sets need wiring into the fixture (`A.1`).
- **The docker stack is up.** `wow-mangosd`, `wow-realmd`, `maria-db`,
  `wwow-pathfinding` (9002), `wwow-scene-data` (9003) all `Up (healthy)`
  as of 2026-05-28. You do NOT manage docker.
- **Two load-bearing unblockers** (DoD §4): **`B.composer`**
  (`Plan/03 S2.0` — the `IActivity`/`IObjective` runtime + composer;
  nothing turns a row into a live Objective graph) and **`C.goalplanner`**
  (replace the hardcoded `ProgressionPlanner` stub with a
  `decision-engine/`-driven chooser). See
  [`S20_COMPOSER_PLAN.md`](S20_COMPOSER_PLAN.md) +
  [`PROGRESSION_LAYER_PLAN.md`](PROGRESSION_LAYER_PLAN.md).
- **Two open threads the layout captured** (so nothing is dropped): the
  `Q-D5-1` pathfinding data-dir drift (`A.6`, an operator-decision gate)
  and the OG-Zeppelin tower-climb live test (`A.7`, unit-green/live-red,
  the subject of a ~40-commit caller-side loop). The pathfinding overhaul
  freeze is active (mesh fixes → `tools/MmapGen/` only).

## Your done condition (when the repo is "churnable")

Exactly the meta-acceptance in
[`00_DEFINITION_OF_DONE.md` §6](00_DEFINITION_OF_DONE.md). In short:

1. **The verification harness is reliable** — `A.1` (the env gates) +
   `A.2` (`run-live.ps1` + this runbook) + `A.3` (a single-bot live smoke
   anchor that passes 3/3) exist, and `A.4` re-validates every `00_INDEX.md`
   `done`/`partial` claim against a real run (the trust gap closed).
2. **The critical-path primitives work** — the activity-runtime composer
   (`B.composer`), base `combat`, `travel`, `questing`, and a solo XP
   loop (`B.solo-xp`) that gains levels unattended.
3. **The progression layer is stood up** — `CharacterProgression` +
   completion predicate (`C.statemodel`, `C.progression`), the
   unlock-graph loaded into a runtime DAG (`C.unlock-runtime`), and a
   data-driven `ProgressionPlanner` that picks the next Activity from
   live state (`C.goalplanner`). This is the heart of "autonomous."
4. **You have proven the loop** — run `CODEX_LOOP_PROMPT.md` yourself for
   several iterations and confirm an agent with zero context can make
   verified progress. Fix anything ambiguous in the prompt/tracker.

When 1–4 hold, hand `CODEX_LOOP_PROMPT.md` to Codex; it churns the rest
of the tracker (Phase B coverage + Phase D roster + the Phase E stretch),
escalating only at the isolated `A.6` operator decision + any
`blocked:human-RE` capture.

## How to work

- Drive the work **through `CHURN_TRACKER.md`** — you are effectively
  running `CODEX_LOOP_PROMPT.md` yourself, prioritizing the critical
  path: `A.1 → A.2 → A.3 → A.4 → B.composer → B.combat → B.travel →
  B.questing → B.solo-xp → C.statemodel → C.progression →
  C.unlock-runtime → C.goalplanner`. Update the tracker as you go.
- **Respect the verification-trust rule:** a row is `done` only when its
  `Accept` gate passes in a run you executed this session. No stale green
  (the `00_INDEX.md` gathering/dungeon `done` claims predate a reliable
  harness — re-verify).
- Commit + push per row (R15, specific files only — the tree has
  untracked logs + `*_wpftmp.csproj` + `tmp/test-runtime/`). Document
  each row's `Accept` result in the commit. Always
  `git -C "E:\repos\Westworld of Warcraft"`.
- When you hit the `A.6` operator decision or live-RE-gated work,
  scaffold + mark blocked + continue — do not stall.

---

## The takeover prompt (paste into a fresh Claude session)

```text
You are taking over the WWoW (Westworld of Warcraft) bot project with ONE
goal: make the repo "churnable" — get it into a state where the Codex
autonomous loop can make real, verified progress toward the project's
Definition of Done with minimal human interaction.

START HERE — read these in order, they are self-contained:
1. E:\repos\Westworld of Warcraft\docs\Plan\Autonomous\NEW_SESSION_HANDOFF.md
   (this file — context + your done condition)
2. E:\repos\Westworld of Warcraft\docs\Plan\Autonomous\00_DEFINITION_OF_DONE.md
   (the checkable target — "everything works")
3. E:\repos\Westworld of Warcraft\docs\Plan\Autonomous\CHURN_TRACKER.md
   (the dependency-ordered backlog you will drive)
4. E:\repos\Westworld of Warcraft\docs\Plan\Autonomous\CODEX_LOOP_PROMPT.md
   (the per-iteration cadence + how to run live tests)
5. E:\repos\Westworld of Warcraft\docs\Plan\Autonomous\LIVE_RUNBOOK.md
   (the headless live harness)
6. E:\repos\Westworld of Warcraft\docs\Plan\Autonomous\S20_COMPOSER_PLAN.md
   + PROGRESSION_LAYER_PLAN.md (the two load-bearing unblockers)
7. E:\repos\CLAUDE.md and E:\repos\Westworld of Warcraft\CLAUDE.md
   (durable rules R1-R18 + the four-layer model + SOAP-only MaNGOS +
   process-safety + the pathfinding freeze)

YOUR DONE CONDITION is the meta-acceptance in 00_DEFINITION_OF_DONE.md
§6: a reliable headless harness, the critical-path primitives working,
the progression layer stood up, and the Codex loop proven by you running
it for several iterations.

DO THE WORK by driving CHURN_TRACKER.md yourself, following the cadence
in CODEX_LOOP_PROMPT.md, prioritizing the CRITICAL PATH:
  A.1 (headless env gates) -> A.2 (run-live.ps1 + runbook)
  -> A.3 (single-bot live smoke anchor) -> A.4 (trust-gap audit)
  -> B.composer (IActivity/IObjective runtime + composer, S2.0)
  -> B.combat -> B.travel -> B.questing -> B.solo-xp
  -> C.statemodel + C.progression + C.unlock-runtime + C.goalplanner
     (the progression layer that consumes leveling-guide/decision-engine/).

HARD RULES:
- Verification-trust rule: a tracker row is Status: done ONLY when its
  Accept gate passed in a run you executed THIS session. Stale "it
  passed before" is treated as red (the 00_INDEX.md gathering/dungeon
  "done" claims predate a reliable harness — re-verify).
- Run live tests headless against the up VMaNGOS stack via
  `pwsh tools/run-live.ps1 -Filter <X>` (it docker-preflights the 5
  containers, sets WWOW_SKIP_SERVER_RESTART=1 + WWOW_DISABLE_UI=1,
  builds, runs the filter, retries on setup-timeout only). First confirm
  the stack is up (docker ps for wow-mangosd/wow-realmd/maria-db/
  wwow-pathfinding/wwow-scene-data). You do NOT manage docker — if it's
  down, stop and tell the human.
- R16: for any live Task/Action diagnosis, read the captured screenshot
  under tmp/test-runtime/screenshots/<test>/ — if the log says X but the
  PNG shows Y, trust the PNG.
- R17 IBotTask in Exports/BotRunner/ only (never StateManager); the
  ProgressionPlanner/GoalPlanner stays SM-side (it SELECTS the Objective,
  BotRunner decomposes it). R18 no parallel old/new — delete replaced
  code in the same commit (the ProgressionPlanner stub branches are
  DELETED when C.goalplanner lands).
- MaNGOS server ops are SOAP-only (port 7878) via the fixture helpers —
  NEVER write the MySQL DB directly. Shodan is the GM Liaison/test
  director, never a behavior-test subject.
- Process safety: NEVER blanket-kill dotnet/WoW.exe (D2Bot + FFXIBot run
  concurrently) — kill only PIDs your run launched.
- Pathfinding overhaul freeze: mesh fixes go in tools/MmapGen/, not the
  managed repair pipeline.
- R15 commit + push per row, SPECIFIC files only (never `git add -A` —
  untracked logs + *_wpftmp.csproj + tmp/test-runtime/). Always
  `git -C "E:\repos\Westworld of Warcraft"`. Hooks mandatory.
- GoalPlanner/progression consumes leveling-guide/decision-engine/ as
  DATA (unlock-graph, leveling-priority, per-bracket-actions). The guide
  is the only authoritative rule source — do NOT hardcode the journey.

AUTONOMY: proceed without asking; document every non-obvious decision in
commit messages + the tracker. Ask the human ONLY if you hit a true fork
that changes the Definition of Done, the A.6 operator decision (the
Q-D5-1 pathfinding data-dir repoint), or the docker stack is down.

WHEN YOUR DONE CONDITION HOLDS: update CHURN_TRACKER.md's snapshot, write
a short "repo is churnable" note (what's green, what Codex should pick up
next, the blocked set), and tell the human the repo is ready to hand to
Codex via CODEX_LOOP_PROMPT.md.

BEGIN: read the docs, verify the docker stack is up, then start at the
first ready critical-path row (A.1).
```

---

## For the human kicking off that session

- Make sure the VMaNGOS docker stack is up first (the session checks but
  can't start it): `docker ps` should show `wow-mangosd`, `wow-realmd`,
  `maria-db`, `wwow-pathfinding`, `wwow-scene-data` all `Up`.
- This session does the *enabling* work (harness + critical path +
  progression layer). Once it reports "churnable," you hand
  `CODEX_LOOP_PROMPT.md` to Codex for the long-tail coverage (the rest of
  Phase B + the Phase D roster + the Phase E stretch), checking in at
  phase boundaries.
- The only things that will ever need *you*: the `A.6` operator decision
  (the `Q-D5-1` pathfinding data-dir repoint — it changes shared bake
  state) and any live-RE capture for un-RE'd content. Both are isolated
  per-row and clearly flagged.
