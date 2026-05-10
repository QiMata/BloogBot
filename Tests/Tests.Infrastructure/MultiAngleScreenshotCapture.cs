using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Infrastructure;

/// <summary>
/// PFS-OVERHAUL-006 (2026-05-10) — captures the same WoW client window
/// from multiple yaw angles, taking advantage of WoW's chase camera
/// trailing the player's facing. The caller supplies an
/// <paramref name="applyOrientationAsync"/> delegate that issues
/// <c>.go xyzo X Y Z &lt;orientationRad&gt; mapId</c> via the bot-chat
/// channel; this helper drives the four cardinal yaw angles plus an
/// optional caller-supplied yaw set, settles briefly between each, and
/// captures the visible WoW window via <see cref="WindowCapture"/>.
///
/// No PowerShell shim, no Lua bridge — re-orienting the player IS the
/// camera change, since WoW's default chase camera follows player facing.
/// </summary>
public static class MultiAngleScreenshotCapture
{
    public readonly record struct YawSpec(string Suffix, float Radians);

    /// <summary>
    /// The four cardinal angles. Naming uses the conventional WoW radians
    /// where 0 = facing east (+X / +1, 0). Suffix encodes degrees so file
    /// listings sort naturally.
    /// </summary>
    public static readonly IReadOnlyList<YawSpec> CardinalYaws = new[]
    {
        new YawSpec("yaw000", 0f),
        new YawSpec("yaw090", MathF.PI * 0.5f),
        new YawSpec("yaw180", MathF.PI),
        new YawSpec("yaw270", MathF.PI * 1.5f),
    };

    /// <summary>
    /// For each yaw in <paramref name="yaws"/>, calls
    /// <paramref name="applyOrientationAsync"/> to re-orient the bot, waits
    /// <paramref name="settleMs"/>, then captures the WoW client window
    /// for the resolved PID into <paramref name="outputDir"/>. Returns the
    /// list of paths written. Best-effort — a failed capture returns an
    /// empty path entry rather than throwing, so a single bad angle does
    /// not abort the rest of the run.
    /// </summary>
    /// <param name="processId">Resolved WoW.exe PID for the account being captured.</param>
    /// <param name="outputDir">Pre-created output directory.</param>
    /// <param name="baseLabel">Sanitized label for filenames; the yaw suffix is appended.</param>
    /// <param name="yaws">Yaw angles to drive (caller may extend <see cref="CardinalYaws"/>).</param>
    /// <param name="applyOrientationAsync">Async callback that issues <c>.go xyzo</c> with the requested orientation.</param>
    /// <param name="settleMs">Milliseconds to wait between orientation change and capture.</param>
    public static async Task<IReadOnlyList<string>> CaptureAsync(
        int processId,
        string outputDir,
        string baseLabel,
        IReadOnlyList<YawSpec> yaws,
        Func<float, CancellationToken, Task> applyOrientationAsync,
        int settleMs = 500,
        CancellationToken ct = default)
    {
        if (processId <= 0) throw new ArgumentException("processId must be > 0", nameof(processId));
        if (string.IsNullOrWhiteSpace(outputDir)) throw new ArgumentException("outputDir must be non-empty", nameof(outputDir));
        if (string.IsNullOrWhiteSpace(baseLabel)) throw new ArgumentException("baseLabel must be non-empty", nameof(baseLabel));
        if (yaws == null || yaws.Count == 0) throw new ArgumentException("yaws must contain at least one angle", nameof(yaws));
        if (applyOrientationAsync == null) throw new ArgumentNullException(nameof(applyOrientationAsync));

        Directory.CreateDirectory(outputDir);
        var captured = new List<string>(yaws.Count);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");

        foreach (var yaw in yaws)
        {
            ct.ThrowIfCancellationRequested();
            await applyOrientationAsync(yaw.Radians, ct).ConfigureAwait(false);
            if (settleMs > 0) await Task.Delay(settleMs, ct).ConfigureAwait(false);

            var hwnd = WindowCapture.FindWoWClientWindow(processId);
            if (hwnd == nint.Zero)
            {
                // No window for this PID — record nothing rather than throw.
                continue;
            }
            var path = Path.Combine(outputDir, $"{baseLabel}-{yaw.Suffix}-{timestamp}.png");
            if (WindowCapture.CaptureWindow(hwnd, path))
                captured.Add(path);
        }

        return captured;
    }
}
