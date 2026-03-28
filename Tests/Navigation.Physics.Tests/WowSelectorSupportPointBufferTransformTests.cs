using Xunit;

using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSupportPointBufferTransformTests
{
    [Fact]
    public void TransformWoWSelectorSupportPointBuffer_UsesRowVectorBasisAndTranslation()
    {
        Vector3[] inputPoints =
        [
            new Vector3(1.0f, 2.0f, 3.0f),
            new Vector3(-4.0f, 5.0f, -6.0f),
            new Vector3(0.5f, -1.5f, 2.5f),
        ];
        Vector3 basisRow0 = new(0.0f, 1.0f, 0.0f);
        Vector3 basisRow1 = new(-1.0f, 0.0f, 0.0f);
        Vector3 basisRow2 = new(0.0f, 0.0f, 1.0f);
        Vector3 translation = new(10.0f, 20.0f, 30.0f);
        Vector3[] outputPoints = new Vector3[inputPoints.Length];

        Assert.True(TransformWoWSelectorSupportPointBuffer(
            inputPoints,
            inputPoints.Length,
            basisRow0,
            basisRow1,
            basisRow2,
            translation,
            outputPoints,
            outputPoints.Length));

        AssertVectorEqual(new Vector3(8.0f, 21.0f, 33.0f), outputPoints[0]);
        AssertVectorEqual(new Vector3(5.0f, 16.0f, 24.0f), outputPoints[1]);
        AssertVectorEqual(new Vector3(11.5f, 20.5f, 32.5f), outputPoints[2]);
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }
}
