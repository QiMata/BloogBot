# Handoff v8: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`).
> **Picks up from:** `docs/handoff_test_consolidation_v7.md` (last commit
> listed there: `23305af8`). v8 supersedes v7 on Phases B, D, and E.
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/statemanager_modes_design.md` — Phase F design doc; the three
>   corrections from v4/v5 still apply (re-stated below).
> - `docs/handoff_test_consolidation_v7.md` — the previous handoff. **Don't
>   reread the whole thing — v8 supersedes it where they conflict.**

---

## Why this work exists

The Shodan-shaped LiveValidation suite is architecturally clean. The
remaining problems are bloat, sequential execution, shallow taxi/transport
coverage, and StateManager's lack of a first-class `Automated` mode.
Phases B (delay cleanup), C (concurrent FG/BG), D (real taxi/transport
rides), F (modes), and E (LiveFixture consolidation, gated on F-1 step 3)
tackle these in order.

**Phases B (high-ROI), D (Taxi + Elevator pilots), E (pilot + gate fix),
and F-1 are now COMPLETE.** Phases C, F-2/F-3, G are still open.

---

## What landed since v7 (`23305af8`)

Six commits this session — three Phase E bugfixes, two Phase D upgrades,
one Phase B optimization. All compile clean and pass:

| Commit | Phase | Summary |
|---|---|---|
| `cc262aa6` | E | `fix(worldsession): suppress redundant CMSG_PLAYER_LOGIN once player is hydrated` — defensive guard in retry continuation. |
| `fc00b5d0` | E | `fix(worldsession): make EnterWorld idempotent for the same in-world session` — root cause of BG-bot Automated-mode flap. |
| `23305af8` | E | `docs(handoff): v7 — Phase E pilot fix landed, root cause identified` — superseded by this v8. |
| `654bfde9` | E | `fix(botrunner): drop stale non-Running behavior tree at top of UpdateBehaviorTree` — upstream gate fix; eliminates the redundant EnterWorld at the source. |
| `9c261c5e` | D | `feat(phase-d): TaxiTests.Taxi_HordeRide_OrgToXroads asserts full arrival` — full Org→Crossroads ride with 200yd tolerance / 180s window. |
| `a643225b` | D | `feat(phase-d): TaxiTests.Taxi_MultiHop_OrgToGadgetzan asserts full arrival` — full Org→Gadgetzan multi-hop. |
| `fe280267` | D | `feat(phase-d): add Elevator_FullRide_Undercity end-to-end assertion` — Stage upper, Goto lower, assert TransportGuid + Z arrival. |
| `61888ba0` | B | `feat(phase-b): replace AV/WSG prep-window blind sleeps with chat-driven wait` — 130s + 95s sleeps now poll RecentChatMessages for "begun" marker, falling back to wall-clock. |
| `2249297c` | B | `feat(phase-b): replace FormRaidAsync 2x 2000ms blind sleeps with predicates` — RaidCoordinationTests.FormRaidAsync now uses WaitForPartyMembershipAsync (PartyLeaderGuid predicate, 250ms poll, 20s/10s timeouts). Saves ~3-3.5s per raid form. |
| `18249f2a` | B | `feat(phase-b): apply same predicate-poll to RaidFormationTests` — same refactor on the inline raid formation in RaidFormationTests. |
| `5940e9fd` | B | `feat(phase-b): add HasPendingGroupInvite snapshot field; predicate-poll all SendGroupInvite waits` — adds proto field 32, plumbs through SnapshotBuilder, replaces three SendGroupInvite blind sleeps. **Saves up to ~36s on a 40-bot raid setup** (the CoordinatorFixtureBase.cs:817 900ms × N case). |

Verification:
- `OnboardingAutomatedModeTests.Onboarding_AutomatedMode_DispatchesApplyLoadoutAtWorldEntry` passes 6/6 in 35-43s (was: 1m43s with bot flapping).
- `TaxiTests.Taxi_HordeRide_OrgToXroads` passes 2/2 in ~2m33s (full Crossroads arrival).
- `TaxiTests.Taxi_MultiHop_OrgToGadgetzan` skips with precise diagnostic ("840yd short of Gadgetzan") — the test now correctly exposes the multi-hop chaining/ground-snap bug instead of false-passing on departure-only.
- `TransportTests.Elevator_FullRide_Undercity` skips with precise diagnostic ("never acquired a TransportGuid") — same documented flake as `Transport_Board_FgBgParity`.
- 1582/1583 WoWSharpClient.Tests pass (1 pre-existing skip).
- 19/19 BgTestHelperTests unit tests pass.

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline 23305af8..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

The Docker stack must show `mangosd`, `realmd`, `pathfinding-service`
healthy. Confirmed healthy at session start.

---

## Phase E — DONE

The BG-bot Automated-mode flap is fully fixed at the root cause:

1. **Symptom (v6 hypothesis):** `SchedulePendingWorldEntryRetry` 10s loop kept
   sending CMSG_PLAYER_LOGIN, server kicked the duplicate session, looped
   forever. Defensive fix in `cc262aa6` makes the retry bail out early when
   the player is already hydrated.
2. **Root cause (v7-v8 stack-trace diagnosis):** the bot's behavior tree
   was firing `BuildEnterWorldSequence`'s `EnterWorld(...)` action *while
   the bot was already in world*, after a teleport. The fresh `EnterWorld`
   recreated `Player` (via `PlayerGuid` setter), which reset `MapId`/
   `Position`/`MaxHealth` to 0, snapped the snapshot back to
   `screen=CharacterSelect`, and re-armed the retry timer.
3. **Idempotency guard (`fc00b5d0`):** inside `WoWSharpObjectManager.
   EnterWorld`, no-op when `HasEnteredWorld == true && _playerGuid.FullGuid
   == characterGuid && prevMapId != 0`.
4. **Upstream gate (`654bfde9`):** the actual reason the behavior tree was
   re-ticking the `EnterWorldSequence`. When `ApplyLoadout` arrived after
   world entry, `BotRunnerService.UpdateBehaviorTree`'s action-processing
   block (`HandleApplyLoadoutAction`, line 803) returned early at line 804
   *without clearing the leftover login-flow tree* (status=Success). The
   bot's main loop re-ticked the completed `Sequence(Condition, Do)` on
   the next iteration, which re-ran the `Do` and called `EnterWorld`
   again. Fix: at the top of `UpdateBehaviorTree`, drop any non-Running
   tree.

The redundant EnterWorld no longer fires (verified by absence of
"Suppressing redundant" warnings in clean test runs since `654bfde9`).

### What's left in Phase E

The pilot is stable. The broader Phase E work is the **migration**: rebuild
the `Equipment.Automated.config.json`-driven `EquipmentEquipTests`
migration that v4 wrote and v5 reverted, then chip away at the rest of
the suite per the per-category siblings plan. Each migrated test moves
from "fixture stages everything via SOAP" to "Automated mode dispatches
LoadoutTask via the snapshot pipeline".

The diagnostic logs added in this session (the EnterWorld stack-trace and
the `[BOT RUNNER] Login flow building EnterWorldSequence` warning) are
still in the code. Useful for the next migration; can be lowered to
Information if they get noisy.

---

## Phase D — Taxi single + multi-hop, Elevator pilot

`Taxi_HordeRide_OrgToXroads` and `Taxi_MultiHop_OrgToGadgetzan` now both
assert *arrival* (close to the destination flight master AND on-transport
flag cleared), not just departure. Coordinates pulled from VMaNGOS
`mangos.taxi_nodes`:

```sql
SELECT id, name, x, y, z FROM mangos.taxi_nodes WHERE id IN (23, 25, 40);
-- 23: Orgrimmar, Durotar    (1677.59, -4315.71, 61.17)
-- 25: Crossroads, The Barrens (-441.80, -2596.08, 96.06)
-- 40: Gadgetzan, Tanaris    (-7048.89, -3780.36, 10.19)
```

`Elevator_FullRide_Undercity` is scaffolded but currently SKIPS on the
"bot never acquired a TransportGuid for the elevator" flake — the SAME
flake `TaxiTransportParityTests.Transport_Board_FgBgParity` documents.
The next session in this phase should investigate why bots don't reliably
get the TransportGuid update from MaNGOS while standing on the elevator
platform.

### What's left in Phase D

- The Org→Gadgetzan multi-hop currently lands ~840yd short of Gadgetzan
  with `pos.Z = -39` (underground). The test now correctly skips with a
  diagnostic; the underlying bug is a flight-path chaining or post-flight
  ground-snap regression. Worth a session.
- A Boat full-ride (Ratchet→Booty Bay or Menethil→Theramore) was not
  added — the Boat tests in `TransportTests` are still staging-only.
  Same shape as the Elevator full-ride; can be added once elevator
  TransportGuid acquisition is solid.

---

## Phase B — chat-driven prep-window for AV + WSG

`BgTestHelper.WaitForBattlegroundStartAsync` polls all bot snapshots'
`RecentChatMessages` for any line containing "begun" (case-insensitive)
every 2s. Returns early when seen, falls back to the original wall-clock
budget. No regression risk: the fallback path is the original sleep.

Used at:
- `BattlegroundEntryTests.cs:123` (was 130s blind, now ≤130s with
  early-exit).
- `WsgObjectiveTests.cs:142` (was 95s blind, now ≤95s with early-exit).

Saves up to (budget - actualOpen) per run. On AV the gate timer is
typically 120s after queue-pop; if the test queues at e.g. T+30 into the
window, the helper exits at T+90 and saves 40s.

### What's left in Phase B

| Wall | File:Line | Notes |
|---|---|---|
| 1.5s × 1 | `Raids/RaidCoordinationTests.cs:160` (post-`AssignLoot`) | No snapshot field for raid loot rule today. Keep as-is unless the proto is extended. |
| 1.5s × 1 | `RaidFormationTests.cs:102` (post-`ChangeRaidSubgroup`) | No subgroup field on the snapshot. Keep as-is. |
| 1.0s × 1 | `RaidFormationTests.cs:116` (post-`DisbandGroup` cleanup) | Cleanup, not test-critical. Keep as-is. |

**v8 progress on this section:**
- 2.0s × 4 raid `Accept`/`ConvertToRaid` waits eliminated by 2249297c + 18249f2a.
- 1.5s × 2 raid `SendGroupInvite` waits and 900ms × N CoordinatorFixtureBase
  invite waits eliminated by 5940e9fd (added proto field 32
  `has_pending_group_invite`).

The remaining three items either need proto changes (loot rule,
subgroup) or are cleanup-only (post-disband).

---

## Phase C — Concurrent FG/BG (PENDING)

(unchanged from v6/v7)
- **Mining take-turns.** Both bots launch `StartGatheringRoute` against the *same* node concurrently.
- **Herbalism follow.** FG follows BG between nodes.
- **Generalize "FG follows BG"** as a BotRunner-side helper.

---

## Phase F-1 — StateManager Automated mode

**COMPLETE.** Don't touch.

## Phase F-2 / F-3 — OnDemand + production polish (PENDING)

(unchanged from v6 description)

## Phase G — Final cleanup (PENDING)

After everything above.

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

### Correction 4 (NEW v8) — `BehaviourTree` library re-ticks completed Sequences

Empirically, the `BehaviourTree.dll` library's `Sequence` node
re-evaluates all children on every `Tick()` even after a previous tick
returned `Success`. So leaving a completed login-flow tree in
`_behaviorTree` causes the `Sequence(Condition, Do EnterWorld)` to
re-fire `EnterWorld` on the next loop iteration. **Always clear
`_behaviorTree` to null after a non-Running tick** (this is what the
`654bfde9` fix at the top of `UpdateBehaviorTree` does).

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
- **NEW v8 — Don't run `dotnet test --filter "Category!=RequiresInfrastructure"`** as a "broad unit suite". The negation includes tests with no category attribute that DO spin up bot processes; that orphans bot processes and leaves the build directory locked. Use positive filters (`FullyQualifiedName~XxxTests`) or run per-project (`Tests/WoWSharpClient.Tests`).

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

1. Run the verification block in § "What landed since v7" above.
2. Read in order: this handoff (top to bottom) → optionally
   `docs/handoff_test_consolidation_v7.md` for the Phase E narrative
   archeology → `docs/statemanager_modes_design.md` (apply the three
   corrections above) → `CLAUDE.md` → `Tests/CLAUDE.md`.
3. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md` — load-bearing critical rules live there.
4. Pick a phase. **Recommended order:**
   - **Phase E broader migration** — pilot is stable; rebuild the
     `Equipment.Automated.config.json` `EquipmentEquipTests` migration
     and start chipping away at per-category siblings.
   - **Phase D — investigate Multi-hop ground-snap regression** — bot
     lands 840yd short of Gadgetzan at z=-39 underground. Look at the
     "Authoritative relocation detected without pending ground snap"
     warning around the multi-hop final hop.
   - **Phase D — investigate elevator TransportGuid acquisition** —
     `Elevator_FullRide_Undercity` and `Transport_Board_FgBgParity` both
     fail to acquire a TransportGuid on the Undercity elevator.
   - **Phase B — Raid 1.5s × 5 predicates** — `WaitForRaidMembershipAsync`-
     style replacement.
   - **Phase C — Concurrent FG/BG** — Mining take-turns, Herbalism
     follow, generalize "FG follows BG".
   - **Phase F-2 — OnDemand handler.**
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v9.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v8 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
