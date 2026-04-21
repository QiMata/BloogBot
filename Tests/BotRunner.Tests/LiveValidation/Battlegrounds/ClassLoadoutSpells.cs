using System;
using System.Collections.Generic;
using System.Linq;
using GameData.Core.Constants;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// P3.7: explicit per-class / per-race spell rosters for a level-60 loadout
/// hand-off. Replaces the previous <c>.learn all_myclass</c> /
/// <c>.learn all_myspells</c> fixture shortcuts — those catch-alls taught
/// spells that were never part of the intended rotation (wrong-spec talents,
/// debug utility spells, etc.). Every entry here resolves to a specific
/// highest-rank spell ID via <see cref="SpellData.SpellNameToIds"/>.
///
/// Rosters are intentionally conservative: they include the combat rotation,
/// core class buffs, defensive cooldowns, and class utility that the bot
/// profiles actually cast. Talent-gated spells (e.g. Stormstrike, Mortal
/// Strike, Conflagrate, Shadowform) are included so the relevant specs can
/// use them once a talent plan is applied.
/// </summary>
internal static class ClassLoadoutSpells
{
    /// <summary>
    /// Weapon / armor proficiency spells. A level-60 .levelup'd bot doesn't
    /// auto-learn all of these from class trainers, so we teach them
    /// explicitly. Harmless to learn something the class can't use — the
    /// spell just sits in the spellbook unused.
    /// </summary>
    internal static readonly uint[] WeaponAndArmorProficiencySpellIds =
    {
        750,   // Plate Mail
        8737,  // Mail
        9077,  // Cloth
        9078,  // Leather
        9116,  // Shield
        196,   // One-Handed Axes
        197,   // Two-Handed Axes
        198,   // One-Handed Maces
        199,   // Two-Handed Maces
        200,   // Polearms
        201,   // One-Handed Swords
        202,   // Two-Handed Swords
        227,   // Staves
        264,   // Bows
        266,   // Guns
        1180,  // Daggers
        2567,  // Thrown
        5009,  // Wands
        5011,  // Crossbows
        15590, // Fist Weapons
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ClassCoreSpellNames
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Warrior"] = new[]
            {
                "Battle Stance", "Berserker Stance", "Defensive Stance",
                "Battle Shout", "Bloodrage", "Charge", "Heroic Strike", "Rend",
                "Thunder Clap", "Overpower", "Execute", "Mortal Strike",
                "Sweeping Strikes", "Slam", "Whirlwind", "Cleave", "Ham String",
                "Berserker Rage", "Bloodthirst", "Pummel", "Death Wish",
                "Intimidating Shout", "Sunder Armor", "Demoralizing Shout",
                "Retaliation", "Shield Bash", "Shield Slam", "Taunt",
                "Concussion Blow", "Revenge", "Last Stand",
            },
            ["Shaman"] = new[]
            {
                "Lightning Bolt", "Healing Wave", "Earth Shock", "Flame Shock",
                "Lightning Shield", "Rockbiter Weapon", "Flametongue Weapon",
                "Windfury Weapon", "Searing Totem", "Stoneclaw Totem",
                "Stoneskin Totem", "Mana Spring Totem", "Grounding Totem",
                "Tremor Totem", "Stormstrike", "Elemental Mastery",
            },
            ["Druid"] = new[]
            {
                "Bear Form", "Cat Form", "Moonkin Form", "Mark of the Wild",
                "Thorns", "Omen of Clarity", "Healing Touch", "Regrowth",
                "Rejuvenation", "Barkskin", "Abolish Poison", "Entangling Roots",
                "Moonfire", "Remove Curse", "Wrath", "Insect Swarm", "Innervate",
                "Maul", "Enrage", "Demoralizing Roar", "Claw", "Rake", "Rip",
                "Ferocious Bite", "Tiger's Fury",
            },
            ["Warlock"] = new[]
            {
                "Corruption", "Curse of Agony", "Shadow Bolt", "Immolate",
                "Life Tap", "Fear", "Drain Soul", "Siphon Life", "Death Coil",
                "Health Funnel", "Demon Armor", "Demon Skin", "Summon Imp",
                "Summon Voidwalker", "Summon Succubus", "Summon Felhunter",
                "Conflagrate", "Consume Shadows", "Torment", "Sacrifice",
            },
            ["Priest"] = new[]
            {
                "Power Word: Fortitude", "Shadow Protection", "Power Word: Shield",
                "Renew", "Holy Fire", "Mind Blast", "Smite", "Shadow Word: Pain",
                "Inner Fire", "Fade", "Psychic Scream", "Mind Flay",
                "Vampiric Embrace", "Dispel Magic", "Shadowform", "Lesser Heal",
                "Heal", "Abolish Disease", "Cure Disease",
            },
            ["Mage"] = new[]
            {
                "Arcane Intellect", "Frost Armor", "Ice Armor", "Dampen Magic",
                "Conjure Food", "Conjure Water", "Fireball", "Frostbolt",
                "Arcane Missiles", "Fire Blast", "Frost Nova", "Mana Shield",
                "Arcane Explosion", "Counterspell", "Cone of Cold", "Flamestrike",
                "Scorch", "Evocation", "Fire Ward", "Frost Ward", "Mage Armor",
                "Pyroblast", "Combustion", "Presence of Mind", "Arcane Power",
                "Cold Snap", "Ice Barrier",
            },
            ["Paladin"] = new[]
            {
                "Blessing of Kings", "Blessing of Might", "Blessing of Sanctuary",
                "Consecration", "Devotion Aura", "Exorcism", "Hammer of Justice",
                "Holy Light", "Holy Shield", "Judgement", "Lay on Hands", "Purify",
                "Retribution Aura", "Righteous Fury", "Seal of Righteousness",
                "Seal of the Crusader", "Seal of Command", "Divine Protection",
                "Sanctity Aura",
            },
            ["Rogue"] = new[]
            {
                "Sinister Strike", "Eviscerate", "Slice and Dice", "Gouge",
                "Evasion", "Kick", "Stealth", "Cheap Shot", "Ambush", "Garrote",
                "Riposte", "Kidney Shot", "Expose Armor", "Distract", "Blind",
                "Blade Flurry", "Adrenaline Rush", "Ghostly Strike",
            },
            ["Hunter"] = new[]
            {
                "Raptor Strike", "Arcane Shot", "Serpent Sting", "Multi-Shot",
                "Immolation Trap", "Mongoose Bite", "Hunter's Mark", "Rapid Fire",
                "Concussive Shot", "Scare Beast", "Aspect of the Hawk", "Call Pet",
                "Mend Pet", "Distracting Shot", "Wing Clip",
                "Aspect Of The Monkey", "Aspect Of The Cheetah", "Revive Pet",
                "Feed Pet", "Parry",
            },
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RaceRacialSpellNames
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orc"] = new[] { "Blood Fury" },
            ["Tauren"] = new[] { "War Stomp" },
            ["Troll"] = new[] { "Berserking" },
            ["Undead"] = new[] { "Cannibalize" },
            // Alliance racials (Human perception, NightElf Shadowmeld, Dwarf Stoneform,
            // Gnome Escape Artist) are not currently cast by any bot profile, so we
            // don't teach them. Add here if/when a profile needs one.
        };

    /// <summary>
    /// Resolve the explicit list of highest-rank spell IDs this bot's class +
    /// race should know for a level-60 loadout. Each ID comes straight from
    /// <see cref="SpellData.SpellNameToIds"/> (highest rank = last entry).
    /// Deterministic + deduplicated; order matches the declared rosters.
    /// Throws if the class is unknown so spec gaps fail loud rather than
    /// silently falling back to <c>.learn all_myclass</c>.
    /// </summary>
    internal static IReadOnlyList<uint> ResolveHighestRankClassSpellIds(
        string characterClass,
        string characterRace)
    {
        if (string.IsNullOrWhiteSpace(characterClass))
            throw new ArgumentException("Character class required", nameof(characterClass));

        if (!ClassCoreSpellNames.TryGetValue(characterClass, out var classSpells))
            throw new InvalidOperationException(
                $"No class spell roster curated for '{characterClass}'. Add one to ClassLoadoutSpells rather than falling back to '.learn all_myclass'.");

        var resolvedIds = new List<uint>(classSpells.Count + WeaponAndArmorProficiencySpellIds.Length + 4);
        var seen = new HashSet<uint>();

        foreach (var spellName in classSpells)
            AppendHighestRankId(spellName, resolvedIds, seen);

        if (!string.IsNullOrWhiteSpace(characterRace)
            && RaceRacialSpellNames.TryGetValue(characterRace, out var racials))
        {
            foreach (var racial in racials)
                AppendHighestRankId(racial, resolvedIds, seen);
        }

        foreach (var proficiency in WeaponAndArmorProficiencySpellIds)
        {
            if (seen.Add(proficiency))
                resolvedIds.Add(proficiency);
        }

        return resolvedIds;
    }

    private static void AppendHighestRankId(string spellName, List<uint> resolved, HashSet<uint> seen)
    {
        if (!SpellData.SpellNameToIds.TryGetValue(spellName, out var ranks) || ranks.Length == 0)
        {
            throw new InvalidOperationException(
                $"SpellData has no entry for '{spellName}'. Add the spell to SpellData.SpellNameToIds before referencing it in ClassLoadoutSpells.");
        }

        // ranks is ordered ascending by rank — last element is the highest rank
        // (what a level-60 character would have learned).
        var highestRankId = ranks[ranks.Length - 1];
        if (seen.Add(highestRankId))
            resolved.Add(highestRankId);
    }
}
