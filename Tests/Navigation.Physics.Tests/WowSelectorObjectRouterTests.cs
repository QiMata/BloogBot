using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectRouterTests
{
    [Fact]
    public void EvaluateWoWSelectorObjectRouterEntries_RejectsNonOverlappingEntriesBeforeNodeGate()
    {
        SelectorObjectRouterEntryRecord[] entries =
        [
            new SelectorObjectRouterEntryRecord
            {
                BoundsMin = new Vector3(10.0f, 10.0f, 10.0f),
                BoundsMax = new Vector3(12.0f, 12.0f, 12.0f),
                NodeToken = 0x1000u,
                NodeEnabled = 0u,
                CallbackReturn = 0x01u,
            },
        ];

        uint result = EvaluateWoWSelectorObjectRouterEntries(
            entries,
            entries.Length,
            selectorEnabled: true,
            queryBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            queryBoundsMax: new Vector3(5.0f, 5.0f, 5.0f),
            out SelectorObjectRouterTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(1u, trace.OverlapRejectedCount);
        Assert.Equal(0u, trace.NodeRejectedCount);
        Assert.Equal(0u, trace.DispatchedCount);
        Assert.Equal(0u, trace.AccumulatorUpdatedCount);
        Assert.Equal(0u, trace.Result);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRouterEntries_RejectsOverlappingEntryWhenNodeDoesNotResolve()
    {
        SelectorObjectRouterEntryRecord[] entries =
        [
            new SelectorObjectRouterEntryRecord
            {
                BoundsMin = new Vector3(-1.0f, -1.0f, -1.0f),
                BoundsMax = new Vector3(2.0f, 2.0f, 2.0f),
                NodeToken = 0x2000u,
                NodeEnabled = 0u,
                CallbackReturn = 0x01u,
            },
        ];

        uint result = EvaluateWoWSelectorObjectRouterEntries(
            entries,
            entries.Length,
            selectorEnabled: true,
            queryBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            queryBoundsMax: new Vector3(5.0f, 5.0f, 5.0f),
            out SelectorObjectRouterTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.OverlapRejectedCount);
        Assert.Equal(1u, trace.NodeRejectedCount);
        Assert.Equal(0u, trace.DispatchedCount);
        Assert.Equal(0u, trace.AccumulatorUpdatedCount);
        Assert.Equal(0u, trace.Result);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRouterEntries_OrsEveryCallbackResultWithoutEarlyOut()
    {
        SelectorObjectRouterEntryRecord[] entries =
        [
            new SelectorObjectRouterEntryRecord
            {
                BoundsMin = new Vector3(-2.0f, -2.0f, -2.0f),
                BoundsMax = new Vector3(1.0f, 1.0f, 1.0f),
                NodeToken = 0x3000u,
                NodeEnabled = 1u,
                CallbackReturn = 0x01u,
            },
            new SelectorObjectRouterEntryRecord
            {
                BoundsMin = new Vector3(-3.0f, -3.0f, -3.0f),
                BoundsMax = new Vector3(2.0f, 2.0f, 2.0f),
                NodeToken = 0x3001u,
                NodeEnabled = 1u,
                CallbackReturn = 0x01u,
            },
            new SelectorObjectRouterEntryRecord
            {
                BoundsMin = new Vector3(-4.0f, -4.0f, -4.0f),
                BoundsMax = new Vector3(3.0f, 3.0f, 3.0f),
                NodeToken = 0x3002u,
                NodeEnabled = 1u,
                CallbackReturn = 0x04u,
            },
        ];

        uint result = EvaluateWoWSelectorObjectRouterEntries(
            entries,
            entries.Length,
            selectorEnabled: true,
            queryBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            queryBoundsMax: new Vector3(5.0f, 5.0f, 5.0f),
            out SelectorObjectRouterTrace trace);

        Assert.Equal(0x05u, result);
        Assert.Equal(0u, trace.OverlapRejectedCount);
        Assert.Equal(0u, trace.NodeRejectedCount);
        Assert.Equal(3u, trace.DispatchedCount);
        Assert.Equal(2u, trace.AccumulatorUpdatedCount);
        Assert.Equal(0x05u, trace.Result);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRouterEntries_ZeroEntryCountReturnsZeroTrace()
    {
        uint result = EvaluateWoWSelectorObjectRouterEntries(
            Array.Empty<SelectorObjectRouterEntryRecord>(),
            0,
            selectorEnabled: true,
            queryBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            queryBoundsMax: new Vector3(5.0f, 5.0f, 5.0f),
            out SelectorObjectRouterTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.OverlapRejectedCount);
        Assert.Equal(0u, trace.NodeRejectedCount);
        Assert.Equal(0u, trace.DispatchedCount);
        Assert.Equal(0u, trace.AccumulatorUpdatedCount);
        Assert.Equal(0u, trace.Result);
    }
}
