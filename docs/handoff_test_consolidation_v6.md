# Handoff v6: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`).
> **Picks up from:** `docs/handoff_test_consolidation_v5.md` (last commit
> listed there: `8d7dac25`).
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/test_cleanup_audit.md` — Phase A audit; the v5 status table
>   supersedes it where they conflict.
> - `docs/statemanager_modes_design.md` — Phase F design doc; locks the
>   schema for Automated/OnDemand modes. Apply the three corrections from
>   v4/v5 (still all valid).
> - `docs/handoff_test_consolidation_v5.md` — the previous handoff. **Don't
>   reread the whole thing — this v6 supersedes it where they conflict.**

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

Phase F-1 is COMPLETE. Phase E is BLOCKED on a BG-bot first-login bug
that v5 documented as "BG bot stuck in CharacterSelect after transition".
**This v6 narrows the diagnosis significantly** — see "Phase E blocker:
new diagnosis" below.

---

## What landed in this session (since `8d7dac25`)

This session was almost entirely investigation. **No code changes.** One
documentation commit:

- This v6 handoff itself, narrowing the Phase E blocker.

The `git log --oneline 8d7dac25..origin/main` should show this v6 doc
commit and nothing else. If it shows code commits you didn't write, the
user did manual work — read those commits before continuing.

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline 8d7dac25..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

The Docker stack must show `mangosd`, `realmd`, `pathfinding-service`
healthy. Confirmed healthy at the start of this session.

---

## Phase E blocker: NEW diagnosis (the WorldSession retry loop)

This is the most important update from this session. v5 said "BG bot is
stuck in CharacterSelect after transition" but didn't pin down the
mechanism. The mechanism is now clear (though the root cause is still
one layer deeper).

### What the logs say

`Bot/Release/net8.0/logs/botrunner_EQUIPBG1.diag.log` — the bot's
per-tick view.

`Bot/Release/net8.0/WWoWLogs/bg_EQUIPBG120260426.log` — the bot's
WoWSharpClient warnings.

`Bot/Release/net8.0/WWoWLogs/bg_EQUIPBG120260425.log` — the previous
day's WoWSharpClient warnings, includes Test-mode runs.

**The smoking gun (compare these two files side by side):**

| | Test mode (04-25 log) | Automated mode (04-26 log) |
|---|---|---|
| `[WorldEntryTransition] Recovered stale transfer gate` | Yes (×2) | Yes (×many) |
| `[WorldSession] Enter world timed out for guid 0xD5. Retrying CMSG_PLAYER_LOGIN.` | **NEVER** | **Every 10s** |
| Bot reaches `screen=InWorld` in snapshot? | Yes | No — flaps to CharacterSelect |

The retry timer is `WoWSharpObjectManager.SchedulePendingWorldEntryRetry`
(at [Exports/WoWSharpClient/WoWSharpObjectManager.cs:826-849](Exports/WoWSharpClient/WoWSharpObjectManager.cs#L826-L849)).
It is scheduled from `EnterWorld()` and reschedules itself recursively
every 10s until `_pendingWorldEntryGuid` is cleared.

`_pendingWorldEntryGuid` is cleared by `ClearPendingWorldEntry()`,
called only from:
- `EnterWorld()` itself when there's no world client (line 777)
- `ResetWorldSessionState()` (line 787)
- The retry continuation when world client is null (line 840)
- `EventEmitter_OnLoginVerifyWorld` ([Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs:917](Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs#L917))

`OnLoginVerifyWorld` fires on both `SMSG_LOGIN_VERIFY_WORLD` and
`SMSG_NEW_WORLD` (see [Exports/WoWSharpClient/Handlers/LoginHandler.cs:98](Exports/WoWSharpClient/Handlers/LoginHandler.cs#L98)).

### The likely flow that breaks Automated mode

1. Bot launches, auths, selects character, calls
   `EnterWorld(guid)` → `HasEnteredWorld=true`, `_pendingWorldEntryGuid=guid`,
   timer scheduled to fire in 10s.
2. Server sends `SMSG_LOGIN_VERIFY_WORLD` (or `SMSG_NEW_WORLD`) →
   `ClearPendingWorldEntry()` → timer attemptId++. The next continuation
   sees attemptId mismatch and returns early. Good.
3. Bot snapshot reports `IsObjectManagerValid=true` for the first time.
4. `AutomatedModeHandler.OnWorldEntryAsync` fires → enqueues
   `ApplyLoadout`. Same loop iteration's response carries `ApplyLoadout`
   back to the bot.
5. Bot pushes `LoadoutTask` onto its task stack.
6. **Something happens around 20-30 seconds later** — there's a second
   `EnterWorld()` call (or the server sends `SMSG_TRANSFER_PENDING` again),
   re-arming the timer. From this point the timer fires every 10s.
7. The CMSG_PLAYER_LOGIN retry is what the server treats as a
   client-initiated session reset. The server probably responds with
   a logout/disconnect, which triggers
   `BackgroundBotWorker.LogoutComplete.Subscribe` → `ResetWorldSessionState`
   → `HasEnteredWorld=false` → snapshot reports `screen=CharacterSelect`,
   `pos=(0,0,0)`, `playerWorldReady=false`.
8. `LoadoutTask` is pinned to the stack but cannot run because
   `playerWorldReady=false`. The fixture times out waiting for
   `IsHydratedInWorldSnapshot`.

The same retry loop is harmless in Test mode because the test director
keeps the bot busy with chat commands; the retry never gets to run while
the bot is also processing `.gm off`/`.go xyz`/`.learn`/etc., **OR** the
server treats the bot's chat traffic as session activity that prevents
the duplicate-login disconnect.

I did not finish proving step 6 (the second `EnterWorld()` source) or
step 7 (the server-side disconnect). The 04-26 bg log shows the timer
firing on a 10s cadence but there's no `LogoutComplete` log line in
this file — only retries. The next agent should add the diagnostic
described below to pin the rest down.

### Recommended next investigation steps

These are concrete enough to drop into the next session:

1. **Add a one-line log to `EnterWorld()` and `ClearPendingWorldEntry()`**
   in `Exports/WoWSharpClient/WoWSharpObjectManager.cs`:
   - Log `[WorldSession] EnterWorld(guid={Guid:X}) called from {StackTrace}`
     with a 5-frame stack trace.
   - Log `[WorldSession] ClearPendingWorldEntry called from {StackTrace}`.
   This will reveal who is re-arming the timer in Automated mode.

2. **Add `[WorldSession] LogoutComplete subscription fired`** to
   `Services/BackgroundBotRunner/BackgroundBotWorker.cs:405-425`.
   Confirms whether the regression to CharacterSelect is via
   LogoutComplete (current hypothesis) or something else.

3. **Run the Phase E pilot test** (e.g. `EquipmentEquipTests` migrated
   to Automated mode — the pre-existing migration pattern is in v5's
   notes; the test code itself was reverted, so rebuild from the v4
   foundation pieces). With the diagnostics from steps 1+2 in place,
   one run should expose the retry-trigger source.

4. **Once the retry source is known**, the fix is small. Probably
   either:
   - Make `TryRecoverStaleWorldEntryTransition` also call
     `ClearPendingWorldEntry()` so a recovered transition cancels
     the retry timer.
   - Make the retry continuation also bail out if `HasEnteredWorld`
     is true and the player has been hydrated for >X ms (i.e. the bot
     thinks it's in world even though `_pendingWorldEntryGuid` wasn't
     cleared).
   - Make `EnterWorld()` a no-op if `HasEnteredWorld` is already true
     and the guid matches.

### What v5 said that's now refined

v5 listed three options. Update:

1. **Best (refined)** — fix the WorldSession retry loop in BG bot per
   the steps above. The investigation done in this v6 session pins down
   the mechanism; what's missing is the trigger and the surgical fix.
2. **Second-best (still valid)** — pre-warm a fresh BG character before
   the test relies on Automated mode. Fixture-side scaffolding.
3. **Worst (still valid)** — accept the blocker and pause Phase E. Move
   to other in-progress phases (B's high-ROI BG queue/prep-window
   predicates, D's real taxi/transport rides, or F-2's OnDemand handler).

This session did NOT finish option 1 because the investigation hit a
context budget. The next session should pick up by following the
"Recommended next investigation steps" above — those are already
scoped tight enough to fit in one focused session.

### What NOT to do (carries from v5)

- Don't keep deleting and recreating BG characters via `.character erase`.
- Don't keep tightening SOAP revive variants.
- Don't move the AutomatedModeHandler dispatch later. The bug is in
  the WoWSharpClient layer, not in the handler timing.

---

## Phase status — open items only

(unchanged from v5; this is a copy with no new work landed)

### Phase B — Snapshot poll hygiene (most easy wins extracted)

| Wall | File:Line | Notes |
|---|---|---|
| 130s | `Battlegrounds/BattlegroundEntryTests.cs:123` | Highest single-fix savings. Replace with `CHAT_MSG_BG_SYSTEM` / queue-update marker poll. **Significant — needs to identify the right snapshot signal first.** Probably worth a research spike before coding. |
| 95s | `Battlegrounds/WsgObjectiveTests.cs:142` | Replace with prep-window-end snapshot signal. |
| 1.5s × 5 | `Raids/RaidCoordinationTests.cs:160,186,193,201,207`; `RaidFormationTests.cs:61,70,88,102,116` | Replace with `WaitForRaidMembershipAsync`-style predicates. Activity-owned, handle carefully. |
| 900ms × N | `CoordinatorFixtureBase.cs:817` (post-`SendGroupInvite`) | No `HasPendingGroupInvite` field on the snapshot proto today. Adding one would unlock this on a per-raid-member basis (~36s for a 40-bot raid setup). Touches `WoWActivitySnapshot` proto + FG/BG snapshot pipeline. |

### Phase C — Concurrent FG/BG (PENDING)

Unchanged from v5:
- **Mining take-turns.** Both bots launch `StartGatheringRoute` against the *same* node concurrently.
- **Herbalism follow.** FG follows BG between nodes.
- **Generalize "FG follows BG"** as a BotRunner-side helper.

### Phase D — Real taxi/transport rides (PENDING)

Unchanged from v5:
- **`TaxiTests`**: full ride (e.g. Org → Crossroads).
- **`TransportTests`**: split into `Boat_Or_Zeppelin_FullRide` and `Elevator_FullRide`.

### Phase E — LiveFixture consolidation (BLOCKED — see new diagnosis above)

Foundation pieces from v4 still intact:
- `Equipment.Automated.config.json` (draft, ready to use)
- `AssertConfiguredCharactersMatchAsync` is now wrapper-aware
- `CanReleaseSpirit` no longer false-positives on uninitialized BG snapshots

The pilot migration code itself is still reverted. Don't recreate it
until the WorldSession retry loop is fixed (or you've decided on
option 2/3).

### Phase F-1 — StateManager Automated mode

**COMPLETE.** All three steps shipped in v4. Don't touch.

### Phase F-2 / F-3 — OnDemand + production polish (PENDING)

Unchanged from v5 description.

### Phase G — Final cleanup (PENDING)

After everything above.

---

## Three course corrections recorded so far (don't re-discover them)

These all carry over from v4/v5 — none have changed.

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

1. Run the verification block in § "What landed in this session" above.
2. Read in order: this handoff (top to bottom) → `docs/handoff_test_consolidation_v5.md` "Phase E blocker" section
   for the symptom history → `docs/statemanager_modes_design.md` (apply the three corrections
   above) → `CLAUDE.md` → `Tests/CLAUDE.md`.
3. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md` — load-bearing critical rules live there.
4. **Decide your path on the Phase E blocker first.** Pick option 1 (now well-scoped per the new diagnosis), 2, or 3. If you go with option 3, pivot to Phase B, D, or F-2.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v7.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v6 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
