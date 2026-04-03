using BackgroundBotRunner;

namespace BotRunner.Tests;

public class BackgroundPhysicsModeResolverTests
{
    [Theory]
    [InlineData(null, BackgroundPhysicsMode.LocalInProcess)]
    [InlineData("", BackgroundPhysicsMode.LocalInProcess)]
    [InlineData("shared", BackgroundPhysicsMode.SharedPathfinding)]
    [InlineData("remote", BackgroundPhysicsMode.SharedPathfinding)]
    [InlineData("pathfindingservice", BackgroundPhysicsMode.SharedPathfinding)]
    [InlineData("local", BackgroundPhysicsMode.LocalInProcess)]
    [InlineData("inprocess", BackgroundPhysicsMode.LocalInProcess)]
    [InlineData("native", BackgroundPhysicsMode.LocalInProcess)]
    [InlineData("unexpected", BackgroundPhysicsMode.LocalInProcess)]
    public void Resolve_MapsConfiguredModes(string? rawValue, BackgroundPhysicsMode expected)
    {
        var actual = BackgroundPhysicsModeResolver.Resolve(rawValue);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(BackgroundPhysicsMode.SharedPathfinding, false, BackgroundPhysicsRuntimeMode.SharedPathfinding)]
    [InlineData(BackgroundPhysicsMode.SharedPathfinding, true, BackgroundPhysicsRuntimeMode.SharedPathfinding)]
    [InlineData(BackgroundPhysicsMode.LocalInProcess, true, BackgroundPhysicsRuntimeMode.LocalSceneSlices)]
    [InlineData(BackgroundPhysicsMode.LocalInProcess, false, BackgroundPhysicsRuntimeMode.LocalPreloadedMaps)]
    public void ResolveRuntimeMode_ChoosesExpectedLocalTransportFromConfiguredEndpoint(
        BackgroundPhysicsMode requestedMode,
        bool sceneDataEndpointConfigured,
        BackgroundPhysicsRuntimeMode expected)
    {
        var actual = BackgroundPhysicsRuntimeModeResolver.Resolve(requestedMode, sceneDataEndpointConfigured);

        Assert.Equal(expected, actual);
    }
}
