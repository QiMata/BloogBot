using BotRunner.Combat;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;

namespace BotRunner.Tests.Combat;

/// <summary>
/// Tests for ConsumableData.FindUsableScrolls, FindUsableElixirs, FindUsableBuffFood.
/// These methods scan inventory for consumable buff items the player doesn't already have.
/// </summary>
public class FindUsableScrollsTests
{
    private Mock<IObjectManager> CreateOmWithItems(params (int bag, int slot, string name)[] items)
    {
        var om = new Mock<IObjectManager>();
        var player = new Mock<IWoWLocalPlayer>();
        player.Setup(p => p.HasBuff(It.IsAny<string>())).Returns(false);
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>())).Returns((IWoWItem?)null);

        foreach (var (bag, slot, name) in items)
        {
            var item = new Mock<IWoWItem>();
            item.Setup(i => i.Name).Returns(name);
            item.Setup(i => i.Info).Returns((ItemCacheInfo?)null);
            om.Setup(o => o.GetContainedItem(bag, slot)).Returns(item.Object);
        }

        return om;
    }

    [Fact]
    public void NullPlayer_ReturnsEmptyList()
    {
        var om = new Mock<IObjectManager>();
        om.Setup(o => o.Player).Returns((IWoWLocalPlayer?)null);

        Assert.Empty(ConsumableData.FindUsableScrolls(om.Object));
    }

    [Fact]
    public void EmptyInventory_ReturnsEmptyList()
    {
        var om = CreateOmWithItems();

        Assert.Empty(ConsumableData.FindUsableScrolls(om.Object));
    }

    [Fact]
    public void SingleScroll_ReturnsIt()
    {
        var om = CreateOmWithItems((0, 0, "Scroll of Agility III"));
        var result = ConsumableData.FindUsableScrolls(om.Object);

        Assert.Single(result);
    }

    [Fact]
    public void AlreadyBuffed_ReturnsEmpty()
    {
        var om = new Mock<IObjectManager>();
        var player = new Mock<IWoWLocalPlayer>();
        player.Setup(p => p.HasBuff("Agility")).Returns(true);
        om.Setup(o => o.Player).Returns(player.Object);

        var item = new Mock<IWoWItem>();
        item.Setup(i => i.Name).Returns("Scroll of Agility III");
        item.Setup(i => i.Info).Returns((ItemCacheInfo?)null);
        om.Setup(o => o.GetContainedItem(0, 0)).Returns(item.Object);
        om.Setup(o => o.GetContainedItem(It.Is<int>(b => b != 0), It.IsAny<int>())).Returns((IWoWItem?)null);
        om.Setup(o => o.GetContainedItem(0, It.Is<int>(s => s != 0))).Returns((IWoWItem?)null);

        var result = ConsumableData.FindUsableScrolls(om.Object);
        Assert.Empty(result);
    }

    [Fact]
    public void MultipleScrollTypes_ReturnsOneEach()
    {
        var om = CreateOmWithItems(
            (0, 0, "Scroll of Agility III"),
            (0, 1, "Scroll of Strength II"));

        var result = ConsumableData.FindUsableScrolls(om.Object);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SameBuffType_KeepsHigherRank()
    {
        // Two Agility scrolls: the one with the "higher" ordinal name should win
        var om = CreateOmWithItems(
            (0, 0, "Scroll of Agility I"),
            (0, 1, "Scroll of Agility IV"));

        var result = ConsumableData.FindUsableScrolls(om.Object);
        Assert.Single(result);
    }

    [Fact]
    public void NonScrollItem_Ignored()
    {
        var om = CreateOmWithItems((0, 0, "Linen Bandage"));

        Assert.Empty(ConsumableData.FindUsableScrolls(om.Object));
    }

    [Theory]
    [InlineData("Scroll of Agility")]
    [InlineData("Scroll of Intellect")]
    [InlineData("Scroll of Protection")]
    [InlineData("Scroll of Spirit")]
    [InlineData("Scroll of Stamina")]
    [InlineData("Scroll of Strength")]
    public void AllScrollTypes_Recognized(string scrollPrefix)
    {
        var om = CreateOmWithItems((0, 0, scrollPrefix + " V"));
        var result = ConsumableData.FindUsableScrolls(om.Object);
        Assert.Single(result);
    }
}

public class FindUsableElixirsTests
{
    private Mock<IObjectManager> CreateOmWithItems(params (int bag, int slot, string name)[] items)
    {
        var om = new Mock<IObjectManager>();
        var player = new Mock<IWoWLocalPlayer>();
        player.Setup(p => p.HasBuff(It.IsAny<string>())).Returns(false);
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>())).Returns((IWoWItem?)null);

        foreach (var (bag, slot, name) in items)
        {
            var item = new Mock<IWoWItem>();
            item.Setup(i => i.Name).Returns(name);
            item.Setup(i => i.Info).Returns((ItemCacheInfo?)null);
            om.Setup(o => o.GetContainedItem(bag, slot)).Returns(item.Object);
        }

        return om;
    }

    [Fact]
    public void NullPlayer_ReturnsEmptyList()
    {
        var om = new Mock<IObjectManager>();
        om.Setup(o => o.Player).Returns((IWoWLocalPlayer?)null);

        Assert.Empty(ConsumableData.FindUsableElixirs(om.Object));
    }

    [Fact]
    public void EmptyInventory_ReturnsEmptyList()
    {
        var om = CreateOmWithItems();

        Assert.Empty(ConsumableData.FindUsableElixirs(om.Object));
    }

    [Fact]
    public void BattleElixir_ReturnsSingle()
    {
        var om = CreateOmWithItems((0, 0, "Elixir of the Mongoose"));
        var result = ConsumableData.FindUsableElixirs(om.Object);

        Assert.Single(result);
    }

    [Fact]
    public void GuardianElixir_ReturnsSingle()
    {
        var om = CreateOmWithItems((0, 0, "Elixir of Superior Defense"));
        var result = ConsumableData.FindUsableElixirs(om.Object);

        Assert.Single(result);
    }

    [Fact]
    public void BattleAndGuardian_ReturnsBoth()
    {
        var om = CreateOmWithItems(
            (0, 0, "Elixir of the Mongoose"),
            (0, 1, "Elixir of Superior Defense"));
        var result = ConsumableData.FindUsableElixirs(om.Object);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AlreadyBuffed_SkipsElixir()
    {
        var om = new Mock<IObjectManager>();
        var player = new Mock<IWoWLocalPlayer>();
        player.Setup(p => p.HasBuff("Elixir of the Mongoose")).Returns(true);
        om.Setup(o => o.Player).Returns(player.Object);

        var item = new Mock<IWoWItem>();
        item.Setup(i => i.Name).Returns("Elixir of the Mongoose");
        item.Setup(i => i.Info).Returns((ItemCacheInfo?)null);
        om.Setup(o => o.GetContainedItem(0, 0)).Returns(item.Object);
        om.Setup(o => o.GetContainedItem(It.Is<int>(b => b != 0), It.IsAny<int>())).Returns((IWoWItem?)null);
        om.Setup(o => o.GetContainedItem(0, It.Is<int>(s => s != 0))).Returns((IWoWItem?)null);

        var result = ConsumableData.FindUsableElixirs(om.Object);
        Assert.Empty(result);
    }

    [Fact]
    public void TwoBattleElixirs_KeepsBestPriority()
    {
        // Mongoose is higher priority than Giants (earlier in list)
        var om = CreateOmWithItems(
            (0, 0, "Elixir of the Giants"),
            (0, 1, "Elixir of the Mongoose"));
        var result = ConsumableData.FindUsableElixirs(om.Object);

        // Should return 1 battle elixir (the best one)
        Assert.Single(result);
    }

    [Fact]
    public void NonElixirItem_Ignored()
    {
        var om = CreateOmWithItems((0, 0, "Greater Healing Potion"));

        Assert.Empty(ConsumableData.FindUsableElixirs(om.Object));
    }
}

public class FindUsableBuffFoodTests
{
    private Mock<IObjectManager> CreateOmWithItems(bool alreadyWellFed, params (int bag, int slot, string name)[] items)
    {
        var om = new Mock<IObjectManager>();
        var player = new Mock<IWoWLocalPlayer>();
        player.Setup(p => p.HasBuff("Well Fed")).Returns(alreadyWellFed);
        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>())).Returns((IWoWItem?)null);

        foreach (var (bag, slot, name) in items)
        {
            var item = new Mock<IWoWItem>();
            item.Setup(i => i.Name).Returns(name);
            item.Setup(i => i.Info).Returns((ItemCacheInfo?)null);
            om.Setup(o => o.GetContainedItem(bag, slot)).Returns(item.Object);
        }

        return om;
    }

    [Fact]
    public void NullPlayer_ReturnsNull()
    {
        var om = new Mock<IObjectManager>();
        om.Setup(o => o.Player).Returns((IWoWLocalPlayer?)null);

        Assert.Null(ConsumableData.FindUsableBuffFood(om.Object));
    }

    [Fact]
    public void AlreadyWellFed_ReturnsNull()
    {
        var om = CreateOmWithItems(alreadyWellFed: true,
            (0, 0, "Grilled Squid"));

        Assert.Null(ConsumableData.FindUsableBuffFood(om.Object));
    }

    [Fact]
    public void EmptyInventory_ReturnsNull()
    {
        var om = CreateOmWithItems(alreadyWellFed: false);

        Assert.Null(ConsumableData.FindUsableBuffFood(om.Object));
    }

    [Theory]
    [InlineData("Grilled Squid")]
    [InlineData("Nightfin Soup")]
    [InlineData("Monster Omelet")]
    [InlineData("Tender Wolf Steak")]
    [InlineData("Runn Tum Tuber Surprise")]
    [InlineData("Herb Baked Egg")]
    [InlineData("Spiced Wolf Meat")]
    public void KnownBuffFood_ReturnsItem(string foodName)
    {
        var om = CreateOmWithItems(alreadyWellFed: false,
            (0, 0, foodName));

        Assert.NotNull(ConsumableData.FindUsableBuffFood(om.Object));
    }

    [Fact]
    public void NonBuffFood_ReturnsNull()
    {
        var om = CreateOmWithItems(alreadyWellFed: false,
            (0, 0, "Tough Jerky")); // regular food, not buff food

        Assert.Null(ConsumableData.FindUsableBuffFood(om.Object));
    }

    [Fact]
    public void CaseInsensitive_Match()
    {
        var om = CreateOmWithItems(alreadyWellFed: false,
            (0, 0, "grilled squid"));

        Assert.NotNull(ConsumableData.FindUsableBuffFood(om.Object));
    }

    [Fact]
    public void MultipleBuffFoods_ReturnsFirst()
    {
        var om = CreateOmWithItems(alreadyWellFed: false,
            (0, 0, "Grilled Squid"),
            (0, 1, "Nightfin Soup"));

        var result = ConsumableData.FindUsableBuffFood(om.Object);
        Assert.NotNull(result);
    }
}
