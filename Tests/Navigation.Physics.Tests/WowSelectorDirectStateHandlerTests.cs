using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorDirectStateHandlerTests
{
    [Fact]
    public void EvaluateWoWSelectorDirectStateHandler_NoJumpingPreservesCachedState()
    {
        Vector3 startPosition = new(9.0f, 8.0f, 7.0f);
        Vector3 cachedPosition = new(1.0f, 2.0f, 3.0f);

        uint outputFlags = EvaluateWoWSelectorDirectStateHandler(
            movementFlags: (uint)MoveFlags.Forward,
            startPosition: startPosition,
            facing: 1.5f,
            pitch: 0.25f,
            cachedPosition: cachedPosition,
            cachedFacing: -0.75f,
            cachedPitch: -0.5f,
            cachedMoveTimestamp: 33u,
            cachedScalar84: 4.5f,
            recomputedScalar84: 9.0f,
            out SelectorDirectStateTrace trace);

        Assert.Equal((uint)MoveFlags.Forward, outputFlags);
        Assert.Equal(0u, trace.JumpingBitWasSet);
        Assert.Equal(0u, trace.ClearedJumpingBit);
        Assert.Equal(0u, trace.CopiedPosition);
        Assert.Equal(0u, trace.CopiedFacing);
        Assert.Equal(0u, trace.CopiedPitch);
        Assert.Equal(0u, trace.ZeroedMoveTimestamp);
        Assert.Equal(0u, trace.WroteScalar84);
        Assert.Equal(cachedPosition.X, trace.OutputCachedPosition.X, 6);
        Assert.Equal(cachedPosition.Y, trace.OutputCachedPosition.Y, 6);
        Assert.Equal(cachedPosition.Z, trace.OutputCachedPosition.Z, 6);
        Assert.Equal(-0.75f, trace.OutputCachedFacing, 6);
        Assert.Equal(-0.5f, trace.OutputCachedPitch, 6);
        Assert.Equal(33u, trace.OutputMoveTimestamp);
        Assert.Equal(4.5f, trace.OutputScalar84, 6);
    }

    [Fact]
    public void EvaluateWoWSelectorDirectStateHandler_JumpingClearsBitAndRefreshesCachedState()
    {
        Vector3 startPosition = new(6.0f, 5.0f, 4.0f);
        Vector3 cachedPosition = new(-1.0f, -2.0f, -3.0f);

        uint outputFlags = EvaluateWoWSelectorDirectStateHandler(
            movementFlags: (uint)(MoveFlags.Forward | MoveFlags.Jumping | MoveFlags.Falling),
            startPosition: startPosition,
            facing: -1.25f,
            pitch: 0.375f,
            cachedPosition: cachedPosition,
            cachedFacing: 0.5f,
            cachedPitch: -0.875f,
            cachedMoveTimestamp: 91u,
            cachedScalar84: 2.0f,
            recomputedScalar84: 7.25f,
            out SelectorDirectStateTrace trace);

        Assert.Equal((uint)(MoveFlags.Forward | MoveFlags.Falling), outputFlags);
        Assert.Equal(1u, trace.JumpingBitWasSet);
        Assert.Equal(1u, trace.ClearedJumpingBit);
        Assert.Equal(1u, trace.CopiedPosition);
        Assert.Equal(1u, trace.CopiedFacing);
        Assert.Equal(1u, trace.CopiedPitch);
        Assert.Equal(1u, trace.ZeroedMoveTimestamp);
        Assert.Equal(1u, trace.WroteScalar84);
        Assert.Equal(startPosition.X, trace.OutputCachedPosition.X, 6);
        Assert.Equal(startPosition.Y, trace.OutputCachedPosition.Y, 6);
        Assert.Equal(startPosition.Z, trace.OutputCachedPosition.Z, 6);
        Assert.Equal(-1.25f, trace.OutputCachedFacing, 6);
        Assert.Equal(0.375f, trace.OutputCachedPitch, 6);
        Assert.Equal(0u, trace.OutputMoveTimestamp);
        Assert.Equal(7.25f, trace.OutputScalar84, 6);
    }
}
