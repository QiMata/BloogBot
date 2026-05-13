# Spec 14 — Config and Hot Reload

## Schema layout

All configs live under `Config/` at repo root. The schema is JSON, with
JSON Schema files committed under `Config/schema/` for validation.

```
Config/
├── schema/                         // JSON Schema definitions
│   ├── statemanager.schema.json
│   ├── character.schema.json
│   ├── activity.schema.json
│   └── ...
├── StateManagerSettings.json        // root config (Mode + Characters)
├── ActivityCatalog.overrides.json   // optional per-activity overrides
├── LoggingProfile.json
├── Metrics.json
├── Pathfinding.json
└── Configs/
    ├── Default.config.json          // per-test rosters
    ├── Onboarding.config.json
    └── OnDemand.config.json
```

The compiled catalog (in `Services/WoWStateManager/Activities/ActivityCatalog.cs`)
is authority; `ActivityCatalog.overrides.json` is optional config that
sets per-row `Enabled`, `BotSelectionPolicy`, custom `RoleTemplate`,
etc. without rebuilding.

## Reloadable sections

These sections support hot reload (change without service restart):

- `LivingServer.*` (most fields)
- `Metrics.*`
- `Logging.*`
- `Pathfinding.RoutePackWarmup`, `Pathfinding.RequestTimeoutSeconds`,
  `Pathfinding.QueueLimit`
- `ActivityCatalog.overrides.*`
- Per-character `AssignedActivity`, `NextActivities`,
  `ProgressionPriority`, `Loadout`
- Coordinator-specific behavior flags

These sections require restart (change is rejected with `restart_required`):

- `Mode` (per-character or root)
- Listener ports
- Service endpoints
- `Pathfinding.ReplicaCount` (sharding)
- Database connection strings

## Hot reload flow

```
1. Editor (UI / file watcher) → StateManager.SaveConfig(scope, payload)
2. StateManager: JSON-schema validate
3. StateManager: dependency analysis (which subscribers care)
4. StateManager: atomic write tmp → rename
5. StateManager: broadcast ConfigChangedEvent{ scope, payload, version }
6. Subscribers (BotRunner, services): reload affected sections, ACK
7. StateManager: collect ACKs within HotReloadAckTimeoutMs (default 2000)
8. On all-ACK: emit SaveAck{ Applied: true, AppliedAt: ... }
   On any NACK or timeout: rollback file, emit
   SaveAck{ Rolledback: true, FailedSubscribers: [...] }
```

## ConfigChangedEvent shape

```protobuf
message ConfigChangedEvent {
  string scope = 1;             // "Logging" | "Activity.OG_dungeon" | ...
  string version = 2;           // monotonic
  bytes payload = 3;            // serialized section
  uint64 effective_at_ms = 4;   // unix ms
  repeated string affected_keys = 5;
}
```

Subscribers register an `IConfigSubscriber` per scope:

```csharp
public interface IConfigSubscriber
{
    string Scope { get; }
    Task<ConfigApplyResult> TryApplyAsync(ConfigChangedEvent evt,
                                          CancellationToken ct);
}

public sealed record ConfigApplyResult(
    bool Applied,
    string? FailureReason,
    string? RollbackHint);
```

## File watcher

StateManager also watches `Config/` for external edits (e.g. operator
hand-edits with the UI offline). The watcher debounces (500 ms), then
runs the same validate → broadcast flow.

To prevent flapping with the UI, the UI tags its writes with a
`SourceTag = "UI"` and the watcher ignores changes within 1 s of a UI
ACK.

## Per-character override

`CharacterSettings` carries per-character config that overrides the
root config. Hot reload on a per-character section affects only that
bot.

```json
{
  "AccountName": "PROD001",
  "Loadout": { "TargetLevel": 60, ... },
  "AssignedActivity": "Quest[Westfall]",
  "ProgressionPriority": "default",
  "OverrideMetrics": { "HighCardinalityDebugMetrics": true }
}
```

## Hot-reload UI flow

See [`Spec/09_UI.md`](09_UI.md) "Hot reload flow (UI side)".

## Backward compatibility

Bare-array roster configs (legacy) load as `Mode = Test` per
[`Spec/02_STATEMANAGER.md`](02_STATEMANAGER.md). Hot reload on a bare
array is supported: the loader detects the JSON root shape.

## Existing code anchors

| Concept | File |
|---|---|
| Settings loader | `Services/WoWStateManager/Settings/StateManagerSettings.cs` |
| Config schema (to be added) | `Config/schema/*.schema.json` |
| Hot reload broadcaster (to be added) | `Services/WoWStateManager/Settings/HotReloadBroadcaster.cs` |
| File watcher (to be added) | `Services/WoWStateManager/Settings/ConfigFileWatcher.cs` |
| Subscriber interface (to be added) | `Exports/GameData.Core/Configuration/IConfigSubscriber.cs` |
