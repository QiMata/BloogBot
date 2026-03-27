using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorAlternateWorkingVectorModeTests
{
    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.25f)]
    [InlineData(-0.25f)]
    [InlineData(0.64278763f)]
    [InlineData(-0.64278763f)]
    public void EvaluateWoWSelectorContactWithinAlternateWorkingVectorBand_AcceptsNormalsInsideSlopeBand(float normalZ)
    {
        Assert.True(EvaluateWoWSelectorContactWithinAlternateWorkingVectorBand(normalZ));
    }

    [Theory]
    [InlineData(0.6429f)]
    [InlineData(1.0f)]
    [InlineData(-0.6429f)]
    [InlineData(-1.0f)]
    public void EvaluateWoWSelectorContactWithinAlternateWorkingVectorBand_RejectsSteepUpAndDownNormals(float normalZ)
    {
        Assert.False(EvaluateWoWSelectorContactWithinAlternateWorkingVectorBand(normalZ));
    }

    [Fact]
    public void EvaluateWoWSelectorAlternateWorkingVectorMode_UsesTwoCandidateBuilderOnlyForTwoRecordBandCase()
    {
        SelectorAlternateWorkingVectorMode mode = EvaluateWoWSelectorAlternateWorkingVectorMode(
            selectedNormalZ: 0.0f,
            candidateCount: 2u);

        Assert.Equal(SelectorAlternateWorkingVectorMode.TwoCandidateBuilder, mode);
    }

    [Theory]
    [InlineData(3u)]
    [InlineData(4u)]
    public void EvaluateWoWSelectorAlternateWorkingVectorMode_UsesSelectedContactNormalForThreeAndFourCandidates(uint candidateCount)
    {
        SelectorAlternateWorkingVectorMode mode = EvaluateWoWSelectorAlternateWorkingVectorMode(
            selectedNormalZ: 0.1f,
            candidateCount);

        Assert.Equal(SelectorAlternateWorkingVectorMode.SelectedContactNormal, mode);
    }

    [Theory]
    [InlineData(0.0f, 0u)]
    [InlineData(0.0f, 1u)]
    [InlineData(0.0f, 5u)]
    [InlineData(1.0f, 2u)]
    [InlineData(-1.0f, 2u)]
    public void EvaluateWoWSelectorAlternateWorkingVectorMode_FallsBackToNegatedFirstCandidateOutsideSupportedBranch(
        float selectedNormalZ,
        uint candidateCount)
    {
        SelectorAlternateWorkingVectorMode mode = EvaluateWoWSelectorAlternateWorkingVectorMode(
            selectedNormalZ,
            candidateCount);

        Assert.Equal(SelectorAlternateWorkingVectorMode.NegatedFirstCandidate, mode);
    }
}
