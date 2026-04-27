# Handoff v12: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`).
> **Picks up from:** [`docs/handoff_test_consolidation_v11.md`](handoff_test_consolidation_v11.md).
> v12 supersedes v11 on the FG `.learn`-on-FG gap (now fixed and verified).
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/statemanager_modes_design.md` — Phase F design doc; the six
>   corrections from v4–v11 still apply.
> - `docs/handoff_test_consolidation_v11.md` — the previous handoff.
>   **Don't reread the whole thing — v12 supersedes it on the FG
>   `.learn`-on-FG gap.**

---

## Why this work exists

The Shodan-shaped LiveValidation suite is architecturally clean. The
remaining problems are bloat, sequential execution, shallow taxi/transport
coverage, and StateManager's lack of a first-class `Automated` mode.
Phases B (delay cleanup), C (concurrent FG/BG), D (real taxi/transport
rides), F (modes), and E (LiveFixture consolidation, gated on F-1 step 3)
tackle these in order.

**Phases B (high-ROI), D (Taxi + Elevator pilots), F-1 are COMPLETE.**
Phase E broader migration is in progress — 3 of N siblings done. After
v12: 2 of those (Equipment Equip, Equipment Unequip) are now FG+BG;
the third (Fishing) is still BG-only because of an unrelated FG
fishing-cast issue. Phases C, F-2/F-3, G are still open.

---

## What landed since v11 (`f58b0282`)

Four commits this session segment, in order:

| Commit | Phase | Summary |
|---|---|---|
| `cb4fd977` | E (fix) | `fix(loadout): treat 'You already know this spell.' as LearnSpellStep success` — subscribes `LearnSpellStep` to `IWoWEventHandler.OnSystemMessage` in addition to `OnLearnedSpell`. Any system message containing "already know" (case-insensitive) flips `MarkAckFired`, satisfying the step. Verified end-to-end on FG: the `EquipItem_AutomatedMode_LoadoutAppliesAndEquips` test passes in 51s vs. the prior 90s timeout. **This is the resolution to the v11 secondary FG gap.** |
| `061183d7` | E | `feat(phase-e): flip EquipItem_AutomatedMode test to FG+BG` — passes `includeForegroundIfActionable: true`, removes the FG-skip `[ACTION-PLAN]` line, updates the doc comment. |
| `36b104ee` | E | `feat(phase-e): flip UnequipItem_AutomatedMode test to FG+BG` — same flip pattern. Verified passes in 48s. Also adds `RecentChatMessages` + `RecentErrors` dump on loadout-failure path. |
| `b6905a91` | E (docs) | `docs(phase-e): clarify Fishing FG-skip is unrelated to LoadoutTask gap; add RecentChatMessages dump` — flipping Fishing to FG+BG built fine and the loadout side worked, but the test failed on a *different* FG-only issue: Ratchet pool cast lands at distance 20.3yd / edgeDist 18.0yd, every loot window times out, after 8 attempts the FishingTask pops with `max_casts_reached`. Reverted to BG-only with a clarified comment that names the new gap. |

Build verification:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release` — 0 errors.
- 40 LoadoutTask unit tests pass.
- `EquipItem_AutomatedMode_LoadoutAppliesAndEquips` — passes FG+BG (51s).
- `UnequipItem_AutomatedMode_LoadoutAppliesAndUnequips` — passes FG+BG (48s).
- `Fishing_AutomatedMode_BgOnly_RatchetStagedPool` — passes BG-only (was BG-only since landing in v10).

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline 1663b798..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

The Docker stack (`mangosd`, `realmd`, `pathfinding-service`) was healthy
at session start.

---

## v11 secondary gap — RESOLVED ✓

The "FG sends 19× CMSG_MESSAGECHAT, server replies with constant 49-byte
SMSG_MESSAGECHAT, never sees SMSG_LEARNED_SPELL" mystery from v11 had a
trivial answer the user pointed out: the 49-byte response is a system
message ("You already know this spell." in English; localized variants
on other clients). The character's server-side spellbook *does* contain
spell 198 — FG's local `KnownSpellIds` was just out of sync with the
server's authoritative state. No SMSG_LEARNED_SPELL fires when no state
change happens.

**Fix:** [Exports/BotRunner/Tasks/LoadoutTask.cs::LearnSpellStep::OnAttachExpectedAck](../Exports/BotRunner/Tasks/LoadoutTask.cs)
now subscribes to `OnSystemMessage` and treats any message containing
"already know" as satisfaction. The match is intentionally narrow and
the subscription is only active while the step is on top of the
LoadoutTask — other steps don't subscribe.

**Lesson the user emphasized — saved as a feedback memory** (see `feedback_check_snapshot_first.md`):
**Server-side system messages flow back through `snapshot.RecentChatMessages` and `RecentErrors`.** When a bot fails (LoadoutTask stuck, GM command silently no-op'd, Action timeout), inspect those snapshot fields *first* before reaching for packet logs, MySQL, or `mangosd` server-log files. v10→v11 burned ~30 min on wire-protocol diagnosis when the snapshot already had the answer.

The three Phase E Automated tests now have `RecentChatMessages` +
`RecentErrors` dumps on their loadout-failure paths so future failures
won't repeat the mistake.

---

## Phase E status — three siblings landed

| Test | Config | FG/BG status |
|---|---|---|
| `EquipmentEquipTests.EquipItem_AutomatedMode_LoadoutAppliesAndEquips` | `Equipment.Automated.config.json` | **FG+BG** (since `061183d7`) |
| `UnequipItemTests.UnequipItem_AutomatedMode_LoadoutAppliesAndUnequips` | `Equipment.Automated.config.json` (shared) | **FG+BG** (since `36b104ee`) |
| `FishingProfessionTests.Fishing_AutomatedMode_BgOnly_RatchetStagedPool` | `Fishing.Automated.config.json` | BG-only — separate FG fishing-cast/loot-window gap (see below). |

### New gap surfaced: FG fishing cast distance / loot window

Flipping the Fishing Automated test to FG+BG passes the loadout side but
the bot's actual fishing fails. From the staged pool at Ratchet:

```
[TASK] FishingTask cast_started attempt=N pool=0xF11... spell=18248 distance=20.3 mode=fishing_cast
[TASK] FishingTask retry reason=loot_window_timeout
... (×8)
[TASK] FishingTask pop reason=max_casts_reached
```

Distance to pool: 20.3 yd (edgeDist 18.0 yd). Per `memory/fishing_fix.md`:

> Ratchet fishing dock coordinates: Dock surface at Z≈5.7, ~5yd wide
> (X: -991 to -986). Safe fishing position: (-988.5, -3834.0, 5.7),
> face 6.21 rad (east toward fishing node at -975, -3835).

The bot's cast position seems too far from the staged pool. Worth a
session: either tune the cast-position selection on FG, fix the
loot-window timing, or stage the pool closer.

**Recommended next siblings (unchanged from v11):**

- **`GatheringProfessionTests`** — Mining + Herbalism on the same
  character. Needs `Gathering.Automated.config.json` with mining pick
  (`SupplementalItemIds=[2901]`) + skills 186/182.
- **`OrgrimmarGroundZAnalysisTests`** — minimal loadout. Lives in
  `Tests/BotRunner.Tests/Diagnostics/` not LiveValidation.
- **`TalentAllocationTests`** — needs `talent_template`; that path goes
  through offline SOAP/MySQL during coordinator pre-launch.

---

## Phase B — chat-driven prep-window (unchanged from v9–v11)

`BgTestHelper.WaitForBattlegroundStartAsync` polls all bot snapshots'
`RecentChatMessages` for any line containing "begun" (case-insensitive)
every 2s. Used at:

- `BattlegroundEntryTests.cs:123` (was 130s blind, now ≤130s with early-exit).
- `WsgObjectiveTests.cs:142` (was 95s blind, now ≤95s with early-exit).

### What's left in Phase B

| Wall | File:Line | Notes |
|---|---|---|
| 1.5s × 1 | `Raids/RaidCoordinationTests.cs:160` (post-`AssignLoot`) | No snapshot field for raid loot rule. Keep as-is. |
| 1.5s × 1 | `RaidFormationTests.cs:102` (post-`ChangeRaidSubgroup`) | No subgroup field on the snapshot. Keep as-is. |
| 1.0s × 1 | `RaidFormationTests.cs:116` (post-`DisbandGroup` cleanup) | Cleanup, not test-critical. Keep as-is. |

---

## Phase D — Taxi single + multi-hop, Elevator pilot (unchanged)

### What's left in Phase D

- The Org→Gadgetzan multi-hop currently lands ~840yd short with `pos.Z = -39`
  (underground). Test correctly skips with a diagnostic; underlying bug
  is a flight-path chaining or post-flight ground-snap regression.
- A Boat full-ride was not added — same shape as the Elevator full-ride
  but blocked by elevator TransportGuid acquisition.

---

## Phase C — Concurrent FG/BG (PENDING, unchanged)

- Mining take-turns. Both bots launch `StartGatheringRoute` against the same node concurrently.
- Herbalism follow. FG follows BG between nodes.
- Generalize "FG follows BG" as a BotRunner-side helper.

---

## Phase F-1 — StateManager Automated mode

**COMPLETE.** Don't touch.

## Phase F-2 / F-3 — OnDemand + production polish (PENDING)

## Phase G — Final cleanup (PENDING)

After everything above.

---

## Course corrections recorded so far (don't re-discover them)

(Unchanged from v11 — all still valid; new Correction 7 added.)

### Correction 1 (from v2) — the `BattlegroundEntryTests` "3s × 7" batch is a false positive

The audit prescribed batching the 3s/5s/10s delays in
`Battlegrounds/BattlegroundEntryTests.cs`. **Skip this entirely.** Every
site is the poll-pacing tail of an outer `while` loop that already checks
the success condition.

### Correction 2 (from v2) — the F-1 design doc puts the snapshot hook in the wrong file

Bot snapshots arrive in
[Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs](../Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs),
not `StateManagerWorker.SnapshotProcessing.cs`. The wiring landed there
in commit `6eb85dcc`.

### Correction 3 (from v4) — AutomatedModeHandler is loadout-only, NOT loadout-then-activity

The bot starts the activity itself, regardless of mode, via
`BotRunnerService.InitializeTaskSequence` and
`ActivityResolver.Resolve(...)`. `AutomatedModeHandler.OnSnapshotAsync`
is intentionally a no-op.

### Correction 4 (from v8) — `BehaviourTree` library re-ticks completed Sequences

Always clear `_behaviorTree` to null after a non-Running tick (this is
what the `654bfde9` fix at the top of `UpdateBehaviorTree` does).

### Correction 5 (from v10) — `InitializeTaskSequence` must preserve action-dispatched tasks

The v9 finding "FG LoadoutTask hangs in LoadoutInProgress" was diagnosed
in v10 as a stack-ordering bug, NOT a chat-throttle issue. The fix lives
in `Exports/BotRunner/BotRunnerService.cs::InitializeTaskSequence`. The
re-stack is verified in v11.

### Correction 6 (from v11) — `Serilog.Log.*` is silently dropped on FG by default

Until commit `9d67c1aa`, FG's `Program.cs` only configured
`Microsoft.Extensions.Logging.AddConsole()`. `BotRunnerService` and
`LoadoutTask` use `Serilog.Log.*` directly, so those traces went
nowhere on FG. The fixture defaults to `Warning`; pass
`WWOW_TEST_BOT_LOG_LEVEL=Information` when invoking `dotnet test` to
see Information-level traces.

### Correction 7 (NEW v12) — read `snapshot.RecentChatMessages` BEFORE diagnosing loadout / GM-command failures

Server-side system messages ("You already know this spell.", "There is
no such command", permission errors) flow back through the snapshot's
`RecentChatMessages` and `RecentErrors` queues. The v11 secondary FG
gap looked like a deep wire-protocol mystery (CMSG/SMSG counts,
SMSG_LEARNED_SPELL absence, server log files, MySQL queries) but the
answer was a single line: "You already know this spell." Always inspect
those snapshot fields first; only escalate to packet/server-log
diagnostics if the snapshot is empty *and* you have evidence the
snapshot pipeline itself is broken. The three Phase E Automated tests
now dump `RecentChatMessages` + `RecentErrors` on loadout failure —
mirror that pattern in any new failure path.

---

## Hard rules (DO NOT VIOLATE)

- **R1 — No blind sequences/counters/timing hacks.** Gate on snapshot signal.
- **R2 — Poll snapshots, don't sleep.** Use `WaitForSnapshotConditionAsync(...)`.
- **R3 — Fail fast.** Per-milestone tight timeouts. `onClientCrashCheck` everywhere.
- **R4 — No silent exception swallowing.** Log warnings with context.
- **R5 — Fixture owns lifecycle, StateManager owns coordination, BotRunner owns execution.**
- **R6 — GM/admin commands must be asserted.** Use `AssertGMCommandSucceededAsync` etc.
- **R7 — Cross-test state must be reset.** `EnsureCleanSlateAsync` at test start.
- **R8 — x86 vs x64.** ForegroundBotRunner = x86. BackgroundBotRunner + StateManager + most tests = x64.
- **No background agents.** `run_in_background: true` is forbidden.
- **Single session only.** Auto-compaction handles context. Don't start a new session — keep going through compactions until you hand off via the recursive handoff prompt below.
- **Commit and push frequently.** Every logical unit of work.
- **Shodan is GM-liaison + setup-only.** Never dispatch behavior actions to Shodan. Don't weaken the `ResolveBotRunnerActionTargets` guard.
- **No `.gobject add`, no synthetic node spawns.** Test against natural world state.
- **Live MaNGOS via Docker is always running** — `docker ps` to confirm; never `tasklist`.
- **No MySQL mutations.** SOAP / bot chat for all game-state changes.
- **No `.learn all_myclass` / `.learn all_myspells`.** Always teach by explicit numeric spell ID.
- **From v8 — Don't run `dotnet test --filter "Category!=RequiresInfrastructure"`** as a "broad unit suite". Use positive filters (`FullyQualifiedName~XxxTests`) or run per-project (`Tests/WoWSharpClient.Tests`).
- **From v11 — When you locally flip a test's `includeForegroundIfActionable` to `true` for verification, REVERT IT before committing** unless you intend to make the flip permanent (which is its own commit, with FG+BG verified end-to-end).
- **From v12 — Read `snapshot.RecentChatMessages` BEFORE diagnosing.** See Correction 7.

### Bash CWD note

`bash` calls in this harness do *not* persist `cd`. Use absolute paths or
chain: `cd "e:/repos/Westworld of Warcraft" && git ...`.

### Docker sub-shell note

Calling `docker exec mangosd /bin/sh -c "..."` from this harness's bash
mangles the `/...` path. Either prefix `MSYS_NO_PATHCONV=1` or invoke
the binary directly: `docker exec mangosd tail -50 /opt/vmangos/storage/logs/Server.log`.

---

## Working agreements with the user (carry over)

- The user prefers concise responses. State decisions and changes; don't narrate deliberation.
- The user expects you to keep iterating without asking permission for each phase. Only ask if you genuinely need a decision they haven't already given.
- For **risky** actions (force push, deleting branches, mass file deletion, schema migrations), always confirm. Code edits and small commits are local/reversible — proceed.
- Integration tests run against the always-live Docker MaNGOS stack. Confirm `docker ps` once at session start; don't keep re-checking.

---

## Starter checklist for the receiving agent

1. Run `git fetch origin && git log --oneline 1663b798..origin/main` and
   confirm `4b24a530`, `d385d31e`, `b0c92f80`, `9d67c1aa`, `d0d99878`,
   `f58b0282`, `cb4fd977`, `061183d7`, `36b104ee`, `b6905a91` are all
   present.
2. Run `docker ps` and confirm `mangosd`, `realmd`, `pathfinding-service`
   are healthy.
3. Read in order: this handoff (top to bottom) → optionally
   [`docs/handoff_test_consolidation_v11.md`](handoff_test_consolidation_v11.md)
   for the diagnosis backstory →
   `docs/statemanager_modes_design.md` (apply the seven corrections
   above) → `CLAUDE.md` → `Tests/CLAUDE.md`.
4. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md`.
5. **Pick a phase. Recommended order:**
   - **Phase E broader migration** — `GatheringProfessionTests` is the
     next sibling target. Pattern: clone the
     `EquipItem_AutomatedMode_LoadoutAppliesAndEquips` shape, build
     `Gathering.Automated.config.json` with a mining pick
     (`SupplementalItemIds=[2901]`) + skills 186/182, dispatch
     `StartGatheringRoute` after loadout lands. Should pass FG+BG
     directly thanks to `cb4fd977`.
   - **Phase E Fishing FG enable** — diagnose the cast-distance /
     loot-window FG-only issue described above. Stage the pool
     closer to the bot's parked position, or tune the FG cast
     position selection.
   - **Phase D — investigate Multi-hop ground-snap regression**.
   - **Phase D — investigate elevator TransportGuid acquisition**.
   - **Phase C — Concurrent FG/BG** — Mining take-turns, Herbalism
     follow, generalize "FG follows BG".
   - **Phase F-2 — OnDemand handler.**
6. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v13.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v12 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
