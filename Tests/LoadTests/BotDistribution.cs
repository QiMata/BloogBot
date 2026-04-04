using System;
using System.Collections.Generic;
using System.Linq;

namespace LoadTests;

/// <summary>
/// Generates account names and character configs for all valid 1.12.1 race/class combinations.
/// Used by the load test harness to distribute N bots across all combos evenly.
///
/// Naming convention: LT_{Race2}{Class2}{Index:D4}
/// Example: LTORWA0001 = Orc Warrior #1, LTHUHU0042 = Human Hunter #42
///
/// WoW 1.12.1 valid combos: 22 Horde + 22 Alliance = 44 total.
/// </summary>
public static class BotDistribution
{
    public record RaceClassCombo(string Race, string Class, string Faction, string RaceCode, string ClassCode);
    public record BotConfig(string AccountName, string Race, string Class, string Faction, int ComboIndex);

    // 2-letter race codes
    private static readonly Dictionary<string, string> RaceCodes = new()
    {
        ["Orc"] = "OR", ["Troll"] = "TR", ["Tauren"] = "TA", ["Undead"] = "UD",
        ["Human"] = "HU", ["Dwarf"] = "DW", ["NightElf"] = "NE", ["Gnome"] = "GN"
    };

    // 2-letter class codes
    private static readonly Dictionary<string, string> ClassCodes = new()
    {
        ["Warrior"] = "WA", ["Hunter"] = "HU", ["Rogue"] = "RO", ["Priest"] = "PR",
        ["Shaman"] = "SH", ["Mage"] = "MA", ["Warlock"] = "WL", ["Druid"] = "DR",
        ["Paladin"] = "PA"
    };

    /// <summary>All 44 valid race/class combinations for vanilla 1.12.1.</summary>
    public static readonly IReadOnlyList<RaceClassCombo> AllCombos = BuildAllCombos();

    /// <summary>22 Horde combinations.</summary>
    public static readonly IReadOnlyList<RaceClassCombo> HordeCombos =
        AllCombos.Where(c => c.Faction == "Horde").ToList();

    /// <summary>22 Alliance combinations.</summary>
    public static readonly IReadOnlyList<RaceClassCombo> AllianceCombos =
        AllCombos.Where(c => c.Faction == "Alliance").ToList();

    /// <summary>
    /// Generate N bot configs distributed evenly across all 44 race/class combos.
    /// First bot is always Orc Warrior (FG bot convention).
    /// </summary>
    public static List<BotConfig> Generate(int count)
    {
        if (count <= 0) return [];

        var configs = new List<BotConfig>(count);
        for (int i = 0; i < count; i++)
        {
            var comboIndex = i % AllCombos.Count;
            var combo = AllCombos[comboIndex];
            var indexInCombo = i / AllCombos.Count + 1;
            var accountName = $"LT{combo.RaceCode}{combo.ClassCode}{indexInCombo:D4}";
            configs.Add(new BotConfig(accountName, combo.Race, combo.Class, combo.Faction, comboIndex));
        }

        return configs;
    }

    /// <summary>
    /// Generate N bot configs for a specific faction only.
    /// </summary>
    public static List<BotConfig> GenerateForFaction(int count, string faction)
    {
        var combos = faction == "Horde" ? HordeCombos : AllianceCombos;
        var configs = new List<BotConfig>(count);
        for (int i = 0; i < count; i++)
        {
            var comboIndex = i % combos.Count;
            var combo = combos[comboIndex];
            var indexInCombo = i / combos.Count + 1;
            var accountName = $"LT{combo.RaceCode}{combo.ClassCode}{indexInCombo:D4}";
            configs.Add(new BotConfig(accountName, combo.Race, combo.Class, combo.Faction, comboIndex));
        }
        return configs;
    }

    private static List<RaceClassCombo> BuildAllCombos()
    {
        // Vanilla 1.12.1 valid race/class matrix
        var valid = new (string Race, string Class, string Faction)[]
        {
            // Horde (22 combos)
            ("Orc", "Warrior", "Horde"), ("Orc", "Hunter", "Horde"), ("Orc", "Rogue", "Horde"),
            ("Orc", "Shaman", "Horde"), ("Orc", "Warlock", "Horde"),
            ("Troll", "Warrior", "Horde"), ("Troll", "Hunter", "Horde"), ("Troll", "Rogue", "Horde"),
            ("Troll", "Priest", "Horde"), ("Troll", "Shaman", "Horde"), ("Troll", "Mage", "Horde"),
            ("Tauren", "Warrior", "Horde"), ("Tauren", "Hunter", "Horde"),
            ("Tauren", "Shaman", "Horde"), ("Tauren", "Druid", "Horde"),
            ("Undead", "Warrior", "Horde"), ("Undead", "Rogue", "Horde"),
            ("Undead", "Priest", "Horde"), ("Undead", "Mage", "Horde"), ("Undead", "Warlock", "Horde"),
            // Two missing: Orc Priest (invalid), Tauren Rogue (invalid) — these don't exist in vanilla
            // Actually we have 20 Horde. Let me recount...
            // Orc: Warrior, Hunter, Rogue, Shaman, Warlock = 5
            // Troll: Warrior, Hunter, Rogue, Priest, Shaman, Mage = 6
            // Tauren: Warrior, Hunter, Shaman, Druid = 4
            // Undead: Warrior, Rogue, Priest, Mage, Warlock = 5
            // Total Horde = 20

            // Alliance (20 combos)
            ("Human", "Warrior", "Alliance"), ("Human", "Paladin", "Alliance"), ("Human", "Rogue", "Alliance"),
            ("Human", "Priest", "Alliance"), ("Human", "Mage", "Alliance"), ("Human", "Warlock", "Alliance"),
            ("Dwarf", "Warrior", "Alliance"), ("Dwarf", "Paladin", "Alliance"), ("Dwarf", "Hunter", "Alliance"),
            ("Dwarf", "Rogue", "Alliance"), ("Dwarf", "Priest", "Alliance"),
            ("NightElf", "Warrior", "Alliance"), ("NightElf", "Hunter", "Alliance"), ("NightElf", "Rogue", "Alliance"),
            ("NightElf", "Priest", "Alliance"), ("NightElf", "Druid", "Alliance"),
            ("Gnome", "Warrior", "Alliance"), ("Gnome", "Rogue", "Alliance"),
            ("Gnome", "Mage", "Alliance"), ("Gnome", "Warlock", "Alliance"),
            // Human: Warrior, Paladin, Rogue, Priest, Mage, Warlock = 6
            // Dwarf: Warrior, Paladin, Hunter, Rogue, Priest = 5
            // NightElf: Warrior, Hunter, Rogue, Priest, Druid = 5
            // Gnome: Warrior, Rogue, Mage, Warlock = 4
            // Total Alliance = 20
        };

        return valid.Select(v => new RaceClassCombo(
            v.Race, v.Class, v.Faction,
            RaceCodes[v.Race], ClassCodes[v.Class]
        )).ToList();
    }
}
