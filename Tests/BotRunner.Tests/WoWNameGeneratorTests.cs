using BotRunner;
using GameData.Core.Enums;

namespace BotRunner.Tests
{
    public class WoWNameGeneratorTests
    {
        // ======== GenerateName ========

        [Theory]
        [InlineData(Race.Human, Gender.Male)]
        [InlineData(Race.Human, Gender.Female)]
        [InlineData(Race.Dwarf, Gender.Male)]
        [InlineData(Race.Dwarf, Gender.Female)]
        [InlineData(Race.NightElf, Gender.Male)]
        [InlineData(Race.NightElf, Gender.Female)]
        [InlineData(Race.Gnome, Gender.Male)]
        [InlineData(Race.Gnome, Gender.Female)]
        [InlineData(Race.Orc, Gender.Male)]
        [InlineData(Race.Orc, Gender.Female)]
        [InlineData(Race.Undead, Gender.Male)]
        [InlineData(Race.Undead, Gender.Female)]
        [InlineData(Race.Tauren, Gender.Male)]
        [InlineData(Race.Tauren, Gender.Female)]
        [InlineData(Race.Troll, Gender.Male)]
        [InlineData(Race.Troll, Gender.Female)]
        public void GenerateName_AllRaceGenderCombos_ReturnsNonEmpty(Race race, Gender gender)
        {
            var name = WoWNameGenerator.GenerateName(race, gender);
            Assert.False(string.IsNullOrEmpty(name));
            Assert.True(name.Length >= 3, $"Name '{name}' too short for {race} {gender}");
        }

        [Theory]
        [InlineData(Race.Human, Gender.Male)]
        [InlineData(Race.Orc, Gender.Female)]
        public void GenerateName_ReturnsVariedNames(Race race, Gender gender)
        {
            var names = new HashSet<string>();
            for (int i = 0; i < 50; i++)
            {
                names.Add(WoWNameGenerator.GenerateName(race, gender));
            }
            // Should produce at least a few different names out of 50 attempts
            Assert.True(names.Count > 1, $"Generated only {names.Count} unique name(s) in 50 attempts");
        }

        // ======== ParseRaceCode ========

        [Theory]
        [InlineData("HU", Race.Human)]
        [InlineData("DW", Race.Dwarf)]
        [InlineData("NE", Race.NightElf)]
        [InlineData("GN", Race.Gnome)]
        [InlineData("OR", Race.Orc)]
        [InlineData("UD", Race.Undead)]
        [InlineData("TA", Race.Tauren)]
        [InlineData("TR", Race.Troll)]
        public void ParseRaceCode_ValidCodes_ReturnsCorrectRace(string code, Race expectedRace)
        {
            Assert.Equal(expectedRace, WoWNameGenerator.ParseRaceCode(code));
        }

        [Theory]
        [InlineData("hu")]
        [InlineData("dw")]
        [InlineData("ne")]
        public void ParseRaceCode_CaseInsensitive(string code)
        {
            // Should not throw — case-insensitive comparison
            var race = WoWNameGenerator.ParseRaceCode(code);
            Assert.True(Enum.IsDefined(typeof(Race), race));
        }

        [Theory]
        [InlineData("XX")]
        [InlineData("")]
        [InlineData("HUMAN")]
        public void ParseRaceCode_InvalidCode_Throws(string code)
        {
            Assert.Throws<ArgumentException>(() => WoWNameGenerator.ParseRaceCode(code));
        }

        // ======== ParseClassCode ========

        [Theory]
        [InlineData("WR", Class.Warrior)]
        [InlineData("PA", Class.Paladin)]
        [InlineData("RO", Class.Rogue)]
        [InlineData("HU", Class.Hunter)]
        [InlineData("MA", Class.Mage)]
        [InlineData("WL", Class.Warlock)]
        [InlineData("PR", Class.Priest)]
        [InlineData("DR", Class.Druid)]
        [InlineData("SH", Class.Shaman)]
        public void ParseClassCode_ValidCodes_ReturnsCorrectClass(string code, Class expectedClass)
        {
            Assert.Equal(expectedClass, WoWNameGenerator.ParseClassCode(code));
        }

        [Theory]
        [InlineData("wr")]
        [InlineData("pa")]
        public void ParseClassCode_CaseInsensitive(string code)
        {
            var cls = WoWNameGenerator.ParseClassCode(code);
            Assert.True(Enum.IsDefined(typeof(Class), cls));
        }

        [Theory]
        [InlineData("XX")]
        [InlineData("")]
        public void ParseClassCode_InvalidCode_Throws(string code)
        {
            Assert.Throws<ArgumentException>(() => WoWNameGenerator.ParseClassCode(code));
        }

        // ======== DetermineGender ========

        [Theory]
        [InlineData(Class.Mage, Gender.Male)]
        [InlineData(Class.Warlock, Gender.Male)]
        [InlineData(Class.Priest, Gender.Male)]
        [InlineData(Class.Druid, Gender.Male)]
        [InlineData(Class.Shaman, Gender.Male)]
        public void DetermineGender_CasterClasses_ReturnsMale(Class cls, Gender expectedGender)
        {
            Assert.Equal(expectedGender, WoWNameGenerator.DetermineGender(cls));
        }

        [Theory]
        [InlineData(Class.Warrior, Gender.Female)]
        [InlineData(Class.Paladin, Gender.Female)]
        [InlineData(Class.Rogue, Gender.Female)]
        [InlineData(Class.Hunter, Gender.Female)]
        public void DetermineGender_MeleeClasses_ReturnsFemale(Class cls, Gender expectedGender)
        {
            Assert.Equal(expectedGender, WoWNameGenerator.DetermineGender(cls));
        }

        // ======== SyllableData ========

        [Fact]
        public void SyllableData_Has16Entries()
        {
            // 8 races × 2 genders = 16 entries
            Assert.Equal(16, WoWNameGenerator.SyllableData.All.Count);
        }

        [Theory]
        [InlineData("HumanMale")]
        [InlineData("HumanFemale")]
        [InlineData("DwarfMale")]
        [InlineData("DwarfFemale")]
        [InlineData("NightElfMale")]
        [InlineData("NightElfFemale")]
        [InlineData("GnomeMale")]
        [InlineData("GnomeFemale")]
        [InlineData("OrcMale")]
        [InlineData("OrcFemale")]
        [InlineData("UndeadMale")]
        [InlineData("UndeadFemale")]
        [InlineData("TaurenMale")]
        [InlineData("TaurenFemale")]
        [InlineData("TrollMale")]
        [InlineData("TrollFemale")]
        public void SyllableData_AllKeysHaveNonEmptyLists(string key)
        {
            Assert.True(WoWNameGenerator.SyllableData.All.ContainsKey(key));
            var (prefixes, middles, suffixes) = WoWNameGenerator.SyllableData.All[key];
            Assert.NotEmpty(prefixes);
            Assert.NotEmpty(middles);
            Assert.NotEmpty(suffixes);
        }
    }
}
