using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceScanWindowTests
{
    [Fact]
    public void BuildWoWSelectorSourceScanWindow_ClampsToLocalEightByEightWindow()
    {
        bool built = BuildWoWSelectorSourceScanWindow(
            cellRowIndex: 2,
            cellColumnIndex: 1,
            queryRowMin: 15,
            queryColumnMin: 7,
            queryRowMax: 25,
            queryColumnMax: 18,
            out SelectorSourceScanWindow window);

        Assert.True(built);
        Assert.Equal(0, window.RowMin);
        Assert.Equal(0, window.ColumnMin);
        Assert.Equal(7, window.RowMax);
        Assert.Equal(7, window.ColumnMax);
        Assert.Equal(0, window.PointStartIndex);
        Assert.Equal(9, window.RowAdvancePointCount);
    }

    [Fact]
    public void BuildWoWSelectorSourceScanWindow_ComputesStartIndexAndRowAdvanceFromBinaryGridWidth()
    {
        bool built = BuildWoWSelectorSourceScanWindow(
            cellRowIndex: 3,
            cellColumnIndex: 4,
            queryRowMin: 26,
            queryColumnMin: 35,
            queryRowMax: 28,
            queryColumnMax: 37,
            out SelectorSourceScanWindow window);

        Assert.True(built);
        Assert.Equal(2, window.RowMin);
        Assert.Equal(3, window.ColumnMin);
        Assert.Equal(4, window.RowMax);
        Assert.Equal(5, window.ColumnMax);
        Assert.Equal(37, window.PointStartIndex);
        Assert.Equal(14, window.RowAdvancePointCount);
    }

    [Fact]
    public void BuildWoWSelectorSourceScanWindow_WhenWindowDoesNotOverlapCell_ReturnsFalse()
    {
        bool built = BuildWoWSelectorSourceScanWindow(
            cellRowIndex: 2,
            cellColumnIndex: 2,
            queryRowMin: 0,
            queryColumnMin: 0,
            queryRowMax: 7,
            queryColumnMax: 7,
            out _);

        Assert.False(built);
    }
}
