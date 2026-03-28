using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorAlternateUnitZStateHandlerTests
{
    [Fact]
    public void EvaluateWoWSelectorAlternateUnitZStateHandler_ZeroesFallStateAndSetsFallingFar()
    {
        uint outputFlags = EvaluateWoWSelectorAlternateUnitZStateHandler(
            movementFlags: (uint)(MoveFlags.Forward | MoveFlags.Jumping),
            positionZ: 17.25f,
            out SelectorAlternateUnitZStateTrace trace);

        Assert.Equal((uint)(MoveFlags.Forward | MoveFlags.Jumping | MoveFlags.Falling), outputFlags);
        Assert.Equal(1u, trace.SetFallingFarFlag);
        Assert.Equal(1u, trace.ClearedFallTime);
        Assert.Equal(1u, trace.ZeroedVerticalSpeed);
        Assert.Equal(1u, trace.CopiedPositionZToFallStartZ);
        Assert.Equal(17.25f, trace.OutputFallStartZ, 6);
        Assert.Equal(0u, trace.OutputFallTime);
        Assert.Equal(0.0f, trace.OutputVerticalSpeed, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorAlternateUnitZStateHandler_PreservesUnrelatedFlagsWhileForcingFallingFar()
    {
        uint outputFlags = EvaluateWoWSelectorAlternateUnitZStateHandler(
            movementFlags: (uint)(MoveFlags.Backward | MoveFlags.Swimming | MoveFlags.Falling),
            positionZ: -3.5f,
            out SelectorAlternateUnitZStateTrace trace);

        Assert.Equal((uint)(MoveFlags.Backward | MoveFlags.Swimming | MoveFlags.Falling), outputFlags);
        Assert.Equal((uint)(MoveFlags.Backward | MoveFlags.Swimming | MoveFlags.Falling), trace.OutputMovementFlags);
        Assert.Equal(-3.5f, trace.InputPositionZ, 6);
        Assert.Equal(-3.5f, trace.OutputFallStartZ, 6);
    }
}
