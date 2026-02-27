using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Dual-client analysis of ground Z precision at known worst-error positions in Orgrimmar.
/// Teleports both FG (injected) and BG (headless) clients to each position, reads their
/// reported Z from snapshots, and compares with the physics engine's computed ground Z.
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
    public async Task DualClient_OrgrimmarGroundZ_PositionAnalysis()
    {
        if (!_bot.IsReady)
        {
            _output.WriteLine($"SKIP: LiveBotFixture not ready — {_bot.FailureReason}");
            return;
        }

        var bgAccount = _bot.BgAccountName;
        var fgAccount = _bot.FgAccountName;

        _output.WriteLine("=== Orgrimmar Ground Z Dual-Client Analysis ===");
        _output.WriteLine($"BG character: {_bot.BgCharacterName} (account: {bgAccount})");
        _output.WriteLine($"FG character: {_bot.FgCharacterName} (account: {fgAccount})");
        _output.WriteLine("");

        bool hasFg = fgAccount != null;
        if (!hasFg)
            _output.WriteLine("WARNING: No FG client available — running BG-only analysis\n");

        _output.WriteLine($"{"Label",-22} {"ProbeX",10} {"ProbeY",12} {"RecZ",8} {"SimZ",8} {"Gap",6} {"BG_Z",10} {"BG_Err",8} {"FG_Z",10} {"FG_Err",8}");
        _output.WriteLine(new string('-', 120));

        foreach (var (label, px, py, recZ, simZ) in ProbePositions)
        {
            // Teleport BG client to probe position (use recZ + 3 to avoid undermap detection)
            float teleZ = recZ + 3.0f;
            await _bot.BotTeleportAsync(bgAccount!, MapId, px, py, teleZ);

            // Teleport FG client to same position
            if (hasFg)
                await _bot.BotTeleportAsync(fgAccount!, MapId, px, py, teleZ);

            // Wait for clients to settle (gravity + ground snap)
            await Task.Delay(3000);

            // Read BG position from snapshot
            float bgZ = float.NaN;
            float bgX = float.NaN, bgY = float.NaN;
            var bgSnap = await _bot.GetSnapshotAsync(bgAccount!);
            var bgPos = bgSnap?.Player?.Unit?.GameObject?.Base?.Position;
            if (bgPos != null)
            {
                bgX = bgPos.X;
                bgY = bgPos.Y;
                bgZ = bgPos.Z;
            }

            // Read FG position from snapshot
            float fgZ = float.NaN;
            float fgX = float.NaN, fgY = float.NaN;
            if (hasFg)
            {
                var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
                var fgPos = fgSnap?.Player?.Unit?.GameObject?.Base?.Position;
                if (fgPos != null)
                {
                    fgX = fgPos.X;
                    fgY = fgPos.Y;
                    fgZ = fgPos.Z;
                }
            }

            float gap = MathF.Abs(recZ - simZ);
            float bgErr = float.IsNaN(bgZ) ? float.NaN : bgZ - recZ;
            float fgErr = float.IsNaN(fgZ) ? float.NaN : fgZ - recZ;

            _output.WriteLine($"{label,-22} {px,10:F3} {py,12:F3} {recZ,8:F3} {simZ,8:F3} {gap,6:F3} {bgZ,10:F3} {bgErr,8:F3} {fgZ,10:F3} {fgErr,8:F3}");

            // Detailed per-position dump
            _output.WriteLine($"  BG full pos: ({bgX:F3}, {bgY:F3}, {bgZ:F3})");
            if (hasFg)
                _output.WriteLine($"  FG full pos: ({fgX:F3}, {fgY:F3}, {fgZ:F3})");
            if (!float.IsNaN(bgZ) && !float.IsNaN(fgZ))
                _output.WriteLine($"  FG-BG delta: Z={fgZ - bgZ:F4}");
            _output.WriteLine("");
        }

        _output.WriteLine("=== Summary ===");
        _output.WriteLine("RecZ = Z from original FG recording (gold standard client position)");
        _output.WriteLine("SimZ = Z from physics engine ground query (0.4-0.5y below RecZ)");
        _output.WriteLine("BG_Z = Z reported by headless client right now");
        _output.WriteLine("FG_Z = Z reported by injected client right now");
        _output.WriteLine("BG_Err / FG_Err = client Z minus RecZ (positive = above, negative = below)");
        _output.WriteLine("");
        _output.WriteLine("If BG_Z ~= FG_Z ~= RecZ: server position is authoritative and correct.");
        _output.WriteLine("If BG_Z ~= SimZ (below RecZ): BG client is using our physics engine ground Z.");
        _output.WriteLine("If FG_Z ~= RecZ (above SimZ): real WoW client has geometry our engine lacks.");
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
