using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorPairFollowupGateTests
{
    private const float NegInvGravity = -1.0f / 19.29110527f;

    [Fact]
    public void EvaluateJumpTimeScalar_JumpingFlagReturnsVerticalSpeedTimesNegativeInverseGravity()
    {
        float jumpTimeScalar = EvaluateWoWJumpTimeScalar(
            (uint)MoveFlags.Jumping,
            verticalSpeed: -7.955547f);

        Assert.Equal(-7.955547f * NegInvGravity, jumpTimeScalar, 6);
    }

    [Fact]
    public void EvaluateJumpTimeScalar_WithoutJumpingFlagReturnsZero()
    {
        float jumpTimeScalar = EvaluateWoWJumpTimeScalar(
            (uint)MoveFlags.FallingFar,
            verticalSpeed: -7.955547f);

        Assert.Equal(0f, jumpTimeScalar, 6);
    }

    [Fact]
    public void EvaluateSelectorPairFollowupGate_AlternateUnitZStateShortCircuitsTrue()
    {
        bool accepted = EvaluateWoWSelectorPairFollowupGate(
            windowStartScalar: 0.5f,
            windowSpanScalar: 0.25f,
            moveVector: new Vector3(10f, 0f, 3f),
            alternateUnitZState: true,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0f,
            horizontalSpeedScale: 0f);

        Assert.True(accepted);
    }

    [Fact]
    public void EvaluateSelectorPairFollowupGate_NonNegativeVerticalSpeedReturnsFalse()
    {
        bool accepted = EvaluateWoWSelectorPairFollowupGate(
            windowStartScalar: 0.5f,
            windowSpanScalar: 0.25f,
            moveVector: new Vector3(1f, 1f, 0f),
            alternateUnitZState: false,
            movementFlags: (uint)MoveFlags.Jumping,
            verticalSpeed: 0f,
            horizontalSpeedScale: 7f);

        Assert.False(accepted);
    }

    [Fact]
    public void EvaluateSelectorPairFollowupGate_WindowEndBeforeJumpScalarReturnsTrue()
    {
        bool accepted = EvaluateWoWSelectorPairFollowupGate(
            windowStartScalar: 0.1f,
            windowSpanScalar: 0.1f,
            moveVector: new Vector3(10f, 0f, 0f),
            alternateUnitZState: false,
            movementFlags: (uint)MoveFlags.Jumping,
            verticalSpeed: -7.955547f,
            horizontalSpeedScale: 7f);

        Assert.True(accepted);
    }

    [Fact]
    public void EvaluateSelectorPairFollowupGate_WindowStartAfterJumpScalarReturnsFalse()
    {
        bool accepted = EvaluateWoWSelectorPairFollowupGate(
            windowStartScalar: 0.5f,
            windowSpanScalar: 0.1f,
            moveVector: new Vector3(0.1f, 0f, 0f),
            alternateUnitZState: false,
            movementFlags: (uint)MoveFlags.Jumping,
            verticalSpeed: -7.955547f,
            horizontalSpeedScale: 7f);

        Assert.False(accepted);
    }

    [Fact]
    public void EvaluateSelectorPairFollowupGate_RemainingHorizontalAllowanceAboveMoveLengthReturnsTrue()
    {
        bool accepted = EvaluateWoWSelectorPairFollowupGate(
            windowStartScalar: 0.3f,
            windowSpanScalar: 0.2f,
            moveVector: new Vector3(0.4f, 0.3f, 0f),
            alternateUnitZState: false,
            movementFlags: (uint)MoveFlags.Jumping,
            verticalSpeed: -7.955547f,
            horizontalSpeedScale: 7f);

        Assert.True(accepted);
    }

    [Fact]
    public void EvaluateSelectorPairFollowupGate_RemainingHorizontalAllowanceEqualToMoveLengthReturnsFalse()
    {
        const float jumpTimeScalar = -7.955547f * NegInvGravity;
        const float windowStartScalar = 0.2f;
        const float horizontalSpeedScale = 7f;
        float remainingAllowance = (jumpTimeScalar - windowStartScalar) * horizontalSpeedScale;

        bool accepted = EvaluateWoWSelectorPairFollowupGate(
            windowStartScalar,
            windowSpanScalar: 0.3f,
            moveVector: new Vector3(remainingAllowance, 0f, 0f),
            alternateUnitZState: false,
            movementFlags: (uint)MoveFlags.Jumping,
            verticalSpeed: -7.955547f,
            horizontalSpeedScale);

        Assert.False(accepted);
    }
}
