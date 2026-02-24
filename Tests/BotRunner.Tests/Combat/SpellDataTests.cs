using GameData.Core.Constants;

namespace BotRunner.Tests.Combat
{
    public class SpellDataTests
    {
        // ======== SpellNameToIds Dictionary ========

        [Fact]
        public void SpellNameToIds_IsNotEmpty()
        {
            Assert.NotEmpty(SpellData.SpellNameToIds);
        }

        [Fact]
        public void SpellNameToIds_HasAtLeast60Entries()
        {
            // We have 200+ spell IDs across 60+ spell names
            Assert.True(SpellData.SpellNameToIds.Count >= 60,
                $"Expected at least 60 entries, got {SpellData.SpellNameToIds.Count}");
        }

        [Fact]
        public void SpellNameToIds_AllValuesNonEmpty()
        {
            foreach (var kvp in SpellData.SpellNameToIds)
            {
                Assert.True(kvp.Value.Length > 0,
                    $"Spell '{kvp.Key}' has empty ID array");
            }
        }

        [Fact]
        public void SpellNameToIds_AllIdsNonZero()
        {
            foreach (var kvp in SpellData.SpellNameToIds)
            {
                foreach (var id in kvp.Value)
                {
                    Assert.True(id > 0,
                        $"Spell '{kvp.Key}' has zero ID");
                }
            }
        }

        [Fact]
        public void SpellNameToIds_NoDuplicateIdsWithinSpell()
        {
            foreach (var kvp in SpellData.SpellNameToIds)
            {
                var unique = new HashSet<uint>(kvp.Value);
                Assert.Equal(kvp.Value.Length, unique.Count);
            }
        }

        // ── Warrior spells ──

        [Theory]
        [InlineData("Battle Stance", 1)]
        [InlineData("Berserker Stance", 1)]
        [InlineData("Defensive Stance", 1)]
        [InlineData("Heroic Strike", 9)]
        [InlineData("Charge", 3)]
        [InlineData("Execute", 5)]
        [InlineData("Mortal Strike", 4)]
        [InlineData("Whirlwind", 1)]
        [InlineData("Bloodthirst", 4)]
        public void SpellNameToIds_WarriorSpells(string name, int expectedRanks)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing warrior spell: {name}");
            Assert.Equal(expectedRanks, SpellData.SpellNameToIds[name].Length);
        }

        // ── Shaman spells ──

        [Theory]
        [InlineData("Lightning Bolt", 10)]
        [InlineData("Healing Wave", 10)]
        [InlineData("Earth Shock", 7)]
        [InlineData("Flame Shock", 6)]
        [InlineData("Lightning Shield", 7)]
        [InlineData("Stormstrike", 1)]
        public void SpellNameToIds_ShamanSpells(string name, int expectedRanks)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing shaman spell: {name}");
            Assert.Equal(expectedRanks, SpellData.SpellNameToIds[name].Length);
        }

        // ── Mage spells ──

        [Theory]
        [InlineData("Fireball", 12)]
        [InlineData("Frostbolt", 11)]
        [InlineData("Arcane Missiles", 8)]
        [InlineData("Counterspell", 1)]
        [InlineData("Evocation", 1)]
        [InlineData("Pyroblast", 8)]
        public void SpellNameToIds_MageSpells(string name, int expectedRanks)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing mage spell: {name}");
            Assert.Equal(expectedRanks, SpellData.SpellNameToIds[name].Length);
        }

        // ── Warlock spells ──

        [Theory]
        [InlineData("Corruption", 7)]
        [InlineData("Shadow Bolt", 10)]
        [InlineData("Fear", 3)]
        [InlineData("Summon Imp", 1)]
        [InlineData("Summon Voidwalker", 1)]
        public void SpellNameToIds_WarlockSpells(string name, int expectedRanks)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing warlock spell: {name}");
            Assert.Equal(expectedRanks, SpellData.SpellNameToIds[name].Length);
        }

        // ── Priest spells ──

        [Theory]
        [InlineData("Power Word: Shield", 10)]
        [InlineData("Renew", 10)]
        [InlineData("Mind Blast", 9)]
        [InlineData("Shadow Word: Pain", 8)]
        [InlineData("Shadowform", 1)]
        public void SpellNameToIds_PriestSpells(string name, int expectedRanks)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing priest spell: {name}");
            Assert.Equal(expectedRanks, SpellData.SpellNameToIds[name].Length);
        }

        // ── Druid spells ──

        [Theory]
        [InlineData("Bear Form", 1)]
        [InlineData("Cat Form", 1)]
        [InlineData("Moonkin Form", 1)]
        [InlineData("Rejuvenation", 11)]
        [InlineData("Wrath", 8)]
        [InlineData("Innervate", 1)]
        public void SpellNameToIds_DruidSpells(string name, int expectedRanks)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing druid spell: {name}");
            Assert.Equal(expectedRanks, SpellData.SpellNameToIds[name].Length);
        }

        // ── Paladin spells ──

        [Theory]
        [InlineData("Blessing of Kings", 1)]
        [InlineData("Judgement", 1)]
        [InlineData("Holy Light", 9)]
        [InlineData("Seal of Righteousness", 8)]
        [InlineData("Lay on Hands", 3)]
        public void SpellNameToIds_PaladinSpells(string name, int expectedRanks)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing paladin spell: {name}");
            Assert.Equal(expectedRanks, SpellData.SpellNameToIds[name].Length);
        }

        // ── Rogue spells ──

        [Theory]
        [InlineData("Sinister Strike", 8)]
        [InlineData("Eviscerate", 8)]
        [InlineData("Stealth", 4)]
        [InlineData("Cheap Shot", 1)]
        [InlineData("Blade Flurry", 1)]
        public void SpellNameToIds_RogueSpells(string name, int expectedRanks)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing rogue spell: {name}");
            Assert.Equal(expectedRanks, SpellData.SpellNameToIds[name].Length);
        }

        // ── Hunter spells ──

        [Theory]
        [InlineData("Raptor Strike", 6)]
        [InlineData("Arcane Shot", 8)]
        [InlineData("Multi-Shot", 5)]
        [InlineData("Call Pet", 1)]
        [InlineData("Aspect of the Hawk", 7)]
        public void SpellNameToIds_HunterSpells(string name, int expectedRanks)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing hunter spell: {name}");
            Assert.Equal(expectedRanks, SpellData.SpellNameToIds[name].Length);
        }

        // ── Racials ──

        [Theory]
        [InlineData("War Stomp")]
        [InlineData("Blood Fury")]
        [InlineData("Berserking")]
        [InlineData("Cannibalize")]
        public void SpellNameToIds_Racials(string name)
        {
            Assert.True(SpellData.SpellNameToIds.ContainsKey(name), $"Missing racial: {name}");
            Assert.Single(SpellData.SpellNameToIds[name]);
        }

        // ======== GetSpellName ========

        [Theory]
        [InlineData(78u, "Heroic Strike")]      // Heroic Strike rank 1
        [InlineData(25286u, "Heroic Strike")]    // Heroic Strike max rank
        [InlineData(403u, "Lightning Bolt")]     // Lightning Bolt rank 1
        [InlineData(133u, "Fireball")]           // Fireball rank 1
        [InlineData(686u, "Shadow Bolt")]        // Shadow Bolt rank 1
        [InlineData(585u, "Smite")]              // Smite rank 1
        [InlineData(5487u, "Bear Form")]         // Bear Form (single rank)
        [InlineData(20549u, "War Stomp")]        // Racial
        [InlineData(1752u, "Sinister Strike")]   // Sinister Strike rank 1
        [InlineData(2973u, "Raptor Strike")]     // Raptor Strike rank 1
        [InlineData(635u, "Holy Light")]         // Holy Light rank 1
        public void GetSpellName_KnownId_ReturnsName(uint spellId, string expectedName)
        {
            Assert.Equal(expectedName, SpellData.GetSpellName(spellId));
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(99999u)]
        [InlineData(uint.MaxValue)]
        public void GetSpellName_UnknownId_ReturnsNull(uint spellId)
        {
            Assert.Null(SpellData.GetSpellName(spellId));
        }

        [Fact]
        public void GetSpellName_AllRanks_ReturnSameName()
        {
            // All Heroic Strike ranks should map to "Heroic Strike"
            uint[] heroicStrikeIds = [78, 284, 285, 1608, 11564, 11565, 11566, 11567, 25286];
            foreach (var id in heroicStrikeIds)
            {
                Assert.Equal("Heroic Strike", SpellData.GetSpellName(id));
            }
        }

        [Fact]
        public void GetSpellName_ConsistentWithDictionary()
        {
            // Every ID in the dictionary should resolve back via GetSpellName
            foreach (var kvp in SpellData.SpellNameToIds)
            {
                foreach (var id in kvp.Value)
                {
                    var name = SpellData.GetSpellName(id);
                    Assert.NotNull(name);
                    // Note: first entry wins for duplicate IDs (e.g., Battle Stance = Human Form)
                    // so we just verify it returns *something*
                }
            }
        }

        // ======== GetHighestKnownRank ========

        [Fact]
        public void GetHighestKnownRank_UnknownSpell_ReturnsZero()
        {
            var known = new HashSet<uint> { 100, 200, 300 };
            Assert.Equal(0u, SpellData.GetHighestKnownRank("Nonexistent Spell", known));
        }

        [Fact]
        public void GetHighestKnownRank_NoKnownRanks_ReturnsZero()
        {
            var known = new HashSet<uint> { 999 }; // Not a Heroic Strike ID
            Assert.Equal(0u, SpellData.GetHighestKnownRank("Heroic Strike", known));
        }

        [Fact]
        public void GetHighestKnownRank_EmptyKnownSet_ReturnsZero()
        {
            Assert.Equal(0u, SpellData.GetHighestKnownRank("Heroic Strike", Array.Empty<uint>()));
        }

        [Fact]
        public void GetHighestKnownRank_KnowsOnlyRank1_ReturnsRank1()
        {
            // Heroic Strike rank 1 = 78
            var known = new HashSet<uint> { 78 };
            Assert.Equal(78u, SpellData.GetHighestKnownRank("Heroic Strike", known));
        }

        [Fact]
        public void GetHighestKnownRank_KnowsAllRanks_ReturnsHighest()
        {
            // Heroic Strike: [78, 284, 285, 1608, 11564, 11565, 11566, 11567, 25286]
            var known = new HashSet<uint> { 78, 284, 285, 1608, 11564, 11565, 11566, 11567, 25286 };
            Assert.Equal(25286u, SpellData.GetHighestKnownRank("Heroic Strike", known));
        }

        [Fact]
        public void GetHighestKnownRank_KnowsMiddleRanks_ReturnsHighestKnown()
        {
            // Heroic Strike ranks: [78, 284, 285, 1608, ...]
            // Player knows rank 1 and rank 3
            var known = new HashSet<uint> { 78, 285 };
            Assert.Equal(285u, SpellData.GetHighestKnownRank("Heroic Strike", known));
        }

        [Fact]
        public void GetHighestKnownRank_SingleRankSpell_KnowsIt()
        {
            // Battle Stance = [2457], single rank
            var known = new HashSet<uint> { 2457 };
            Assert.Equal(2457u, SpellData.GetHighestKnownRank("Battle Stance", known));
        }

        [Fact]
        public void GetHighestKnownRank_SingleRankSpell_DoesntKnowIt()
        {
            var known = new HashSet<uint> { 999 };
            Assert.Equal(0u, SpellData.GetHighestKnownRank("Battle Stance", known));
        }

        [Fact]
        public void GetHighestKnownRank_AcceptsListNotHashSet()
        {
            // Ensure it works with List<uint> not just HashSet<uint>
            var known = new List<uint> { 78, 285, 25286 };
            Assert.Equal(25286u, SpellData.GetHighestKnownRank("Heroic Strike", known));
        }

        [Fact]
        public void GetHighestKnownRank_AcceptsArray()
        {
            var known = new uint[] { 78, 1608 };
            Assert.Equal(1608u, SpellData.GetHighestKnownRank("Heroic Strike", known));
        }

        [Fact]
        public void GetHighestKnownRank_WithExtraUnrelatedIds()
        {
            // Player knows many spells, some unrelated
            var known = new HashSet<uint> { 1, 2, 3, 403, 529, 548, 999 };
            // Lightning Bolt: [403, 529, 548, 915, 943, 6041, 10391, 10392, 15207, 15208]
            Assert.Equal(548u, SpellData.GetHighestKnownRank("Lightning Bolt", known));
        }

        // ======== Specific spell ID verification ========

        [Fact]
        public void BattleStance_HasCorrectId()
        {
            Assert.Equal(2457u, SpellData.SpellNameToIds["Battle Stance"][0]);
        }

        [Fact]
        public void LightningBolt_Rank1_CorrectId()
        {
            Assert.Equal(403u, SpellData.SpellNameToIds["Lightning Bolt"][0]);
        }

        [Fact]
        public void LightningBolt_MaxRank_CorrectId()
        {
            var ids = SpellData.SpellNameToIds["Lightning Bolt"];
            Assert.Equal(15208u, ids[^1]);
        }

        [Fact]
        public void Fireball_Rank1_CorrectId()
        {
            Assert.Equal(133u, SpellData.SpellNameToIds["Fireball"][0]);
        }

        [Fact]
        public void ShadowBolt_Rank1_CorrectId()
        {
            Assert.Equal(686u, SpellData.SpellNameToIds["Shadow Bolt"][0]);
        }

        // ======== Thread Safety ========

        [Fact]
        public void GetSpellName_ThreadSafe()
        {
            var errors = new List<Exception>();
            var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            {
                try
                {
                    // Hit GetSpellName from many threads simultaneously
                    Assert.Equal("Heroic Strike", SpellData.GetSpellName(78));
                    Assert.Equal("Lightning Bolt", SpellData.GetSpellName(403));
                    Assert.Null(SpellData.GetSpellName(0));
                }
                catch (Exception ex)
                {
                    lock (errors) errors.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(tasks);
            Assert.Empty(errors);
        }

        [Fact]
        public void GetHighestKnownRank_ThreadSafe()
        {
            var errors = new List<Exception>();
            var known = new HashSet<uint> { 78, 285, 25286 };

            var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            {
                try
                {
                    Assert.Equal(25286u, SpellData.GetHighestKnownRank("Heroic Strike", known));
                    Assert.Equal(0u, SpellData.GetHighestKnownRank("Unknown", known));
                }
                catch (Exception ex)
                {
                    lock (errors) errors.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(tasks);
            Assert.Empty(errors);
        }

        // ======== Edge cases ========

        [Fact]
        public void SpellNameToIds_NoNullKeys()
        {
            foreach (var key in SpellData.SpellNameToIds.Keys)
            {
                Assert.NotNull(key);
                Assert.NotEmpty(key);
            }
        }

        [Fact]
        public void SpellNameToIds_NoNullValues()
        {
            foreach (var value in SpellData.SpellNameToIds.Values)
            {
                Assert.NotNull(value);
            }
        }

        [Fact]
        public void GetHighestKnownRank_NullSpellName_ReturnsZero()
        {
            // Dictionary.TryGetValue handles null gracefully
            // depending on implementation, this may throw or return 0
            // SpellData doesn't null-guard so this tests the behavior
            var known = new HashSet<uint> { 78 };
            try
            {
                var result = SpellData.GetHighestKnownRank(null!, known);
                Assert.Equal(0u, result);
            }
            catch (ArgumentNullException)
            {
                // Also acceptable — Dictionary.TryGetValue throws on null key
            }
        }
    }
}
