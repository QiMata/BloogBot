using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Models;

namespace WoWSharpClient.Tests.Models
{
    public class WoWPlayerTests
    {
        [Fact]
        public void WoWPlayer_IsDrinking_ChecksDrinkBuff()
        {
            var player = new WoWPlayer(new HighGuid(1));
            Assert.False(player.IsDrinking);

            player.Buffs.Add(new Spell(430, 0, "Drink", "", ""));
            Assert.True(player.IsDrinking);
        }

        [Fact]
        public void WoWPlayer_IsEating_ChecksFoodBuff()
        {
            var player = new WoWPlayer(new HighGuid(1));
            Assert.False(player.IsEating);

            player.Buffs.Add(new Spell(433, 0, "Food", "", ""));
            Assert.True(player.IsEating);
        }

        [Fact]
        public void WoWPlayer_Clone_CopiesScalarProperties()
        {
            var original = new WoWPlayer(new HighGuid(1))
            {
                Race = Race.Human,
                Class = Class.Warrior,
                Gender = Gender.Male,
                MapId = 1,
                XP = 5000,
                NextLevelXP = 10000,
                Coinage = 99999,
                GuildId = 42,
                GuildRank = 3,
                CharacterPoints1 = 5,
                PlayerFlags = PlayerFlags.PLAYER_FLAGS_RESTING
            };

            var clone = original.Clone();

            Assert.Equal(Race.Human, clone.Race);
            Assert.Equal(Class.Warrior, clone.Class);
            Assert.Equal(Gender.Male, clone.Gender);
            Assert.Equal(1u, clone.MapId);
            Assert.Equal(5000u, clone.XP);
            Assert.Equal(10000u, clone.NextLevelXP);
            Assert.Equal(99999u, clone.Coinage);
            Assert.Equal(42u, clone.GuildId);
            Assert.Equal(3u, clone.GuildRank);
            Assert.Equal(5u, clone.CharacterPoints1);
            Assert.Equal(PlayerFlags.PLAYER_FLAGS_RESTING, clone.PlayerFlags);
        }

        [Fact]
        public void WoWPlayer_Clone_CopiesArrays()
        {
            var original = new WoWPlayer(new HighGuid(1));
            original.Inventory[0] = 100;
            original.Inventory[45] = 200;
            original.PackSlots[0] = 300;
            original.PackSlots[31] = 400;

            var clone = original.Clone();

            Assert.Equal(100u, clone.Inventory[0]);
            Assert.Equal(200u, clone.Inventory[45]);
            Assert.Equal(300u, clone.PackSlots[0]);
            Assert.Equal(400u, clone.PackSlots[31]);
        }

        [Fact]
        public void WoWPlayer_Clone_ArraysAreIndependent()
        {
            var original = new WoWPlayer(new HighGuid(1));
            original.Inventory[0] = 100;

            var clone = original.Clone();
            clone.Inventory[0] = 999;

            Assert.Equal(100u, original.Inventory[0]);
        }

        [Fact]
        public void WoWPlayer_Clone_ScalarsAreIndependent()
        {
            var original = new WoWPlayer(new HighGuid(1))
            {
                Coinage = 1000,
                XP = 500
            };

            var clone = original.Clone();
            clone.Coinage = 0;
            clone.XP = 9999;

            Assert.Equal(1000u, original.Coinage);
            Assert.Equal(500u, original.XP);
        }

        [Fact]
        public void WoWPlayer_ArraySizes_MatchProtocol()
        {
            var player = new WoWPlayer(new HighGuid(1));
            Assert.Equal(20, player.QuestLog.Length);
            Assert.Equal(19, player.VisibleItems.Length);
            Assert.Equal(46, player.Inventory.Length);
            Assert.Equal(32, player.PackSlots.Length);
            Assert.Equal(48, player.BankSlots.Length);
            Assert.Equal(12, player.BankBagSlots.Length);
            Assert.Equal(24, player.VendorBuybackSlots.Length);
            Assert.Equal(64, player.KeyringSlots.Length);
            Assert.Equal(102, player.SkillInfo.Length);
            Assert.Equal(64, player.ExploredZones.Length);
            Assert.Equal(20, player.CombatRating.Length);
        }

        [Fact]
        public void WoWPlayer_PvPStats_DefaultToZero()
        {
            var player = new WoWPlayer(new HighGuid(1));
            Assert.Equal(0u, player.SessionKills);
            Assert.Equal(0u, player.LifetimeHonorableKills);
            Assert.Equal(0u, player.LifetimeDishonorableKills);
        }

        [Fact]
        public void WoWPlayer_CopyFrom_IgnoresNonPlayerSource()
        {
            var player = new WoWPlayer(new HighGuid(1)) { Race = Race.Human };
            var unit = new WoWUnit(new HighGuid(2));

            player.CopyFrom(unit);

            // Race should remain unchanged since source is WoWUnit, not WoWPlayer
            Assert.Equal(Race.Human, player.Race);
        }
    }

    public class WoWLocalPlayerTests
    {
        [Fact]
        public void InGhostForm_TrueWhenHasGhostBuff()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            Assert.False(player.InGhostForm);

            player.Buffs.Add(new Spell(8326, 0, "Ghost", "", ""));
            Assert.True(player.InGhostForm);
        }

        [Fact]
        public void IsCursed_MatchesCurseDebuffs()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            Assert.False(player.IsCursed);

            player.Debuffs.Add(new Spell(702, 0, "Curse of Weakness", "", ""));
            Assert.True(player.IsCursed);
        }

        [Fact]
        public void IsPoisoned_MatchesPoisonAndVenom()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            Assert.False(player.IsPoisoned);

            player.Debuffs.Add(new Spell(3583, 0, "Deadly Poison", "", ""));
            Assert.True(player.IsPoisoned);

            player.Debuffs.Clear();
            player.Debuffs.Add(new Spell(25810, 0, "Brood Power: Green (Venom)", "", ""));
            Assert.True(player.IsPoisoned);
        }

        [Fact]
        public void IsDiseased_MatchesDiseaseAndPlague()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            Assert.False(player.IsDiseased);

            player.Debuffs.Add(new Spell(12946, 0, "Rotting Disease", "", ""));
            Assert.True(player.IsDiseased);

            player.Debuffs.Clear();
            player.Debuffs.Add(new Spell(12814, 0, "Devouring Plague", "", ""));
            Assert.True(player.IsDiseased);
        }

        [Fact]
        public void HasMagicDebuff_MatchesKnownMagicEffects()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            Assert.False(player.HasMagicDebuff);

            player.Debuffs.Add(new Spell(118, 0, "Polymorph", "", ""));
            Assert.True(player.HasMagicDebuff);

            player.Debuffs.Clear();
            player.Debuffs.Add(new Spell(116, 0, "Frost Bolt Slow", "", ""));
            Assert.True(player.HasMagicDebuff);

            player.Debuffs.Clear();
            player.Debuffs.Add(new Spell(122, 0, "Frost Nova", "", ""));
            Assert.True(player.HasMagicDebuff);
        }

        [Fact]
        public void CurrentStance_ReturnsBattleStance()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            Assert.Equal("None", player.CurrentStance);

            player.Buffs.Add(new Spell(2457, 0, "Battle Stance", "", ""));
            Assert.Equal("Battle Stance", player.CurrentStance);
        }

        [Fact]
        public void CurrentStance_ReturnsDefensiveStance()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            player.Buffs.Add(new Spell(71, 0, "Defensive Stance", "", ""));
            Assert.Equal("Defensive Stance", player.CurrentStance);
        }

        [Fact]
        public void CurrentStance_ReturnsBerserkerStance()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            player.Buffs.Add(new Spell(2458, 0, "Berserker Stance", "", ""));
            Assert.Equal("Berserker Stance", player.CurrentStance);
        }

        [Fact]
        public void CurrentStance_PrioritizesBattleOverOthers()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            // Add multiple stances — should prioritize Battle (checked first)
            player.Buffs.Add(new Spell(2458, 0, "Berserker Stance", "", ""));
            player.Buffs.Add(new Spell(2457, 0, "Battle Stance", "", ""));
            Assert.Equal("Battle Stance", player.CurrentStance);
        }

        [Fact]
        public void CanResurrect_RequiresDeadAndCorpsePosition()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));

            // Alive — cannot resurrect
            player.Health = 100;
            Assert.False(player.CanResurrect);

            // Dead but no corpse position — cannot resurrect
            player.Health = 0;
            Assert.False(player.CanResurrect);

            // Dead with corpse position — CAN resurrect
            player.CorpsePosition = new Position(100, 200, 300);
            Assert.True(player.CanResurrect);
        }

        [Fact]
        public void CanResurrect_FalseWhenCorpseAtOrigin()
        {
            var player = new WoWLocalPlayer(new HighGuid(1))
            {
                Health = 0,
                CorpsePosition = new Position(0, 0, 0)
            };
            Assert.False(player.CanResurrect);
        }

        [Theory]
        [InlineData(30u, true)]   // Alterac Valley
        [InlineData(489u, true)]  // Warsong Gulch
        [InlineData(529u, true)]  // Arathi Basin
        [InlineData(566u, true)]  // Eye of the Storm
        [InlineData(0u, false)]   // Eastern Kingdoms
        [InlineData(1u, false)]   // Kalimdor
        [InlineData(571u, false)] // Northrend
        public void InBattleground_ChecksMapId(uint mapId, bool expected)
        {
            var player = new WoWLocalPlayer(new HighGuid(1)) { MapId = mapId };
            Assert.Equal(expected, player.InBattleground);
        }

        [Fact]
        public void Copper_ReturnsCoinageValue()
        {
            var player = new WoWLocalPlayer(new HighGuid(1)) { Coinage = 12345 };
            Assert.Equal(12345u, player.Copper);
        }

        [Fact]
        public void ComboPoints_GetSet()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            Assert.Equal(0, player.ComboPoints);
            player.ComboPoints = 5;
            Assert.Equal(5, player.ComboPoints);
        }

        [Fact]
        public void IsAutoAttacking_GetSet()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            Assert.False(player.IsAutoAttacking);
            player.IsAutoAttacking = true;
            Assert.True(player.IsAutoAttacking);
        }

        [Fact]
        public void Clone_CopiesLocalPlayerProperties()
        {
            var original = new WoWLocalPlayer(new HighGuid(1))
            {
                ComboPoints = 3,
                IsAutoAttacking = true,
                CanRiposte = true,
                MainhandIsEnchanted = true,
                TastyCorpsesNearby = true,
                CorpsePosition = new Position(100, 200, 300)
            };

            var clone = original.Clone();

            Assert.Equal(3, clone.ComboPoints);
            Assert.True(clone.IsAutoAttacking);
            Assert.True(clone.CanRiposte);
            Assert.True(clone.MainhandIsEnchanted);
            Assert.True(clone.TastyCorpsesNearby);
            Assert.Equal(100f, clone.CorpsePosition.X);
            Assert.Equal(200f, clone.CorpsePosition.Y);
            Assert.Equal(300f, clone.CorpsePosition.Z);
        }

        [Fact]
        public void Clone_CorpsePositionIsIndependent()
        {
            var original = new WoWLocalPlayer(new HighGuid(1))
            {
                CorpsePosition = new Position(10, 20, 30)
            };

            var clone = original.Clone();
            clone.CorpsePosition = new Position(999, 999, 999);

            Assert.Equal(10f, original.CorpsePosition.X);
        }

        [Fact]
        public void HasQuestTargets_ChecksQuestLog()
        {
            var player = new WoWLocalPlayer(new HighGuid(1));
            // Default — all quest slots have QuestId == 0
            Assert.False(player.HasQuestTargets);

            // Set a quest ID in the log
            player.QuestLog[0] = new QuestSlot { QuestId = 100 };
            Assert.True(player.HasQuestTargets);
        }
    }

    public class WoWContainerTests
    {
        [Fact]
        public void GetItemGuid_ReconstructsFromPairs()
        {
            var container = new WoWContainer(new HighGuid(1))
            {
                NumOfSlots = 16
            };
            // Slot 0: store low=0xDEADBEEF, high=0x12345678
            container.Slots[0] = 0xDEADBEEF;
            container.Slots[1] = 0x12345678;

            ulong expected = ((ulong)0x12345678 << 32) | 0xDEADBEEF;
            Assert.Equal(expected, container.GetItemGuid(0));
        }

        [Fact]
        public void GetItemGuid_Slot5_UsesCorrectOffset()
        {
            var container = new WoWContainer(new HighGuid(1))
            {
                NumOfSlots = 16
            };
            // Slot 5 → index = 5 * 2 = 10
            container.Slots[10] = 42;
            container.Slots[11] = 0;

            Assert.Equal(42UL, container.GetItemGuid(5));
        }

        [Fact]
        public void GetItemGuid_NegativeSlot_ReturnsZero()
        {
            var container = new WoWContainer(new HighGuid(1));
            Assert.Equal(0UL, container.GetItemGuid(-1));
        }

        [Fact]
        public void GetItemGuid_MaxValidSlot_Works()
        {
            var container = new WoWContainer(new HighGuid(1));
            // 72 slots / 2 per guid = 36 max slots, but index must be valid
            // Slot 35 → index = 70, needs 70 and 71 (both valid in 72-element array)
            container.Slots[70] = 100;
            container.Slots[71] = 200;
            ulong expected = ((ulong)200 << 32) | 100;
            Assert.Equal(expected, container.GetItemGuid(35));
        }

        [Fact]
        public void GetItemGuid_EmptySlot_ReturnsZero()
        {
            var container = new WoWContainer(new HighGuid(1));
            Assert.Equal(0UL, container.GetItemGuid(0));
        }

        [Fact]
        public void Clone_CopiesNumOfSlotsAndSlots()
        {
            var original = new WoWContainer(new HighGuid(1))
            {
                NumOfSlots = 16
            };
            original.Slots[0] = 42;
            original.Slots[1] = 99;

            var clone = (WoWContainer)original.Clone();

            Assert.Equal(16, clone.NumOfSlots);
            Assert.Equal(42u, clone.Slots[0]);
            Assert.Equal(99u, clone.Slots[1]);
        }

        [Fact]
        public void Clone_SlotsAreIndependent()
        {
            var original = new WoWContainer(new HighGuid(1));
            original.Slots[0] = 42;

            var clone = (WoWContainer)original.Clone();
            clone.Slots[0] = 999;

            Assert.Equal(42u, original.Slots[0]);
        }

        [Fact]
        public void Slots_Has72Elements()
        {
            var container = new WoWContainer(new HighGuid(1));
            Assert.Equal(72, container.Slots.Length);
        }
    }

    public class WoWCorpseTests
    {
        [Fact]
        public void IsBones_TrueWhenFlagSet()
        {
            var corpse = new WoWCorpse(new HighGuid(1));
            Assert.False(corpse.IsBones());

            corpse.CorpseFlags = CorpseFlags.CORPSE_FLAG_BONES;
            Assert.True(corpse.IsBones());
        }

        [Fact]
        public void IsBones_TrueWithMultipleFlags()
        {
            var corpse = new WoWCorpse(new HighGuid(1))
            {
                CorpseFlags = CorpseFlags.CORPSE_FLAG_BONES | CorpseFlags.CORPSE_FLAG_LOOTABLE
            };
            Assert.True(corpse.IsBones());
        }

        [Fact]
        public void IsPvP_TrueForPvPCorpse()
        {
            var corpse = new WoWCorpse(new HighGuid(1));
            Assert.False(corpse.IsPvP());

            corpse.Type = CorpseType.CORPSE_RESURRECTABLE_PVP;
            Assert.True(corpse.IsPvP());
        }

        [Fact]
        public void IsPvP_FalseForPvE()
        {
            var corpse = new WoWCorpse(new HighGuid(1))
            {
                Type = CorpseType.CORPSE_RESURRECTABLE_PVE
            };
            Assert.False(corpse.IsPvP());
        }

        [Fact]
        public void IsPvP_FalseForBones()
        {
            var corpse = new WoWCorpse(new HighGuid(1))
            {
                Type = CorpseType.CORPSE_BONES
            };
            Assert.False(corpse.IsPvP());
        }

        [Fact]
        public void Clone_CopiesAllProperties()
        {
            var original = new WoWCorpse(new HighGuid(1))
            {
                GhostTime = 12345,
                Type = CorpseType.CORPSE_RESURRECTABLE_PVP,
                Angle = 1.5f,
                CorpseFlags = CorpseFlags.CORPSE_FLAG_LOOTABLE,
                Guild = 42
            };
            original.Items[0] = 100;
            original.Items[63] = 200;

            var clone = (WoWCorpse)original.Clone();

            Assert.Equal(12345u, clone.GhostTime);
            Assert.Equal(CorpseType.CORPSE_RESURRECTABLE_PVP, clone.Type);
            Assert.Equal(1.5f, clone.Angle);
            Assert.Equal(CorpseFlags.CORPSE_FLAG_LOOTABLE, clone.CorpseFlags);
            Assert.Equal(42u, clone.Guild);
            Assert.Equal(100u, clone.Items[0]);
            Assert.Equal(200u, clone.Items[63]);
        }

        [Fact]
        public void Clone_ItemsAreIndependent()
        {
            var original = new WoWCorpse(new HighGuid(1));
            original.Items[0] = 42;

            var clone = (WoWCorpse)original.Clone();
            clone.Items[0] = 999;

            Assert.Equal(42u, original.Items[0]);
        }

        [Fact]
        public void Items_Has64Elements()
        {
            var corpse = new WoWCorpse(new HighGuid(1));
            Assert.Equal(64, corpse.Items.Length);
        }
    }

    public class WoWDynamicObjectTests
    {
        [Fact]
        public void Clone_CopiesAllProperties()
        {
            var original = new WoWDynamicObject(new HighGuid(1))
            {
                SpellId = 1449,
                Radius = 10.5f
            };

            var clone = original.Clone();

            Assert.Equal(1449u, clone.SpellId);
            Assert.Equal(10.5f, clone.Radius);
        }

        [Fact]
        public void Clone_CopiesBytesArray()
        {
            var original = new WoWDynamicObject(new HighGuid(1));
            original.Bytes[0] = 0xAB;
            original.Bytes[3] = 0xCD;

            var clone = original.Clone();

            Assert.Equal(0xAB, clone.Bytes[0]);
            Assert.Equal(0xCD, clone.Bytes[3]);
        }

        [Fact]
        public void Clone_BytesAreIndependent()
        {
            var original = new WoWDynamicObject(new HighGuid(1));
            original.Bytes[0] = 42;

            var clone = original.Clone();
            clone.Bytes[0] = 99;

            Assert.Equal((byte)42, original.Bytes[0]);
        }

        [Fact]
        public void Clone_IsIndependent()
        {
            var original = new WoWDynamicObject(new HighGuid(1))
            {
                SpellId = 100,
                Radius = 5.0f
            };

            var clone = original.Clone();
            clone.SpellId = 999;
            clone.Radius = 0.0f;

            Assert.Equal(100u, original.SpellId);
            Assert.Equal(5.0f, original.Radius);
        }

        [Fact]
        public void Bytes_DefaultLength4()
        {
            var obj = new WoWDynamicObject(new HighGuid(1));
            Assert.Equal(4, obj.Bytes.Length);
        }
    }

    public class WoWLocalPetTests
    {
        [Fact]
        public void CanUse_TrueWhenHasBuff()
        {
            var pet = new WoWLocalPet(new HighGuid(1));
            pet.Buffs.Add(new Spell(17253, 0, "Bite", "", ""));
            Assert.True(pet.CanUse("Bite"));
        }

        [Fact]
        public void CanUse_TrueWhenNotCasting()
        {
            var pet = new WoWLocalPet(new HighGuid(1));
            // Not casting, no buff — the method checks HasBuff || !IsCasting
            // HasBuff("Claw") = false, !IsCasting = true → true
            Assert.True(pet.CanUse("Claw"));
        }

        [Fact]
        public void CanUse_FalseWhenCastingAndNoBuff()
        {
            var pet = new WoWLocalPet(new HighGuid(1))
            {
                SpellcastId = 100 // IsCasting = true
            };
            // HasBuff("Claw") = false, !IsCasting = false → false
            Assert.False(pet.CanUse("Claw"));
        }

        [Fact]
        public void CanUse_TrueWhenCastingButHasBuff()
        {
            var pet = new WoWLocalPet(new HighGuid(1))
            {
                SpellcastId = 100
            };
            pet.Buffs.Add(new Spell(16827, 0, "Claw", "", ""));
            // HasBuff("Claw") = true → short-circuits to true
            Assert.True(pet.CanUse("Claw"));
        }

        [Fact]
        public void IsHappy_AlwaysTrue()
        {
            var pet = new WoWLocalPet(new HighGuid(1));
            Assert.True(pet.IsHappy());
        }

        [Fact]
        public void Attack_DoesNotThrow()
        {
            var pet = new WoWLocalPet(new HighGuid(1));
            pet.Attack(); // Should be a no-op
        }

        [Fact]
        public void Cast_DoesNotThrow()
        {
            var pet = new WoWLocalPet(new HighGuid(1));
            pet.Cast("Growl"); // Should be a no-op
        }

        [Fact]
        public void FollowPlayer_DoesNotThrow()
        {
            var pet = new WoWLocalPet(new HighGuid(1));
            pet.FollowPlayer(); // Should be a no-op
        }
    }
}
