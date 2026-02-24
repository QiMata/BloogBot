using System.Collections.Generic;

namespace BotRunner.Combat;

/// <summary>
/// Static database of vanilla 1.12.1 gathering node names and required skill levels.
/// Used by the gathering system to detect and gather herbs/ores while grinding.
/// </summary>
public static class GatheringData
{
    /// <summary>
    /// Mining node names mapped to required Mining skill level.
    /// </summary>
    public static readonly Dictionary<string, int> MiningNodes = new()
    {
        // Copper (1)
        ["Copper Vein"] = 1,
        // Tin (65)
        ["Tin Vein"] = 65,
        // Silver (75)
        ["Silver Vein"] = 75,
        // Iron (125)
        ["Iron Deposit"] = 125,
        // Gold (155)
        ["Gold Vein"] = 155,
        // Mithril (175)
        ["Mithril Deposit"] = 175,
        // Truesilver (230)
        ["Truesilver Deposit"] = 230,
        // Dark Iron (230)
        ["Dark Iron Deposit"] = 230,
        // Small Thorium (245)
        ["Small Thorium Vein"] = 245,
        // Rich Thorium (275)
        ["Rich Thorium Vein"] = 275,
        // Ooze Covered nodes
        ["Ooze Covered Silver Vein"] = 75,
        ["Ooze Covered Gold Vein"] = 155,
        ["Ooze Covered Mithril Deposit"] = 175,
        ["Ooze Covered Rich Thorium Vein"] = 275,
        ["Ooze Covered Truesilver Deposit"] = 230,
    };

    /// <summary>
    /// Herbalism node names mapped to required Herbalism skill level.
    /// </summary>
    public static readonly Dictionary<string, int> HerbalismNodes = new()
    {
        // Tier 1 (1-50)
        ["Peacebloom"] = 1,
        ["Silverleaf"] = 1,
        ["Earthroot"] = 15,
        // Tier 2 (50-100)
        ["Mageroyal"] = 50,
        ["Briarthorn"] = 70,
        ["Stranglekelp"] = 85,
        ["Bruiseweed"] = 100,
        // Tier 3 (100-150)
        ["Wild Steelbloom"] = 115,
        ["Grave Moss"] = 120,
        ["Kingsblood"] = 125,
        ["Liferoot"] = 150,
        // Tier 4 (150-200)
        ["Fadeleaf"] = 160,
        ["Goldthorn"] = 170,
        ["Khadgar's Whisker"] = 185,
        ["Wintersbite"] = 195,
        // Tier 5 (200-250)
        ["Firebloom"] = 205,
        ["Purple Lotus"] = 210,
        ["Arthas' Tears"] = 220,
        ["Sungrass"] = 230,
        ["Blindweed"] = 235,
        ["Ghost Mushroom"] = 245,
        ["Gromsblood"] = 250,
        // Tier 6 (250-300)
        ["Golden Sansam"] = 260,
        ["Dreamfoil"] = 270,
        ["Mountain Silversage"] = 280,
        ["Plaguebloom"] = 285,
        ["Icecap"] = 290,
        ["Black Lotus"] = 300,
    };

    /// <summary>Mining skill ID for SkillInfo lookup.</summary>
    public const uint MINING_SKILL_ID = 186;

    /// <summary>Herbalism skill ID for SkillInfo lookup.</summary>
    public const uint HERBALISM_SKILL_ID = 182;

    /// <summary>Skinning skill ID for SkillInfo lookup.</summary>
    public const uint SKINNING_SKILL_ID = 393;

    /// <summary>
    /// Mining gathering spell (cast on mining nodes to trigger SPELL_EFFECT_OPEN_LOCK).
    /// Spell 2575 = "Mining" — the profession spell that acts as the channeled gather spell.
    /// The real WoW client casts this (3.2s channel) when right-clicking a mining node.
    /// Note: spell 2656 is a different instant-effect mining spell that does NOT trigger loot.
    /// </summary>
    public const uint MINING_GATHER_SPELL = 2575;

    /// <summary>
    /// Herbalism gathering spell (cast on herb nodes to trigger SPELL_EFFECT_OPEN_LOCK).
    /// The WoW client sends CMSG_CAST_SPELL with this AFTER CMSG_GAMEOBJ_USE.
    /// </summary>
    public const uint HERBALISM_GATHER_SPELL = 2366;
}
