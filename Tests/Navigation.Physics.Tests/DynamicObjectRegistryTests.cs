using Xunit;
using Xunit.Abstractions;
using System;

namespace Navigation.Physics.Tests;

using static NavigationInterop;

[Collection("PhysicsEngine")]
public class DynamicObjectRegistryTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
{
    private readonly PhysicsEngineFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void SegmentIntersectsDynamicObjectsDetailed_ReturnsBlockingObjectIdentity()
    {
        if (!_fixture.IsInitialized)
            return;

        const ulong guid = 0xDEADBEEF455UL;
        const uint displayId = 455;
        const uint mapId = 0;

        ClearAllDynamicObjects();
        try
        {
            Assert.True(RegisterDynamicObject(guid, 0, displayId, mapId, 1.0f));
            UpdateDynamicObjectPosition(guid, 1544f, 241f, 55f, 0f, 0u);

            var hit = SegmentIntersectsDynamicObjectsDetailed(
                mapId,
                1544f, 241f, 70f,
                1544f, 241f, 40f,
                out var blockingInstanceId,
                out var blockingGuid,
                out var blockingDisplayId);

            _output.WriteLine(
                $"hit={hit} instance=0x{blockingInstanceId:X8} guid=0x{blockingGuid:X} display={blockingDisplayId}");

            Assert.True(hit);
            Assert.NotEqual(0u, blockingInstanceId);
            Assert.Equal(guid, blockingGuid);
            Assert.Equal(displayId, blockingDisplayId);
        }
        finally
        {
            ClearAllDynamicObjects();
        }
    }

    [Fact]
    public void FindPath_WithActiveDynamicOverlay_ReformsRouteAroundBlockingObject()
    {
        if (!_fixture.IsInitialized)
            return;

        const ulong guid = 0xDEADBEEF456UL;
        const uint displayId = 455;
        const uint mapId = 0;
        var start = new NavigationInterop.Vector3(1544f, 200f, 55f);
        var end = new NavigationInterop.Vector3(1544f, 280f, 55f);

        ClearAllDynamicObjects();
        try
        {
            Assert.True(RegisterDynamicObject(guid, 0, displayId, mapId, 1.0f));
            UpdateDynamicObjectPosition(guid, 1544f, 241f, 55f, 0f, 0u);

            var directHit = SegmentIntersectsDynamicObjectsDetailed(
                mapId,
                start.X, start.Y, start.Z,
                end.X, end.Y, end.Z,
                out var blockingInstanceId,
                out var blockingGuid,
                out var blockingDisplayId);

            _output.WriteLine(
                $"directHit={directHit} instance=0x{blockingInstanceId:X8} guid=0x{blockingGuid:X} display={blockingDisplayId}");

            Assert.True(directHit);
            Assert.Equal(guid, blockingGuid);
            Assert.Equal(displayId, blockingDisplayId);

            var path = FindPath(mapId, start, end, smoothPath: true);
            for (var i = 0; i < path.Length; i++)
                _output.WriteLine($"overlay path[{i}]={path[i]}");

            Assert.NotEmpty(path);
            Assert.True(path.Length >= 3, $"Expected an overlay-aware detour path, got {path.Length} points.");

            for (var i = 0; i < path.Length - 1; i++)
            {
                var segmentHit = SegmentIntersectsDynamicObjectsDetailed(
                    mapId,
                    path[i].X, path[i].Y, path[i].Z,
                    path[i + 1].X, path[i + 1].Y, path[i + 1].Z,
                    out _,
                    out _,
                    out _);

                Assert.False(segmentHit, $"Segment {i}->{i + 1} still intersects the registered dynamic object.");
            }
        }
        finally
        {
            ClearAllDynamicObjects();
        }
    }

    [Fact]
    public void FindPathCorridor_WithActiveDynamicOverlay_ReturnsRepairedBlockIdentity()
    {
        if (!_fixture.IsInitialized)
            return;

        const ulong guid = 0xDEADBEEF457UL;
        const uint displayId = 455;
        const uint mapId = 0;
        var start = new NavigationInterop.Vector3(1544f, 200f, 55f);
        var end = new NavigationInterop.Vector3(1544f, 280f, 55f);

        ClearAllDynamicObjects();
        try
        {
            Assert.True(RegisterDynamicObject(guid, 0, displayId, mapId, 1.0f));
            UpdateDynamicObjectPosition(guid, 1544f, 241f, 55f, 0f, 0u);

            var result = FindPathCorridor(mapId, start, end);
            try
            {
                _output.WriteLine(
                    $"handle={result.Handle} corners={result.CornerCount} blockedIdx={result.BlockedSegmentIndex} instance=0x{result.BlockingInstanceId:X8} guid=0x{result.BlockingGuid:X} display={result.BlockingDisplayId} flags=0x{result.Flags:X8}");

                Assert.NotEqual(0u, result.Handle);
                Assert.True(result.CornerCount >= 2, $"Expected a repaired corridor path, got {result.CornerCount} corners.");
                Assert.Equal(0, result.BlockedSegmentIndex);
                Assert.NotEqual(0u, result.BlockingInstanceId);
                Assert.Equal(guid, result.BlockingGuid);
                Assert.Equal(displayId, result.BlockingDisplayId);
                Assert.NotEqual(0u, result.Flags & CorridorResultFlagOverlayRepaired);

                var path = ReadCorridorPath(start, result);
                for (var i = 0; i < path.Length - 1; i++)
                {
                    var segmentHit = SegmentIntersectsDynamicObjectsDetailed(
                        mapId,
                        path[i].X, path[i].Y, path[i].Z,
                        path[i + 1].X, path[i + 1].Y, path[i + 1].Z,
                        out _,
                        out _,
                        out _);

                    Assert.False(segmentHit, $"Repaired corridor segment {i}->{i + 1} still intersects the registered dynamic object.");
                }
            }
            finally
            {
                if (result.Handle != 0)
                    CorridorDestroy(result.Handle);
            }
        }
        finally
        {
            ClearAllDynamicObjects();
        }
    }

    private static NavigationInterop.Vector3[] ReadCorridorPath(
        NavigationInterop.Vector3 start,
        NavigationInterop.CorridorResult result)
    {
        var count = Math.Clamp(result.CornerCount, 0, NavigationInterop.CorridorMaxCorners);
        var path = new NavigationInterop.Vector3[count + 1];
        path[0] = start;
        for (var i = 0; i < count; i++)
        {
            path[i + 1] = new NavigationInterop.Vector3(
                result.Corners[i * 3],
                result.Corners[i * 3 + 1],
                result.Corners[i * 3 + 2]);
        }

        return path;
    }
}
