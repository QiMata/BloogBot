# Handoff v4: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`, clean tree).
> **Picks up from:** `docs/handoff_test_consolidation_v3.md` (last commit listed there: `69426ed6`).
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/test_cleanup_audit.md` — Phase A audit; **read for context but the v4 status table below supersedes it where they conflict.**
> - `docs/statemanager_modes_design.md` — Phase F design doc; locks the schema for Automated/OnDemand modes. **Apply two corrections from v2 _and_ a third from v4 below.**
> - `docs/handoff_test_consolidation_v3.md` — the previous handoff. **Don't reread the whole thing — this v4 supersedes it where they conflict.**

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

## What landed in v3's session (since `69426ed6`)

**Phase F-1 step 3 is now COMPLETE.** Three commits since v3:

- **`491fa5c9` — F-1 step 3a: AutomatedModeHandler + DI registration.**
  Adds `Services/WoWStateManager/Modes/AutomatedModeHandler.cs`. At
  world entry it enqueues `APPLY_LOADOUT(character.Loadout)` exactly
  once via the listener's `EnqueueAction`. The `IStateManagerModeHandler`
  interface evolved to take a `Func<string, ActionMessage, bool>
  enqueueAction` parameter on each method so handlers stay free of any
  reference to the listener and can be unit-tested in isolation.
  `TestModeHandler` updated for the new signature; `Program.cs` DI
  fall-through now resolves `AutomatedModeHandler` for
  `Mode=Automated`. OnDemandActivities still falls through to
  `TestModeHandler` until F-2.

- **`6eb85dcc` — F-1 step 3b: wire mode-handler call sites.**
  `CharacterStateSocketListener` constructor now takes
  `IStateManagerModeHandler`. After every full snapshot (skipping
  heartbeats), it invokes `OnWorldEntryAsync` (latched once per account
  on first `IsObjectManagerValid=true` via a `_worldEntryDispatched`
  ConcurrentDictionary) and `OnSnapshotAsync`. Errors are logged but
  never bubble. `StateManagerWorker` passes its singleton handler
  through. `ActionForwardingContractTests.CreateListener` constructs a
  `TestModeHandler` to satisfy the new parameter. **And —
  `LiveBotFixture.SeedExpectedAccountsFromStateManagerSettings` now
  accepts the wrapped `{ Mode, Characters }` shape**; without this any
  test that loads an Automated/OnDemand config via `EnsureSettingsAsync`
  fails at fixture init.

- **`3b95f452` — F-1 step 3c: Onboarding live test.**
  Adds `Services/WoWStateManager/Settings/Configs/Onboarding.config.json`
  using the wrapped schema with `Mode=Automated`. Uses TESTBOT2 (BG)
  with a small Loadout (Fishing skill 75 + fishing pole 6256). Adds
  `Tests/BotRunner.Tests/LiveValidation/OnboardingAutomatedModeTests.cs`
  which loads the config and asserts `LoadoutStatus` advances past
  `LoadoutNotStarted` within 60s — that single signal proves
  AutomatedModeHandler.OnWorldEntryAsync fired, BuildApplyLoadoutAction
  succeeded, and the bot received APPLY_LOADOUT and built a LoadoutTask.
  Best-effort `LoadoutReady` check runs but doesn't fail the test (see
  flapping note below). Verified live: 1 passed in ~1m43s.

End-to-end signal observed in the StateManager logs from the verified run:
```
[MODE] StateManager dispatch handler resolved as AutomatedModeHandler for Mode=Automated
[MODE=Automated] World-entry dispatched APPLY_LOADOUT for 'TESTBOT2' (skills=1, supplemental=1, ...)
QUEUED ACTION for 'TESTBOT2': ApplyLoadout
DELIVERING ACTION to 'TESTBOT2': ApplyLoadout
```

Confirm at session start (paste verbatim):

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline 3b95f452..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

If `git log` shows commits between `3b95f452` and `origin/main`, the user
did manual work — read those commits before continuing. The Docker stack
must show `mangosd`, `realmd`, `pathfinding-service` healthy.

---

## Three course corrections recorded so far (don't re-discover them)

### Correction 1 (from v2) — the `BattlegroundEntryTests` "3s × 7" batch is a false positive

The audit prescribed batching the 3s/5s/10s delays in
`Battlegrounds/BattlegroundEntryTests.cs`. **Skip this entirely.** Every
site is the poll-pacing tail of an outer `while` loop that already checks
the success condition. On 80-bot BG fixtures, tightening to 500ms would
multiply `QueryAllSnapshotsAsync` load without saving wall time.

### Correction 2 (from v2) — the F-1 design doc puts the snapshot hook in the wrong file

`docs/statemanager_modes_design.md` § "Wiring into `StateManagerWorker`"
shows `OnSnapshotAsync` being called from
`StateManagerWorker.SnapshotProcessing.cs`. **That file processes external
API queries on port 8088, not bot snapshots.** Bot snapshots arrive in
[Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs](Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs).
The wiring landed there in commit `6eb85dcc` — see the new
`InvokeModeHandler` private method.

### Correction 3 (NEW in v4) — AutomatedModeHandler is loadout-only, NOT loadout-then-activity

The design doc § "AutomatedModeHandler" says:

> 1. If `character.Loadout != null`: enqueue `APPLY_LOADOUT(loadout)` once.
> 2. After `WoWActivitySnapshot.LoadoutStatus == Complete`: parse
>    `character.AssignedActivity` (e.g. `"Fishing[Ratchet]"`) and
>    dispatch the corresponding `StartXxx` action.

Step 2 is **already wired on the bot side** and does not belong in
StateManager. `StateManagerWorker.BotManagement.cs:131,297` already
forwards `CharacterSettings.AssignedActivity` to the bot process via the
`WWOW_ASSIGNED_ACTIVITY` environment variable. The bot's
`BotRunnerService.InitializeTaskSequence` (around line 1119) calls
`ActivityResolver.Resolve(context, _assignedActivity, _useGmCommands)` and
pushes the resolved task on top of `IdleTask` at world entry. So the
bot starts the activity itself, regardless of mode.

`AutomatedModeHandler.OnSnapshotAsync` is a no-op as a result. If a
future need arises to gate activity start on loadout completion, add it
here — but the current architecture already serializes correctly because
`HandleApplyLoadoutAction` pushes `LoadoutTask` on top of the activity
task in the bot's task stack, so loadout runs first, then the activity
resumes when LoadoutTask pops.

---

## Phase status — open items only

### Phase B — Snapshot poll hygiene (most easy wins extracted)

The remaining bare-delay census in `docs/test_cleanup_audit.md` § A1 is
mostly low-ROI now. The high-ROI items left (unchanged from v3):

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

Unchanged from v2/v3:
- **Mining take-turns.** Both bots launch `StartGatheringRoute` against the *same* node concurrently. Coordinate via a "claim/release" signal at the node level. Update `GatheringRouteSelection` to support two-bot convergence + graceful yield.
- **Herbalism follow.** FG gathers first node; on completion, FG enters a follow-mode (BotRunner-level `Follow` action against BG GUID). BG moves to a different node. Assert both bag entries land + FG ends within follow-distance of BG.
- **Generalize "FG follows BG"** as a BotRunner-side helper.

**Where to start:** `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`, `Exports/BotRunner/Tasks/GatheringTask.cs`, `GatheringRouteSelection.cs`. Claim/release signal needs a place to live — likely on `WoWActivitySnapshot` or a new `GatheringNodeClaim` message.

### Phase D — Real taxi/transport rides (PENDING)

Unchanged from v2/v3:
- **`TaxiTests`**: full ride (e.g. Org → Crossroads). Assert bot lands on destination flightmaster's grid square within `ExpectedRideSeconds + slack`.
- **`TransportTests`**: split into `Boat_Or_Zeppelin_FullRide` (board on arrival, assert `MOVEFLAG_ONTRANSPORT`, assert at destination) and `Elevator_FullRide` (Undercity West / Thunder Bluff). All snapshot-driven, no sleeps.

**Where to start:** `TransportTests.cs` and `TaxiTests.cs` already have Shodan staging in place; the action dispatch is the gap. Look for `MOVEFLAG_ONTRANSPORT` in the snapshot proto. The transport-arrival timer table likely lives in BotRunner gameobject data — grep `transport.*schedule` or look at `FgTaxiFrame.cs`.

### Phase E — LiveFixture consolidation (NEXT — F-1 unblocked it)

**This is your starting task.** F-1 step 3 is green, so Automated mode now
handles loadout dispatch autonomously. Tests that pre-stage with
`StageBotRunnerLoadoutAsync` against a Test-mode config can be migrated
in stages to load an Automated config + assert on snapshot milestones,
collapsing most of `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
(2 299 lines).

**Pilot recommended in v3:** `Tests/BotRunner.Tests/LiveValidation/EquipmentEquipTests.cs`.
Today it loads `Equipment.config.json` (Test mode) and stages each FG/BG
target via `StageBotRunnerLoadoutAsync`. The migration plan:
1. Make a sibling `Equipment.Automated.config.json` with `Mode=Automated`
   + per-character `Loadout` on EQUIPFG1 and EQUIPBG1 (port the spell
   IDs and item IDs that `StageBotRunnerLoadoutAsync` is currently
   issuing for the Equipment test).
2. Update `EquipmentEquipTests` to load that config and **wait for
   `LoadoutStatus == LoadoutReady`** before dispatching the test
   actions — instead of calling `StageBotRunnerLoadoutAsync`.
3. Verify on the live Docker stack.
4. If green, repeat for `UnequipItemTests`, `WandAttackTests`, etc.

**Target:** `LiveFixture.cs` ~1 200 lines vs the current ~9 000 in
partials. Most of the helpers in `LiveBotFixture.TestDirector.cs` become
unnecessary once Automated mode owns loadout.

**One known fragility to plan for:** the v3 attempt to reuse a brand-new
ONBOARDBG1 account in the Onboarding test failed because newly-created BG
accounts flap between `InWorld` and `CharacterSelect` during their first
stable login window, which prevents `LoadoutTask` from finishing. The
fix in v3 was to reuse TESTBOT2 (a stable, previously-used account).
**For Phase E migrations: keep using the existing per-category sibling
accounts (EQUIPFG1, EQUIPBG1, etc.) — don't introduce new account
prefixes.** If a test legitimately needs a fresh account for some
reason, add a "warm up" pass before the test relies on Automated mode.

### Phase F-1 — StateManager Automated mode

**COMPLETE.** All three steps shipped:

1. ~~F-1 step 1: schema enum + backward-compatible loader.~~ **DONE** (`1c530f36`).
2. ~~F-1 step 2: `IStateManagerModeHandler` + `TestModeHandler` (no-op).~~ **DONE** (`78bbbb36`).
3. ~~F-1 step 3a: `AutomatedModeHandler` + DI registration.~~ **DONE** (`491fa5c9`).
4. ~~F-1 step 3b: wire mode handler call sites in `CharacterStateSocketListener`.~~ **DONE** (`6eb85dcc`).
5. ~~F-1 step 3c: `Onboarding.config.json` + live test.~~ **DONE** (`3b95f452`).

### Phase F-2 / F-3 — OnDemand + production polish (PENDING)

Unchanged from v2/v3 description. After Phase E. See v3 § "Phase F-2/F-3"
for context. In short:
- **F-2:** `OnDemandActivitiesModeHandler` + Shodan whisper-command
  routing + WPF UI POST endpoint on port 8088.
- **F-3:** Audit fixture helpers that still apply loadout / dispatch
  activities and migrate the production-relevant ones to BotRunner-side
  `LoadoutTask` / `ActivityResolver`. Trim the fixture-side wrappers to
  thin adapters.

### Phase G — Final cleanup (PENDING)

After everything above. See v3 § "Phase G".

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

1. Run the verification block in § "What landed in v3's session" above.
2. Read in order: this handoff (top to bottom) → relevant sections of
   `docs/handoff_test_consolidation_v3.md` (only what's referenced) →
   `docs/statemanager_modes_design.md` (apply the three corrections
   above) → `CLAUDE.md` → `Tests/CLAUDE.md`.
3. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md` — load-bearing critical rules live there.
4. Start Phase E with `EquipmentEquipTests` migration as the pilot.
   Re-verify the current shape of `Equipment.config.json`,
   `StageBotRunnerLoadoutAsync` call sites in the test, and the
   `LoadoutSpec` proto fields against the *current* codebase before
   relying on this handoff's claims.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v5.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v4 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
