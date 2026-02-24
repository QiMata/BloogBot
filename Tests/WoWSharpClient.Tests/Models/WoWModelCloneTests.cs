using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Models;

namespace WoWSharpClient.Tests.Models
{
    public class WoWModelCloneTests
    {
        // ======== WoWObject Clone ========

        [Fact]
        public void WoWObject_Clone_CopiesBaseProperties()
        {
            var original = new WoWObject(new HighGuid(42))
            {
                Entry = 100,
                ScaleX = 1.5f,
                Position = new Position(10, 20, 30),
                Facing = 3.14f,
                LastUpdated = 999
            };

            var clone = original.Clone();

            Assert.Equal(42UL, clone.Guid);
            Assert.Equal(100u, clone.Entry);
            Assert.Equal(1.5f, clone.ScaleX);
            Assert.Equal(10f, clone.Position.X);
            Assert.Equal(20f, clone.Position.Y);
            Assert.Equal(30f, clone.Position.Z);
            Assert.Equal(3.14f, clone.Facing);
            Assert.Equal(999u, clone.LastUpdated);
        }

        [Fact]
        public void WoWObject_Clone_IsIndependent()
        {
            var original = new WoWObject(new HighGuid(1))
            {
                Entry = 50,
                ScaleX = 2.0f
            };

            var clone = original.Clone();
            clone.Entry = 999;
            clone.ScaleX = 0.5f;

            Assert.Equal(50u, original.Entry);
            Assert.Equal(2.0f, original.ScaleX);
        }

        // ======== WoWItem Clone ========

        [Fact]
        public void WoWItem_Clone_CopiesItemProperties()
        {
            var original = new WoWItem(new HighGuid(100))
            {
                ItemId = 12345,
                Quantity = 5,
                StackCount = 10,
                MaxDurability = 100,
                RequiredLevel = 20,
                Durability = 80,
                Duration = 3600,
                Quality = ItemQuality.Rare,
                Name = "Test Sword"
            };

            var clone = (WoWItem)original.Clone();

            Assert.Equal(12345u, clone.ItemId);
            Assert.Equal(5u, clone.Quantity);
            Assert.Equal(10u, clone.StackCount);
            Assert.Equal(100u, clone.MaxDurability);
            Assert.Equal(20u, clone.RequiredLevel);
            Assert.Equal(80u, clone.Durability);
            Assert.Equal(3600u, clone.Duration);
            Assert.Equal(ItemQuality.Rare, clone.Quality);
        }

        [Fact]
        public void WoWItem_Clone_CopiesSpellCharges()
        {
            var original = new WoWItem(new HighGuid(100));
            original.SpellCharges[0] = 3;
            original.SpellCharges[2] = 5;

            var clone = (WoWItem)original.Clone();

            Assert.Equal(3u, clone.SpellCharges[0]);
            Assert.Equal(0u, clone.SpellCharges[1]);
            Assert.Equal(5u, clone.SpellCharges[2]);
        }

        [Fact]
        public void WoWItem_Clone_CopiesEnchantments()
        {
            var original = new WoWItem(new HighGuid(100));
            original.Enchantments[0] = 1000;
            original.Enchantments[5] = 2000;

            var clone = (WoWItem)original.Clone();

            Assert.Equal(1000u, clone.Enchantments[0]);
            Assert.Equal(2000u, clone.Enchantments[5]);
        }

        [Fact]
        public void WoWItem_Clone_IsIndependent()
        {
            var original = new WoWItem(new HighGuid(100))
            {
                StackCount = 20,
                Durability = 50
            };

            var clone = (WoWItem)original.Clone();
            clone.StackCount = 1;
            clone.Durability = 0;

            Assert.Equal(20u, original.StackCount);
            Assert.Equal(50u, original.Durability);
        }

        [Fact]
        public void WoWItem_ItemId_FallsBackToEntry()
        {
            var item = new WoWItem(new HighGuid(1))
            {
                Entry = 5678
            };
            // ItemId not set, should fall back to Entry
            Assert.Equal(5678u, item.ItemId);
        }

        [Fact]
        public void WoWItem_ItemId_ExplicitOverridesEntry()
        {
            var item = new WoWItem(new HighGuid(1))
            {
                Entry = 5678,
                ItemId = 9999
            };
            Assert.Equal(9999u, item.ItemId);
        }

        // ======== WoWGameObject Clone ========

        [Fact]
        public void WoWGameObject_Clone_CopiesProperties()
        {
            var original = new WoWGameObject(new HighGuid(200))
            {
                DisplayId = 42,
                Flags = 3,
                GoState = GOState.Ready,
                FactionTemplate = 1,
                Level = 60,
                Name = "Copper Vein"
            };

            var clone = (WoWGameObject)original.Clone();

            Assert.Equal(42u, clone.DisplayId);
            Assert.Equal(3u, clone.Flags);
            Assert.Equal(GOState.Ready, clone.GoState);
            Assert.Equal(1u, clone.FactionTemplate);
            Assert.Equal(60u, clone.Level);
            Assert.Equal("Copper Vein", clone.Name);
        }

        [Fact]
        public void WoWGameObject_Clone_CopiesRotation()
        {
            var original = new WoWGameObject(new HighGuid(200));
            original.Rotation[0] = 1.0f;
            original.Rotation[1] = 2.0f;

            var clone = (WoWGameObject)original.Clone();

            Assert.Equal(1.0f, clone.Rotation[0]);
            Assert.Equal(2.0f, clone.Rotation[1]);
        }

        [Fact]
        public void WoWGameObject_GetPointBehindUnit_CalculatesCorrectly()
        {
            var obj = new WoWGameObject(new HighGuid(1))
            {
                Position = new Position(100, 100, 50),
                Facing = 0 // Facing east
            };

            // Behind = facing + PI = facing west
            var behind = obj.GetPointBehindUnit(5.0f);

            // cos(PI) = -1, sin(PI) = 0
            Assert.Equal(95.0f, behind.X, 0.1f);
            Assert.Equal(100.0f, behind.Y, 0.1f);
            Assert.Equal(50.0f, behind.Z);
        }

        // ======== WoWUnit Clone ========

        [Fact]
        public void WoWUnit_Clone_CopiesCombatProperties()
        {
            var original = new WoWUnit(new HighGuid(300))
            {
                Health = 1000,
                MaxHealth = 2000,
                TargetGuid = 42,
                RunSpeed = 7.0f,
                WalkSpeed = 2.5f,
                SwimSpeed = 4.722f,
                CombatReach = 1.5f,
                BoundingRadius = 0.5f
            };

            var clone = original.Clone();

            Assert.Equal(1000u, clone.Health);
            Assert.Equal(2000u, clone.MaxHealth);
            Assert.Equal(42UL, clone.TargetGuid);
            Assert.Equal(7.0f, clone.RunSpeed);
            Assert.Equal(2.5f, clone.WalkSpeed);
            Assert.Equal(4.722f, clone.SwimSpeed);
            Assert.Equal(1.5f, clone.CombatReach);
            Assert.Equal(0.5f, clone.BoundingRadius);
        }

        [Fact]
        public void WoWUnit_Clone_CopiesFlags()
        {
            var original = new WoWUnit(new HighGuid(300))
            {
                UnitFlags = UnitFlags.UNIT_FLAG_IN_COMBAT | UnitFlags.UNIT_FLAG_PVP,
                MovementFlags = MovementFlags.MOVEFLAG_FORWARD,
                NpcFlags = NPCFlags.UNIT_NPC_FLAG_VENDOR
            };

            var clone = original.Clone();

            Assert.True(clone.UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_IN_COMBAT));
            Assert.True(clone.UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_PVP));
            Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, clone.MovementFlags);
            Assert.Equal(NPCFlags.UNIT_NPC_FLAG_VENDOR, clone.NpcFlags);
        }

        [Fact]
        public void WoWUnit_Clone_CopiesSplineData()
        {
            var original = new WoWUnit(new HighGuid(300))
            {
                SplineTimePassed = 5000,
                SplineDuration = 10000,
                SplineId = 42,
                SplineFinalPoint = new Position(100, 200, 300),
                SplineFinalDestination = new Position(400, 500, 600)
            };
            original.SplineNodes.Add(new Position(1, 2, 3));
            original.SplineNodes.Add(new Position(4, 5, 6));

            var clone = original.Clone();

            Assert.Equal(5000, clone.SplineTimePassed);
            Assert.Equal(10000, clone.SplineDuration);
            Assert.Equal(42u, clone.SplineId);
            Assert.Equal(100f, clone.SplineFinalPoint.X);
            Assert.Equal(400f, clone.SplineFinalDestination.X);
            Assert.Equal(2, clone.SplineNodes.Count);
        }

        [Fact]
        public void WoWUnit_Clone_CopiesStats()
        {
            var original = new WoWUnit(new HighGuid(300))
            {
                Strength = 100,
                Agility = 80,
                Stamina = 120,
                Intellect = 60,
                Spirit = 50,
                AttackPower = 250
            };

            var clone = original.Clone();

            Assert.Equal(100u, clone.Strength);
            Assert.Equal(80u, clone.Agility);
            Assert.Equal(120u, clone.Stamina);
            Assert.Equal(60u, clone.Intellect);
            Assert.Equal(50u, clone.Spirit);
            Assert.Equal(250u, clone.AttackPower);
        }

        [Fact]
        public void WoWUnit_IsInCombat_ReflectsFlags()
        {
            var unit = new WoWUnit(new HighGuid(1));
            Assert.False(unit.IsInCombat);

            unit.UnitFlags = UnitFlags.UNIT_FLAG_IN_COMBAT;
            Assert.True(unit.IsInCombat);
        }

        [Fact]
        public void WoWUnit_IsCasting_ReflectsSpellcastId()
        {
            var unit = new WoWUnit(new HighGuid(1));
            Assert.False(unit.IsCasting);

            unit.SpellcastId = 100;
            Assert.True(unit.IsCasting);
        }

        [Fact]
        public void WoWUnit_IsChanneling_ReflectsChannelingId()
        {
            var unit = new WoWUnit(new HighGuid(1));
            Assert.False(unit.IsChanneling);

            unit.ChannelingId = 50;
            Assert.True(unit.IsChanneling);
        }

        [Fact]
        public void WoWUnit_HasBuff_ChecksByName()
        {
            var unit = new WoWUnit(new HighGuid(1));
            Assert.False(unit.HasBuff("Mark of the Wild"));

            unit.Buffs.Add(new Spell(1126, 0, "Mark of the Wild", "", ""));
            Assert.True(unit.HasBuff("Mark of the Wild"));
            Assert.False(unit.HasBuff("Thorns"));
        }

        [Fact]
        public void WoWUnit_HasDebuff_ChecksByName()
        {
            var unit = new WoWUnit(new HighGuid(1));
            Assert.False(unit.HasDebuff("Rend"));

            unit.Debuffs.Add(new Spell(772, 0, "Rend", "", ""));
            Assert.True(unit.HasDebuff("Rend"));
        }

        [Fact]
        public void WoWUnit_GetPointBehindUnit_DefaultFacingAngle()
        {
            // FacingAngle defaults to 0 (facing east), so behind = west
            var unit = new WoWUnit(new HighGuid(1))
            {
                Position = new Position(0, 0, 10)
            };

            var behind = unit.GetPointBehindUnit(10.0f);

            // FacingAngle=0 → behind = PI → cos(PI)=-1, sin(PI)≈0
            Assert.Equal(-10.0f, behind.X, 0.1f);
            Assert.Equal(0.0f, behind.Y, 0.5f);
            Assert.Equal(10.0f, behind.Z);
        }

        [Fact]
        public void WoWUnit_Clone_IndependentBuffLists()
        {
            var original = new WoWUnit(new HighGuid(1));
            original.Buffs.Add(new Spell(1, 0, "Buff1", "", ""));

            var clone = original.Clone();
            clone.Buffs.Add(new Spell(2, 0, "Buff2", "", ""));

            Assert.Single(original.Buffs);
            Assert.Equal(2, clone.Buffs.Count);
        }

        [Fact]
        public void WoWUnit_Clone_IndependentSplineNodes()
        {
            var original = new WoWUnit(new HighGuid(1));
            original.SplineNodes.Add(new Position(1, 2, 3));

            var clone = original.Clone();
            clone.SplineNodes.Add(new Position(4, 5, 6));

            Assert.Single(original.SplineNodes);
            Assert.Equal(2, clone.SplineNodes.Count);
        }
    }
}
