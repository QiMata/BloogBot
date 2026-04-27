# Handoff v10: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`).
> **Picks up from:** `docs/handoff_test_consolidation_v9.md` (last commit
> listed there: `1663b798`). v10 supersedes v9 on the FG LoadoutTask gap
> (now diagnosed and a candidate fix landed) and adds a third Phase E
> sibling migration.
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/statemanager_modes_design.md` — Phase F design doc; the four
>   corrections from v4–v9 still apply.
> - `docs/handoff_test_consolidation_v9.md` — the previous handoff. **Don't
>   reread the whole thing — v10 supersedes it where they conflict.**

---

## Why this work exists

The Shodan-shaped LiveValidation suite is architecturally clean. The
remaining problems are bloat, sequential execution, shallow taxi/transport
coverage, and StateManager's lack of a first-class `Automated` mode.
Phases B (delay cleanup), C (concurrent FG/BG), D (real taxi/transport
rides), F (modes), and E (LiveFixture consolidation, gated on F-1 step 3)
tackle these in order.

**Phases B (high-ROI), D (Taxi + Elevator pilots), F-1 are COMPLETE.**
Phase E broader migration is in progress (3 of N siblings done, BG-only,
plus a candidate fix for the FG gap that lets future siblings be FG/BG
end-to-end if the fix verifies). Phases C, F-2/F-3, G are still open.

---

## What landed since v9 (`1663b798`)

Two commits this session:

| Commit | Phase | Summary |
|---|---|---|
| `4b24a530` | E | `feat(phase-e): add Fishing_AutomatedMode_BgOnly_RatchetStagedPool (BG-only)` — third Phase E sibling. Adds `Fishing.Automated.config.json` (Mode=Automated, Skills=[356/75/75], SupplementalItems=[6256 Fishing Pole]) and a new test that asserts the BG bot's `CharacterSettings.Loadout` is dispatched as `APPLY_LOADOUT` by `AutomatedModeHandler.OnWorldEntryAsync`, then stages a Ratchet pool via Shodan and asserts `FishingTask` reaches `fishing_loot_success`. BG-only per the v9 FG gap. |
| `d385d31e` | E | `fix(bot-runner): preserve LoadoutTask when InitializeTaskSequence runs` — candidate fix for the FG LoadoutTask gap. **NOT YET VERIFIED end-to-end on FG.** See "FG gap fix" section below. |

Build verification:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release` — 0 errors.
- 40 LoadoutTask unit tests pass.
- 1 unrelated pre-existing flake in
  `BotRunnerServiceCombatDispatchTests.BuildBehaviorTreeFromActions_ReleaseCorpse_HealthZeroWithoutStandDead_ReleasesSpirit`
  fails identically with and without this branch's changes (verified via
  `git stash` toggle). Not caused by v10.

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline 1663b798..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

The Docker stack (`mangosd`, `realmd`, `pathfinding-service`) was healthy
at session start.

---

## FG gap fix — diagnosis + candidate fix (REQUIRES VERIFICATION)

### Diagnosis

The v9 finding was that the FG bot's `LoadoutTask` hangs in
`LoadoutInProgress` for 90+ seconds and never delivers supplemental
items, while BG completes the same loadout in seconds. v9 hypothesized
this was a chat-throttle / `MainThreadLuaCall` issue. **It's not.**

Real root cause, found in `D:\World of Warcraft\Logs\botrunner_EQUIPFG1.diag.log`:

```
[20:26:27.294] [ACTION-RECV] type=ApplyLoadout params=0 ready=True
[20:26:43.952] [TICK#201] ready=True action=null tree=Failure tasks=2(IdleTask) screen=InWorld …
[20:27:00.599] [TICK#301] ready=True action=null tree=Failure tasks=2(IdleTask) screen=InWorld …
…
```

`tasks=2(IdleTask)` is the smoking gun. The `IdleTask` is on **top** of
the stack, not the `LoadoutTask`. `_botTasks.Peek().Update()` only ticks
the top, so the `LoadoutTask` is buried and never executes.

The mechanism, in
[Exports/BotRunner/BotRunnerService.cs](../Exports/BotRunner/BotRunnerService.cs):

1. The action dispatch block (lines 768–843, reached at the top of
   `UpdateBehaviorTree`) processes incoming actions BEFORE the
   world-ready / `InitializeTaskSequence` block (line 851).
2. The `ApplyLoadout` branch returns early after pushing `LoadoutTask`
   (line 817).
3. On a subsequent tick, the world-ready check enters
   `InitializeTaskSequence`, which naively pushed `IdleTask` then the
   activity task ON TOP of the existing `LoadoutTask`.

This race only fires when an `ApplyLoadout` action is received before
`_tasksInitialized` flips true. On BG, IPC + connect timings put the
world-ready check (and `InitializeTaskSequence`) before action dispatch
processes the action, so the bug stayed hidden. On FG, the snapshot
sometimes reports `IsObjectManagerValid=true` while
`WorldEntryHydration.IsReadyForWorldInteraction(Player)` is still false
(`Player.MaxHealth=0` momentarily during world-load), which triggers
`AutomatedModeHandler.OnWorldEntryAsync` to dispatch APPLY_LOADOUT before
the bot has run `InitializeTaskSequence`.

### Fix

`d385d31e` modifies `InitializeTaskSequence` to capture any pre-existing
tasks (e.g. a `LoadoutTask` pushed by an earlier action dispatch),
seed `IdleTask`/activity at the bottom of the stack as before, then
re-push the captured tasks in their original order so the previous top
stays on top:

```csharp
var preExistingTasks = new List<IBotTask>();
while (_botTasks.Count > 0)
    preExistingTasks.Add(_botTasks.Pop());

_botTasks.Push(new Tasks.IdleTask(context));
// (push activity if assigned)
for (var i = preExistingTasks.Count - 1; i >= 0; i--)
    _botTasks.Push(preExistingTasks[i]);
```

When the stack is empty at entry (the BG common case), behavior is
identical to before. Only the FG race case gets the new behavior.

### What's still open: end-to-end verification on FG

The fix builds clean and unit tests pass, but **the FG Automated-mode
end-to-end path has not been validated against a live MaNGOS stack yet
this session.** Concretely, the next session should:

1. Confirm `git log --oneline -3` includes `d385d31e`.
2. Kill any stale `WoW.exe` PIDs (only ones owned by stale test runs —
   never blanket-kill, per the CLAUDE.md process-safety rule).
3. Run:
   ```bash
   cd "e:/repos/Westworld of Warcraft"
   dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj \
     --configuration Release \
     --filter "FullyQualifiedName~EquipItem_AutomatedMode_LoadoutAppliesAndEquips"
   ```
4. The test currently dispatches BG-only via
   `ResolveBotRunnerActionTargets(includeForegroundIfActionable: false)`.
   To verify FG/BG parity post-fix, **flip that to `true`** locally for the
   verification run (do NOT commit that flip). If the FG path also reaches
   `LoadoutReady` and the `EquipItem` dispatch lands the Worn Mace into
   mainhand, the fix is verified.
5. Inspect `D:\World of Warcraft\Logs\botrunner_EQUIPFG1.diag.log` after
   the run: the new tick line should show
   `tasks=2(LoadoutTask)` (LoadoutTask on top, IdleTask underneath) for
   the period between APPLY_LOADOUT receipt and `LoadoutReady`. The
   Re-stacked diagnostic line `[BOT RUNNER] Re-stacked 1 pre-existing
   task(s) on top of seeded IdleTask/activity for EQUIPFG1: top=LoadoutTask`
   should appear once.
6. If verified, the BG-only restriction can be relaxed for all Phase E
   Automated migrations (the three already-landed tests, plus future
   ones). Update each test to pass
   `includeForegroundIfActionable: true` and remove the FG-skip
   `[ACTION-PLAN]` line.

### If verification fails

The fix is targeted and small. Likely failure modes:
- **Tests that already passed BG-only break**: my fix runs identically
  for BG (`preExistingTasks.Count == 0` short-circuits). If a BG test
  regresses, look at whether the test runs `InitializeTaskSequence`
  twice (e.g. `_tasksInitialized` gets reset). Unlikely but possible.
- **FG still hangs**: the gap may have a second cause downstream
  (e.g. the FG bot's `KnownSpellIds` not picking up an already-known
  spell, blocking `LearnSpellStep.IsSatisfied`). v9's narrative pointed
  at this but the diag log makes clear the LoadoutTask wasn't even
  ticking. If FG ticks now but stalls on the LearnSpell step, the
  failure log will show many `[BOT RUNNER]` traces from `LoadoutTask`
  through the named pipe → StateManager log. Search for
  `ForegroundBot.EQUIPFG1` in `TestResults/LiveLogs/EquipmentEquipTests.log`.
- **FG's Serilog is not configured**: noted but separate. The FG
  ForegroundBotWorker only adds `AddConsole` + `NamedPipeLoggerProvider`
  (which only routes `ILogger`, not `Serilog.Log.*`). `BotRunnerService`
  and `LoadoutTask` use `Serilog.Log.Information`. Those traces are silent
  on FG by default. The instance-level `DiagLog` in `BotRunnerService`
  DOES write to `<AppContext.BaseDirectory>/logs/botrunner_<account>.diag.log`,
  which on FG resolves to `D:\World of Warcraft\Logs\` — that's where the
  evidence above came from. If you need richer traces, add a Serilog
  static-logger init (file sink only, no console) in
  `Services/ForegroundBotRunner/Program.cs::CreateHostBuilder`. Mirror
  `Services/BackgroundBotRunner/Program.cs` lines 28–46.

---

## Phase E status — the three siblings that landed

(Existing two commits from v9 plus today's third):

| Test | Config | FG/BG status |
|---|---|---|
| `EquipmentEquipTests.EquipItem_AutomatedMode_LoadoutAppliesAndEquips` | `Equipment.Automated.config.json` | BG-only today; eligible to flip to FG+BG once `d385d31e` verifies |
| `UnequipItemTests.UnequipItem_AutomatedMode_LoadoutAppliesAndUnequips` | `Equipment.Automated.config.json` (shared) | BG-only today; same |
| `FishingProfessionTests.Fishing_AutomatedMode_BgOnly_RatchetStagedPool` | `Fishing.Automated.config.json` (NEW v10) | BG-only today; same. Reuses `EnsureCloseFishingPoolActiveNearAsync` from Shodan staging. |

### Recommended next siblings (unchanged from v9)

Pick from the **Dual-Bot Conditional** group in
`Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`.

- **`GatheringProfessionTests`** — Mining + Herbalism on the same
  character. Needs `Gathering.Automated.config.json` with mining pick
  (`SupplementalItemIds=[2901]`) + skills 186/182.
- **`OrgrimmarGroundZAnalysisTests`** — minimal loadout (just spawn at
  Orgrimmar). Could be the simplest migration, just to validate the
  pattern works for "no-loadout" cases. Note: this test lives in
  `Tests/BotRunner.Tests/Diagnostics/` not LiveValidation — keep it there.
- **`TalentAllocationTests`** — needs `talent_template` in the
  `Loadout`, and that path goes through offline SOAP/MySQL during
  coordinator pre-launch (see `LoadoutTask.cs` scope note). May need
  schema work first.

The shared helpers from this session — `DispatchEquipAndAssertAsync` /
`DispatchUnequipAndAssertAsync` (Equipment) and the new
`RunAutomatedFishingScenario` — are good models. Each migrated
`<Category>Tests.cs` should keep the legacy method intact and add a new
`*_AutomatedMode_*` method following the now-three-test pattern.

---

## Phase B — chat-driven prep-window for AV + WSG

(unchanged from v9)

`BgTestHelper.WaitForBattlegroundStartAsync` polls all bot snapshots'
`RecentChatMessages` for any line containing "begun" (case-insensitive)
every 2s. Used at:

- `BattlegroundEntryTests.cs:123` (was 130s blind, now ≤130s with
  early-exit).
- `WsgObjectiveTests.cs:142` (was 95s blind, now ≤95s with early-exit).

### What's left in Phase B

| Wall | File:Line | Notes |
|---|---|---|
| 1.5s × 1 | `Raids/RaidCoordinationTests.cs:160` (post-`AssignLoot`) | No snapshot field for raid loot rule today. Keep as-is unless the proto is extended. |
| 1.5s × 1 | `RaidFormationTests.cs:102` (post-`ChangeRaidSubgroup`) | No subgroup field on the snapshot. Keep as-is. |
| 1.0s × 1 | `RaidFormationTests.cs:116` (post-`DisbandGroup` cleanup) | Cleanup, not test-critical. Keep as-is. |

---

## Phase D — Taxi single + multi-hop, Elevator pilot

(unchanged from v9)

`Taxi_HordeRide_OrgToXroads` and `Taxi_MultiHop_OrgToGadgetzan` both
assert *arrival*. `Elevator_FullRide_Undercity` is scaffolded but
currently SKIPS on the "bot never acquired a TransportGuid for the
elevator" flake.

### What's left in Phase D

- The Org→Gadgetzan multi-hop currently lands ~840yd short of Gadgetzan
  with `pos.Z = -39` (underground). Test correctly skips with a
  diagnostic; underlying bug is a flight-path chaining or post-flight
  ground-snap regression. Worth a session.
- A Boat full-ride was not added — same shape as the Elevator full-ride
  but blocked by elevator TransportGuid acquisition.

---

## Phase C — Concurrent FG/BG (PENDING)

(unchanged from v6/v7/v8/v9)
- Mining take-turns. Both bots launch `StartGatheringRoute` against the same node concurrently.
- Herbalism follow. FG follows BG between nodes.
- Generalize "FG follows BG" as a BotRunner-side helper.

---

## Phase F-1 — StateManager Automated mode

**COMPLETE.** Don't touch.

## Phase F-2 / F-3 — OnDemand + production polish (PENDING)

(unchanged from v6 description)

## Phase G — Final cleanup (PENDING)

After everything above.

---

## Course corrections recorded so far (don't re-discover them)

(unchanged from v9 — all still valid; new Correction 5 added)

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

### Correction 5 (NEW v10) — `InitializeTaskSequence` must preserve action-dispatched tasks

The v9 finding "FG LoadoutTask hangs in LoadoutInProgress" was diagnosed
in v10 as a stack-ordering bug, NOT a chat-throttle issue. The fix lives
in `Exports/BotRunner/BotRunnerService.cs::InitializeTaskSequence`. If
you see a future symptom where a task pushed by action dispatch
(`HandleApplyLoadoutAction`, etc.) ends up buried under `IdleTask`,
verify the re-stacking logic still runs.

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
- **From v8 — Don't run `dotnet test --filter "Category!=RequiresInfrastructure"`** as a "broad unit suite". The negation includes tests with no category attribute that DO spin up bot processes; that orphans bot processes and leaves the build directory locked. Use positive filters (`FullyQualifiedName~XxxTests`) or run per-project (`Tests/WoWSharpClient.Tests`).
- **From v9 — Phase E Automated migrations are BG-only until the FG LoadoutTask gap is fixed.** v10 fix is *candidate, not verified*. Keep new migrations BG-only until the verification in "FG gap fix" section above succeeds.

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

1. Run `git fetch origin && git log --oneline 1663b798..origin/main` and
   confirm both `4b24a530` and `d385d31e` are present.
2. Run `docker ps` and confirm `mangosd`, `realmd`, `pathfinding-service`
   are healthy.
3. Read in order: this handoff (top to bottom) → optionally
   `docs/handoff_test_consolidation_v9.md` for the FG investigation
   narrative that v10 supersedes → `docs/statemanager_modes_design.md`
   (apply the five corrections above) → `CLAUDE.md` → `Tests/CLAUDE.md`.
4. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md`.
5. **First task — verify the FG LoadoutTask fix end-to-end.** See
   "FG gap fix → What's still open: end-to-end verification on FG"
   above. If it works, that unblocks every Phase E migration to flip
   from BG-only to FG+BG.
6. After that, pick a phase. **Recommended order:**
   - **If the FG fix verifies:** flip the three landed Automated tests
     (Equipment Equip, Equipment Unequip, Fishing) from BG-only to
     FG+BG by passing `includeForegroundIfActionable: true` and
     removing the FG-skip log line. One commit per test.
   - **If the FG fix does NOT verify:** investigate the secondary
     symptom (likely `LearnSpellStep.IsSatisfied` for already-known
     spells, or `KnownSpellIds` not populating). Add Serilog config to
     FG's `Program.cs` (mirror BG's pattern at lines 28–46) so
     `LoadoutTask` traces appear in a log. Then trace which step is
     stuck.
   - **Phase E broader migration** — `GatheringProfessionTests` is the
     next sibling target.
   - **Phase D — investigate Multi-hop ground-snap regression**.
   - **Phase D — investigate elevator TransportGuid acquisition**.
   - **Phase C — Concurrent FG/BG** — Mining take-turns, Herbalism
     follow, generalize "FG follows BG".
   - **Phase F-2 — OnDemand handler.**
7. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v11.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v10 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
