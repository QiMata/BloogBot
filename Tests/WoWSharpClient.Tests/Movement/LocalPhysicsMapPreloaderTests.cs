using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WoWSharpClient.Movement;

namespace WoWSharpClient.Tests.Movement;

public sealed class LocalPhysicsMapPreloaderTests
{
    [Fact]
    public void EnsureMapPreloaded_PreloadsRequestedMapOnlyOnce()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"wowsharp-local-preload-single-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var preloadedMapIds = new List<uint>();
        string? configuredDataDir = null;

        NativeLocalPhysics.ResetCachedStateForTests();
        try
        {
            NativeLocalPhysics.TestResolveDataDirectoryOverride = () => tempRoot;
            NativeLocalPhysics.TestSetDataDirectoryOverride = path => configuredDataDir = path;
            NativeLocalPhysics.TestPreloadMapOverride = mapId => preloadedMapIds.Add(mapId);

            LocalPhysicsMapPreloader.EnsureMapPreloaded(30);
            LocalPhysicsMapPreloader.EnsureMapPreloaded(30);
            LocalPhysicsMapPreloader.EnsureMapPreloaded(1);

            Assert.Equal(Path.GetFullPath(tempRoot), Path.GetFullPath(configuredDataDir!).TrimEnd(Path.DirectorySeparatorChar));
            Assert.Equal(new uint[] { 30, 1 }, preloadedMapIds.ToArray());
            Assert.Equal(preloadedMapIds, LocalPhysicsMapPreloader.PreloadedMapIds);
        }
        finally
        {
            NativeLocalPhysics.TestResolveDataDirectoryOverride = null;
            NativeLocalPhysics.TestSetDataDirectoryOverride = null;
            NativeLocalPhysics.TestPreloadMapOverride = null;
            NativeLocalPhysics.ResetCachedStateForTests();

            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void PreloadAvailableMaps_DiscoversMapsAndPreloadsOnlyOnce()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"wowsharp-local-preload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "maps"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "mmaps"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "scenes"));

        File.WriteAllText(Path.Combine(tempRoot, "maps", "0002035.map"), string.Empty);
        File.WriteAllText(Path.Combine(tempRoot, "maps", "0012035.map"), string.Empty);
        File.WriteAllText(Path.Combine(tempRoot, "mmaps", "030.mmap"), string.Empty);
        File.WriteAllText(Path.Combine(tempRoot, "scenes", "489.scene"), string.Empty);

        var preloadedMapIds = new List<uint>();
        string? configuredDataDir = null;

        NativeLocalPhysics.ResetCachedStateForTests();
        try
        {
            NativeLocalPhysics.TestResolveDataDirectoryOverride = () => tempRoot;
            NativeLocalPhysics.TestSetDataDirectoryOverride = path => configuredDataDir = path;
            NativeLocalPhysics.TestPreloadMapOverride = mapId => preloadedMapIds.Add(mapId);

            var first = LocalPhysicsMapPreloader.PreloadAvailableMaps().ToArray();
            var second = LocalPhysicsMapPreloader.PreloadAvailableMaps().ToArray();

            Assert.Equal(Path.GetFullPath(tempRoot), Path.GetFullPath(configuredDataDir!).TrimEnd(Path.DirectorySeparatorChar));
            Assert.Equal(new uint[] { 0, 1, 30, 489 }, first);
            Assert.Equal(first, second);
            Assert.Equal(first, preloadedMapIds.ToArray());
        }
        finally
        {
            NativeLocalPhysics.TestResolveDataDirectoryOverride = null;
            NativeLocalPhysics.TestSetDataDirectoryOverride = null;
            NativeLocalPhysics.TestPreloadMapOverride = null;
            NativeLocalPhysics.ResetCachedStateForTests();

            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }
}
