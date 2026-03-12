using BotRunner.Combat;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;

namespace BotRunner.Tests.Combat
{
    public class FishingDataTests
    {
        [Theory]
        [InlineData(0, FishingData.FishingRank1)]
        [InlineData(1, FishingData.FishingRank1)]
        [InlineData(74, FishingData.FishingRank1)]
        [InlineData(75, FishingData.FishingRank2)]
        [InlineData(149, FishingData.FishingRank2)]
        [InlineData(150, FishingData.FishingRank3)]
        [InlineData(224, FishingData.FishingRank3)]
        [InlineData(225, FishingData.FishingRank4)]
        [InlineData(300, FishingData.FishingRank4)]
        public void GetBestFishingSpellId_ReturnsCorrectRank(int skill, uint expectedSpellId)
        {
            Assert.Equal(expectedSpellId, FishingData.GetBestFishingSpellId(skill));
        }

        [Fact]
        public void GetBestKnownFishingSpellId_PrefersHighestKnownRank()
        {
            uint result = FishingData.GetBestKnownFishingSpellId(
            [
                FishingData.FishingRank1,
                FishingData.FishingRank2,
                FishingData.FishingRank4
            ]);

            Assert.Equal(FishingData.FishingRank4, result);
        }

        [Fact]
        public void ResolveCastableFishingSpellId_FallsBackToSkillRank_WhenKnownListMissing()
        {
            uint result = FishingData.ResolveCastableFishingSpellId(null, 80);
            Assert.Equal(FishingData.FishingRank2, result);
        }

        [Theory]
        [InlineData(FishingData.FishingPole, true)]
        [InlineData(FishingData.StrongFishingPole, true)]
        [InlineData(FishingData.BigIronFishingPole, true)]
        [InlineData(FishingData.DarkwoodFishingPole, true)]
        [InlineData(0u, false)]
        [InlineData(6529u, false)] // Shiny Bauble (lure, not pole)
        [InlineData(12345u, false)]
        public void IsFishingPole_CorrectlyIdentifiesPoles(uint itemId, bool expected)
        {
            Assert.Equal(expected, FishingData.IsFishingPole(itemId));
        }

        [Fact]
        public void FindUsableLure_EmptyInventory_ReturnsNull()
        {
            var om = CreateEmptyObjectManager();
            Assert.Null(FishingData.FindUsableLure(om.Object));
        }

        [Fact]
        public void FindUsableLure_PrefersBestLure()
        {
            var om = new Mock<IObjectManager>();
            var bauble = CreateMockItem(FishingData.ShinyBauble);
            var attractor = CreateMockItem(FishingData.AquadynamicFishAttractor);

            // ShinyBauble in bag 0 slot 0, AquadynamicFishAttractor in bag 0 slot 1
            om.Setup(o => o.GetItem(0, 0)).Returns(bauble.Object);
            om.Setup(o => o.GetItem(0, 1)).Returns(attractor.Object);

            var result = FishingData.FindUsableLure(om.Object);
            Assert.NotNull(result);
            Assert.Equal(FishingData.AquadynamicFishAttractor, result.ItemId);
        }

        [Fact]
        public void FindUsableLure_FindsLureInExtraBag()
        {
            var om = new Mock<IObjectManager>();
            var lure = CreateMockItem(FishingData.NightcrawlerBait);

            // Lure in bag 2 slot 5
            om.Setup(o => o.GetItem(2, 5)).Returns(lure.Object);

            var result = FishingData.FindUsableLure(om.Object);
            Assert.NotNull(result);
            Assert.Equal(FishingData.NightcrawlerBait, result.ItemId);
        }

        [Fact]
        public void FindFishingPoleInBags_EmptyInventory_ReturnsNull()
        {
            var om = CreateEmptyObjectManager();
            Assert.Null(FishingData.FindFishingPoleInBags(om.Object));
        }

        [Fact]
        public void FindFishingPoleInBags_FindsPoleAndReturnsLocation()
        {
            var om = new Mock<IObjectManager>();
            var pole = CreateMockItem(FishingData.FishingPole);

            om.Setup(o => o.GetItem(1, 3)).Returns(pole.Object);

            var result = FishingData.FindFishingPoleInBags(om.Object);
            Assert.NotNull(result);
            Assert.Equal(1, result.Value.bag);
            Assert.Equal(3, result.Value.slot);
        }

        [Fact]
        public void FindFishingPoleInBags_PrefersBestPole()
        {
            var om = new Mock<IObjectManager>();
            var basicPole = CreateMockItem(FishingData.FishingPole);
            var bigIron = CreateMockItem(FishingData.BigIronFishingPole);

            // Basic in bag 0 slot 0, Big Iron in bag 0 slot 1
            om.Setup(o => o.GetItem(0, 0)).Returns(basicPole.Object);
            om.Setup(o => o.GetItem(0, 1)).Returns(bigIron.Object);

            var result = FishingData.FindFishingPoleInBags(om.Object);
            Assert.NotNull(result);
            // Big Iron should be found first (higher bonus)
            Assert.Equal(0, result.Value.bag);
            Assert.Equal(1, result.Value.slot);
        }

        [Fact]
        public void FindUsableLure_IgnoresNonLureItems()
        {
            var om = new Mock<IObjectManager>();
            var sword = CreateMockItem(12345); // Some random item

            om.Setup(o => o.GetItem(0, 0)).Returns(sword.Object);

            Assert.Null(FishingData.FindUsableLure(om.Object));
        }

        [Fact]
        public void SpellConstants_AreCorrectVanillaValues()
        {
            Assert.Equal(7620u, FishingData.FishingRank1);
            Assert.Equal(7731u, FishingData.FishingRank2);
            Assert.Equal(7732u, FishingData.FishingRank3);
            Assert.Equal(18248u, FishingData.FishingRank4);
            Assert.Equal(356u, FishingData.FishingSkillId);
        }

        [Fact]
        public void HasFishingPoleEquipped_ReturnsTrueWhenPoleIsEquipped()
        {
            var om = new Mock<IObjectManager>();
            om.Setup(o => o.GetEquippedItems()).Returns(
            [
                CreateMockItem(FishingData.FishingPole).Object
            ]);

            Assert.True(FishingData.HasFishingPoleEquipped(om.Object));
        }

        [Fact]
        public void IsFishingPool_ReturnsTrueForFishingHoleType()
        {
            var go = new Mock<IWoWGameObject>();
            go.Setup(g => g.TypeId).Returns((uint)GameObjectType.FishingHole);
            go.Setup(g => g.Position).Returns(new Position(0, 0, 0));

            Assert.True(FishingData.IsFishingPool(go.Object));
        }

        [Fact]
        public void FindNearestFishingPool_ReturnsClosestVisiblePool()
        {
            var playerPosition = new Position(-995f, -3850f, 4f);
            var closePool = CreateMockGameObject(180582, (uint)GameObjectType.FishingHole, new Position(-975.7f, -3835.2f, 0f));
            var farPool = CreateMockGameObject(180683, (uint)GameObjectType.FishingHole, new Position(-900f, -3700f, 0f));
            var om = new Mock<IObjectManager>();
            om.Setup(o => o.GameObjects).Returns([farPool.Object, closePool.Object]);

            var result = FishingData.FindNearestFishingPool(om.Object, playerPosition, 40f);

            Assert.NotNull(result);
            Assert.Equal(closePool.Object.Guid, result.Guid);
        }

        [Fact]
        public void GetPoolApproachPosition_KeepsDesiredDistanceFromPool()
        {
            var playerPosition = new Position(-995f, -3850f, 4f);
            var poolPosition = new Position(-975.7f, -3835.2f, 0f);

            var approach = FishingData.GetPoolApproachPosition(playerPosition, poolPosition, 14f);

            Assert.InRange(approach.DistanceTo(poolPosition), 13.9f, 14.1f);
            Assert.Equal(playerPosition.Z, approach.Z);
        }

        private static Mock<IObjectManager> CreateEmptyObjectManager()
        {
            var om = new Mock<IObjectManager>();
            // All GetItem calls return null by default
            return om;
        }

        private static Mock<IWoWItem> CreateMockItem(uint itemId)
        {
            var mock = new Mock<IWoWItem>();
            mock.Setup(i => i.ItemId).Returns(itemId);
            mock.Setup(i => i.Name).Returns($"Item_{itemId}");
            mock.Setup(i => i.StackCount).Returns(1);
            return mock;
        }

        private static Mock<IWoWGameObject> CreateMockGameObject(uint entry, uint typeId, Position position)
        {
            var mock = new Mock<IWoWGameObject>();
            mock.Setup(g => g.Guid).Returns(((ulong)entry << 8) | typeId);
            mock.Setup(g => g.Entry).Returns(entry);
            mock.Setup(g => g.TypeId).Returns(typeId);
            mock.Setup(g => g.Position).Returns(position);
            mock.Setup(g => g.Name).Returns($"Pool_{entry}");
            return mock;
        }
    }
}
