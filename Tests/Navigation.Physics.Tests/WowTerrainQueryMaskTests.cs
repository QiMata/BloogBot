using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTerrainQueryMaskTests
{
    private const uint BaseMaskModelTrue = 0x00100111u;
    private const uint BaseMaskModelFalse = 0x00102111u;
    private const uint WaterwalkAugment = 0x00030000u;
    private const uint TreeAugment = 0x00008000u;
    private const uint MoveFlagSwimming = 0x00200000u;
    private const uint MoveFlagWaterWalking = 0x10000000u;
    private const float Field20Threshold = -0.6457718014717102f; // 0x80DFE8

    [Fact]
    public void TerrainQueryMask_UsesModelPropertyBaseMask_When5FA550ReturnsTrue()
    {
        uint mask = EvaluateWoWTerrainQueryMask(
            modelPropertyFlagSet: true,
            movementFlags: 0u,
            field20Value: Field20Threshold,
            rootTreeFlagSet: false,
            childTreeFlagSet: false);

        Assert.Equal(BaseMaskModelTrue, mask);
    }

    [Fact]
    public void TerrainQueryMask_UsesAlternateBaseMask_When5FA550ReturnsFalse()
    {
        uint mask = EvaluateWoWTerrainQueryMask(
            modelPropertyFlagSet: false,
            movementFlags: 0u,
            field20Value: Field20Threshold,
            rootTreeFlagSet: false,
            childTreeFlagSet: false);

        Assert.Equal(BaseMaskModelFalse, mask);
    }

    [Fact]
    public void TerrainQueryMask_AddsWaterwalkAugment_OnlyAboveStrictField20Threshold()
    {
        uint mask = EvaluateWoWTerrainQueryMask(
            modelPropertyFlagSet: true,
            movementFlags: MoveFlagWaterWalking,
            field20Value: Field20Threshold + 0.0001f,
            rootTreeFlagSet: false,
            childTreeFlagSet: false);

        Assert.Equal(BaseMaskModelTrue | WaterwalkAugment, mask);
    }

    [Fact]
    public void TerrainQueryMask_DoesNotAddWaterwalkAugment_AtEqualThresholdOrWhileSwimming()
    {
        uint equalThresholdMask = EvaluateWoWTerrainQueryMask(
            modelPropertyFlagSet: true,
            movementFlags: MoveFlagWaterWalking,
            field20Value: Field20Threshold,
            rootTreeFlagSet: false,
            childTreeFlagSet: false);

        uint swimmingMask = EvaluateWoWTerrainQueryMask(
            modelPropertyFlagSet: true,
            movementFlags: MoveFlagWaterWalking | MoveFlagSwimming,
            field20Value: Field20Threshold + 1.0f,
            rootTreeFlagSet: false,
            childTreeFlagSet: false);

        Assert.Equal(BaseMaskModelTrue, equalThresholdMask);
        Assert.Equal(BaseMaskModelTrue, swimmingMask);
    }

    [Fact]
    public void TerrainQueryMask_AddsTreeAugment_OnlyWhenBothTreeBitsAreSet()
    {
        uint singleTreeMask = EvaluateWoWTerrainQueryMask(
            modelPropertyFlagSet: false,
            movementFlags: 0u,
            field20Value: Field20Threshold,
            rootTreeFlagSet: true,
            childTreeFlagSet: false);

        uint dualTreeMask = EvaluateWoWTerrainQueryMask(
            modelPropertyFlagSet: false,
            movementFlags: 0u,
            field20Value: Field20Threshold,
            rootTreeFlagSet: true,
            childTreeFlagSet: true);

        Assert.Equal(BaseMaskModelFalse, singleTreeMask);
        Assert.Equal(BaseMaskModelFalse | TreeAugment, dualTreeMask);
    }
}
