# Handoff v9: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`).
> **Picks up from:** `docs/handoff_test_consolidation_v8.md` (last commit
> listed there: `97c2a811`). v9 supersedes v8 on Phase E broader migration.
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/statemanager_modes_design.md` — Phase F design doc; the four
>   corrections from v4–v8 still apply.
> - `docs/handoff_test_consolidation_v8.md` — the previous handoff. **Don't
>   reread the whole thing — v9 supersedes it where they conflict.**

---

## Why this work exists

The Shodan-shaped LiveValidation suite is architecturally clean. The
remaining problems are bloat, sequential execution, shallow taxi/transport
coverage, and StateManager's lack of a first-class `Automated` mode.
Phases B (delay cleanup), C (concurrent FG/BG), D (real taxi/transport
rides), F (modes), and E (LiveFixture consolidation, gated on F-1 step 3)
tackle these in order.

**Phases B (high-ROI), D (Taxi + Elevator pilots), E (pilot + first two
sibling migrations), and F-1 are now COMPLETE.** Phases C, F-2/F-3, G are
still open. Phase E broader migration is in progress (2 of N siblings
done, BG-only — see "Phase E status" below).

---

## What landed since v8 (`97c2a811`)

Two Phase E sibling migrations. Both compile clean and pass:

| Commit | Phase | Summary |
|---|---|---|
| `f51c4ca2` | E | `feat(phase-e): add EquipItem_AutomatedMode_LoadoutAppliesAndEquips (BG-only)` — pilot rebuild of the v4 EquipmentEquipTests Automated migration that v5 reverted. Loads `Equipment.Automated.config.json`, asserts loadout lands and EquipItem moves the Worn Mace into mainhand. Refactors the legacy test's tail into shared `DispatchEquipAndAssertAsync`. |
| `cc8fb940` | E | `feat(phase-e): add UnequipItem_AutomatedMode_LoadoutAppliesAndUnequips (BG-only)` — second sibling. Reuses `Equipment.Automated.config.json` (same loadout). Equip-if-needed precondition then asserts `UnequipItem(MainhandEquipSlot)` empties mainhand. Refactors the legacy tail into shared `DispatchUnequipAndAssertAsync`. |

Verification:
- `EquipmentEquipTests.EquipItem_AutomatedMode_LoadoutAppliesAndEquips` passes 1/1 in ~1m20s (full StateManager restart + first world entry + 90s loadout window + 8s equip transition).
- `UnequipItemTests.UnequipItem_AutomatedMode_LoadoutAppliesAndUnequips` passes 1/1 in ~58s (faster because the second test in a session reuses the StateManager from the first when both use the same config path).
- Legacy tests untouched semantically — the Automated tests share helpers (`DispatchEquipAndAssertAsync`, `DispatchUnequipAndAssertAsync`) but each scenario builder still funnels into the same final dispatch+assert.

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline 97c2a811..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

The Docker stack must show `mangosd`, `realmd`, `pathfinding-service`
healthy. Confirmed healthy at session start.

---

## Phase E status — broader migration is the active work

### What's done (this session)

- `EquipmentEquipTests` — `EquipItem_AutomatedMode_LoadoutAppliesAndEquips`. BG-only.
- `UnequipItemTests` — `UnequipItem_AutomatedMode_LoadoutAppliesAndUnequips`. BG-only.
- Both reuse the existing `Equipment.Automated.config.json` (no new config needed).

### NEW v9 finding — FG Automated-mode LoadoutTask gap

When you run a Phase E migration that resolves both FG and BG targets,
the **FG bot's LoadoutTask hangs in `LoadoutInProgress` for >90s and never
delivers the supplemental items**. BG completes the same loadout in
seconds. Trace from the failed first run of EquipItem Automated with both
targets:

```
[BG] Before equip: mainhand=0x400000000000033F, maces in bags=1
[BG] Dispatching EquipItem for Worn Mace (36).
[BG] Equip detected after 218ms: mainhand=0x400000000000044E, maces in bags=0
[automated-loadout EQUIPFG1] Still waiting... 5s / 90s elapsed
... (continues every 5s up to 86s)
[FG] Automated loadout never delivered Worn Mace within 90s. LoadoutStatus='LoadoutInProgress', failureReason=''.
```

The mode handler resolves correctly (`[MODE] StateManager dispatch handler resolved as AutomatedModeHandler for Mode=Automated`)
and the FG bot enters world. The shared `LoadoutTask` calls
`om.SendChatMessage(".additem 36")` for both. On FG that goes through
`MainThreadLuaCall($"SendChatMessage(...)")` in
`Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs:90`.
The Onboarding pilot was BG-only (`TESTBOT2`), so this gap was not
exposed by F-1.

**Where to start the FG investigation:**

1. Inspect the FG side of `LoadoutTask`'s plan walk (`Exports/BotRunner/Tasks/LoadoutTask.cs`).
   Each step's `TryExecute` returns true if `om?.Player == null` is false.
   Add a `Log.Information` to `AddItemStep.TryExecute` and
   `LearnSpellStep.TryExecute` that logs the FG path — does the chat
   command actually fire? Does `BagContainsItem` flip to true after?
2. Tail the FG bot's log via the named pipe
   (`Bot/Release/net8.0/logs/botrunner_EQUIPFG1.diag.log` or similar — FG
   doesn't write to `WWoWLogs/bg_*.log`; check `BotLogPipeServer` in
   `StateManagerWorker.cs` for the actual sink path).
3. Check whether FG's chat throttle / `MainThreadLuaCall` queue is
   blocked during a fresh world entry. The Lua-safe gate
   (`ConnectionStateMachine.IsLuaSafe`) may be flipping false during
   character-create / world-load.
4. Compare BG's `om.SendChatMessage(".additem 36")` path
   (`Exports/WoWSharpClient/`) — that one demonstrably works in the same
   session — to see what assumption FG's path violates.

Until FG parity lands, **all new Phase E Automated migrations should be
BG-only** (`includeForegroundIfActionable: false`) with a tracked-skip
note pointing back to the legacy Shodan-staged test for FG/BG parity.

### Recommended next siblings

Pick from the **Dual-Bot Conditional** group in
`Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`. Most
of these only need a small `<Category>.Automated.config.json` with a
`Loadout` block. **Note: BG-only for now per the FG gap.**

Highest value (per the Shodan inventory):

- **`FishingProfessionTests`** — already has TESTBOT1/2 in
  `Fishing.config.json`; new `Fishing.Automated.config.json` only needs
  `Skills=[{ SkillId: 356, Value: 75, Max: 75 }]` and
  `SupplementalItemIds=[6256]` (Fishing Pole). Pattern is identical to
  `Onboarding.config.json` — the Onboarding pilot already exercises this
  loadout, so the migration is mostly a fixture path swap.
- **`GatheringProfessionTests`** — Mining + Herbalism on the same
  character. Needs `Gathering.Automated.config.json` with mining pick
  (`SupplementalItemIds=[2901]`) + skills 186/182.
- **`OrgrimmarGroundZAnalysisTests`** — minimal loadout (just spawn at
  Orgrimmar). Could be the simplest migration, just to validate the
  pattern works for "no-loadout" cases.
- **`TalentAllocationTests`** — needs `talent_template` in the
  `Loadout`, and that path goes through offline SOAP/MySQL during
  coordinator pre-launch (see `LoadoutTask.cs` scope note). May need
  schema work first.

The shared helpers from this session — `DispatchEquipAndAssertAsync` /
`DispatchUnequipAndAssertAsync` — are good models. Each migrated
`<Category>Tests.cs` should keep the legacy method intact and add a new
`*_AutomatedMode_*` method that:

1. Loads `<Category>.Automated.config.json` via `EnsureSettingsAsync`.
2. Calls `ResolveBotRunnerActionTargets(includeForegroundIfActionable: false)`.
3. Logs a tracked-skip note for FG.
4. Polls snapshots for `LoadoutStatus.LoadoutReady` (or a category-specific
   loadout marker — e.g. fishing pole in bags).
5. Dispatches the action under test exactly as the legacy test does.

### What v8 said that's now refined (Phase E)

v8 said the pilot is stable; broader migration is "rebuild the
EquipmentEquipTests migration that v4 wrote and v5 reverted, then chip
away at per-category siblings." Refinement:

- The pilot rebuild and one extra sibling are now done (commits above).
- The FG LoadoutTask gap is the ceiling on the broader migration. Either
  fix it (so both targets can be migrated end-to-end) or accept BG-only
  Automated coverage and keep the legacy Shodan-staged tests as the
  FG/BG parity proof.

---

## Phase B — chat-driven prep-window for AV + WSG

(unchanged from v8)

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

The remaining items either need proto changes or are cleanup-only.

---

## Phase D — Taxi single + multi-hop, Elevator pilot

(unchanged from v8)

`Taxi_HordeRide_OrgToXroads` and `Taxi_MultiHop_OrgToGadgetzan` both
assert *arrival* (close to the destination flight master AND on-transport
flag cleared), not just departure. `Elevator_FullRide_Undercity` is
scaffolded but currently SKIPS on the "bot never acquired a TransportGuid
for the elevator" flake — same flake as
`TaxiTransportParityTests.Transport_Board_FgBgParity`.

### What's left in Phase D

- The Org→Gadgetzan multi-hop currently lands ~840yd short of Gadgetzan
  with `pos.Z = -39` (underground). Test correctly skips with a
  diagnostic; underlying bug is a flight-path chaining or post-flight
  ground-snap regression. Worth a session.
- A Boat full-ride (Ratchet→Booty Bay or Menethil→Theramore) was not
  added — the Boat tests in `TransportTests` are still staging-only.
  Same shape as the Elevator full-ride; can be added once elevator
  TransportGuid acquisition is solid.

---

## Phase C — Concurrent FG/BG (PENDING)

(unchanged from v6/v7/v8)
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

## Four course corrections recorded so far (don't re-discover them)

(unchanged from v8 — all still valid)

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

### Correction 4 (from v8) — `BehaviourTree` library re-ticks completed Sequences

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
- **From v8 — Don't run `dotnet test --filter "Category!=RequiresInfrastructure"`** as a "broad unit suite". The negation includes tests with no category attribute that DO spin up bot processes; that orphans bot processes and leaves the build directory locked. Use positive filters (`FullyQualifiedName~XxxTests`) or run per-project (`Tests/WoWSharpClient.Tests`).
- **NEW v9 — Phase E Automated migrations are BG-only until the FG LoadoutTask gap is fixed.** Pass `includeForegroundIfActionable: false` and add a tracked-skip note pointing at the legacy Shodan-staged sibling for FG/BG parity. Don't try to make a new Automated test cover FG without first fixing the underlying gap (see "Phase E status" above).

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

1. Run the verification block in § "What landed since v8" above.
2. Read in order: this handoff (top to bottom) → optionally
   `docs/handoff_test_consolidation_v8.md` for the Phase E pilot fix
   narrative → `docs/statemanager_modes_design.md` (apply the four
   corrections above) → `CLAUDE.md` → `Tests/CLAUDE.md`.
3. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md` — load-bearing critical rules live there.
4. Pick a phase. **Recommended order:**
   - **(NEW) Investigate the FG Automated-mode LoadoutTask gap.** Real
     bug, well-bounded by the v9 logs and trace above. Once fixed, every
     future Phase E migration unlocks FG/BG parity end-to-end. See
     "NEW v9 finding" for concrete starting points.
   - **Phase E broader migration (BG-only)** — the next sibling targets
     are listed in "Recommended next siblings" above. Each is a small,
     ~50-line addition + a small JSON config. `FishingProfessionTests`
     is the lowest-risk next pick because the Onboarding pilot already
     proves the loadout shape (skill 356 + item 6256).
   - **Phase D — investigate Multi-hop ground-snap regression** — bot
     lands 840yd short of Gadgetzan at z=-39 underground. Look at the
     "Authoritative relocation detected without pending ground snap"
     warning around the multi-hop final hop.
   - **Phase D — investigate elevator TransportGuid acquisition** —
     `Elevator_FullRide_Undercity` and `Transport_Board_FgBgParity` both
     fail to acquire a TransportGuid on the Undercity elevator.
   - **Phase C — Concurrent FG/BG** — Mining take-turns, Herbalism
     follow, generalize "FG follows BG".
   - **Phase F-2 — OnDemand handler.**
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v10.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v9 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
