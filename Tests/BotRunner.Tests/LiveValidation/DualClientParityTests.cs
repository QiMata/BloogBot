using System;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Dual-client parity tests — runs the same operation on BOTH FG (injected)
/// and BG (headless) bots, then compares results. Validates that BG packet-based
/// implementations match FG Lua/memory-based implementations.
///
/// Each test:
///   1. Teleports both bots to the same location
///   2. Performs the same action on both
///   3. Asserts both produce equivalent snapshots
///
/// Uses the standard LiveBotFixture (TESTBOT1=FG, TESTBOT2=BG).
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~DualClientParityTests" -v n
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class DualClientParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    // Orgrimmar safe zone
    private const int MapId = 1;
    private const float OrgX = 1629.4f;
    private const float OrgY = -4373.4f;
    private const float OrgZ = 34.2f;

    public DualClientParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// Both FG and BG bots should detect the same nearby units after teleporting
    /// to the same position. Validates ObjectManager parity.
    /// </summary>
    [SkippableFact]
    public async Task NearbyUnits_BothBotsDetectSameUnits()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName!;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrEmpty(fgAccount), "FG bot not available");

        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        await _bot.EnsureCleanSlateAsync(fgAccount, "FG");

        // Teleport both to same location
        await _bot.BotTeleportAsync(bgAccount, MapId, OrgX, OrgY, OrgZ);
        await _bot.BotTeleportAsync(fgAccount, MapId, OrgX, OrgY, OrgZ);
        await _bot.WaitForTeleportSettledAsync(bgAccount, OrgX, OrgY);
        await _bot.WaitForTeleportSettledAsync(fgAccount, OrgX, OrgY);
        await Task.Delay(2000); // Let both ObjectManagers populate

        await _bot.RefreshSnapshotsAsync();
        var bgSnap = _bot.BackgroundBot;
        var fgSnap = _bot.ForegroundBot;

        Assert.NotNull(bgSnap);
        Assert.NotNull(fgSnap);

        var bgUnitCount = bgSnap.NearbyUnits?.Count ?? 0;
        var fgUnitCount = fgSnap.NearbyUnits?.Count ?? 0;

        _output.WriteLine($"BG nearby units: {bgUnitCount}");
        _output.WriteLine($"FG nearby units: {fgUnitCount}");

        // Both should detect units (exact count may vary due to timing)
        Assert.True(bgUnitCount > 0, "BG bot should detect nearby units");
        Assert.True(fgUnitCount > 0, "FG bot should detect nearby units");

        // Counts should be within 50% of each other
        var ratio = (double)Math.Min(bgUnitCount, fgUnitCount) / Math.Max(bgUnitCount, fgUnitCount);
        _output.WriteLine($"Unit count ratio: {ratio:P0}");
        Assert.True(ratio > 0.5, $"Unit counts too different: BG={bgUnitCount}, FG={fgUnitCount}");
    }

    /// <summary>
    /// Both bots should report the same map ID and similar positions after teleport.
    /// </summary>
    [SkippableFact]
    public async Task Position_BothBotsAgreeOnMapAndLocation()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName!;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrEmpty(fgAccount), "FG bot not available");

        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        await _bot.EnsureCleanSlateAsync(fgAccount, "FG");

        await _bot.BotTeleportAsync(bgAccount, MapId, OrgX, OrgY, OrgZ);
        await _bot.BotTeleportAsync(fgAccount, MapId, OrgX, OrgY, OrgZ);
        var bgSettled = await _bot.WaitForTeleportSettledAsync(
            bgAccount,
            OrgX,
            OrgY,
            timeoutMs: 8000,
            progressLabel: "BG dual-parity-position",
            xyToleranceYards: 10f);
        var fgSettled = await _bot.WaitForTeleportSettledAsync(
            fgAccount,
            OrgX,
            OrgY,
            timeoutMs: 8000,
            progressLabel: "FG dual-parity-position",
            xyToleranceYards: 10f);
        Assert.True(bgSettled, "BG bot should settle near the shared parity location.");
        Assert.True(fgSettled, "FG bot should settle near the shared parity location.");

        await _bot.RefreshSnapshotsAsync();
        var bgSnap = _bot.BackgroundBot;
        var fgSnap = _bot.ForegroundBot;

        var bgMapId = bgSnap?.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
        var fgMapId = fgSnap?.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
        var bgPos = bgSnap?.Player?.Unit?.GameObject?.Base?.Position;
        var fgPos = fgSnap?.Player?.Unit?.GameObject?.Base?.Position;

        _output.WriteLine($"BG: map={bgMapId}, pos=({bgPos?.X:F1},{bgPos?.Y:F1},{bgPos?.Z:F1})");
        _output.WriteLine($"FG: map={fgMapId}, pos=({fgPos?.X:F1},{fgPos?.Y:F1},{fgPos?.Z:F1})");

        Assert.Equal((uint)MapId, bgMapId);
        Assert.Equal((uint)MapId, fgMapId);
        Assert.NotNull(bgPos);
        Assert.NotNull(fgPos);

        // Positions should be within 10y after teleport + settle
        var dist = Math.Sqrt(
            Math.Pow(bgPos.X - fgPos.X, 2) +
            Math.Pow(bgPos.Y - fgPos.Y, 2) +
            Math.Pow(bgPos.Z - fgPos.Z, 2));
        _output.WriteLine($"Position delta: {dist:F1}y");
        Assert.True(dist < 10, $"Bots too far apart: {dist:F1}y");
    }

    /// <summary>
    /// Both bots should have spells in their spell list after setup.
    /// </summary>
    [SkippableFact]
    public async Task SpellList_BothBotsHaveSpells()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName!;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrEmpty(fgAccount), "FG bot not available");

        await _bot.RefreshSnapshotsAsync();
        var bgSnap = _bot.BackgroundBot;
        var fgSnap = _bot.ForegroundBot;

        var bgSpellCount = bgSnap?.Player?.SpellList?.Count ?? 0;
        var fgSpellCount = fgSnap?.Player?.SpellList?.Count ?? 0;

        _output.WriteLine($"BG spells: {bgSpellCount}");
        _output.WriteLine($"FG spells: {fgSpellCount}");

        Assert.True(bgSpellCount > 0, "BG bot should have spells");
        Assert.True(fgSpellCount > 0, "FG bot should have spells");
    }

    /// <summary>
    /// Both bots should report health and max health consistently.
    /// </summary>
    [SkippableFact]
    public async Task Health_BothBotsReportValidHealth()
    {
        await _bot.RefreshSnapshotsAsync();
        var bgSnap = _bot.BackgroundBot;
        var fgSnap = _bot.ForegroundBot;

        global::Tests.Infrastructure.Skip.If(fgSnap == null, "FG bot not available");

        var bgHealth = bgSnap?.Player?.Unit?.Health ?? 0;
        var bgMaxHealth = bgSnap?.Player?.Unit?.MaxHealth ?? 0;
        var fgHealth = fgSnap?.Player?.Unit?.Health ?? 0;
        var fgMaxHealth = fgSnap?.Player?.Unit?.MaxHealth ?? 0;

        _output.WriteLine($"BG health: {bgHealth}/{bgMaxHealth}");
        _output.WriteLine($"FG health: {fgHealth}/{fgMaxHealth}");

        Assert.True(bgMaxHealth > 0, "BG bot should have max health > 0");
        Assert.True(fgMaxHealth > 0, "FG bot should have max health > 0");
        Assert.True(bgHealth <= bgMaxHealth, "BG health should not exceed max");
        Assert.True(fgHealth <= fgMaxHealth, "FG health should not exceed max");
    }

    /// <summary>
    /// Both bots should be able to send a chat command and have it processed.
    /// Tests the GM command pipeline parity.
    /// </summary>
    [SkippableFact]
    public async Task GmCommand_BothBotsCanExecuteCommands()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName!;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrEmpty(fgAccount), "FG bot not available");

        // Both bots send a harmless GM command
        await _bot.SendGmChatCommandAsync(bgAccount, ".targetself");
        await _bot.SendGmChatCommandAsync(fgAccount, ".targetself");
        await Task.Delay(500);

        // Both should still be functional after the command
        await _bot.RefreshSnapshotsAsync();
        var bgSnap = _bot.BackgroundBot;
        var fgSnap = _bot.ForegroundBot;

        Assert.True(bgSnap?.IsObjectManagerValid ?? false, "BG ObjectManager should be valid after GM command");
        Assert.True(fgSnap?.IsObjectManagerValid ?? false, "FG ObjectManager should be valid after GM command");
    }
}
