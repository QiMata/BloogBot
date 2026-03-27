using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorPairWindowAdjustmentTests
{
    private const float Gravity = 19.29110527f;

    [Fact]
    public void EvaluateWoWSelectorPairWindowAdjustment_ZeroesMoveWhenVerticalTravelAlreadyElapsed()
    {
        Vector3 move = new(3.0f, 4.0f, 0.0f);
        float outputMagnitude = 123.0f;

        float scalar = EvaluateWoWSelectorPairWindowAdjustment(
            windowSpanScalar: 0.5f,
            windowStartScalar: 2.0f,
            ref move,
            ref outputMagnitude,
            alternateUnitZState: false,
            horizontalReferenceMagnitude: 5.0f,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 10.0f,
            positionZ: 0.0f);

        Assert.Equal(0.0f, scalar, 6);
        Assert.Equal(0.0f, move.X, 6);
        Assert.Equal(0.0f, move.Y, 6);
        Assert.Equal(0.0f, move.Z, 6);
        Assert.Equal(0.0f, outputMagnitude, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorPairWindowAdjustment_ReturnsWindowSpanWhenRemainingWindowIsLarger()
    {
        Vector3 move = new(3.0f, 4.0f, 0.0f);
        float outputMagnitude = 123.0f;

        float scalar = EvaluateWoWSelectorPairWindowAdjustment(
            windowSpanScalar: 0.25f,
            windowStartScalar: 0.1f,
            ref move,
            ref outputMagnitude,
            alternateUnitZState: false,
            horizontalReferenceMagnitude: 5.0f,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 10.0f,
            positionZ: 0.0f);

        Assert.Equal(0.25f, scalar, 6);
        Assert.Equal(3.0f, move.X, 6);
        Assert.Equal(4.0f, move.Y, 6);
        Assert.Equal(0.0f, move.Z, 6);
        Assert.Equal(123.0f, outputMagnitude, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorPairWindowAdjustment_ScalesHorizontalMoveWhenRemainingWindowIsShorter()
    {
        Vector3 move = new(3.0f, 4.0f, 0.5f);
        float outputMagnitude = -1.0f;

        float expectedTravel = MathF.Sqrt(9.5f * (2.0f / Gravity));
        float expectedRemaining = expectedTravel - 0.2f;
        float expectedScale = expectedRemaining / 7.5f;

        float scalar = EvaluateWoWSelectorPairWindowAdjustment(
            windowSpanScalar: 1.5f,
            windowStartScalar: 0.2f,
            ref move,
            ref outputMagnitude,
            alternateUnitZState: false,
            horizontalReferenceMagnitude: 1.0f,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 10.0f,
            positionZ: 0.0f);

        Assert.Equal(expectedRemaining, scalar, 5);
        Assert.Equal(3.0f * expectedScale, move.X, 5);
        Assert.Equal(4.0f * expectedScale, move.Y, 5);
        Assert.Equal(0.5f, move.Z, 6);
        Assert.Equal(MathF.Sqrt((move.X * move.X) + (move.Y * move.Y) + (move.Z * move.Z)), outputMagnitude, 5);
    }

    [Fact]
    public void EvaluateWoWSelectorPairWindowAdjustment_EqualHorizontalWindowDoesNotScaleOrRewriteOutput()
    {
        Vector3 move = new(3.0f, 4.0f, 0.5f);
        float outputMagnitude = 321.0f;
        float remaining = MathF.Sqrt(9.5f * (2.0f / Gravity)) - 0.2f;
        float horizontalReferenceMagnitude = (5.0f * 1.5f) / remaining;

        float scalar = EvaluateWoWSelectorPairWindowAdjustment(
            windowSpanScalar: 1.5f,
            windowStartScalar: 0.2f,
            ref move,
            ref outputMagnitude,
            alternateUnitZState: false,
            horizontalReferenceMagnitude,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 10.0f,
            positionZ: 0.0f);

        Assert.Equal(remaining, scalar, 5);
        Assert.Equal(3.0f, move.X, 6);
        Assert.Equal(4.0f, move.Y, 6);
        Assert.Equal(0.5f, move.Z, 6);
        Assert.Equal(321.0f, outputMagnitude, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorPairWindowAdjustment_AlternateStateUsesEarlierPositiveRoot()
    {
        Vector3 alternateMove = new(1.0f, 0.0f, 1.0f);
        float alternateOutputMagnitude = 111.0f;
        Vector3 normalMove = new(1.0f, 0.0f, 1.0f);
        float normalOutputMagnitude = 222.0f;

        float alternateScalar = EvaluateWoWSelectorPairWindowAdjustment(
            windowSpanScalar: 1.0f,
            windowStartScalar: 0.2f,
            ref alternateMove,
            ref alternateOutputMagnitude,
            alternateUnitZState: true,
            horizontalReferenceMagnitude: 10.0f,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: -8.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 0.0f,
            positionZ: 0.0f);
        float normalScalar = EvaluateWoWSelectorPairWindowAdjustment(
            windowSpanScalar: 1.0f,
            windowStartScalar: 0.2f,
            ref normalMove,
            ref normalOutputMagnitude,
            alternateUnitZState: false,
            horizontalReferenceMagnitude: 10.0f,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: -8.0f,
            horizontalSpeedScale: 7.0f,
            referenceZ: 0.0f,
            positionZ: 0.0f);

        Assert.Equal(0.0f, alternateScalar, 6);
        Assert.Equal(0.0f, alternateMove.X, 6);
        Assert.Equal(0.0f, alternateMove.Y, 6);
        Assert.Equal(0.0f, alternateMove.Z, 6);
        Assert.Equal(0.0f, alternateOutputMagnitude, 6);

        Assert.True(normalScalar > 0.2f);
        Assert.NotEqual(0.0f, normalMove.X);
        Assert.Equal(222.0f, normalOutputMagnitude, 6);
    }
}
