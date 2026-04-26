# Handoff v5: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`).
> **Picks up from:** `docs/handoff_test_consolidation_v4.md` (last commit listed there: `ba9b1d3f`).
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/test_cleanup_audit.md` — Phase A audit; the v5 status table below
>   supersedes it where they conflict.
> - `docs/statemanager_modes_design.md` — Phase F design doc; locks the schema
>   for Automated/OnDemand modes. **Apply the three corrections recorded in
>   v4 (still all valid).**
> - `docs/handoff_test_consolidation_v4.md` — the previous handoff. **Don't
>   reread the whole thing — this v5 supersedes it where they conflict.**

---

## Why this work exists

The Shodan-shaped LiveValidation suite is architecturally clean: Shodan is
the production GM liaison, behavior tests dispatch `ActionType.*` against
dedicated test accounts (TESTBOT1/2 + per-category siblings), and the
fixture layer enforces the separation. The remaining problems are **bloat,
sequential execution, shallow taxi/transport coverage, and StateManager's
lack of a first-class `Automated` mode**. Phases B (delay cleanup), C
(concurrent FG/BG), D (real taxi/transport rides), F (modes), and E
(LiveFixture consolidation, gated on F-1 step 3) tackle these in order.

Phase F-1 is COMPLETE (v4). Phase E started in v4's session — see the
"What landed in v4" section below. Phase E's pilot test migration
(`EquipmentEquipTests`) is **blocked on a BG-bot first-login bug**; the
blocker plus the scope of the next investigation is the most important
thing this handoff conveys.

---

## What landed in v4's session (since `ba9b1d3f`)

This is foundational work for Phase E plus an orthogonal bug fix. The
test migration itself was reverted because of a BG-bot stability issue
that's larger than the pilot — see the dedicated section below.

- **AssertConfiguredCharactersMatchAsync wrapper-aware loader.**
  `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  now treats both the legacy bare-array config shape and the new
  `{ Mode, Characters }` wrapper as a flat list of CharacterSettings —
  mirroring the loader in `SeedExpectedAccountsFromStateManagerSettings`
  that v4 added. Without this fix any test that loads an Automated config
  via `AssertConfiguredCharactersMatchAsync` throws on the
  `EnumerateArray()` call.

- **`Services/WoWStateManager/Settings/Configs/Equipment.Automated.config.json`
  (draft).**
  Wrapped-schema config with `Mode=Automated` and per-character
  `Loadout` for EQUIPFG1 and EQUIPBG1 (spell 198 / skill 54 1 300 /
  supplemental item 36 — the same spec that
  `StageBotRunnerLoadoutAsync` issues today). Not yet wired into a
  test; ready for the next pilot attempt once the BG-bot blocker
  below is resolved.

- **`Exports/BotRunner/Helpers/DeathStateDetection.cs` —
  `CanReleaseSpirit` now requires `MaxHealth > 0`.**
  Real bot-runner bug fix discovered while debugging the Phase E
  pilot. Old behavior: returned true when `Health == 0`, even on
  uninitialized snapshots where `MaxHealth == 0` (i.e. the player
  object was still hydrating). Fresh BG bots hit this race on first
  login: the bot received `APPLY_LOADOUT`, immediately fired
  `PushDeathRecoveryIfNeeded` because `Health == 0`, and the
  diagnostic line `DeathRecovery: corpse detected but auto-release
  is disabled; waiting for explicit ReleaseCorpse action` repeated
  forever while the LoadoutTask sat idle on the stack. The new guard
  blocks that path until the player is actually hydrated. 43/43
  LoadoutTask + DeathState unit tests still pass after the change.
  The deeper "BG bot stuck in CharacterSelect after transition" issue
  is unrelated to this fix and remains open — see below.

End-to-end signal observed: Onboarding test still works against
TESTBOT2 with the new code (no behavior change for stable BG accounts).
EquipmentEquipTests still works against EQUIPFG1+EQUIPBG1 in Test mode
(the test was reverted to its pre-v5 form).

Confirm at session start (paste verbatim):

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline ba9b1d3f..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

If `git log` shows commits between `ba9b1d3f` and `origin/main` that
you didn't write, the user did manual work — read those commits before
continuing. The Docker stack must show `mangosd`, `realmd`,
`pathfinding-service` healthy.

---

## Phase E blocker: BG bot first-login flap

This is the most important thing in this handoff. It's the thing that
killed the v4 pilot attempt for `EquipmentEquipTests`.

### The symptom

Loading `Equipment.Automated.config.json` and waiting for the BG bot
(EQUIPBG1) to reach `IsHydratedInWorldSnapshot` fails. The BG bot:

1. Logs in. WoWSharpClient briefly emits `enteredWorld=True
   inTransition=True player=True pos=(0,0,0)`.
2. Spends 19–22 seconds in `[TRANSITION-SKIP]` state. Resumes once.
3. Receives `APPLY_LOADOUT` (3 ms after transition resumes).
4. Fires `DeathRecovery: corpse detected ...` — caused by the
   `CanReleaseSpirit` race (now patched in v4). Even with the patch,
   the bot stays at `screen=CharacterSelect`, `pos=(0,0,0)`,
   `char=`(empty) for the rest of its lifetime.
5. The BG bot's `ready` flag never flips to true, so `LoadoutTask`
   sits on the stack and never executes. The fixture times out
   waiting for an `IsObjectManagerValid && MaxHealth > 0` snapshot.

The FG bot (EQUIPFG1) on the same fixture launch worked correctly
through the same code path: it reached InWorld, received APPLY_LOADOUT,
ran the LoadoutTask, and reported `LoadoutInProgress` then
`LoadoutReady`. The bug is BG-side only.

The full diag log is at
`Bot/Release/net8.0/logs/botrunner_EQUIPBG1.diag.log` — every run
during v4's debugging produced the same pattern.

### What v3 already warned about

The v4 handoff (now the v3 handoff from the next agent's perspective)
flagged:

> One known fragility to plan for: the v3 attempt to reuse a brand-new
> ONBOARDBG1 account in the Onboarding test failed because newly-created
> BG accounts flap between InWorld and CharacterSelect during their
> first stable login window, which prevents LoadoutTask from finishing.
> The fix in v3 was to reuse TESTBOT2 (a stable, previously-used account).

The v4 attempt hit this exact issue. **EQUIPBG1's character (Garromokxfr)
was somehow corrupted before v4 ran**: the BG bot detected a corpse on
login even though the `corpse` table was empty. SOAP `.revive (offline)`
did not clear the state. v4 ran `.character erase Garromokxfr` to force
the BG bot to recreate from scratch — but the freshly-created character
also flapped, because the same first-login race hits brand-new accounts
just like it hit ONBOARDBG1 in v3.

### Recommended approach for the next session

Three options, ranked by how well they address the problem:

1. **Best — fix the BG bot's CharacterSelect-after-transition stall**
   (real bug). The bot's screen state machine should advance to
   `InWorld` once `enteredWorld=True` resolves, instead of staying in
   `CharacterSelect` indefinitely. Investigate
   `Services/BackgroundBotRunner/BackgroundBotWorker.cs` and the
   transition-resume path. Look for what signal (SMSG packet,
   ObjectManager flag, etc.) the bot is waiting for that never
   arrives. The CanReleaseSpirit guard from v4 was a precondition,
   not the whole fix — it removed the noise so the deeper stall is
   now diagnosable. Once a brand-new BG account can stabilize, the
   Equipment.Automated migration becomes a one-liner.

2. **Second-best — pre-warm a fresh BG character before the test
   relies on Automated mode.** v3 worked around this exact issue
   for the Onboarding test by switching from ONBOARDBG1 to TESTBOT2
   (an already-stable account). For Phase E, that means: before
   launching `Equipment.Automated.config.json`, first launch a Test-
   mode config that uses the same EQUIPFG1/EQUIPBG1 accounts and
   wait for them to reach `IsHydratedInWorldSnapshot`. After they
   stabilize once, subsequent restarts seem to skip the flap. This
   is fixture-side scaffolding rather than a real fix, but it
   unblocks Phase E migrations one at a time. The handoff explicitly
   said don't introduce new account prefixes, so a "warm-up pass"
   is the only Test-mode option that respects that rule.

3. **Worst — accept the blocker and pause Phase E.** Move to other
   in-progress phases (B's high-ROI BG queue/prep-window predicates,
   D's real taxi/transport rides, or F-2's OnDemand handler — none of
   which depend on Automated-mode loadout dispatch landing). This
   unblocks visible progress while the underlying BG-bot bug is
   tackled separately. Document that Phase E is paused on the v5
   blocker and ship the foundation pieces (which v4 already did).

### What NOT to do

- Don't keep deleting and recreating BG characters via
  `.character erase`. v4 tried this and it just produces fresh-but-
  unstable characters that hit the same flap. The DB state is not
  the root cause.
- Don't keep tightening SOAP revive variants (`.revive`,
  `.character revive`, `.modify hp`). v4 confirmed the `corpse`
  table is empty and the character_flags don't carry a ghost bit;
  the "corpse" is a BG-bot-side false positive.
- Don't move the AutomatedModeHandler dispatch later (e.g. wait for
  N consecutive `IsObjectManagerValid=true` ticks) — that just hides
  the bug; the LoadoutTask still won't run because `ready=False` is
  driven by the screen-state stall, not by the dispatch timing.

---

## Phase status — open items only

### Phase B — Snapshot poll hygiene (most easy wins extracted)

Unchanged from v4. The remaining bare-delay census in
`docs/test_cleanup_audit.md` § A1 is mostly low-ROI now. The high-ROI
items left:

| Wall | File:Line | Notes |
|---|---|---|
| 130s | `Battlegrounds/BattlegroundEntryTests.cs:123` | Highest single-fix savings. Replace with `CHAT_MSG_BG_SYSTEM` / queue-update marker poll. **Significant — needs to identify the right snapshot signal first.** Probably worth a research spike before coding. |
| 95s | `Battlegrounds/WsgObjectiveTests.cs:142` | Replace with prep-window-end snapshot signal. |
| 1.5s × 5 | `Raids/RaidCoordinationTests.cs:160,186,193,201,207`; `RaidFormationTests.cs:61,70,88,102,116` | Replace with `WaitForRaidMembershipAsync`-style predicates. Activity-owned, handle carefully. |
| 900ms × N | `CoordinatorFixtureBase.cs:817` (post-`SendGroupInvite`) | No `HasPendingGroupInvite` field on the snapshot proto today. Adding one would unlock this on a per-raid-member basis (~36s for a 40-bot raid setup). Touches `WoWActivitySnapshot` proto + FG/BG snapshot pipeline. |

### Phase C — Concurrent FG/BG (PENDING)

Unchanged from v4:
- **Mining take-turns.** Both bots launch `StartGatheringRoute` against the *same* node concurrently. Coordinate via a "claim/release" signal at the node level. Update `GatheringRouteSelection` to support two-bot convergence + graceful yield.
- **Herbalism follow.** FG gathers first node; on completion, FG enters a follow-mode (BotRunner-level `Follow` action against BG GUID). BG moves to a different node. Assert both bag entries land + FG ends within follow-distance of BG.
- **Generalize "FG follows BG"** as a BotRunner-side helper.

**Where to start:** `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`, `Exports/BotRunner/Tasks/GatheringTask.cs`, `GatheringRouteSelection.cs`. Claim/release signal needs a place to live — likely on `WoWActivitySnapshot` or a new `GatheringNodeClaim` message.

### Phase D — Real taxi/transport rides (PENDING)

Unchanged from v4:
- **`TaxiTests`**: full ride (e.g. Org → Crossroads). Assert bot lands on destination flightmaster's grid square within `ExpectedRideSeconds + slack`.
- **`TransportTests`**: split into `Boat_Or_Zeppelin_FullRide` (board on arrival, assert `MOVEFLAG_ONTRANSPORT`, assert at destination) and `Elevator_FullRide` (Undercity West / Thunder Bluff). All snapshot-driven, no sleeps.

**Where to start:** `TransportTests.cs` and `TaxiTests.cs` already have Shodan staging in place; the action dispatch is the gap. Look for `MOVEFLAG_ONTRANSPORT` in the snapshot proto. The transport-arrival timer table likely lives in BotRunner gameobject data — grep `transport.*schedule` or look at `FgTaxiFrame.cs`.

### Phase E — LiveFixture consolidation (BLOCKED on BG-bot stall)

**Pilot:** `EquipmentEquipTests`. Foundation pieces shipped in v4:
- `Equipment.Automated.config.json` (draft, ready to use)
- `AssertConfiguredCharactersMatchAsync` is now wrapper-aware
- `CanReleaseSpirit` no longer false-positives on uninitialized BG snapshots

The migrated test code itself was reverted because the BG bot can't
stabilize in-world for a brand-new or freshly-erased character. See
the dedicated "Phase E blocker" section above. Once that's unblocked,
the pilot migration is small (the v4 test diff is recoverable from
`git log -p docs/handoff_test_consolidation_v5.md` if needed — or
just rebuild it against the foundation that's already shipped).

After EquipmentEquipTests is green: repeat for `UnequipItemTests`,
`WandAttackTests`, etc., as planned in v4.

**One known fragility to keep planning for:** every Phase E migration
will hit the same BG-bot first-login flap unless the underlying bot
bug is fixed. Plan accordingly.

### Phase F-1 — StateManager Automated mode

**COMPLETE.** All three steps shipped in v4. See v4's "Phase F-1"
section for commit details.

### Phase F-2 / F-3 — OnDemand + production polish (PENDING)

Unchanged from v4 description. After Phase E (or in parallel if Phase E
stays blocked). In short:
- **F-2:** `OnDemandActivitiesModeHandler` + Shodan whisper-command
  routing + WPF UI POST endpoint on port 8088.
- **F-3:** Audit fixture helpers that still apply loadout / dispatch
  activities and migrate the production-relevant ones to BotRunner-side
  `LoadoutTask` / `ActivityResolver`. Trim the fixture-side wrappers
  to thin adapters.

### Phase G — Final cleanup (PENDING)

After everything above. See v3 § "Phase G".

---

## Three course corrections recorded so far (don't re-discover them)

These all carry over from v4 — none have changed.

### Correction 1 (from v2) — the `BattlegroundEntryTests` "3s × 7" batch is a false positive

The audit prescribed batching the 3s/5s/10s delays in
`Battlegrounds/BattlegroundEntryTests.cs`. **Skip this entirely.** Every
site is the poll-pacing tail of an outer `while` loop that already checks
the success condition. On 80-bot BG fixtures, tightening to 500ms would
multiply `QueryAllSnapshotsAsync` load without saving wall time.

### Correction 2 (from v2) — the F-1 design doc puts the snapshot hook in the wrong file

`docs/statemanager_modes_design.md` § "Wiring into `StateManagerWorker`"
shows `OnSnapshotAsync` being called from
`StateManagerWorker.SnapshotProcessing.cs`. **That file processes
external API queries on port 8088, not bot snapshots.** Bot snapshots
arrive in
[Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs](Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs).
The wiring landed there in commit `6eb85dcc` — see the
`InvokeModeHandler` private method.

### Correction 3 (from v4) — AutomatedModeHandler is loadout-only, NOT loadout-then-activity

The design doc § "AutomatedModeHandler" says step 2 is "after
LoadoutStatus == Complete, dispatch the corresponding StartXxx action".
**That's already wired on the bot side and does not belong in
StateManager.** `StateManagerWorker.BotManagement.cs:131,297` forwards
`CharacterSettings.AssignedActivity` to the bot via
`WWOW_ASSIGNED_ACTIVITY`. The bot's
`BotRunnerService.InitializeTaskSequence` (~line 1119) calls
`ActivityResolver.Resolve(...)` and pushes the resolved task on top of
`IdleTask` at world entry. So the bot starts the activity itself,
regardless of mode. `AutomatedModeHandler.OnSnapshotAsync` is
intentionally a no-op.

---

## Hard rules (DO NOT VIOLATE)

- **R1 — No blind sequences/counters/timing hacks.** Gate on snapshot signal.
- **R2 — Poll snapshots, don't sleep.** Use `WaitForSnapshotConditionAsync(...)`.
- **R3 — Fail fast.** Per-milestone tight timeouts. `onClientCrashCheck` everywhere.
- **R4 — No silent exception swallowing.** Log warnings with context.
- **R5 — Fixture owns lifecycle, StateManager owns coordination, BotRunner owns execution.** This relaxes slightly with F-1: the new `LiveFixture` owns LESS than today's, because BotRunner+StateManager own loadout. Don't move coordination into the fixture.
- **R6 — GM/admin commands must be asserted.** Use `AssertGMCommandSucceededAsync` etc.
- **R7 — Cross-test state must be reset.** `EnsureCleanSlateAsync` at test start. F-1 may replace this with a snapshot-ready signal.
- **R8 — x86 vs x64.** ForegroundBotRunner = x86. BackgroundBotRunner + StateManager + most tests = x64.
- **No background agents.** `run_in_background: true` is forbidden.
- **Single session only.** Auto-compaction handles context. Don't start a new session — keep going through compactions until you hand off via the recursive handoff prompt below.
- **Commit and push frequently.** Every logical unit of work.
- **Shodan is GM-liaison + setup-only.** Never dispatch behavior actions to Shodan. Don't weaken the `ResolveBotRunnerActionTargets` guard.
- **No `.gobject add`, no synthetic node spawns.** Test against natural world state.
- **Live MaNGOS via Docker is always running** — `docker ps` to confirm; never `tasklist`.
- **No MySQL mutations.** SOAP / bot chat for all game-state changes.
- **No `.learn all_myclass` / `.learn all_myspells`.** Always teach by explicit numeric spell ID.

### Bash CWD note

`bash` calls in this harness do *not* persist `cd`. Use absolute paths or
chain: `cd "e:/repos/Westworld of Warcraft" && git ...`.

---

## Working agreements with the user (carry over)

- The user prefers concise responses. State decisions and changes; don't narrate deliberation.
- The user expects you to keep iterating without asking permission for each phase. Only ask if you genuinely need a decision they haven't already given.
- For **risky** actions (force push, deleting branches, mass file deletion, schema migrations), always confirm. Code edits and small commits are local/reversible — proceed.
- Integration tests run against the always-live Docker MaNGOS stack. Confirm `docker ps` once at session start; don't keep re-checking.

---

## Starter checklist for the receiving agent

1. Run the verification block in § "What landed in v4's session" above.
2. Read in order: this handoff (top to bottom) → relevant sections of
   `docs/handoff_test_consolidation_v4.md` (only what's referenced) →
   `docs/statemanager_modes_design.md` (apply the three corrections
   above) → `CLAUDE.md` → `Tests/CLAUDE.md`.
3. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md` — load-bearing critical rules live there.
4. **Decide your path on the Phase E blocker first.** Pick option 1, 2,
   or 3 from the "Recommended approach" subsection. If you go with
   option 3 (pause Phase E), pivot to Phase B, D, or F-2.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v6.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v5 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
