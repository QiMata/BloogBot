using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Progression;

/// <summary>
/// Static database of key vanilla quest chains. Each chain is an ordered list of quest IDs
/// with NPC locations and prerequisites. Used by ProgressionPlanner to determine the next
/// quest step when a character has a quest chain goal.
///
/// Source: MaNGOS quest_template table + wowhead quest chain data.
/// </summary>
public static class QuestChainData
{
    public record QuestStep(
        uint QuestId,
        string QuestName,
        string QuestGiverNpc,
        uint MapId,
        float X, float Y, float Z);

    public record QuestChain(
        string ChainId,
        string DisplayName,
        string Faction,     // "Horde", "Alliance", "Both"
        List<QuestStep> Steps);

    public static readonly IReadOnlyDictionary<string, QuestChain> AllChains = BuildChains();

    public static QuestChain? GetChain(string chainId)
        => AllChains.TryGetValue(chainId, out var chain) ? chain : null;

    public static IReadOnlyList<QuestChain> GetByFaction(string faction)
        => AllChains.Values.Where(c => c.Faction == faction || c.Faction == "Both").ToList();

    private static Dictionary<string, QuestChain> BuildChains()
    {
        var chains = new Dictionary<string, QuestChain>
        {
            // ═══ Attunements ═══

            ["OnyxiaAttunementHorde"] = new("OnyxiaAttunementHorde", "Onyxia Attunement (Horde)", "Horde",
            [
                new(4741, "Warlord's Command", "Warlord Goretooth", 0, -7518f, -1224f, 286f),
                new(4903, "Eitrigg's Wisdom", "Eitrigg", 1, 1581f, -4420f, 6f),
                new(4941, "For The Horde!", "Thrall", 1, 1923f, -4141f, 40f),
                new(4974, "What the Wind Carries", "Thrall", 1, 1923f, -4141f, 40f),
                new(6566, "The Champion of the Horde", "Thrall", 1, 1923f, -4141f, 40f),
                new(6602, "Dragonkin Menace", "Marshal Maxwell", 0, -7534f, -1237f, 286f),
                new(6568, "Blood of the Black Dragon Champion", "Rokaro", 0, -7657f, -1233f, 287f),
            ]),

            ["MoltenCoreAttunement"] = new("MoltenCoreAttunement", "Molten Core Attunement", "Both",
            [
                new(7848, "Attunement to the Core", "Lothos Riftwaker", 0, -7462f, -1089f, 265f),
            ]),

            ["BWLAttunement"] = new("BWLAttunement", "Blackwing Lair Attunement", "Both",
            [
                new(7761, "Blackhand's Command", "Scarshield Quartermaster", 0, -7545f, -1216f, 286f),
            ]),

            // ═══ Class Quests ═══

            ["WarriorWhirlwindAxe"] = new("WarriorWhirlwindAxe", "Whirlwind Axe (Warrior)", "Horde",
            [
                new(1791, "The Windwatcher", "Bath'rah the Windwatcher", 0, -723f, -3739f, 12f),
                new(1792, "Cyclonian", "Bath'rah the Windwatcher", 0, -723f, -3739f, 12f),
            ]),

            ["WarlockMountHorde"] = new("WarlockMountHorde", "Dreadsteed (Warlock)", "Horde",
            [
                new(7562, "Mor'zul Bloodbringer", "Strahad Farsan", 1, -848f, -3750f, 14f),
                new(7563, "Rage of Blood", "Mor'zul Bloodbringer", 0, -11466f, -297f, 17f),
                new(7564, "Bell of Dethmoora", "Goraluk Anvilcrack", 0, -7608f, -1086f, 273f),
                new(7581, "Lord Banehollow", "Ulathek", 1, -4086f, 1277f, 132f),
                new(7583, "Dreadsteed of Xoroth", "Mor'zul Bloodbringer", 0, -11466f, -297f, 17f),
            ]),

            // ═══ Reputation Chains ═══

            ["ArgentDawnIntro"] = new("ArgentDawnIntro", "Argent Dawn Introduction", "Both",
            [
                new(5944, "Dispelling Evil", "Argent Officer Pureheart", 0, 2276f, -5271f, 82f),
                new(5862, "Light's Hope Chapel", "Commander Ashlam Valorfist", 0, 2276f, -5271f, 82f),
            ]),

            // ═══ Zone Quest Chains ═══

            ["DefiasBrotherhoodAlliance"] = new("DefiasBrotherhoodAlliance", "Defias Brotherhood", "Alliance",
            [
                new(65, "The Defias Brotherhood", "Gryan Stoutmantle", 0, -11452f, 1550f, 50f),
                new(132, "The Defias Brotherhood (2)", "Gryan Stoutmantle", 0, -11452f, 1550f, 50f),
                new(135, "The Defias Brotherhood (3)", "Gryan Stoutmantle", 0, -11452f, 1550f, 50f),
                new(141, "The Defias Brotherhood (4)", "Gryan Stoutmantle", 0, -11452f, 1550f, 50f),
                new(142, "The Defias Brotherhood (5)", "Gryan Stoutmantle", 0, -11452f, 1550f, 50f),
                new(155, "The Defias Kingpin", "Gryan Stoutmantle", 0, -11452f, 1550f, 50f),
                new(166, "Brotherhood's End", "Gryan Stoutmantle", 0, -11452f, 1550f, 50f),
            ]),
        };

        return chains;
    }
}
