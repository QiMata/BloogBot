using GameData.Core.Models;
using PathfindingService.Repository;
using System.Collections.Generic;
using Xunit;

namespace PathfindingService.Tests;

[Collection(NavigationCollection.Name)]
public class DeckLipRawPathContractTests(NavigationFixture fixture)
{
    private const uint Kalimdor = 1;
    private const float TaurenMaleRadius = 1.0247f;
    private const float TaurenMaleHeight = 2.625f;

    private static readonly XYZ DeckLipGruntBaseStart = new(1332.76f, -4633.40f, 24.0783f);
    private static readonly XYZ OrgrimmarZeppelinUpperDeckApproach = new(1338.13f, -4645.96f, 51.60f);
    private static readonly XYZ OrgrimmarZeppelinMasterFrezzaSpawn = new(1331.11f, -4649.45f, 53.6269f);
    private static readonly XYZ OrgrimmarUndercityZeppelinBoardingPoint = new(1320.142944f, -4653.158691f, 53.891945f);
    private const float OrcZeppelinHouseMaxY = -4618.9623f;
    private const float ExteriorHillMarginY = 16.0f;
    private const float LowerTowerLayerCeilingZ = 45.0f;

    private readonly Navigation _navigation = fixture.Navigation;

    [Fact]
    public void CalculateRawPath_DeckLipGruntBaseToLiteralFrezza_ReachesUpperTowerLayer()
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
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal("none", result.BlockedReason);
        Assert.True(result.Path.Length >= 3, $"Expected the Grunt-base -> Frezza path to use tower navigation corners; got {result.Path.Length} corners.");
        Assert.True(finalDistance2D <= 4.0f, $"Expected the literal Frezza path to finish near the NPC spawn; final 2D distance was {finalDistance2D:F2}y.");
        Assert.True(finalWaypoint.Z >= 50.0f, $"Expected the literal Frezza path to finish on the upper tower layer; final waypoint was ({finalWaypoint.X:F2},{finalWaypoint.Y:F2},{finalWaypoint.Z:F2}).");
        Assert.True(finalDeltaZ <= 2.0f, $"Expected the literal Frezza path to finish on the upper-deck Z layer; final |dz| was {finalDeltaZ:F2}y.");
        Assert.Equal(result.Path, result.RawPath);
    }

    [Fact]
    public void CalculateRawPath_DeckLipGruntBaseToBoardingCorridor_ReachesUpperTowerLayer()
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
        Assert.True(result.Path.Length >= 3, $"Expected the Grunt-base smooth path to keep tower navigation corners; got {result.Path.Length}.");
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal("none", result.BlockedReason);
        var finalWaypoint = result.Path[^1];
        var finalDistance2D = Distance2D(finalWaypoint, OrgrimmarUndercityZeppelinBoardingPoint);
        var finalDeltaZ = MathF.Abs(finalWaypoint.Z - OrgrimmarUndercityZeppelinBoardingPoint.Z);
        Assert.True(finalDistance2D <= 4.0f, $"Expected the boarding route to finish near the upper deck target; final 2D distance was {finalDistance2D:F2}y.");
        Assert.True(finalWaypoint.Z >= 50.0f, $"Expected the boarding route to finish on the upper tower layer; final waypoint was ({finalWaypoint.X:F2},{finalWaypoint.Y:F2},{finalWaypoint.Z:F2}).");
        Assert.True(finalDeltaZ <= 2.0f, $"Expected the boarding route to finish on the upper-deck Z layer; final |dz| was {finalDeltaZ:F2}y.");
        Assert.Equal(result.Path, result.RawPath);
    }

    [Fact]
    public void CalculateRawPath_DeckLipGruntBaseToBoardingCorridor_DoesNotLeaveTowerForExteriorHill()
    {
        var result = _navigation.CalculateRawPath(
            Kalimdor,
            DeckLipGruntBaseStart,
            OrgrimmarUndercityZeppelinBoardingPoint,
            smoothPath: true,
            agentRadius: TaurenMaleRadius,
            agentHeight: TaurenMaleHeight);

        var exteriorHillWaypoints = new List<string>();
        for (var i = 0; i < result.Path.Length; i++)
        {
            var waypoint = result.Path[i];
            if (waypoint.Y <= OrcZeppelinHouseMaxY + ExteriorHillMarginY ||
                waypoint.Z >= LowerTowerLayerCeilingZ)
            {
                continue;
            }

            exteriorHillWaypoints.Add(
                $"[{i}] ({waypoint.X:F1},{waypoint.Y:F1},{waypoint.Z:F1})");
        }

        Assert.True(
            exteriorHillWaypoints.Count == 0,
            $"Deck-lip route left the Orczeppelinhouse.wmo bounds for the exterior hill " +
            $"before reaching the upper deck. Tower maxY={OrcZeppelinHouseMaxY:F1}, " +
            $"allowedY={OrcZeppelinHouseMaxY + ExteriorHillMarginY:F1}, " +
            $"badWaypoints={string.Join(", ", exteriorHillWaypoints)}" +
            $"{Environment.NewLine}{FormatPath(result.Path)}");
    }

    [Fact]
    public void CalculateRawPath_UpperDeckStraightRoute_RemainsUnblocked()
    {
        var result = _navigation.CalculateRawPath(
            Kalimdor,
            OrgrimmarZeppelinUpperDeckApproach,
            OrgrimmarUndercityZeppelinBoardingPoint,
            smoothPath: false,
            agentRadius: TaurenMaleRadius,
            agentHeight: TaurenMaleHeight);

        Assert.Equal("raw_detour", result.Result);
        Assert.Null(result.BlockedSegmentIndex);
        Assert.Equal("none", result.BlockedReason);
        Assert.True(result.Path.Length >= 2, $"Expected the straight upper-deck corridor to stay available; got {result.Path.Length} corners.");
        Assert.All(result.Path, waypoint =>
            Assert.True(
                waypoint.Z >= 50.0f,
                $"Expected the upper-deck corridor to stay on the upper layer; got ({waypoint.X:F2},{waypoint.Y:F2},{waypoint.Z:F2}).{Environment.NewLine}{FormatPath(result.Path)}"));
        Assert.Equal(result.Path, result.RawPath);
    }

    private static float Distance2D(XYZ a, XYZ b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static string FormatPath(IReadOnlyList<XYZ> path)
    {
        if (path.Count == 0)
            return "<empty path>";

        var lines = new List<string>(path.Count);
        for (var i = 0; i < path.Count; i++)
        {
            var point = path[i];
            lines.Add($"[{i:D3}] ({point.X:F3},{point.Y:F3},{point.Z:F3})");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
