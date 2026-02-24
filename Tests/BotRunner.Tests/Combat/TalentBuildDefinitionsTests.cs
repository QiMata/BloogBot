using BotRunner.Combat;

namespace BotRunner.Tests.Combat
{
    public class TalentBuildDefinitionsTests
    {
        /// <summary>All 27 class/spec names that should have builds defined.</summary>
        public static IEnumerable<object[]> AllSpecNames =>
        [
            ["Arms Warrior"], ["Fury Warrior"], ["Protection Warrior"],
            ["Enhancement Shaman"], ["Elemental Shaman"], ["Restoration Shaman"],
            ["Frost Mage"], ["Fire Mage"], ["Arcane Mage"],
            ["Combat Rogue"], ["Assassination Rogue"], ["Subtlety Rogue"],
            ["Shadow Priest"], ["Holy Priest"], ["Discipline Priest"],
            ["Affliction Warlock"], ["Demonology Warlock"], ["Destruction Warlock"],
            ["Beast Mastery Hunter"], ["Marksmanship Hunter"], ["Survival Hunter"],
            ["Retribution Paladin"], ["Holy Paladin"], ["Protection Paladin"],
            ["Feral Combat Druid"], ["Balance Druid"], ["Restoration Druid"]
        ];

        [Theory]
        [MemberData(nameof(AllSpecNames))]
        public void GetBuildOrder_ReturnsNonNull(string specName)
        {
            var build = TalentBuildDefinitions.GetBuildOrder(specName);
            Assert.NotNull(build);
        }

        [Theory]
        [MemberData(nameof(AllSpecNames))]
        public void GetBuildOrder_HasAtLeast31Points(string specName)
        {
            // Every build should have at least 31 points (enough for a 31-point talent)
            var build = TalentBuildDefinitions.GetBuildOrder(specName)!;
            Assert.True(build.Length >= 31,
                $"Spec '{specName}' has only {build.Length} talent points (minimum 31 expected)");
        }

        [Theory]
        [MemberData(nameof(AllSpecNames))]
        public void GetBuildOrder_HasAtMost51Points(string specName)
        {
            // Can't have more than 51 points (level 10-60)
            var build = TalentBuildDefinitions.GetBuildOrder(specName)!;
            Assert.True(build.Length <= 51,
                $"Spec '{specName}' has {build.Length} talent points (maximum 51)");
        }

        [Theory]
        [MemberData(nameof(AllSpecNames))]
        public void GetBuildOrder_AllTabsInRange(string specName)
        {
            var build = TalentBuildDefinitions.GetBuildOrder(specName)!;
            for (int i = 0; i < build.Length; i++)
            {
                Assert.InRange(build[i].tab, 0u, 2u);
            }
        }

        [Theory]
        [MemberData(nameof(AllSpecNames))]
        public void GetBuildOrder_AllPositionsInRange(string specName)
        {
            var build = TalentBuildDefinitions.GetBuildOrder(specName)!;
            for (int i = 0; i < build.Length; i++)
            {
                // Vanilla talent trees have max ~20 talent slots per tab
                Assert.InRange(build[i].pos, 0u, 20u);
            }
        }

        [Theory]
        [MemberData(nameof(AllSpecNames))]
        public void GetBuildOrder_NoMoreThan5PointsPerTalent(string specName)
        {
            var build = TalentBuildDefinitions.GetBuildOrder(specName)!;

            // Count points per (tab, pos) pair — no talent can have more than 5 points
            var pointCounts = new Dictionary<(uint tab, uint pos), int>();
            foreach (var (tab, pos) in build)
            {
                var key = (tab, pos);
                pointCounts.TryGetValue(key, out int count);
                pointCounts[key] = count + 1;
            }

            foreach (var kvp in pointCounts)
            {
                Assert.True(kvp.Value <= 5,
                    $"Spec '{specName}': talent ({kvp.Key.tab}, {kvp.Key.pos}) has {kvp.Value} points (max 5)");
            }
        }

        [Fact]
        public void GetBuildOrder_UnknownSpec_ReturnsNull()
        {
            var build = TalentBuildDefinitions.GetBuildOrder("Unknown Spec");
            Assert.Null(build);
        }

        [Fact]
        public void GetBuildOrder_FeralDruid_AliasWorks()
        {
            // "Feral Druid" is an alias for "Feral Combat Druid"
            var feral = TalentBuildDefinitions.GetBuildOrder("Feral Druid");
            var feralCombat = TalentBuildDefinitions.GetBuildOrder("Feral Combat Druid");

            Assert.NotNull(feral);
            Assert.NotNull(feralCombat);
            Assert.Equal(feral, feralCombat);
        }

        [Fact]
        public void GetBuildOrder_All27SpecsExist()
        {
            int count = 0;
            foreach (var specData in AllSpecNames)
            {
                string specName = (string)specData[0];
                var build = TalentBuildDefinitions.GetBuildOrder(specName);
                if (build != null) count++;
            }
            Assert.Equal(27, count);
        }
    }
}
