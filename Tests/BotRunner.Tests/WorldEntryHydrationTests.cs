using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;

namespace BotRunner.Tests;

public class WorldEntryHydrationTests
{
    [Fact]
    public void IsReadyForWorldInteraction_ReturnsFalse_WhenPlayerIsNull()
    {
        Assert.False(WorldEntryHydration.IsReadyForWorldInteraction(null));
    }

    [Fact]
    public void IsReadyForWorldInteraction_ReturnsFalse_WhenPlayerHasNoHydratedStats()
    {
        var player = new Mock<IWoWLocalPlayer>();
        player.SetupGet(x => x.Guid).Returns(0x1234);
        player.SetupGet(x => x.Position).Returns(new Position(1, 2, 3));
        player.SetupGet(x => x.MaxHealth).Returns(0u);

        Assert.False(WorldEntryHydration.IsReadyForWorldInteraction(player.Object));
    }

    [Fact]
    public void IsReadyForWorldInteraction_ReturnsTrue_WhenPlayerHasGuidPositionAndMaxHealth()
    {
        var player = new Mock<IWoWLocalPlayer>();
        player.SetupGet(x => x.Guid).Returns(0x1234);
        player.SetupGet(x => x.Position).Returns(new Position(1, 2, 3));
        player.SetupGet(x => x.MaxHealth).Returns(100u);

        Assert.True(WorldEntryHydration.IsReadyForWorldInteraction(player.Object));
    }
}
