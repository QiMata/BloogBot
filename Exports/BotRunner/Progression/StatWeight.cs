using System;
using System.Collections.Generic;

namespace BotRunner.Progression;

/// <summary>
/// Defines per-stat multipliers for a given class/spec combination.
/// Used by <see cref="ItemScorer"/> to compute a single numeric score for any item.
/// </summary>
public record StatWeightProfile(
    string SpecName,
    float Strength,
    float Agility,
    float Stamina,
    float Intellect,
    float Spirit,
    float AttackPower,
    float SpellDamage,
    float HealingPower,
    float CritRating,
    float HitRating,
    float DodgeRating,
    float ParryRating,
    float BlockRating,
    float Armor,
    float DpsWeight,
    float ManaPerFive,
    float HealthPerFive);

/// <summary>
/// Static registry of stat weight profiles for all 27 Vanilla WoW class/spec combinations.
/// </summary>
public static class StatWeights
{
    private static readonly Dictionary<string, StatWeightProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Warriors ──
        ["WarriorFury"] = new StatWeightProfile(
            SpecName: "WarriorFury",
            Strength: 1.0f, Agility: 0.5f, Stamina: 0.3f, Intellect: 0f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.8f, HitRating: 1.2f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 2.0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["WarriorArms"] = new StatWeightProfile(
            SpecName: "WarriorArms",
            Strength: 1.0f, Agility: 0.4f, Stamina: 0.4f, Intellect: 0f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 1.0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 1.8f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["WarriorProtection"] = new StatWeightProfile(
            SpecName: "WarriorProtection",
            Strength: 0.3f, Agility: 0f, Stamina: 1.0f, Intellect: 0f, Spirit: 0f,
            AttackPower: 0f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0f, HitRating: 0f,
            DodgeRating: 0.7f, ParryRating: 0.6f, BlockRating: 0.5f,
            Armor: 0.8f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        // ── Rogues ──
        ["RogueCombat"] = new StatWeightProfile(
            SpecName: "RogueCombat",
            Strength: 0.5f, Agility: 1.0f, Stamina: 0.3f, Intellect: 0f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.8f, HitRating: 1.2f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 2.0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["RogueAssassination"] = new StatWeightProfile(
            SpecName: "RogueAssassination",
            Strength: 0.4f, Agility: 1.0f, Stamina: 0.3f, Intellect: 0f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.9f, HitRating: 1.0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 1.8f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["RogueSubtlety"] = new StatWeightProfile(
            SpecName: "RogueSubtlety",
            Strength: 0.4f, Agility: 1.0f, Stamina: 0.3f, Intellect: 0f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 0.8f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 1.5f, ManaPerFive: 0f, HealthPerFive: 0f),

        // ── Hunters ──
        ["HunterBeastMastery"] = new StatWeightProfile(
            SpecName: "HunterBeastMastery",
            Strength: 0f, Agility: 1.0f, Stamina: 0.3f, Intellect: 0.2f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 1.0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 1.5f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["HunterMarksmanship"] = new StatWeightProfile(
            SpecName: "HunterMarksmanship",
            Strength: 0f, Agility: 1.0f, Stamina: 0.3f, Intellect: 0.2f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.8f, HitRating: 1.0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 1.5f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["HunterSurvival"] = new StatWeightProfile(
            SpecName: "HunterSurvival",
            Strength: 0f, Agility: 1.0f, Stamina: 0.4f, Intellect: 0.2f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 0.8f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        // ── Mages ──
        ["MageArcane"] = new StatWeightProfile(
            SpecName: "MageArcane",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 1.0f, Spirit: 0.2f,
            AttackPower: 0f, SpellDamage: 0.8f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 1.0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0.4f, HealthPerFive: 0f),

        ["MageFire"] = new StatWeightProfile(
            SpecName: "MageFire",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 0.8f, Spirit: 0f,
            AttackPower: 0f, SpellDamage: 1.0f, HealingPower: 0f,
            CritRating: 0.9f, HitRating: 1.0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["MageFrost"] = new StatWeightProfile(
            SpecName: "MageFrost",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 0.8f, Spirit: 0.2f,
            AttackPower: 0f, SpellDamage: 1.0f, HealingPower: 0f,
            CritRating: 0.6f, HitRating: 0.8f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        // ── Priests ──
        ["PriestShadow"] = new StatWeightProfile(
            SpecName: "PriestShadow",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 0.6f, Spirit: 0.4f,
            AttackPower: 0f, SpellDamage: 1.0f, HealingPower: 0f,
            CritRating: 0.6f, HitRating: 0.8f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["PriestHoly"] = new StatWeightProfile(
            SpecName: "PriestHoly",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 0.8f, Spirit: 0.7f,
            AttackPower: 0f, SpellDamage: 0f, HealingPower: 1.0f,
            CritRating: 0.5f, HitRating: 0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0.8f, HealthPerFive: 0f),

        // ── Paladins ──
        ["PaladinRetribution"] = new StatWeightProfile(
            SpecName: "PaladinRetribution",
            Strength: 1.0f, Agility: 0f, Stamina: 0.4f, Intellect: 0.3f, Spirit: 0f,
            AttackPower: 0f, SpellDamage: 0.3f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 0.8f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["PaladinHoly"] = new StatWeightProfile(
            SpecName: "PaladinHoly",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 0.8f, Spirit: 0f,
            AttackPower: 0f, SpellDamage: 0f, HealingPower: 1.0f,
            CritRating: 0.5f, HitRating: 0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0.9f, HealthPerFive: 0f),

        ["PaladinProtection"] = new StatWeightProfile(
            SpecName: "PaladinProtection",
            Strength: 0.3f, Agility: 0f, Stamina: 1.0f, Intellect: 0.2f, Spirit: 0f,
            AttackPower: 0f, SpellDamage: 0.2f, HealingPower: 0f,
            CritRating: 0f, HitRating: 0f,
            DodgeRating: 0.5f, ParryRating: 0f, BlockRating: 0.7f,
            Armor: 0.8f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        // ── Shamans ──
        ["ShamanEnhancement"] = new StatWeightProfile(
            SpecName: "ShamanEnhancement",
            Strength: 0.8f, Agility: 0.5f, Stamina: 0.3f, Intellect: 0.2f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 1.0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["ShamanElemental"] = new StatWeightProfile(
            SpecName: "ShamanElemental",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 0.8f, Spirit: 0f,
            AttackPower: 0f, SpellDamage: 1.0f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 0.9f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["ShamanRestoration"] = new StatWeightProfile(
            SpecName: "ShamanRestoration",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 0.8f, Spirit: 0.3f,
            AttackPower: 0f, SpellDamage: 0f, HealingPower: 1.0f,
            CritRating: 0f, HitRating: 0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0.9f, HealthPerFive: 0f),

        // ── Warlocks ──
        ["WarlockAffliction"] = new StatWeightProfile(
            SpecName: "WarlockAffliction",
            Strength: 0f, Agility: 0f, Stamina: 0.5f, Intellect: 0.5f, Spirit: 0.3f,
            AttackPower: 0f, SpellDamage: 1.0f, HealingPower: 0f,
            CritRating: 0.5f, HitRating: 1.0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["WarlockDemonology"] = new StatWeightProfile(
            SpecName: "WarlockDemonology",
            Strength: 0f, Agility: 0f, Stamina: 0.6f, Intellect: 0.6f, Spirit: 0f,
            AttackPower: 0f, SpellDamage: 0.9f, HealingPower: 0f,
            CritRating: 0.5f, HitRating: 0.8f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["WarlockDestruction"] = new StatWeightProfile(
            SpecName: "WarlockDestruction",
            Strength: 0f, Agility: 0f, Stamina: 0.4f, Intellect: 0.5f, Spirit: 0f,
            AttackPower: 0f, SpellDamage: 1.0f, HealingPower: 0f,
            CritRating: 0.8f, HitRating: 1.0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        // ── Druids ──
        ["DruidFeral"] = new StatWeightProfile(
            SpecName: "DruidFeral",
            Strength: 0.8f, Agility: 1.0f, Stamina: 0.5f, Intellect: 0f, Spirit: 0f,
            AttackPower: 0.5f, SpellDamage: 0f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 1.5f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["DruidBalance"] = new StatWeightProfile(
            SpecName: "DruidBalance",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 0.8f, Spirit: 0f,
            AttackPower: 0f, SpellDamage: 1.0f, HealingPower: 0f,
            CritRating: 0.7f, HitRating: 0.9f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0f, HealthPerFive: 0f),

        ["DruidRestoration"] = new StatWeightProfile(
            SpecName: "DruidRestoration",
            Strength: 0f, Agility: 0f, Stamina: 0.3f, Intellect: 0.8f, Spirit: 0.6f,
            AttackPower: 0f, SpellDamage: 0f, HealingPower: 1.0f,
            CritRating: 0f, HitRating: 0f,
            DodgeRating: 0f, ParryRating: 0f, BlockRating: 0f,
            Armor: 0f, DpsWeight: 0f, ManaPerFive: 0.8f, HealthPerFive: 0f),
    };

    /// <summary>
    /// Returns the stat weight profile for the given spec name (case-insensitive).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no profile exists for the given spec name.</exception>
    public static StatWeightProfile GetProfile(string specName)
    {
        if (Profiles.TryGetValue(specName, out var profile))
            return profile;

        throw new KeyNotFoundException(
            $"No stat weight profile found for spec '{specName}'. " +
            $"Available specs: {string.Join(", ", Profiles.Keys)}");
    }

    /// <summary>
    /// Tries to retrieve a stat weight profile without throwing.
    /// </summary>
    public static bool TryGetProfile(string specName, out StatWeightProfile? profile)
    {
        return Profiles.TryGetValue(specName, out profile);
    }

    /// <summary>
    /// Returns all available spec names.
    /// </summary>
    public static IEnumerable<string> AvailableSpecs => Profiles.Keys;
}
