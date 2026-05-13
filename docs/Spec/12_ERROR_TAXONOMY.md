# Spec 12 — Error Taxonomy

## Purpose

A small fixed enum to which every failure normalizes before it touches
metrics or logs. Stable time-series come from stable reason labels.

## The enum

```csharp
public enum FailureReason
{
    // --- pathfinding / scene ---
    path_timeout,
    no_path,
    routepack_bypass,
    routepack_invalidated,
    scene_data_missing,
    scene_data_stale,

    // --- transport ---
    transport_missed,
    transport_wrong_entry,
    transport_boarding_failed,
    transport_disembark_failed,

    // --- map transfer ---
    map_transfer_timeout,
    map_transfer_rejected,

    // --- socket / IPC ---
    socket_connect_failed,
    socket_disconnect_expected,
    socket_disconnect_unexpected,
    socket_frame_truncated,
    handshake_mismatch,

    // --- bot lifecycle ---
    login_failed,
    auth_proof_failed,
    realm_select_failed,
    char_select_failed,
    enter_world_failed,
    bot_crash,
    bot_disconnected,

    // --- physics ---
    physics_parity_break,
    physics_underground,
    physics_overhead_snap,
    physics_stuck,

    // --- task execution ---
    task_timeout,
    task_precondition_failed,
    task_cancelled,
    task_unrecoverable,

    // --- activity legality ---
    illegal_activity_request,
    missing_role,
    missing_attunement,
    missing_key,
    missing_flight_path,
    missing_reputation,
    missing_level,
    lockout_active,
    faction_unreachable,

    // --- inventory / gear ---
    inventory_full,
    durability_broken,
    item_unavailable,

    // --- server ---
    server_capability_missing,
    server_unavailable,
    server_rejected,

    // --- catalog ---
    catalog_drift,
    catalog_invalid,
}
```

The enum is in `Exports/GameData.Core/Enums/FailureReason.cs`.

## Mapping rules

- **Map every catch / failure path to one of these** at the boundary
  (the point where the failure becomes a metric or log line).
- **Never invent a new reason** in code; add it to the enum first.
- **`Detail` is the human-readable string.** It does not appear in
  metric labels; it appears in log messages.

```csharp
throw new BotTaskFailedException(
    FailureReason.transport_missed,
    detail: $"Zeppelin OG→UC departed at {ts:HH:mm:ss} before boarding window opened");
```

## Adding a new reason

1. Edit `FailureReason.cs` to add the new value.
2. Update this doc.
3. Add a mapping in any code that previously surfaced this failure as
   a string.
4. Update test coverage to assert the new reason on the expected path.
5. Bump the catalog test that enforces 1:1 mapping between enum and
   doc.

## Reason vs. result

`result` is `success | failure | cancelled | timeout`. `reason` is set
only when `result != success`. Metrics carry both labels.

## Existing code anchors

| Concept | File |
|---|---|
| Failure enum (to be added) | `Exports/GameData.Core/Enums/FailureReason.cs` |
| Exception type (to be added) | `Exports/GameData.Core/Exceptions/BotTaskFailedException.cs` |
| Test enforcement | `Tests/BotRunner.Tests/Spec/FailureReasonCatalogTests.cs` |
