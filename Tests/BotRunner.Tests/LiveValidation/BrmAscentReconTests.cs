using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// PFS-OVERHAUL — FG-rendering reconnaissance for the FlameCrest → BRM
/// dungeon-entrance route. After four reverted bake/runtime attempts whose
/// pattern was "bench-green, live-regress", this recon establishes the
/// canonical FG client rendering as ground truth BEFORE any further fix
/// surface is picked. See <c>e:/repos/.claude/skills/mmo-pathfinding/SKILL.md</c>
/// "FG-rendering reconnaissance" section, and
/// <c>Westworld of Warcraft/docs/physics/PATHFINDING_VISUAL_DIAGNOSTICS.md</c>
/// "BRD / BRM" attempt log.
///
/// For each seeded coord (drawn from
/// <see cref="PathfindingService.Tests.WaypointGeneration.BrmDungeonRouteDiagnostic"/>
/// and the four reverted attempts' stall coords), the test:
///   1. teleports the FG bot via <c>.go xyz</c>,
///   2. lets the client settle 2s,
///   3. drives four cardinal yaw angles (0, π/2, π, 3π/2) via <c>.go xyzo</c>,
///   4. captures the WoW client window after each via <see cref="WindowCapture"/>,
///   5. writes a paired JSON snapshot (settled XYZ, facing, currentSpeed, etc.).
///
/// Output lands under <c>tmp/test-runtime/screenshots/brm-ascent-recon/</c>.
/// Polyref data for the same coords is produced in parallel by the x64
/// sidecar <c>BrmAscentReconPolyrefDump</c> in PathfindingService.Tests
/// (the BotRunner.Tests project is x86 and cannot load the Navigation.dll
/// x64 exports for <c>QueryPolyAtCoord</c>; see
/// <see cref="Harness.LiveBakeValidationHost"/> for the same boundary).
///
/// Gated on <c>WWOW_BRM_ASCENT_RECON=1</c>. Bot account: LPATHFG1 (the
/// FG long-pathing target resolved by the LongPathing roster).
/// </summary>
[Collection(LongPathingValidationCollection.Name)]
public class BrmAscentReconTests
{
    private const string EnableEnvVar = "WWOW_BRM_ASCENT_RECON";
    private const int FlameCrestMapId = 0;
    private const int SettleAfterTeleportMs = 2000;
    private const int SettleAfterOrientationMs = 500;
    private const string LongPathingConfigFileName = "LongPathing.config.json";
    private const string ExpectedTargetRace = "Tauren";
    private const string ExpectedTargetGender = "Male";

    private readonly LongPathingFixture _bot;
    private readonly ITestOutputHelper _output;

    public BrmAscentReconTests(LongPathingFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    /// <summary>
    /// One coord = one row of the recon. Why is recorded so the RECON_SUMMARY
    /// can cite each entry's provenance (audit, prior stall, midpoint guess).
    /// </summary>
    private readonly record struct ReconCoord(
        string Name, float X, float Y, float Z, string Why);

    private static readonly ReconCoord[] Coords =
    {
        new("fc_start",      -7518.7f, -2159.9f, 131.9f,
            "FlameCrest bot spawn (BotRunner long-pathing test entry)"),
        new("fc_stall",      -7519.0f, -2100.4f, 130.3f,
            "Round-1 (Attempt 1 both-sides-52) stall coord, ~60y north of FC"),
        new("ruins_wall",    -7665.0f, -1808.0f, 137.0f,
            "Round-2 (Attempt 2 terrain-only-52) wall-creep coord in Ruins of Thaurissan"),
        new("brm_south_lo",  -7949.7f, -1162.8f, 170.8f,
            "BRM south-face baseline coord (UBRS Round-2/3 BRM south-face)"),
        new("brm_southnew",  -7825.4f, -1129.2f, 133.8f,
            "Round-3 stuck-recovery coord after polyIdx range cull (project_pfs_overhaul_006_brm_iteration_final)"),
        new("brm_mid_lbrs",  -7647.1f, -1197.1f, 225.2f,
            "Smooth-path endWP for FC→LBRS/UBRS (BrmDungeonRouteDiagnostic.Audit_BrmDungeonEndpoints_ResolveAndCorridor)"),
        new("brm_mid_bwl",   -7640.0f, -1213.4f, 228.4f,
            "Smooth-path endWP for FC→BWL (Audit_BrmDungeonEndpoints_ResolveAndCorridor)"),
        new("ubrs_portal",   -7524.0f, -1233.0f, 287.0f,
            "UBRS portal target (literal portal poly, mesh-reachable)"),
        new("lbrs_portal",   -7531.0f, -1226.0f, 286.0f,
            "LBRS portal target (literal portal poly, mesh-reachable)"),
        new("bwl_portal",    -7659.0f, -1214.0f, 291.0f,
            "BWL approach (corridor terminus; literal portal at z=400 is bake-isolated)"),
        new("brd_portal",    -7187.0f,  -958.0f, 254.0f,
            "BRD approach (corridor terminus; literal portal at z=165 is bake-isolated)"),
    };

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task BrmAscentRecon_TeleportAndCaptureMultiAngle()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(EnableEnvVar),
                "1",
                StringComparison.Ordinal),
            $"BRM ascent recon disabled (set {EnableEnvVar}=1).");

        var target = await EnsureFgTargetAsync();
        await _bot.EnsureCleanSlateAsync(target.AccountName, target.RoleLabel);

        var outputDir = ResolveReconOutputDir();
        Directory.CreateDirectory(outputDir);

        var manifest = new List<object>(Coords.Length);

        foreach (var c in Coords)
        {
            _output.WriteLine(
                $"[RECON] {c.Name}: tele {target.AccountName} -> map={FlameCrestMapId} ({c.X:F2},{c.Y:F2},{c.Z:F2}) — {c.Why}");

            await _bot.BotTeleportAsync(target.AccountName, FlameCrestMapId, c.X, c.Y, c.Z);
            await Task.Delay(SettleAfterTeleportMs);
            await _bot.RefreshSnapshotsAsync();
            var settled = await _bot.GetSnapshotAsync(target.AccountName);

            // PID resolution: the StateManager log only emits ONE
            // "WoW.exe started for account ..." line per launch, but it
            // auto-restarts WoW.exe on crash without re-emitting that line.
            // The resolver returns the original PID, which by capture time
            // may be dead. Fall back to scanning live WoW.exe processes for
            // one whose top-level window class is GxWindowClassD3d.
            var hintPid = ResolveManagedWowProcessId(target.AccountName);
            var pid = ResolveLiveWoWClientPid(hintPid);
            var captured = new List<string>();
            string? captureWarning = null;

            if (pid is null)
            {
                captureWarning = $"no live WoW.exe with a GxWindowClassD3d window (hintPid={hintPid?.ToString() ?? "null"})";
                _output.WriteLine($"[RECON-WARN] {c.Name}: {captureWarning}");
            }
            else
            {
                // Re-orient + capture for each yaw via the existing helper. The
                // helper writes PNGs into outputDir with names of the form
                //   <baseLabel>-<yawNNN>-<UTC>.png
                // We pass settledX/Y/Z (NOT the request coords) so the camera
                // shows the actual rendering at where the bot ended up.
                var pos = GetPosition(settled);
                var settledX = pos?.X ?? c.X;
                var settledY = pos?.Y ?? c.Y;
                var settledZ = pos?.Z ?? c.Z;

                try
                {
                    var paths = await MultiAngleScreenshotCapture.CaptureAsync(
                        processId: pid.Value,
                        outputDir: outputDir,
                        baseLabel: $"{c.Name}-{target.AccountName}",
                        yaws: MultiAngleScreenshotCapture.CardinalYaws,
                        applyOrientationAsync: async (yawRad, ct) =>
                        {
                            await _bot.BotTeleportWithOrientationAsync(
                                target.AccountName, FlameCrestMapId, settledX, settledY, settledZ, yawRad)
                                .ConfigureAwait(false);
                        },
                        settleMs: SettleAfterOrientationMs,
                        ct: CancellationToken.None);
                    captured.AddRange(paths);
                }
                catch (Exception ex)
                {
                    captureWarning = $"capture exception: {ex.Message}";
                    _output.WriteLine($"[RECON-ERR] {c.Name}: {captureWarning}");
                }

                if (captured.Count != MultiAngleScreenshotCapture.CardinalYaws.Count)
                {
                    captureWarning ??=
                        $"expected {MultiAngleScreenshotCapture.CardinalYaws.Count} captures, got {captured.Count}";
                    _output.WriteLine(
                        $"[RECON-WARN] {c.Name}: only {captured.Count}/{MultiAngleScreenshotCapture.CardinalYaws.Count} angles captured.");
                }
            }

            var settledPos = GetPosition(settled);
            var movement = settled?.MovementData;
            var jsonPath = Path.Combine(outputDir, $"{c.Name}-{target.AccountName}.json");
            var record = new
            {
                timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                name = c.Name,
                why = c.Why,
                request = new { mapId = FlameCrestMapId, x = c.X, y = c.Y, z = c.Z },
                hintPid,
                pid,
                account = target.AccountName,
                settled = settled == null ? null : new
                {
                    currentMapId = settled.CurrentMapId,
                    position = settledPos == null ? null : new
                    {
                        x = settledPos.X,
                        y = settledPos.Y,
                        z = settledPos.Z,
                    },
                    deltaXY = settledPos == null
                        ? (float?)null
                        : LiveBotFixture.Distance2D(settledPos.X, settledPos.Y, c.X, c.Y),
                    deltaZ = settledPos == null ? (float?)null : settledPos.Z - c.Z,
                    facing = movement?.Facing,
                    currentSpeed = movement?.CurrentSpeed,
                    runSpeed = movement?.RunSpeed,
                    movementFlags = movement == null ? (uint?)null : movement.MovementFlags,
                    splineFlags = movement?.SplineFlags,
                    fallTime = movement?.FallTime,
                },
                captures = captured,
                captureWarning,
            };

            File.WriteAllText(
                jsonPath,
                JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));

            manifest.Add(new
            {
                name = c.Name,
                why = c.Why,
                requested = new { x = c.X, y = c.Y, z = c.Z },
                jsonPath,
                captures = captured,
                captureWarning,
            });
        }

        var manifestPath = Path.Combine(outputDir, "recon-manifest.json");
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(
                new
                {
                    generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    account = target.AccountName,
                    coordCount = Coords.Length,
                    yawAngles = MultiAngleScreenshotCapture.CardinalYaws
                        .Select(y => new { y.Suffix, y.Radians }).ToArray(),
                    coords = manifest,
                },
                new JsonSerializerOptions { WriteIndented = true }));

        _output.WriteLine($"[RECON] manifest written to {manifestPath}");
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureFgTargetAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", LongPathingConfigFileName);

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            $"Shodan director was not launched by {LongPathingConfigFileName}.");

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: true,
                foregroundFirst: true)
            .Single(t => t.IsForeground);

        AssertConfiguredTaurenMaleTarget(settingsPath, target.AccountName);
        _output.WriteLine(
            $"[RECON-TARGET] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: {ExpectedTargetRace} {ExpectedTargetGender}.");
        return target;
    }

    private int? ResolveManagedWowProcessId(string account)
        => global::Tests.Infrastructure.ManagedWowProcessIdResolver.Resolve(
            account, _bot.GetStateManagerOutput());

    /// <summary>
    /// Picks the live WoW.exe PID whose top-level window is the WoW client
    /// (class <c>GxWindowClassD3d</c>). Tries <paramref name="hintPid"/> first;
    /// if that PID is dead or windowless, scans every <c>WoW.exe</c> process
    /// and returns the first one whose handle resolves. Returns null if no
    /// process is hosting a WoW window. Workaround for the StateManager log
    /// only recording the FIRST WoW.exe launch per account; auto-restarts
    /// after a crash leave the log's PID stale.
    /// </summary>
    private static int? ResolveLiveWoWClientPid(int? hintPid)
    {
        if (hintPid.HasValue && WindowCapture.FindWoWClientWindow(hintPid.Value) != nint.Zero)
            return hintPid;

        foreach (var proc in Process.GetProcessesByName("WoW"))
        {
            try
            {
                if (WindowCapture.FindWoWClientWindow(proc.Id) != nint.Zero)
                    return proc.Id;
            }
            finally { proc.Dispose(); }
        }
        return null;
    }

    private static Game.Position? GetPosition(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.Unit?.GameObject?.Base?.Position
            ?? snapshot?.MovementData?.Position;

    private static string ResolveReconOutputDir()
    {
        var repoRoot = ResolveRepoRoot();
        return Path.Combine(repoRoot, "tmp", "test-runtime", "screenshots", "brm-ascent-recon");
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WestworldOfWarcraft.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
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
        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
    }

    private static void AssertConfiguredTaurenMaleTarget(string settingsPath, string account)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var target = document.RootElement.EnumerateArray()
            .SingleOrDefault(element =>
                element.TryGetProperty("AccountName", out var accountProperty)
                && string.Equals(accountProperty.GetString(), account, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(JsonValueKind.Object, target.ValueKind);
        Assert.Equal(ExpectedTargetRace, target.GetProperty("CharacterRace").GetString());
        Assert.Equal(ExpectedTargetGender, target.GetProperty("CharacterGender").GetString());
    }
}
