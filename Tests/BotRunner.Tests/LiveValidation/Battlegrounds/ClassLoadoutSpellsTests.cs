using System.Collections.Generic;
using System.Linq;
using GameData.Core.Constants;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// P3.7: verifies the explicit per-(class, race) spell roster that replaces
/// <c>.learn all_myclass</c> / <c>.learn all_myspells</c>. Every resolved ID
/// must correspond to the highest rank in <see cref="SpellData.SpellNameToIds"/>
/// so level-60 bots learn exactly what they'd know from normal class
/// progression, nothing more.
/// </summary>
public sealed class ClassLoadoutSpellsTests
{
    [Theory]
    [InlineData("Warrior", "Orc")]
    [InlineData("Shaman", "Tauren")]
    [InlineData("Druid", "Tauren")]
    [InlineData("Warlock", "Undead")]
    [InlineData("Priest", "Undead")]
    [InlineData("Mage", "Troll")]
    [InlineData("Paladin", "Human")]
    [InlineData("Rogue", "Orc")]
    [InlineData("Hunter", "Orc")]
    public void ResolveHighestRankClassSpellIds_ReturnsNonEmpty_AllKnownIds(string characterClass, string race)
    {
        var ids = ClassLoadoutSpells.ResolveHighestRankClassSpellIds(characterClass, race);

        Assert.NotEmpty(ids);

        // Every returned ID must correspond to a real spell from SpellData (via
        // the reverse lookup) OR be one of the weapon/armor proficiency IDs.
        var proficiencySet = new HashSet<uint>(ClassLoadoutSpells.WeaponAndArmorProficiencySpellIds);
        foreach (var id in ids)
        {
            if (proficiencySet.Contains(id))
                continue;
            var name = SpellData.GetSpellName(id);
            Assert.False(name == null, $"Spell id {id} for {characterClass}/{race} has no SpellData entry");
        }
    }

    [Fact]
    public void ResolveHighestRankClassSpellIds_ForWarrior_IncludesMortalStrikeHighestRank()
    {
        var ids = ClassLoadoutSpells.ResolveHighestRankClassSpellIds("Warrior", "Orc");

        // Mortal Strike ranks are [12294, 21551, 21552, 21553]; highest = 21553.
        var mortalStrikeHighestRank = SpellData.SpellNameToIds["Mortal Strike"].Last();
        Assert.Contains(mortalStrikeHighestRank, ids);
        Assert.DoesNotContain((uint)12294, ids); // Should NOT include the lower rank.
    }

    [Fact]
    public void ResolveHighestRankClassSpellIds_IncludesRaceRacial_ForKnownHordeRaces()
    {
        var orcIds = ClassLoadoutSpells.ResolveHighestRankClassSpellIds("Warrior", "Orc");
        Assert.Contains(SpellData.SpellNameToIds["Blood Fury"].Last(), orcIds);

        var taurenIds = ClassLoadoutSpells.ResolveHighestRankClassSpellIds("Shaman", "Tauren");
        Assert.Contains(SpellData.SpellNameToIds["War Stomp"].Last(), taurenIds);

        var trollIds = ClassLoadoutSpells.ResolveHighestRankClassSpellIds("Mage", "Troll");
        Assert.Contains(SpellData.SpellNameToIds["Berserking"].Last(), trollIds);

        var undeadIds = ClassLoadoutSpells.ResolveHighestRankClassSpellIds("Priest", "Undead");
        Assert.Contains(SpellData.SpellNameToIds["Cannibalize"].Last(), undeadIds);
    }

    [Fact]
    public void ResolveHighestRankClassSpellIds_IncludesWeaponAndArmorProficiencies()
    {
        var ids = ClassLoadoutSpells.ResolveHighestRankClassSpellIds("Warrior", "Orc");

        foreach (var proficiencyId in ClassLoadoutSpells.WeaponAndArmorProficiencySpellIds)
            Assert.Contains(proficiencyId, ids);
    }

    [Fact]
    public void ResolveHighestRankClassSpellIds_DeduplicatesIds()
    {
        var ids = ClassLoadoutSpells.ResolveHighestRankClassSpellIds("Mage", "Troll");
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void ResolveHighestRankClassSpellIds_ThrowsForUnknownClass()
    {
        Assert.Throws<System.InvalidOperationException>(
            () => ClassLoadoutSpells.ResolveHighestRankClassSpellIds("DeathKnight", "Orc"));
    }

    [Fact]
    public void ResolveHighestRankClassSpellIds_UnknownRace_StillReturnsClassRoster()
    {
        // Alliance races are currently not in the racial map — the resolver
        // must still return the class roster without throwing.
        var ids = ClassLoadoutSpells.ResolveHighestRankClassSpellIds("Paladin", "Human");
        Assert.NotEmpty(ids);
    }
}
