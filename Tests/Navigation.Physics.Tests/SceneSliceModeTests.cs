using System;
using System.IO;

namespace Navigation.Physics.Tests;

using static NavigationInterop;

[Collection("PhysicsEngine")]
public sealed class SceneSliceModeTests
{
    private readonly PhysicsEngineFixture _fixture;

    public SceneSliceModeTests(PhysicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void GetGroundZ_SceneSliceMode_DoesNotAutoloadFullSceneCache()
    {
        Assert.True(_fixture.IsInitialized, "Native physics fixture failed to initialize.");

        PhysicsEngineFixture.EnsureDataDir();
        string? dataDir = ResolveDataDir();
        Assert.False(string.IsNullOrWhiteSpace(dataDir));

        string scenesDir = Path.Combine(dataDir!, "scenes") + Path.DirectorySeparatorChar;
        string scenePath = Path.Combine(dataDir, "scenes", "1.scene");
        Assert.True(File.Exists(scenePath), $"Expected scene cache fixture at {scenePath}.");

        const uint mapId = 1;
        const float queryX = -224f;
        const float queryY = -4310f;
        const float queryZ = 100f;
        const float maxSearchDist = 50f;

        SetScenesDir(scenesDir);

        try
        {
            SetSceneSliceMode(false);
            UnloadSceneCache(mapId);

            float autoloadGroundZ = GetGroundZ(mapId, queryX, queryY, queryZ, maxSearchDist);
            Assert.True(autoloadGroundZ > -100000f,
                $"Expected baseline autoload to resolve ground Z, but got {autoloadGroundZ:F3}.");
            Assert.True(HasSceneCache(mapId), "Baseline query should auto-load the scene cache.");

            UnloadSceneCache(mapId);
            Assert.False(HasSceneCache(mapId), "Scene cache should be unloaded before thin-slice verification.");

            SetSceneSliceMode(true);
            float sliceOnlyGroundZ = GetGroundZ(mapId, queryX, queryY, queryZ, maxSearchDist);

            Assert.True(sliceOnlyGroundZ <= -100000f,
                $"Thin scene-slice mode should not autoload full-map cache, but got {sliceOnlyGroundZ:F3}.");
            Assert.False(HasSceneCache(mapId), "Thin scene-slice mode should leave the full scene cache unloaded.");
        }
        finally
        {
            SetSceneSliceMode(false);
            UnloadSceneCache(mapId);
        }
    }

    private static string? ResolveDataDir()
    {
        string? configured = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        foreach (string root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            string? dir = root;
            while (!string.IsNullOrWhiteSpace(dir))
            {
                string candidate = Path.Combine(dir, "Data");
                if (Directory.Exists(Path.Combine(candidate, "scenes")))
                    return candidate;

                string? parent = Path.GetDirectoryName(dir);
                if (string.Equals(parent, dir, StringComparison.Ordinal))
                    break;
                dir = parent;
            }
        }

        return null;
    }
}
