using BotRunner.Combat;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
using System.Reflection;

namespace BotRunner.Tests.Combat
{
    public class EquipmentServiceTests
    {
        private static ItemCacheInfo CreateItemCacheInfo(EquipSlot equipSlot, int requiredLevel = 1,
            ItemClass itemClass = default, int itemSubclassId = 0)
        {
            var entry = new ItemCacheEntry(0);
            // Use reflection to set internal fields
            var type = typeof(ItemCacheEntry);
            type.GetField("EquipSlot", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(entry, equipSlot);
            type.GetField("RequiredLevel", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(entry, requiredLevel);
            type.GetField("ItemClass", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(entry, itemClass);
            type.GetField("ItemSubclassID", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(entry, itemSubclassId);
            return new ItemCacheInfo(entry);
        }

        // ItemClass values that map to WoW's internal class IDs (enum naming is misleading)
        private const ItemClass WoWWeaponClass = (ItemClass)2;  // Weapon
        private const ItemClass WoWArmorClass = (ItemClass)4;   // Armor

        private static Mock<IWoWItem> CreateMockItem(EquipSlot slot, ItemQuality quality, uint requiredLevel)
        {
            var mock = new Mock<IWoWItem>();
            mock.Setup(i => i.Info).Returns(CreateItemCacheInfo(slot, (int)requiredLevel));
            mock.Setup(i => i.Quality).Returns(quality);
            mock.Setup(i => i.RequiredLevel).Returns(requiredLevel);
            return mock;
        }

        // --- IsUpgrade logic ---

        [Fact]
        public void TryEquipUpgrades_EmptyBags_ReturnsZero()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>())).Returns((IWoWItem?)null);

            int result = service.TryEquipUpgrades(om.Object, 10);

            Assert.Equal(0, result);
        }

        [Fact]
        public void TryEquipUpgrades_ItemInEmptySlot_EquipsIt()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Bag 0 slot 0 has a green chest piece
            var item = CreateMockItem(EquipSlot.Chest, ItemQuality.Uncommon, 5);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(item.Object);
            om.Setup(o => o.GetContainedItem(It.Is<int>(b => b != 0 || true), It.Is<int>(s => s != 0 || true)))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? item.Object : null);

            // No item currently equipped in chest slot
            om.Setup(o => o.GetEquippedItem(EquipSlot.Chest)).Returns((IWoWItem?)null);

            int result = service.TryEquipUpgrades(om.Object, 10);

            Assert.Equal(1, result);
            om.Verify(o => o.EquipItem(0, 0, EquipSlot.Chest), Times.Once);
        }

        [Fact]
        public void TryEquipUpgrades_HigherQuality_IsUpgrade()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Green item in bags
            var bagItem = CreateMockItem(EquipSlot.Head, ItemQuality.Uncommon, 10);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagItem.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagItem.Object : null);

            // White item equipped
            var equippedItem = CreateMockItem(EquipSlot.Head, ItemQuality.Common, 10);
            om.Setup(o => o.GetEquippedItem(EquipSlot.Head)).Returns(equippedItem.Object);

            int result = service.TryEquipUpgrades(om.Object, 20);

            Assert.Equal(1, result);
            om.Verify(o => o.EquipItem(0, 0, EquipSlot.Head), Times.Once);
        }

        [Fact]
        public void TryEquipUpgrades_LowerQuality_NotUpgrade()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // White item in bags
            var bagItem = CreateMockItem(EquipSlot.Head, ItemQuality.Common, 10);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagItem.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagItem.Object : null);

            // Green item equipped
            var equippedItem = CreateMockItem(EquipSlot.Head, ItemQuality.Uncommon, 10);
            om.Setup(o => o.GetEquippedItem(EquipSlot.Head)).Returns(equippedItem.Object);

            int result = service.TryEquipUpgrades(om.Object, 20);

            Assert.Equal(0, result);
            om.Verify(o => o.EquipItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<EquipSlot>()), Times.Never);
        }

        [Fact]
        public void TryEquipUpgrades_SameQualityHigherLevel_IsUpgrade()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Green RL15 in bags
            var bagItem = CreateMockItem(EquipSlot.Legs, ItemQuality.Uncommon, 15);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagItem.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagItem.Object : null);

            // Green RL10 equipped
            var equippedItem = CreateMockItem(EquipSlot.Legs, ItemQuality.Uncommon, 10);
            om.Setup(o => o.GetEquippedItem(EquipSlot.Legs)).Returns(equippedItem.Object);

            int result = service.TryEquipUpgrades(om.Object, 20);

            Assert.Equal(1, result);
        }

        [Fact]
        public void TryEquipUpgrades_SameQualityLowerLevel_NotUpgrade()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Green RL5 in bags
            var bagItem = CreateMockItem(EquipSlot.Legs, ItemQuality.Uncommon, 5);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagItem.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagItem.Object : null);

            // Green RL10 equipped
            var equippedItem = CreateMockItem(EquipSlot.Legs, ItemQuality.Uncommon, 10);
            om.Setup(o => o.GetEquippedItem(EquipSlot.Legs)).Returns(equippedItem.Object);

            int result = service.TryEquipUpgrades(om.Object, 20);

            Assert.Equal(0, result);
        }

        // --- Level requirement ---

        [Fact]
        public void TryEquipUpgrades_ItemAbovePlayerLevel_Skipped()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // RL20 item but player is level 10
            var bagItem = CreateMockItem(EquipSlot.Chest, ItemQuality.Rare, 20);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagItem.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagItem.Object : null);
            om.Setup(o => o.GetEquippedItem(EquipSlot.Chest)).Returns((IWoWItem?)null);

            int result = service.TryEquipUpgrades(om.Object, 10);

            Assert.Equal(0, result);
            om.Verify(o => o.EquipItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<EquipSlot>()), Times.Never);
        }

        // --- Cooldown ---

        [Fact]
        public void TryEquipUpgrades_CalledTwiceQuickly_SecondCallReturnsZero()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Item in bags with empty slot
            var bagItem = CreateMockItem(EquipSlot.Chest, ItemQuality.Common, 1);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagItem.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagItem.Object : null);
            om.Setup(o => o.GetEquippedItem(EquipSlot.Chest)).Returns((IWoWItem?)null);

            // First call should work
            int first = service.TryEquipUpgrades(om.Object, 10);
            Assert.Equal(1, first);

            // Second call immediately after should be cooldown-blocked
            int second = service.TryEquipUpgrades(om.Object, 10);
            Assert.Equal(0, second);
        }

        // --- Dual-slot items ---

        [Fact]
        public void TryEquipUpgrades_RingGoesToEmptyFinger2WhenFinger1Occupied()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Ring in bags
            var bagRing = CreateMockItem(EquipSlot.Finger1, ItemQuality.Uncommon, 10);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagRing.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagRing.Object : null);

            // Finger1 occupied, Finger2 empty
            var finger1Item = CreateMockItem(EquipSlot.Finger1, ItemQuality.Uncommon, 10);
            om.Setup(o => o.GetEquippedItem(EquipSlot.Finger1)).Returns(finger1Item.Object);
            om.Setup(o => o.GetEquippedItem(EquipSlot.Finger2)).Returns((IWoWItem?)null);

            int result = service.TryEquipUpgrades(om.Object, 20);

            Assert.Equal(1, result);
            om.Verify(o => o.EquipItem(0, 0, EquipSlot.Finger2), Times.Once);
        }

        // --- Non-equipment slot ---

        [Fact]
        public void TryEquipUpgrades_NonEquipSlot_Skipped()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Item with Ammo slot (not equippable by our logic)
            var bagItem = CreateMockItem(EquipSlot.Ammo, ItemQuality.Common, 1);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagItem.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagItem.Object : null);

            int result = service.TryEquipUpgrades(om.Object, 10);

            Assert.Equal(0, result);
        }

        [Fact]
        public void TryEquipUpgrades_NullInfo_Skipped()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Item exists but has null Info
            var bagItem = new Mock<IWoWItem>();
            bagItem.Setup(i => i.Info).Returns((ItemCacheInfo?)null);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagItem.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagItem.Object : null);

            int result = service.TryEquipUpgrades(om.Object, 10);

            Assert.Equal(0, result);
        }

        // --- Only equips one per scan ---

        [Fact]
        public void TryEquipUpgrades_MultipleUpgrades_EquipsOnlyFirst()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Two upgrades in bag: slot 0 = chest, slot 1 = legs
            var chestItem = CreateMockItem(EquipSlot.Chest, ItemQuality.Uncommon, 5);
            var legsItem = CreateMockItem(EquipSlot.Legs, ItemQuality.Uncommon, 5);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) =>
              {
                  if (bag == 0 && slot == 0) return chestItem.Object;
                  if (bag == 0 && slot == 1) return legsItem.Object;
                  return null;
              });

            // Both slots empty
            om.Setup(o => o.GetEquippedItem(EquipSlot.Chest)).Returns((IWoWItem?)null);
            om.Setup(o => o.GetEquippedItem(EquipSlot.Legs)).Returns((IWoWItem?)null);

            int result = service.TryEquipUpgrades(om.Object, 10);

            // Should equip only 1 item (returns early after first equip)
            Assert.Equal(1, result);
        }

        // --- Weapon Proficiency ---

        [Theory]
        [InlineData(Class.Warrior, ItemSubclass.OneHandedSword, true)]
        [InlineData(Class.Warrior, ItemSubclass.TwoHandedAxe, true)]
        [InlineData(Class.Warrior, ItemSubclass.Bow, true)]
        [InlineData(Class.Warrior, ItemSubclass.Thrown, true)]
        [InlineData(Class.Warrior, ItemSubclass.Wand, false)]
        [InlineData(Class.Rogue, ItemSubclass.Dagger, true)]
        [InlineData(Class.Rogue, ItemSubclass.OneHandedMace, true)]
        [InlineData(Class.Rogue, ItemSubclass.TwoHandedSword, false)]
        [InlineData(Class.Rogue, ItemSubclass.TwoHandedAxe, false)]
        [InlineData(Class.Rogue, ItemSubclass.Staff, false)]
        [InlineData(Class.Mage, ItemSubclass.Staff, true)]
        [InlineData(Class.Mage, ItemSubclass.Wand, true)]
        [InlineData(Class.Mage, ItemSubclass.Dagger, true)]
        [InlineData(Class.Mage, ItemSubclass.OneHandedSword, true)]
        [InlineData(Class.Mage, ItemSubclass.OneHandedAxe, false)]
        [InlineData(Class.Mage, ItemSubclass.TwoHandedSword, false)]
        [InlineData(Class.Priest, ItemSubclass.OneHandedMace, true)]
        [InlineData(Class.Priest, ItemSubclass.Dagger, true)]
        [InlineData(Class.Priest, ItemSubclass.Staff, true)]
        [InlineData(Class.Priest, ItemSubclass.Wand, true)]
        [InlineData(Class.Priest, ItemSubclass.OneHandedSword, false)]
        [InlineData(Class.Paladin, ItemSubclass.OneHandedSword, true)]
        [InlineData(Class.Paladin, ItemSubclass.TwoHandedMace, true)]
        [InlineData(Class.Paladin, ItemSubclass.Polearm, true)]
        [InlineData(Class.Paladin, ItemSubclass.Dagger, false)]
        [InlineData(Class.Paladin, ItemSubclass.FistWeapon, false)]
        [InlineData(Class.Hunter, ItemSubclass.Bow, true)]
        [InlineData(Class.Hunter, ItemSubclass.Gun, true)]
        [InlineData(Class.Hunter, ItemSubclass.Crossbow, true)]
        [InlineData(Class.Hunter, ItemSubclass.TwoHandedSword, true)]
        [InlineData(Class.Hunter, ItemSubclass.Wand, false)]
        [InlineData(Class.Shaman, ItemSubclass.OneHandedAxe, true)]
        [InlineData(Class.Shaman, ItemSubclass.TwoHandedMace, true)]
        [InlineData(Class.Shaman, ItemSubclass.FistWeapon, true)]
        [InlineData(Class.Shaman, ItemSubclass.OneHandedSword, false)]
        [InlineData(Class.Shaman, ItemSubclass.Bow, false)]
        [InlineData(Class.Warlock, ItemSubclass.Dagger, true)]
        [InlineData(Class.Warlock, ItemSubclass.Staff, true)]
        [InlineData(Class.Warlock, ItemSubclass.Wand, true)]
        [InlineData(Class.Warlock, ItemSubclass.OneHandedAxe, false)]
        [InlineData(Class.Druid, ItemSubclass.Staff, true)]
        [InlineData(Class.Druid, ItemSubclass.FistWeapon, true)]
        [InlineData(Class.Druid, ItemSubclass.TwoHandedMace, true)]
        [InlineData(Class.Druid, ItemSubclass.OneHandedSword, false)]
        [InlineData(Class.Druid, ItemSubclass.Bow, false)]
        public void CanUseWeaponType_ReturnsExpected(Class playerClass, ItemSubclass weapon, bool expected)
        {
            Assert.Equal(expected, EquipmentService.CanUseWeaponType(playerClass, weapon));
        }

        // --- Armor Proficiency ---

        [Theory]
        [InlineData(Class.Warrior, ItemSubclass.Plate, true)]
        [InlineData(Class.Warrior, ItemSubclass.Mail, true)]
        [InlineData(Class.Warrior, ItemSubclass.Leather, true)]
        [InlineData(Class.Warrior, ItemSubclass.Cloth, true)]
        [InlineData(Class.Warrior, ItemSubclass.Shield, true)]
        [InlineData(Class.Warrior, ItemSubclass.Libram, false)]
        [InlineData(Class.Paladin, ItemSubclass.Plate, true)]
        [InlineData(Class.Paladin, ItemSubclass.Shield, true)]
        [InlineData(Class.Paladin, ItemSubclass.Libram, true)]
        [InlineData(Class.Paladin, ItemSubclass.Idol, false)]
        [InlineData(Class.Hunter, ItemSubclass.Mail, true)]
        [InlineData(Class.Hunter, ItemSubclass.Leather, true)]
        [InlineData(Class.Hunter, ItemSubclass.Plate, false)]
        [InlineData(Class.Hunter, ItemSubclass.Shield, false)]
        [InlineData(Class.Rogue, ItemSubclass.Leather, true)]
        [InlineData(Class.Rogue, ItemSubclass.Mail, false)]
        [InlineData(Class.Rogue, ItemSubclass.Plate, false)]
        [InlineData(Class.Priest, ItemSubclass.Cloth, true)]
        [InlineData(Class.Priest, ItemSubclass.Leather, false)]
        [InlineData(Class.Mage, ItemSubclass.Cloth, true)]
        [InlineData(Class.Mage, ItemSubclass.Leather, false)]
        [InlineData(Class.Warlock, ItemSubclass.Cloth, true)]
        [InlineData(Class.Warlock, ItemSubclass.Leather, false)]
        [InlineData(Class.Shaman, ItemSubclass.Mail, true)]
        [InlineData(Class.Shaman, ItemSubclass.Leather, true)]
        [InlineData(Class.Shaman, ItemSubclass.Shield, true)]
        [InlineData(Class.Shaman, ItemSubclass.Totem, true)]
        [InlineData(Class.Shaman, ItemSubclass.Plate, false)]
        [InlineData(Class.Druid, ItemSubclass.Leather, true)]
        [InlineData(Class.Druid, ItemSubclass.Idol, true)]
        [InlineData(Class.Druid, ItemSubclass.Mail, false)]
        [InlineData(Class.Druid, ItemSubclass.Shield, false)]
        public void CanUseArmorType_ReturnsExpected(Class playerClass, ItemSubclass armor, bool expected)
        {
            Assert.Equal(expected, EquipmentService.CanUseArmorType(playerClass, armor));
        }

        // --- CanEquipItem integration ---

        [Fact]
        public void CanEquipItem_NonWeaponNonArmor_AlwaysTrue()
        {
            // Consumable subclass — should always return true
            var info = CreateItemCacheInfo(EquipSlot.MainHand, itemClass: default, itemSubclassId: 0);
            Assert.True(EquipmentService.CanEquipItem(Class.Mage, info));
        }

        [Fact]
        public void CanEquipItem_WeaponRogueCannotUse_ReturnsFalse()
        {
            // TwoHandedSword (index 8 in weapon subarray) — Rogue can't use
            var info = CreateItemCacheInfo(EquipSlot.MainHand, itemClass: WoWWeaponClass, itemSubclassId: 8);
            Assert.False(EquipmentService.CanEquipItem(Class.Rogue, info));
        }

        [Fact]
        public void CanEquipItem_WeaponRogueCanUse_ReturnsTrue()
        {
            // Dagger (index 15 in weapon subarray) — Rogue can use
            var info = CreateItemCacheInfo(EquipSlot.MainHand, itemClass: WoWWeaponClass, itemSubclassId: 15);
            Assert.True(EquipmentService.CanEquipItem(Class.Rogue, info));
        }

        [Fact]
        public void CanEquipItem_PlateForMage_ReturnsFalse()
        {
            // Plate (index 4 in armor subarray) — Mage can't use
            var info = CreateItemCacheInfo(EquipSlot.Chest, itemClass: WoWArmorClass, itemSubclassId: 4);
            Assert.False(EquipmentService.CanEquipItem(Class.Mage, info));
        }

        [Fact]
        public void CanEquipItem_ClothForMage_ReturnsTrue()
        {
            // Cloth (index 1 in armor subarray) — Mage can use
            var info = CreateItemCacheInfo(EquipSlot.Chest, itemClass: WoWArmorClass, itemSubclassId: 1);
            Assert.True(EquipmentService.CanEquipItem(Class.Mage, info));
        }

        // --- TryEquipUpgrades with class restriction ---

        [Fact]
        public void TryEquipUpgrades_WeaponClassCantUse_Skipped()
        {
            var service = new EquipmentService();
            var om = new Mock<IObjectManager>();

            // Staff in bags (weapon class index 2, staff subclass index 10) — Rogue can't use
            var info = CreateItemCacheInfo(EquipSlot.MainHand, 5, WoWWeaponClass, 10);
            var bagItem = new Mock<IWoWItem>();
            bagItem.Setup(i => i.Info).Returns(info);
            bagItem.Setup(i => i.Quality).Returns(ItemQuality.Uncommon);
            bagItem.Setup(i => i.RequiredLevel).Returns(5u);
            om.Setup(o => o.GetContainedItem(0, 0)).Returns(bagItem.Object);
            om.Setup(o => o.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
              .Returns((int bag, int slot) => bag == 0 && slot == 0 ? bagItem.Object : null);
            om.Setup(o => o.GetEquippedItem(EquipSlot.MainHand)).Returns((IWoWItem?)null);

            // Player is a Rogue
            var player = new Mock<IWoWLocalPlayer>();
            player.Setup(p => p.Class).Returns(Class.Rogue);
            om.Setup(o => o.Player).Returns(player.Object);

            int result = service.TryEquipUpgrades(om.Object, 10);

            Assert.Equal(0, result);
            om.Verify(o => o.EquipItem(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<EquipSlot>()), Times.Never);
        }
    }
}
