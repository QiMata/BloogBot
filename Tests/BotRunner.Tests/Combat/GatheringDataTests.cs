using BotRunner.Combat;

namespace BotRunner.Tests.Combat
{
    public class GatheringDataTests
    {
        // ======== Mining Nodes ========

        [Fact]
        public void MiningNodes_Contains15Entries()
        {
            Assert.Equal(15, GatheringData.MiningNodes.Count);
        }

        [Theory]
        [InlineData("Copper Vein", 1)]
        [InlineData("Tin Vein", 65)]
        [InlineData("Silver Vein", 75)]
        [InlineData("Iron Deposit", 125)]
        [InlineData("Gold Vein", 155)]
        [InlineData("Mithril Deposit", 175)]
        [InlineData("Truesilver Deposit", 230)]
        [InlineData("Dark Iron Deposit", 230)]
        [InlineData("Small Thorium Vein", 245)]
        [InlineData("Rich Thorium Vein", 275)]
        public void MiningNodes_StandardNodes_CorrectSkillLevel(string name, int expectedSkill)
        {
            Assert.True(GatheringData.MiningNodes.ContainsKey(name));
            Assert.Equal(expectedSkill, GatheringData.MiningNodes[name]);
        }

        [Theory]
        [InlineData("Ooze Covered Silver Vein", 75)]
        [InlineData("Ooze Covered Gold Vein", 155)]
        [InlineData("Ooze Covered Mithril Deposit", 175)]
        [InlineData("Ooze Covered Rich Thorium Vein", 275)]
        [InlineData("Ooze Covered Truesilver Deposit", 230)]
        public void MiningNodes_OozeCoveredNodes_CorrectSkillLevel(string name, int expectedSkill)
        {
            Assert.True(GatheringData.MiningNodes.ContainsKey(name));
            Assert.Equal(expectedSkill, GatheringData.MiningNodes[name]);
        }

        [Theory]
        [InlineData("Fake Node")]
        [InlineData("Adamantite Deposit")]
        [InlineData("")]
        public void MiningNodes_UnknownNode_NotPresent(string name)
        {
            Assert.False(GatheringData.MiningNodes.ContainsKey(name));
        }

        // ======== Herbalism Nodes ========

        [Fact]
        public void HerbalismNodes_Contains28Entries()
        {
            Assert.Equal(28, GatheringData.HerbalismNodes.Count);
        }

        [Theory]
        [InlineData("Peacebloom", 1)]
        [InlineData("Silverleaf", 1)]
        [InlineData("Earthroot", 15)]
        [InlineData("Mageroyal", 50)]
        [InlineData("Briarthorn", 70)]
        [InlineData("Stranglekelp", 85)]
        [InlineData("Bruiseweed", 100)]
        [InlineData("Wild Steelbloom", 115)]
        [InlineData("Grave Moss", 120)]
        [InlineData("Kingsblood", 125)]
        [InlineData("Liferoot", 150)]
        [InlineData("Fadeleaf", 160)]
        [InlineData("Goldthorn", 170)]
        [InlineData("Khadgar's Whisker", 185)]
        [InlineData("Wintersbite", 195)]
        [InlineData("Firebloom", 205)]
        [InlineData("Purple Lotus", 210)]
        [InlineData("Arthas' Tears", 220)]
        [InlineData("Sungrass", 230)]
        [InlineData("Blindweed", 235)]
        [InlineData("Ghost Mushroom", 245)]
        [InlineData("Gromsblood", 250)]
        [InlineData("Golden Sansam", 260)]
        [InlineData("Dreamfoil", 270)]
        [InlineData("Mountain Silversage", 280)]
        [InlineData("Plaguebloom", 285)]
        [InlineData("Icecap", 290)]
        [InlineData("Black Lotus", 300)]
        public void HerbalismNodes_AllNodes_CorrectSkillLevel(string name, int expectedSkill)
        {
            Assert.True(GatheringData.HerbalismNodes.ContainsKey(name));
            Assert.Equal(expectedSkill, GatheringData.HerbalismNodes[name]);
        }

        [Theory]
        [InlineData("Felweed")]
        [InlineData("Fake Herb")]
        public void HerbalismNodes_UnknownNode_NotPresent(string name)
        {
            Assert.False(GatheringData.HerbalismNodes.ContainsKey(name));
        }

        // ======== Skill Level Ordering ========

        [Fact]
        public void MiningNodes_AllSkillLevelsInRange()
        {
            foreach (var kvp in GatheringData.MiningNodes)
            {
                Assert.InRange(kvp.Value, 1, 300);
            }
        }

        [Fact]
        public void HerbalismNodes_AllSkillLevelsInRange()
        {
            foreach (var kvp in GatheringData.HerbalismNodes)
            {
                Assert.InRange(kvp.Value, 1, 300);
            }
        }

        // ======== Skill Constants ========

        [Fact]
        public void MiningSkillId_Is186()
        {
            Assert.Equal(186u, GatheringData.MINING_SKILL_ID);
        }

        [Fact]
        public void HerbalismSkillId_Is182()
        {
            Assert.Equal(182u, GatheringData.HERBALISM_SKILL_ID);
        }

        [Fact]
        public void SkinningSkillId_Is393()
        {
            Assert.Equal(393u, GatheringData.SKINNING_SKILL_ID);
        }
    }
}
