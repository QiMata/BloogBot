# Handoff v7: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`).
> **Picks up from:** `docs/handoff_test_consolidation_v6.md` (last commit
> listed there: `925083d0`). v7 supersedes v6 on Phase E.
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/statemanager_modes_design.md` — Phase F design doc; the three
>   corrections from v4/v5 still apply (re-stated below).
> - `docs/handoff_test_consolidation_v6.md` — the previous handoff. **Don't
>   reread the whole thing — v7 supersedes it where they conflict.**

---

## Why this work exists

The Shodan-shaped LiveValidation suite is architecturally clean. The
remaining problems are bloat, sequential execution, shallow taxi/transport
coverage, and StateManager's lack of a first-class `Automated` mode.
Phases B (delay cleanup), C (concurrent FG/BG), D (real taxi/transport
rides), F (modes), and E (LiveFixture consolidation, gated on F-1 step 3)
tackle these in order.

**Phase F-1 is COMPLETE.** **Phase E pilot blocker is FIXED in v7.** Phases
B, C, D, F-2/F-3, G are still open.

---

## What landed since v6 (`925083d0`)

Two commits, both surgical fixes that close out the Phase E blocker that
v6 narrowed:

1. **`cc262aa6`** — `fix(worldsession): suppress redundant CMSG_PLAYER_LOGIN
   once player is hydrated`. Inside `SchedulePendingWorldEntryRetry`'s
   continuation, bail out when `Player.MapId != 0` (server already accepted
   the session). Defensive against the v6 retry-loop diagnosis.

2. **`fc00b5d0`** — `fix(worldsession): make EnterWorld idempotent for the
   same in-world session`. The actual root cause of the BG-bot Automated-
   mode flap. Inside `WoWSharpObjectManager.EnterWorld`, no-op when
   `HasEnteredWorld == true && _playerGuid.FullGuid == characterGuid &&
   prevMapId != 0`. Also adds a 6-frame stack-trace helper
   (`GetShortStackTrace`) so the residual gating bug can be located without
   re-instrumenting.

`OnboardingAutomatedModeTests.Onboarding_AutomatedMode_DispatchesApplyLoadoutAtWorldEntry`
now passes 3/3 in 36s each (was: passing flakily in 1m43s with the bot
flapping in the background).

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline 925083d0..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

The Docker stack must show `mangosd`, `realmd`, `pathfinding-service`
healthy. Confirmed healthy at session start.

---

## Phase E: ROOT CAUSE FOUND, fix applied, residual gating bug open

v6 identified the `SchedulePendingWorldEntryRetry` 10s loop as a symptom.
v7 stack-traced it to the actual cause: the bot's behavior tree was
calling `BuildEnterWorldSequence`'s `EnterWorld(...)` action **while the
bot was already in world**, after a teleport. That second `EnterWorld`
call recreated `Player` (via the `PlayerGuid` setter), which dropped
`MapId`/`Position` to 0, snapped `screen` back to `CharacterSelect`, and
re-armed the retry timer.

Stack-trace diagnostic (still in the code at
[Exports/WoWSharpClient/WoWSharpObjectManager.cs:776-792](Exports/WoWSharpClient/WoWSharpObjectManager.cs#L776-L792))
in the bg log on a passing run shows:

```
[00:35:16.872] Post-teleport ground snap complete
[00:35:16.955] Suppressing redundant EnterWorld(guid=0xD0) — already in
               world (mapId=1). Caller chain:
               <BuildEnterWorldSequence>b__1 <- ActionNode.Tick <-
               SequenceNode.Tick <- <StartBotTaskRunnerAsync>d__79.MoveNext <- ...
```

The post-teleport ground snap fires, then 83 ms later the behavior tree
ticks `EnterWorldSequence` again and is correctly suppressed.

### Why does the behavior tree fire `EnterWorldSequence` when in world?

`BotRunnerService.UpdateBehaviorTree` (lines 833-960 of
[Exports/BotRunner/BotRunnerService.cs](Exports/BotRunner/BotRunnerService.cs))
gates the login flow on `!HasEnteredWorld` and clears `_behaviorTree` to
null when `HasEnteredWorld` is true (line 858). But empirically the gate
fails after a teleport (likely on the first tick after the post-teleport
ground snap, when something transient flips `HasEnteredWorld` false in
the snapshot view but no `ResetWorldSessionState` log fires).

I did **not** locate the gating bug in v7 — the idempotency guard inside
`EnterWorld` itself is sufficient as a defensive measure. If you want to
fix the gate too:

1. Add `Log.Warning("[BotRunner] login flow re-entered while in world (HasEnteredWorld={H}, IsReady={R})", ...)`
   right before line 877 in `BotRunnerService.cs` where the LoginScreen
   sequence is built.
2. Re-run `OnboardingAutomatedModeTests`. The new log will fire on the
   tick that bypassed the gate. Cross-reference the snapshot's
   `_objectManager.HasEnteredWorld` and the WoWClient's session state at
   that moment.
3. The fix is probably one of: tighten the gate to require both
   `HasEnteredWorld` AND `Player.Guid != 0`; or move the `_behaviorTree =
   null` after the party-tree check to the top of the HasEnteredWorld
   block so the OLD tree from the login flow is cleared even when
   hydration isn't ready yet.

### What v6 said that's now superseded

v6 listed three options for fixing the retry loop. Update:

1. **Option 1 — surgical fix in retry continuation.** Implemented as
   `cc262aa6`. Defensive only; the underlying redundant-EnterWorld is
   what should not happen. **Kept** as a belt-and-suspenders measure.
2. **Option 2 — pre-warm BG character.** No longer needed.
3. **Option 3 — pause Phase E.** No longer needed.

The actual fix is `fc00b5d0` (idempotent EnterWorld). The v6 hypotheses
about WorldSession retry being the *cause* were wrong — retry was a
*symptom* of the redundant `EnterWorld`.

---

## Phase status — open items only

### Phase B — Snapshot poll hygiene (most easy wins extracted)

(unchanged from v6)

| Wall | File:Line | Notes |
|---|---|---|
| 130s | `Battlegrounds/BattlegroundEntryTests.cs:123` | Highest single-fix savings. Replace with `CHAT_MSG_BG_SYSTEM` / queue-update marker poll. **Significant — needs to identify the right snapshot signal first.** Probably worth a research spike before coding. |
| 95s | `Battlegrounds/WsgObjectiveTests.cs:142` | Replace with prep-window-end snapshot signal. |
| 1.5s × 5 | `Raids/RaidCoordinationTests.cs:160,186,193,201,207`; `RaidFormationTests.cs:61,70,88,102,116` | Replace with `WaitForRaidMembershipAsync`-style predicates. Activity-owned, handle carefully. |
| 900ms × N | `CoordinatorFixtureBase.cs:817` (post-`SendGroupInvite`) | No `HasPendingGroupInvite` field on the snapshot proto today. Adding one would unlock this on a per-raid-member basis (~36s for a 40-bot raid setup). Touches `WoWActivitySnapshot` proto + FG/BG snapshot pipeline. |

### Phase C — Concurrent FG/BG (PENDING)

(unchanged from v6)
- **Mining take-turns.** Both bots launch `StartGatheringRoute` against the *same* node concurrently.
- **Herbalism follow.** FG follows BG between nodes.
- **Generalize "FG follows BG"** as a BotRunner-side helper.

### Phase D — Real taxi/transport rides (PENDING)

(unchanged from v6)
- **`TaxiTests`**: full ride (e.g. Org → Crossroads).
- **`TransportTests`**: split into `Boat_Or_Zeppelin_FullRide` and `Elevator_FullRide`.

### Phase E — LiveFixture consolidation (UNBLOCKED — pilot stable)

Foundation pieces from v4 still intact. Pilot
`OnboardingAutomatedModeTests` now passes consistently. The remaining
work in Phase E is the broader migration: rebuild the
`Equipment.Automated.config.json`-driven `EquipmentEquipTests` migration
that v4 wrote and v5 reverted, then chip away at the rest of the suite
per the per-category siblings plan. The BG-flap blocker is gone.

### Phase F-1 — StateManager Automated mode

**COMPLETE.** Don't touch.

### Phase F-2 / F-3 — OnDemand + production polish (PENDING)

(unchanged from v6 description)

### Phase G — Final cleanup (PENDING)

After everything above.

### Residual: BotRunnerService.UpdateBehaviorTree login-flow gate

Open follow-up — see "Why does the behavior tree fire EnterWorldSequence
when in world?" above. Not blocking; defensive guard at the EnterWorld
layer makes this safe.

---

## Three course corrections recorded so far (don't re-discover them)

(unchanged from v6 — all still valid)

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
The wiring landed there in commit `6eb85dcc`.

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
- **R5 — Fixture owns lifecycle, StateManager owns coordination, BotRunner owns execution.** F-1 relaxes this slightly (LiveFixture owns less because BotRunner+StateManager own loadout). Don't move coordination into the fixture.
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

1. Run the verification block in § "What landed since v6" above.
2. Read in order: this handoff (top to bottom) → optionally
   `docs/handoff_test_consolidation_v6.md` "Phase E blocker" section
   for the symptom history → `docs/statemanager_modes_design.md` (apply
   the three corrections above) → `CLAUDE.md` → `Tests/CLAUDE.md`.
3. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md` — load-bearing critical rules live there.
4. Pick a phase. **Recommended order:** Phase D (taxi/transport — clear scope and high value) → Phase B 130s easy win (BattlegroundEntryTests:123) → Phase E broader migration → Phase C concurrent FG/BG → Phase F-2 OnDemand.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v8.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v7 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
