// PhysicsTestFixtures.cs - Shared test fixtures for physics tests

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Navigation.Physics.Tests.Helpers;

namespace Navigation.Physics.Tests;

using static NavigationInterop;

/// <summary>
/// Collection definition that shares a single PhysicsEngineFixture across all test classes
/// tagged with [Collection("PhysicsEngine")]. This avoids re-initializing the native physics
/// engine (VMAP, map loader, etc.) for every test class.
/// </summary>
[CollectionDefinition("PhysicsEngine")]
public class PhysicsEngineCollection : ICollectionFixture<PhysicsEngineFixture> { }

/// <summary>
/// Module initializer ensures WWOW_DATA_DIR is set and crash dialogs are suppressed
/// before any test runs.
/// </summary>
public static class PhysicsTestModuleInit
{
    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint uMode);

    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOGPFAULTERRORBOX = 0x0002;
    private const uint SEM_NOOPENFILEERRORBOX = 0x8000;

    [ModuleInitializer]
    public static void Init()
    {
        // Suppress Windows Error Reporting dialogs and CRT assertion popups
        SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
        PhysicsEngineFixture.EnsureDataDir();
    }
}

/// <summary>
/// Fixture that initializes the physics engine once for all tests that need it.
/// </summary>
public class PhysicsEngineFixture : IDisposable
{
    public bool IsInitialized { get; }

    /// <summary>
    /// Shared replay cache — each recording replayed at most once across all test classes.
    /// </summary>
    public ReplayResultsCache ReplayCache { get; } = new();

    public PhysicsEngineFixture()
    {
        try
        {
            EnsureDataDir();
            // InitializePhysics now auto-loads the displayId→model mapping
            IsInitialized = InitializePhysics();

            // Also initialize MapLoader for ADT terrain data (used by GetGroundZ, SweepCapsule)
            if (IsInitialized)
            {
                var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR") ?? "";
                var mapsPath = string.IsNullOrEmpty(dataDir)
                    ? "maps/"
                    : Path.Combine(dataDir, "maps") + Path.DirectorySeparatorChar;
                try { InitializeMapLoader(mapsPath); } catch { /* optional */ }

                // Set scenes directory for pre-cached collision data (production code path).
                // SceneQuery::EnsureMapLoaded() will auto-discover .scene files here.
                var scenesPath = string.IsNullOrEmpty(dataDir)
                    ? "scenes/"
                    : Path.Combine(dataDir, "scenes") + Path.DirectorySeparatorChar;
                try { SetScenesDir(scenesPath); } catch { /* optional */ }
            }
        }
        catch (DllNotFoundException)
        {
            // Navigation.dll not found - tests requiring native code will be skipped
            IsInitialized = false;
        }
        catch (Exception)
        {
            IsInitialized = false;
        }
    }

    /// <summary>
    /// Sets WWOW_DATA_DIR if not already set, pointing to the build output
    /// that contains mmaps/, vmaps/, maps/ subdirectories.
    /// </summary>
    public static void EnsureDataDir()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WWOW_DATA_DIR")))
            return;

        // Everything outputs to AppContext.BaseDirectory (Bot/$(Config)/net8.0/).
        // Data directories (maps, vmaps, mmaps, scenes) live here too.
        var baseDir = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Directory.Exists(Path.Combine(baseDir, "mmaps")))
        {
            Environment.SetEnvironmentVariable("WWOW_DATA_DIR", baseDir);
            return;
        }
    }

    public void Dispose()
    {
        if (IsInitialized)
        {
            try
            {
                ShutdownPhysics();
            }
            catch
            {
                // Ignore shutdown errors
            }
        }
    }
}

/// <summary>
/// Fixture that initializes map data for tests that need terrain.
/// </summary>
public class MapDataFixture : IDisposable
{
    public bool IsInitialized { get; }
    public string DataPath { get; }

    public MapDataFixture()
    {
        // Try common paths for map data
        var possiblePaths = new[]
        {
            "maps/",
            "../maps/",
            "../../maps/",
            Environment.GetEnvironmentVariable("WWOW_MAP_PATH") ?? ""
        };

        DataPath = possiblePaths.FirstOrDefault(p => 
            !string.IsNullOrEmpty(p) && Directory.Exists(p)) ?? "maps/";

        try
        {
            IsInitialized = InitializeMapLoader(DataPath);
        }
        catch
        {
            IsInitialized = false;
        }
    }

    public bool LoadTile(uint mapId, uint tileX, uint tileY)
    {
        if (!IsInitialized) return false;
        
        try
        {
            return LoadMapTile(mapId, tileX, tileY);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        // Map loader cleanup is handled by ShutdownPhysics
    }
}
