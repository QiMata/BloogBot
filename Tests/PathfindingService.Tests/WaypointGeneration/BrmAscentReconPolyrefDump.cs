using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameData.Core.Models;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PathfindingService.Tests.WaypointGeneration;

/// <summary>
/// PFS-OVERHAUL — sidecar to
/// <see cref="BotRunner.Tests.LiveValidation.BrmAscentReconTests"/>. The
/// BotRunner.Tests project is x86 and cannot directly load the x64
/// Navigation.dll exports for <c>QueryPolyAtCoord</c>; this test runs in
/// PathfindingService.Tests (x64), queries the navmesh at the same coord
/// set, and writes a JSON sidecar into the recon output directory so the
/// human RECON_SUMMARY.md author can correlate FG-rendering observations
/// with bake state.
///
/// Output: <c>tmp/test-runtime/screenshots/brm-ascent-recon/recon-polyrefs.json</c>.
/// Coord list MUST stay in sync with <c>BrmAscentReconTests.Coords</c>.
///
/// Gated on the same env var as the FG recon: <c>WWOW_BRM_ASCENT_RECON=1</c>.
/// </summary>
[Trait("Category", "Unit")]
public class BrmAscentReconPolyrefDump : IClassFixture<PathfindingValidationFixture>
{
    private const string EnableEnvVar = "WWOW_BRM_ASCENT_RECON";
    private const uint MapId = 0;            // Eastern Kingdoms
    private const float AgentRadius = 1.0247f;
    private const float WalkableClimb = 1.8f;

    // Wide search extent for "is there ANY walkable poly near this coord?"
    // Used in the second pass — first pass uses the strict capsule extents
    // so the walkable property tests can later use the same shape.
    private const float WideSearchXY = 10f;
    private const float WideSearchZ = 300f;

    private readonly PathfindingValidationFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BrmAscentReconPolyrefDump(PathfindingValidationFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private readonly record struct ReconCoord(
        string Name, float X, float Y, float Z, string Why);

    /// <summary>
    /// MUST stay in sync with
    /// <c>BotRunner.Tests.LiveValidation.BrmAscentReconTests.Coords</c>.
    /// Cross-project string sharing isn't worth the build coupling for a
    /// one-off recon — both files cite each other in their summary docs.
    /// </summary>
    private static readonly ReconCoord[] Coords =
    {
        new("fc_start",      -7518.7f, -2159.9f, 131.9f, "FlameCrest bot spawn"),
        new("fc_stall",      -7519.0f, -2100.4f, 130.3f, "Round-1 stall, ~60y north of FC"),
        new("ruins_wall",    -7665.0f, -1808.0f, 137.0f, "Round-2 wall-creep coord (Ruins of Thaurissan)"),
        new("brm_south_lo",  -7949.7f, -1162.8f, 170.8f, "BRM south-face baseline"),
        new("brm_southnew",  -7825.4f, -1129.2f, 133.8f, "Round-3 stuck-recovery coord"),
        new("brm_mid_lbrs",  -7647.1f, -1197.1f, 225.2f, "smooth-path endWP for FC->LBRS/UBRS"),
        new("brm_mid_bwl",   -7640.0f, -1213.4f, 228.4f, "smooth-path endWP for FC->BWL"),
        new("ubrs_portal",   -7524.0f, -1233.0f, 287.0f, "UBRS portal target"),
        new("lbrs_portal",   -7531.0f, -1226.0f, 286.0f, "LBRS portal target"),
        new("bwl_portal",    -7659.0f, -1214.0f, 291.0f, "BWL approach"),
        new("brd_portal",    -7187.0f,  -958.0f, 254.0f, "BRD approach"),
    };

    [SkippableFact]
    public void DumpReconPolyrefs()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(EnableEnvVar),
                "1",
                StringComparison.Ordinal),
            $"BRM ascent recon disabled (set {EnableEnvVar}=1).");

        var outDir = ResolveReconOutputDir();
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "recon-polyrefs.json");

        var rows = new List<object>(Coords.Length);

        foreach (var c in Coords)
        {
            var pos = new XYZ(c.X, c.Y, c.Z);

            var strict = NavigationInterop.QueryPolyAtCoord(
                MapId, pos, searchExtentXY: AgentRadius, searchExtentZ: WalkableClimb);
            var wide = NavigationInterop.QueryPolyAtCoord(
                MapId, pos, searchExtentXY: WideSearchXY, searchExtentZ: WideSearchZ);

            object SerializeProbe(NavigationInterop.PolyAtCoordResult p)
            {
                if (!p.Success || !p.HasPoly)
                {
                    return new
                    {
                        success = p.Success,
                        hasPoly = false,
                        polyRef = (string?)null,
                        polyType = "Unknown",
                        flagsHex = (string?)null,
                        area = (byte?)null,
                        steepSlopes = (bool?)null,
                        nearestPoint = (object?)null,
                        surfaceZ = (float?)null,
                        deltaZ = (float?)null,
                    };
                }
                var flags = NavigationInterop.QueryPolyFlags(MapId, p.PolyRef);
                return new
                {
                    success = true,
                    hasPoly = true,
                    polyRef = $"0x{p.PolyRef:X16}",
                    polyType = p.PolyType.ToString(),
                    flagsHex = flags.Success ? $"0x{flags.Flags:X4}" : null,
                    area = flags.Success ? (byte?)flags.Area : null,
                    steepSlopes = flags.Success ? flags.HasSteepSlopes : (bool?)null,
                    nearestPoint = new { x = p.NearestPoint.X, y = p.NearestPoint.Y, z = p.NearestPoint.Z },
                    surfaceZ = p.HasSurface ? (float?)p.SurfaceZ : null,
                    deltaZ = p.HasSurface ? (float?)Math.Abs(c.Z - p.SurfaceZ) : null,
                };
            }

            _output.WriteLine(
                $"# {c.Name,-12} ({c.X,8:F1},{c.Y,8:F1},{c.Z,6:F1})  "
                + $"strict.hasPoly={strict.HasPoly} wide.hasPoly={wide.HasPoly} "
                + (strict.HasSurface ? $"strict.dz={Math.Abs(c.Z - strict.SurfaceZ):F2}" : "strict.dz=N/A"));

            rows.Add(new
            {
                name = c.Name,
                why = c.Why,
                requested = new { x = c.X, y = c.Y, z = c.Z },
                searchExtents = new
                {
                    strict = new { xy = AgentRadius, z = WalkableClimb },
                    wide = new { xy = WideSearchXY, z = WideSearchZ },
                },
                strict = SerializeProbe(strict),
                wide = SerializeProbe(wide),
            });
        }

        var doc = new
        {
            generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            mapId = MapId,
            agent = new { radius = AgentRadius, walkableClimb = WalkableClimb },
            dataDir = _fixture.DataDir,
            coordCount = Coords.Length,
            coords = rows,
        };

        File.WriteAllText(
            outPath,
            JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));

        _output.WriteLine($"# polyref sidecar written to {outPath}");
        _output.WriteLine($"# WWOW_DATA_DIR: {_fixture.DataDir}");
    }

    private static string ResolveReconOutputDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WestworldOfWarcraft.sln")))
                return Path.Combine(dir.FullName, "tmp", "test-runtime", "screenshots", "brm-ascent-recon");
            dir = dir.Parent;
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "tmp", "test-runtime", "screenshots", "brm-ascent-recon");
    }
}
