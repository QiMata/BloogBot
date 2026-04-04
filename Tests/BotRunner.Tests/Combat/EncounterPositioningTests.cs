using BotRunner.Combat;
using GameData.Core.Models;
using Moq;
using GameData.Core.Interfaces;
using Xunit;
using System;

namespace BotRunner.Tests.Combat;

public class EncounterPositioningTests
{
    private static IWoWUnit CreateMockBoss(float x, float y, float z, float facing, float boundingRadius = 3f)
    {
        var boss = new Mock<IWoWUnit>();
        boss.Setup(b => b.Position).Returns(new Position(x, y, z));
        boss.Setup(b => b.Facing).Returns(facing);
        boss.Setup(b => b.BoundingRadius).Returns(boundingRadius);
        return boss.Object;
    }

    [Fact]
    public void GetMeleePosition_BehindBoss()
    {
        var boss = CreateMockBoss(100, 100, 0, 0f); // Facing east (0 rad)
        var pos = EncounterPositioning.GetMeleePosition(boss);
        // Behind = facing + PI = west
        Assert.True(pos.X < 100, "Melee should be west of east-facing boss");
    }

    [Fact]
    public void GetTankPosition_InFrontOfBoss()
    {
        var boss = CreateMockBoss(100, 100, 0, 0f); // Facing east
        var pos = EncounterPositioning.GetTankPosition(boss);
        Assert.True(pos.X > 100, "Tank should be east of east-facing boss");
    }

    [Fact]
    public void GetRangedPosition_FarBehindBoss()
    {
        var boss = CreateMockBoss(100, 100, 0, 0f);
        var pos = EncounterPositioning.GetRangedPosition(boss, maxRange: 30f);
        var dist = MathF.Sqrt((pos.X - 100) * (pos.X - 100) + (pos.Y - 100) * (pos.Y - 100));
        Assert.True(dist > 25f, $"Ranged should be ~30y away, was {dist:F1}y");
    }

    [Fact]
    public void IsInFrontCleaveZone_TrueForPositionInFront()
    {
        var boss = CreateMockBoss(100, 100, 0, 0f); // Facing east
        var inFront = new Position(105, 100, 0);     // Due east
        Assert.True(EncounterPositioning.IsInFrontCleaveZone(inFront, boss));
    }

    [Fact]
    public void IsInFrontCleaveZone_FalseForPositionBehind()
    {
        var boss = CreateMockBoss(100, 100, 0, 0f);
        var behind = new Position(95, 100, 0);       // Due west
        Assert.False(EncounterPositioning.IsInFrontCleaveZone(behind, boss));
    }

    [Fact]
    public void IsInTailSwipeZone_TrueForPositionDirectlyBehind()
    {
        var boss = CreateMockBoss(100, 100, 0, 0f);
        var behind = new Position(95, 100, 0);
        Assert.True(EncounterPositioning.IsInTailSwipeZone(behind, boss));
    }
}
