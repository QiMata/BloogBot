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
    public void ValidateWalkableSegment_SteepSweepContainsRejectedUphillSegment()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var candidates = new[]
        {
            PhysicsSweepCoordinates.VerticalSurfaces.UngoroCraterWalls,
            PhysicsSweepCoordinates.VerticalSurfaces.DesolaceCliffs,
            PhysicsSweepCoordinates.SteepSlopes.ThousandNeedlesMesas,
        };

        foreach (var candidate in candidates)
        {
            if (TryFindRejectedUphillSegment(candidate, out var start, out var end, out var result,
                out var resolvedEndZ, out var supportDelta, out var travelFraction))
            {
                _output.WriteLine(
                    $"Rejected uphill segment found in {candidate.Description}: start={start} end={end} result={result} resolvedEndZ={resolvedEndZ:F3} supportDelta={supportDelta:F3} travelFraction={travelFraction:F3}");

                Assert.True(result == SegmentValidationResult.StepUpTooHigh || result == SegmentValidationResult.BlockedGeometry,
                    $"Expected steep uphill rejection, got {result}");
                Assert.InRange(travelFraction, 0.0f, 0.95f);
                Assert.True(resolvedEndZ < end.Z - 3.0f,
                    $"Expected the resolved support to stay materially below the blocked top point, got resolvedEndZ={resolvedEndZ:F3}, endZ={end.Z:F3}");
                return;
            }
        }

        Assert.Fail("Expected to find at least one rejected uphill segment in the steep-slope sweep corpus.");
    }

    [Fact]
    public void FindPath_OrgrimmarCorpseRun_PathExistsAndReachesDestination()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = new Vector3(1543f, -4959f, 9f);
        var end = new Vector3(1680f, -4315f, 62f);
        var path = FindPath(1, start, end, smoothPath: true);

        Assert.NotEmpty(path);
        Assert.True(path.Length >= 3, $"Expected multi-point corpse-run path, got {path.Length}");

        var terminalDistance = Distance2D(path[^1], end);
        Assert.InRange(terminalDistance, 0.0f, 12.0f);
    }

    [Fact]
    public void FindPath_RatchetFishingApproach_ReformsBlockedSegment()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = new Vector3(-957.0f, -3755.0f, 5.0f);
        var end = new Vector3(-956.2f, -3775.0f, 0.0f);

        var directResult = ValidateWalkableSegment(
            1,
            start,
            end,
            PhysicsTestConstants.DefaultCapsuleRadius,
            PhysicsTestConstants.DefaultCapsuleHeight,
            out var directResolvedEndZ,
            out var directSupportDelta,
            out var directTravelFraction);

        _output.WriteLine(
            $"Ratchet direct result={directResult} resolvedEndZ={directResolvedEndZ:F3} supportDelta={directSupportDelta:F3} travelFraction={directTravelFraction:F3}");

        Assert.NotEqual(SegmentValidationResult.Clear, directResult);

        var path = FindPath(1, start, end, smoothPath: true);

        for (var i = 0; i < path.Length; i++)
            _output.WriteLine($"ratchet pt[{i}]={path[i]}");

        Assert.NotEmpty(path);
        Assert.True(path.Length >= 3, $"Expected shoreline detour path, got {path.Length} points");

        var terminalDistance = Distance2D(path[^1], end);
        Assert.InRange(terminalDistance, 0.0f, 12.0f);
        AssertRouteShortSegmentsValidate(1, path, "Ratchet fishing approach");
    }

    [Fact]
    public void FindPath_ObstructedDirectSegment_ReformsIntoWalkableDetour()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        var start = new Vector3(-8949.95f, -132.49f, 83.53f);
        var end = new Vector3(-8880.00f, -220.00f, 83.53f);

        var directResult = ValidateWalkableSegment(
            0,
            start,
            end,
            PhysicsTestConstants.DefaultCapsuleRadius,
            PhysicsTestConstants.DefaultCapsuleHeight,
            out var directResolvedEndZ,
            out var directSupportDelta,
            out var directTravelFraction);

        _output.WriteLine(
            $"Obstructed direct result={directResult} resolvedEndZ={directResolvedEndZ:F3} supportDelta={directSupportDelta:F3} travelFraction={directTravelFraction:F3}");

        Assert.NotEqual(SegmentValidationResult.Clear, directResult);

        var path = FindPath(0, start, end, smoothPath: true);

        for (var i = 0; i < path.Length; i++)
            _output.WriteLine($"obstructed pt[{i}]={path[i]}");

        Assert.NotEmpty(path);
        Assert.True(path.Length >= 3, $"Expected detour path around obstruction, got {path.Length} points");
        AssertRouteShortSegmentsValidate(0, path, "Obstructed detour");
    }

    private void AssertRouteShortSegmentsValidate(uint mapId, Vector3[] path, string routeLabel)
    {
        Assert.NotEmpty(path);

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
                $"{routeLabel} seg {i}->{i + 1} from={from} to={to} result={result} resolvedEndZ={resolvedEndZ:F3} supportDelta={supportDelta:F3} travelFraction={travelFraction:F3}");

            Assert.True(
                result == SegmentValidationResult.Clear || result == SegmentValidationResult.MissingSupport,
                $"{routeLabel} segment {i}->{i + 1} failed validation with {result} from={from} to={to}");

            current = result == SegmentValidationResult.Clear && float.IsFinite(resolvedEndZ)
                ? new Vector3(to.X, to.Y, resolvedEndZ)
                : to;
        }
    }

    private bool TryFindRejectedUphillSegment(
        SweepLocation location,
        out Vector3 start,
        out Vector3 end,
        out SegmentValidationResult result,
        out float resolvedEndZ,
        out float supportDelta,
        out float travelFraction)
    {
        static bool IsValidGround(float z) => z > -100000f && z < 100000f;

        for (var offsetX = -location.SweepRadius; offsetX <= location.SweepRadius; offsetX += 2.0f)
        {
            for (var offsetY = -location.SweepRadius; offsetY <= location.SweepRadius; offsetY += 2.0f)
            {
                var sampleX = location.CenterX + offsetX;
                var sampleY = location.CenterY + offsetY;

                var startZ = GetGroundZ(location.MapId, sampleX, sampleY, location.CenterZ + 10.0f, 40.0f);
                if (!IsValidGround(startZ))
                    startZ = GetGroundZ(location.MapId, sampleX, sampleY, location.CenterZ + 30.0f, 80.0f);
                if (!IsValidGround(startZ))
                    continue;

                for (var step = 1.0f; step <= 4.0f; step += 1.0f)
                {
                    for (var dirIndex = 0; dirIndex < 32; dirIndex++)
                    {
                        var angle = dirIndex * (Math.PI / 16.0);
                        var endX = sampleX + (float)(Math.Cos(angle) * step);
                        var endY = sampleY + (float)(Math.Sin(angle) * step);
                        var endZ = GetGroundZ(location.MapId, endX, endY, startZ + 80.0f, 120.0f);
                        if (!IsValidGround(endZ) || endZ < startZ + 5.0f)
                            continue;

                        start = new Vector3(sampleX, sampleY, startZ);
                        end = new Vector3(endX, endY, endZ);
                        result = ValidateWalkableSegment(
                            location.MapId,
                            start,
                            end,
                            PhysicsTestConstants.DefaultCapsuleRadius,
                            PhysicsTestConstants.DefaultCapsuleHeight,
                            out resolvedEndZ,
                            out supportDelta,
                            out travelFraction);

                        if (result == SegmentValidationResult.StepUpTooHigh || result == SegmentValidationResult.BlockedGeometry)
                            return true;
                    }
                }
            }
        }

        start = default;
        end = default;
        result = SegmentValidationResult.Clear;
        resolvedEndZ = 0.0f;
        supportDelta = 0.0f;
        travelFraction = 0.0f;
        return false;
    }

    private static float Distance2D(in Vector3 from, in Vector3 to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
