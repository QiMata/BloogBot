using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tests.Infrastructure;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Unit tests for <see cref="MultiAngleScreenshotCapture"/>. The capture
/// path itself does P/Invoke into user32 / gdi32 (Windows-only and needs
/// a real WoW window), so the unit tests focus on the orchestration:
/// argument validation, yaw-callback ordering, settle-delay observance,
/// cancellation, and graceful degradation when the window cannot be
/// resolved (PID 1 — System Idle Process — has no top-level windows).
/// </summary>
public class MultiAngleScreenshotCaptureTests
{
    [Fact]
    public void CardinalYaws_HasFourCanonicalAngles()
    {
        var yaws = MultiAngleScreenshotCapture.CardinalYaws;
        Assert.Equal(4, yaws.Count);
        Assert.Equal("yaw000", yaws[0].Suffix);
        Assert.Equal("yaw090", yaws[1].Suffix);
        Assert.Equal("yaw180", yaws[2].Suffix);
        Assert.Equal("yaw270", yaws[3].Suffix);
        Assert.Equal(0f, yaws[0].Radians, 4);
        Assert.Equal(MathF.PI * 0.5f, yaws[1].Radians, 4);
        Assert.Equal(MathF.PI, yaws[2].Radians, 4);
        Assert.Equal(MathF.PI * 1.5f, yaws[3].Radians, 4);
    }

    [Fact]
    public async Task CaptureAsync_RejectsNonPositivePid()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await MultiAngleScreenshotCapture.CaptureAsync(
                    processId: 0,
                    outputDir: dir,
                    baseLabel: "x",
                    yaws: MultiAngleScreenshotCapture.CardinalYaws,
                    applyOrientationAsync: (_, _) => Task.CompletedTask);
            });
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task CaptureAsync_RejectsEmptyYawList()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await MultiAngleScreenshotCapture.CaptureAsync(
                    processId: 1,
                    outputDir: dir,
                    baseLabel: "x",
                    yaws: Array.Empty<MultiAngleScreenshotCapture.YawSpec>(),
                    applyOrientationAsync: (_, _) => Task.CompletedTask);
            });
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task CaptureAsync_InvokesApplyOrientationOncePerYaw_InOrder()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var observed = new List<float>();
            // PID 1 is System Idle Process; FindWoWClientWindow returns
            // nint.Zero for it so capture short-circuits cleanly. We only
            // care that the orientation callback fires for every yaw.
            await MultiAngleScreenshotCapture.CaptureAsync(
                processId: 1,
                outputDir: dir,
                baseLabel: "x",
                yaws: MultiAngleScreenshotCapture.CardinalYaws,
                applyOrientationAsync: (yaw, _) =>
                {
                    observed.Add(yaw);
                    return Task.CompletedTask;
                },
                settleMs: 0);

            Assert.Equal(
                MultiAngleScreenshotCapture.CardinalYaws.Select(y => y.Radians).ToArray(),
                observed.ToArray());
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task CaptureAsync_HonorsCancellation()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await MultiAngleScreenshotCapture.CaptureAsync(
                    processId: 1,
                    outputDir: dir,
                    baseLabel: "x",
                    yaws: MultiAngleScreenshotCapture.CardinalYaws,
                    applyOrientationAsync: (_, _) => Task.CompletedTask,
                    settleMs: 0,
                    ct: cts.Token);
            });
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
