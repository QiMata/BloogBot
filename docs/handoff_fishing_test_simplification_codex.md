# Fishing Test Simplification — Codex handoff

This doc is the handoff packet for a fresh Codex session to pick up cold. The
repo root is `E:\repos\Westworld of Warcraft` on branch `main`. Last green is
commit `2c5e76bb` (records 4th consecutive Ratchet fishing rerun).

## Prompt (paste to Codex)

> You are simplifying the dual-bot Ratchet fishing live test in the Westworld
> of Warcraft / BloogBot repo. The test passes (4 consecutive green runs) but
> does 4 full settings-file roster restarts, which forces 2 separate
> `WoW.exe` launches and makes the run take ~22 minutes. The goal is
> **one roster launch, zero roster restarts**, phases driven by action
> dispatch. Read this entire document, then `CLAUDE.md`, `Tests/CLAUDE.md`,
> and `docs/TASKS.md` before touching code. Commit in small logical units
> and push after each commit.

## TL;DR

`ActionType.StartFishing = 67` **already exists** in the protocol and already
pushes a `FishingTask` onto the bot's task stack. The protocol surface is
mostly there. The real work is (1) extending the `StartFishing` dispatch
parameter schema so the action-driven path matches the feature set of the
env-var path, (2) dropping `AssignedActivity` from FG/BG in
`Fishing.config.json` so they idle until told, and (3) rewriting the test
body to use a single settings file plus three `SendActionAsync` phases.

## Current state (why it's over-complicated)

File: [Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs](../Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs)

Current flow in `Fishing_CatchFish_BgAndFg_RatchetStagedPool`:

1. `EnsureSettingsAsync(Fishing.ShodanOnly.config.json)` → only SHODAN up, FG/BG torn down
2. Shodan stages pool (GM-chat via `EnsureCloseFishingPoolActiveNearAsync`)
3. `EnsureSettingsAsync(Fishing.FgOnly.runtime.config.json)` → Shodan torn down, FG relaunched (**2nd WoW.exe**)
4. Wait for FG `fishing_loot_success`
5. `EnsureSettingsAsync(Fishing.ShodanOnly.config.json)` → FG torn down, Shodan up
6. Shodan re-stages pool
7. `EnsureSettingsAsync(Fishing.BgOnly.runtime.config.json)` → Shodan torn down, BG up
8. Wait for BG `fishing_loot_success`

Trace from the most recent rerun ([fishing_final_rerun_after_fixture_change.console.txt](../tmp/test-runtime/results-live/fishing_final_rerun_after_fixture_change.console.txt)):
- Line 127: `WoW.exe started for account TESTBOT1 (Process ID: 32980)`
- Line 790 → 814: restart to ShodanOnly — FG torn down
- Line 1080 → 1104: restart to FgOnly — WoW.exe relaunched
- Line 1106 → 1130: restart back to ShodanOnly
- Line 1404 → 1428: restart to BgOnly
- Total runtime: 22m28s

The restart cost is real (each FG restart re-injects Loader.dll and walks the
login screen). Collapsing to one launch cuts roughly half the wall-clock.

## Target state

Single roster (FG + BG + Shodan, all idle). Test body:

1. Fixture ready (one `EnsureSettingsAsync(Fishing.config.json)`)
2. `EnsureShodanAdminLoadoutAsync(Shodan, ...)`
3. `EnsureCloseFishingPoolActiveNearAsync(Shodan, ...)` → pool staged
4. `SendActionAsync(FG, ActionMessage{ StartFishing, location="Ratchet",
   useGmCommands=true, masterPoolId=2628 })`
5. Wait until `[TASK] FishingTask fishing_loot_success` appears in FG snapshot
   `RecentChatMessages` (FishingTask pops itself on success — see
   [Exports/BotRunner/Tasks/FishingTask.cs:2081](../Exports/BotRunner/Tasks/FishingTask.cs#L2081))
6. `EnsureCloseFishingPoolActiveNearAsync(Shodan, ...)` → re-stage pool
7. `SendActionAsync(BG, ActionMessage{ StartFishing, location="Ratchet",
   useGmCommands=true, masterPoolId=2628 })`
8. Wait for BG `fishing_loot_success`

No roster restarts. FG's `WoW.exe` stays up the entire test.

## Why this works (research summary)

### The protocol already has `StartFishing`

- Proto: [Exports/BotCommLayer/Models/ProtoDef/communication.proto:122](../Exports/BotCommLayer/Models/ProtoDef/communication.proto#L122)
  — `START_FISHING = 67`
- Proto → CharacterAction mapping: [Exports/BotRunner/BotRunnerService.ActionMapping.cs:88](../Exports/BotRunner/BotRunnerService.ActionMapping.cs#L88)
- CharacterAction dispatch: [Exports/BotRunner/ActionDispatcher.cs:503-512](../Exports/BotRunner/ActionDispatcher.cs#L503-L512)

Current dispatch body:

```csharp
case CharacterAction.StartFishing:
{
    var fishingSearchWaypoints = ParseGatheringRoutePositions(actionEntry.Item2);
    builder.Do("Queue Fishing Task", time =>
    {
        if (_botTasks.Count == 0 || _botTasks.Peek() is not Tasks.FishingTask)
            _botTasks.Push(new Tasks.FishingTask(context, fishingSearchWaypoints.Count > 0 ? fishingSearchWaypoints : null));
        return BehaviourTreeStatus.Success;
    });
    break;
}
```

The `_botTasks.Peek() is not Tasks.FishingTask` guard makes dispatch
idempotent — firing `StartFishing` twice is a no-op if the task is already
running. `StartGatheringRoute` (lines 514-542) and `StartDungeoneering` use
the identical pattern.

### The gap: dispatch drops three constructor params

`ActivityResolver.Resolve` (the env-var path,
[Exports/BotRunner/Activities/ActivityResolver.cs:62-71](../Exports/BotRunner/Activities/ActivityResolver.cs#L62-L71))
constructs:

```csharp
return new FishingTask(
    context,
    searchWaypoints: null,
    location: location,         // "Ratchet"
    useGmCommands: useGmCommands,
    masterPoolId: masterPoolId); // 2628
```

`ActionDispatcher.StartFishing` constructs:

```csharp
new Tasks.FishingTask(context, fishingSearchWaypoints...)
// location = null, useGmCommands = false, masterPoolId = null
```

The `FishingTask` constructor ([Exports/BotRunner/Tasks/FishingTask.cs:21-37](../Exports/BotRunner/Tasks/FishingTask.cs#L21-L37))
branches on those flags:

```csharp
_state = useGmCommands
    ? FishingState.EnsureGearAndSpells
    : (_location != null ? FishingState.TravelToLocation : FishingState.EnsurePoleEquipped);
```

If the action-driven path doesn't pass them, the bot skips gear/spell
setup, skips `.tele name <char> Ratchet`, and skips `.pool update` — which
means the bot fishes wherever it currently is, without any of the Ratchet
staging machinery that makes the test work. **The `StartFishing` dispatch
must be extended to forward these parameters.**

### Config fix is trivial

- [Services/WoWStateManager/Settings/Configs/Fishing.config.json](../Services/WoWStateManager/Settings/Configs/Fishing.config.json)
  currently sets `"AssignedActivity": "Fishing[Ratchet]"` on TESTBOT1 and
  TESTBOT2 (lines 16, 32). SHODAN has no such field and idles correctly.
- Drop those two lines. FG and BG will launch idle exactly like Shodan does
  today.
- The env-var clear-on-null fix from commit `1f1d6746`
  ([Services/WoWStateManager/StateManagerWorker.BotManagement.cs:55-67](../Services/WoWStateManager/StateManagerWorker.BotManagement.cs#L55-L67))
  already removes `WWOW_ASSIGNED_ACTIVITY` from the process env when the
  field is absent, for both BG and FG launches.
- `ActivityResolver.Resolve(null)` returns null
  ([ActivityResolver.cs:45-46](../Exports/BotRunner/Activities/ActivityResolver.cs#L45-L46)),
  so the bot stays idle.

## Plan

### Phase A — extend `StartFishing` dispatch (protocol surface)

No `.proto` change needed; the params flow through the existing
`repeated RequestParameter parameters` field. Codex picks the param
convention.

**A.1** Define the param schema for `StartFishing`. Suggested:

| Index | Type   | Meaning                                             |
|-------|--------|-----------------------------------------------------|
| 0     | string | location (e.g. "Ratchet"); empty = no travel        |
| 1     | int    | useGmCommands (0 or 1); default 0                   |
| 2     | int    | masterPoolId (0 = unset)                            |
| 3+    | floats | search waypoints (x, y, z triples) — existing shape |

The existing code already tolerates variable-shape param lists
([ActionDispatcher.cs:519-527](../Exports/BotRunner/ActionDispatcher.cs#L519-L527)
does the same thing for `StartGatheringRoute`).

**A.2** Update the `StartFishing` case at
[ActionDispatcher.cs:503-512](../Exports/BotRunner/ActionDispatcher.cs#L503-L512):
read optional `location`, `useGmCommands`, `masterPoolId` from `actionEntry.Item2`,
then pass them into `new FishingTask(...)`.

**A.3** Add a unit test in
[Tests/BotRunner.Tests/](../Tests/BotRunner.Tests/) (follow the existing
`ActionDispatcherTests` or equivalent) that verifies the constructor params
are forwarded correctly. Pattern to copy:
- Look for an existing `StartGatheringRoute` dispatch test if one exists.
- If none, add one for the Fishing case — inject an `ActionMessage` with
  mixed string + int + float params, run the dispatcher, assert the
  resulting `FishingTask` has the expected `_location` / `_useGmCommands` /
  `_masterPoolId` (will need internals visible or a small accessor).

### Phase B — config

**B.1** Edit [Services/WoWStateManager/Settings/Configs/Fishing.config.json](../Services/WoWStateManager/Settings/Configs/Fishing.config.json):
remove `"AssignedActivity": "Fishing[Ratchet]"` from TESTBOT1 (line 16) and
TESTBOT2 (line 32). Shodan already has no assigned activity.

**B.2** Delete [Services/WoWStateManager/Settings/Configs/Fishing.ShodanOnly.config.json](../Services/WoWStateManager/Settings/Configs/Fishing.ShodanOnly.config.json).
It has no remaining callers after the test rewrite. Grep for
`Fishing.ShodanOnly.config.json` to confirm before deleting — a stale
reference is worse than a dead file.

### Phase C — rewrite the test

Edit [Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs](../Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs):

**C.1** Delete:
- `PrepareShodanStagedPoolAsync` helper (becomes inlined)
- `CreateSingleBotFishingSettings` helper (runtime config writes are gone)
- All three `EnsureSettingsAsync` calls except the initial one

**C.2** New test body (pseudocode — Codex writes the real C#):

```csharp
await _bot.EnsureSettingsAsync(ResolveRepoPath("Services", ..., "Fishing.config.json"));
Skip.IfNot(_bot.IsReady, _bot.FailureReason);

var shodan = _bot.ShodanAccountName!;
var fg = _bot.FgAccountName!;
var bg = _bot.BgAccountName!;

// Phase 1 — Shodan setup + stage pool
await _bot.EnsureShodanAdminLoadoutAsync(shodan, _bot.ShodanCharacterName);
Assert.True(await _bot.EnsureCloseFishingPoolActiveNearAsync(shodan, ...));

// Phase 2 — FG fishes
await _bot.SendActionAsync(fg, new ActionMessage
{
    ActionType = Communication.ActionType.StartFishing,
    Parameters = {
        new RequestParameter { StringParam = "Ratchet" },
        new RequestParameter { IntParam = 1 },            // useGmCommands
        new RequestParameter { IntParam = 2628 },         // masterPoolId
    },
});
var fgResult = await WaitForSingleLootSuccessAsync(fg, "FG", FishingLootDeadline);
Assert.True(fgResult.SawLootSuccess, ...);

// FG pops its own task on fishing_loot_success (FishingTask.cs:2081). FG is
// now idle again — verify by snapshot if desired.

// Phase 3 — Shodan re-stages
Assert.True(await _bot.EnsureCloseFishingPoolActiveNearAsync(shodan, ...));

// Phase 4 — BG fishes
await _bot.SendActionAsync(bg, new ActionMessage { ActionType = ..., Parameters = {...} });
var bgResult = await WaitForSingleLootSuccessAsync(bg, "BG", FishingLootDeadline);
Assert.True(bgResult.SawLootSuccess, ...);
```

**C.3** Keep `WaitForSingleLootSuccessAsync` — it's snapshot-polling and
doesn't depend on roster shape.

### Phase D — validation

Build: `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false`

Deterministic slice first (protects against dispatch regression):

```
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~ActionDispatcher" --logger "console;verbosity=minimal"
```

Live slice (repo-scoped cleanup + focused filter):

```
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly
$env:WWOW_DATA_DIR='D:/MaNGOS/data'
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch.trx"
```

Success criteria:
- Test passes
- Console shows **exactly one** `WoW.exe started for account TESTBOT1` line
- Console shows **exactly one** `[FIXTURE] Ready. BG='...' FG='Gargandurooj'` line
- No `[FIXTURE] Restarting with custom settings:` lines after initial ready
- Wall-clock noticeably lower than 22m (target ≤ 15m)

Then rerun the live slice **three times** to match the current 4x evidence
bar for Ratchet fishing. Update
[docs/TASKS.md](../docs/TASKS.md) Handoff + [Tests/BotRunner.Tests/TASKS.md](../Tests/BotRunner.Tests/TASKS.md)
Session Handoff with the new green-run evidence.

## Risks and unknowns

1. **Does FG go fully idle after `fishing_loot_success`?**
   - `FishingTask.cs:2081` does `PopFishingTask("fishing_loot_success")`, which
     pops the task off `_botTasks`. So yes, FG should idle after the marker.
   - But during Phase 3 (re-staging), FG is still in-world at the pier. If
     Shodan's `.gobject move` shuffles pools, FG could observe a pool change.
     FG has no active fishing task, so it shouldn't react, but watch for any
     `FishingTask activity_start` markers during re-staging. If that happens,
     add an explicit `SendActionAsync(fg, Wait)` or equivalent to clear any
     residual.

2. **`FishingTask.cs` constructor branches on `_useGmCommands`.** Current
   env-var path sets `useGmCommands = true` from `CharacterSettings.UseGmCommands`
   (which is `true` for all three test accounts in `Fishing.config.json`).
   The action-driven path needs to pass `1` explicitly. The dispatcher
   should **not** read `UseGmCommands` from settings — it must come from
   the action parameter, so different callers can drive different setups.

3. **Correlation ID assertion.** P4/P5 wired `CommandAckEvent` end-to-end
   ([docs/TASKS.md](../docs/TASKS.md) P4.4-P4.5). `StartFishing` will produce
   an ack. Consider using
   [LiveBotFixture.BotChat.SendGmChatCommandTrackedAsync](../Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs)
   as a reference for the action-tracking pattern if you want an
   ack-gated dispatch. Not strictly required — the `fishing_loot_success`
   marker is authoritative — but it would give a tighter failure signal
   when the dispatch itself fails vs. the task failing.

4. **Shodan GM chat during FG fishing.** Phase 3 runs Shodan staging while FG
   is idle but still in-world. Shodan's `.gobject move` / `.pool update`
   commands run via its own GM chat pipe. There should be no cross-bot
   interference, but double-check that the fixture's Shodan sync
   (`SendGmChatCommandAndAwaitServerAckAsync`) blocks only on Shodan's own
   correlation ids, not any bot's.

5. **Existing 4-green Ratchet evidence.** The current test is stable. If
   Phase A through C land but the first live run fails, **do not delete the
   current evidence pile** — those 4 trx files are the regression baseline.

## Files you'll touch

Write paths:
- [Exports/BotRunner/ActionDispatcher.cs](../Exports/BotRunner/ActionDispatcher.cs) — extend `StartFishing` case
- [Services/WoWStateManager/Settings/Configs/Fishing.config.json](../Services/WoWStateManager/Settings/Configs/Fishing.config.json) — drop `AssignedActivity` from FG/BG
- [Services/WoWStateManager/Settings/Configs/Fishing.ShodanOnly.config.json](../Services/WoWStateManager/Settings/Configs/Fishing.ShodanOnly.config.json) — delete
- [Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs](../Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs) — rewrite body
- [docs/TASKS.md](../docs/TASKS.md) + [Tests/BotRunner.Tests/TASKS.md](../Tests/BotRunner.Tests/TASKS.md) — record the handoff, then record green evidence

Read-only references:
- [Exports/BotRunner/Tasks/FishingTask.cs](../Exports/BotRunner/Tasks/FishingTask.cs) (constructor at 21-37; pop on success at 2081)
- [Exports/BotRunner/Activities/ActivityResolver.cs](../Exports/BotRunner/Activities/ActivityResolver.cs) (how the env-var path constructs FishingTask)
- [Exports/BotCommLayer/Models/ProtoDef/communication.proto](../Exports/BotCommLayer/Models/ProtoDef/communication.proto) (ActionMessage + ActionType surface)
- [Services/WoWStateManager/StateManagerWorker.BotManagement.cs](../Services/WoWStateManager/StateManagerWorker.BotManagement.cs) (env-var clear-on-null helpers at 55-67)
- [Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs](../Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs) (SendActionAsync + SendGmChatCommandTrackedAsync)
- [Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ShodanLoadout.cs](../Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ShodanLoadout.cs) (EnsureShodanAdminLoadoutAsync)
- [Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs](../Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs) (EnsureCloseFishingPoolActiveNearAsync)

## Repo rules that apply

- No blanket-kill of `dotnet` or `WoW.exe`. Use `run-tests.ps1 -CleanupRepoScopedOnly`.
- MaNGOS is live in Docker. `docker ps` before running live tests.
- No direct MySQL writes for game state. Use SOAP / bot chat.
- Do not skip hooks (`--no-verify`) on commits unless explicitly asked.
- Commit and push after each logical unit.
- `CLAUDE.md` line "No Crash Patching / No Fallbacks": if the action-driven
  path uncovers a bug in `FishingTask`, **fix the root cause** — do not add
  a fallback to route around it.

## Previous handoff

Last test-side work: commit `c3f24dad fix(livevalidation): stabilize shodan ratchet fishing`
(staged local-pool rotation + 4-green Ratchet evidence). That work stays —
this phase is purely about collapsing the roster-restart gymnastics that
commit accepts as a given.
