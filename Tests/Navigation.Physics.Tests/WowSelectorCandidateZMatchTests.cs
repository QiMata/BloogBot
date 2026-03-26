using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorCandidateZMatchTests
{
    private const float BinaryEpsilon = 9.5367431640625e-07f; // 0x8026BC
    private const float NegativeDiagonalZ = -0.4756366014480591f; // 0x80E014

    [Fact]
    public void HasSelectorCandidateWithUnitZ_ReturnsTrueForDefaultCandidate()
    {
        SelectorSupportPlane[] candidates =
        [
            CreateCandidate(0f, 0f, 1f),
            CreateCandidate(0f, 0f, 0.5f),
        ];

        bool matched = HasWoWSelectorCandidateWithUnitZ(candidates, candidates.Length);

        Assert.True(matched);
    }

    [Fact]
    public void HasSelectorCandidateWithNegativeDiagonalZ_ReturnsTrueWithinBinaryEpsilon()
    {
        SelectorSupportPlane[] candidates =
        [
            CreateCandidate(0f, 0f, NegativeDiagonalZ + (BinaryEpsilon * 0.5f)),
            CreateCandidate(0f, 0f, 1f),
        ];

        bool matched = HasWoWSelectorCandidateWithNegativeDiagonalZ(candidates, candidates.Length);

        Assert.True(matched);
    }

    [Fact]
    public void HasSelectorCandidateWithNegativeDiagonalZ_ReturnsFalseOutsideBinaryEpsilon()
    {
        SelectorSupportPlane[] candidates =
        [
            CreateCandidate(0f, 0f, NegativeDiagonalZ + (BinaryEpsilon * 2.0f)),
            CreateCandidate(0f, 0f, 0.25f),
        ];

        bool matched = HasWoWSelectorCandidateWithNegativeDiagonalZ(candidates, candidates.Length);

        Assert.False(matched);
    }

    [Fact]
    public void HasSelectorCandidateWithNegativeDiagonalZ_StopsAtCandidateCount()
    {
        SelectorSupportPlane[] candidates =
        [
            CreateCandidate(0f, 0f, 1f),
            CreateCandidate(0f, 0f, NegativeDiagonalZ),
        ];

        bool matched = HasWoWSelectorCandidateWithNegativeDiagonalZ(candidates, 1);

        Assert.False(matched);
    }

    private static SelectorSupportPlane CreateCandidate(float x, float y, float z) =>
        new()
        {
            Normal = new Vector3(x, y, z),
            PlaneDistance = 0f,
        };
}
