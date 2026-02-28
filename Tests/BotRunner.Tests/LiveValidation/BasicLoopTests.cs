using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Basic loop validation with snapshot-driven assertions.
///
/// Scope:
/// - In-world login state and identity data.
/// - Physics stability (no falling through world).
/// - Teleport behavior and movement-flag reset checks.
/// - Nearby unit/object visibility in populated locations.
/// - Level field update/read path with chat-first setup.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class BasicLoopTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int DurotarMapId = 1;
    private const float RazorHillX = 326.81f;
    private const float RazorHillY = -4706.65f;
    private const float RazorHillZ = 15.37f;
    private const float RazorHillArrivalRadius = 35f;
    private const uint MoveFlagForward = 0x00000001;

    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

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
        Assert.True(IsStrictAlive(bg), "BG should be strict-alive at basic-loop login check.");

        var bgPos = bg.Player!.Unit!.GameObject!.Base!.Position!;
        _output.WriteLine($"BG Bot: {bg.CharacterName} ({bg.AccountName}) GUID=0x{bg.Player.Unit.GameObject.Base.Guid:X}");
        _output.WriteLine($"  Position: ({bgPos.X:F2}, {bgPos.Y:F2}, {bgPos.Z:F2})");
        _output.WriteLine($"  HP: {bg.Player.Unit.Health}/{bg.Player.Unit.MaxHealth}");

        if (_bot.ForegroundBot != null)
        {
            var fg = _bot.ForegroundBot;
            Assert.Equal("InWorld", fg.ScreenState);
            Assert.False(string.IsNullOrWhiteSpace(fg.CharacterName));
            Assert.False(string.IsNullOrWhiteSpace(fg.AccountName));
            Assert.NotEqual(0UL, fg.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL);
            Assert.NotNull(fg.Player?.Unit?.GameObject?.Base?.Position);
            Assert.True(IsStrictAlive(fg), "FG should be strict-alive at basic-loop login check.");

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
        await EnsureStrictAliveAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();

        var bgPos = _bot.BackgroundBot?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(bgPos);
        var initialZ = bgPos.Z;
        _output.WriteLine($"BG initial Z: {initialZ:F2}");

        var (stable, finalZ) = await _bot.WaitForZStabilizationAsync(_bot.BgAccountName, waitMs: 6000);
        _output.WriteLine($"BG final Z: {finalZ:F2}, stable={stable}, delta={Math.Abs(finalZ - initialZ):F2}");

        Assert.True(finalZ > -500, $"BG physics broken: Z={finalZ:F2} below world floor threshold.");
        Assert.True(stable, $"BG Z failed to stabilize: start={initialZ:F2}, end={finalZ:F2}.");

        if (_bot.ForegroundBot != null)
        {
            await EnsureStrictAliveAsync(_bot.FgAccountName!, "FG");
            var (fgStable, fgFinalZ) = await _bot.WaitForZStabilizationAsync(_bot.FgAccountName, waitMs: 6000);
            _output.WriteLine($"FG final Z: {fgFinalZ:F2}, stable={fgStable}");
            Assert.True(fgFinalZ > -500, $"FG physics broken: Z={fgFinalZ:F2} below world floor threshold.");
            Assert.True(fgStable, $"FG Z failed to stabilize: end={fgFinalZ:F2}.");
        }
    }

    [SkippableFact]
    public async Task Teleport_PlayerMovesToNewPosition()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        await EnsureStrictAliveAsync(bgAccount, "BG");

        await _bot.RefreshSnapshotsAsync();
        var beforeBg = await _bot.GetSnapshotAsync(bgAccount);
        var beforeBgPos = beforeBg?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(beforeBgPos);
        var bgDistanceBefore = Distance2D(beforeBgPos!.X, beforeBgPos.Y, RazorHillX, RazorHillY);

        var bgMoved = await TeleportAndVerifyAsync(bgAccount, "BG");
        Assert.True(bgMoved, "BG should arrive near Razor Hill after teleport command.");

        var afterBg = await _bot.GetSnapshotAsync(bgAccount);
        var afterBgPos = afterBg?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(afterBgPos);
        var bgStep = Distance2D(beforeBgPos.X, beforeBgPos.Y, afterBgPos!.X, afterBgPos.Y);
        _output.WriteLine($"[BG] Teleport displacement: {bgStep:F1}y (distanceBefore={bgDistanceBefore:F1}y)");
        if (bgDistanceBefore > RazorHillArrivalRadius)
            Assert.True(bgStep > 5f, "BG teleport should move position by more than a trivial amount when not already near target.");

        if (_bot.ForegroundBot != null)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            await EnsureStrictAliveAsync(fgAccount, "FG");

            await _bot.RefreshSnapshotsAsync();
            var beforeFg = await _bot.GetSnapshotAsync(fgAccount);
            var beforeFgPos = beforeFg?.Player?.Unit?.GameObject?.Base?.Position;
            Assert.NotNull(beforeFgPos);
            var fgDistanceBefore = Distance2D(beforeFgPos!.X, beforeFgPos.Y, RazorHillX, RazorHillY);

            var fgMoved = await TeleportAndVerifyAsync(fgAccount, "FG");
            Assert.True(fgMoved, "FG should arrive near Razor Hill after teleport command.");

            var afterFg = await _bot.GetSnapshotAsync(fgAccount);
            var afterFgPos = afterFg?.Player?.Unit?.GameObject?.Base?.Position;
            Assert.NotNull(afterFgPos);
            var fgStep = Distance2D(beforeFgPos.X, beforeFgPos.Y, afterFgPos!.X, afterFgPos.Y);
            _output.WriteLine($"[FG] Teleport displacement: {fgStep:F1}y (distanceBefore={fgDistanceBefore:F1}y)");
            if (fgDistanceBefore > RazorHillArrivalRadius)
                Assert.True(fgStep > 5f, "FG teleport should move position by more than a trivial amount when not already near target.");
        }
    }

    [SkippableFact]
    public async Task Snapshot_SeesNearbyUnits()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        await EnsureStrictAliveAsync(bgAccount, "BG");
        await EnsureNearRazorHillAsync(bgAccount, "BG");

        var units = await WaitForNearbyUnitsAsync(bgAccount, TimeSpan.FromSeconds(10));
        _output.WriteLine($"BG visible units in snapshot: {units.Count}");

        foreach (var unit in units.Take(10))
        {
            var pos = unit.GameObject?.Base?.Position;
            _output.WriteLine($"  [GUID=0x{unit.GameObject?.Base?.Guid:X}] L{unit.GameObject?.Level} HP={unit.Health}/{unit.MaxHealth} ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");
        }

        Assert.True(units.Count > 0, "BG snapshot should see nearby units at Razor Hill.");
    }

    [SkippableFact]
    public async Task Snapshot_SeesNearbyGameObjects()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        await EnsureStrictAliveAsync(bgAccount, "BG");
        await EnsureNearRazorHillAsync(bgAccount, "BG");

        var objects = await WaitForNearbyObjectsAsync(bgAccount, TimeSpan.FromSeconds(10));
        _output.WriteLine($"BG visible game objects in snapshot: {objects.Count}");

        foreach (var go in objects.Take(10))
        {
            var pos = go.Base?.Position;
            _output.WriteLine($"  [GUID=0x{go.Base?.Guid:X}] ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");
        }

        Assert.True(objects.Count > 0, "BG snapshot should see nearby game objects at Razor Hill.");
    }

    [SkippableFact]
    public async Task SetLevel_ChangesPlayerLevel()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        await EnsureStrictAliveAsync(bgAccount, "BG");

        await _bot.RefreshSnapshotsAsync();
        var baseline = await _bot.GetSnapshotAsync(bgAccount);
        var baselineLevel = baseline?.Player?.Unit?.GameObject?.Level ?? 0;
        _output.WriteLine($"BG level before setup: {baselineLevel}");

        if (baselineLevel < 10)
        {
            var command = $".character level {baseline?.CharacterName} 10";
            var trace = await _bot.SendGmChatCommandTrackedAsync(bgAccount, command, captureResponse: true, delayMs: 1200);
            var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
            if (trace.DispatchResult != ResponseResult.Success || rejected)
            {
                _output.WriteLine("BG chat level command rejected; falling back to SOAP level setup.");
                await _bot.SetLevelAsync(baseline?.CharacterName, 10);
            }
        }

        var levelSet = await WaitForLevelAtLeastAsync(bgAccount, 10, TimeSpan.FromSeconds(10));
        Assert.True(levelSet, "BG level should be >= 10 after setup.");

        var after = await _bot.GetSnapshotAsync(bgAccount);
        var levelAfter = after?.Player?.Unit?.GameObject?.Level ?? 0;
        _output.WriteLine($"BG level after setup: {levelAfter}");
        Assert.True(levelAfter >= 10, "BG level should be readable and >= 10.");
    }

    private async Task<bool> TeleportAndVerifyAsync(string account, string label)
    {
        await _bot.BotTeleportAsync(account, DurotarMapId, RazorHillX, RazorHillY, RazorHillZ);

        var arrived = await WaitForNearPositionAsync(account, RazorHillX, RazorHillY, RazorHillArrivalRadius, TimeSpan.FromSeconds(12));
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        var movementFlags = snap?.Player?.Unit?.MovementFlags ?? snap?.MovementData?.MovementFlags ?? 0;

        _output.WriteLine($"[{label}] Post-teleport pos: ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1}), moveFlags=0x{movementFlags:X}");

        var (stable, finalZ) = await _bot.WaitForZStabilizationAsync(account, waitMs: 6000);
        _output.WriteLine($"[{label}] Post-teleport Z stable={stable}, finalZ={finalZ:F2}");
        Assert.True(finalZ > -500, $"{label}: physics broken after teleport (Z={finalZ:F2}).");
        Assert.True(stable, $"{label}: post-teleport Z did not stabilize.");
        Assert.Equal(0u, movementFlags & MoveFlagForward);

        return arrived;
    }

    private async Task EnsureNearRazorHillAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null)
            return;

        var distance = Distance2D(pos.X, pos.Y, RazorHillX, RazorHillY);
        if (distance <= RazorHillArrivalRadius)
        {
            _output.WriteLine($"[{label}] Already near Razor Hill (distance={distance:F1}y); skipping teleport.");
            return;
        }

        _output.WriteLine($"[{label}] Not near Razor Hill (distance={distance:F1}y); teleporting.");
        var moved = await TeleportAndVerifyAsync(account, label);
        Assert.True(moved, $"{label}: failed to arrive near Razor Hill during setup.");
    }

    private async Task EnsureStrictAliveAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (IsStrictAlive(snap))
            return;

        var characterName = snap?.CharacterName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(characterName), $"{label}: missing character name for revive setup.");

        _output.WriteLine($"[{label}] Not strict-alive; reviving for setup.");
        await _bot.RevivePlayerAsync(characterName!);

        var restored = await WaitForStrictAliveAsync(account, TimeSpan.FromSeconds(15));
        global::Tests.Infrastructure.Skip.If(!restored, $"{label}: failed to restore strict-alive setup state.");
    }

    private async Task<bool> WaitForStrictAliveAsync(string account, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (IsStrictAlive(snap))
                return true;
            await Task.Delay(400);
        }

        return false;
    }

    private async Task<bool> WaitForLevelAtLeastAsync(string account, uint level, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var current = snap?.Player?.Unit?.GameObject?.Level ?? 0;
            if (current >= level)
                return true;
            await Task.Delay(400);
        }

        return false;
    }

    private async Task<bool> WaitForNearPositionAsync(string account, float x, float y, float radius, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos != null && Distance2D(pos.X, pos.Y, x, y) <= radius)
                return true;
            await Task.Delay(400);
        }

        return false;
    }

    private async Task<System.Collections.Generic.IReadOnlyList<Game.WoWUnit>> WaitForNearbyUnitsAsync(string account, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var units = snap?.NearbyUnits;
            if (units != null && units.Count > 0)
                return units;
            await Task.Delay(500);
        }

        return Array.Empty<Game.WoWUnit>();
    }

    private async Task<System.Collections.Generic.IReadOnlyList<Game.WoWGameObject>> WaitForNearbyObjectsAsync(string account, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var objects = snap?.NearbyObjects;
            if (objects != null && objects.Count > 0)
                return objects;
            await Task.Delay(500);
        }

        return Array.Empty<Game.WoWGameObject>();
    }

    private static float Distance2D(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool IsStrictAlive(WoWActivitySnapshot? snap)
    {
        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        var hasGhostFlag = (player.PlayerFlags & PlayerFlagGhost) != 0;
        var standState = unit.Bytes1 & StandStateMask;
        return unit.Health > 0 && !hasGhostFlag && standState != StandStateDead;
    }

}
