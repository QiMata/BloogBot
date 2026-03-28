using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceScanWindowRecordTests
{
    [Fact]
    public void BuildWoWSelectorSourceScanWindowCandidateRecords_SkipsMaskedSubcells()
    {
        Vector3[] pointGrid = BuildPointGrid();
        SelectorSourceScanWindow scanWindow = new()
        {
            RowMin = 0,
            ColumnMin = 0,
            RowMax = 0,
            ColumnMax = 2,
            PointStartIndex = 0,
            RowAdvancePointCount = 14,
        };

        Vector3 translation = new(1f, 2f, 3f);
        SelectorCandidateRecord[] records = new SelectorCandidateRecord[8];
        int count = BuildWoWSelectorSourceScanWindowCandidateRecords(
            Array.Empty<SelectorSupportPlane>(),
            0,
            pointGrid,
            pointGrid.Length,
            scanWindow,
            0x0001u,
            translation,
            false,
            records,
            records.Length);

        SelectorCandidateRecord[] expected = new SelectorCandidateRecord[4];
        int expectedCount = BuildWoWSelectorSourceTriangleCandidateRecords(
            Array.Empty<SelectorSupportPlane>(),
            0,
            pointGrid[2..],
            pointGrid.Length - 2,
            translation,
            false,
            expected,
            expected.Length);

        Assert.Equal(expectedCount, count);
        AssertRecordsEqual(expected, expectedCount, records);
    }

    [Fact]
    public void BuildWoWSelectorSourceScanWindowCandidateRecords_AdvancesOneSeventeenWideRowAtATime()
    {
        Vector3[] pointGrid = BuildPointGrid();
        SelectorSourceScanWindow scanWindow = new()
        {
            RowMin = 0,
            ColumnMin = 0,
            RowMax = 1,
            ColumnMax = 0,
            PointStartIndex = 0,
            RowAdvancePointCount = 16,
        };

        Vector3 translation = new(-2f, 0.5f, 4f);
        SelectorCandidateRecord[] records = new SelectorCandidateRecord[8];
        int count = BuildWoWSelectorSourceScanWindowCandidateRecords(
            Array.Empty<SelectorSupportPlane>(),
            0,
            pointGrid,
            pointGrid.Length,
            scanWindow,
            0u,
            translation,
            false,
            records,
            records.Length);

        SelectorCandidateRecord[] firstRow = new SelectorCandidateRecord[4];
        SelectorCandidateRecord[] secondRow = new SelectorCandidateRecord[4];
        int firstCount = BuildWoWSelectorSourceTriangleCandidateRecords(
            Array.Empty<SelectorSupportPlane>(),
            0,
            pointGrid,
            pointGrid.Length,
            translation,
            false,
            firstRow,
            firstRow.Length);
        int secondCount = BuildWoWSelectorSourceTriangleCandidateRecords(
            Array.Empty<SelectorSupportPlane>(),
            0,
            pointGrid[17..],
            pointGrid.Length - 17,
            translation,
            false,
            secondRow,
            secondRow.Length);

        Assert.Equal(firstCount + secondCount, count);
        AssertRecordsEqual(firstRow, firstCount, records, 0);
        AssertRecordsEqual(secondRow, secondCount, records, firstCount);
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
                    0.25f + (row * 0.125f) + (column * 0.03125f));
            }
        }

        return pointGrid;
    }

    private static void AssertRecordsEqual(SelectorCandidateRecord[] expected, int expectedCount, SelectorCandidateRecord[] actual, int actualOffset = 0)
    {
        for (int i = 0; i < expectedCount; ++i) {
            int actualIndex = actualOffset + i;
            Assert.Equal(expected[i].FilterPlane.Normal.X, actual[actualIndex].FilterPlane.Normal.X);
            Assert.Equal(expected[i].FilterPlane.Normal.Y, actual[actualIndex].FilterPlane.Normal.Y);
            Assert.Equal(expected[i].FilterPlane.Normal.Z, actual[actualIndex].FilterPlane.Normal.Z);
            Assert.Equal(expected[i].FilterPlane.PlaneDistance, actual[actualIndex].FilterPlane.PlaneDistance);
            Assert.Equal(expected[i].Point0.X, actual[actualIndex].Point0.X);
            Assert.Equal(expected[i].Point0.Y, actual[actualIndex].Point0.Y);
            Assert.Equal(expected[i].Point0.Z, actual[actualIndex].Point0.Z);
            Assert.Equal(expected[i].Point1.X, actual[actualIndex].Point1.X);
            Assert.Equal(expected[i].Point1.Y, actual[actualIndex].Point1.Y);
            Assert.Equal(expected[i].Point1.Z, actual[actualIndex].Point1.Z);
            Assert.Equal(expected[i].Point2.X, actual[actualIndex].Point2.X);
            Assert.Equal(expected[i].Point2.Y, actual[actualIndex].Point2.Y);
            Assert.Equal(expected[i].Point2.Z, actual[actualIndex].Point2.Z);
        }
    }
}
