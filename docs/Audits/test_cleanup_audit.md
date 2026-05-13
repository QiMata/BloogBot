# LiveValidation Test Cleanup Audit (Phase A)

> Authored: 2026-04-26 by the live-test consolidation handoff.
> Companion: `docs/handoff_test_consolidation.md`,
> `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`.
>
> **Phase A is read-only.** This document records what is here today, what
> is redundant, and what should change in Phase B+. No code changed in this
> phase; that is the next phase's job, gated on this audit.

---

## TL;DR

1. **Per-test GM redundancy is already very low.** `.gm off` / `.gm on` /
   `.reset *` calls outside fixture code are essentially gone — the
   Shodan migration finished the cleanup. Phase B's "remove duplicated
   `.gm off` calls" is mostly a no-op against the test bodies and instead
   becomes "audit the fixture-side calls for double-toggling."
2. **The real bloat is bare `Task.Delay` in two places:**
   - Long settle delays in test bodies (5–130s in worst cases).
   - Many short delays inside the **fixture partials and
     `CoordinatorFixtureBase`**. That fixture alone has 23+ delays.
3. **Fixture sprawl is real.** The seven `LiveBotFixture.*` partials
   total ~9 000 lines, and `CoordinatorFixtureBase.cs` adds another
   1 235. Most tests can be served by a much thinner surface once
   StateManager `Automated` mode lands (Phase F-1).
4. **Concurrent FG/BG is partially in place** (e.g., `EquipmentEquipTests`
   already runs both targets via `Task.WhenAll`). Other tests still
   serialize FG-then-BG by Shodan-staging design (e.g.,
   `FishingProfessionTests`). Mining/herbalism need new
   coordination semantics — see Phase C.

---

## A1. GM redundancy census

### `.gm off` / `.gm on` (5 hits total)

| File:Line | Context | Verdict |
|---|---|---|
| `LiveBotFixture.Assertions.cs:318` | `EnsureCleanSlateAsync` final step | **Necessary.** This is the canonical clean-slate `.gm off`. Source of truth. |
| `CoordinatorFixtureBase.cs:1139` | After `.reset level` re-leveling sequence | **Necessary.** Re-level path uses temporary `.gm on`; this restores. |
| `Battlegrounds/AlteracValleyFixture.cs:169` | After AV roster build | **Necessary.** AV-specific cleanup; not test-body bloat. |
| `RfcBotFixture.cs:92` | After RFC roster build | **Necessary.** RFC-specific cleanup. |
| `LiveBotFixture.Assertions.cs:316` | Comment line, not a call | n/a |

**Verdict:** No per-test `.gm off`/`.gm on` to delete. **Phase B B1 is a
no-op against test bodies** — already done. Action: ensure no future
test bodies regress; consider an analyzer/test fixture-guard.

### `.reset *` (subset)

| File:Line | What | Verdict |
|---|---|---|
| `LiveBotFixture.GmCommands.cs:259` | `.reset spells` helper — public API | Used only by RfcBotFixture. Could be dropped if RFC moves to BotRunner-side prep. |
| `LiveBotFixture.GmCommands.cs:276` | `.reset items {char}` helper — public API | Used by ShodanLoadout. Necessary. |
| `LiveBotFixture.ShodanLoadout.cs:52,60` | Shodan's own bag wipe | **Necessary.** Different from test-target reset. |
| `RfcBotFixture.cs:66-68` | spells/talents/items reset on RFC roster | **Move to BotRunner-side prep** when activity-owned loadout migrates (Phase F-1 enables this). |
| `CoordinatorFixtureBase.cs:1100` | `.reset level` for over-level recovery | **Move to BotRunner-side level reset** when Automated mode lands. |
| `LiveBotFixture.BotChat.cs:249,250` (comments) | doc text | n/a |
| `CoordinatorStrictCountTests.cs:648` | Asserts `.reset` is **not** in chat — guard test | **Keep.** Anti-regression. |

**Verdict:** No per-test `.reset` calls in test bodies. The remaining
calls are in fixture/activity code and are correct in current
architecture. They become redundant once StateManager `Automated` mode
runs the loadout/level/reset flow inside BotRunner (Phase F-1).

### `EnsureCleanSlateAsync` call sites (60 hits across 24 files)

Most calls fall into two classes:

1. **Test-body calls before staging** (legitimate per `Tests/CLAUDE.md`).
   Examples: `BasicLoopTests`, `ChannelTests`, `CharacterLifecycleTests`,
   `ScalabilityTests`, `RaidFormationTests`, `LoadTestMilestoneTests`.
   These are the canonical "guard, setup, action, assert" pattern.
2. **Fixture-internal calls inside every `StageBotRunner*Async`
   helper** (at least 14 sites in `LiveBotFixture.TestDirector.cs`).
   Each staging helper opens with `await EnsureCleanSlateAsync(...)`
   as a defensive precaution.

**Cost of one `EnsureCleanSlateAsync` call:**
- `WaitForSnapshotConditionAsync` snapshot-hydrate, 8s timeout, ~500ms typical.
- Optional revive path: up to +10s.
- Optional `DisbandGroup`: +250ms hard delay.
- `LeaveGroup` + 750ms hard delay + 4s waiter timeout.
- `BotTeleportAsync` to safe zone + `WaitForZStabilizationAsync(2000ms)`.
- `.gm off` chat command.

**Best case ≈ 3–4s. Worst case ≈ 15s.** This call is invoked **twice
per test** (FG+BG) in dual-target tests, plus once **inside each
StageBotRunner helper invoked**. A test that stages both bots at a
location pays for 4× `EnsureCleanSlateAsync` rounds — that's 12–16s
of best-case overhead before the action even dispatches.

**Verdict (Phase B):**
- **B-keep:** Test-body call at the start of every test (R7 mandates it).
- **B-prune:** Fixture-internal calls inside `StageBotRunner*Async`.
  When the test body has just called `EnsureCleanSlateAsync(target,
  label)`, the staging helper's clean-slate is a no-op cost. Make it
  opt-in (`bool ensureClean = false`) and have the test body set
  `true` only when it skipped its own cleanup. Estimated savings: 6–10s
  per test that stages both bots.
- **B-rework:** When Phase F-1 lands `Automated` mode, BotRunner can
  emit a "ready" snapshot signal and the test can poll for it instead
  of running `EnsureCleanSlateAsync` at all on a freshly-launched
  config.

---

## A1. Bare delay census

Total `Task.Delay` / `Thread.Sleep` hits in `LiveValidation/`: **246**
across **70 files**.

### Legitimate poll-loop intervals (KEEP)

These are inside a `while (deadline)` loop checking a snapshot
predicate. They satisfy R2.

| File:Line | Body |
|---|---|
| `LiveBotFixture.BotChat.cs:536` | `await Task.Delay(pollIntervalMs)` |
| `LiveBotFixture.ServerManagement.cs:1131` | `pollIntervalMs` |
| `LiveBotFixture.Snapshots.cs:246` | `pollIntervalMs` |
| `MovementParityTests.cs:404, 779` | `PollIntervalMs` |
| `NavigationTests.cs:173` | `pollMs` |
| `Scenarios/TestScenarioRunner.cs:229` | `observe.PollIntervalMs` |
| `Dungeons/DungeonEntryTests.cs:154`, `Dungeons/WailingCavernsTests.cs:163`, `RagefireChasmTests.cs:160` | `pollInterval` |
| `CoordinatorFixtureBase.cs:263` | `pollInterval` |

**Verdict:** keep — these are how `WaitForSnapshotConditionAsync`-style
helpers actually wait. (Many of them already use the helper; the rest
are bespoke loops that should migrate to the helper for consistent
progress-logging.)

### Worst offenders — long bare delays in test bodies (PRUNE FIRST)

Listed in descending wall-clock order; these alone account for ~5
minutes of forced wall-clock waste per full suite run.

| Wall | File:Line | Context | Verdict |
|---|---|---|---|
| 130s | `Battlegrounds/BattlegroundEntryTests.cs:123` | Wait for queue match | **Replace with chat/system-message poll** (`CHAT_MSG_BG_SYSTEM` / battle queue update marker). Tight 5s poll. |
| 95s | `Battlegrounds/WsgObjectiveTests.cs:142` | WSG prep window | **Replace** with snapshot signal `BG_PREPARATION_OVER` or chat-marker poll. |
| 12s | `QuestObjectiveTests.cs:90` | Wait after dispatching `StartMeleeAttack` | **Replace** with `WaitForSnapshotConditionAsync(target dies, 12s)`. |
| 5s | `CornerNavigationTests.cs:75, 127` | post-stage settle | **Replace** with `WaitForBotIdleAsync` (already exists in fixture) — 1s typical. |
| 5s | `TileBoundaryCrossingTests.cs:95, 185` | post-stage settle | Same. |
| 5s | `Dungeons/SummoningStoneTests.cs:90` | summon arc wait | **Replace** with `WaitForSummonCompleteAsync` predicate. |
| 5s × 3 + 3s | `TransportTests.cs:49, 102, 143, 179` | snapshot-only checks | **Replace** with `WaitForGameObjectVisibleAsync` predicate. The current snapshot is taken once after a 5s sleep — moving to predicate gives <1s typical. |
| 3s × 7 | `Battlegrounds/BattlegroundEntryTests.cs:267, 629, 957, 1018` etc | various waits | Per-marker poll. |
| 3s | `ChannelTests.cs:58` | wait for `.join` channel chat to land | Replace with `WaitForChatMessageAsync`. |
| 3s | `DualClientParityTests.cs:206` | post-stage settle | Replace with quiesce. |
| 3s | `GuildOperationTests.cs:46` | wait for guild create chat | Replace with chat-msg poll. |
| 3s | `Raids/RaidCoordinationTests.cs:47, 93` | raid roster settle | Replace with `WaitForRaidMembershipAsync`. |
| 3s | `TaxiTests.cs:60` | wait for flight master selection | Replace with snapshot poll on `ActiveTaxiNode`. |
| 2.5s × 2 | `NpcInteractionTests.cs:191, 314` | settle after vendor visit | Replace with vendor-frame predicate. |

**Estimated wall-clock savings if all of the above are pruned to
predicate polls with reasonable typical-case latency: ~4 minutes per
full suite run.**

### Mid-tier delays (PRUNE BUT LOWER PRIORITY)

200–1500ms scattered across:

| Files | Pattern | Verdict |
|---|---|---|
| `EquipmentEquipTests.cs:132,165`, `UnequipItemTests.cs:136,156,181,194`, `BgInteractionTests.cs:242`, `MailParityTests.cs:197`, `MailSystemTests.cs:198`, `EconomyInteractionTests.cs:155`, `BuffAndConsumableTests.cs:221,248`, `ConsumableUsageTests.cs:142,163`, `SpellCastOnTargetTests.cs:203`, `WandAttackTests.cs:177,282,298,314` | 200–500ms intra-poll inside ad-hoc `while` loops | These are the "missing helper" pattern: each is the inner sleep of a per-test polling loop. Migrate the loop to `WaitForSnapshotConditionAsync` and the delay disappears. |
| `MountEnvironmentTests.cs:140`, `GossipQuestTests.cs:59,96`, `Raids/RaidCoordinationTests.cs:160,186,193,201,207`, `RaidFormationTests.cs:61,70,88,102,116`, `TaxiTransportParityTests.cs:80,177,259`, `MovementParityTests.cs:332,360,369,523,530,748,832,838`, `MovementSpeedTests.cs:93`, `NavigationTests.cs:231` | 500–2000ms post-action settles | Replace with a single quiesce predicate (`task done` or `position settled`). |
| `Scenarios/TestScenarioRunner.cs:98,179,216` | scenario tick padding | Drop — the scenario runner has its own per-tick observe. |
| `LootCorpseTests.cs:108,149,236`, `DeathCorpseRunTests.cs:208,364,399`, `PetManagementTests.cs:51,70`, `FishingProfessionTests.cs:161` | 500ms–5s "wait for X to happen" | Predicate poll. |

### Fixture-side delays (PRUNE — large surface)

| File | Hits | Notes |
|---|---|---|
| `CoordinatorFixtureBase.cs` | 23 | Most are old-style `await Task.Delay(N)` between manual coordinator setup steps. Many of the predicate-style `Wait*` helpers used elsewhere already exist; this fixture pre-dates them. **Phase B target.** |
| `LiveBotFixture.TestDirector.cs` | 9 | Most are 300–500ms. Two are 5000ms (`L1951`, `L2001`) — settle after Shodan respawn loops. |
| `LiveBotFixture.BotChat.cs` | 10 | Mostly 100-300ms after sending bot chat. Most are inside loops that send N commands. Could batch. |
| `LiveBotFixture.Assertions.cs` | 6 | Inside `EnsureCleanSlateAsync` and group-state helpers. Already noted in the EnsureCleanSlateAsync section. |
| `LiveBotFixture.ServerManagement.cs` | 6 | `await Task.Delay(10000)` (L707) for cold-start + 5s/3s settles for full-init. Used once per session — keep. Tag with comment that they are session-scoped, not per-test. |
| `LiveBotFixture.Snapshots.cs` | 6 | Mix of poll intervals and 1000ms settles. |
| `LiveBotFixture.cs` | 8 | World-init / connection retry loops. Lower priority. |
| `RfcBotFixture.cs` | 5 | RFC roster build. 2× 2000ms + 3000ms + 1000ms + 200ms. **Replace with predicate polls** when the fixture moves to BotRunner-side prep. |
| `Combat*ArenaFixture.cs` (3 files) | 9 total | Arena ramp-up. Each 400/250/500. Replace with predicate. |
| `DungeonInstanceFixture.cs` | 2 | 250ms inside loops. Probably correct as poll intervals; verify they're inside `while`. |
| `Battlegrounds/AlteracValleyFixture.cs` | 3 | 1000/90/800. Replace with predicate where possible. |

**Coordinator estimate:** trimming `CoordinatorFixtureBase.cs` from
23 bare delays to predicate polls would save **8–15s per coordinator
test** (`CoordinatorStrictCountTests`, `RaidFormationTests`,
`GroupFormationTests`, etc.). Largest single ROI in the fixture
layer.

---

## A2. Per-test wall-clock baseline

> **TODO — re-run before Phase B and capture per-test timing into
> this section.** The audit shouldn't block on a full live run, but the
> Phase B "did we actually save time" comparison requires this baseline.
>
> Suggested representative subset (these cover the major patterns):
>
> 1. `EquipmentEquipTests.EquipItem_AddWeaponAndEquip_AppearsInEquipmentSlot`
>    — pilot, parallel FG/BG.
> 2. `FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool`
>    — long-running serial dual-bot reference (~6 minutes ceiling).
> 3. `GatheringProfessionTests.*` — concurrent target candidate (Phase C).
> 4. `TransportTests.*` — heavy fixed-delay test (Phase D rewrite target).
> 5. `TaxiTests.*` — second Phase D target.
> 6. `BattlegroundEntryTests.Queue_*` — 130s+ delay outlier.
> 7. `EconomyInteractionTests.*` — Shodan-shaped Economy.config.json
>    representative.
> 8. `CoordinatorStrictCountTests.*` — `CoordinatorFixtureBase` pressure.
>
> Capture after Phase B with the same subset to compute savings.

---

## A3. Fixture inventory & merge plan

### Top-level live fixtures

| File | Lines | Purpose | Merge target |
|---|---|---|---|
| `LiveBotFixture.cs` | 1 789 | Core lifecycle, ready/failure, account roster | **NEW LiveFixture core.** |
| `LiveBotFixture.TestDirector.cs` | 2 299 | Shodan staging helpers (`StageBotRunner*Async`, `ResolveBotRunnerActionTargets`) | **Move into BotRunner-side `LoadoutTask`** (F-1). The fixture should not own this much logic. |
| `LiveBotFixture.ServerManagement.cs` | 2 442 | StateManager process lifecycle + GM command dispatch | **NEW LiveFixture core** (server lifecycle is fixture territory). |
| `LiveBotFixture.Assertions.cs` | 906 | `EnsureCleanSlateAsync` + IsStrictAlive + safe-zone consts | **NEW LiveFixture core**, but trim once F-1 emits a "ready" snapshot. |
| `LiveBotFixture.BotChat.cs` | 765 | `SendGmChatCommand*`, bot-chat poll helpers | **NEW LiveFixture core**, surface the small hot path; private rest. |
| `LiveBotFixture.Snapshots.cs` | 453 | `RefreshSnapshotsAsync`, `GetSnapshotAsync`, `WaitForSnapshotConditionAsync` | **NEW LiveFixture core.** |
| `LiveBotFixture.GmCommands.cs` | 365 | `BotLearnSpellAsync`, `BotSetSkillAsync`, `BotAddItemAsync`, `BotTeleportAsync` | **Move to BotRunner LoadoutTask** (F-1). |
| `LiveBotFixture.ShodanLoadout.cs` | 135 | Shodan's own bag setup | **Keep** — Shodan is a real production character; its prep stays a fixture concern. |
| `LiveBotFixture.Diagnostics.cs` | 89 | Snapshot diagnostics dump | **NEW LiveFixture core** (small, harmless). |
| `BgOnlyBotFixture.cs` | 20 | Thin wrapper that disables FG | **Delete** — replaced by config (`BgOnly.config.json`) + `LiveFixture`. |
| `SingleBotFixture.cs` | 11 | Thin wrapper for a single bot | **Delete** — replaced by config + `LiveFixture`. |
| `CoordinatorFixtureBase.cs` | 1 235 | Coordinator orchestration, roster build, scaling tests | **Move to its own targeted fixture** for `Coordinator*` / `Scalability*` tests. Don't merge into `LiveFixture` (different concerns), but trim its bare delays. |
| `DungeonInstanceFixture.cs` | 299 | Dungeon entry orchestration | **Keep**, activity-owned. |
| `RfcBotFixture.cs` | 101 | RFC-specific roster setup | **Move to BotRunner activity flow** when F-1 enables, then delete. |
| `CombatArenaFixture.cs` | 151 | Arena coordination | **Keep**, activity-owned. |
| `CombatBgArenaFixture.cs` | 130 | BG-only arena coordination | **Keep**, activity-owned. |
| `CombatFgArenaFixture.cs` | 130 | FG-only arena coordination | **Keep**, activity-owned. |
| `Battlegrounds/AlteracValleyFixture.cs` | (~280) | AV roster | **Keep**, activity-owned. |
| `Battlegrounds/AlteracValleyObjectiveFixture.cs` | (~) | AV objective tracking | **Keep**, activity-owned. |
| `Battlegrounds/ArathiBasinFixture.cs` | (~) | AB | **Keep**, activity-owned. |
| `Battlegrounds/WarsongGulchFixture.cs` | (~) | WSG | **Keep**, activity-owned. |
| `Dungeons/WailingCavernsFixture.cs` | (~) | WC | **Keep**, activity-owned. |

### Test collections (markers — no code)

| File | Purpose | Merge target |
|---|---|---|
| `LiveValidationCollection.cs` | `[CollectionDefinition]` for `LiveBotFixture` | Keep, rename to `LiveCollection`. |
| `BgOnlyValidationCollection.cs` | For `BgOnlyBotFixture` | **Delete** with `BgOnlyBotFixture`. |
| `SingleBotValidationCollection.cs` | For `SingleBotFixture` | **Delete** with `SingleBotFixture`. |
| `RfcValidationCollection.cs` | For `RfcBotFixture` | **Delete** when RFC moves to F-1. |
| `Battlegrounds/WarsongGulchCollection.cs`, `WarsongGulchObjectiveCollection.cs`, `Dungeons/DungeonCollections.cs`, `Raids/RaidCollections.cs` | Activity-owned | **Keep.** |

### Proposed final shape (post-Phase E + F-1)

```
Tests/BotRunner.Tests/LiveValidation/
├── LiveFixture.cs                     ← new, replaces 7 partials + BgOnly + SingleBot
├── LiveCollection.cs                  ← renamed LiveValidationCollection
├── CoordinatorFixtureBase.cs          ← unchanged (different concern, but delays trimmed)
├── Battlegrounds/                     ← unchanged
├── Dungeons/                          ← unchanged
├── Raids/                             ← unchanged
├── Scenarios/                         ← unchanged
├── docs/                              ← unchanged
└── (everything else)                  ← test classes, no fixture changes
```

`LiveFixture.cs` size estimate: **~1 200 lines** down from ~9 000
across the partials (excluding TestDirector/GmCommands which move to
BotRunner).

---

## A3. Capabilities by current owner — what moves and what stays

| Capability | Current owner | Phase E target | Phase F-1 target |
|---|---|---|---|
| StateManager process launch | `LiveBotFixture.ServerManagement.cs` | `LiveFixture` | (unchanged) |
| `EnsureSettingsAsync(configPath)` | `LiveBotFixture.cs` | `LiveFixture` | unchanged, but the new "Automated" mode self-prep means tests can call this and dispatch the first action immediately |
| `IsReady` / `FailureReason` | `LiveBotFixture.cs` | `LiveFixture` | unchanged |
| `ResolveBotRunnerActionTargets()` | `LiveBotFixture.TestDirector.cs` | `LiveFixture` | unchanged |
| `SendActionAsync` / `RefreshSnapshotsAsync` / `GetSnapshotAsync` | `LiveBotFixture.Snapshots.cs` | `LiveFixture` | unchanged |
| `WaitForSnapshotConditionAsync` | `LiveBotFixture.Snapshots.cs` | `LiveFixture` | unchanged |
| `SendGmChatCommandAsync` (Shodan only) | `LiveBotFixture.BotChat.cs` | `LiveFixture` (Shodan-scope only) | unchanged |
| `EnsureCleanSlateAsync` | `LiveBotFixture.Assertions.cs` | `LiveFixture` (kept) | replaced by snapshot-ready signal once Automated mode emits one |
| `BotLearnSpellAsync` / `BotSetSkillAsync` / `BotAddItemAsync` / `BotTeleportAsync` | `LiveBotFixture.GmCommands.cs` | **moved out** | **BotRunner LoadoutTask** consumed by all three modes |
| `StageBotRunnerLoadoutAsync` / `StageBotRunnerAt*Async` | `LiveBotFixture.TestDirector.cs` | **moved out** | **BotRunner LoadoutTask** + per-config staging |
| Pool refresh / gobject respawn helpers | `LiveBotFixture.TestDirector.cs` | `LiveFixture` (production GM-liaison surface lives here) | unchanged — Shodan owns this in production too |
| Shodan admin loadout | `LiveBotFixture.ShodanLoadout.cs` | `LiveFixture` | unchanged |
| RFC / dungeon / battleground / raid setup | per-fixture | per-fixture (unchanged) | gradually migrated to BotRunner activity flows |

---

## A4. Phase B savings estimate

| Bucket | Hits | Estimated saving |
|---|---|---|
| Long bare delays (≥3s) in test bodies | ~25 | 4 min total per full suite run |
| Mid-tier delays (200ms–2s) folded into predicate loops | ~80 | 1.5 min total |
| `CoordinatorFixtureBase` delay → predicate | 23 | 0.5 min total per coordinator-test class |
| Redundant `EnsureCleanSlateAsync` inside `StageBotRunner*` helpers | 14 | 1.5–2 min total |
| **Aggregate** | | **~7–8 min per full suite run** |

For the live-validation suite specifically, that's ~25–30% wall-clock
reduction depending on which subset is being run.

---

## Phase B execution order (ready when this audit is reviewed)

1. **B1 (no-op):** Confirm no `.gm off` / `.gm on` / `.reset *` regressions in
   test bodies. If a future test introduces one, refuse it.
2. **B2:** Replace the 25 long bare delays in test bodies (table above) one
   commit at a time. Order: longest first (BattlegroundEntry queue → WSG
   prep → QuestObjective → Transport → CornerNavigation → TileBoundary).
3. **B3:** Fold the 80-ish mid-tier intra-loop delays into
   `WaitForSnapshotConditionAsync` calls. Group by test class.
4. **B4:** Trim the 23 bare delays in `CoordinatorFixtureBase.cs` by
   migrating to existing predicate helpers.
5. **B5:** Make `StageBotRunner*Async` helpers' internal
   `EnsureCleanSlateAsync` opt-in (`bool ensureClean = false`).
   Default off; only the test itself opts in. Audit each call site.

After each step, re-run the representative subset and capture per-test
wall-clock. Update this doc's A2 section.

---

## Open questions / next-session notes

1. **Per-test wall-clock baseline (A2):** intentionally left blank.
   Phase B's first action should be a single live-suite run capturing
   per-test timings before any pruning.
2. **`Task.Delay` inside polling helpers** is not a regression — those
   are `pollIntervalMs` arguments and satisfy R2. Don't blindly grep
   them out.
3. **Don't move `StageBotRunner*Async` to BotRunner before Phase F-1.**
   Until BotRunner emits a "config-loadout-complete" snapshot signal,
   the test still has to drive the loadout from the fixture. Move
   together with the F-1 mode skeleton.
