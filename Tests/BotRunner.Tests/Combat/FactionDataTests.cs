using GameData.Core.Constants;
using GameData.Core.Enums;

namespace BotRunner.Tests.Combat;

public class FactionDataTests
{
    // ======== Template Data ========

    [Fact]
    public void Templates_IsNotEmpty()
    {
        Assert.NotEmpty(FactionData.Templates);
    }

    [Fact]
    public void Templates_HasAllPlayerRaces()
    {
        // Human=1, Orc=2, Dwarf=3, NightElf=4, Undead=5, Tauren=6, Gnome=115, Troll=116
        uint[] playerTemplates = [1, 2, 3, 4, 5, 6, 115, 116];
        foreach (var id in playerTemplates)
            Assert.True(FactionData.Templates.ContainsKey(id), $"Missing player faction template {id}");
    }

    // ======== Hostile Creatures vs Players ========

    [Theory]
    [InlineData(2u, 14u)]   // Orc vs Creature (hostile to all players)
    [InlineData(1u, 14u)]   // Human vs Creature (hostile to all players)
    [InlineData(116u, 14u)] // Troll vs Creature (hostile to all players)
    [InlineData(115u, 14u)] // Gnome vs Creature (hostile to all players)
    public void GetReaction_HostileCreature_ReturnsHostile(uint playerFt, uint unitFt)
    {
        Assert.Equal(UnitReaction.Hostile, FactionData.GetReaction(playerFt, unitFt));
    }

    // ======== Neutral Creatures ========

    [Theory]
    [InlineData(2u, 7u)]    // Orc vs neutral creature
    [InlineData(1u, 7u)]    // Human vs neutral creature
    public void GetReaction_NeutralCreature_ReturnsNeutral(uint playerFt, uint unitFt)
    {
        Assert.Equal(UnitReaction.Neutral, FactionData.GetReaction(playerFt, unitFt));
    }

    // ======== Friendly NPCs (same faction) ========

    [Theory]
    [InlineData(2u, 85u)]   // Orc vs Orgrimmar Grunt (Horde city faction)
    [InlineData(5u, 85u)]   // Undead vs Orgrimmar Grunt
    [InlineData(6u, 85u)]   // Tauren vs Orgrimmar Grunt
    [InlineData(116u, 85u)] // Troll vs Orgrimmar Grunt
    public void GetReaction_HordeFriendlyNpc_ReturnsFriendly(uint playerFt, uint unitFt)
    {
        Assert.Equal(UnitReaction.Friendly, FactionData.GetReaction(playerFt, unitFt));
    }

    [Theory]
    [InlineData(1u, 85u)]   // Human vs Orgrimmar Grunt → hostile
    [InlineData(3u, 85u)]   // Dwarf vs Orgrimmar Grunt → hostile
    [InlineData(4u, 85u)]   // Night Elf vs Orgrimmar Grunt → hostile
    [InlineData(115u, 85u)] // Gnome vs Orgrimmar Grunt → hostile
    public void GetReaction_AllianceVsHordeNpc_ReturnsHostile(uint playerFt, uint unitFt)
    {
        Assert.Equal(UnitReaction.Hostile, FactionData.GetReaction(playerFt, unitFt));
    }

    // ======== Horde NPC types ========

    [Fact]
    public void GetReaction_OrcVsHordeGenericNpc_ReturnsFriendly()
    {
        // Template 29 = Horde generic NPC (faction 76, friendly_mask=4/Horde)
        Assert.Equal(UnitReaction.Friendly, FactionData.GetReaction(2, 29));
    }

    [Fact]
    public void GetReaction_HumanVsHordeGenericNpc_ReturnsHostile()
    {
        Assert.Equal(UnitReaction.Hostile, FactionData.GetReaction(1, 29));
    }

    // ======== Helper Methods ========

    [Fact]
    public void IsHostile_HostileCreature_ReturnsTrue()
    {
        Assert.True(FactionData.IsHostile(2, 14));
    }

    [Fact]
    public void IsHostile_NeutralCreature_ReturnsFalse()
    {
        Assert.False(FactionData.IsHostile(2, 7));
    }

    [Fact]
    public void IsFriendly_FriendlyNpc_ReturnsTrue()
    {
        Assert.True(FactionData.IsFriendly(2, 85));
    }

    [Fact]
    public void IsFriendly_HostileCreature_ReturnsFalse()
    {
        Assert.False(FactionData.IsFriendly(2, 14));
    }

    // ======== Edge Cases ========

    [Fact]
    public void GetReaction_UnknownFactionTemplate_ReturnsNeutral()
    {
        Assert.Equal(UnitReaction.Neutral, FactionData.GetReaction(2, 99999));
    }

    [Fact]
    public void GetReaction_ZeroFactionTemplate_ReturnsNeutral()
    {
        Assert.Equal(UnitReaction.Neutral, FactionData.GetReaction(2, 0));
    }

    [Fact]
    public void GetReaction_SameFaction_ReturnsFriendly()
    {
        // Orc player vs Orc player faction
        Assert.Equal(UnitReaction.Friendly, FactionData.GetReaction(2, 2));
    }
}
