using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPairCommitTailTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitTail_SelectedIndexSentinelStartsFallZero()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverSelectedPairCommitTail(
            selectedIndex: 5u,
            selectedCount: 5u,
            consumedSelectedState: 0u,
            snapshotBeforeCommitState: 0u,
            movementFlags: (uint)(MoveFlags.Forward | MoveFlags.SplineElevation | MoveFlags.Swimming),
            cachedPair: new SelectorPair { First = 2.0f, Second = 3.0f },
            currentPosition: new Vector3(10.0f, 11.0f, 12.0f),
            currentFacing: 1.25f,
            currentPitch: -0.5f,
            cachedPosition: new Vector3(1.0f, 2.0f, 3.0f),
            cachedFacing: 0.1f,
            cachedPitch: 0.2f,
            cachedMoveTimestamp: 44u,
            inputFallTime: 91u,
            inputFallStartZ: 7.0f,
            inputVerticalSpeed: -8.0f,
            out GroundedDriverSelectedPairCommitTailTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPairCommitTailKind.StartFallZero, dispatchKind);
        Assert.Equal(1u, trace.UsedStartFallZero);
        Assert.Equal(1u, trace.UsedCacheSnapshot);
        Assert.Equal(0u, trace.ForwardedPair);
        Assert.Equal((uint)(MoveFlags.Forward | MoveFlags.Jumping), trace.OutputMovementFlags);
        Assert.Equal(10.0f, trace.OutputCachedPosition.X, 6);
        Assert.Equal(11.0f, trace.OutputCachedPosition.Y, 6);
        Assert.Equal(12.0f, trace.OutputCachedPosition.Z, 6);
        Assert.Equal(1.25f, trace.OutputCachedFacing, 6);
        Assert.Equal(-0.5f, trace.OutputCachedPitch, 6);
        Assert.Equal(0u, trace.OutputMoveTimestamp);
        Assert.Equal(0u, trace.OutputFallTime);
        Assert.Equal(12.0f, trace.OutputFallStartZ, 6);
        Assert.Equal(0.0f, trace.OutputVerticalSpeed, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitTail_ConsumedStateStartsFallZero()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverSelectedPairCommitTail(
            selectedIndex: 2u,
            selectedCount: 5u,
            consumedSelectedState: 1u,
            snapshotBeforeCommitState: 0u,
            movementFlags: (uint)MoveFlags.Forward,
            cachedPair: new SelectorPair { First = 4.0f, Second = 5.0f },
            currentPosition: new Vector3(3.0f, 4.0f, 5.0f),
            currentFacing: 0.75f,
            currentPitch: 0.5f,
            cachedPosition: new Vector3(-1.0f, -2.0f, -3.0f),
            cachedFacing: -0.25f,
            cachedPitch: -0.125f,
            cachedMoveTimestamp: 12u,
            inputFallTime: 14u,
            inputFallStartZ: 8.0f,
            inputVerticalSpeed: -2.0f,
            out GroundedDriverSelectedPairCommitTailTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPairCommitTailKind.StartFallZero, dispatchKind);
        Assert.Equal(1u, trace.UsedStartFallZero);
        Assert.Equal(1u, trace.UsedCacheSnapshot);
        Assert.Equal(0u, trace.ForwardedPair);
        Assert.Equal((uint)(MoveFlags.Forward | MoveFlags.Jumping), trace.OutputMovementFlags);
        Assert.Equal(5.0f, trace.OutputFallStartZ, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitTail_SnapshotStateCopiesMovementCacheThenForwardsPair()
    {
        SelectorPair cachedPair = new() { First = 6.0f, Second = 7.0f };

        uint dispatchKind = EvaluateWoWGroundedDriverSelectedPairCommitTail(
            selectedIndex: 1u,
            selectedCount: 5u,
            consumedSelectedState: 0u,
            snapshotBeforeCommitState: 1u,
            movementFlags: (uint)(MoveFlags.Forward | MoveFlags.Hover),
            cachedPair: cachedPair,
            currentPosition: new Vector3(9.0f, 8.0f, 7.0f),
            currentFacing: -1.0f,
            currentPitch: 0.375f,
            cachedPosition: new Vector3(1.0f, 1.0f, 1.0f),
            cachedFacing: 0.5f,
            cachedPitch: -0.75f,
            cachedMoveTimestamp: 64u,
            inputFallTime: 10u,
            inputFallStartZ: 2.0f,
            inputVerticalSpeed: -1.0f,
            out GroundedDriverSelectedPairCommitTailTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPairCommitTailKind.ForwardPair, dispatchKind);
        Assert.Equal(1u, trace.UsedCacheSnapshot);
        Assert.Equal(1u, trace.ForwardedPair);
        Assert.Equal(0u, trace.DeferredHoverRerank);
        Assert.Equal(cachedPair.First, trace.OutputPair.First, 6);
        Assert.Equal(cachedPair.Second, trace.OutputPair.Second, 6);
        Assert.Equal(9.0f, trace.OutputCachedPosition.X, 6);
        Assert.Equal(8.0f, trace.OutputCachedPosition.Y, 6);
        Assert.Equal(7.0f, trace.OutputCachedPosition.Z, 6);
        Assert.Equal(-1.0f, trace.OutputCachedFacing, 6);
        Assert.Equal(0.375f, trace.OutputCachedPitch, 6);
        Assert.Equal(0u, trace.OutputMoveTimestamp);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitTail_NonHoverFastPathForwardsPairWithoutSnapshot()
    {
        SelectorPair cachedPair = new() { First = 8.0f, Second = 9.0f };

        uint dispatchKind = EvaluateWoWGroundedDriverSelectedPairCommitTail(
            selectedIndex: 1u,
            selectedCount: 5u,
            consumedSelectedState: 0u,
            snapshotBeforeCommitState: 0u,
            movementFlags: (uint)MoveFlags.Forward,
            cachedPair: cachedPair,
            currentPosition: new Vector3(4.0f, 5.0f, 6.0f),
            currentFacing: 1.0f,
            currentPitch: 0.25f,
            cachedPosition: new Vector3(7.0f, 8.0f, 9.0f),
            cachedFacing: -0.5f,
            cachedPitch: -0.25f,
            cachedMoveTimestamp: 22u,
            inputFallTime: 15u,
            inputFallStartZ: 3.0f,
            inputVerticalSpeed: -4.0f,
            out GroundedDriverSelectedPairCommitTailTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPairCommitTailKind.ForwardPair, dispatchKind);
        Assert.Equal(0u, trace.UsedCacheSnapshot);
        Assert.Equal(1u, trace.ForwardedPair);
        Assert.Equal(0u, trace.DeferredHoverRerank);
        Assert.Equal(cachedPair.First, trace.OutputPair.First, 6);
        Assert.Equal(cachedPair.Second, trace.OutputPair.Second, 6);
        Assert.Equal(7.0f, trace.OutputCachedPosition.X, 6);
        Assert.Equal(8.0f, trace.OutputCachedPosition.Y, 6);
        Assert.Equal(9.0f, trace.OutputCachedPosition.Z, 6);
        Assert.Equal(22u, trace.OutputMoveTimestamp);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitTail_HoverBitDefersToLargerRerankPath()
    {
        uint dispatchKind = EvaluateWoWGroundedDriverSelectedPairCommitTail(
            selectedIndex: 1u,
            selectedCount: 5u,
            consumedSelectedState: 0u,
            snapshotBeforeCommitState: 0u,
            movementFlags: (uint)MoveFlags.Hover,
            cachedPair: new SelectorPair { First = 1.0f, Second = 2.0f },
            currentPosition: new Vector3(1.0f, 2.0f, 3.0f),
            currentFacing: 0.0f,
            currentPitch: 0.0f,
            cachedPosition: new Vector3(4.0f, 5.0f, 6.0f),
            cachedFacing: 0.5f,
            cachedPitch: -0.5f,
            cachedMoveTimestamp: 18u,
            inputFallTime: 3u,
            inputFallStartZ: 1.0f,
            inputVerticalSpeed: -1.0f,
            out GroundedDriverSelectedPairCommitTailTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPairCommitTailKind.DeferredHoverRerank, dispatchKind);
        Assert.Equal(0u, trace.UsedStartFallZero);
        Assert.Equal(0u, trace.ForwardedPair);
        Assert.Equal(1u, trace.DeferredHoverRerank);
        Assert.Equal(0.0f, trace.OutputPair.First, 6);
        Assert.Equal(0.0f, trace.OutputPair.Second, 6);
        Assert.Equal((uint)MoveFlags.Hover, trace.OutputMovementFlags);
    }
}
