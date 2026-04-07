using System;
using System.Collections.Generic;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using SceneData;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;

namespace WoWSharpClient.Tests.Movement;

/// <summary>
/// Integration tests for the MovementController -> SceneDataClient -> NativeLocalPhysics pipeline.
/// Verifies that scene triangles flow from SceneDataService tile responses through protobuf
/// deserialization into InjectedTriangle structs, and that the physics step receives
/// correct scene-backed input when scene slice mode is enabled.
///
/// These tests do NOT require Navigation.dll or a live server — they use test overrides
/// to intercept at each layer boundary and verify the data flow.
/// </summary>
public sealed class SceneDataPhysicsPipelineTests : IDisposable
{
    private readonly Mock<WoWClient> _mockClient;
    private readonly WoWLocalPlayer _player;
    private readonly List<(Opcode opcode, byte[] buffer)> _sentPackets = [];

    public SceneDataPhysicsPipelineTests()
    {
        _mockClient = new Mock<WoWClient>();
        _player = new WoWLocalPlayer(new HighGuid(42))
        {
            Position = new Position(1629f, -4373f, 50f),
            Facing = 1.57f,
            WalkSpeed = 2.5f,
            RunSpeed = 7.0f,
            RunBackSpeed = 4.5f,
            SwimSpeed = 4.722f,
            SwimBackSpeed = 2.5f,
            MapId = 1,
        };

        _mockClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((op, buf, _) => _sentPackets.Add((op, buf)))
            .Returns(Task.CompletedTask);
    }

    public void Dispose()
    {
        NativeLocalPhysics.TestStepOverride = null;
        NativeLocalPhysics.TestClearSceneCacheOverride = null;
        NativeLocalPhysics.TestSetSceneSliceModeOverride = null;
        NativeLocalPhysics.TestPreloadMapOverride = null;
        NativeLocalPhysics.TestSetDataDirectoryOverride = null;
        NativeLocalPhysics.TestResolveDataDirectoryOverride = null;
        SceneDataClient.TestSendTileRequestOverride = null;
        SceneDataClient.TestEnsureSceneDataAroundOverride = null;
        SceneDataClient.TestInjectOverride = null;
        SceneDataClient.TestUtcNowOverride = null;
    }

    /// <summary>Build a tile response with the given triangles for any tile request.</summary>
    private static SceneTileResponse BuildTileResponse(SceneTileRequest req, params (float[] verts, float[] normal, bool walkable)[] triangles)
    {
        var response = new SceneTileResponse
        {
            MapId = req.MapId,
            TileX = req.TileX,
            TileY = req.TileY,
            Success = true,
            TriangleCount = (uint)triangles.Length,
        };

        var (minX, minY, maxX, maxY) = SceneDataClient.TileBounds(req.TileX, req.TileY);
        response.MinX = minX;
        response.MinY = minY;
        response.MaxX = maxX;
        response.MaxY = maxY;

        foreach (var (verts, normal, walkable) in triangles)
        {
            response.TriangleData.AddRange(verts);
            response.NormalData.AddRange(normal);
            response.Walkable.Add(walkable);
        }

        return response;
    }

    /// <summary>Build a tile response with a single flat ground triangle.</summary>
    private static SceneTileResponse BuildGroundTileResponse(SceneTileRequest req)
    {
        return BuildTileResponse(req,
            (new float[] { 1620, -4380, 34, 1640, -4380, 34, 1630, -4360, 34 },
             new float[] { 0, 0, 1 },
             true));
    }

    // =========================================================================
    // 1. SceneSliceMode enabled when SceneDataClient present
    // =========================================================================

    [Fact]
    public void Constructor_WithSceneDataClient_EnablesSceneSliceMode()
    {
        bool? sliceModeEnabled = null;
        NativeLocalPhysics.TestSetSceneSliceModeOverride = v => sliceModeEnabled = v;

        _ = new MovementController(_mockClient.Object, _player,
            new SceneDataClient(Mock.Of<ILogger>()));

        Assert.True(sliceModeEnabled);
    }

    [Fact]
    public void Constructor_WithoutSceneDataClient_DoesNotEnableSceneSliceMode()
    {
        bool? sliceModeEnabled = null;
        NativeLocalPhysics.TestSetSceneSliceModeOverride = v => sliceModeEnabled = v;

        _ = new MovementController(_mockClient.Object, _player);

        Assert.Null(sliceModeEnabled);
    }

    // =========================================================================
    // 2. EnsureLocalSceneDataFresh called during physics step
    // =========================================================================

    [Fact]
    public void Update_WithSceneDataClient_CallsEnsureSceneDataAround()
    {
        uint? requestedMapId = null;
        float? requestedX = null, requestedY = null;

        SceneDataClient.TestEnsureSceneDataAroundOverride = (mapId, x, y) =>
        {
            requestedMapId = mapId;
            requestedX = x;
            requestedY = y;
            return true;
        };

        NativeLocalPhysics.TestSetSceneSliceModeOverride = _ => { };
        NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
        {
            X = input.X, Y = input.Y, Z = input.Z,
            GroundZ = input.Z, GroundNz = 1,
            MoveFlags = input.MoveFlags,
        };

        var sceneClient = new SceneDataClient(Mock.Of<ILogger>());
        var controller = new MovementController(_mockClient.Object, _player, sceneClient);

        _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        controller.Update(0.016f, 1000);

        Assert.Equal(1u, requestedMapId);
        Assert.NotNull(requestedX);
        Assert.NotNull(requestedY);
    }

    [Fact]
    public void Update_WithoutSceneDataClient_DoesNotCallEnsureSceneDataAround()
    {
        bool sceneCalled = false;
        SceneDataClient.TestEnsureSceneDataAroundOverride = (_, _, _) =>
        {
            sceneCalled = true;
            return true;
        };

        NativeLocalPhysics.TestSetSceneSliceModeOverride = _ => { };
        NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
        {
            X = input.X, Y = input.Y, Z = input.Z,
            GroundZ = input.Z, GroundNz = 1,
            MoveFlags = input.MoveFlags,
        };

        var controller = new MovementController(_mockClient.Object, _player);

        _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        controller.Update(0.016f, 1000);

        Assert.False(sceneCalled);
    }

    // =========================================================================
    // 3. Full pipeline: SceneDataService tile response -> triangle unpacking -> inject
    // =========================================================================

    [Fact]
    public void Pipeline_TileResponseTriangles_UnpackedCorrectlyToInjectedTriangleStructs()
    {
        NativePhysics.InjectedTriangle[]? capturedTriangles = null;
        uint capturedMapId = 0;

        // Build tile responses with 3 triangles (on the center tile)
        SceneDataClient.TestSendTileRequestOverride = request =>
        {
            // Only center tile (29,41) gets triangles; others empty
            var (cx, cy) = SceneDataClient.WorldToTile(1629f, -4373f);
            if (request.TileX == cx && request.TileY == cy)
            {
                return BuildTileResponse(request,
                    // Triangle 0: flat ground at Z=34
                    (new float[] { 1620, -4380, 34, 1640, -4380, 34, 1630, -4360, 34 },
                     new float[] { 0, 0, 1 }, true),
                    // Triangle 1: sloped terrain
                    (new float[] { 1640, -4380, 34, 1660, -4380, 38, 1650, -4360, 36 },
                     new float[] { 0, -0.2f, 0.98f }, true),
                    // Triangle 2: wall (non-walkable)
                    (new float[] { 1620, -4380, 34, 1620, -4380, 40, 1620, -4360, 37 },
                     new float[] { 1, 0, 0 }, false)
                );
            }

            return new SceneTileResponse
            {
                MapId = request.MapId, TileX = request.TileX, TileY = request.TileY,
                Success = true, TriangleCount = 0,
            };
        };

        // Capture injected triangles
        SceneDataClient.TestInjectOverride = (mapId, minX, minY, maxX, maxY, triangles) =>
        {
            capturedMapId = mapId;
            capturedTriangles = (NativePhysics.InjectedTriangle[])triangles.Clone();
            return true;
        };

        var client = new SceneDataClient(Mock.Of<ILogger>());
        var result = client.EnsureSceneDataAround(1, 1629f, -4373f);

        Assert.True(result);
        Assert.Equal(1u, capturedMapId);
        Assert.NotNull(capturedTriangles);
        Assert.Equal(3, capturedTriangles!.Length);

        // Triangle 0: verify all vertices and normal
        Assert.Equal(1620f, capturedTriangles[0].V0X);
        Assert.Equal(-4380f, capturedTriangles[0].V0Y);
        Assert.Equal(34f, capturedTriangles[0].V0Z);
        Assert.Equal(1640f, capturedTriangles[0].V1X);
        Assert.Equal(-4380f, capturedTriangles[0].V1Y);
        Assert.Equal(34f, capturedTriangles[0].V1Z);
        Assert.Equal(1630f, capturedTriangles[0].V2X);
        Assert.Equal(-4360f, capturedTriangles[0].V2Y);
        Assert.Equal(34f, capturedTriangles[0].V2Z);
        Assert.Equal(0f, capturedTriangles[0].NX);
        Assert.Equal(0f, capturedTriangles[0].NY);
        Assert.Equal(1f, capturedTriangles[0].NZ);

        // Triangle 1: sloped
        Assert.Equal(1660f, capturedTriangles[1].V1X);
        Assert.Equal(38f, capturedTriangles[1].V1Z);
        Assert.Equal(-0.2f, capturedTriangles[1].NY, precision: 2);
        Assert.Equal(0.98f, capturedTriangles[1].NZ, precision: 2);

        // Triangle 2: wall normal
        Assert.Equal(1f, capturedTriangles[2].NX);
        Assert.Equal(0f, capturedTriangles[2].NY);
        Assert.Equal(0f, capturedTriangles[2].NZ);
    }

    // =========================================================================
    // 4. End-to-end: MovementController.Update triggers scene data + physics
    // =========================================================================

    [Fact]
    public void EndToEnd_Update_FetchesSceneData_RunsPhysicsWithCorrectInput()
    {
        bool sceneDataRequested = false;
        NativePhysics.PhysicsInput? capturedPhysicsInput = null;

        NativeLocalPhysics.TestSetSceneSliceModeOverride = _ => { };

        // Mock scene data service tile response
        SceneDataClient.TestSendTileRequestOverride = request =>
        {
            sceneDataRequested = true;
            return BuildGroundTileResponse(request);
        };

        // Capture inject call (prevents P/Invoke)
        SceneDataClient.TestInjectOverride = (_, _, _, _, _, _) => true;

        // Capture physics step input
        NativeLocalPhysics.TestStepOverride = input =>
        {
            capturedPhysicsInput = input;
            return new NativePhysics.PhysicsOutput
            {
                X = input.X, Y = input.Y, Z = 34f,
                GroundZ = 34f, GroundNz = 1f,
                MoveFlags = input.MoveFlags & ~(uint)MovementFlags.MOVEFLAG_FALLINGFAR,
            };
        };

        var sceneClient = new SceneDataClient(Mock.Of<ILogger>());
        var controller = new MovementController(_mockClient.Object, _player, sceneClient);

        _player.Position = new Position(1629f, -4373f, 50f);
        _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        controller.Update(0.016f, 1000);

        Assert.True(sceneDataRequested);
        Assert.NotNull(capturedPhysicsInput);
        Assert.Equal(1629f, capturedPhysicsInput!.Value.X);
        Assert.Equal(-4373f, capturedPhysicsInput.Value.Y);
        Assert.Equal(50f, capturedPhysicsInput.Value.Z);
        Assert.Equal(1u, capturedPhysicsInput.Value.MapId);
        Assert.Equal(0.016f, capturedPhysicsInput.Value.DeltaTime, precision: 3);

        Assert.Equal(34f, _player.Position.Z);
    }

    [Fact]
    public void EndToEnd_MultipleFrames_SceneDataOnlyFetchedOnce()
    {
        int sceneRequestCount = 0;
        int physicsStepCount = 0;

        NativeLocalPhysics.TestSetSceneSliceModeOverride = _ => { };

        SceneDataClient.TestSendTileRequestOverride = request =>
        {
            sceneRequestCount++;
            return BuildGroundTileResponse(request);
        };

        SceneDataClient.TestInjectOverride = (_, _, _, _, _, _) => true;

        NativeLocalPhysics.TestStepOverride = input =>
        {
            physicsStepCount++;
            return new NativePhysics.PhysicsOutput
            {
                X = input.X, Y = input.Y, Z = input.Z,
                GroundZ = input.Z, GroundNz = 1f,
                MoveFlags = input.MoveFlags,
            };
        };

        var sceneClient = new SceneDataClient(Mock.Of<ILogger>());
        var controller = new MovementController(_mockClient.Object, _player, sceneClient);

        _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        // Run 10 physics frames at same position
        for (int i = 0; i < 10; i++)
            controller.Update(0.016f, (uint)(1000 + i * 16));

        // Scene data requested once for 9 tiles (3x3 neighborhood), not per frame
        Assert.Equal(9, sceneRequestCount);
        Assert.Equal(10, physicsStepCount);
    }

    // =========================================================================
    // 5. Protobuf response integrity — field count validation
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void ProtobufResponse_TriangleDataSizing_MatchesCount(int triangleCount)
    {
        var response = new SceneTileResponse
        {
            MapId = 1,
            Success = true,
            TriangleCount = (uint)triangleCount,
        };

        for (int i = 0; i < triangleCount; i++)
        {
            response.TriangleData.AddRange(new float[] { i, i, i, i, i, i, i, i, i });
            response.NormalData.AddRange(new float[] { 0, 0, 1 });
            response.Walkable.Add(true);
        }

        Assert.Equal(triangleCount * 9, response.TriangleData.Count);
        Assert.Equal(triangleCount * 3, response.NormalData.Count);
        Assert.Equal(triangleCount, response.Walkable.Count);
    }

    // =========================================================================
    // 6. SceneData failure -> physics still runs (graceful degradation)
    // =========================================================================

    [Fact]
    public void Update_SceneDataFails_PhysicsStillRuns()
    {
        int physicsStepCount = 0;

        NativeLocalPhysics.TestSetSceneSliceModeOverride = _ => { };

        // Scene data fails
        SceneDataClient.TestSendTileRequestOverride = _ =>
            new SceneTileResponse { Success = false, ErrorMessage = "service down" };

        NativeLocalPhysics.TestStepOverride = input =>
        {
            physicsStepCount++;
            return new NativePhysics.PhysicsOutput
            {
                X = input.X, Y = input.Y, Z = input.Z - 1f,
                GroundZ = -200000f, GroundNz = 1f,
                MoveFlags = (uint)MovementFlags.MOVEFLAG_FALLINGFAR,
                FallTime = 50,
            };
        };

        var sceneClient = new SceneDataClient(Mock.Of<ILogger>());
        var controller = new MovementController(_mockClient.Object, _player, sceneClient);

        _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        controller.Update(0.016f, 1000);

        Assert.Equal(1, physicsStepCount);
    }
}
