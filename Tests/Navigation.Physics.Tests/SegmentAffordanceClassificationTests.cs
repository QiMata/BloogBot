namespace Navigation.Physics.Tests;

using static NavigationInterop;
using Xunit.Abstractions;

[Collection("PhysicsEngine")]
public sealed class SegmentAffordanceClassificationTests
{
    private readonly PhysicsEngineFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SegmentAffordanceClassificationTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void ClassifyPathSegmentAffordance_KnownGroundSegment_ReturnsWalk()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = new Vector3(1546.6f, -4962.4f, 12.0f);
        var end = new Vector3(1550.4f, -4958.4f, 11.4f);

        var affordance = ClassifyPathSegmentAffordance(
            1,
            start,
            end,
            PhysicsTestConstants.DefaultCapsuleRadius,
            PhysicsTestConstants.DefaultCapsuleHeight,
            out var climbHeight,
            out var gapDistance,
            out var dropHeight,
            out var slopeAngleDeg,
            out var resolvedEndZ,
            out var validationCode);

        _output.WriteLine(
            $"Walk classification affordance={affordance} validation={validationCode} climb={climbHeight:F3} gap={gapDistance:F3} drop={dropHeight:F3} slope={slopeAngleDeg:F3} resolvedEndZ={resolvedEndZ:F3}");

        Assert.Equal(SegmentValidationResult.Clear, validationCode);
        Assert.Equal(SegmentAffordanceResult.Walk, affordance);
        Assert.InRange(climbHeight, 0f, 0.5f);
        Assert.Equal(0f, gapDistance, precision: 3);
        Assert.InRange(dropHeight, 0f, 1f);
        Assert.True(float.IsFinite(resolvedEndZ));
    }

    [Fact]
    public void ClassifyPathSegmentAffordance_ObstructedRoute_ReturnsBlocked()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = new Vector3(-8949.95f, -132.49f, 83.53f);
        var end = new Vector3(-8880.00f, -220.00f, 83.53f);

        var affordance = ClassifyPathSegmentAffordance(
            0,
            start,
            end,
            PhysicsTestConstants.DefaultCapsuleRadius,
            PhysicsTestConstants.DefaultCapsuleHeight,
            out var climbHeight,
            out var gapDistance,
            out var dropHeight,
            out var slopeAngleDeg,
            out var resolvedEndZ,
            out var validationCode);

        _output.WriteLine(
            $"Blocked classification affordance={affordance} validation={validationCode} climb={climbHeight:F3} gap={gapDistance:F3} drop={dropHeight:F3} slope={slopeAngleDeg:F3} resolvedEndZ={resolvedEndZ:F3}");

        Assert.NotEqual(SegmentValidationResult.Clear, validationCode);
        Assert.Equal(SegmentAffordanceResult.Blocked, affordance);
        Assert.True(float.IsFinite(resolvedEndZ));
    }
}
