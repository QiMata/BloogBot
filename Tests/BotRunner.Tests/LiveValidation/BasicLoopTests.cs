using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Minimal live-validation health checks that stay useful after the overhaul:
/// fixture readiness and post-teleport physics stability.
/// See docs/BasicLoopTests.md for the owning production code paths.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class BasicLoopTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public BasicLoopTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task LoginAndEnterWorld_BothBotsPresent()
    {
        await _bot.RefreshSnapshotsAsync();

        var bg = _bot.BackgroundBot;
        Assert.NotNull(bg);
        Assert.Equal("InWorld", bg.ScreenState);
        Assert.False(string.IsNullOrWhiteSpace(bg.CharacterName));
        Assert.False(string.IsNullOrWhiteSpace(bg.AccountName));
        Assert.NotEqual(0UL, bg.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL);
        Assert.NotNull(bg.Player?.Unit?.GameObject?.Base?.Position);
        Assert.True(LiveBotFixture.IsStrictAlive(bg), "BG should be strict-alive at basic-loop login check.");

        var bgPos = bg.Player!.Unit!.GameObject!.Base!.Position!;
        _output.WriteLine($"BG Bot: {bg.CharacterName} ({bg.AccountName}) GUID=0x{bg.Player.Unit.GameObject.Base.Guid:X}");
        _output.WriteLine($"  Position: ({bgPos.X:F2}, {bgPos.Y:F2}, {bgPos.Z:F2})");
        _output.WriteLine($"  HP: {bg.Player.Unit.Health}/{bg.Player.Unit.MaxHealth}");

        if (_bot.IsFgActionable)
        {
            var fg = _bot.ForegroundBot;
            Assert.NotNull(fg);
            Assert.Equal("InWorld", fg.ScreenState);
            Assert.False(string.IsNullOrWhiteSpace(fg.CharacterName));
            Assert.False(string.IsNullOrWhiteSpace(fg.AccountName));
            Assert.NotEqual(0UL, fg.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL);
            Assert.NotNull(fg.Player?.Unit?.GameObject?.Base?.Position);
            Assert.True(LiveBotFixture.IsStrictAlive(fg), "FG should be strict-alive at basic-loop login check.");

            var fgPos = fg.Player!.Unit!.GameObject!.Base!.Position!;
            _output.WriteLine($"FG Bot: {fg.CharacterName} ({fg.AccountName}) GUID=0x{fg.Player.Unit.GameObject.Base.Guid:X}");
            _output.WriteLine($"  Position: ({fgPos.X:F2}, {fgPos.Y:F2}, {fgPos.Z:F2})");
            _output.WriteLine($"  HP: {fg.Player.Unit.Health}/{fg.Player.Unit.MaxHealth}");
        }
        else
        {
            _output.WriteLine("FG Bot: NOT AVAILABLE (WoW.exe not running or injection failed)");
        }
    }

    [SkippableFact]
    public async Task Physics_PlayerNotFallingThroughWorld()
    {
        var setupTasks = new List<Task>
        {
            _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG")
        };

        if (_bot.IsFgActionable)
            setupTasks.Add(_bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG"));

        await Task.WhenAll(setupTasks);
        await _bot.RefreshSnapshotsAsync();

        var bgPos = _bot.BackgroundBot?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(bgPos);
        var initialZ = bgPos.Z;
        _output.WriteLine($"BG initial Z: {initialZ:F2}");

        if (_bot.IsFgActionable)
        {
            _output.WriteLine("[PARITY] Running BG and FG Z-stabilization checks in parallel.");
            var bgStabTask = _bot.WaitForZStabilizationAsync(_bot.BgAccountName, waitMs: 3000);
            var fgStabTask = _bot.WaitForZStabilizationAsync(_bot.FgAccountName, waitMs: 3000);
            await Task.WhenAll(bgStabTask, fgStabTask);

            var (bgStable, bgFinalZ) = await bgStabTask;
            _output.WriteLine($"BG final Z: {bgFinalZ:F2}, stable={bgStable}, delta={Math.Abs(bgFinalZ - initialZ):F2}");
            Assert.True(bgFinalZ > -500, $"BG physics broken: Z={bgFinalZ:F2} below world floor threshold.");
            Assert.True(bgStable, $"BG Z failed to stabilize: start={initialZ:F2}, end={bgFinalZ:F2}.");

            var (fgStable, fgFinalZ) = await fgStabTask;
            _output.WriteLine($"FG final Z: {fgFinalZ:F2}, stable={fgStable}");
            Assert.True(fgFinalZ > -500, $"FG physics broken: Z={fgFinalZ:F2} below world floor threshold.");
            Assert.True(fgStable, $"FG Z failed to stabilize: end={fgFinalZ:F2}.");
        }
        else
        {
            var (stable, finalZ) = await _bot.WaitForZStabilizationAsync(_bot.BgAccountName, waitMs: 3000);
            _output.WriteLine($"BG final Z: {finalZ:F2}, stable={stable}, delta={Math.Abs(finalZ - initialZ):F2}");
            Assert.True(finalZ > -500, $"BG physics broken: Z={finalZ:F2} below world floor threshold.");
            Assert.True(stable, $"BG Z failed to stabilize: start={initialZ:F2}, end={finalZ:F2}.");
        }
    }
}
