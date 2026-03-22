using Pathfinding;
using PathfindingService.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PathfindingService.Tests;

public class RequestScopedDynamicObjectOverlayTests
{
    [Fact]
    public void ExecuteWithOverlay_RegistersUpdatesAndUnregistersValidObjects()
    {
        var registry = new FakeDynamicObjectOverlayRegistry();
        var overlay = new RequestScopedDynamicObjectOverlay(registry);
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
                Scale = 1.5f,
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
                Scale = 0f,
                GoState = 0,
            }
        };

        var result = overlay.ExecuteWithOverlay(1, nearbyObjects, () => "ok");

        Assert.Equal("ok", result.Value);
        Assert.Equal(2, result.Summary.RequestedCount);
        Assert.Equal(2, result.Summary.RegisteredCount);
        Assert.Equal(0, result.Summary.FilteredCount);
        Assert.Equal([17u, 42u], result.Summary.RegisteredDisplayIds);

        Assert.Equal(2, registry.Registered.Count);
        Assert.Equal(2, registry.Updated.Count);
        Assert.Equal(2, registry.Unregistered.Count);
        Assert.Equal(registry.Registered.Select(x => x.Guid), registry.Unregistered);
        Assert.All(registry.Registered, entry => Assert.Equal(1u, entry.MapId));
        Assert.Equal(1.5f, registry.Registered[0].Scale);
        Assert.Equal(1f, registry.Registered[1].Scale);
        Assert.Equal(1u, registry.Updated[0].GoState);
        Assert.Equal(0u, registry.Updated[1].GoState);
    }

    [Fact]
    public void ExecuteWithOverlay_FiltersInvalidObjects_AndStillUnregistersOnFailure()
    {
        var registry = new FakeDynamicObjectOverlayRegistry();
        var overlay = new RequestScopedDynamicObjectOverlay(registry);
        var nearbyObjects = new[]
        {
            new DynamicObjectProto
            {
                Guid = 0x2001,
                DisplayId = 0,
                X = 1f,
                Y = 2f,
                Z = 3f,
                Orientation = 0.5f,
                Scale = 1f,
                GoState = 0,
            },
            new DynamicObjectProto
            {
                Guid = 0x2002,
                DisplayId = 55,
                X = float.NaN,
                Y = 2f,
                Z = 3f,
                Orientation = 0.5f,
                Scale = 1f,
                GoState = 0,
            },
            new DynamicObjectProto
            {
                Guid = 0x2003,
                DisplayId = 66,
                X = 7f,
                Y = 8f,
                Z = 9f,
                Orientation = 0.25f,
                Scale = 1f,
                GoState = 1,
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            overlay.ExecuteWithOverlay<object>(1, nearbyObjects, () => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", ex.Message);
        Assert.Single(registry.Registered);
        Assert.Single(registry.Updated);
        Assert.Single(registry.Unregistered);
        Assert.Equal(registry.Registered[0].Guid, registry.Unregistered[0]);
        Assert.Equal(66u, registry.Registered[0].DisplayId);
        Assert.Equal(1u, registry.Updated[0].GoState);
    }

    [Fact]
    public async Task ExecuteExclusive_WaitsUntilOverlayLifecycleCompletes()
    {
        var registry = new FakeDynamicObjectOverlayRegistry();
        var overlay = new RequestScopedDynamicObjectOverlay(registry);
        var nearbyObjects = new[]
        {
            new DynamicObjectProto
            {
                Guid = 0x3001,
                DisplayId = 77,
                X = 11f,
                Y = 12f,
                Z = 13f,
                Orientation = 0.75f,
                Scale = 1f,
                GoState = 0,
            }
        };

        using var overlayEntered = new ManualResetEventSlim(false);
        using var releaseOverlay = new ManualResetEventSlim(false);
        using var exclusiveEntered = new ManualResetEventSlim(false);

        var overlayTask = Task.Run(() =>
            overlay.ExecuteWithOverlay(
                1,
                nearbyObjects,
                () =>
                {
                    overlayEntered.Set();
                    releaseOverlay.Wait(TimeSpan.FromSeconds(5));
                    return "done";
                }));

        Assert.True(overlayEntered.Wait(TimeSpan.FromSeconds(5)));

        var exclusiveTask = Task.Run(() =>
            overlay.ExecuteExclusive(() =>
            {
                exclusiveEntered.Set();
                return 123;
            }));

        Assert.False(exclusiveEntered.Wait(TimeSpan.FromMilliseconds(200)));

        releaseOverlay.Set();

        var overlayResult = await overlayTask;
        var exclusiveResult = await exclusiveTask;

        Assert.Equal("done", overlayResult.Value);
        Assert.Equal(123, exclusiveResult);
        Assert.True(exclusiveEntered.IsSet);
        Assert.Single(registry.Unregistered);
    }

    [Fact]
    public async Task ExecuteWithOverlay_WithoutObjects_RunsConcurrentlyWithSharedCalls()
    {
        // Empty overlay uses read lock — should NOT block shared (read) operations.
        // This is critical: path requests without dynamic objects must not starve physics.
        var registry = new FakeDynamicObjectOverlayRegistry();
        var overlay = new RequestScopedDynamicObjectOverlay(registry);

        using var emptyOverlayEntered = new ManualResetEventSlim(false);
        using var releaseEmptyOverlay = new ManualResetEventSlim(false);
        using var sharedEntered = new ManualResetEventSlim(false);

        var emptyOverlayTask = Task.Run(() =>
            overlay.ExecuteWithOverlay(
                1,
                Array.Empty<DynamicObjectProto>(),
                () =>
                {
                    emptyOverlayEntered.Set();
                    releaseEmptyOverlay.Wait(TimeSpan.FromSeconds(5));
                    return "empty";
                }));

        Assert.True(emptyOverlayEntered.Wait(TimeSpan.FromSeconds(5)));

        var sharedTask = Task.Run(() =>
            overlay.ExecuteExclusive(() =>
            {
                sharedEntered.Set();
                return 456;
            }));

        // Shared call should proceed concurrently (both are read locks)
        Assert.True(sharedEntered.Wait(TimeSpan.FromSeconds(5)));

        releaseEmptyOverlay.Set();

        var emptyOverlayResult = await emptyOverlayTask;
        var sharedResult = await sharedTask;

        Assert.Equal("empty", emptyOverlayResult.Value);
        Assert.Equal(0, emptyOverlayResult.Summary.RequestedCount);
        Assert.Equal(456, sharedResult);
        Assert.Empty(registry.Registered);
        Assert.Empty(registry.Unregistered);
    }

    [Fact]
    public async Task ExecuteWithOverlay_WithObjects_BlocksSharedCalls()
    {
        // Overlay WITH dynamic objects uses write lock — MUST block shared (read) operations.
        // This ensures registry mutations are exclusive.
        var registry = new FakeDynamicObjectOverlayRegistry();
        var overlay = new RequestScopedDynamicObjectOverlay(registry);
        var nearbyObjects = new[]
        {
            new DynamicObjectProto
            {
                Guid = 0x5001,
                DisplayId = 99,
                X = 1f,
                Y = 2f,
                Z = 3f,
                Orientation = 0.5f,
                Scale = 1f,
                GoState = 0,
            }
        };

        using var overlayEntered = new ManualResetEventSlim(false);
        using var releaseOverlay = new ManualResetEventSlim(false);
        using var sharedEntered = new ManualResetEventSlim(false);

        var overlayTask = Task.Run(() =>
            overlay.ExecuteWithOverlay(
                1,
                nearbyObjects,
                () =>
                {
                    overlayEntered.Set();
                    releaseOverlay.Wait(TimeSpan.FromSeconds(5));
                    return "path";
                }));

        Assert.True(overlayEntered.Wait(TimeSpan.FromSeconds(5)));

        var sharedTask = Task.Run(() =>
            overlay.ExecuteExclusive(() =>
            {
                sharedEntered.Set();
                return 2;
            }));

        // Shared call should be blocked by write lock
        Assert.False(sharedEntered.Wait(TimeSpan.FromMilliseconds(200)));

        releaseOverlay.Set();

        var overlayResult = await overlayTask;
        var sharedResult = await sharedTask;

        Assert.Equal("path", overlayResult.Value);
        Assert.Equal(2, sharedResult);
        Assert.True(sharedEntered.IsSet);
    }

    [Fact]
    public async Task MultipleSharedCalls_RunConcurrently()
    {
        // Multiple physics/LOS calls must run concurrently — this is the fix for
        // lock starvation where 9 bots' path requests starved physics.
        var registry = new FakeDynamicObjectOverlayRegistry();
        var overlay = new RequestScopedDynamicObjectOverlay(registry);

        using var shared1Entered = new ManualResetEventSlim(false);
        using var shared2Entered = new ManualResetEventSlim(false);
        using var releaseAll = new ManualResetEventSlim(false);

        var task1 = Task.Run(() =>
            overlay.ExecuteExclusive(() =>
            {
                shared1Entered.Set();
                releaseAll.Wait(TimeSpan.FromSeconds(5));
                return 1;
            }));

        var task2 = Task.Run(() =>
            overlay.ExecuteExclusive(() =>
            {
                shared2Entered.Set();
                releaseAll.Wait(TimeSpan.FromSeconds(5));
                return 2;
            }));

        // Both should enter concurrently (both are read locks)
        Assert.True(shared1Entered.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(shared2Entered.Wait(TimeSpan.FromSeconds(5)));

        releaseAll.Set();

        Assert.Equal(1, await task1);
        Assert.Equal(2, await task2);
    }

    private sealed class FakeDynamicObjectOverlayRegistry : IDynamicObjectOverlayRegistry
    {
        public List<RegisterCall> Registered { get; } = [];
        public List<UpdateCall> Updated { get; } = [];
        public List<ulong> Unregistered { get; } = [];

        public bool RegisterObject(ulong guid, uint entry, uint displayId, uint mapId, float scale)
        {
            Registered.Add(new RegisterCall(guid, entry, displayId, mapId, scale));
            return true;
        }

        public void UpdatePosition(ulong guid, float x, float y, float z, float orientation, uint goState)
            => Updated.Add(new UpdateCall(guid, x, y, z, orientation, goState));

        public void UnregisterObject(ulong guid)
            => Unregistered.Add(guid);
    }

    private readonly record struct RegisterCall(ulong Guid, uint Entry, uint DisplayId, uint MapId, float Scale);
    private readonly record struct UpdateCall(ulong Guid, float X, float Y, float Z, float Orientation, uint GoState);
}
