using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailReturn2LateBranchTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailReturn2LateBranch_SubtractsRoundedConsumedWindowFromBothCounters()
    {
        int roundedMilliseconds = EvaluateWoWGroundedDriverSelectedPlaneTailReturn2LateBranch(
            consumedWindowSeconds: 0.0015f,
            field58: 120,
            field78: 240,
            out GroundedDriverSelectedPlaneTailReturn2LateBranchTrace trace);

        Assert.Equal(2, roundedMilliseconds);
        Assert.Equal(2, trace.ElapsedMillisecondsTrace.RoundedMilliseconds);
        Assert.Equal(1u, trace.InvokedConsumedWindowCommit);
        Assert.Equal(120, trace.InputField58);
        Assert.Equal(118, trace.OutputField58);
        Assert.Equal(240, trace.InputField78);
        Assert.Equal(238, trace.OutputField78);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailReturn2LateBranch_NegativeConsumedWindowAddsBackIntoCounters()
    {
        int roundedMilliseconds = EvaluateWoWGroundedDriverSelectedPlaneTailReturn2LateBranch(
            consumedWindowSeconds: -0.0015f,
            field58: 50,
            field78: 75,
            out GroundedDriverSelectedPlaneTailReturn2LateBranchTrace trace);

        Assert.Equal(-2, roundedMilliseconds);
        Assert.Equal(-2, trace.ElapsedMillisecondsTrace.RoundedMilliseconds);
        Assert.Equal(52, trace.OutputField58);
        Assert.Equal(77, trace.OutputField78);
    }
}
