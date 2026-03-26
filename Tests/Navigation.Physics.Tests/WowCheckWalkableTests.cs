using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowCheckWalkableTests
{
    [Fact]
    public void PositiveSlopeInsideTriangle_ClearsGroundedWallFlag()
    {
        var triangle = new Triangle(
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f));
        var position = new Vector3(0.2f, 0.2f, 0f);
        var rawNormal = new Vector3(0f, 0f, 1f);

        bool walkable = EvaluateWoWCheckWalkable(
            triangle,
            rawNormal,
            position,
            collisionRadius: 0.333333f,
            boundingHeight: 2.027778f,
            useStandardWalkableThreshold: true,
            groundedWallFlagBefore: true,
            out bool walkableState,
            out bool groundedWallFlagAfter);

        Assert.True(walkable);
        Assert.False(walkableState);
        Assert.False(groundedWallFlagAfter);
    }

    [Fact]
    public void ShallowPositiveSlope_UsesGroundedWallFlagWithoutClearingIt()
    {
        var triangle = new Triangle(
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0.5f),
            new Vector3(0f, 1f, 0.5f));
        var position = new Vector3(0.2f, 0.2f, 0f);
        var rawNormal = new Vector3(0f, 0.8660254f, 0.5f);

        bool walkable = EvaluateWoWCheckWalkable(
            triangle,
            rawNormal,
            position,
            collisionRadius: 0.333333f,
            boundingHeight: 2.027778f,
            useStandardWalkableThreshold: true,
            groundedWallFlagBefore: true,
            out bool walkableState,
            out bool groundedWallFlagAfter);

        Assert.True(walkable);
        Assert.False(walkableState);
        Assert.True(groundedWallFlagAfter);
    }

    [Fact]
    public void NegativeSteepSlope_TopCornerTouch_ConsumesGroundedWallFlag()
    {
        var triangle = new Triangle(
            new Vector3(-1f, -1f, 2f),
            new Vector3(1f, -1f, 2f),
            new Vector3(-1f, 1f, 2f));
        var position = new Vector3(0f, 0f, 0f);
        var rawNormal = new Vector3(0f, 0f, -1f);

        bool walkable = EvaluateWoWCheckWalkable(
            triangle,
            rawNormal,
            position,
            collisionRadius: 1f,
            boundingHeight: 2f,
            useStandardWalkableThreshold: true,
            groundedWallFlagBefore: true,
            out bool walkableState,
            out bool groundedWallFlagAfter);

        Assert.True(walkable);
        Assert.True(walkableState);
        Assert.False(groundedWallFlagAfter);
    }

    [Fact]
    public void NegativeSteepSlope_WithoutTopCornerTouch_RemainsNonWalkable()
    {
        var triangle = new Triangle(
            new Vector3(-1f, -1f, 4f),
            new Vector3(1f, -1f, 4f),
            new Vector3(-1f, 1f, 4f));
        var position = new Vector3(0f, 0f, 0f);
        var rawNormal = new Vector3(0f, 0f, -1f);

        bool walkable = EvaluateWoWCheckWalkable(
            triangle,
            rawNormal,
            position,
            collisionRadius: 1f,
            boundingHeight: 2f,
            useStandardWalkableThreshold: true,
            groundedWallFlagBefore: false,
            out bool walkableState,
            out bool groundedWallFlagAfter);

        Assert.False(walkable);
        Assert.False(walkableState);
        Assert.False(groundedWallFlagAfter);
    }
}
