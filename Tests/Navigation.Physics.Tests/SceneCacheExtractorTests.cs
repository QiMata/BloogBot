using Xunit;
using Xunit.Abstractions;
using System;
using System.IO;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

/// <summary>
/// Extracts .scene files for all important maps.
/// Run once after VMAP data changes to regenerate scene caches.
///
/// dotnet test --filter "FullyQualifiedName~SceneCacheExtractor" --configuration Release -v n
/// </summary>
[Collection("PhysicsEngine")]
public class SceneCacheExtractorTests
{
    private readonly ITestOutputHelper _output;

    public SceneCacheExtractorTests(ITestOutputHelper output) => _output = output;

    // Maps needed for BG/dungeon tests
    private static readonly (uint mapId, string name)[] MapsToExtract =
    [
        (0, "Eastern Kingdoms"),
        (1, "Kalimdor"),
        (13, "Wailing Caverns test"),
        (30, "Alterac Valley"),
        (33, "Shadowfang Keep"),
        (34, "Stormwind Stockade"),
        (36, "Deadmines"),
        (43, "Wailing Caverns"),
        (47, "Razorfen Kraul"),
        (48, "Blackfathom Deeps"),
        (70, "Stormwind Tram"),
        (90, "Gnomeregan"),
        (109, "Sunken Temple"),
        (129, "Razorfen Downs"),
        (169, "Emerald Dream"),
        (189, "Scarlet Monastery"),
        (209, "Zul'Farrak"),
        (229, "Blackrock Spire"),
        (230, "Blackrock Depths"),
        (249, "Onyxia's Lair"),
        (289, "Scholomance"),
        (309, "Zul'Gurub"),
        (329, "Stratholme"),
        (349, "Maraudon"),
        (369, "Deeprun Tram"),
        (389, "Ragefire Chasm"),
        (409, "Molten Core"),
        (429, "Dire Maul"),
        (449, "Ahn'Qiraj Temple"),
        (469, "Blackwing Lair"),
        (489, "Warsong Gulch"),
        (509, "Ruins of Ahn'Qiraj"),
        (529, "Arathi Basin"),
        (531, "AQ40"),
        (533, "Naxxramas"),
    ];

    [Fact]
    [Trait("Category", "SceneExtraction")]
    public void ExtractAllSceneCaches()
    {
        var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (string.IsNullOrEmpty(dataDir))
        {
            // Try common locations
            var candidates = new[]
            {
                @"E:\repos\Westworld of Warcraft\Data",
                @"D:\MaNGOS\data",
                @"D:\vmangos-server\data",
            };
            foreach (var c in candidates)
                if (Directory.Exists(c)) { dataDir = c; break; }
        }

        Skip.If(string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir),
            $"WWOW_DATA_DIR not set and no data directory found");

        var scenesDir = Path.Combine(dataDir!, "scenes");
        Directory.CreateDirectory(scenesDir);

        _output.WriteLine($"Data directory: {dataDir}");
        _output.WriteLine($"Scenes output: {scenesDir}");
        _output.WriteLine($"Maps to extract: {MapsToExtract.Length}");

        int extracted = 0, skipped = 0, failed = 0;

        foreach (var (mapId, name) in MapsToExtract)
        {
            var scenePath = Path.Combine(scenesDir, $"{mapId}.scene");
            if (File.Exists(scenePath))
            {
                var existingSize = new FileInfo(scenePath).Length;
                _output.WriteLine($"  [{mapId}] {name}: SKIP (exists, {existingSize / 1024}KB)");
                skipped++;
                continue;
            }

            _output.WriteLine($"  [{mapId}] {name}: extracting...");
            try
            {
                // Full-map extraction (0,0,0,0 = entire map)
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool ok = ExtractSceneCache(mapId, scenePath, 0, 0, 0, 0);
                sw.Stop();

                if (ok && File.Exists(scenePath))
                {
                    var size = new FileInfo(scenePath).Length;
                    _output.WriteLine($"  [{mapId}] {name}: OK ({size / 1024}KB, {sw.Elapsed.TotalSeconds:F1}s)");
                    extracted++;
                }
                else
                {
                    _output.WriteLine($"  [{mapId}] {name}: FAILED (ExtractSceneCache returned {ok})");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  [{mapId}] {name}: ERROR ({ex.Message})");
                failed++;
            }
        }

        _output.WriteLine($"\nDone: {extracted} extracted, {skipped} skipped, {failed} failed");
        Assert.True(failed == 0, $"{failed} maps failed to extract");
    }
}
