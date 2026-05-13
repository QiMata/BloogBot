namespace GameData.Core.Enums;

/// <summary>
/// Canonical failure reasons. Every failure path normalizes to one of
/// these values before it touches metrics or logs — stable time-series
/// require stable reason labels.
///
/// Source of truth: <c>docs/Spec/12_ERROR_TAXONOMY.md</c>. Never invent
/// a new reason in code; add it to the doc first, then mirror here.
/// </summary>
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
