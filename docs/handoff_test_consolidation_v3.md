# Handoff v3: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`, clean tree).
> **Picks up from:** `docs/handoff_test_consolidation_v2.md` (last commit listed there: `5b5f4f2f`).
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/test_cleanup_audit.md` — Phase A audit; **read for context but the v3 status table below supersedes it where they conflict.**
> - `docs/statemanager_modes_design.md` — Phase F design doc; locks the schema for Automated/OnDemand modes. **Read the v2 handoff for two corrections to this doc** (or read v2 directly).
> - `docs/handoff_test_consolidation_v2.md` — the previous handoff. **Don't reread the whole thing — this v3 supersedes it where they conflict.**

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

---

## What landed in v2's session (since `8ec218ff`)

See `docs/handoff_test_consolidation_v2.md` § "What landed in this session"
for the full ledger up through `5b5f4f2f`. Headlines:

- Phase A audit landed (`3566625c`).
- Phase B snapshot-poll cleanup: ~10 commits replacing bare delays in
  `QuestObjective`, `Channel`, `Gossip`, `Mount`, `AuctionHouse`,
  `NpcInteraction`, `DualClientParity`, `GuildOperation`, `SummoningStone`,
  and `CoordinatorFixtureBase` (`ReviveAndLevelBotsAsync`,
  `ResetBattlegroundStateAsync`, `EnsureAccountsNotGroupedAsync`).
- F-1 step 1 (`1c530f36`): `StateManagerMode` enum + backward-compatible
  config loader. No behavior change.
- F-1 step 2 (`78bbbb36`): `IStateManagerModeHandler` + no-op
  `TestModeHandler` + DI registration + startup log. Zero behavior
  change. Live call-site wiring deferred to step 3.

**Net wall-clock saving so far (estimated): ~40–55s per full live-suite
run, plus ~7–13s per BG-coordinator test setup.**

Confirm at session start (paste verbatim):

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline 5b5f4f2f..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

If `git log` shows commits between `5b5f4f2f` and `origin/main`, the user
did manual work — read those commits before continuing. The Docker stack
must show `mangosd`, `realmd`, `pathfinding-service` healthy.

---

## Two course corrections recorded by v2 (don't re-discover them)

### Correction 1 — the `BattlegroundEntryTests` "3s × 7" batch is a false positive

The audit and v1 handoff prescribed batching the seven `3s` delays in
`Battlegrounds/BattlegroundEntryTests.cs` (lines 267, 629, 749, 826, 871,
957, 1018, 1137 — note: also includes 5s and 10s sites, header was wrong).
**Skip this entirely.** Every site is the poll-pacing tail of an outer
`while` loop that already checks the success condition (`count == expected`,
`onBg >= expectedOnMap`, etc.). On 80-bot BG fixtures, tightening to 500ms
would multiply `QueryAllSnapshotsAsync` load without saving wall time.
These are not blind sleeps; they're correct poll intervals.

### Correction 2 — the F-1 design doc puts the snapshot hook in the wrong file

`docs/statemanager_modes_design.md` § "Wiring into `StateManagerWorker`"
shows `OnSnapshotAsync` being called from
`StateManagerWorker.SnapshotProcessing.cs`. **That file processes external
API queries on port 8088, not bot snapshots.** Bot snapshots arrive at
[Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs](Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs)
in `HandleRequest` around line 173 (right after
`CurrentActivityMemberList[accountName] = request;`). That's where the
mode handler's `OnSnapshotAsync` belongs, and where the per-account
"first world-ready" tracker for `OnWorldEntryAsync` slots in.

The F-1 step 2 commit (`78bbbb36`) deliberately deferred call-site wiring
so it lands cleanly in step 3 alongside the real `AutomatedModeHandler`.

---

## Phase status — open items only

### Phase B — Snapshot poll hygiene (most easy wins extracted)

The remaining bare-delay census in `docs/test_cleanup_audit.md` § A1 is
mostly low-ROI now. The high-ROI items left:

| Wall | File:Line | Notes |
|---|---|---|
| 130s | `Battlegrounds/BattlegroundEntryTests.cs:123` | Highest single-fix savings. Replace with `CHAT_MSG_BG_SYSTEM` / queue-update marker poll. **Significant — needs to identify the right snapshot signal first.** Probably worth a research spike before coding. |
| 95s | `Battlegrounds/WsgObjectiveTests.cs:142` | Replace with prep-window-end snapshot signal. |
| 1.5s × 5 | `Raids/RaidCoordinationTests.cs:160,186,193,201,207`; `RaidFormationTests.cs:61,70,88,102,116` | Replace with `WaitForRaidMembershipAsync`-style predicates. Activity-owned, handle carefully. |
| 900ms × N | `CoordinatorFixtureBase.cs:817` (post-`SendGroupInvite`) | No `HasPendingGroupInvite` field on the snapshot proto today. Adding one would unlock this on a per-raid-member basis (~36s for a 40-bot raid setup). Touches `WoWActivitySnapshot` proto + FG/BG snapshot pipeline. |

The remaining 200–1500ms intra-poll delays in
`EquipmentEquipTests`/`UnequipItemTests`/`MailParity`/etc. should migrate
to `WaitForSnapshotConditionAsync` for **consistency**, not speed.

### Phase C — Concurrent FG/BG (PENDING)

Unchanged from v2:
- **Mining take-turns.** Both bots launch `StartGatheringRoute` against the *same* node concurrently. Coordinate via a "claim/release" signal at the node level. Update `GatheringRouteSelection` to support two-bot convergence + graceful yield.
- **Herbalism follow.** FG gathers first node; on completion, FG enters a follow-mode (BotRunner-level `Follow` action against BG GUID). BG moves to a different node. Assert both bag entries land + FG ends within follow-distance of BG.
- **Generalize "FG follows BG"** as a BotRunner-side helper.

**Where to start:** `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`, `Exports/BotRunner/Tasks/GatheringTask.cs`, `GatheringRouteSelection.cs`. Claim/release signal needs a place to live — likely on `WoWActivitySnapshot` or a new `GatheringNodeClaim` message.

### Phase D — Real taxi/transport rides (PENDING)

Unchanged from v2:
- **`TaxiTests`**: full ride (e.g. Org → Crossroads). Assert bot lands on destination flightmaster's grid square within `ExpectedRideSeconds + slack`.
- **`TransportTests`**: split into `Boat_Or_Zeppelin_FullRide` (board on arrival, assert `MOVEFLAG_ONTRANSPORT`, assert at destination) and `Elevator_FullRide` (Undercity West / Thunder Bluff). All snapshot-driven, no sleeps.

**Where to start:** `TransportTests.cs` and `TaxiTests.cs` already have Shodan staging in place; the action dispatch is the gap. Look for `MOVEFLAG_ONTRANSPORT` in the snapshot proto. The transport-arrival timer table likely lives in BotRunner gameobject data — grep `transport.*schedule` or look at `FgTaxiFrame.cs`.

### Phase F-1 — StateManager Automated mode (in progress)

1. ~~F-1 step 1: schema enum + backward-compatible loader.~~ **DONE** (`1c530f36`).
2. ~~F-1 step 2: `IStateManagerModeHandler` + `TestModeHandler` (no-op).~~ **DONE** (`78bbbb36`).
3. **F-1 step 3 (NEXT) — this is your starting task.** Three deliverables in one commit each:
   - **3a.** Implement `AutomatedModeHandler`. `OnWorldEntryAsync`: enqueue `APPLY_LOADOUT(character.Loadout)` once, then on `LoadoutStatus == Complete` parse `character.AssignedActivity` and dispatch the corresponding `StartXxx` action. `OnSnapshotAsync`: drive the activity loop, idle on completion. `OnExternalActivityRequestAsync`: ignore (OnDemand's job). Update the DI fall-through in [Services/WoWStateManager/Program.cs](Services/WoWStateManager/Program.cs) to register the real handler for `Mode=Automated` instead of falling back to `TestModeHandler`.
   - **3b.** Wire the call sites in [Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs](Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs) `HandleRequest` (around line 173). Add a per-account "first world-ready" tracker (e.g. `ConcurrentDictionary<string, bool> _worldEntryDispatched`) so `OnWorldEntryAsync` fires exactly once per account when `IsObjectManagerValid` first goes true. Then on every snapshot tick (after the existing `CurrentActivityMemberList[accountName] = request;` line), call `_modeHandler.OnSnapshotAsync(...)`. The handler is async; the listener path is sync — use `.GetAwaiter().GetResult()` since `TestModeHandler` is no-op and `AutomatedModeHandler` should never block long.
   - **3c.** Add `Services/WoWStateManager/Settings/Configs/Onboarding.config.json` using the new wrapped schema (`{ "Mode": "Automated", "Characters": [...] }`). Use a single character with a real `Loadout` and `AssignedActivity = "Fishing[Ratchet]"` (or similar — pick whatever activity is best-tested). Add ONE live test that loads this config, waits for `LoadoutStatus == Complete` via `WaitForSnapshotConditionAsync`, then asserts the activity started. Verify on the live Docker stack.

   **Before you start step 3:** `LoadoutTask` and `ActivityResolver` may have evolved since v2 was written. Re-grep for them and read the current implementation. Also verify that `LoadoutStatus` is actually on `WoWActivitySnapshot` — design doc claims it is (per `CharacterSettings.cs:142-149` doc comment) but v2 didn't confirm.

### Phase E — LiveFixture consolidation (PENDING — gated on F-1 step 3)

Once F-1 step 3 is green, start `LiveFixture` migration. See v2 § "Phase E"
for details. Pilot: `EquipmentEquipTests`. Target: `LiveFixture.cs` ~1 200
lines vs the current ~9 000 in partials.

### Phase F-2/F-3 — OnDemand + production polish (PENDING)

After F-1 + E. See v2 § "Phase F-2/F-3".

### Phase G — Final cleanup (PENDING)

After everything above. See v2 § "Phase G".

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

1. Run the verification block in § "What landed in v2's session" above.
2. Read in order: this handoff (top to bottom) → relevant sections of
   `docs/handoff_test_consolidation_v2.md` (only what's referenced) →
   `docs/statemanager_modes_design.md` (apply the two corrections above) →
   `CLAUDE.md` → `Tests/CLAUDE.md`.
3. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md` — load-bearing critical rules live there.
4. Start F-1 step 3a (`AutomatedModeHandler`). Re-verify `LoadoutTask` /
   `ActivityResolver` / `LoadoutStatus` against the *current* codebase
   before relying on the design doc's claims.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v4.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v3 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
