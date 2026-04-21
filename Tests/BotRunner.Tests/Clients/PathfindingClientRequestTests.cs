using BotRunner.Clients;
using GameData.Core.Models;
using Pathfinding;

namespace BotRunner.Tests.Clients;

public class PathfindingClientRequestTests
{
    [Fact]
    public void GetPathResult_WithNearbyObjects_SendsOverlayAndReturnsMetadata()
    {
        var expectedPath = new[]
        {
            new Game.Position { X = 1.5f, Y = 2.5f, Z = 3.5f },
            new Game.Position { X = 4.5f, Y = 5.5f, Z = 6.5f },
        };
        var client = new CapturingPathfindingClient(new PathfindingResponse
        {
            Path = new CalculatePathResponse
            {
                Result = "blocked_by_dynamic_overlay",
                RawCornerCount = (uint)expectedPath.Length,
                HasBlockedSegment = true,
                BlockedSegmentIndex = 1,
                BlockedReason = "dynamic_overlay",
                MaxAffordance = PathSegmentAffordance.Cliff,
                PathSupported = false,
                CliffCount = 1,
                TotalZLoss = 7.5f,
                MaxSlopeAngleDeg = 52.5f,
                JumpGapCount = 2,
                SafeDropCount = 3,
                UnsafeDropCount = 1,
                BlockedCount = 4,
                MaxClimbHeight = 1.25f,
                MaxGapDistance = 2.5f,
                MaxDropHeight = 7.5f,
            }
        });
        client.Response.Path.Corners.Add(expectedPath);

        var nearbyObjects = new[]
        {
            new DynamicObjectProto
            {
                Guid = 0x1001,
                DisplayId = 17,
                X = 10f,
                Y = 20f,
                Z = 30f,
                Orientation = 1.25f,
                Scale = 1.0f,
                GoState = 1,
            },
            new DynamicObjectProto
            {
                Guid = 0x1002,
                DisplayId = 42,
                X = -5f,
                Y = 8f,
                Z = 12f,
                Orientation = 0.75f,
                Scale = 1.5f,
                GoState = 0,
            }
        };

        var result = client.GetPathResult(
            1,
            new Position(1f, 2f, 3f),
            new Position(4f, 5f, 6f),
            nearbyObjects,
            smoothPath: true);

        Assert.Equal(expectedPath.Length, result.Corners.Length);
        Assert.Equal(expectedPath[0].X, result.Corners[0].X);
        Assert.Equal(expectedPath[1].Z, result.Corners[1].Z);
        Assert.Equal("blocked_by_dynamic_overlay", result.Result);
        Assert.Equal((uint)expectedPath.Length, result.RawCornerCount);
        Assert.Equal(1, result.BlockedSegmentIndex);
        Assert.Equal("dynamic_overlay", result.BlockedReason);
        Assert.Equal(PathSegmentAffordance.Cliff, result.MaxAffordance);
        Assert.False(result.PathSupported);
        Assert.Equal((uint)1, result.CliffCount);
        Assert.Equal(7.5f, result.TotalZLoss);
        Assert.Equal(52.5f, result.MaxSlopeAngleDeg);
        Assert.Equal((uint)2, result.JumpGapCount);
        Assert.Equal((uint)3, result.SafeDropCount);
        Assert.Equal((uint)1, result.UnsafeDropCount);
        Assert.Equal((uint)4, result.BlockedCount);
        Assert.Equal(1.25f, result.MaxClimbHeight);
        Assert.Equal(2.5f, result.MaxGapDistance);
        Assert.Equal(7.5f, result.MaxDropHeight);

        var sent = Assert.IsType<CalculatePathRequest>(client.LastRequest?.Path);
        Assert.Equal(1u, sent.MapId);
        Assert.True(sent.Straight);
        Assert.Equal(nearbyObjects.Length, sent.NearbyObjects.Count);
        Assert.Equal(nearbyObjects[0].Guid, sent.NearbyObjects[0].Guid);
        Assert.Equal(nearbyObjects[1].DisplayId, sent.NearbyObjects[1].DisplayId);
        Assert.Equal(nearbyObjects[1].Scale, sent.NearbyObjects[1].Scale);
    }

    [Fact]
    public void GetPath_CompatibilityOverload_SendsNoOverlayObjects()
    {
        var client = new CapturingPathfindingClient(new PathfindingResponse
        {
            Path = new CalculatePathResponse
            {
                Result = "native_path",
                RawCornerCount = 0,
                BlockedReason = "none",
                PathSupported = true,
            }
        });

        _ = client.GetPath(530, new Position(11f, 12f, 13f), new Position(14f, 15f, 16f));

        var sent = Assert.IsType<CalculatePathRequest>(client.LastRequest?.Path);
        Assert.Equal(530u, sent.MapId);
        Assert.False(sent.Straight);
        Assert.Empty(sent.NearbyObjects);
    }

    private sealed class CapturingPathfindingClient(PathfindingResponse response) : PathfindingClient
    {
        public PathfindingRequest? LastRequest { get; private set; }
        public PathfindingResponse Response { get; } = response;

        protected override PathfindingResponse SendRequest(PathfindingRequest request)
        {
            LastRequest = request;
            return Response;
        }

        protected override PathfindingResponse SendRequest(PathfindingRequest request, int timeoutMs)
        {
            LastRequest = request;
            return Response;
        }
    }
}
