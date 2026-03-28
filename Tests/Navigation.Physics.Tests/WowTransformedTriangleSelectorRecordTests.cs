using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTransformedTriangleSelectorRecordTests
{
    [Fact]
    public void BuildWoWTransformedTriangleSelectorRecord_NormalizesBasisRowsBeforeTransformingNormal()
    {
        Vector3[] basisRows =
        [
            new Vector3(2f, 0f, 0f),
            new Vector3(0f, 3f, 0f),
            new Vector3(0f, 0f, 4f),
        ];
        Vector3 localNormal = new(1f, 2f, 3f);
        Vector3 point0 = new(10f, 20f, 30f);
        Vector3 point1 = new(11f, 20f, 30f);
        Vector3 point2 = new(10f, 21f, 30f);

        bool built = BuildWoWTransformedTriangleSelectorRecord(
            basisRows,
            basisRows.Length,
            localNormal,
            point0,
            point1,
            point2,
            out SelectorCandidateRecord record);

        Assert.True(built);
        AssertVector(new Vector3(1f, 2f, 3f), record.FilterPlane.Normal);
        Assert.Equal(-140f, record.FilterPlane.PlaneDistance, 6);
        AssertVector(point0, record.Point0);
        AssertVector(point1, record.Point1);
        AssertVector(point2, record.Point2);
    }

    [Fact]
    public void BuildWoWTransformedTriangleSelectorRecord_PermutesNormalThroughBasisRows()
    {
        Vector3[] basisRows =
        [
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 0f),
        ];
        Vector3 localNormal = new(1f, 2f, 3f);
        Vector3 point0 = new(4f, 5f, 6f);

        bool built = BuildWoWTransformedTriangleSelectorRecord(
            basisRows,
            basisRows.Length,
            localNormal,
            point0,
            new Vector3(7f, 8f, 9f),
            new Vector3(10f, 11f, 12f),
            out SelectorCandidateRecord record);

        Assert.True(built);
        AssertVector(new Vector3(3f, 1f, 2f), record.FilterPlane.Normal);
        Assert.Equal(-29f, record.FilterPlane.PlaneDistance, 6);
    }

    [Fact]
    public void BuildWoWTransformedTriangleSelectorRecord_LeavesZeroLengthBasisRowsUnchanged()
    {
        Vector3[] basisRows =
        [
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 5f, 0f),
            new Vector3(0f, 0f, 7f),
        ];
        Vector3 localNormal = new(9f, 1f, 2f);
        Vector3 point0 = new(1f, 2f, 3f);

        bool built = BuildWoWTransformedTriangleSelectorRecord(
            basisRows,
            basisRows.Length,
            localNormal,
            point0,
            new Vector3(2f, 3f, 4f),
            new Vector3(3f, 4f, 5f),
            out SelectorCandidateRecord record);

        Assert.True(built);
        AssertVector(new Vector3(0f, 1f, 2f), record.FilterPlane.Normal);
        Assert.Equal(-8f, record.FilterPlane.PlaneDistance, 6);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }
}
