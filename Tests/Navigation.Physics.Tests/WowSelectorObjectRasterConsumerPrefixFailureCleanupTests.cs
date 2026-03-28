using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectRasterConsumerPrefixFailureCleanupTests
{
    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrefixFailureCleanupExit_ModeGateRejectReturnsWithoutCleanup()
    {
        SelectorObjectRasterPrefixTrace prefix = default;

        uint result = EvaluateWoWSelectorObjectRasterPrefixFailureCleanupExit(
            prefixAccepted: 0u,
            prefix,
            out SelectorObjectRasterPrefixFailureCleanupTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.PrefixAccepted);
        Assert.Equal(0u, trace.ModeGateAccepted);
        Assert.Equal(0u, trace.FailureCleanupExecuted);
        Assert.Equal(0u, trace.FailureCleanupDestroyedPayloadBlocks);
        Assert.Equal(1u, trace.ReturnedBeforePrepass);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrefixFailureCleanupExit_PrefixRejectAfterModeGateRunsVisibleCleanup()
    {
        SelectorObjectRasterPrefixTrace prefix = new()
        {
            ModeGateAccepted = 1u,
        };

        uint result = EvaluateWoWSelectorObjectRasterPrefixFailureCleanupExit(
            prefixAccepted: 0u,
            prefix,
            out SelectorObjectRasterPrefixFailureCleanupTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, trace.PrefixAccepted);
        Assert.Equal(1u, trace.ModeGateAccepted);
        Assert.Equal(1u, trace.FailureCleanupExecuted);
        Assert.Equal(6u, trace.FailureCleanupDestroyedPayloadBlocks);
        Assert.Equal(1u, trace.ReturnedBeforePrepass);
    }

    [Fact]
    public void EvaluateWoWSelectorObjectRasterPrefixFailureCleanupExit_AcceptedPrefixContinuesWithoutCleanup()
    {
        SelectorObjectRasterPrefixTrace prefix = new()
        {
            ModeGateAccepted = 1u,
            QuantizedWindowAccepted = 1u,
        };

        uint result = EvaluateWoWSelectorObjectRasterPrefixFailureCleanupExit(
            prefixAccepted: 1u,
            prefix,
            out SelectorObjectRasterPrefixFailureCleanupTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(1u, trace.PrefixAccepted);
        Assert.Equal(1u, trace.ModeGateAccepted);
        Assert.Equal(0u, trace.FailureCleanupExecuted);
        Assert.Equal(0u, trace.FailureCleanupDestroyedPayloadBlocks);
        Assert.Equal(0u, trace.ReturnedBeforePrepass);
    }
}
