using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTriangleAabbOutcodeCountTests
{
    [Fact]
    public void CountWoWTrianglesPassingAabbOutcodeReject_CountsOnlyTrianglesWithoutSharedOutsideBits()
    {
        ushort[] triangleIndices =
        [
            0, 1, 2,
            3, 4, 5,
            6, 7, 8,
        ];
        uint[] vertexOutcodes =
        [
            0x01u, 0x11u, 0x21u,
            0x01u, 0x02u, 0x00u,
            0x00u, 0x00u, 0x00u,
        ];

        uint count = CountWoWTrianglesPassingAabbOutcodeReject(
            triangleIndices,
            triangleIndices.Length,
            vertexOutcodes,
            vertexOutcodes.Length);

        Assert.Equal(2u, count);
    }

    [Fact]
    public void CountWoWTrianglesPassingAabbOutcodeReject_ReturnsZeroWhenAllTrianglesShareOutsideBits()
    {
        ushort[] triangleIndices =
        [
            0, 1, 2,
            3, 4, 5,
        ];
        uint[] vertexOutcodes =
        [
            0x04u, 0x14u, 0x24u,
            0x08u, 0x18u, 0x28u,
        ];

        uint count = CountWoWTrianglesPassingAabbOutcodeReject(
            triangleIndices,
            triangleIndices.Length,
            vertexOutcodes,
            vertexOutcodes.Length);

        Assert.Equal(0u, count);
    }
}
