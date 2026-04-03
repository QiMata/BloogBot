using Microsoft.Extensions.Configuration;
using System;

namespace BackgroundBotRunner;

public enum BackgroundPhysicsMode
{
    SharedPathfinding,
    LocalInProcess,
}

public enum BackgroundPhysicsRuntimeMode
{
    SharedPathfinding,
    LocalSceneSlices,
    LocalPreloadedMaps,
}

public static class BackgroundPhysicsModeResolver
{
    public const string EnvironmentVariableName = "WWOW_BG_PHYSICS_MODE";
    private const string ConfigurationKey = "BackgroundBotRunner:PhysicsMode";

    public static BackgroundPhysicsMode Resolve(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return BackgroundPhysicsMode.LocalInProcess;

        return rawValue.Trim().ToLowerInvariant() switch
        {
            "local" or "inprocess" or "native" => BackgroundPhysicsMode.LocalInProcess,
            "shared" or "remote" or "pathfindingservice" => BackgroundPhysicsMode.SharedPathfinding,
            _ => BackgroundPhysicsMode.LocalInProcess,
        };
    }

    public static BackgroundPhysicsMode Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var rawValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(rawValue))
            rawValue = configuration[ConfigurationKey];

        return Resolve(rawValue);
    }

    public static string Describe(BackgroundPhysicsMode mode)
        => mode switch
        {
            BackgroundPhysicsMode.LocalInProcess => "local Navigation.dll physics per process",
            _ => "shared PathfindingService physics",
        };
}

public static class BackgroundPhysicsRuntimeModeResolver
{
    public static BackgroundPhysicsRuntimeMode Resolve(BackgroundPhysicsMode requestedMode, bool sceneDataEndpointConfigured)
        => requestedMode switch
        {
            BackgroundPhysicsMode.SharedPathfinding => BackgroundPhysicsRuntimeMode.SharedPathfinding,
            _ when sceneDataEndpointConfigured => BackgroundPhysicsRuntimeMode.LocalSceneSlices,
            _ => BackgroundPhysicsRuntimeMode.LocalPreloadedMaps,
        };

    public static string Describe(BackgroundPhysicsRuntimeMode mode)
        => mode switch
        {
            BackgroundPhysicsRuntimeMode.LocalSceneSlices => "local Navigation.dll physics with SceneDataService slices",
            BackgroundPhysicsRuntimeMode.LocalPreloadedMaps => "local Navigation.dll physics with on-demand per-map preload",
            _ => "shared PathfindingService physics",
        };
}
