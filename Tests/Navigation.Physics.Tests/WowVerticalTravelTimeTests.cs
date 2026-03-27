using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowVerticalTravelTimeTests
{
    private const float Gravity = 19.29110527f;
    private const float TerminalVelocity = 60.14800262f;
    private const float SafeFallTerminalVelocity = 7.0f;

    [Fact]
    public void EvaluateWoWVerticalTravelTimeScalar_StationaryShortDistanceUsesSquareRootBranch()
    {
        const float verticalDistance = 10.0f;

        float scalar = EvaluateWoWVerticalTravelTimeScalar(
            verticalDistance,
            preferEarlierPositiveRoot: false,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f);

        float expected = MathF.Sqrt(verticalDistance * (2.0f / Gravity));
        Assert.Equal(expected, scalar, 5);
    }

    [Fact]
    public void EvaluateWoWVerticalTravelTimeScalar_StationaryNonPositiveDistanceReturnsZero()
    {
        float scalar = EvaluateWoWVerticalTravelTimeScalar(
            verticalDistance: -1.0f,
            preferEarlierPositiveRoot: false,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f);

        Assert.Equal(0.0f, scalar, 6);
    }

    [Fact]
    public void EvaluateWoWVerticalTravelTimeScalar_StationaryLongDistanceUsesTerminalVelocityBranch()
    {
        const float verticalDistance = 200.0f;

        float scalar = EvaluateWoWVerticalTravelTimeScalar(
            verticalDistance,
            preferEarlierPositiveRoot: false,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f);

        float timeToTerminal = TerminalVelocity / Gravity;
        float distanceToTerminal = 0.5f * TerminalVelocity * timeToTerminal;
        float expected = ((verticalDistance - distanceToTerminal) / TerminalVelocity) + timeToTerminal;
        Assert.Equal(expected, scalar, 5);
    }

    [Fact]
    public void EvaluateWoWVerticalTravelTimeScalar_SafeFallUsesReducedTerminalVelocity()
    {
        const float verticalDistance = 200.0f;

        float normalScalar = EvaluateWoWVerticalTravelTimeScalar(
            verticalDistance,
            preferEarlierPositiveRoot: false,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed: 0.0f);
        float safeFallScalar = EvaluateWoWVerticalTravelTimeScalar(
            verticalDistance,
            preferEarlierPositiveRoot: false,
            movementFlags: (uint)MoveFlags.SafeFall,
            verticalSpeed: 0.0f);

        float timeToTerminal = SafeFallTerminalVelocity / Gravity;
        float distanceToTerminal = 0.5f * SafeFallTerminalVelocity * timeToTerminal;
        float expected = ((verticalDistance - distanceToTerminal) / SafeFallTerminalVelocity) + timeToTerminal;

        Assert.Equal(expected, safeFallScalar, 5);
        Assert.True(safeFallScalar > normalScalar);
    }

    [Fact]
    public void EvaluateWoWVerticalTravelTimeScalar_PreferEarlierPositiveRootSelectsFirstCrossing()
    {
        const float verticalDistance = -1.0f;
        const float verticalSpeed = -8.0f;

        float earlierScalar = EvaluateWoWVerticalTravelTimeScalar(
            verticalDistance,
            preferEarlierPositiveRoot: true,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed);
        float laterScalar = EvaluateWoWVerticalTravelTimeScalar(
            verticalDistance,
            preferEarlierPositiveRoot: false,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed);

        float root = MathF.Sqrt((verticalSpeed * verticalSpeed) + (verticalDistance * (2.0f * Gravity)));
        float expectedEarlier = (-verticalSpeed - root) / Gravity;
        float expectedLater = (root - verticalSpeed) / Gravity;

        Assert.Equal(expectedEarlier, earlierScalar, 5);
        Assert.Equal(expectedLater, laterScalar, 5);
        Assert.True(earlierScalar > 0.0f);
        Assert.True(laterScalar > earlierScalar);
    }

    [Fact]
    public void EvaluateWoWVerticalTravelTimeScalar_ClampsPositiveOverspeedToTerminalBeforeSolving()
    {
        const float verticalDistance = 500.0f;
        const float verticalSpeed = 80.0f;

        float scalar = EvaluateWoWVerticalTravelTimeScalar(
            verticalDistance,
            preferEarlierPositiveRoot: false,
            movementFlags: (uint)MoveFlags.None,
            verticalSpeed);

        float clampedSpeed = TerminalVelocity;
        float timeToTerminal = (TerminalVelocity - clampedSpeed) / Gravity;
        float distanceToTerminal = ((timeToTerminal * (Gravity * 0.5f)) + clampedSpeed) * timeToTerminal;
        float expected = timeToTerminal + ((verticalDistance - distanceToTerminal) / TerminalVelocity);

        Assert.Equal(expected, scalar, 5);
    }
}
