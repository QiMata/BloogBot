using Microsoft.Extensions.Configuration;
using System;

namespace BackgroundBotRunner;

/// <summary>
/// Physics is ALWAYS local via NativeLocalPhysics.Step().
/// This enum controls how scene collision data is loaded.
/// </summary>
public enum BackgroundPhysicsMode
{
    /// <summary>Local physics with on-demand map preload or scene slices.</summary>
    LocalInProcess,
}

/// <summary>
/// Runtime resolution of how local physics gets its scene data.
/// </summary>
public enum BackgroundPhysicsRuntimeMode
{
    /// <summary>Scene triangles streamed from SceneDataService.</summary>
    LocalSceneSlices,
    /// <summary>Full map data preloaded from disk (mmaps/vmaps).</summary>
    LocalPreloadedMaps,
}

public static class BackgroundPhysicsModeResolver
{
    public const string EnvironmentVariableName = "WWOW_BG_PHYSICS_MODE";
    private const string ConfigurationKey = "BackgroundBotRunner:PhysicsMode";

    public static BackgroundPhysicsMode Resolve(string? rawValue)
        => BackgroundPhysicsMode.LocalInProcess;

    public static BackgroundPhysicsMode Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return BackgroundPhysicsMode.LocalInProcess;
    }

    public static string Describe(BackgroundPhysicsMode mode)
        => "local Navigation.dll physics per process";
}

public static class BackgroundPhysicsRuntimeModeResolver
{
    public static BackgroundPhysicsRuntimeMode Resolve(BackgroundPhysicsMode requestedMode, bool sceneDataEndpointConfigured)
        => sceneDataEndpointConfigured
            ? BackgroundPhysicsRuntimeMode.LocalSceneSlices
            : BackgroundPhysicsRuntimeMode.LocalPreloadedMaps;

    public static string Describe(BackgroundPhysicsRuntimeMode mode)
        => mode switch
        {
            BackgroundPhysicsRuntimeMode.LocalSceneSlices => "local Navigation.dll physics with SceneDataService slices",
            BackgroundPhysicsRuntimeMode.LocalPreloadedMaps => "local Navigation.dll physics with on-demand per-map preload",
            _ => "local Navigation.dll physics",
        };
}
