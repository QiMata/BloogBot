using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public sealed class SceneEnvironmentFlagTests(PhysicsEngineFixture fixture, ITestOutputHelper output) : IDisposable
{
    private const uint TestMapId = 1;
    private readonly PhysicsEngineFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    public void Dispose()
    {
        if (_fixture.IsInitialized)
            ClearSceneCache(TestMapId);
    }

    [SkippableFact]
    public void StepPhysicsV2_InjectedInteriorSupport_SetsIndoorsFlag()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        PreloadMap(TestMapId);
        ClearSceneCache(TestMapId);

        var triangles = new[]
        {
            new InjectedTriangle
            {
                V0X = 0f, V0Y = 0f, V0Z = 0f,
                V1X = 6f, V1Y = 0f, V1Z = 0f,
                V2X = 0f, V2Y = 6f, V2Z = 0f,
                SourceType = 0u,
                InstanceId = 0x1234u,
                GroupFlags = 0x00002000u,
            }
        };

        Assert.True(InjectSceneTriangles(TestMapId, -1f, -1f, 8f, 8f, triangles, triangles.Length));

        var input = new PhysicsInput
        {
            X = 1f,
            Y = 1f,
            Z = 0f,
            Height = 2f,
            Radius = 0.5f,
            MapId = TestMapId,
            DeltaTime = 1f / 60f,
            PrevGroundZ = 0f,
            PrevGroundNz = 1f,
            WasGrounded = 1u,
        };

        var output = StepPhysicsV2(ref input);
        _output.WriteLine($"environment=0x{output.EnvironmentFlags:X8} groundZ={output.GroundZ:F3} moveFlags=0x{output.MoveFlags:X8}");

        Assert.NotEqual(0u, output.EnvironmentFlags & (uint)SceneEnvironmentFlags.Indoors);
        Assert.Equal(0u, output.EnvironmentFlags & (uint)SceneEnvironmentFlags.MountAllowed);
        Assert.False(((SceneEnvironmentFlags)output.EnvironmentFlags).AllowsMountByEnvironment());
    }

    [SkippableFact]
    public void StepPhysicsV2_InjectedMountAllowedInterior_SetsBothFlags()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        PreloadMap(TestMapId);
        ClearSceneCache(TestMapId);

        var triangles = new[]
        {
            new InjectedTriangle
            {
                V0X = 0f, V0Y = 0f, V0Z = 0f,
                V1X = 6f, V1Y = 0f, V1Z = 0f,
                V2X = 0f, V2Y = 6f, V2Z = 0f,
                SourceType = 0u,
                InstanceId = 0x5678u,
                GroupFlags = 0x0000A000u,
            }
        };

        Assert.True(InjectSceneTriangles(TestMapId, -1f, -1f, 8f, 8f, triangles, triangles.Length));

        var input = new PhysicsInput
        {
            X = 1f,
            Y = 1f,
            Z = 0f,
            Height = 2f,
            Radius = 0.5f,
            MapId = TestMapId,
            DeltaTime = 1f / 60f,
            PrevGroundZ = 0f,
            PrevGroundNz = 1f,
            WasGrounded = 1u,
        };

        var output = StepPhysicsV2(ref input);
        _output.WriteLine($"environment=0x{output.EnvironmentFlags:X8} groundZ={output.GroundZ:F3} moveFlags=0x{output.MoveFlags:X8}");

        uint expected = (uint)(SceneEnvironmentFlags.Indoors | SceneEnvironmentFlags.MountAllowed);
        Assert.Equal(expected, output.EnvironmentFlags & expected);
        Assert.True(((SceneEnvironmentFlags)output.EnvironmentFlags).AllowsMountByEnvironment());
    }

    [SkippableFact]
    public void StepPhysicsV2_KnownIndoorSupport_BlocksMountByEnvironment()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var output = RunIdleStepAt(new WorldPosition(
            PhysicsSweepCoordinates.IndoorAreas.WailingCaverns.MapId,
            PhysicsSweepCoordinates.IndoorAreas.WailingCaverns.CenterX,
            PhysicsSweepCoordinates.IndoorAreas.WailingCaverns.CenterY,
            PhysicsSweepCoordinates.IndoorAreas.WailingCaverns.CenterZ));
        var flags = (SceneEnvironmentFlags)output.EnvironmentFlags;

        Assert.True(flags.IsIndoors());
        Assert.False(flags.AllowsMountByEnvironment());
    }

    [SkippableFact]
    public void StepPhysicsV2_KnownOutdoorSupport_AllowsMountByEnvironment()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var output = RunIdleStepAt(WoWWorldCoordinates.Durotar.ValleyOfTrials.SpawnPoint);
        var flags = (SceneEnvironmentFlags)output.EnvironmentFlags;

        Assert.False(flags.IsIndoors());
        Assert.True(flags.AllowsMountByEnvironment());
    }

    private PhysicsOutput RunIdleStepAt(WorldPosition position)
    {
        SetSceneSliceMode(false);
        ClearSceneCache(position.MapId);
        PreloadMap(position.MapId);

        float groundZ = GetGroundZ(position.MapId, position.X, position.Y, position.Z + 80f, 160f);
        Assert.True(groundZ > -100000f, $"Failed to resolve ground at map={position.MapId} ({position.X:F3}, {position.Y:F3}, {position.Z:F3})");

        var input = new PhysicsInput
        {
            X = position.X,
            Y = position.Y,
            Z = groundZ,
            Height = 2f,
            Radius = 0.5f,
            MapId = position.MapId,
            DeltaTime = 1f / 60f,
            PrevGroundZ = groundZ,
            PrevGroundNz = 1f,
            WasGrounded = 1u,
        };

        var output = StepPhysicsV2(ref input);
        _output.WriteLine(
            $"probe map={position.MapId} pos=({position.X:F3}, {position.Y:F3}, {groundZ:F3}) " +
            $"environment=0x{output.EnvironmentFlags:X8} groundZ={output.GroundZ:F3} moveFlags=0x{output.MoveFlags:X8}");
        return output;
    }
}
