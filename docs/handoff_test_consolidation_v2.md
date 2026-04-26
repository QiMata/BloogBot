# Handoff v2: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo).
> **Picks up from:** `docs/handoff_test_consolidation.md` (committed at `8ec218ff`).
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/test_cleanup_audit.md` — Phase A audit, the source-of-truth list of bloat to prune.
> - `docs/statemanager_modes_design.md` — Phase F design doc; locks the schema for Automated/OnDemand modes.
> - `docs/handoff_test_consolidation.md` — the previous handoff. **Don't reread the whole thing — this v2 supersedes it where they conflict.**

---

## Why this work exists (unchanged from v1)

The Shodan-shaped LiveValidation suite is architecturally clean: Shodan is
the production GM liaison, behavior tests dispatch `ActionType.*` against
dedicated test accounts (TESTBOT1/2 + per-category siblings), and the
fixture layer enforces the separation. The next problem is **bloat,
sequential execution, shallow taxi/transport coverage, and StateManager's
lack of a first-class `Automated` mode** — see v1 for the full framing.

---

## What landed in this session (since the v1 handoff at `8ec218ff`)

| Commit | What |
|---|---|
| `3566625c` | docs(test-cleanup): Phase A audit of LiveValidation bloat — `docs/test_cleanup_audit.md` |
| `52e98fee` | test(live): replace 12s sleep with mob-killed predicate in `QuestObjectiveTests` |
| `acba1cdc` | test(live): replace post-action sleeps with snapshot predicates (`ChannelTests`, `GossipQuestTests` ×2, `MountEnvironmentTests`) |
| `ea69acad` | test(live): replace post-action sleeps in `AuctionHouseTests`, `NpcInteractionTests` (vendor + flightmaster) |
| `678b0cc7` | test(live): drop redundant 2s tail after Quiesce in `DualClientParityTests` |
| `b6348d4e` | test(live): replace 6 hard delays in `GuildOperationTests` with predicates (-9.5s) |
| `046ecc78` | docs(statemanager): F-1 mode dispatch design — `docs/statemanager_modes_design.md` |
| `854ecdce` | docs: handoff v2 for live-test consolidation work (this file) |
| `43fe3e05` | test(live): replace 3 hard delays in `SummoningStoneTests` with predicates (-8s) |
| `1c530f36` | **feat(statemanager): F-1 step 1 — mode enum + backward-compat loader.** No behavior change; foundation for F-1 step 2. |

**Net wall-clock saving so far (estimated): ~33–43s per full live-suite run.**

**F-1 step 1 is DONE.** The next session starts at F-1 step 2:
`IStateManagerModeHandler` interface + `TestModeHandler` (no-op
wrapper) + `StateManagerWorker` wiring. See
`docs/statemanager_modes_design.md` § "Mode handler interface" and
"Wiring into `StateManagerWorker`".

Confirm at session start:

```bash
git fetch origin && git log --oneline 8ec218ff..origin/main
```

---

## Major design pivot discovered in F-1 exploration

**The Loadout / AssignedActivity hand-off is already wired in the
codebase.** `CharacterSettings.Loadout` (`LoadoutSpecSettings`) and
`CharacterSettings.AssignedActivity` (string descriptor like
`"Fishing[Ratchet]"`) are real fields that BotRunner's `LoadoutTask` and
`ActivityResolver` already consume. `BattlegroundCoordinator` already
does Automated-style orchestration.

**Implication:** F-1 is mostly a flag-and-dispatch exercise, not a
rewrite. The schema change wraps the bare-array config in
`{ "Mode": ..., "Characters": [...] }` with a backward-compatible
loader so every existing config keeps working. See
`docs/statemanager_modes_design.md` for the full plan and an
implementation order.

This means **Phase E (LiveFixture consolidation) is closer than v1
implied** — once Automated mode is wired, the 2 299-line
`LiveBotFixture.TestDirector.cs` collapses to a thin adapter because
StateManager handles loadout/activity automatically.

---

## Phase status

### Phase A — Audit (DONE — `3566625c`)

`docs/test_cleanup_audit.md` is the source of truth for what to prune.
**Go read it first** — every item below references it.

### Phase B — Snapshot poll hygiene + GM redundancy purge (in progress)

#### Done so far
- B1 (no-op confirmed): no per-test `.gm off`/`.gm on` in test bodies.
  All 5 hits live in fixture/setup code.
- 6 hard delays replaced in `QuestObjective`, `Channel`, `Gossip` ×2,
  `Mount`, `AuctionHouse`, `NpcInteraction` ×2, `DualClientParity`,
  `GuildOperation` ×6.

#### Remaining bare-delay targets in priority order
> Source: `docs/test_cleanup_audit.md` § "A1. Bare delay census".

| Wall | File:Line | Status |
|---|---|---|
| 130s | `Battlegrounds/BattlegroundEntryTests.cs:123` | **Pending.** Replace with `CHAT_MSG_BG_SYSTEM` / queue-update marker poll. Significant: needs to identify the right snapshot signal first. |
| 95s | `Battlegrounds/WsgObjectiveTests.cs:142` | **Pending.** Replace with prep-window-end snapshot signal. |
| 5s × 4 | `TransportTests.cs:49,102,143,179` | **Skip — Phase D rewrites this entire test class.** |
| 5s × 2 | `CornerNavigationTests.cs:75,127` | **Pending — but these are valid 5s poll intervals inside a 60s arrival loop. Lower ROI. Migrate to `WaitForSnapshotConditionAsync` for consistency, not speed.** |
| 5s × 2 | `TileBoundaryCrossingTests.cs:95,185` | Same as above. |
| 5s | `Dungeons/SummoningStoneTests.cs:90` | **Pending.** Replace with `WaitForSummonCompleteAsync` predicate. |
| 3s × 7 | `Battlegrounds/BattlegroundEntryTests.cs:267, 629, 957, 1018, 749, 826, 871, 1137` | **Pending.** Per-marker poll. |
| 3s × 2 | `TaxiTests.cs:60`, etc | **Skip — Phase D rewrites this.** |
| 1.5s × 5 | `Raids/RaidCoordinationTests.cs:160,186,193,201,207`; `RaidFormationTests.cs:61,70,88,102,116` | **Pending.** Replace with `WaitForRaidMembershipAsync`-style predicates. Activity-owned, so handle carefully. |
| Mid-tier (200–1500ms) | `EquipmentEquipTests`, `UnequipItemTests`, `BgInteraction`, `MailParity`, `MailSystem`, `EconomyInteraction`, `BuffAndConsumable`, `ConsumableUsage`, `SpellCastOnTarget`, `WandAttack` | **Pending. Lower ROI — these are intra-poll inside ad-hoc `while` loops. Migrate the loops to `WaitForSnapshotConditionAsync` for consistency.** |
| Fixture-side | `CoordinatorFixtureBase.cs` (23 hits) | **Pending — biggest single ROI in fixture layer. ~8–15s saving per coordinator test.** |

#### Phase B remaining tasks (concrete, do these next)
1. Pick off `Dungeons/SummoningStoneTests.cs:90` (single 5s, well-scoped).
2. Tackle `Battlegrounds/BattlegroundEntryTests.cs` 3s delays (seven of them, batched).
3. Migrate `CoordinatorFixtureBase.cs` 23 hits to `WaitForSnapshotConditionAsync` — small commits, one cluster at a time.
4. **B5:** Make `StageBotRunner*Async` helpers' internal `EnsureCleanSlateAsync` opt-in (`bool ensureClean = false`). Estimated +6–10s per dual-target test. **Defer until after F-1 lands** because F-1's snapshot-ready signal is the cleaner replacement.

### Phase C — Concurrent FG/BG (PENDING)

User wants:
- **Mining take-turns.** Both bots launch `StartGatheringRoute` against the *same* node concurrently. Coordinate via a "claim/release" signal at the node level. Update `GatheringRouteSelection` to support two-bot convergence + graceful yield.
- **Herbalism follow.** FG gathers first node; on completion, FG enters a follow-mode (BotRunner-level `Follow` action against BG GUID). BG moves to a different node. Assert both bag entries land + FG ends within follow-distance of BG.
- **Generalize "FG follows BG"** as a BotRunner-side helper. Apply to every test with "FG idle for topology parity." Update `TEST_EXECUTION_MODES.md`.

**Where to start:** read `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`, `Exports/BotRunner/Tasks/GatheringTask.cs` (search for it), and `GatheringRouteSelection.cs` for the current single-bot logic. The claim/release signal needs a place to live — likely on `WoWActivitySnapshot` or a new `GatheringNodeClaim` message.

### Phase D — Real taxi/transport rides (PENDING)

User wants:
- **`TaxiTests`**: replace `VisitFlightMaster + SelectTaxiNode` ACK-only assertions with a full ride. Pick a short Horde route (Org → Crossroads or Thunder Bluff → Bloodhoof Village). Assert bot lands on destination flightmaster's grid square within `ExpectedRideSeconds + slack`.
- **`TransportTests`** split into:
  - `Boat_Or_Zeppelin_FullRide` — query active transport schedules to pick whichever vessel docks next, board on arrival, assert in-transit (`MOVEFLAG_ONTRANSPORT`), assert at destination.
  - `Elevator_FullRide` — pick Undercity West or Thunder Bluff. Bot rides down, asserts ground-level position at bottom.
- All snapshot-driven, no sleeps, per-milestone tight timeouts.

**Where to start:** the existing `TransportTests.cs` and `TaxiTests.cs` already have Shodan staging in place; the action dispatch is the gap. Look for `MOVEFLAG_ONTRANSPORT` in the snapshot proto. The transport-arrival timer table likely lives in BotRunner gameobject data — grep `transport.*schedule` or look at `FgTaxiFrame.cs` (in `Services/ForegroundBotRunner/Frames/`).

### Phase F-1 — StateManager Automated mode (in progress)

**Read `docs/statemanager_modes_design.md` first.** Implementation order locked there:

1. ~~F-1 step 1: schema enum + backward-compatible loader.~~ **DONE** at `1c530f36`. No behavior change yet.
2. F-1 step 2 (NEXT): `IStateManagerModeHandler` + `TestModeHandler` (no-op wrapper). Wire into `StateManagerWorker`.
3. F-1 step 3: `AutomatedModeHandler`. Add `Onboarding.config.json`. Verify with one live test.

The design doc covers each step. The work is ~3 commits, mostly mechanical.

### Phase E — LiveFixture consolidation (PENDING — gated on F-1 step 3)

Once F-1 step 3 is green, start `LiveFixture` migration:
1. Pilot: `EquipmentEquipTests`. Smallest, cleanest. Confirm green, commit.
2. Migrate the rest in dependency order. Each its own commit.
3. Delete obsoleted partials, `BgOnlyBotFixture`, `SingleBotFixture`. Run full suite. Commit.

Target: `LiveFixture.cs` ~1 200 lines vs the current ~9 000 in partials. See `docs/test_cleanup_audit.md` § "A3. Fixture inventory & merge plan" for the per-file mapping.

### Phase F-2/F-3 — OnDemand + production polish (PENDING)

After F-1 + E:
- F-2: `OnDemandActivitiesModeHandler` + `POST /activities/request` endpoint on `StateManagerListener` + Shodan whisper command parser. See design doc.
- F-3: Audit fixture helpers; move production-relevant ones to `BotRunner.Tasks.LoadoutTask` / `ActivityResolver`.

### Phase G — Final cleanup (PENDING)

Last commits:
- Delete obsoleted `RfcBotFixture`, `CoordinatorFixtureBase` (or trim to just delays cleanup), partial files, etc.
- Update root `CLAUDE.md` Shodan section to also describe OnDemand mode.
- Update `SHODAN_MIGRATION_INVENTORY.md` and `TEST_EXECUTION_MODES.md`.
- Final full-suite run. Verify origin/main clean.

---

## Hard rules (DO NOT VIOLATE — repeat from v1)

- **R1 — No blind sequences/counters/timing hacks.** Gate on snapshot signal.
- **R2 — Poll snapshots, don't sleep.** Use `WaitForSnapshotConditionAsync(...)`.
- **R3 — Fail fast.** Per-milestone tight timeouts. `onClientCrashCheck` everywhere.
- **R4 — No silent exception swallowing.** Log warnings with context.
- **R5 — Fixture owns lifecycle, StateManager owns coordination, BotRunner owns execution.** This relaxes slightly with F-1: the new `LiveFixture` owns LESS than today's, because BotRunner+StateManager own loadout. Don't move coordination into the fixture.
- **R6 — GM/admin commands must be asserted.** Use `AssertGMCommandSucceededAsync` etc.
- **R7 — Cross-test state must be reset.** `EnsureCleanSlateAsync` at test start. F-1 may replace this with a snapshot-ready signal.
- **R8 — x86 vs x64.** ForegroundBotRunner = x86. BackgroundBotRunner + StateManager + most tests = x64.
- **No background agents.** `run_in_background: true` is forbidden.
- **Single session only.** Auto-compaction handles context. Don't start a new session — keep going through compactions until you hand off via the recursive handoff prompt.
- **Commit and push frequently.** Every logical unit of work.
- **Shodan is GM-liaison + setup-only.** Never dispatch behavior actions to Shodan. Don't weaken the `ResolveBotRunnerActionTargets` guard.
- **No `.gobject add`, no synthetic node spawns.** Test against natural world state.
- **Live MaNGOS via Docker is always running** — `docker ps` to confirm; never `tasklist`.

### Bash CWD note

`bash` calls in this harness do *not* persist `cd`. Use absolute paths
or chain: `cd "e:/repos/Westworld of Warcraft" && git ...`. The
previous session got bitten by a stale CWD once.

---

## Working agreements with the user (carry over)

- The user prefers concise responses. State decisions and changes; don't narrate deliberation.
- The user expects you to keep iterating without asking permission for each phase. Only ask if you genuinely need a decision they haven't already given.
- For **risky** actions (force push, deleting branches, mass file deletion, schema migrations), always confirm. Code edits and small commits are local/reversible — proceed.
- Integration tests run against the always-live Docker MaNGOS stack. Confirm `docker ps` once at session start; don't keep re-checking.

---

## Starter checklist for the receiving agent

1. `git fetch origin && git log --oneline 046ecc78..origin/main` — if anything is between the last commit listed in this handoff and origin/main, the user did manual work; read those commits first.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service` are healthy.
3. Read in order: this handoff (top to bottom) → `docs/test_cleanup_audit.md` → `docs/statemanager_modes_design.md` → `CLAUDE.md` → `Tests/CLAUDE.md`.
4. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md` — load-bearing critical rules live there.
5. Continue Phase B against the worksheet. The next commit is most likely `Dungeons/SummoningStoneTests.cs:90` (single 5s, well-scoped) or the `BattlegroundEntry` 3s delays (batched).
6. **Before doing F-1**, re-verify the design doc's assumptions with the *current* codebase — `LoadoutTask` and `ActivityResolver` may have evolved.
7. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v3.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v2 was written.
3. The remaining phases with completed work moved to a "Done" appendix.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
