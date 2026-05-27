using GameData.Core.Models;
using PathfindingService.Repository;
using Xunit;

namespace PathfindingService.Tests;

[Collection(NavigationCollection.Name)]
public class DeckLipRawPathContractTests(NavigationFixture fixture)
{
    private const uint Kalimdor = 1;
    private const float TaurenMaleRadius = 1.0247f;
    private const float TaurenMaleHeight = 2.625f;

    private static readonly XYZ DeckLipGruntBaseStart = new(1332.76f, -4633.40f, 24.0783f);
    private static readonly XYZ DeckLipLastGoodStart = new(1355.6f, -4522.3f, 33.1f);
    private static readonly XYZ OrgrimmarZeppelinMasterFrezzaSpawn = new(1331.11f, -4649.45f, 53.6269f);
    private static readonly XYZ OrgrimmarUndercityZeppelinBoardingPoint = new(1320.142944f, -4653.158691f, 53.891945f);

    private readonly Navigation _navigation = fixture.Navigation;

    [Fact]
    public void CalculateRawPath_DeckLipGruntBaseToLiteralFrezza_EndsNearRequestedTargetDespiteInteriorProjectionGap()
    {
        var result = _navigation.CalculateRawPath(
            Kalimdor,
            DeckLipGruntBaseStart,
            OrgrimmarZeppelinMasterFrezzaSpawn,
            smoothPath: true,
            agentRadius: TaurenMaleRadius,
            agentHeight: TaurenMaleHeight);

        var finalWaypoint = result.Path[^1];
        var finalDistance2D = Distance2D(finalWaypoint, OrgrimmarZeppelinMasterFrezzaSpawn);
        var finalDeltaZ = MathF.Abs(finalWaypoint.Z - OrgrimmarZeppelinMasterFrezzaSpawn.Z);

        Console.WriteLine(
            $"Literal Frezza path: result={result.Result} len={result.Path.Length} blockedSeg={(result.BlockedSegmentIndex?.ToString() ?? "null")} blockedReason={result.BlockedReason} final=({finalWaypoint.X:F2},{finalWaypoint.Y:F2},{finalWaypoint.Z:F2}) dist2D={finalDistance2D:F2} dz={finalDeltaZ:F2}");

        Assert.Equal("raw_detour", result.Result);
        Assert.NotNull(result.BlockedSegmentIndex);
        Assert.InRange(result.BlockedSegmentIndex!.Value, 90, 110);
        Assert.StartsWith("interior_projection:", result.BlockedReason, System.StringComparison.Ordinal);
        Assert.True(result.Path.Length >= 120, $"Expected the promoted Grunt-base -> Frezza path to preserve the long in-tower climb; got {result.Path.Length} corners.");
        Assert.True(finalDistance2D <= 4.0f, $"Expected the literal Frezza path to finish near the NPC spawn; final 2D distance was {finalDistance2D:F2}y.");
        Assert.True(finalDeltaZ <= 1.0f, $"Expected the literal Frezza path to finish on the upper-deck Z layer; final |dz| was {finalDeltaZ:F2}y.");
        Assert.Equal(result.Path, result.RawPath);
    }

    [Fact]
    public void CalculateRawPath_DeckLipGruntBaseToBoardingCorridor_SurfacesInteriorProjectionBlockedReason()
    {
        var result = _navigation.CalculateRawPath(
            Kalimdor,
            DeckLipGruntBaseStart,
            OrgrimmarUndercityZeppelinBoardingPoint,
            smoothPath: true,
            agentRadius: TaurenMaleRadius,
            agentHeight: TaurenMaleHeight);

        Console.WriteLine(
            $"PathResult: result={result.Result} len={result.Path.Length} blockedSeg={(result.BlockedSegmentIndex?.ToString() ?? "null")} blockedReason={result.BlockedReason}");

        Assert.Equal("raw_detour", result.Result);
        Assert.True(result.Path.Length >= 32, $"Expected the promoted Grunt-base smooth path to keep diagnostic corners; got {result.Path.Length}.");
        Assert.NotNull(result.BlockedSegmentIndex);
        Assert.InRange(result.BlockedSegmentIndex!.Value, 90, 110);
        Assert.StartsWith("interior_projection:", result.BlockedReason, System.StringComparison.Ordinal);
        Assert.Equal(result.Path, result.RawPath);
    }

    [Fact]
    public void CalculateRawPath_DeckLipStraightRoute_RemainsUnblocked()
    {
        var result = _navigation.CalculateRawPath(
            Kalimdor,
            DeckLipLastGoodStart,
            OrgrimmarUndercityZeppelinBoardingPoint,
            smoothPath: false,
            agentRadius: TaurenMaleRadius,
            agentHeight: TaurenMaleHeight);

        Assert.Equal("raw_detour", result.Result);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal("none", result.BlockedReason);
        Assert.True(result.Path.Length >= 4, $"Expected the straight deck-lip corridor to stay available; got {result.Path.Length} corners.");
        Assert.Equal(result.Path, result.RawPath);
    }

    private static float Distance2D(XYZ a, XYZ b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
