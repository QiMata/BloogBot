using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceSubcellMaskTests
{
    [Theory]
    [InlineData(0u, 0u, 0x0001u)]
    [InlineData(0u, 1u, 0x0001u)]
    [InlineData(1u, 0u, 0x0001u)]
    [InlineData(1u, 1u, 0x0001u)]
    [InlineData(0u, 2u, 0x0002u)]
    [InlineData(2u, 0u, 0x0010u)]
    [InlineData(6u, 3u, 0x2000u)]
    [InlineData(7u, 7u, 0x8000u)]
    public void BuildWoWSelectorSourceSubcellMask_Maps2x2SubcellsToBinaryBitTable(uint rowIndex, uint columnIndex, uint expectedMask)
    {
        uint actualMask = BuildWoWSelectorSourceSubcellMask(rowIndex, columnIndex);

        Assert.Equal(expectedMask, actualMask);
    }

    [Fact]
    public void EvaluateWoWSelectorSourceSubcellMask_UsesComputedSubcellBit()
    {
        Assert.True(EvaluateWoWSelectorSourceSubcellMask(6u, 3u, 0x2000u));
        Assert.False(EvaluateWoWSelectorSourceSubcellMask(6u, 3u, 0x1000u));
    }
}
