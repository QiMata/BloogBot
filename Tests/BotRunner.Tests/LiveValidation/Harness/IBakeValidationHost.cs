using System;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Tests.LiveValidation.Harness;

/// <summary>
/// Test-time abstraction for the few async operations
/// <see cref="WaypointSettleValidator"/> needs from the live fixture
/// (teleport, settle, snapshot, smooth-path query, segment classification).
/// Allows the validator's logic to be unit-tested with an in-memory mock,
/// while the live fixture wires it through to <c>LiveBotFixture</c> +
/// <c>PathfindingClient</c>.
/// </summary>
public interface IBakeValidationHost
{
    /// <summary>
    /// Teleport the named bot, wait for the given settle delay, refresh
    /// snapshots, and return the bot's settled position. Returns null if
    /// the bot is offline or the snapshot has no position.
    /// </summary>
    Task<SettledPosition?> TeleportAndSettleAsync(
        string accountName,
        uint mapId,
        float x,
        float y,
        float z,
        TimeSpan settleDelay,
        CancellationToken ct);

    /// <summary>
    /// Query a smooth path between two world coords. Returns null if
    /// path-query infrastructure isn't available in the current host
    /// (e.g., a unit-test mock that only validates settle behavior).
    /// </summary>
    Task<float[][]?> QuerySmoothPathAsync(
        uint mapId,
        float[] start,
        float[] dest,
        CancellationToken ct);

    /// <summary>
    /// Classify a single (a → b) segment using the runtime physics
    /// affordance classifier. Returns null when classification isn't
    /// available; callers treat null as "skip this check".
    /// </summary>
    Task<string?> ClassifySegmentAsync(
        uint mapId,
        float[] a,
        float[] b,
        CancellationToken ct);

    void Log(string message);
}

public sealed record SettledPosition(
    float X,
    float Y,
    float Z,
    ulong? PolyRef);
