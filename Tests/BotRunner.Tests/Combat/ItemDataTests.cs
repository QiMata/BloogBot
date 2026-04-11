using GameData.Core.Constants;

namespace BotRunner.Tests.Combat;

public class ItemDataTests
{
    [Fact]
    public void MountItemIds_DbDerivedCatalog_HasExpectedCount()
    {
        Assert.Equal(125, ItemData.MountItemIds.Count);
    }

    [Theory]
    [InlineData(2411u)]  // Black Stallion Bridle
    [InlineData(19029u)] // Horn of the Frostwolf Howler
    [InlineData(23720u)] // Riding Turtle
    public void IsMountItem_DbDerivedCatalog_ReturnsTrue(uint itemId)
    {
        Assert.True(ItemData.IsMountItem(itemId));
    }

    [Theory]
    [InlineData(117u)]  // Tough Jerky
    [InlineData(6948u)] // Hearthstone
    public void IsMountItem_NonMountItem_ReturnsFalse(uint itemId)
    {
        Assert.False(ItemData.IsMountItem(itemId));
    }
}
