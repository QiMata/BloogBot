using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PathfindingService.Tests;

public sealed class PathfindingSocketServerPreloadConfigTests
{
    [Fact]
    public void ParsePreloadMapIds_HandlesDisabledAndExplicitMapLists()
    {
        Assert.Empty(PathfindingSocketServer.ParsePreloadMapIds("none", dataRoot: null));
        Assert.Empty(PathfindingSocketServer.ParsePreloadMapIds("false", dataRoot: null));

        var parsed = PathfindingSocketServer.ParsePreloadMapIds("1; 0,389|1", dataRoot: null);

        Assert.Equal([0u, 1u, 389u], parsed);
    }

    [Fact]
    public void ParsePreloadMapIds_AllDiscoversAvailableMMapFiles()
    {
        var dataRoot = CreateTempDataRoot();
        try
        {
            File.WriteAllBytes(Path.Combine(dataRoot, "mmaps", "001.mmap"), []);
            File.WriteAllBytes(Path.Combine(dataRoot, "mmaps", "000.mmap"), []);
            File.WriteAllBytes(Path.Combine(dataRoot, "mmaps", "389.mmap"), []);

            var parsed = PathfindingSocketServer.ParsePreloadMapIds("all", dataRoot);

            Assert.Equal([0u, 1u, 389u], parsed);
        }
        finally
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveConfiguredPreloadMapSetting_PrefersNativeEnvironmentOverride()
    {
        var previous = Environment.GetEnvironmentVariable("WWOW_NAVIGATION_PRELOAD_MAPS");
        try
        {
            Environment.SetEnvironmentVariable("WWOW_NAVIGATION_PRELOAD_MAPS", "all");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Navigation:PreloadMaps"] = "0,1",
                })
                .Build();

            var resolved = PathfindingSocketServer.ResolveConfiguredPreloadMapSetting(configuration);

            Assert.Equal("all", resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WWOW_NAVIGATION_PRELOAD_MAPS", previous);
        }
    }

    [Fact]
    public void IsDynamicObjectOverlayEnabled_DefaultsOffAndHonorsConfiguration()
    {
        var previous = Environment.GetEnvironmentVariable("WWOW_ENABLE_PATHFINDING_DYNAMIC_OVERLAY");
        try
        {
            Environment.SetEnvironmentVariable("WWOW_ENABLE_PATHFINDING_DYNAMIC_OVERLAY", null);
            var disabled = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Navigation:EnableDynamicObjectOverlay"] = "false",
                })
                .Build();
            var enabled = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Navigation:EnableDynamicObjectOverlay"] = "true",
                })
                .Build();

            Assert.False(PathfindingSocketServer.IsDynamicObjectOverlayEnabled());
            Assert.False(PathfindingSocketServer.IsDynamicObjectOverlayEnabled(disabled));
            Assert.True(PathfindingSocketServer.IsDynamicObjectOverlayEnabled(enabled));

            Environment.SetEnvironmentVariable("WWOW_ENABLE_PATHFINDING_DYNAMIC_OVERLAY", "1");
            Assert.True(PathfindingSocketServer.IsDynamicObjectOverlayEnabled(disabled));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WWOW_ENABLE_PATHFINDING_DYNAMIC_OVERLAY", previous);
        }
    }

    private static string CreateTempDataRoot()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "wwow-preload-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dataRoot, "mmaps"));
        return dataRoot;
    }
}
