using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Dual-client verification of ground Z precision at known worst-error positions in Orgrimmar.
/// Teleports both FG (injected) and BG (headless) clients to each position, waits for
/// the post-teleport ground snap, and asserts BG Z matches the physics engine ground.
///
/// Run: dotnet test --filter "FullyQualifiedName~OrgrimmarGroundZAnalysisTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class OrgrimmarGroundZAnalysisTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor

    // After teleport, BG Z must be within this tolerance of the physics engine's ground Z (SimZ).
    // The 0.4-0.5y gap between RecZ (real client) and SimZ (engine) is a known geometry issue
    // (missing WMO doodad collision); this test validates that the BG client actually falls
    // to the engine's ground rather than floating at the teleport height.
    private const float BG_TO_SIM_TOLERANCE = 1.5f;

    // BG must NOT remain at the teleport height. If BG_Z is within this distance of teleZ,
    // the ground snap failed.
    private const float TELEPORT_HEIGHT_DEAD_ZONE = 0.5f;

    /// <summary>
    /// Worst ground-Z-error positions from physics replay calibration.
    /// Engine ground Z was consistently 0.4-0.5y below the real WoW client's reported Z.
    /// </summary>
    private static readonly (string Label, float X, float Y, float RecZ, float SimZ)[] ProbePositions =
    [
        ("ValleyOfStrength_A", 1637.264f, -4374.140f, 29.369f, 28.850f), // frame 1727, gap 0.519
        ("ValleyOfStrength_B", 1671.257f, -4356.295f, 29.856f, 29.443f), // frame 2227, gap 0.413
        ("ValleyOfStrength_C", 1651.753f, -4374.463f, 24.705f, 24.299f), // frame  998, gap 0.406
        ("UpperLevel",         1660.734f, -4332.938f, 61.669f, 61.266f), // frame 1425, gap 0.403
        ("MainGateApproach",   1625.772f, -4380.119f, 29.320f, 28.921f), // frame  839, gap 0.399
    ];

    public OrgrimmarGroundZAnalysisTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [Fact]
    public async Task DualClient_OrgrimmarGroundZ_PostTeleportSnap()
    {
        if (!_bot.IsReady)
        {
            _output.WriteLine($"SKIP: LiveBotFixture not ready — {_bot.FailureReason}");
            return;
        }

        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;

        _output.WriteLine("=== Orgrimmar Ground Z Post-Teleport Snap Verification ===");
        _output.WriteLine($"BG character: {_bot.BgCharacterName} (account: {bgAccount})");
        _output.WriteLine($"FG character: {_bot.FgCharacterName} (account: {fgAccount})");
        _output.WriteLine("");

        bool hasFg = fgAccount != null;
        if (!hasFg)
            _output.WriteLine("WARNING: No FG client available — running BG-only verification\n");

        _output.WriteLine($"{"Label",-22} {"TeleZ",8} {"SimZ",8} {"BG_Z",10} {"BG-Sim",8} {"FG_Z",10} {"FG-BG",8} {"Result",8}");
        _output.WriteLine(new string('-', 100));

        var failures = new List<string>();

        foreach (var (label, px, py, recZ, simZ) in ProbePositions)
        {
            // Teleport both clients to probe position in parallel (use recZ + 3 to avoid undermap detection)
            float teleZ = recZ + 3.0f;
            var teleportTasks = new List<Task> { _bot.BotTeleportAsync(bgAccount!, MapId, px, py, teleZ) };
            if (hasFg)
                teleportTasks.Add(_bot.BotTeleportAsync(fgAccount!, MapId, px, py, teleZ));
            await Task.WhenAll(teleportTasks);

            // Wait for clients to settle (gravity + ground snap)
            await Task.Delay(3000);

            // Read both positions from snapshots
            float bgZ = float.NaN;
            float fgZ = float.NaN;

            await _bot.RefreshSnapshotsAsync();
            var bgSnap = await _bot.GetSnapshotAsync(bgAccount!);
            var bgPos = bgSnap?.Player?.Unit?.GameObject?.Base?.Position;
            if (bgPos != null)
                bgZ = bgPos.Z;

            if (hasFg)
            {
                var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
                var fgPos = fgSnap?.Player?.Unit?.GameObject?.Base?.Position;
                if (fgPos != null)
                    fgZ = fgPos.Z;
            }

            float bgSimDelta = float.IsNaN(bgZ) ? float.NaN : bgZ - simZ;
            float fgBgDelta = (!float.IsNaN(bgZ) && !float.IsNaN(fgZ)) ? fgZ - bgZ : float.NaN;

            // Check assertions
            bool passed = true;
            string reason = "OK";

            if (float.IsNaN(bgZ))
            {
                passed = false;
                reason = "NO_BG_POS";
            }
            else if (MathF.Abs(bgZ - teleZ) < TELEPORT_HEIGHT_DEAD_ZONE)
            {
                passed = false;
                reason = "NO_SNAP";
                failures.Add($"{label}: BG stayed at teleport height ({bgZ:F3} ~= teleZ {teleZ:F3}) — ground snap did not fire");
            }
            else if (MathF.Abs(bgSimDelta) > BG_TO_SIM_TOLERANCE)
            {
                passed = false;
                reason = "Z_DRIFT";
                failures.Add($"{label}: BG_Z={bgZ:F3} too far from SimZ={simZ:F3} (delta={bgSimDelta:F3}, tolerance={BG_TO_SIM_TOLERANCE})");
            }

            _output.WriteLine($"{label,-22} {teleZ,8:F3} {simZ,8:F3} {bgZ,10:F3} {bgSimDelta,8:F3} {fgZ,10:F3} {fgBgDelta,8:F3} {(passed ? "PASS" : reason),8}");
        }

        _output.WriteLine("");
        if (failures.Count > 0)
        {
            _output.WriteLine($"=== FAILURES ({failures.Count}) ===");
            foreach (var f in failures)
                _output.WriteLine($"  - {f}");
        }
        else
        {
            _output.WriteLine("=== ALL POSITIONS PASSED ===");
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task DualClient_OrgrimmarGroundZ_StandAndWalk()
    {
        if (!_bot.IsReady)
        {
            _output.WriteLine($"SKIP: LiveBotFixture not ready — {_bot.FailureReason}");
            return;
        }

        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;
        bool hasFg = fgAccount != null;

        _output.WriteLine("=== Orgrimmar Stand-and-Walk Test ===");
        _output.WriteLine("Teleporting both clients to Valley of Strength center,");
        _output.WriteLine("then sampling position every second for 10 seconds while idle.\n");

        // Use the worst-error position (ValleyOfStrength_A)
        float px = 1637.264f, py = -4374.140f, pz = 29.369f;
        float teleZ = pz + 3.0f;

        // Teleport both clients
        await _bot.BotTeleportAsync(bgAccount!, MapId, px, py, teleZ);
        if (hasFg)
            await _bot.BotTeleportAsync(fgAccount!, MapId, px, py, teleZ);

        // Let them settle from the 3y drop
        await Task.Delay(2000);

        _output.WriteLine($"{"Time",6} {"BG_X",10} {"BG_Y",12} {"BG_Z",10} {"FG_X",10} {"FG_Y",12} {"FG_Z",10} {"Z_Delta",8}");
        _output.WriteLine(new string('-', 90));

        // Sample 10 times at 1-second intervals
        for (int i = 0; i < 10; i++)
        {
            var bgSnap = await _bot.GetSnapshotAsync(bgAccount!);
            var bgPos = bgSnap?.Player?.Unit?.GameObject?.Base?.Position;

            float bgX = bgPos?.X ?? float.NaN;
            float bgY = bgPos?.Y ?? float.NaN;
            float bgZ = bgPos?.Z ?? float.NaN;

            float fgX = float.NaN, fgY = float.NaN, fgZ = float.NaN;
            if (hasFg)
            {
                var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
                var fgPos = fgSnap?.Player?.Unit?.GameObject?.Base?.Position;
                fgX = fgPos?.X ?? float.NaN;
                fgY = fgPos?.Y ?? float.NaN;
                fgZ = fgPos?.Z ?? float.NaN;
            }

            float zDelta = (float.IsNaN(bgZ) || float.IsNaN(fgZ)) ? float.NaN : fgZ - bgZ;
            _output.WriteLine($"{i,6}s {bgX,10:F3} {bgY,12:F3} {bgZ,10:F3} {fgX,10:F3} {fgY,12:F3} {fgZ,10:F3} {zDelta,8:F4}");

            if (i < 9) await Task.Delay(1000);
        }

        _output.WriteLine("\nKey: If Z_Delta is consistently non-zero, the clients settle to different ground heights.");
        _output.WriteLine("This indicates either our physics ground Z differs from MaNGOS server ground Z,");
        _output.WriteLine("or the headless client's MOVEMENTFLAG_ON_TRANSPORT/similar is being handled differently.");
    }
}
