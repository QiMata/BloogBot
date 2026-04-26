# Handoff: Live-Test Consolidation, Concurrency, and StateManager "Automated" Mode

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, main is clean at the time of writing).
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section is new.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md` — current per-test inventory + Shodan rationale.
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md` — execution-mode legend.
> - `Services/WoWStateManager/Settings/Configs/*.json` — current per-category configs (Fishing, Equipment, Wand, MageTeleport, Gathering, Crafting, PetManagement, Economy, Loot, Navigation, NpcInteraction).
> - `docs/MONOREPO_OVERVIEW.md`, `docs/STATEMANAGER_PATTERNS.md`, `docs/BOTRUNNER_PATTERNS.md` for cross-repo context.

---

## Why this work exists (the user's explicit framing)

The Shodan-shaped LiveValidation suite is now **architecturally clean** —
Shodan is the production GM liaison (the in-world admin character that
lets human players communicate with WoWStateManager and request on-demand
activities), and behavior tests correctly dispatch `ActionType.*` against
dedicated test accounts (TESTBOT1/TESTBOT2 and per-category siblings).
Verified at commit `edda8b7d` on main.

**The next problem is that the tests themselves are bloated and slow.**
Specifically, the user called out:

1. **Redundant GM commands.** Bots repeatedly turn things off / reset
   things that don't need resetting. `EnsureCleanSlateAsync` plus
   `StageBotRunnerLoadoutAsync` plus per-test `.gm off` / `.reset items`
   / `.reset spells` are layering on top of each other. Many of these
   are no-ops in practice. They need to be evaluated and pruned.
2. **Long pauses / time gaps.** `Task.Delay`, `await Task.Delay(3000)`,
   poll loops with overly-generous progress timeouts, and serial
   "wait for X to settle" stages are wasting wall-clock time. Hunt
   them down.
3. **FG and BG run sequentially when they should run concurrently.**
   The current pattern in many migrated tests is "run BG scenario,
   restage, run FG scenario." For mining we want them to **take turns
   on the same node**. For herbalism we want FG to gather first then
   *follow* BG around as BG moves to its own node. Concurrent
   execution is the design target.
4. **Taxi/Transport coverage is shallow.** The current taxi test
   confirms `VisitFlightMaster` + `SelectTaxiNode` ACK and stops there.
   The transport tests check static snapshots near zeppelin/elevator
   pads. The user wants:
   - **Taxi**: complete the entire ride from boarding to landing.
   - **Transport**: full board → ride → unload for one zeppelin **or**
     boat (whichever is closest to docking next, picked at runtime),
     **plus** one elevator (Undercity or Thunder Bluff is fine).
5. **Too many fixtures.** `LiveBotFixture`,
   `LiveBotFixture.TestDirector.cs`, `LiveBotFixture.ShodanLoadout.cs`,
   `LiveBotFixture.Snapshots.cs`, `LiveBotFixture.BotChat.cs`,
   `LiveBotFixture.Diagnostics.cs`, `LiveBotFixture.Assertions.cs`,
   `LiveBotFixture.GmCommands.cs`, `LiveBotFixture.ServerManagement.cs`,
   `BgOnlyBotFixture`, `SingleBotFixture`, `CoordinatorFixtureBase`,
   `DungeonInstanceFixture`, `RfcBotFixture`, `CombatArenaFixture`,
   `CombatBgArenaFixture`, `CombatFgArenaFixture`,
   `AlteracValleyFixture`, `WailingCavernsFixture`, etc.
   Now that BotRunner can drive its own loadout / level / spell
   acquisition through StateManager-coordinated activities, the user
   wants this collapsed to **one** `LiveFixture` that:
   - Launches StateManager with the test's category config.
   - Verifies the stack is up.
   - Hands the test the snapshot/action surface.
   - Lets BotRunner take care of itself for prep, level, gear,
     spells, etc.
6. **StateManager modes need a structural extension.** This is the
   biggest piece and the reason fixture consolidation is possible:
   - **`Automated` mode**: StateManager spins up a fresh character
     and acts as the brain for that character's full progression —
     creation, leveling, gear, spell rotation choice, profession
     pickup, all the way to whatever final state the config asks
     for. The bots emulate a full player; tests assert on snapshot
     milestones.
   - **`OnDemandActivities` mode**: human players use the
     WoWStateManagerUI (or chat with Shodan) to request an activity.
     StateManager fires up the necessary bot grouping, dispatches
     actions, and tears down. Same code path as the test suite
     uses for category configs (`Gathering`, `Trading`, etc.).

> **Important user expectation:** the user wants you to keep iterating
> on this through several context compactions in a single session.
> When you near the end of your useful runway, your final job is to
> write **another handoff prompt** like this one for the *next*
> session to pick up cold. That handoff requirement is recursive —
> every prompt you produce must instruct the next agent to do the
> same.

---

## Hard rules (DO NOT violate, no exceptions)

These are repeated from `CLAUDE.md` and the project memory because they
get violated constantly:

- **R1 — No blind sequences/counters/timing hacks.** No `_attempts >= N`
  guards; gate on snapshot signal.
- **R2 — Poll snapshots, don't sleep.** No bare `Thread.Sleep`. No
  `await Task.Delay(ms)` without a surrounding predicate loop. Use
  `WaitForConditionAsync(predicate, timeout, description, pollMs,
  progressLogMs)` / `WaitForSnapshotConditionAsync`.
- **R3 — Fail fast.** Tests exit the moment the first wrong thing
  happens. Per-milestone tight timeouts (5s/15s/90s) over one giant
  test-level timeout. Every wait accepts an `onClientCrashCheck`.
- **R4 — No silent exception swallowing.** No `catch { }`. Log at
  warning with context.
- **R5 — Fixture owns lifecycle, StateManager owns coordination,
  BotRunner owns execution.** This will *change* slightly: the new
  `LiveFixture` should own LESS than today's, because BotRunner
  + StateManager handle their own loadout. Don't move coordination
  into the fixture.
- **R6 — GM/admin commands must be asserted.** Use
  `AssertGMCommandSucceededAsync` (or equivalent). Never discard a
  SOAP response — silent `FAULT:` returns hide regressions.
- **R7 — Cross-test state must be reset.** Every test calls
  `EnsureCleanSlateAsync` (or its replacement) at the start. After
  consolidation that helper may move to BotRunner-side prep; if so,
  ensure the test still gets a clean slate without violating R5.
- **R8 — x86 vs x64.** ForegroundBotRunner = x86. BackgroundBotRunner
  + StateManager + most tests = x64. Don't break the platform mix.
- **No background agents.** `run_in_background: true` is forbidden by
  user memory.
- **Single session only.** Auto-compaction handles context. Don't
  start a new session — keep going through compactions until you
  hand off via the recursive handoff prompt.
- **Commit and push frequently.** Every logical unit of work gets a
  commit, push immediately. Even partial progress is valuable.
- **Shodan is GM-liaison + setup-only.** Behavior actions never
  dispatch to Shodan. The fixture-level guard
  (`ResolveBotRunnerActionTargets`) throws if violated — don't
  weaken it.
- **No `.gobject add`, no synthetic node spawns**. Test against
  natural world state.
- **Live MaNGOS via Docker is always running** — don't waste tokens
  spinning it up. `docker ps` to confirm; never `tasklist` for
  server state.

---

## Phase plan (do these in order; commit after each phase)

### Phase A — Audit & inventory the bloat

A1. Grep every LiveValidation test for `Task.Delay`, `Thread.Sleep`,
    `.gm off`, `.gm on`, `.reset`, `EnsureCleanSlateAsync`, repeated
    GM teleports. Build a worksheet (`docs/test_cleanup_audit.md`)
    listing every occurrence with file:line and a one-line
    justification call: *necessary*, *redundant*, *replaceable with
    snapshot poll*, *should move to BotRunner*. Don't change code
    during this phase.

A2. Time the current LiveValidation suite end-to-end (or
    representative subset). Record per-test wall-clock in the
    audit. The goal of subsequent phases is to drive these down.

A3. Inventory the fixture sprawl: every `*.Fixture.cs`,
    `LiveBotFixture.*.cs` partial, every `[CollectionDefinition]`,
    every `[ClassFixture<>]`. Map each one to the runtime
    capability it provides. Identify which capabilities can move
    behind a single `LiveFixture` once BotRunner self-prep lands.
    Document overlap and the proposed merge target.

A4. Commit the audit doc. Push.

### Phase B — Snapshot poll hygiene + GM redundancy purge (test-side)

Goal: drop the easy weight without changing architecture.

B1. Remove duplicated `.gm off` calls. `EnsureCleanSlateAsync`
    already turns it off at the end; downstream calls are no-ops.

B2. Replace bare `Task.Delay(3000)` (and similar) with
    `WaitForSnapshotConditionAsync` keyed on the actual signal the
    test cares about. Keep tight per-milestone timeouts.

B3. Where `EnsureCleanSlateAsync` is called *and* a per-test
    `.reset items` runs, prove that one is redundant (likely the
    per-test reset — `.reset` happens implicitly through the
    loadout helper in many cases). Drop the redundant one.

B4. Per-test `BotTeleportAsync` calls that are immediately followed
    by a `StageBotRunnerAt*Async` (which itself teleports) are
    redundant. Drop the test-side teleport.

B5. Each cleanup gets its own commit and push (small commits — easier
    to revert if a deletion was load-bearing).

B6. Re-run the affected category bundles after each commit. Confirm
    no regressions.

### Phase C — Concurrent FG/BG execution

C1. **Mining take-turns**: rewrite `Mining_*_GatherCopperVein` so
    FG and BG launch their `StartGatheringRoute` actions
    concurrently against the *same* node. They must take turns
    (use BotRunner-side coordination through StateManager — emit
    a "claimed" / "released" signal on the node). Update
    `GatheringRouteSelection` if needed so two bots can converge
    on one node and yield gracefully when the other has it
    locked. Assertion: both bots gather successfully, neither
    "owns" the node for more than one channel cycle.

C2. **Herbalism follow-the-leader**: `Herbalism_FollowAndGather`
    test (rename existing). FG starts the route, finds + gathers
    the first herb. As soon as FG's `IsGatheringComplete`, FG
    enters a follow-mode (BotRunner-level "Follow" action against
    BG GUID). BG then moves to a different herb spawn and
    gathers. Assertion: both bag entries land + FG ends within
    follow-distance of BG.

C3. Generalize the "FG follows after primary action complete"
    pattern as a BotRunner-side helper. Apply to every test where
    "FG idle for topology parity" exists today (per
    SHODAN_MIGRATION_INVENTORY.md, that's most of the
    `Economy.config.json` tests). Tests should not have to opt
    into "FG idle" explicitly; the default should be "FG follows
    BG, takes turns when relevant, and asserts on its own
    snapshot at completion."

C4. Update `TEST_EXECUTION_MODES.md` to drop **most** of the
    "Shodan BG-action / FG idle" rows in favor of the new
    concurrent default.

C5. Each test rewrite is its own commit + push.

### Phase D — Real taxi + real transport

D1. **`TaxiTests.cs`**: replace `VisitFlightMaster +
    SelectTaxiNode` ACK-only assertions with a full ride. Pick
    a short Horde route (Orgrimmar → Crossroads or
    Thunder Bluff → Bloodhoof Village). Assertion: bot lands
    on the destination flightmaster's grid square within
    `ExpectedRideSeconds + slack`. Use snapshot poll for the
    landed-and-on-ground state, not a fixed-duration sleep.

D2. **`TransportTests.cs`** — split into two scenarios:
    - **`Boat_Or_Zeppelin_FullRide`**: at runtime, query active
      transport schedules (via the BotRunner gameobject snapshot
      / known transport-arrival timer table) to pick whichever
      vessel docks next. Bot stages near the dock, boards on
      arrival, asserts in-transit (`MOVEFLAG_ONTRANSPORT`),
      asserts at destination.
    - **`Elevator_FullRide`**: pick Undercity West or Thunder
      Bluff (whichever you've already got working transport
      data for; both are acceptable per user). Bot rides
      down, asserts ground-level position at bottom.

D3. Snapshot-driven everywhere. No sleeps. Per-milestone tight
    timeouts.

D4. Commit, push, re-run.

### Phase E — `LiveFixture` consolidation (the big one)

> **Pre-req:** Phase F-1 (StateManager `Automated` mode) must work
> end-to-end at least for one config (Equipment is a good
> candidate — it's the simplest). Without that, BotRunner can't
> drive its own loadout and the consolidation is premature.

E1. Design a single `LiveFixture` (replacing today's
    `LiveBotFixture` + `BgOnlyBotFixture` + `SingleBotFixture` +
    the per-collection variants). Surface area:
    - `EnsureSettingsAsync(configPath)` — point StateManager at the
      right config.
    - `IsReady` / `FailureReason` — same as today.
    - `ResolveBotRunnerActionTargets()` — keep the Shodan-rejecting
      resolver.
    - `SendActionAsync`, `RefreshSnapshotsAsync`, `GetSnapshotAsync`,
      `WaitForSnapshotConditionAsync` — surface-level snapshot/action
      API.
    - `Shodan*` helpers for live-only setup ops that genuinely need
      GM targeting (pool respawns, gobject respawns).
    - **Removed**: per-bot loadout/level/spell helpers (BotRunner
      handles these via StateManager coordination once F-1 lands).

E2. Migrate one test class as a pilot (recommend `EquipmentEquipTests`
    — smallest, already Shodan-shaped, BotRunner self-prep is most
    advanced here). Confirm green. Commit. Push.

E3. Migrate the rest in dependency order. Each migration is its own
    commit. Test bundles run after each commit.

E4. When all live tests use the new fixture, delete the
    obsoleted partials, BgOnlyBotFixture, SingleBotFixture, etc.
    Run full suite. Commit. Push.

### Phase F — StateManager `Automated` and `OnDemandActivities` modes

This is parallel to E and unlocks E. Do F-1 first, then E can
start.

F-1. **`Automated` mode skeleton**: extend `StateManagerSettings.json`
     with a `Mode: "Automated" | "OnDemandActivities" | "Test"`
     enum. `Automated` instructs StateManager to:
     - Create the character if missing (via existing SOAP
       `.character create` or DB seed if SOAP doesn't expose it
       directly).
     - Apply per-config loadout/level/spells/skills using the
       same logic Shodan uses today (refactor
       `StageBotRunnerLoadoutAsync` into a StateManager-side
       LoadoutTask consumed by both modes).
     - Run the configured activity (e.g.
       `StartGatheringRoute`, `JoinBattleground`,
       `TravelToTaxiNode → Ride → DismountAtDestination`).
     - Decide next-step actions when the activity is
       open-ended.

F-2. **`OnDemandActivities` mode**: a UI / chat path that lets
     human players (in-game, talking to Shodan, OR via the WPF
     UI) request an activity by name. StateManager pulls the
     matching config off disk (or its in-memory registry),
     reuses the same LoadoutTask + activity dispatch, and runs
     it on the available bot pool. Shodan is the in-world
     liaison — when a player says "/whisper Shodan
     start_mining_run", Shodan ACKs in chat and StateManager
     fires the bots.

F-3. **`Test` mode**: roughly today's behavior — StateManager
     reacts to `SendActionAsync` from tests but doesn't drive
     the activity itself. Keep this as the regression mode while
     the new modes mature.

F-4. Each mode lands behind a feature flag in
     `StateManagerSettings.json` so you can revert without code
     changes. Commit per-mode. Push.

### Phase G — Final cleanup

G-1. Delete obsoleted per-category fixtures (`RfcBotFixture`,
     `CoordinatorFixtureBase`, etc.) once their tests work
     under the unified fixture. Keep activity-owned fixtures
     (`AlteracValleyFixture`, `WailingCavernsFixture`,
     `RaidCoordinationTests` setup) — those represent real
     production activity orchestration and shouldn't be
     collapsed.

G-2. Update all CLAUDE.md / Tests/CLAUDE.md /
     SHODAN_MIGRATION_INVENTORY.md / TEST_EXECUTION_MODES.md to
     reflect the new shape. The Shodan section in the root
     CLAUDE.md should now also describe the
     `OnDemandActivities` mode.

G-3. Final full-suite run. Commit. Push. Verify origin/main is
     clean.

---

## Working agreements with the user (carry over)

- The user prefers concise responses. State decisions and changes;
  don't narrate deliberation.
- The user expects you to keep iterating without asking permission
  for each phase. Only ask if you genuinely need a decision they
  haven't already given (e.g. "this rewrite breaks an
  unrelated-looking activity test — proceed or pause?").
- For **risky** actions (force push, deleting branches, mass file
  deletion, schema migrations), always confirm. Code edits and
  small commits are local/reversible — proceed.
- Integration tests run against the always-live Docker MaNGOS
  stack. Confirm `docker ps` once at session start; don't keep
  re-checking.

---

## When you near context exhaustion

You **must** write the next handoff prompt before you stop. Save it
at `docs/handoff_<short-topic-name>.md`. Include:

1. A concise restatement of the goal (this file's "Why this work
   exists" section, updated for *current* state).
2. The current commit hash on main (`git rev-parse origin/main`)
   and a short summary of what's landed since this prompt was
   written.
3. The **remaining** phases from the plan above, with completed
   work struck through or moved to a "Done" appendix.
4. Any new blockers, surprises, or design pivots discovered.
5. A repeat of all the **Hard rules** above.
6. A repeat of this **"When you near context exhaustion"**
   instruction so the *next* agent does the same.

The user will copy/paste your handoff into a fresh session. Make
it self-contained — assume the next agent has zero memory of this
session.

---

## Starter checklist for the receiving agent

When you sit down with this prompt:

1. `git fetch origin && git status && git log --oneline -10` —
   confirm you're on a clean main matching origin.
2. `docker ps` — confirm the VMaNGOS stack is up. (`vmangos-realmd`,
   `vmangos-mangosd` containers should be Up.)
3. Read `CLAUDE.md`, `Tests/CLAUDE.md`, the Shodan inventory + exec
   modes docs, and this handoff in full before touching code.
4. Skim the auto-memory at `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md` —
   load-bearing critical rules live there.
5. Begin **Phase A**. Don't skip ahead — the audit is what
   prevents Phase B from deleting a load-bearing reset.
6. Commit and push after each phase. Don't accumulate.
7. When you near context exhaustion, write the next handoff
   prompt under `docs/handoff_*.md` per the rules above and
   tell the user where to find it.
