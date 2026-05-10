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

    /// <summary>
    /// Capture multi-angle screenshots of the bot at its current settled
    /// position. Implementations re-orient the player to each cardinal yaw
    /// (and any caller-supplied additional angles) and grab the WoW client
    /// window for the given account. Returns an empty list when the host
    /// does not implement screenshot capture (e.g., the unit-test mock).
    ///
    /// The validator calls this after every <c>expectedWalkable</c> /
    /// <c>expectedHoles</c> checkpoint settle so each fixture run produces
    /// a visual record of where the bot ended up at every angle.
    /// </summary>
    Task<IReadOnlyList<string>> CaptureMultiAngleAsync(
        string accountName,
        string baseLabel,
        uint mapId,
        float settledX,
        float settledY,
        float settledZ,
        string outputDir,
        CancellationToken ct);

    void Log(string message);
}

public sealed record SettledPosition(
    float X,
    float Y,
    float Z,
    ulong? PolyRef);
