namespace Navigation.Physics.Tests;

using static NavigationInterop;
using Xunit.Abstractions;

[Collection("PhysicsEngine")]
public class SegmentWalkabilityTests
{
    private readonly PhysicsEngineFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SegmentWalkabilityTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void ValidateWalkableSegment_KnownGroundPoint_ReturnsClear()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = new Vector3(1629.359985f, -4373.380377f, 31.255800f);
        var end = start;

        var result = ValidateWalkableSegment(
            0,
            start,
            end,
            PhysicsTestConstants.DefaultCapsuleRadius,
            PhysicsTestConstants.DefaultCapsuleHeight,
            out var resolvedEndZ,
            out var supportDelta,
            out var travelFraction);

        _output.WriteLine(
            $"Ground point result={result} resolvedEndZ={resolvedEndZ:F3} supportDelta={supportDelta:F3} travelFraction={travelFraction:F3}");

        Assert.Equal(SegmentValidationResult.Clear, result);
        Assert.True(float.IsFinite(resolvedEndZ));
        Assert.InRange(MathF.Abs(supportDelta), 0f, 0.5f);
        Assert.InRange(travelFraction, 0.99f, 1.01f);
    }

    [Fact]
    public void ValidateWalkableSegment_ObstructedRoute_IsRejected()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = new Vector3(-8949.95f, -132.49f, 83.53f);
        var end = new Vector3(-8880.00f, -220.00f, 83.53f);

        var result = ValidateWalkableSegment(
            0,
            start,
            end,
            PhysicsTestConstants.DefaultCapsuleRadius,
            PhysicsTestConstants.DefaultCapsuleHeight,
            out var resolvedEndZ,
            out var supportDelta,
            out var travelFraction);

        _output.WriteLine(
            $"Blocked route result={result} resolvedEndZ={resolvedEndZ:F3} supportDelta={supportDelta:F3} travelFraction={travelFraction:F3}");

        Assert.NotEqual(SegmentValidationResult.Clear, result);
        Assert.True(travelFraction < 1.0f, $"Expected early rejection, got travelFraction={travelFraction:F3}");
    }

    [Fact]
    public void ValidateWalkableSegment_OrgrimmarCorpseRunRawSegment_IsClear()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // First false-negative segment from the native graveyard->center raw path probe.
        var start = new Vector3(1546.6f, -4962.4f, 12.0f);
        var end = new Vector3(1550.4f, -4958.4f, 11.4f);

        var result = ValidateWalkableSegment(
            1,
            start,
            end,
            PhysicsTestConstants.DefaultCapsuleRadius,
            PhysicsTestConstants.DefaultCapsuleHeight,
            out var resolvedEndZ,
            out var supportDelta,
            out var travelFraction);

        _output.WriteLine(
            $"Corpse-run segment result={result} resolvedEndZ={resolvedEndZ:F3} supportDelta={supportDelta:F3} travelFraction={travelFraction:F3}");

        Assert.Equal(SegmentValidationResult.Clear, result);
        Assert.True(float.IsFinite(resolvedEndZ));
        Assert.InRange(travelFraction, 0.98f, 1.01f);
    }
}
