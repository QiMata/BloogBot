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

    [Fact]
    public void ValidateWalkableSegment_OrgrimmarShortRampNearCompleteSegment_IsClear()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Short smooth-path segment that previously false-negatived as BlockedGeometry
        // after reaching >98% completion on the corpse-run climb.
        var start = new Vector3(1510.2681f, -4876.3428f, 30.7479f);
        var end = new Vector3(1507.8679f, -4874.8135f, 31.3779f);

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
            $"Near-complete corpse-run segment result={result} resolvedEndZ={resolvedEndZ:F3} supportDelta={supportDelta:F3} travelFraction={travelFraction:F3}");

        Assert.Equal(SegmentValidationResult.Clear, result);
        Assert.True(float.IsFinite(resolvedEndZ));
        Assert.InRange(travelFraction, 0.98f, 1.01f);
    }

    [Fact]
    public void FindPath_OrgrimmarCorpseRun_AllShortSegmentsValidate()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = new Vector3(1543f, -4959f, 9f);
        var end = new Vector3(1680f, -4315f, 62f);
        var path = FindPath(1, start, end, smoothPath: true);

        Assert.NotEmpty(path);
        Assert.True(path.Length >= 3, $"Expected multi-point corpse-run path, got {path.Length}");

        var current = path[0];
        for (var i = 0; i < path.Length - 1; i++)
        {
            var from = current;
            var to = path[i + 1];
            var horizontalDistance = MathF.Sqrt(MathF.Pow(to.X - from.X, 2) + MathF.Pow(to.Y - from.Y, 2));
            if (horizontalDistance > 20f)
            {
                current = to;
                continue;
            }

            var result = ValidateWalkableSegment(
                1,
                from,
                to,
                PhysicsTestConstants.DefaultCapsuleRadius,
                PhysicsTestConstants.DefaultCapsuleHeight,
                out var resolvedEndZ,
                out var supportDelta,
                out var travelFraction);

            _output.WriteLine(
                $"seg {i}->{i + 1} from={from} to={to} result={result} resolvedEndZ={resolvedEndZ:F3} supportDelta={supportDelta:F3} travelFraction={travelFraction:F3}");

            Assert.True(
                result == SegmentValidationResult.Clear || result == SegmentValidationResult.MissingSupport,
                $"Segment {i}->{i + 1} failed validation with {result} from={from} to={to}");

            current = result == SegmentValidationResult.Clear && float.IsFinite(resolvedEndZ)
                ? new Vector3(to.X, to.Y, resolvedEndZ)
                : to;
        }
    }
}
