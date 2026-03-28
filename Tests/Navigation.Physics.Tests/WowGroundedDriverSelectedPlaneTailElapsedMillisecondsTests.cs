using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailElapsedMillisecondsTests
{
    [Theory]
    [InlineData(0.0f, 0, 1u, 0u)]
    [InlineData(0.0014f, 1, 1u, 0u)]
    [InlineData(0.0015f, 2, 1u, 0u)]
    [InlineData(-0.0014f, -1, 0u, 1u)]
    [InlineData(-0.0015f, -2, 0u, 1u)]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailElapsedMilliseconds_UsesHalfAwayFromZeroQuantization(
        float elapsedSeconds,
        int expectedMilliseconds,
        uint expectedPositiveBias,
        uint expectedNegativeBias)
    {
        int roundedMilliseconds = EvaluateWoWGroundedDriverSelectedPlaneTailElapsedMilliseconds(
            elapsedSeconds,
            out GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace trace);

        Assert.Equal(expectedMilliseconds, roundedMilliseconds);
        Assert.Equal(expectedMilliseconds, trace.RoundedMilliseconds);
        Assert.Equal(expectedPositiveBias, trace.AddedPositiveHalfBias);
        Assert.Equal(expectedNegativeBias, trace.AddedNegativeHalfBias);
        Assert.Equal(elapsedSeconds, trace.InputElapsedSeconds, 6);
    }
}
