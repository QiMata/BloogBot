using System;
using System.IO;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public sealed class SceneDataRegionFallbackTests(PhysicsEngineFixture fixture)
{
    private readonly PhysicsEngineFixture _fixture = fixture;

    [Fact]
    public void QueryTerrainAabbContacts_WithoutSceneCacheFile_UsesLiveBoundedExtract()
    {
        Assert.True(_fixture.IsInitialized, "Native physics fixture failed to initialize.");

        PhysicsEngineFixture.EnsureDataDir();
        string dataDir = GetDataDir();
        string originalScenesDir = Path.Combine(dataDir, "scenes") + Path.DirectorySeparatorChar;
        string tempRoot = Path.Combine(Path.GetTempPath(), $"wwow_scene_fallback_{Guid.NewGuid():N}");
        string tempScenesDir = Path.Combine(tempRoot, "scenes");
        Directory.CreateDirectory(tempScenesDir);

        var (boxMin, boxMax) = GetAlteracValleyStagingBounds();

        try
        {
            SetSceneSliceMode(false);
            SetScenesDir(tempScenesDir + Path.DirectorySeparatorChar);
            UnloadSceneCache(1);

            var contacts = new TerrainAabbContact[512];
            int count = QueryTerrainAABBContacts(1, in boxMin, in boxMax, contacts, contacts.Length);

            Assert.True(count > 0, "Expected live bounded extraction to provide contacts when no scene cache file exists.");
        }
        finally
        {
            SetSceneSliceMode(false);
            UnloadSceneCache(1);
            SetScenesDir(originalScenesDir);
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void QueryTerrainAabbContacts_OutsideCachedSceneBounds_UsesLiveBoundedExtract()
    {
        Assert.True(_fixture.IsInitialized, "Native physics fixture failed to initialize.");

        PhysicsEngineFixture.EnsureDataDir();
        string dataDir = GetDataDir();
        string originalScenesDir = Path.Combine(dataDir, "scenes") + Path.DirectorySeparatorChar;
        string tempRoot = Path.Combine(Path.GetTempPath(), $"wwow_scene_fallback_{Guid.NewGuid():N}");
        string tempScenesDir = Path.Combine(tempRoot, "scenes");
        Directory.CreateDirectory(tempScenesDir);

        string partialScenePath = Path.Combine(tempScenesDir, "1.scene");
        Assert.True(ExtractSceneCache(1, partialScenePath, -400f, -4500f, -100f, -4100f),
            "Expected to create a bounded Kalimdor scene cache fixture.");
        Assert.True(File.Exists(partialScenePath), $"Expected bounded scene cache at {partialScenePath}.");

        var (boxMin, boxMax) = GetAlteracValleyStagingBounds();

        try
        {
            SetSceneSliceMode(false);
            SetScenesDir(tempScenesDir + Path.DirectorySeparatorChar);
            UnloadSceneCache(1);

            var contacts = new TerrainAabbContact[512];
            int count = QueryTerrainAABBContacts(1, in boxMin, in boxMax, contacts, contacts.Length);

            Assert.True(count > 0,
                "Expected live bounded extraction to provide contacts when the cached scene file does not cover the requested region.");
        }
        finally
        {
            SetSceneSliceMode(false);
            UnloadSceneCache(1);
            SetScenesDir(originalScenesDir);
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void PreloadMap_WhenSceneAutoloadDisabled_DoesNotLoadSceneCacheFromDisk()
    {
        Assert.True(_fixture.IsInitialized, "Native physics fixture failed to initialize.");

        PhysicsEngineFixture.EnsureDataDir();
        string dataDir = GetDataDir();
        string originalScenesDir = Path.Combine(dataDir, "scenes") + Path.DirectorySeparatorChar;
        string tempRoot = Path.Combine(Path.GetTempPath(), $"wwow_scene_autoload_gate_{Guid.NewGuid():N}");
        string tempScenesDir = Path.Combine(tempRoot, "scenes");
        Directory.CreateDirectory(tempScenesDir);

        string partialScenePath = Path.Combine(tempScenesDir, "1.scene");
        Assert.True(ExtractSceneCache(1, partialScenePath, -400f, -4500f, -100f, -4100f),
            "Expected to create a bounded Kalimdor scene cache fixture.");
        Assert.True(File.Exists(partialScenePath), $"Expected bounded scene cache at {partialScenePath}.");

        try
        {
            SetSceneAutoloadEnabled(true);
            SetScenesDir(tempScenesDir + Path.DirectorySeparatorChar);
            UnloadSceneCache(1);
            Assert.False(HasSceneCache(1), "Scene cache should start unloaded for the control probe.");

            PreloadMap(1);
            Assert.True(HasSceneCache(1), "Expected autoload-enabled preload to materialize the scene cache from disk.");

            UnloadSceneCache(1);
            SetSceneAutoloadEnabled(false);
            PreloadMap(1);

            Assert.False(HasSceneCache(1),
                "Scene cache should stay unloaded when scene autoload is disabled for service-managed runtimes.");
        }
        finally
        {
            SetSceneAutoloadEnabled(true);
            UnloadSceneCache(1);
            SetScenesDir(originalScenesDir);
            TryDeleteDirectory(tempRoot);
        }
    }

    private static (Vector3 boxMin, Vector3 boxMax) GetAlteracValleyStagingBounds()
        => (new Vector3(1600f, -5000f, -500f), new Vector3(2200f, -4400f, 2000f));

    private static string GetDataDir()
    {
        string? dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        Assert.False(string.IsNullOrWhiteSpace(dataDir), "WWOW_DATA_DIR must be configured for native scene tests.");
        return dataDir!;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
