using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowLocalBoundsProducerTests
{
    [Fact]
    public void BuildWoWObjectLocalQueryBounds_SubtractsObjectPositionFromBothCorners()
    {
        Vector3 worldBoundsMin = new(10f, 20f, 30f);
        Vector3 worldBoundsMax = new(40f, 50f, 60f);
        Vector3 objectPosition = new(1.5f, -2f, 3.25f);

        bool built = BuildWoWObjectLocalQueryBounds(
            worldBoundsMin,
            worldBoundsMax,
            objectPosition,
            out Vector3 localBoundsMin,
            out Vector3 localBoundsMax);

        Assert.True(built);
        Assert.Equal(8.5f, localBoundsMin.X);
        Assert.Equal(22f, localBoundsMin.Y);
        Assert.Equal(26.75f, localBoundsMin.Z);
        Assert.Equal(38.5f, localBoundsMax.X);
        Assert.Equal(52f, localBoundsMax.Y);
        Assert.Equal(56.75f, localBoundsMax.Z);
    }

    [Fact]
    public void BuildWoWLocalBoundsAabbOutcode_UsesPositiveBinaryToleranceOnBothSides()
    {
        Vector3 localBoundsMin = new(1f, 2f, 3f);
        Vector3 localBoundsMax = new(4f, 5f, 6f);

        Assert.Equal(0u, BuildWoWLocalBoundsAabbOutcode(localBoundsMin, localBoundsMax, new Vector3(0.985f, 2f, 3f)));
        Assert.Equal(0x01u, BuildWoWLocalBoundsAabbOutcode(localBoundsMin, localBoundsMax, new Vector3(0.97f, 2f, 3f)));
        Assert.Equal(0u, BuildWoWLocalBoundsAabbOutcode(localBoundsMin, localBoundsMax, new Vector3(4.015f, 5f, 6f)));
        Assert.Equal(0x02u, BuildWoWLocalBoundsAabbOutcode(localBoundsMin, localBoundsMax, new Vector3(4.03f, 5f, 6f)));
        Assert.Equal(0x28u, BuildWoWLocalBoundsAabbOutcode(localBoundsMin, localBoundsMax, new Vector3(2f, 5.03f, 6.03f)));
    }

    [Fact]
    public void BuildWoWLocalBoundsScanWindowCandidateRecords_UsesLocalBoundsOutcodesBeforeAppendingFixedTriangles()
    {
        Vector3[] pointGrid = BuildPointGrid();
        SelectorSourceScanWindow scanWindow = new()
        {
            RowMin = 0,
            ColumnMin = 0,
            RowMax = 0,
            ColumnMax = 0,
            PointStartIndex = 0,
            RowAdvancePointCount = 16,
        };

        Vector3 localBoundsMin = new(-1f, -1f, -1f);
        Vector3 localBoundsMax = new(10f, 10f, 0.30f);
        Vector3 translation = new(1f, 2f, 3f);
        SelectorCandidateRecord[] records = new SelectorCandidateRecord[4];

        int count = BuildWoWLocalBoundsScanWindowCandidateRecords(
            localBoundsMin,
            localBoundsMax,
            pointGrid,
            pointGrid.Length,
            scanWindow,
            0u,
            translation,
            false,
            records,
            records.Length);

        Assert.Equal(3, count);
        AssertRecordMatchesFallbackPlane(records[0], pointGrid[17] + translation, pointGrid[9] + translation, pointGrid[0] + translation);
        AssertRecordMatchesFallbackPlane(records[1], pointGrid[9] + translation, pointGrid[1] + translation, pointGrid[0] + translation);
        AssertRecordMatchesFallbackPlane(records[2], pointGrid[9] + translation, pointGrid[18] + translation, pointGrid[1] + translation);
    }

    private static Vector3[] BuildPointGrid()
    {
        Vector3[] pointGrid = new Vector3[17 * 17];
        for (int row = 0; row < 17; ++row) {
            for (int column = 0; column < 17; ++column) {
                int index = (row * 17) + column;
                pointGrid[index] = new Vector3(
                    (column * 0.5f) + (row * 0.03125f),
                    (row * 0.375f) - (column * 0.0625f),
                    0.25f + (row * 0.125f) + (column * 0.03125f) + (column * column * 0.001953125f));
            }
        }

        return pointGrid;
    }

    private static void AssertRecordMatchesFallbackPlane(SelectorCandidateRecord record, Vector3 expectedPoint0, Vector3 expectedPoint1, Vector3 expectedPoint2)
    {
        Assert.Equal(expectedPoint0.X, record.Point0.X);
        Assert.Equal(expectedPoint0.Y, record.Point0.Y);
        Assert.Equal(expectedPoint0.Z, record.Point0.Z);
        Assert.Equal(expectedPoint1.X, record.Point1.X);
        Assert.Equal(expectedPoint1.Y, record.Point1.Y);
        Assert.Equal(expectedPoint1.Z, record.Point1.Z);
        Assert.Equal(expectedPoint2.X, record.Point2.X);
        Assert.Equal(expectedPoint2.Y, record.Point2.Y);
        Assert.Equal(expectedPoint2.Z, record.Point2.Z);

        Assert.True(BuildWoWPlaneFromTrianglePoints(expectedPoint0, expectedPoint1, expectedPoint2, out SelectorSupportPlane plane));
        Assert.Equal(plane.Normal.X, record.FilterPlane.Normal.X);
        Assert.Equal(plane.Normal.Y, record.FilterPlane.Normal.Y);
        Assert.Equal(plane.Normal.Z, record.FilterPlane.Normal.Z);
        Assert.Equal(plane.PlaneDistance, record.FilterPlane.PlaneDistance);
    }
}
