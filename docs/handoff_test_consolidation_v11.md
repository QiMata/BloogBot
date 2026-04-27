# Handoff v11: Live-Test Consolidation, Concurrency, and StateManager Modes

> **Audience:** the next Claude Code session that picks up this work cold.
> **Repo:** `e:\repos\Westworld of Warcraft` (already a git repo, branch `main`).
> **Picks up from:** [`docs/handoff_test_consolidation_v10.md`](handoff_test_consolidation_v10.md). v11 supersedes v10
> on the FG LoadoutTask verification (re-stack fix is verified; a
> separate downstream `.learn`-on-FG gap is now diagnosed end-to-end).
> **Companion files you must read before touching anything:**
> - `CLAUDE.md` (root) — load-bearing rules, Shodan section.
> - `Tests/CLAUDE.md` — LiveValidation test pattern.
> - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
> - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
> - `docs/statemanager_modes_design.md` — Phase F design doc; the five
>   corrections from v4–v10 still apply.
> - `docs/handoff_test_consolidation_v10.md` — the previous handoff.
>   **Don't reread the whole thing — v11 supersedes it on the FG
>   LoadoutTask verification.**

---

## Why this work exists

The Shodan-shaped LiveValidation suite is architecturally clean. The
remaining problems are bloat, sequential execution, shallow taxi/transport
coverage, and StateManager's lack of a first-class `Automated` mode.
Phases B (delay cleanup), C (concurrent FG/BG), D (real taxi/transport
rides), F (modes), and E (LiveFixture consolidation, gated on F-1 step 3)
tackle these in order.

**Phases B (high-ROI), D (Taxi + Elevator pilots), F-1 are COMPLETE.**
Phase E broader migration is in progress (3 of N siblings done, BG-only).
The v10 InitializeTaskSequence re-stack fix is **verified** end-to-end
on FG, but a *separate* FG-specific gap on `.learn <spellId>` blocks
the FG path of every Phase E Automated migration. Phases C, F-2/F-3, G
are still open.

---

## What landed since v10 (`b0c92f80`)

Two commits this session:

| Commit | Phase | Summary |
|---|---|---|
| `9d67c1aa` | E (diag) | `diag(fg): wire Serilog static logger to file sink for LoadoutTask traces` — adds `Serilog 3.1.1` + `Serilog.Sinks.File 5.0.0` to the FG csproj and `ConfigureSerilogStaticLogger()` in [Services/ForegroundBotRunner/Program.cs](../Services/ForegroundBotRunner/Program.cs) (mirrors the BG pattern, file-only). Wires `Log.Logger` to `<AppContext.BaseDirectory>/WWoWLogs/fg_<account>.log`. Honors `WWOW_LOG_LEVEL` / `WWOW_FILE_LOG_LEVEL` / `WWOW_DISABLE_FILE_LOGS`. Without this, every `Serilog.Log.*` call in `BotRunnerService` and `LoadoutTask` was silently dropped on FG. |
| `d0d99878` | E (diag) | `diag(loadout): trace LoadoutTask plan + per-step dispatches, add WWOW_TEST_BOT_LOG_LEVEL escape hatch` — adds Information-level traces in `LoadoutTask.Update` (plan-built, per-step TryExecute outcome, step-satisfied advance) and in `LearnSpellStep.TryExecute` (no-Player retry, KnowsSpell short-circuit, full dispatch with `knownSpellsCount` + `ackFired`). Also extends [Tests/Tests.Infrastructure/BotServiceFixture.cs](../Tests/Tests.Infrastructure/BotServiceFixture.cs) so it honors a parent `WWOW_TEST_BOT_LOG_LEVEL=Information` env var (instead of hard-coding `Warning`) — without this the new traces would be filtered out under live tests. Default behavior (`Warning`) unchanged. |

Build verification:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release` — 0 errors.
- 40 `LoadoutTask` unit tests pass.

```bash
cd "e:/repos/Westworld of Warcraft"
git fetch origin && git log --oneline 1663b798..origin/main
docker ps --format "table {{.Names}}\t{{.Status}}"
```

The Docker stack (`mangosd`, `realmd`, `pathfinding-service`) was healthy
at session start.

---

## v10 fix verification — RESULT

### The re-stack fix (`d385d31e`) is VERIFIED ✓

With `EquipmentEquipTests.EquipItem_AutomatedMode_LoadoutAppliesAndEquips`
locally flipped to `includeForegroundIfActionable: true`, `D:\World of
Warcraft\Logs\botrunner_EQUIPFG1.diag.log` shows:

```
[14:17:12.954] [ACTION-RECV] type=ApplyLoadout params=0 ready=True
[14:17:29.925] [TICK#201] ready=True ... tasks=2(LoadoutTask) screen=InWorld map=1 …
[14:17:46.931] [TICK#301] ready=True ... tasks=2(LoadoutTask) screen=InWorld map=1 …
…
```

Compare to v9's `tasks=2(IdleTask)` evidence — that was the bug. After
`d385d31e`, `LoadoutTask` is on top, ticking, and producing
`LoadoutFailed` snapshots with a step-level `failureReason`. **Correction
5 in v10 stands and the fix should not be re-investigated.**

### The secondary FG gap on `.learn <spellId>` is the next blocker ✗

With the new diagnostics enabled (`WWOW_TEST_BOT_LOG_LEVEL=Information`)
the FG log [`D:/World of Warcraft/WWoWLogs/fg_EQUIPFG120260427.log`](../../World%20of%20Warcraft/WWoWLogs/) shows the full LoadoutTask flow:

```
[10:45:34.530 INF] [DIAG] [ACTION-RECV] type=ApplyLoadout
[10:45:34.534 INF] [BOT RUNNER] ApplyLoadout pushed LoadoutTask (targetLevel=0 spells=1 equip=0 supplemental=1)
[10:45:34.551 INF] [LOADOUT] Plan built: 3 step(s): [0:learn spell 198 | 1:set skill 54=1/300 | 2:add item 36]
[10:45:34.552 INF] [LOADOUT-LEARN] spell 198: dispatching '.learn 198' (knownSpellsCount=0, ackFired=false)
[10:45:34.559 INF] [LOADOUT] Step 0 'learn spell 198' TryExecute=true (attempts=0, dispatch=0)
[10:45:34.760 INF] [BOT RUNNER] Initialized idle task sequence for EQUIPFG1
[10:45:34.761 INF] [BOT RUNNER] Re-stacked 1 pre-existing task(s) on top of seeded IdleTask/activity for EQUIPFG1: top=LoadoutTask
[10:45:34.918 INF] [BOT RUNNER] Inventory changed: 0 contained items, 4 item objects in OM
[10:45:34.932 INF] [LOADOUT-LEARN] spell 198: dispatching '.learn 198' (knownSpellsCount=75, ackFired=false)
[10:45:35.119 INF] [LOADOUT-LEARN] spell 198: dispatching '.learn 198' (knownSpellsCount=75, ackFired=false)
…  (16 more identical dispatches at ~180 ms cadence)
[10:45:38.184 WRN] [LOADOUT] Failed: step 'learn spell 198' exceeded 20 retries without being satisfied
```

### What the wire shows (FG packet log)

Each `LOADOUT-LEARN` line lines up with one outbound CMSG_MESSAGECHAT
and one inbound SMSG_MESSAGECHAT:

```
[10:45:34.559] [C→S] 0x0095 CMSG_MESSAGECHAT size=23 (#2)
[10:45:34.689] [S→C] 0x0096 SMSG_MESSAGECHAT size=49 (#4)
[10:45:34.778] [C→S] 0x0095 CMSG_MESSAGECHAT size=23 (#5)
[10:45:34.889] [S→C] 0x0096 SMSG_MESSAGECHAT size=49 (#6)
…
```

**No `SMSG_LEARNED_SPELL (0x017C)` is ever seen.** The chat is reaching
the server (with the dot prefix); the server is responding (49 bytes;
size is constant across all 19 retries — strongly suggests a single
canned system message text, e.g. "There is no such command" or
"Permission denied" or "Player not found"); but the spell is never
learned and `KnownSpellIds` stays at 75 (the Orc Warrior starting set,
which does **not** include 198 per `mangos.playercreateinfo_spell`
where `race=2 class=1` has 39 entries and 198 is absent).

### What the BG path does that works

[Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs:509-519](../Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs#L509-L519)
sends `.learn 198` as `ChatMsg.CHAT_MSG_SAY` with the race-appropriate
language (`Language.Orcish` for Orc/Undead/Tauren/Troll). FG's
`Statics/ObjectManager.Interaction.cs` line 90 calls
`SendChatMessage("\"\"")` via `MainThreadLuaCall("SendChatMessage(\"...\")")`,
and Lua's one-arg `SendChatMessage(text)` defaults to
`SAY` + `GetDefaultLanguage("player")`. The wire-level packet should be
identical, but BG succeeds and FG does not on the same character /
account / GM level.

### Hypotheses to investigate next

In rough order of likelihood:

1. **Server-side chat throttle.** MaNGOS / VMaNGOS rate-limits chat
   per-player; if the throttle is `1` send/sec the FG cadence (~190 ms
   between sends) silently drops 18 of the 19 dispatches. Look for the
   chat throttle constant in the VMaNGOS source under
   `D:\vmangos-server\src\game\Chat` — if found, the fix is to slow
   `LoadoutTask.StepPacingMs` for chat-driven steps OR add a per-step
   pacing override. **But BG sends at the same cadence and succeeds**,
   so this is not a clean fit unless BG happens to win the race on
   send #1 and advance past the step before sends #2-19 even fire.
2. **BG advances on send #1; FG burns retries because `.learn 198`
   races the spellbook hydrate.** On BG, `KnowsSpell` may flip true
   between dispatch and the next `IsSatisfied` poll because the bot
   sees `SMSG_LEARNED_SPELL` immediately. On FG, the FG `_lastKnownSpellIds`
   refresh is (a) on the bot loop thread, (b) gated by a 2-second
   throttle in `RefreshSpells()`, (c) updated by the `LEARNED_SPELL`
   Lua event hook on WoW's main thread. If `LEARNED_SPELL` doesn't
   fire (because the server didn't actually learn the spell) AND
   `RefreshSpells()` doesn't see 198 either, the step never satisfies.
   This re-frames the bug: it might be `SMSG_LEARNED_SPELL` is sent,
   FG just doesn't update `_lastKnownSpellIds` from it. **Check
   `Services/ForegroundBotRunner/Statics/ObjectManager.Spells.cs`
   line 326** (`eventHandler.FireOnLearnedSpell(spellId);`) — is the
   `LEARNED_SPELL` Lua event reliably firing on FG when the GM command
   succeeds? If not, why?
3. **The chat is being SAY-broadcast, not GM-intercepted.** If FG's
   character somehow doesn't have GM mode the way BG does, MaNGOS may
   accept the text as a SAY message ("/s .learn 198"), broadcast it
   back as the 49-byte SMSG_MESSAGECHAT, and never run it as a GM
   command. The accounts have `gmlevel=6` in `realmd.account_access`,
   but `.gm on/off` toggles separately — the `EnsureCleanSlateAsync`
   path explicitly issues `.gm off` at teardown, and the Phase E
   Automated tests **don't call `EnsureCleanSlateAsync`** before
   dispatching APPLY_LOADOUT, so prior-test state could matter.
4. **Mid-login GameSession isn't ready to process GM commands.** The
   first dispatch fires at `+128 ms` after `SMSG_LOGIN_VERIFY_WORLD`.
   The server may be still hydrating the player's spellbook /
   permissions and silently drop GM commands during that window with
   the system message. BG might happen to land its first dispatch
   slightly later, giving the server enough time. **Worth checking
   the BG log for the timing offset between `LOGIN_VERIFY_WORLD` and
   the first `.learn 198` send.**

### Where to look next

- [Exports/BotRunner/Tasks/LoadoutTask.cs](../Exports/BotRunner/Tasks/LoadoutTask.cs) lines 440-470 (`LearnSpellStep`).
- [Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs](../Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs) lines 509-555 (BG chat / GM path).
- [Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs](../Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs) lines 88-95 (FG chat).
- [Services/ForegroundBotRunner/Statics/ObjectManager.Spells.cs](../Services/ForegroundBotRunner/Statics/ObjectManager.Spells.cs) lines 80-150 + 320-330 (`LEARNED_SPELL` event flow + `RefreshSpells` throttle).
- VMaNGOS source: `D:\vmangos-server\src\game\Chat\` for throttle and
  command parsing.
- Run with `WWOW_TEST_BOT_LOG_LEVEL=Information dotnet test ...
  --filter EquipItem_AutomatedMode_LoadoutAppliesAndEquips` and inspect
  `D:\World of Warcraft\WWoWLogs\fg_EQUIPFG1<date>.log`.

### Useful test invocation

```bash
# Locally flip the test to FG+BG (do NOT commit this flip):
# Tests/BotRunner.Tests/LiveValidation/EquipmentEquipTests.cs:104
#   includeForegroundIfActionable: false  →  true

# Run with full LoadoutTask traces:
WWOW_TEST_BOT_LOG_LEVEL=Information dotnet test \
  Tests/BotRunner.Tests/BotRunner.Tests.csproj \
  --configuration Release --no-build \
  --filter "FullyQualifiedName~EquipItem_AutomatedMode_LoadoutAppliesAndEquips"

# Inspect logs:
ls -la "/d/World of Warcraft/WWoWLogs/" | grep EQUIPFG
ls -la "/d/World of Warcraft/Logs/" | grep EQUIPFG
```

If the local flip is in place, **revert it before committing anything
else** (`includeForegroundIfActionable: false` is the v10/v11
ground-truth value).

---

## Phase E status — three siblings landed, all BG-only

(Unchanged from v10):

| Test | Config | FG/BG status |
|---|---|---|
| `EquipmentEquipTests.EquipItem_AutomatedMode_LoadoutAppliesAndEquips` | `Equipment.Automated.config.json` | BG-only |
| `UnequipItemTests.UnequipItem_AutomatedMode_LoadoutAppliesAndUnequips` | `Equipment.Automated.config.json` (shared) | BG-only |
| `FishingProfessionTests.Fishing_AutomatedMode_BgOnly_RatchetStagedPool` | `Fishing.Automated.config.json` | BG-only. Reuses `EnsureCloseFishingPoolActiveNearAsync` from Shodan staging. |

**All three remain BG-only until the `.learn`-on-FG gap is fixed.**
Once fixed, flip each to `includeForegroundIfActionable: true` and
remove the `[ACTION-PLAN] FG: skipped …` log line — one commit per
test.

### Recommended next siblings (unchanged from v10)

- **`GatheringProfessionTests`** — Mining + Herbalism on the same
  character. Needs `Gathering.Automated.config.json` with mining pick
  (`SupplementalItemIds=[2901]`) + skills 186/182.
- **`OrgrimmarGroundZAnalysisTests`** — minimal loadout (just spawn at
  Orgrimmar). Could be the simplest migration. Note: this test lives in
  `Tests/BotRunner.Tests/Diagnostics/` not LiveValidation — keep it there.
- **`TalentAllocationTests`** — needs `talent_template` in the
  `Loadout`; that path goes through offline SOAP/MySQL during
  coordinator pre-launch. May need schema work first.

---

## Phase B — chat-driven prep-window (unchanged from v9/v10)

`BgTestHelper.WaitForBattlegroundStartAsync` polls all bot snapshots'
`RecentChatMessages` for any line containing "begun" (case-insensitive)
every 2s. Used at:

- `BattlegroundEntryTests.cs:123` (was 130s blind, now ≤130s with
  early-exit).
- `WsgObjectiveTests.cs:142` (was 95s blind, now ≤95s with early-exit).

### What's left in Phase B

| Wall | File:Line | Notes |
|---|---|---|
| 1.5s × 1 | `Raids/RaidCoordinationTests.cs:160` (post-`AssignLoot`) | No snapshot field for raid loot rule today. Keep as-is. |
| 1.5s × 1 | `RaidFormationTests.cs:102` (post-`ChangeRaidSubgroup`) | No subgroup field on the snapshot. Keep as-is. |
| 1.0s × 1 | `RaidFormationTests.cs:116` (post-`DisbandGroup` cleanup) | Cleanup, not test-critical. Keep as-is. |

---

## Phase D — Taxi single + multi-hop, Elevator pilot (unchanged)

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

## Phase C — Concurrent FG/BG (PENDING, unchanged)

- Mining take-turns. Both bots launch `StartGatheringRoute` against the same node concurrently.
- Herbalism follow. FG follows BG between nodes.
- Generalize "FG follows BG" as a BotRunner-side helper.

---

## Phase F-1 — StateManager Automated mode

**COMPLETE.** Don't touch.

## Phase F-2 / F-3 — OnDemand + production polish (PENDING, unchanged)

## Phase G — Final cleanup (PENDING)

After everything above.

---

## Course corrections recorded so far (don't re-discover them)

(Unchanged from v10 — all still valid; new Correction 6 added.)

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
re-stack is **verified** in v11. If you see a future symptom where a
task pushed by action dispatch (`HandleApplyLoadoutAction`, etc.) ends
up buried under `IdleTask`, verify the re-stacking logic still runs.

### Correction 6 (NEW v11) — `Serilog.Log.*` is silently dropped on FG by default

Until commit `9d67c1aa`, `Services/ForegroundBotRunner/Program.cs` only
configured `Microsoft.Extensions.Logging.AddConsole()` — there was no
Serilog static-logger init. `BotRunnerService` and `LoadoutTask` use
`Serilog.Log.Information/Warning/...` directly, so those traces went
nowhere on FG. The instance-level `BotRunnerService.DiagLog(...)` does
write to `D:\World of Warcraft\Logs\botrunner_<account>.diag.log`, but
that's a per-tick coarse-grained format only. **If you add new
`Log.Information(...)` traces and they don't appear in the FG file
log, you're either at the wrong log level (the fixture defaults to
`Warning`) or you forgot to set `WWOW_TEST_BOT_LOG_LEVEL=Information`
when invoking `dotnet test`.**

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
- **From v9 — Phase E Automated migrations are BG-only until the FG `.learn`-step gap is fixed.** v10's re-stack fix is verified in v11; the *secondary* `.learn`-on-FG gap is the new blocker. Keep new migrations BG-only until that secondary gap is fixed.
- **From v11 — When you ran the test with the local FG-flip in `EquipmentEquipTests.cs`, REVERT IT before committing.** The committed value is `includeForegroundIfActionable: false`.

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
   confirm `4b24a530`, `d385d31e`, `b0c92f80`, `9d67c1aa`, `d0d99878`
   are all present.
2. Run `docker ps` and confirm `mangosd`, `realmd`, `pathfinding-service`
   are healthy.
3. Read in order: this handoff (top to bottom) → optionally
   [`docs/handoff_test_consolidation_v10.md`](handoff_test_consolidation_v10.md)
   for the FG re-stack diagnosis that v10 contributed and v11
   verifies → `docs/statemanager_modes_design.md` (apply the six
   corrections above) → `CLAUDE.md` → `Tests/CLAUDE.md`.
4. Skim `C:\Users\lrhod\.claude\projects\e--repos-Westworld-of-Warcraft\memory\MEMORY.md`.
5. **First task — diagnose the `.learn`-on-FG gap.** Use the test
   invocation in "Useful test invocation" above with
   `WWOW_TEST_BOT_LOG_LEVEL=Information`. The hypothesis ranking is
   spelled out in "Hypotheses to investigate next". A good first probe
   is **Hypothesis 2 (LEARNED_SPELL event not firing on FG)** because
   it's the most code-local and the chat *did* reach the server.
6. Once the secondary gap is fixed: flip the three landed Automated
   tests (Equipment Equip, Equipment Unequip, Fishing) from BG-only
   to FG+BG by passing `includeForegroundIfActionable: true` and
   removing the FG-skip `[ACTION-PLAN]` log line. One commit per
   test.
7. After that, pick a phase. **Recommended order:**
   - **Phase E broader migration** — `GatheringProfessionTests` is
     the next sibling target.
   - **Phase D — investigate Multi-hop ground-snap regression**.
   - **Phase D — investigate elevator TransportGuid acquisition**.
   - **Phase C — Concurrent FG/BG** — Mining take-turns, Herbalism
     follow, generalize "FG follows BG".
   - **Phase F-2 — OnDemand handler.**
8. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next handoff prompt at `docs/handoff_test_consolidation_v12.md`
(bump the suffix). Include:

1. Concise restatement of the goal (this file's "Why this work exists").
2. Current commit hash and a short summary of work landed since this v11 was written.
3. The remaining phases with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session. **Make
it self-contained** — assume the next agent has zero memory.
