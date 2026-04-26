# StateManager Modes — Design Doc (Phase F)

> Authored: 2026-04-26 in support of `docs/handoff_test_consolidation.md`.
> Purpose: lock the schema and dispatch shape for the three StateManager
> modes (`Test`, `Automated`, `OnDemandActivities`) before code changes
> begin. The next session executes against this design.

---

## What's already in the codebase (load-bearing!)

The user's framing implies F-1 ("Automated mode") needs greenfield work.
**It does not.** A grep across `Services/WoWStateManager` and
`Exports/BotRunner` shows the loadout/activity hand-off is already wired:

| Component | What it does | File |
|---|---|---|
| `CharacterSettings.Loadout` | Per-character `LoadoutSpecSettings` with target level, skills, equip items, factions, talents, etc. | `Services/WoWStateManager/Settings/CharacterSettings.cs:148` |
| `CharacterSettings.AssignedActivity` | String descriptor like `"Fishing[Ratchet]"`, `"Battleground[WSG]"`, `"Dungeon[RFC]"` parsed by `BotRunner.Activities.ActivityParser` at world-entry. | `Services/WoWStateManager/Settings/CharacterSettings.cs:172` |
| `LoadoutSpecConverter` | POCO → proto translation for the `APPLY_LOADOUT` action | `Services/WoWStateManager/Settings/LoadoutSpecConverter.cs` |
| `LoadoutTask` (BotRunner side) | Executes the loadout plan inside the bot. Reports completion via `WoWActivitySnapshot.LoadoutStatus`. | `Exports/BotRunner/Tasks/LoadoutTask.cs` |
| `ActivityResolver` (BotRunner side) | Parses `AssignedActivity` and starts the corresponding activity. | `Exports/BotRunner/Activities/ActivityResolver.cs` |
| `BattlegroundCoordinator` | Already does Automated-style orchestration for BG roster | `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs` |
| `BotRunnerService` | Top-level BotRunner orchestrator that knows about `APPLY_LOADOUT` | `Exports/BotRunner/BotRunnerService.cs` |

This means F-1 is **mostly a wiring + flag exercise**, not a rewrite.

The fixture-side `StageBotRunnerLoadoutAsync` (in
`Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`)
exists because the **`Test` mode** doesn't auto-trigger the
`APPLY_LOADOUT` from the per-character `Loadout` field — instead it
waits for a fixture to enqueue the action. Other paths (BG coordinator,
production) already trigger it autonomously via
`AssignedActivity`/`Loadout` at world-entry.

---

## Schema change

### Current schema (legacy)

`StateManagerSettings.json` and every `Settings/Configs/*.config.json`
file is a **bare JSON array of `CharacterSettings`** — no top-level
wrapper:

```json
[
  { "AccountName": "TESTBOT1", ... },
  { "AccountName": "TESTBOT2", ... }
]
```

The loader is at `Services/WoWStateManager/Settings/StateManagerSettings.cs:43`:

```csharp
CharacterSettings = JsonConvert.DeserializeObject<List<CharacterSettings>>(
    File.ReadAllText(settingsFilePath)) ?? [];
```

### Proposed schema (forward-compatible)

```json
{
  "Mode": "Automated",
  "Characters": [
    { "AccountName": "TESTBOT1", ... }
  ]
}
```

`Mode` is one of:

| Value | Meaning |
|---|---|
| `"Test"` | Today's behavior. StateManager waits for an `ActionMessage` (from a test fixture) and dispatches it. **Default if `Mode` omitted.** |
| `"Automated"` | At world-entry, StateManager auto-dispatches the per-character `Loadout` (`APPLY_LOADOUT`) followed by `AssignedActivity` parsing. The bot self-progresses through all configured stages. Tests assert against snapshot milestones. |
| `"OnDemandActivities"` | StateManager listens for human-player requests (via Shodan in-game, or via the WPF UI) on top of `AssignedActivity` parsing. Same dispatch path as Automated, but triggered by external request rather than at world-entry. |

### Backward-compatible loader

Update `StateManagerSettings.LoadConfig` to detect the JSON root shape:

```csharp
private void LoadConfig()
{
    var raw = File.ReadAllText(settingsFilePath);
    var token = JToken.Parse(raw);

    if (token is JArray)
    {
        // Legacy: bare array means "Test mode + this character roster".
        Mode = StateManagerMode.Test;
        CharacterSettings = token.ToObject<List<CharacterSettings>>() ?? [];
    }
    else if (token is JObject obj)
    {
        Mode = obj["Mode"]?.ToObject<StateManagerMode>() ?? StateManagerMode.Test;
        CharacterSettings = obj["Characters"]?.ToObject<List<CharacterSettings>>() ?? [];
    }
    else
    {
        throw new InvalidOperationException(
            $"Unexpected JSON root in {settingsFilePath}: must be array (legacy) or object with 'Mode' + 'Characters'.");
    }
}
```

This means **every existing config file remains valid**: bare arrays get
`Mode = Test`. New configs can opt into Automated/OnDemand by switching
to the wrapped form.

### Per-config mode plan

| Config | Current implicit mode | Phase F target |
|---|---|---|
| `Default.config.json` | Test | Test (unchanged) |
| `Equipment.config.json`, `Wand.config.json`, `MageTeleport.config.json`, `Gathering.config.json`, `Crafting.config.json`, `PetManagement.config.json`, `Economy.config.json`, `NpcInteraction.config.json`, `Navigation.config.json`, `Loot.config.json`, `Fishing.config.json` | Test | **Test (kept)** — these drive xUnit test bodies that rely on explicit action dispatch. |
| `BgOnly.config.json` | Test | Test (kept) |
| `RagefireChasm.config.json`, `WarsongGulch.config.json`, `AlteracValley.config.json`, `ArathiBasin.config.json` | Test (BG coordinator overrides at runtime) | **Automated** — these activities self-orchestrate. |
| `CombatArena.config.json`, `CombatBg.config.json`, `CombatFg.config.json` | Test | Test (kept). |
| `GmCamera.config.json` | Test | Test (kept). |
| New: `Onboarding.config.json` | n/a | **Automated** — example for the "fresh character → fully geared" flow the user described. |
| New: `OnDemand.config.json` | n/a | **OnDemandActivities** — example with Shodan + idle bot pool. |

---

## Mode handler interface

```csharp
namespace WoWStateManager.Modes
{
    public interface IStateManagerModeHandler
    {
        StateManagerMode Mode { get; }

        Task OnWorldEntryAsync(
            CharacterSettings character,
            CancellationToken ct);

        Task OnSnapshotAsync(
            CharacterSettings character,
            WoWActivitySnapshot snapshot,
            CancellationToken ct);

        Task OnExternalActivityRequestAsync(
            string requestingPlayer,
            string activityDescriptor,
            CancellationToken ct);
    }
}
```

### `TestModeHandler`

- `OnWorldEntryAsync`: no-op. Wait for the test to dispatch.
- `OnSnapshotAsync`: forward to the existing snapshot processor.
- `OnExternalActivityRequestAsync`: throw `InvalidOperationException`.

### `AutomatedModeHandler`

- `OnWorldEntryAsync`:
  1. If `character.Loadout != null`: enqueue `APPLY_LOADOUT(loadout)` once.
  2. After `WoWActivitySnapshot.LoadoutStatus == Complete`: parse
     `character.AssignedActivity` (e.g. `"Fishing[Ratchet]"`) and
     dispatch the corresponding `StartXxx` action.
  3. Logs each transition with `[AUTOMATED]` prefix.
- `OnSnapshotAsync`: drive the activity loop. When the activity
  completes (snapshot reports `IsActivityComplete` or task chat marker),
  decide the next action — for now, idle. Future: chain activities by a
  `NextActivities` field on `CharacterSettings`.
- `OnExternalActivityRequestAsync`: ignore (this is OnDemand's job).

### `OnDemandActivitiesModeHandler`

- `OnWorldEntryAsync`:
  - For Shodan: register a chat command listener (`!fish`,
    `!gather copper`, etc.) keyed off Shodan's whisper inbox.
  - For other bots: idle until `OnExternalActivityRequestAsync` fires
    or `AssignedActivity` is set.
- `OnSnapshotAsync`: same as Automated.
- `OnExternalActivityRequestAsync(player, descriptor)`:
  1. Resolve `descriptor` via `ActivityResolver` (same parser BotRunner
     uses).
  2. Pick an idle bot from the pool.
  3. Dispatch the activity to that bot.
  4. Whisper `player` an ACK ("Sending TESTBOT2 to fish at Ratchet").

The WPF UI plugs into the same `OnExternalActivityRequestAsync` via
the existing `StateManagerListener` API on port 8088 — add a new
`POST /activities/request` endpoint.

---

## Wiring into `StateManagerWorker`

```csharp
// In StateManagerWorker.ExecuteAsync (or appropriate startup point):
var handler = _serviceProvider.GetRequiredService<IStateManagerModeHandler>();
_logger.LogInformation("[MODE] StateManager running in {Mode}", handler.Mode);

// On bot world-entry (StateManagerWorker.BotManagement.cs):
await handler.OnWorldEntryAsync(character, ct);

// On every snapshot tick (StateManagerWorker.SnapshotProcessing.cs):
await handler.OnSnapshotAsync(character, snapshot, ct);
```

Registration in `Program.cs`:

```csharp
services.AddSingleton<IStateManagerModeHandler>(sp =>
    StateManagerSettings.Instance.Mode switch
    {
        StateManagerMode.Test => new TestModeHandler(...),
        StateManagerMode.Automated => new AutomatedModeHandler(...),
        StateManagerMode.OnDemandActivities => new OnDemandActivitiesModeHandler(...),
        _ => throw new InvalidOperationException(...),
    });
```

---

## Test impact

`Test` mode is the default for legacy bare-array configs, so every
existing live-validation test continues to work without change.

To make a test exercise Automated mode:

```csharp
// In a test using a config with Mode == "Automated":
await _bot.EnsureSettingsAsync(automatedConfigPath);
// No StageBotRunnerLoadoutAsync call needed —
// StateManager already kicked off APPLY_LOADOUT at world-entry.
await _bot.WaitForSnapshotConditionAsync(
    target.AccountName,
    snap => snap.LoadoutStatus == LoadoutStatus.Complete,
    TimeSpan.FromMinutes(2),
    progressLabel: "automated-loadout");
// Now dispatch the test action.
```

This is what unlocks **Phase E** fixture consolidation: with Automated
mode handling loadout, the fixture's `StageBotRunner*Async` helpers
collapse to "wait for ready" and most of the 2 299 lines in
`LiveBotFixture.TestDirector.cs` become unnecessary.

---

## Implementation order (matches the handoff)

1. **F-1 step 1:** Add `StateManagerMode` enum + the wrapped
   schema + backward-compatible loader. **No behavior change** — all
   existing configs default to `Test`. Commit + push.
2. **F-1 step 2:** Add `IStateManagerModeHandler` and
   `TestModeHandler` (which is a no-op wrapper around current
   behavior). Wire it into `StateManagerWorker`. Commit + push.
3. **F-1 step 3:** Add `AutomatedModeHandler`. Add an
   `Onboarding.config.json` that uses it for one bot character.
   Verify end-to-end with a single live test. Commit + push.
4. **F-2:** Add `OnDemandActivitiesModeHandler`. Add the
   `POST /activities/request` endpoint to the `StateManagerListener`.
   Add Shodan whisper-command handling (`!fish ratchet`).
   Commit + push.
5. **F-3:** Once F-1 + F-2 are stable, audit the existing fixture
   helpers and start moving the production-relevant ones (loadout
   apply, activity dispatch) to `BotRunner.Tasks.LoadoutTask` /
   `ActivityResolver`. Trim the fixture-side wrappers to thin
   adapters. Commit per-helper.
6. **Phase E:** Replace `LiveBotFixture.*.cs` partials with one
   `LiveFixture.cs`. Most behavior moved to F-1/F-2/F-3 by this
   point.

---

## Open questions for the next session

1. **Where does Shodan listen for whispers in OnDemand mode?**
   Current chat-command plumbing exists for bot self-chat
   (`SendGmChatCommandAsync`); inbound whisper handling is sketchier.
   Likely needs a new chat-message subscriber on the BG bot for
   Shodan's account.
2. **Is `LoadoutStatus` already on `WoWActivitySnapshot`?** The doc
   comment at `CharacterSettings.cs:142-149` says yes, but verify in
   the proto schema before relying on it.
3. **`AssignedActivity` parser scope.** Confirm
   `ActivityParser`/`ActivityResolver` covers all activities the test
   suite exercises (mining, herbalism, fishing, BG, dungeon, raid,
   gathering routes, crafting). If gaps exist, expand the parser
   before F-1 step 3.
4. **`BattlegroundCoordinator` overlap.** The BG coordinator already
   does Automated-style orchestration. Does it become a special case
   of `AutomatedModeHandler`, or stay separate? Phase F-3 question.
