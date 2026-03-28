using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorBvhRecursionStepTests
{
    [Fact]
    public void EvaluateWoWSelectorBvhRecursionStep_StraddlingTraversalAppliesLowChildBeforeHighChild()
    {
        SelectorBvhChildTraversal traversal = new()
        {
            VisitLow = 1u,
            VisitHigh = 1u,
        };

        SelectorBvhRecursionChildOutcome lowChild = new()
        {
            Result = 0x02u,
            PendingCountDelta = 3u,
            AcceptedCountDelta = 2u,
            OverflowFlagsDelta = 0x04u,
        };

        SelectorBvhRecursionChildOutcome highChild = new()
        {
            Result = 0x08u,
            PendingCountDelta = 5u,
            AcceptedCountDelta = 1u,
            OverflowFlagsDelta = 0x80u,
        };

        uint result = EvaluateWoWSelectorBvhRecursionStep(
            traversal,
            lowChild,
            highChild,
            inputOverflowFlags: 0x20u,
            inputPendingCount: 4u,
            inputAcceptedCount: 7u,
            inputResult: 0x10u,
            out SelectorBvhRecursionStepTrace trace);

        Assert.Equal(0x1Au, result);
        Assert.Equal(1u, trace.VisitLow);
        Assert.Equal(1u, trace.VisitHigh);
        Assert.Equal(1u, trace.EnteredLowChild);
        Assert.Equal(1u, trace.EnteredHighChild);
        Assert.Equal(0x10u, trace.ResultBefore);
        Assert.Equal(0x12u, trace.ResultAfterLow);
        Assert.Equal(0x1Au, trace.ResultAfterHigh);
        Assert.Equal(4u, trace.PendingCountBefore);
        Assert.Equal(7u, trace.PendingCountAfterLow);
        Assert.Equal(12u, trace.PendingCountAfterHigh);
        Assert.Equal(7u, trace.AcceptedCountBefore);
        Assert.Equal(9u, trace.AcceptedCountAfterLow);
        Assert.Equal(10u, trace.AcceptedCountAfterHigh);
        Assert.Equal(0x20u, trace.OverflowFlagsBefore);
        Assert.Equal(0x24u, trace.OverflowFlagsAfterLow);
        Assert.Equal(0xA4u, trace.OverflowFlagsAfterHigh);
    }

    [Fact]
    public void EvaluateWoWSelectorBvhRecursionStep_LowOnlyTraversalSkipsHighChild()
    {
        SelectorBvhChildTraversal traversal = new()
        {
            VisitLow = 1u,
            VisitHigh = 0u,
        };

        SelectorBvhRecursionChildOutcome lowChild = new()
        {
            Result = 0x40u,
            PendingCountDelta = 1u,
            AcceptedCountDelta = 0u,
            OverflowFlagsDelta = 0x01u,
        };

        SelectorBvhRecursionChildOutcome highChild = new()
        {
            Result = 0x80u,
            PendingCountDelta = 9u,
            AcceptedCountDelta = 4u,
            OverflowFlagsDelta = 0x10u,
        };

        uint result = EvaluateWoWSelectorBvhRecursionStep(
            traversal,
            lowChild,
            highChild,
            inputOverflowFlags: 0u,
            inputPendingCount: 2u,
            inputAcceptedCount: 6u,
            inputResult: 0x04u,
            out SelectorBvhRecursionStepTrace trace);

        Assert.Equal(0x44u, result);
        Assert.Equal(1u, trace.VisitLow);
        Assert.Equal(0u, trace.VisitHigh);
        Assert.Equal(1u, trace.EnteredLowChild);
        Assert.Equal(0u, trace.EnteredHighChild);
        Assert.Equal(0x04u, trace.ResultBefore);
        Assert.Equal(0x44u, trace.ResultAfterLow);
        Assert.Equal(0x44u, trace.ResultAfterHigh);
        Assert.Equal(2u, trace.PendingCountBefore);
        Assert.Equal(3u, trace.PendingCountAfterLow);
        Assert.Equal(3u, trace.PendingCountAfterHigh);
        Assert.Equal(6u, trace.AcceptedCountBefore);
        Assert.Equal(6u, trace.AcceptedCountAfterLow);
        Assert.Equal(6u, trace.AcceptedCountAfterHigh);
        Assert.Equal(0u, trace.OverflowFlagsBefore);
        Assert.Equal(0x01u, trace.OverflowFlagsAfterLow);
        Assert.Equal(0x01u, trace.OverflowFlagsAfterHigh);
    }

    [Fact]
    public void EvaluateWoWSelectorBvhRecursionStep_HighOnlyTraversalSkipsLowChild()
    {
        SelectorBvhChildTraversal traversal = new()
        {
            VisitLow = 0u,
            VisitHigh = 1u,
        };

        SelectorBvhRecursionChildOutcome lowChild = new()
        {
            Result = 0x20u,
            PendingCountDelta = 7u,
            AcceptedCountDelta = 8u,
            OverflowFlagsDelta = 0x02u,
        };

        SelectorBvhRecursionChildOutcome highChild = new()
        {
            Result = 0x01u,
            PendingCountDelta = 4u,
            AcceptedCountDelta = 3u,
            OverflowFlagsDelta = 0x08u,
        };

        uint result = EvaluateWoWSelectorBvhRecursionStep(
            traversal,
            lowChild,
            highChild,
            inputOverflowFlags: 0x10u,
            inputPendingCount: 11u,
            inputAcceptedCount: 5u,
            inputResult: 0x80u,
            out SelectorBvhRecursionStepTrace trace);

        Assert.Equal(0x81u, result);
        Assert.Equal(0u, trace.VisitLow);
        Assert.Equal(1u, trace.VisitHigh);
        Assert.Equal(0u, trace.EnteredLowChild);
        Assert.Equal(1u, trace.EnteredHighChild);
        Assert.Equal(0x80u, trace.ResultBefore);
        Assert.Equal(0x80u, trace.ResultAfterLow);
        Assert.Equal(0x81u, trace.ResultAfterHigh);
        Assert.Equal(11u, trace.PendingCountBefore);
        Assert.Equal(11u, trace.PendingCountAfterLow);
        Assert.Equal(15u, trace.PendingCountAfterHigh);
        Assert.Equal(5u, trace.AcceptedCountBefore);
        Assert.Equal(5u, trace.AcceptedCountAfterLow);
        Assert.Equal(8u, trace.AcceptedCountAfterHigh);
        Assert.Equal(0x10u, trace.OverflowFlagsBefore);
        Assert.Equal(0x10u, trace.OverflowFlagsAfterLow);
        Assert.Equal(0x18u, trace.OverflowFlagsAfterHigh);
    }
}
