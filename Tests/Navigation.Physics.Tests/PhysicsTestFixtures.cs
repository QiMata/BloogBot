// PhysicsTestFixtures.cs - Shared test fixtures for physics tests

namespace Navigation.Physics.Tests;

using static NavigationInterop;

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
