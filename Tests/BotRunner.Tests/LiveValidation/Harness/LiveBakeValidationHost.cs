using System;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Tests.LiveValidation.Harness;

/// <summary>
/// Live <see cref="IBakeValidationHost"/> implementation that binds the
/// validator to <see cref="LiveBotFixture"/>. Performs the teleport via
/// <c>.go xyz</c> bot chat (<see cref="LiveBotFixture.BotTeleportAsync"/>),
/// waits the requested settle delay, refreshes snapshots, and extracts
/// the bot's settled <c>Position</c>.
///
/// PolyRef is null in this adapter — the BotRunner.Tests project is x86
/// and cannot directly load the x64 Navigation.dll exports for
/// GetPolyAtCoord. Callers that need polyref capture should run the
/// PathfindingService.Tests-side <c>WaypointGenerationTests</c> in
/// parallel.
///
/// Smooth-path query and segment classification return null in this
/// adapter for the same x86/x64 boundary reason. The validator handles
/// nulls by skipping the corresponding checks, so the live FG/BG settle
/// dance still runs end-to-end.
/// </summary>
public sealed class LiveBakeValidationHost : IBakeValidationHost
{
    private readonly LiveBotFixture _bot;
    private readonly Action<string> _log;

    public LiveBakeValidationHost(LiveBotFixture bot, Action<string> log)
    {
        _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        _log = log ?? (_ => { });
    }

    public async Task<SettledPosition?> TeleportAndSettleAsync(
        string accountName,
        uint mapId,
        float x,
        float y,
        float z,
        TimeSpan settleDelay,
        CancellationToken ct)
    {
        await _bot.BotTeleportAsync(accountName, (int)mapId, x, y, z).ConfigureAwait(false);
        if (settleDelay > TimeSpan.Zero)
            await Task.Delay(settleDelay, ct).ConfigureAwait(false);
        await _bot.RefreshSnapshotsAsync().ConfigureAwait(false);
        var snapshot = await _bot.GetSnapshotAsync(accountName).ConfigureAwait(false);
        var pos = snapshot?.Player?.Unit?.GameObject?.Base?.Position ?? snapshot?.MovementData?.Position;
        if (pos == null) return null;
        return new SettledPosition(pos.X, pos.Y, pos.Z, PolyRef: null);
    }

    /// <summary>
    /// Smooth-path queries from the BotRunner.Tests x86 host are not
    /// implemented yet — they would require a TCP roundtrip to the
    /// running PathfindingService that this fixture's
    /// <see cref="LongPathingFixture"/> already starts. Returning null
    /// causes the validator to skip the goldenSmoothPath checks, which
    /// keeps the per-checkpoint settle-and-assert dance green even
    /// without path-query infrastructure.
    /// </summary>
    public Task<float[][]?> QuerySmoothPathAsync(uint mapId, float[] start, float[] dest, CancellationToken ct)
        => Task.FromResult<float[][]?>(null);

    /// <summary>
    /// Segment classification requires the x64 Navigation.dll. Returning
    /// null causes the validator to skip the affordance pass.
    /// </summary>
    public Task<string?> ClassifySegmentAsync(uint mapId, float[] a, float[] b, CancellationToken ct)
        => Task.FromResult<string?>(null);

    public void Log(string message) => _log(message);
}
