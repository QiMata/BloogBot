using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorObjectTraversalHelperTests
{
    [Fact]
    public void BuildWoWSelectorObjectCallbackMask_UsesDefaultFoldWhenOptionalMovementBitsAreAbsent()
    {
        Assert.Equal(0xACu, BuildWoWSelectorObjectCallbackMask(0u));
        Assert.Equal(0x84u, BuildWoWSelectorObjectCallbackMask(0x10u));
    }

    [Fact]
    public void BuildWoWSelectorObjectCallbackMask_PreservesOrClearsBinaryBitsExactlyLike6A3DC0()
    {
        Assert.Equal(0xC6u, BuildWoWSelectorObjectCallbackMask(0x10u | 0x40u | 0x4000u));
        Assert.Equal(0xC2u, BuildWoWSelectorObjectCallbackMask(0x10u | 0x20u | 0x40u | 0x4000u));
    }

    [Fact]
    public void EvaluateWoWShouldResolveSelectorObjectNode_RequiresSelectorTreeAndEitherLiveOrForcedNode()
    {
        Assert.False(EvaluateWoWShouldResolveSelectorObjectNode(
            selectorEnabled: false,
            nodeEnabled: true,
            allowInactiveNode: true));

        Assert.False(EvaluateWoWShouldResolveSelectorObjectNode(
            selectorEnabled: true,
            nodeEnabled: false,
            allowInactiveNode: false));

        Assert.True(EvaluateWoWShouldResolveSelectorObjectNode(
            selectorEnabled: true,
            nodeEnabled: true,
            allowInactiveNode: false));

        Assert.True(EvaluateWoWShouldResolveSelectorObjectNode(
            selectorEnabled: true,
            nodeEnabled: false,
            allowInactiveNode: true));
    }

    [Fact]
    public void ResolveWoWSelectorObjectNodePointer_ReturnsPointerOnlyWhenNodePassesTheBinaryGate()
    {
        IntPtr nodePointer = new(0x12345678);

        Assert.Equal(IntPtr.Zero, ResolveWoWSelectorObjectNodePointer(
            selectorEnabled: false,
            nodePointer: nodePointer,
            nodeEnabled: true,
            allowInactiveNode: true));

        Assert.Equal(IntPtr.Zero, ResolveWoWSelectorObjectNodePointer(
            selectorEnabled: true,
            nodePointer: nodePointer,
            nodeEnabled: false,
            allowInactiveNode: false));

        Assert.Equal(nodePointer, ResolveWoWSelectorObjectNodePointer(
            selectorEnabled: true,
            nodePointer: nodePointer,
            nodeEnabled: true,
            allowInactiveNode: false));

        Assert.Equal(nodePointer, ResolveWoWSelectorObjectNodePointer(
            selectorEnabled: true,
            nodePointer: nodePointer,
            nodeEnabled: false,
            allowInactiveNode: true));
    }

    [Fact]
    public void EvaluateWoWShouldUseSelectorObjectCallback_RequiresNonNullCallbackPointer()
    {
        Assert.False(EvaluateWoWShouldUseSelectorObjectCallback(0u));
        Assert.True(EvaluateWoWShouldUseSelectorObjectCallback(0x1234u));
    }

    [Fact]
    public void FinalizeWoWSelectorObjectNoCallbackState_PreservesAllCallerVisibleOutputs()
    {
        Assert.True(FinalizeWoWSelectorObjectNoCallbackState(
            inputHitResult: 0x05u,
            inputRecordCount: 7u,
            inputOutputFlags: 0x20u,
            out SelectorObjectNoCallbackState state));

        Assert.Equal(0x05u, state.HitResult);
        Assert.Equal(7u, state.RecordCount);
        Assert.Equal(0x20u, state.OutputFlags);
    }

    [Fact]
    public void BuildWoWSelectorSupportPointBounds_ZeroCountReturnsZeroBounds()
    {
        Assert.True(BuildWoWSelectorSupportPointBounds(
            Array.Empty<Vector3>(),
            0,
            out Vector3 boundsMin,
            out Vector3 boundsMax));

        AssertVector(new Vector3(0.0f, 0.0f, 0.0f), boundsMin);
        AssertVector(new Vector3(0.0f, 0.0f, 0.0f), boundsMax);
    }

    [Fact]
    public void BuildWoWSelectorSupportPointBounds_MergesEverySupportPointIntoMinAndMaxExtrema()
    {
        Vector3[] points =
        [
            new Vector3(2.0f, 5.0f, 1.0f),
            new Vector3(-1.0f, 4.0f, 3.0f),
            new Vector3(3.0f, -2.0f, 0.5f),
            new Vector3(0.0f, 8.0f, -4.0f),
            new Vector3(7.0f, 1.0f, 9.0f),
            new Vector3(-3.0f, 6.0f, 2.0f),
            new Vector3(1.5f, -5.0f, 4.0f),
            new Vector3(4.0f, 3.0f, -1.0f),
        ];

        Assert.True(BuildWoWSelectorSupportPointBounds(
            points,
            points.Length,
            out Vector3 boundsMin,
            out Vector3 boundsMax));

        AssertVector(new Vector3(-3.0f, -5.0f, -4.0f), boundsMin);
        AssertVector(new Vector3(7.0f, 8.0f, 9.0f), boundsMax);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }
}
