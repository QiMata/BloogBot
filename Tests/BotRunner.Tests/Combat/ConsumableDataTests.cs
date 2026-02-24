using BotRunner.Combat;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Moq;

namespace BotRunner.Tests.Combat
{
    public class ConsumableDataTests
    {
        // ======== GetFoodItemId ========

        [Theory]
        [InlineData(1u, 117u)]     // Tough Jerky (level 1)
        [InlineData(4u, 117u)]     // Still Tough Jerky
        [InlineData(5u, 2287u)]    // Haunch of Meat (level 5)
        [InlineData(14u, 2287u)]   // Still Haunch
        [InlineData(15u, 3770u)]   // Mutton Chop (level 15)
        [InlineData(25u, 3771u)]   // Wild Hog Shank (level 25)
        [InlineData(35u, 8952u)]   // Roasted Quail (level 35)
        [InlineData(45u, 8950u)]   // Homemade Cherry Pie (level 45)
        [InlineData(60u, 8950u)]   // Max level still gets Cherry Pie
        public void GetFoodItemId_ReturnsCorrectTier(uint level, uint expectedId)
        {
            Assert.Equal(expectedId, ConsumableData.GetFoodItemId(level));
        }

        [Theory]
        [InlineData(1u, 159u)]     // Refreshing Spring Water (level 1)
        [InlineData(5u, 1179u)]    // Ice Cold Milk (level 5)
        [InlineData(15u, 1205u)]   // Melon Juice (level 15)
        [InlineData(25u, 1708u)]   // Sweet Nectar (level 25)
        [InlineData(35u, 1645u)]   // Moonberry Juice (level 35)
        [InlineData(45u, 8766u)]   // Morning Glory Dew (level 45)
        [InlineData(60u, 8766u)]   // Max level
        public void GetDrinkItemId_ReturnsCorrectTier(uint level, uint expectedId)
        {
            Assert.Equal(expectedId, ConsumableData.GetDrinkItemId(level));
        }

        // ======== AllFoodItemIds / AllDrinkItemIds / AllBandageItemIds ========

        [Fact]
        public void AllFoodItemIds_Contains6Tiers()
        {
            Assert.Equal(6, ConsumableData.AllFoodItemIds.Count);
            Assert.Contains(117u, ConsumableData.AllFoodItemIds);   // Tough Jerky
            Assert.Contains(8950u, ConsumableData.AllFoodItemIds);  // Cherry Pie
        }

        [Fact]
        public void AllDrinkItemIds_Contains6Tiers()
        {
            Assert.Equal(6, ConsumableData.AllDrinkItemIds.Count);
            Assert.Contains(159u, ConsumableData.AllDrinkItemIds);  // Spring Water
            Assert.Contains(8766u, ConsumableData.AllDrinkItemIds); // Morning Glory Dew
        }

        [Fact]
        public void AllBandageItemIds_Contains10Types()
        {
            Assert.Equal(10, ConsumableData.AllBandageItemIds.Count);
            Assert.Contains(1251u, ConsumableData.AllBandageItemIds);  // Linen Bandage
            Assert.Contains(14530u, ConsumableData.AllBandageItemIds); // Heavy Runecloth
        }

        // ======== CountFood / CountDrink ========

        [Fact]
        public void CountFood_EmptyInventory_ReturnsZero()
        {
            var om = new Mock<IObjectManager>();
            Assert.Equal(0, ConsumableData.CountFood(om.Object));
        }

        [Fact]
        public void CountFood_CountsStacksCorrectly()
        {
            var om = new Mock<IObjectManager>();
            var food1 = CreateMockItem(117u, stackCount: 5);   // 5x Tough Jerky
            var food2 = CreateMockItem(2287u, stackCount: 10); // 10x Haunch of Meat

            om.Setup(o => o.GetContainedItem(0, 0)).Returns(food1.Object);
            om.Setup(o => o.GetContainedItem(0, 5)).Returns(food2.Object);

            Assert.Equal(15, ConsumableData.CountFood(om.Object));
        }

        [Fact]
        public void CountDrink_EmptyInventory_ReturnsZero()
        {
            var om = new Mock<IObjectManager>();
            Assert.Equal(0, ConsumableData.CountDrink(om.Object));
        }

        // ======== FindBestFood / FindBestDrink / FindBestBandage ========

        [Fact]
        public void FindBestFood_EmptyInventory_ReturnsNull()
        {
            var om = new Mock<IObjectManager>();
            Assert.Null(ConsumableData.FindBestFood(om.Object));
        }

        [Fact]
        public void FindBestFood_ReturnsBestTier()
        {
            var om = new Mock<IObjectManager>();
            var jerky = CreateMockItem(117u);    // Tier 1 food
            var quail = CreateMockItem(8952u);   // Tier 4 food

            om.Setup(o => o.GetContainedItem(0, 0)).Returns(jerky.Object);
            om.Setup(o => o.GetContainedItem(0, 1)).Returns(quail.Object);

            var result = ConsumableData.FindBestFood(om.Object);
            Assert.NotNull(result);
            Assert.Equal(8952u, result.ItemId);  // Should pick the higher tier
        }

        [Fact]
        public void FindBestBandage_EmptyInventory_ReturnsNull()
        {
            var om = new Mock<IObjectManager>();
            Assert.Null(ConsumableData.FindBestBandage(om.Object));
        }

        // ======== FindHearthstone ========

        [Fact]
        public void FindHearthstone_NotInInventory_ReturnsNull()
        {
            var om = new Mock<IObjectManager>();
            Assert.Null(ConsumableData.FindHearthstone(om.Object));
        }

        [Fact]
        public void FindHearthstone_FindsInBackpack()
        {
            var om = new Mock<IObjectManager>();
            var hs = CreateMockItem(6948u); // Hearthstone item ID

            om.Setup(o => o.GetContainedItem(0, 10)).Returns(hs.Object);

            var result = ConsumableData.FindHearthstone(om.Object);
            Assert.NotNull(result);
            Assert.Equal(6948u, result.ItemId);
        }

        // ======== GetConsumablesToBuy ========

        [Fact]
        public void GetConsumablesToBuy_EmptyBags_BuysFoodAndDrink()
        {
            var om = new Mock<IObjectManager>();
            var result = ConsumableData.GetConsumablesToBuy(om.Object, 25, usesMana: true);

            // Should want to buy food and drink
            Assert.True(result.Count >= 2);
            Assert.Contains(ConsumableData.GetFoodItemId(25), result.Keys);
            Assert.Contains(ConsumableData.GetDrinkItemId(25), result.Keys);
        }

        [Fact]
        public void GetConsumablesToBuy_NoManaClass_SkipsDrink()
        {
            var om = new Mock<IObjectManager>();
            var result = ConsumableData.GetConsumablesToBuy(om.Object, 25, usesMana: false);

            Assert.Contains(ConsumableData.GetFoodItemId(25), result.Keys);
            Assert.DoesNotContain(ConsumableData.GetDrinkItemId(25), result.Keys);
        }

        // ======== IsLowOnConsumables ========

        [Fact]
        public void IsLowOnConsumables_EmptyInventory_ReturnsTrue()
        {
            var om = new Mock<IObjectManager>();
            Assert.True(ConsumableData.IsLowOnConsumables(om.Object, usesMana: false));
        }

        // ======== GetReagentsToBuy ========

        [Fact]
        public void GetReagentsToBuy_NonReagentClass_ReturnsEmpty()
        {
            var om = new Mock<IObjectManager>();
            // Rogues don't have class reagents
            var result = ConsumableData.GetReagentsToBuy(om.Object, Class.Rogue, 60);
            Assert.Empty(result);
        }

        [Fact]
        public void GetReagentsToBuy_ShamanNeedsAnkh()
        {
            var om = new Mock<IObjectManager>();
            var result = ConsumableData.GetReagentsToBuy(om.Object, Class.Shaman, 60);

            // Shaman needs Ankh (17030) at level 30+
            Assert.Contains(17030u, result.Keys);
            Assert.Equal(5u, result[17030u]); // Target count for Ankh
        }

        [Fact]
        public void GetReagentsToBuy_ShamanBelowMinLevel_ReturnsEmpty()
        {
            var om = new Mock<IObjectManager>();
            // Shaman at level 20 — below Ankh requirement (30)
            var result = ConsumableData.GetReagentsToBuy(om.Object, Class.Shaman, 20);
            Assert.Empty(result);
        }

        // ======== AllReagentItemIds ========

        [Fact]
        public void AllReagentItemIds_Contains4Items()
        {
            Assert.Equal(4, ConsumableData.AllReagentItemIds.Count);
            Assert.Contains(17030u, ConsumableData.AllReagentItemIds); // Ankh
            Assert.Contains(21177u, ConsumableData.AllReagentItemIds); // Symbol of Kings
            Assert.Contains(17029u, ConsumableData.AllReagentItemIds); // Sacred Candle
            Assert.Contains(17020u, ConsumableData.AllReagentItemIds); // Arcane Powder
        }

        private static Mock<IWoWItem> CreateMockItem(uint itemId, uint stackCount = 1)
        {
            var mock = new Mock<IWoWItem>();
            mock.Setup(i => i.ItemId).Returns(itemId);
            mock.Setup(i => i.StackCount).Returns(stackCount);
            mock.Setup(i => i.Name).Returns($"Item_{itemId}");
            return mock;
        }
    }
}
