// PhysicsTestFixtures.cs - Shared test fixtures for physics tests

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Navigation.Physics.Tests;

using static NavigationInterop;

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

    public PhysicsEngineFixture()
    {
        try
        {
            EnsureDataDir();
            IsInitialized = InitializePhysics();
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

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Bot", "Debug", "net8.0")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Bot", "Release", "net8.0")),
            @"E:\repos\BloogBot\Bot\Debug\net8.0",
        };

        foreach (var dir in candidates)
        {
            if (Directory.Exists(Path.Combine(dir, "mmaps")))
            {
                Environment.SetEnvironmentVariable("WWOW_DATA_DIR", dir);
                return;
            }
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
