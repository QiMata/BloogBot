using System.Collections.Generic;
using System.Linq;

namespace GameData.Core.Constants
{
    /// <summary>
    /// Static spell name → spell ID mapping for vanilla WoW 1.12.1.
    /// Maps each Spellbook constant to an array of spell IDs sorted by rank (lowest first).
    /// Used by the background bot for name→ID resolution since SMSG_INITIAL_SPELLS only provides IDs.
    /// </summary>
    public static class SpellData
    {
        /// <summary>
        /// Maps spell name → all rank spell IDs, sorted ascending by rank.
        /// Last element is the highest rank.
        /// </summary>
        public static readonly Dictionary<string, uint[]> SpellNameToIds = new()
        {
            // ── Warrior ──
            ["Battle Stance"] = [2457],
            ["Berserker Stance"] = [2458],
            ["Defensive Stance"] = [71],
            ["Battle Shout"] = [6673, 5242, 6192, 11549, 11550, 11551, 25289],
            ["Bloodrage"] = [2687],
            ["Charge"] = [100, 6178, 11578],
            ["Heroic Strike"] = [78, 284, 285, 1608, 11564, 11565, 11566, 11567, 25286],
            ["Rend"] = [772, 6546, 6547, 6548, 11572, 11573, 11574],
            ["Thunder Clap"] = [6343, 8198, 8204, 8205, 11580, 11581],
            ["Overpower"] = [7384, 7887, 11584, 11585],
            ["Execute"] = [5308, 20658, 20660, 20661, 20662],
            ["Mortal Strike"] = [12294, 21551, 21552, 21553],
            ["Sweeping Strikes"] = [12328],
            ["Slam"] = [1464, 8820, 11604, 11605],
            ["Whirlwind"] = [1680],
            ["Cleave"] = [845, 7369, 11608, 11609, 20569],
            ["Ham String"] = [1715, 7372, 7373],
            ["Berserker Rage"] = [18499],
            ["Bloodthirst"] = [23881, 23892, 23893, 23894],
            ["Pummel"] = [6552, 6554],
            ["Death Wish"] = [12292],
            ["Intimidating Shout"] = [5246],
            ["Sunder Armor"] = [7386, 7405, 8380, 11596, 11597],
            ["Demoralizing Shout"] = [1160, 6190, 11554, 11555, 11556],
            ["Retaliation"] = [20230],
            ["Shield Bash"] = [72, 1671, 1672],
            ["Shield Slam"] = [23922, 23923, 23924, 23925],
            ["Taunt"] = [355],
            ["Concussion Blow"] = [12809],
            ["Revenge"] = [6572, 6574, 7379, 11600, 11601, 25288],
            ["Last Stand"] = [12975],

            // ── Shaman ──
            ["Lightning Bolt"] = [403, 529, 548, 915, 943, 6041, 10391, 10392, 15207, 15208],
            ["Healing Wave"] = [331, 332, 547, 913, 939, 959, 8005, 10395, 10396, 25357],
            ["Earth Shock"] = [8042, 8044, 8045, 8046, 10412, 10413, 10414],
            ["Flame Shock"] = [8050, 8052, 8053, 10447, 10448, 29228],
            ["Lightning Shield"] = [324, 325, 905, 945, 8134, 10431, 10432],
            ["Rockbiter Weapon"] = [8017, 8018, 8019, 10399],
            ["Flametongue Weapon"] = [8024, 8027, 8030, 16339, 16341, 16342],
            ["Windfury Weapon"] = [8232, 8235, 10486, 16362],
            ["Searing Totem"] = [3599, 6363, 6364, 6365, 10437, 10438],
            ["Stoneclaw Totem"] = [5730, 6390, 6391, 6392, 10427, 10428],
            ["Stoneskin Totem"] = [8071, 8154, 8155, 10406, 10407, 10408],
            ["Mana Spring Totem"] = [5675, 10495, 10496, 10497],
            ["Grounding Totem"] = [8177],
            ["Tremor Totem"] = [8143],
            ["Stormstrike"] = [17364],
            ["Elemental Mastery"] = [16166],

            // ── Druid ──
            ["Bear Form"] = [5487],
            ["Cat Form"] = [768],
            ["Moonkin Form"] = [24858],
            ["Mark of the Wild"] = [1126, 5232, 6756, 5234, 8907, 9884, 9885, 26990],
            ["Thorns"] = [467, 782, 1075, 8914, 9756, 9910],
            ["Omen of Clarity"] = [16864],
            ["Healing Touch"] = [5185, 5186, 5187, 5188, 5189, 6778, 8903, 9758, 9888, 9889, 25297],
            ["Regrowth"] = [8936, 8938, 8939, 8940, 8941, 9750, 9856, 9857, 9858, 26980],
            ["Rejuvenation"] = [774, 1058, 1430, 2090, 2091, 3627, 8910, 9839, 9840, 9841, 25299],
            ["Barkskin"] = [22812],
            ["Abolish Poison"] = [2893],
            ["Entangling Roots"] = [339, 1062, 5195, 5196, 9852, 9853],
            ["Moonfire"] = [8921, 8924, 8925, 8926, 8927, 8928, 8929, 9833, 9834, 9835],
            ["Remove Curse"] = [2782],
            ["Wrath"] = [5176, 5177, 5178, 5179, 5180, 6780, 8905, 9912],
            ["Insect Swarm"] = [5570, 24974, 24975, 24976, 24977],
            ["Innervate"] = [29166],
            ["Maul"] = [6807, 6808, 6809, 8972, 9745, 9880, 9881],
            ["Enrage"] = [5229],
            ["Demoralizing Roar"] = [99, 1735, 9490, 9747, 9898],
            ["Claw"] = [1082, 3029, 5201, 9849, 9850],
            ["Rake"] = [1822, 1823, 1824, 9904],
            ["Rip"] = [1079, 9492, 9493, 9752, 9894, 9896],
            ["Ferocious Bite"] = [22568, 22827, 22828, 22829, 31018],
            ["Tiger's Fury"] = [5217, 6793, 9845, 9846],

            // ── Warlock ──
            ["Corruption"] = [172, 6222, 6223, 7648, 11671, 11672, 25311],
            ["Curse of Agony"] = [980, 1014, 6217, 11711, 11712, 11713],
            ["Shadow Bolt"] = [686, 695, 705, 1088, 1106, 7641, 11659, 11660, 11661, 25307],
            ["Immolate"] = [348, 707, 1094, 2941, 11665, 11667, 11668, 25309],
            ["Life Tap"] = [1454, 1455, 1456, 11687, 11688, 11689],
            ["Fear"] = [5782, 6213, 6215],
            ["Drain Soul"] = [1120, 8288, 8289, 11675],
            ["Siphon Life"] = [18265, 18879, 18880, 18881],
            ["Death Coil"] = [6789, 17925, 17926],
            ["Health Funnel"] = [755, 3698, 3699, 3700, 11693, 11694, 11695],
            ["Demon Armor"] = [706, 1086, 11733, 11734, 11735],
            ["Demon Skin"] = [687, 696],
            ["Summon Imp"] = [688],
            ["Summon Voidwalker"] = [697],
            ["Summon Succubus"] = [712],
            ["Summon Felhunter"] = [691],
            ["Conflagrate"] = [17962, 18930, 18931, 18932],
            ["Consume Shadows"] = [17767],
            ["Torment"] = [3716, 7809, 7810, 7811, 11774, 11775],
            ["Sacrifice"] = [7812, 19438, 19440, 19441, 19442, 19443],

            // ── Priest ──
            ["Power Word: Fortitude"] = [1243, 1244, 1245, 2791, 10937, 10938],
            ["Shadow Protection"] = [976, 10957, 10958],
            ["Power Word: Shield"] = [17, 592, 600, 3747, 6065, 6066, 10898, 10899, 10900, 10901],
            ["Renew"] = [139, 6074, 6075, 6076, 6077, 6078, 10927, 10928, 10929, 25315],
            ["Holy Fire"] = [14914, 15262, 15263, 15264, 15265, 15266, 15267],
            ["Mind Blast"] = [8092, 8102, 8103, 8104, 8105, 8106, 10945, 10946, 10947],
            ["Smite"] = [585, 591, 598, 984, 1004, 6060, 10933, 10934],
            ["Shadow Word: Pain"] = [589, 594, 970, 992, 2767, 10892, 10893, 10894],
            ["Inner Fire"] = [588, 7128, 602, 1006, 10951, 10952],
            ["Fade"] = [586, 9578, 9579, 9592, 10941, 10942],
            ["Psychic Scream"] = [8122, 8124, 10888, 10890],
            ["Mind Flay"] = [15407, 17311, 17312, 17313, 17314],
            ["Vampiric Embrace"] = [15286],
            ["Dispel Magic"] = [527, 988],
            ["Shadowform"] = [15473],
            ["Lesser Heal"] = [2050, 2052, 2053],
            ["Heal"] = [2054, 2055, 6063, 6064],
            ["Abolish Disease"] = [552],
            ["Cure Disease"] = [528],

            // ── Mage ──
            ["Arcane Intellect"] = [1459, 1460, 1461, 10156, 10157],
            ["Frost Armor"] = [168, 7300, 7301],
            ["Ice Armor"] = [7302, 7320, 10219, 10220],
            ["Dampen Magic"] = [604, 8450, 8451, 10173, 10174],
            ["Conjure Food"] = [587, 597, 990, 6129, 10144, 10145, 28612],
            ["Conjure Water"] = [5504, 5505, 5506, 6127, 10138, 10139, 10140],
            ["Fireball"] = [133, 143, 145, 3140, 8400, 8401, 8402, 10148, 10149, 10150, 10151, 25306],
            ["Frostbolt"] = [116, 205, 837, 7322, 8406, 8407, 8408, 10179, 10180, 10181, 25304],
            ["Arcane Missiles"] = [5143, 5144, 5145, 8416, 8417, 10211, 10212, 25345],
            ["Fire Blast"] = [2136, 2137, 2138, 8412, 8413, 10197, 10199],
            ["Frost Nova"] = [122, 865, 6131, 10230],
            ["Mana Shield"] = [1463, 8494, 8495, 10191, 10192, 10193],
            ["Arcane Explosion"] = [1449, 8437, 8438, 8439, 10201, 10202],
            ["Counterspell"] = [2139],
            ["Cone of Cold"] = [120, 8492, 10159, 10160, 10161],
            ["Flamestrike"] = [2120, 2121, 8422, 8423, 10215, 10216],
            ["Scorch"] = [2948, 8444, 8445, 8446, 10205, 10206, 10207],
            ["Evocation"] = [12051],
            ["Fire Ward"] = [543, 8457, 8458, 10223, 10225],
            ["Frost Ward"] = [6143, 8461, 8462, 10177, 28609],
            ["Mage Armor"] = [6117, 22782, 22783],
            ["Pyroblast"] = [11366, 12505, 12522, 12523, 12524, 12525, 12526, 18809],
            ["Combustion"] = [11129],
            ["Presence of Mind"] = [12043],
            ["Arcane Power"] = [12042],
            ["Cold Snap"] = [11958],
            ["Ice Barrier"] = [11426, 13031, 13032, 13033],

            // ── Paladin ──
            ["Blessing of Kings"] = [20217],
            ["Blessing of Might"] = [19740, 19834, 19835, 19836, 19837, 19838, 25291],
            ["Blessing of Sanctuary"] = [20911, 20912, 20913],
            ["Consecration"] = [26573, 20116, 20922, 20923, 20924],
            ["Devotion Aura"] = [465, 10290, 643, 10291, 1032, 10292, 10293],
            ["Exorcism"] = [879, 5614, 5615, 10312, 10313, 10314],
            ["Hammer of Justice"] = [853, 5588, 5589, 10308],
            ["Holy Light"] = [635, 639, 647, 1026, 1042, 3472, 10328, 10329, 25292],
            ["Holy Shield"] = [20925, 20927, 20928],
            ["Judgement"] = [20271],
            ["Lay on Hands"] = [633, 2800, 10310],
            ["Purify"] = [1152],
            ["Retribution Aura"] = [7294, 10298, 10299, 10300, 10301],
            ["Righteous Fury"] = [25780],
            ["Seal of Righteousness"] = [20154, 20287, 20288, 20289, 20290, 20291, 20292, 20293],
            ["Seal of the Crusader"] = [21082, 20162, 20305, 20306, 20307, 20308],
            ["Seal of Command"] = [20375, 20915, 20918, 20919, 20920],
            ["Divine Protection"] = [498, 5573],
            ["Sanctity Aura"] = [20218],

            // ── Rogue ──
            ["Sinister Strike"] = [1752, 1757, 1758, 1759, 1760, 8621, 11293, 11294],
            ["Eviscerate"] = [2098, 6760, 6761, 6762, 8623, 8624, 11299, 11300],
            ["Slice and Dice"] = [5171, 6774],
            ["Gouge"] = [1776],
            ["Evasion"] = [5277],
            ["Kick"] = [1766, 1767, 1768, 1769],
            ["Stealth"] = [1784, 1785, 1786, 1787],
            ["Cheap Shot"] = [1833],
            ["Ambush"] = [8676, 8724, 8725, 11267, 11268, 11269],
            ["Garrote"] = [703, 8631, 8632, 8633, 11289, 11290],
            ["Riposte"] = [14251],
            ["Kidney Shot"] = [408, 8643],
            ["Expose Armor"] = [8647, 8649, 8650, 11197, 11198],
            ["Distract"] = [1725],
            ["Blind"] = [2094],
            ["Blade Flurry"] = [13877],
            ["Adrenaline Rush"] = [13750],
            ["Ghostly Strike"] = [14278],

            // ── Hunter ──
            ["Raptor Strike"] = [2973, 14260, 14261, 14262, 14263, 14264],
            ["Arcane Shot"] = [3044, 14281, 14282, 14283, 14284, 14285, 14286, 14287],
            ["Serpent Sting"] = [1978, 13549, 13550, 13551, 13552, 13553, 13554, 13555, 25295],
            ["Multi-Shot"] = [2643, 14288, 14289, 14290, 25294],
            ["Immolation Trap"] = [13795, 14302, 14303, 14304, 14305],
            ["Mongoose Bite"] = [1495, 14269, 14270, 14271],
            ["Hunter's Mark"] = [1130, 14323, 14324, 14325],
            ["Rapid Fire"] = [3045],
            ["Concussive Shot"] = [5116],
            ["Scare Beast"] = [1513, 14326, 14327],
            ["Aspect of the Hawk"] = [13165, 14318, 14319, 14320, 14321, 14322, 25296],
            ["Call Pet"] = [883],
            ["Mend Pet"] = [136, 3111, 3661, 3662, 13542, 13543, 13544],
            ["Distracting Shot"] = [20736],
            ["Wing Clip"] = [2974, 14267, 14268],
            ["Aspect Of The Monkey"] = [13163],
            ["Aspect Of The Cheetah"] = [5118],
            ["Revive Pet"] = [982],
            ["Feed Pet"] = [6991],
            ["Parry"] = [3127],

            // ── Racials ──
            ["War Stomp"] = [20549],
            ["Blood Fury"] = [20572],
            ["Berserking"] = [20554],
            ["Cannibalize"] = [20577],

            // ── Forms ──
            ["Human Form"] = [2457], // Same as Battle Stance for druids leaving form
        };

        /// <summary>
        /// Reverse lookup: spell ID → spell name. Built lazily from SpellNameToIds.
        /// </summary>
        private static Dictionary<uint, string> _idToName;
        private static readonly object _lock = new();

        /// <summary>
        /// Get the spell name for a given spell ID, or null if unknown.
        /// </summary>
        public static string GetSpellName(uint spellId)
        {
            EnsureReverseLookup();
            return _idToName.TryGetValue(spellId, out var name) ? name : null;
        }

        /// <summary>
        /// Given a spell name and the player's known spell IDs, return the highest-rank
        /// spell ID the player knows for that spell, or 0 if unknown.
        /// </summary>
        public static uint GetHighestKnownRank(string spellName, IEnumerable<uint> knownSpellIds)
        {
            if (!SpellNameToIds.TryGetValue(spellName, out var rankIds))
                return 0;

            var knownSet = knownSpellIds is HashSet<uint> hs ? hs : new HashSet<uint>(knownSpellIds);

            // rankIds is sorted ascending by rank; iterate from highest to lowest
            for (int i = rankIds.Length - 1; i >= 0; i--)
            {
                if (knownSet.Contains(rankIds[i]))
                    return rankIds[i];
            }

            return 0;
        }

        private static void EnsureReverseLookup()
        {
            if (_idToName != null) return;
            lock (_lock)
            {
                if (_idToName != null) return;
                _idToName = new Dictionary<uint, string>();
                foreach (var kvp in SpellNameToIds)
                {
                    foreach (var id in kvp.Value)
                    {
                        _idToName.TryAdd(id, kvp.Key);
                    }
                }
            }
        }
    }
}
