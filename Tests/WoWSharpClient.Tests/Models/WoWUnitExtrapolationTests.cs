using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Models;

namespace WoWSharpClient.Tests.Models;

public class WoWUnitExtrapolationTests
{
    [Fact]
    public void GetExtrapolatedPosition_BackwardUsesRunBackSpeed()
    {
        var unit = CreateUnit(
            new Position(10f, 20f, 30f),
            MovementFlags.MOVEFLAG_BACKWARD,
            facing: 0f,
            runSpeed: 7f,
            runBackSpeed: 4.5f,
            timestampMs: 1000);

        var predicted = unit.GetExtrapolatedPosition(2000);

        Assert.Equal(5.5f, predicted.X, 3);
        Assert.Equal(20f, predicted.Y, 3);
        Assert.Equal(30f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_StrafeRightUsesPerpendicularBasis()
    {
        var unit = CreateUnit(
            new Position(0f, 0f, 0f),
            MovementFlags.MOVEFLAG_STRAFE_RIGHT,
            facing: 0f,
            runSpeed: 7f,
            runBackSpeed: 4.5f,
            timestampMs: 1000);

        var predicted = unit.GetExtrapolatedPosition(2000);

        Assert.Equal(0f, predicted.X, 3);
        Assert.Equal(-7f, predicted.Y, 3);
        Assert.Equal(0f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_ForwardStrafeAppliesDiagonalDamping()
    {
        var unit = CreateUnit(
            new Position(0f, 0f, 0f),
            MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT,
            facing: MathF.PI / 2f,
            runSpeed: 7f,
            runBackSpeed: 4.5f,
            timestampMs: 1000);

        var predicted = unit.GetExtrapolatedPosition(2000);

        Assert.Equal(-4.9497f, predicted.X, 3);
        Assert.Equal(4.9497f, predicted.Y, 3);
        Assert.Equal(0f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_SpeedBelowJitterThreshold_ReturnsCurrentPosition()
    {
        var unit = CreateUnit(
            new Position(30f, 40f, 50f),
            MovementFlags.MOVEFLAG_FORWARD,
            facing: 0f,
            runSpeed: 2.9f,
            runBackSpeed: 2.9f,
            timestampMs: 1000);

        unit.Position = new Position(31f, 41f, 51f);

        var predicted = unit.GetExtrapolatedPosition(1500);

        Assert.Equal(31f, predicted.X, 3);
        Assert.Equal(41f, predicted.Y, 3);
        Assert.Equal(51f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_SpeedAboveTeleportThreshold_ReturnsCurrentPosition()
    {
        var unit = CreateUnit(
            new Position(30f, 40f, 50f),
            MovementFlags.MOVEFLAG_FORWARD,
            facing: 0f,
            runSpeed: 60.5f,
            runBackSpeed: 60.5f,
            timestampMs: 1000);

        unit.Position = new Position(31f, 41f, 51f);

        var predicted = unit.GetExtrapolatedPosition(1500);

        Assert.Equal(31f, predicted.X, 3);
        Assert.Equal(41f, predicted.Y, 3);
        Assert.Equal(51f, predicted.Z, 3);
    }

    [Fact]
    public void GetExtrapolatedPosition_StaleUpdate_ReturnsCurrentPosition()
    {
        var unit = CreateUnit(
            new Position(10f, 20f, 30f),
            MovementFlags.MOVEFLAG_FORWARD,
            facing: 0f,
            runSpeed: 7f,
            runBackSpeed: 4.5f,
            timestampMs: 1000);

        unit.Position = new Position(12f, 22f, 32f);

        var predicted = unit.GetExtrapolatedPosition(2601);

        Assert.Equal(12f, predicted.X, 3);
        Assert.Equal(22f, predicted.Y, 3);
        Assert.Equal(32f, predicted.Z, 3);
    }

    private static WoWUnit CreateUnit(
        Position position,
        MovementFlags movementFlags,
        float facing,
        float runSpeed,
        float runBackSpeed,
        uint timestampMs)
    {
        return new WoWUnit(new HighGuid(1))
        {
            Position = position,
            RunSpeed = runSpeed,
            RunBackSpeed = runBackSpeed,
            ExtrapolationBasePosition = new Position(position.X, position.Y, position.Z),
            ExtrapolationFlags = movementFlags,
            ExtrapolationFacing = facing,
            ExtrapolationTimeMs = timestampMs,
        };
    }
}
