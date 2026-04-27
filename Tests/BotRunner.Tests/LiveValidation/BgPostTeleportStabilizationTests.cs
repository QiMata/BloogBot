using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Regression test for the BG bot post-teleport "double-fall" symptom tracked
/// in <c>docs/handoff_session_bg_movement_parity_followup.md</c>.
///
/// Symptom: a third-party WoW client observing the BG bot after a teleport
/// would see the falling animation play twice — once on local prediction,
/// then again when the authoritative position update arrived showing the bot
/// still in mid-air.
///
/// This test asserts the snapshot-side contract that StateManager observes
/// from the BG bot, independent of the third-party-observer animation issue:
///
///   1. After the bot is teleported a few yards above ground, the
///      <c>MOVEFLAG_FALLINGFAR</c> / <c>MOVEFLAG_JUMPING</c> bits clear
///      within the asserted bound.
///   2. The reported XY position settles inside a tight radius of the
///      teleport target within the same bound.
///
/// If the BG-side ground-snap regresses (e.g. the suppression window in
/// <see cref="MovementController"/> grows, or the
/// <c>MSG_MOVE_TELEPORT_ACK</c> gating in <c>WoWSharpObjectManager</c>
/// holds the ACK longer), this test will fail with the elapsed time and the
/// final flags so the regression is loud.
/// </summary>
[Collection(LiveValidationCollection.Name)]
[Trait("Category", "MovementParity")]
[Trait("ParityLayer", "Live")]
public class BgPostTeleportStabilizationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;

    // Stage at a flat Durotar road so the bot starts grounded and idle.
    // Ground Z here is ~38 per existing MovementParityTests.
    private const float StageX = -460f;
    private const float StageY = -4760f;
    private const float StageGroundZ = 38f;

    // Teleport target deliberately ~10y above ground so we exercise the
    // genuine post-teleport fall path (not just a same-Z snap).
    private const float TargetX = -460f;
    private const float TargetY = -4760f;
    private const float TargetGroundZ = 38f;
    private const float TargetTeleportZ = TargetGroundZ + 10f;

    // Tight bound from the handoff: snapshot must report cleared FALLING flags
    // and stable XY within 1.5s of the teleport command landing.
    private static readonly TimeSpan StabilizationBound = TimeSpan.FromSeconds(1.5);
    private const float StabilizationRadiusYards = 1.5f;

    private const uint FallingFlagsMask =
        (uint)MovementFlags.MOVEFLAG_FALLINGFAR
        | (uint)MovementFlags.MOVEFLAG_JUMPING;

    public BgPostTeleportStabilizationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task BgBot_TeleportAboveGround_FallingFlagsClearAndPositionStabilizesWithinBound()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Navigation.config.json");
        await _bot.EnsureSettingsAsync(settingsPath);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);

        var bgTarget = _bot
            .ResolveBotRunnerActionTargets(includeForegroundIfActionable: false, foregroundFirst: false)
            .FirstOrDefault(t => !t.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bgTarget.AccountName),
            "BG bot required for post-teleport stabilization test.");

        _output.WriteLine(
            $"[ACTION-PLAN] {bgTarget.RoleLabel} {bgTarget.AccountName}/{bgTarget.CharacterName}: BG post-teleport stabilization target.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no teleport-stabilization dispatch.");

        await _bot.EnsureCleanSlateAsync(bgTarget.AccountName, bgTarget.RoleLabel);

        // Ground-stage the bot so we start from a known idle/grounded snapshot.
        await _bot.BotTeleportAsync(bgTarget.AccountName, KalimdorMapId, StageX, StageY, StageGroundZ);
        var staged = await _bot.WaitForSnapshotConditionAsync(
            bgTarget.AccountName,
            snap =>
            {
                var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
                if (pos == null) return false;
                var flags = snap.Player?.Unit?.MovementFlags ?? 0;
                var dxy = MathF.Sqrt(
                    MathF.Pow(pos.X - StageX, 2f) + MathF.Pow(pos.Y - StageY, 2f));
                return dxy <= 5f && (flags & FallingFlagsMask) == 0;
            },
            TimeSpan.FromSeconds(15),
            pollIntervalMs: 200,
            progressLabel: $"{bgTarget.RoleLabel} pre-teleport-stage");
        Assert.True(staged, $"BG bot failed to settle at ground stage ({StageX:F1},{StageY:F1},{StageGroundZ:F1}).");

        // Issue the above-ground teleport. Start the stabilization stopwatch
        // immediately so the bound covers BotTeleportAsync's send + wait.
        var teleportStart = Stopwatch.StartNew();
        await _bot.BotTeleportAsync(bgTarget.AccountName, KalimdorMapId,
            TargetX, TargetY, TargetTeleportZ);

        var stabilized = await _bot.WaitForSnapshotConditionAsync(
            bgTarget.AccountName,
            snap =>
            {
                var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
                if (pos == null) return false;
                var flags = snap.Player?.Unit?.MovementFlags ?? 0;
                var dxy = MathF.Sqrt(
                    MathF.Pow(pos.X - TargetX, 2f) + MathF.Pow(pos.Y - TargetY, 2f));
                return dxy <= StabilizationRadiusYards && (flags & FallingFlagsMask) == 0;
            },
            StabilizationBound,
            pollIntervalMs: 50,
            progressLabel: $"{bgTarget.RoleLabel} post-teleport-stabilization");

        teleportStart.Stop();

        await _bot.RefreshSnapshotsAsync();
        var finalSnap = await _bot.GetSnapshotAsync(bgTarget.AccountName);
        var finalPos = finalSnap?.Player?.Unit?.GameObject?.Base?.Position;
        var finalFlags = finalSnap?.Player?.Unit?.MovementFlags ?? 0;
        _output.WriteLine(
            $"[{bgTarget.RoleLabel}] post-teleport elapsed={teleportStart.ElapsedMilliseconds}ms " +
            $"target=({TargetX:F1},{TargetY:F1},{TargetTeleportZ:F1}) " +
            $"final=({finalPos?.X:F2},{finalPos?.Y:F2},{finalPos?.Z:F2}) flags=0x{finalFlags:X}");

        Assert.True(
            stabilized,
            $"BG bot did not stabilize within {StabilizationBound.TotalSeconds:F1}s after teleport: " +
            $"target=({TargetX:F1},{TargetY:F1},{TargetTeleportZ:F1}) " +
            $"final=({finalPos?.X:F2},{finalPos?.Y:F2},{finalPos?.Z:F2}) flags=0x{finalFlags:X}.");
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            $"Could not resolve repo path: {string.Join('/', segments)}");
    }
}
