using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorNodeTraversalPayloadTests
{
    [Fact]
    public void BuildWoWSelectorNodeTraversalPayload_BuildsBoundsAndForcesCallbackMaskBit()
    {
        SelectorNodeTraversalRecord node = new()
        {
            TraversalBaseToken = 0x44u,
            ExtraNodeToken = 0x90u,
            StateBytesToken = 0xC4u,
            VertexBufferToken = 0xC8u,
            TriangleIndexToken = 0xD0u,
        };
        Vector3[] supportPoints =
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

        Assert.True(BuildWoWSelectorNodeTraversalPayload(
            node,
            supportPoints,
            supportPoints.Length,
            callbackMask: 0x12340040u,
            out SelectorNodeTraversalPayload payload));

        AssertVector(new Vector3(-3.0f, -5.0f, -4.0f), payload.QueryBoundsMin);
        AssertVector(new Vector3(7.0f, 8.0f, 9.0f), payload.QueryBoundsMax);
        Assert.Equal(0x00C0u, payload.CallbackMaskWord);
        Assert.Equal(0u, payload.AcceptedCount);
        Assert.Equal(node.TraversalBaseToken, payload.TraversalBaseToken);
        Assert.Equal(node.ExtraNodeToken, payload.ExtraNodeToken);
        Assert.Equal(node.StateBytesToken, payload.StateBytesToken);
        Assert.Equal(node.VertexBufferToken, payload.VertexBufferToken);
        Assert.Equal(node.TriangleIndexToken, payload.TriangleIndexToken);
    }

    [Fact]
    public void BuildWoWSelectorNodeTraversalPayload_RejectsMissingSupportPoints()
    {
        SelectorNodeTraversalRecord node = new();

        Assert.False(BuildWoWSelectorNodeTraversalPayload(
            node,
            Array.Empty<Vector3>(),
            0,
            callbackMask: 0u,
            out _));
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }
}
