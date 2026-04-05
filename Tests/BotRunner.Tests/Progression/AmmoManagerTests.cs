using BotRunner.Tasks.Progression;
using GameData.Core.Models;

namespace BotRunner.Tests.Progression;

public class AmmoManagerTests
{
    [Theory]
    [InlineData(1, false, 2512u)]    // Rough Arrow
    [InlineData(10, false, 2515u)]   // Sharp Arrow
    [InlineData(25, false, 3030u)]   // Razor Arrow
    [InlineData(40, false, 11285u)]  // Jagged Arrow
    [InlineData(55, false, 18042u)]  // Thorium Headed Arrow
    [InlineData(1, true, 2516u)]     // Light Shot
    [InlineData(10, true, 2519u)]    // Heavy Shot
    [InlineData(25, true, 3033u)]    // Solid Shot
    [InlineData(40, true, 11284u)]   // Hi-Impact Mithril Slugs
    [InlineData(55, true, 15997u)]   // Thorium Shells
    public void GetBestAmmoForLevel_ReturnsCorrectTier(int level, bool useBullets, uint expectedId)
    {
        Assert.Equal(expectedId, AmmoManager.GetBestAmmoForLevel(level, useBullets));
    }

    [Fact]
    public void GetNearestAmmoVendor_ReturnsClosest()
    {
        // Position near Orgrimmar vendor (2109, -4636, 48)
        var playerPos = new Position(2100f, -4630f, 48f);
        var nearest = AmmoManager.GetNearestAmmoVendor(playerPos, isHorde: true);

        Assert.NotNull(nearest);
        // Should be the Orgrimmar vendor, not Crossroads
        Assert.True(nearest!.DistanceTo(playerPos) < 50f,
            "Nearest vendor should be within 50 yards of test position");
    }

    [Fact]
    public void GetNearestAmmoVendor_ReturnsAllianceVendor()
    {
        // Position near Stormwind vendor
        var playerPos = new Position(-8413f, 541f, 91f);
        var nearest = AmmoManager.GetNearestAmmoVendor(playerPos, isHorde: false);

        Assert.NotNull(nearest);
        Assert.True(nearest!.DistanceTo(playerPos) < 10f);
    }

    [Fact]
    public void ArrowAndBulletTables_HaveSameTierCount()
    {
        Assert.Equal(AmmoManager.ArrowIds.Count, AmmoManager.BulletIds.Count);
    }
}
