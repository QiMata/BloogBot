# WWoW Autonomous — Codex Loop Prompt

> **What this is.** A self-contained prompt for an autonomous coding
> agent (Codex, or a Claude `/loop`) to churn
> [`CHURN_TRACKER.md`](CHURN_TRACKER.md) toward
> [`00_DEFINITION_OF_DONE.md`](00_DEFINITION_OF_DONE.md) with minimal
> human interaction.
>
> Paste everything in the fenced block below to start the loop. It
> assumes **zero prior conversation context** — everything needed is
> here or reachable from the linked files.

---

## Preconditions the operator sets up once (before starting the loop)

1. **The VMaNGOS docker stack is up and healthy.** Verify:
   `docker ps --filter name=wow- --filter name=maria --filter name=wwow- --format "{{.Names}}: {{.Status}}"`
   shows `wow-mangosd`, `wow-realmd`, `maria-db`, `wwow-pathfinding`,
   `wwow-scene-data` all `Up`. (The loop assumes the stack is up so it
   can use the headless gates — it does NOT manage docker itself.)
2. **The WoW 1.12.1 client + injection tooling exist:** `WoW.exe`
   (`GameClient:ExecutablePath` / `$env:WWOW_TEST_WOW_PATH`, e.g.
   `D:\World of Warcraft\WoW.exe`), the built `Loader.dll` /
   `Navigation.dll` / `FastCall.dll`. **Kill any running `WoW.exe`
   before a build** — the injector locks the DLLs (`CLAUDE.md`).
3. **Repo on `main`, clean-ish working tree.** The tree has many
   untracked logs + `*_wpftmp.csproj` + `tmp/test-runtime/` artifacts —
   **never `git add -A`** (R15).

---

## The loop prompt (paste this)

```text
Continue the WWoW (Westworld of Warcraft) Autonomous churnable-repo loop.

SOURCE OF TRUTH: read
`E:\repos\Westworld of Warcraft\docs\Plan\Autonomous\CHURN_TRACKER.md`
at the start of EVERY iteration. It is the canonical backlog. The target
end-state is
`E:\repos\Westworld of Warcraft\docs\Plan\Autonomous\00_DEFINITION_OF_DONE.md`.
Do NOT rely on memory of prior iterations — re-read the tracker fresh.

PER-ITER CADENCE
1. Verify HEAD: `git -C "E:\repos\Westworld of Warcraft" log -1 --oneline`.
   If newer commits exist than you expect, reconcile (a human or another
   agent may have pushed).
2. Confirm the VMaNGOS stack is up (docker ps for wow-mangosd / wow-realmd
   / maria-db / wwow-pathfinding / wwow-scene-data, all Up). If it is NOT
   up, STOP and report — you do not manage docker.
3. Read CHURN_TRACKER.md. Pick the FIRST row where Status: todo AND every
   Dep ID has Status: done. (Dependency-first; phase order secondary.)
   Prefer the critical path noted at the bottom of the tracker when
   multiple rows are ready.
4. Do the row's work in a SINGLE commit. For multi-iter rows (Iters > 1):
   either ship a partial slice + bump Iters-Done + leave todo, OR ship the
   whole row if scope allows. Prefer partial for Iters >= 3.
5. RUN THE ACCEPT GATE — this iteration, in a clean run you execute:
   - LIVE:<Class>[.<Method>]  → run the live test (see RUNNING LIVE TESTS
     below). It must PASS this run, AND its trace must satisfy the
     dynamic-progressive invariant (roster_distance_delta <= 0 on
     completion — Activities/00_INDEX.md).
   - UNIT:<Class>             → run the unit slice; must pass.
   - PRED:<expr>              → run the live Activity + read the written
     snapshot artifact and confirm the CharacterProgression predicate.
   - DOC:<file>               → the file exists + is internally consistent.
   - SOAK:<dur>               → only attempt when explicitly scheduled.
   THE VERIFICATION-TRUST RULE: a row becomes Status: done ONLY if its
   Accept gate passed in THIS run. A stale "it passed before" is NOT
   acceptance (the 00_INDEX.md gathering/dungeon "done" claims predate a
   reliable harness — re-verify, do not trust). If the gate fails, leave
   Status: todo, write what you observed into the row's notes, and either
   continue next iter or open a blocker row.
6. Update the tracker row: bump Iters-Done; on the final iter set
   Status: done + record the commit hash in Commit. Update the
   "Progress snapshot" block.
7. Regression check: run the affected unit slice
   (`pwsh run-tests.ps1 -Layer 3 -SkipBuild` for unit, or a targeted
   --filter). Do not regress the green slot baselines (NavigationPathTests,
   RecordedTests.Pathing, OgZeppelin — see the tracker snapshot).
8. Stage SPECIFIC files only (NEVER `git add -A` — the tree has untracked
   logs + *_wpftmp.csproj + tmp/test-runtime/):
   `git -C "E:\repos\Westworld of Warcraft" add <path1> <path2> ...`
9. Commit: short title + WHY paragraph + the tracker row ID + the Accept
   gate you ran + the result + any autonomous-default decisions. Hooks
   mandatory (never --no-verify / --no-gpg-sign).
10. Push: `git -C "E:\repos\Westworld of Warcraft" push origin main`.
11. Append a one-line iteration record to
    `E:\repos\Westworld of Warcraft\docs\TASKS.md` (per the repo's
    TASKS.md maintenance protocol in CLAUDE.md).
12. Continue to the next ready row. (If self-pacing via a scheduler,
    re-fire this same prompt; the tracker is the resume point.)

RUNNING LIVE TESTS (the Accept gate for LIVE: rows)
- PREFERRED — the run-live.ps1 wrapper (tracker A.2;
  docs/Plan/Autonomous/LIVE_RUNBOOK.md):
    pwsh tools/run-live.ps1 -Filter <TestClassOrMethod> [-NoBuild]
  It docker-preflights the 5 containers, sets the headless gates
  (WWOW_SKIP_SERVER_RESTART=1 + WWOW_DISABLE_UI=1), points TEMP/NUGET at
  repo-local tmp/, builds once, runs the filter, and retries up to
  -MaxAttempts (default 3) on setup-timeout ONLY (flaky boots; never a
  real behavioral failure). Exit 0 = green.
  (If run-live.ps1 does not exist yet, tracker row A.2 is the FIRST thing
  to build — do A.1 then A.2 before any other LIVE: gate.)
- Raw fallback (what the wrapper runs):
    dotnet build WestworldOfWarcraft.sln -c Debug
    dotnet test Tests/BotRunner.Tests --no-build `
      --filter "FullyQualifiedName~<TestClassOrMethod>" `
      --blame-hang --blame-hang-timeout 3m
  Live tests are Category=Integration; they boot a bot via StateManager
  (FG: inject Loader.dll into WoW.exe, connect on 9001; BG: headless
  WoWSharpClient) against the up VMaNGOS stack and poll StateManager
  snapshots (port 9000) with tight timeouts + crash checks.
- ARTIFACTS / R16: live tests write screenshots + snapshots to
  tmp/test-runtime/screenshots/<test>/ and results to
  tmp/test-runtime/results/. For ANY Task/Action diagnosis READ the
  captured PNG — if the log says X but the screenshot shows Y, trust the
  screenshot (the bug is in state detection, not the action).
- FLAKE POLICY: a LIVE row's Accept requires the test to pass. "Passed
  1/3" is NOT done — treat multi-bot/timing flakiness as a bug to fix
  (e.g. B.group organic formation), not noise to retry past.

MaNGOS SERVER OPS (test setup) — SOAP ONLY, NEVER direct MySQL writes
- All character/server mutation goes through SOAP (http://127.0.0.1:7878/,
  ADMINISTRATOR:PASSWORD) via the fixture helpers
  (LiveBotFixture.ExecuteGMCommandAsync / SendGmChatCommandAsync). Use
  `.reset items/level/spells/talents/all` for clean test setup, `.tele` /
  `.teleport` for positioning. Read-only MySQL is OK for connectivity +
  non-mutating lookups (faction/item ids). NEVER INSERT/UPDATE MaNGOS
  tables directly (CLAUDE.md).
- SHODAN is the production GM Liaison / test director — director only,
  never a behavior-test subject. Dispatch behavior tests to the dedicated
  test accounts (TESTBOT1/TESTBOT2, GATHFG1/BG1, etc.), assert on THEIR
  snapshots. Never SendActionAsync(shodan, ...).

AUTONOMOUS DEFAULTS (do NOT ask a human; proceed + document in the commit
+ tracker)
- New Activities follow the per-Activity build contract in CHURN_TRACKER.md
  Phase B header + the matching docs/Plan/Activities/<family>.md. Catalog
  rows live in Services/WoWStateManager/Activities/ActivityCatalogRows.Shard*.cs
  (keep CatalogMarkdownDriftTests green). Copy-templates: gathering
  (GatheringRouteTask), dungeon.deadmines, econ.vendor-loop.
- New IBotTask files: Exports/BotRunner/ task families ONLY (R17 — never
  StateManager). Four-layer model Activity->Objective->Task->Action
  (Spec/18_TERMINOLOGY.md); Actions are atomic + local, Objectives cross
  the wire.
- New LiveValidation tests DRIVE an Activity (via IActivity.Start once
  B.composer/S2.0 lands; via the AssignedActivity string +
  StageBotRunner*Async helpers until then) and ASSERT THE SNAPSHOT —
  NEVER construct a raw ObjectiveMessage in the test body (CLAUDE.md
  test-isolation rule).
- Progression/GoalPlanner: consume the leveling-guide/decision-engine/
  markdown as DATA (state-flags, unlock-graph, leveling-priority,
  per-bracket-actions). The guide is the only authoritative rule source —
  do NOT hardcode the journey in C# (decision-engine/README.md).
- Snapshot additions: next-free protobuf ProtoMember (additive, never
  renumber — R10).
- Ids resolve by name via IGameDatabase / read-only MaNGOS lookups (no
  hardcoded ids). Faction/item ids carrying "Unverified" flags
  (QUESTIONS.md Q-S0.9.x) must be verified against mangos.* before use.

HUMAN-GATED WORK (the ONLY things you escalate)
- A.6 Q-D5-1 OPERATOR DECISION: the pathfinding test-runner data-dir
  repoint changes shared bake state. Propose the diff (recommended: point
  run-pathfinding-tests.ps1 at D:\wwow-bot\prod-data), then leave the row
  blocked:human-decision until approved. Continue other rows meanwhile.
- LIVE-RE CAPTURE: new memory offsets / packet ids for un-RE'd content.
  You CANNOT do this headless. Scaffold the wire/predicate, mark the row
  blocked:human-RE, document exactly what capture is needed, and CONTINUE
  to the next ready non-blocked row. Do not stall the loop.

DURABLE RULES (root + repo CLAUDE.md — non-negotiable)
- R13 validate in order scene-data -> FG/BG physics parity -> pathfinding;
  the FG_BG_PARITY_BREAK canary means physics, not the pathfinder.
- R15 commit + push per iter; stage specific files (never `git add -A`).
- R16 capture/inspect screenshots for any live Task/Action diagnosis; if
  the log says X but the PNG shows Y, trust the PNG.
- R17 IBotTask in Exports/BotRunner/ only (never StateManager). The
  ProgressionPlanner/GoalPlanner stays SM-side — it SELECTS an Objective
  and sends ObjectiveMessage; BotRunner decomposes it into Tasks.
- R18 no parallel old/new — delete replaced code in the same commit (the
  ProgressionPlanner stub branches are DELETED when C.goalplanner lands).
- R10 proto changes compile all clients (additive ProtoMember).
- R14 game paths via env vars (WWOW_TEST_WOW_PATH etc.); no hardcoded
  drive letters in new code.
- Pathfinding overhaul freeze: mesh fixes go in tools/MmapGen/, NOT the
  managed repair pipeline (docs/physics/PATHFINDING_OVERHAUL.md).
- Process safety: NEVER blanket-kill dotnet/WoW.exe — other repos
  (D2Bot, FFXIBot) run concurrently. Kill only PIDs your run launched;
  use `run-tests.ps1 -CleanupRepoScopedOnly` for scoped cleanup.

WORKING DIRECTORY
- ALWAYS `git -C "E:\repos\Westworld of Warcraft"` for git.
- Absolute paths for file ops. PowerShell cwd may shift between
  `E:\repos` and the repo root.

STATE RECOVERY (checkpoints)
- Every 10 done rows, and on each Phase (A/B/C/D/E) completion, write a
  short progress note (in TASKS.md): rows done this batch (with commit
  hashes), Accept gates passed, current phase + next row, any blocked
  rows, any autonomous-default decisions a human should review.

STOP CONDITIONS
- All tracker rows Status: done (or done/blocked) — natural completion;
  report the blocked set for the human's decision/RE session.
- The VMaNGOS docker stack is down (precondition fails) — stop + report.
- A unit regression vs the green slot baselines you cannot resolve in 2
  iters.
- Build broken 3 consecutive iters.
- The SAME live test flakes red across 5 iters with no diagnosis progress
  — stop + write the repro + last-known state, escalate.
- 30 iters without measurable tracker progress — stop + record stuck
  state.

SOME ROWS HAVE COMPANION PLAN DOCS (read before implementing that row):
- B.composer (IActivity/IObjective runtime + IActivityComposer, S2.0):
  S20_COMPOSER_PLAN.md — the runtime that walks a catalog row into an
  Objective graph; today nothing does. Read it; do not re-derive.
- Phase C (C.statemodel/progression/itemset/unlock-runtime/goalplanner/
  advisor-wire): PROGRESSION_LAYER_PLAN.md — the "what already exists"
  map (the ProgressionPlanner STUB, the absent CharacterProgression
  model, the orphaned DecisionEngineService) + the decision-engine
  markdown to consume. C is complete+consolidate+replace, NOT
  build-from-scratch.

BEGIN with the first ready row in CHURN_TRACKER.md. Initial state
(2026-05-28): NOTHING is verified-done (fresh layout). The first ready
critical-path rows are A.1 (headless env gates) then A.2 (run-live.ps1 +
runbook) then A.3 (live smoke anchor) — you MUST stand up the harness
before any other LIVE: gate is meaningful. After A.1-A.4, the critical
path is B.composer (S2.0 — the load-bearing unblocker) -> B.combat ->
B.travel -> B.questing -> B.solo-xp -> C.* -> D.roster. Pure-unit rows
(C.statemodel after A.4) are the safest churn when a live slot is flaky.
```

---

## Why this loop is safe to run mostly-unattended

- **Every row self-verifies.** The `Accept` gate means the loop can only
  mark progress it actually observed — it cannot drift on stale status
  (and the `00_INDEX.md` `done` claims are explicitly distrusted).
- **The critical path is short.** Even a single unattended run that only
  reaches `C.goalplanner` produces a bot that autonomously levels.
- **Human gates are isolated + non-blocking.** Only the `A.6` operator
  decision + any live-RE capture need a person; the loop scaffolds and
  routes around them.
- **It fails loud, not silent.** Stop conditions catch flake-loops,
  regressions, and stuck states instead of churning forever.

## Notes for the operator

- **Pause:** stop replying to the scheduler / interrupt. The tracker is
  the resume point.
- **Re-scope:** edit `CHURN_TRACKER.md` directly; the loop reads it fresh
  each iter. Add/remove/re-order rows or change deps freely.
- **Audit trail:** every commit names its tracker row + `Accept` gate +
  result. `git log <start>..HEAD --format=fuller` is the full review.
- **Estimated effort:** the critical path is ~30–45 iters (the composer
  + progression layer are the heavy lifts); full coverage (all of B + the
  Phase E stretch) is ~90–130 iters. Spread across sessions; checkpoint at
  phase boundaries.
