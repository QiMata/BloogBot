namespace BotRunner.Combat;

/// <summary>
/// Predefined talent build orders for all class/spec combinations.
/// Each build is an ordered array of (tabIndex, talentIndex) tuples.
/// Index into the array = total talent points already spent.
/// tabIndex: 0-2 (talent tree tab), talentIndex: 0-based position within tab (left-to-right, top-to-bottom).
/// All builds target level 60 (51 talent points).
/// </summary>
public static class TalentBuildDefinitions
{
    public static (uint tab, uint pos)[]? GetBuildOrder(string classSpecName) =>
        classSpecName switch
        {
            // Warrior
            "Arms Warrior" => ArmsWarrior,
            "Fury Warrior" => FuryWarrior,
            "Protection Warrior" => ProtectionWarrior,

            // Shaman
            "Enhancement Shaman" => EnhancementShaman,
            "Elemental Shaman" => ElementalShaman,
            "Restoration Shaman" => RestorationShaman,

            // Mage
            "Frost Mage" => FrostMage,
            "Fire Mage" => FireMage,
            "Arcane Mage" => ArcaneMage,

            // Rogue
            "Combat Rogue" => CombatRogue,
            "Assassination Rogue" => AssassinationRogue,
            "Subtlety Rogue" => SubtletyRogue,

            // Priest
            "Shadow Priest" => ShadowPriest,
            "Holy Priest" => HolyPriest,
            "Discipline Priest" => DisciplinePriest,

            // Warlock
            "Affliction Warlock" => AfflictionWarlock,
            "Demonology Warlock" => DemonologyWarlock,
            "Destruction Warlock" => DestructionWarlock,

            // Hunter
            "Beast Mastery Hunter" => BeastMasteryHunter,
            "Marksmanship Hunter" => MarksmanshipHunter,
            "Survival Hunter" => SurvivalHunter,

            // Paladin
            "Retribution Paladin" => RetributionPaladin,
            "Holy Paladin" => HolyPaladin,
            "Protection Paladin" => ProtectionPaladin,

            // Druid
            "Feral Combat Druid" or "Feral Druid" => FeralDruid,
            "Balance Druid" => BalanceDruid,
            "Restoration Druid" => RestorationDruid,

            _ => null
        };

    // ===== WARRIOR =====
    // Tab 0: Arms, Tab 1: Fury, Tab 2: Protection
    // Arms positions: 0=ImpHS, 1=Deflection, 2=ImpRend, 3=ImpCharge, 4=TactMastery,
    //   5=ImpTC, 6=ImpOverpower, 7=AngerMgmt, 8=DeepWounds, 9=2hSpec,
    //   10=Impale, 11=AxeSpec, 12=Sweeping, 13=MaceSpec, 14=SwordSpec,
    //   15=PolearmSpec, 16=ImpHamstring, 17=MortalStrike
    // Fury positions: 0=BoomingVoice, 1=Cruelty, 2=ImpDemoShout, 3=UnbridledWrath,
    //   4=ImpCleave, 5=PiercingHowl, 6=BloodCraving, 7=ImpBattleShout,
    //   8=DWSpec, 9=ImpExecute, 10=Enrage, 11=ImpSlam, 12=DeathWish,
    //   13=ImpIntercept, 14=ImpBerserkerRage, 15=Flurry, 16=Bloodthirst

    // Arms Warrior — 31/20/0 leveling
    private static readonly (uint, uint)[] ArmsWarrior =
    [
        // Level 10-14: Deflection 5/5
        (0, 1), (0, 1), (0, 1), (0, 1), (0, 1),
        // Level 15-17: Improved Rend 3/3
        (0, 2), (0, 2), (0, 2),
        // Level 18-19: Improved Charge 2/2
        (0, 3), (0, 3),
        // Level 20-24: Tactical Mastery 5/5
        (0, 4), (0, 4), (0, 4), (0, 4), (0, 4),
        // Level 25-26: Improved Overpower 2/2
        (0, 6), (0, 6),
        // Level 27: Anger Management 1/1
        (0, 7),
        // Level 28-30: Deep Wounds 3/3
        (0, 8), (0, 8), (0, 8),
        // Level 31-35: Two-Handed Weapon Spec 5/5
        (0, 9), (0, 9), (0, 9), (0, 9), (0, 9),
        // Level 36-37: Impale 2/2
        (0, 10), (0, 10),
        // Level 38: Sweeping Strikes 1/1
        (0, 12),
        // Level 39: Improved Heroic Strike 1/3 (filler for MS requirement)
        (0, 0),
        // Level 40: Mortal Strike 1/1
        (0, 17),
        // Level 41-45: Cruelty 5/5 (Fury)
        (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
        // Level 46-50: Improved Battle Shout 5/5
        (1, 7), (1, 7), (1, 7), (1, 7), (1, 7),
        // Level 51-55: Unbridled Wrath 5/5
        (1, 3), (1, 3), (1, 3), (1, 3), (1, 3),
        // Level 56-60: Improved Demoralizing Shout 5/5
        (1, 2), (1, 2), (1, 2), (1, 2), (1, 2),
    ];

    // Fury Warrior — 20/31/0 leveling
    private static readonly (uint, uint)[] FuryWarrior =
    [
        // Level 10-14: Cruelty 5/5
        (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
        // Level 15-17: Improved Demoralizing Shout 3/5
        (1, 2), (1, 2), (1, 2),
        // Level 18-19: Improved Battle Shout 2/5
        (1, 7), (1, 7),
        // Level 20-24: Unbridled Wrath 5/5
        (1, 3), (1, 3), (1, 3), (1, 3), (1, 3),
        // Level 25-29: Enrage 5/5
        (1, 10), (1, 10), (1, 10), (1, 10), (1, 10),
        // Level 30-31: Improved Execute 2/2
        (1, 9), (1, 9),
        // Level 32-34: Improved Battle Shout 3-5/5
        (1, 7), (1, 7), (1, 7),
        // Level 35-39: Flurry 5/5
        (1, 15), (1, 15), (1, 15), (1, 15), (1, 15),
        // Level 40: Bloodthirst 1/1
        (1, 16),
        // Level 41-45: Deflection 5/5 (Arms)
        (0, 1), (0, 1), (0, 1), (0, 1), (0, 1),
        // Level 46-48: Improved Heroic Strike 3/3
        (0, 0), (0, 0), (0, 0),
        // Level 49-50: Improved Rend 2/3
        (0, 2), (0, 2),
        // Level 51-55: Tactical Mastery 5/5
        (0, 4), (0, 4), (0, 4), (0, 4), (0, 4),
        // Level 56-58: Improved Overpower 2/2 + Anger Management 1/1
        (0, 6), (0, 6), (0, 7),
        // Level 59-60: Improved Demoralizing Shout 4-5/5
        (1, 2), (1, 2),
    ];

    // Protection Warrior — 5/5/41 leveling (note: prot leveling is slow, this is for dungeons)
    private static readonly (uint, uint)[] ProtectionWarrior =
    [
        // Level 10-14: Shield Specialization 5/5 (Tab 2, pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 15-19: Anticipation 5/5 (pos 1)
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 20-24: Toughness 5/5 (pos 4)
        (2, 4), (2, 4), (2, 4), (2, 4), (2, 4),
        // Level 25-27: Improved Shield Block 3/3 (pos 5)
        (2, 5), (2, 5), (2, 5),
        // Level 28-29: Improved Revenge 2/3 (pos 3)
        (2, 3), (2, 3),
        // Level 30: Last Stand 1/1 (pos 6)
        (2, 6),
        // Level 31-35: Defiance 5/5 (pos 7)
        (2, 7), (2, 7), (2, 7), (2, 7), (2, 7),
        // Level 36-37: Improved Sunder Armor 2/3 (pos 8) -- filler
        (2, 8), (2, 8),
        // Level 38: Concussion Blow 1/1 (pos 9)
        (2, 9),
        // Level 39-40: Improved Shield Wall 2/2 (pos 10)
        (2, 10), (2, 10),
        // Level 41-45: One-Handed Weapon Spec 5/5 (pos 11)
        (2, 11), (2, 11), (2, 11), (2, 11), (2, 11),
        // Level 46-48: Improved Revenge 3/3 + filler
        (2, 3), (2, 8), (2, 12),
        // Level 49: Shield Slam 1/1 (pos 13)
        (2, 13),
        // Level 50: Improved Revenge filler done — Cruelty (Fury)
        (1, 1),
        // Level 51-55: Cruelty 2-5 + Deflection 1 (Arms)
        (1, 1), (1, 1), (1, 1), (1, 1), (0, 1),
        // Level 56-60: Deflection 2-5 + Improved HS 1
        (0, 1), (0, 1), (0, 1), (0, 1), (0, 0),
    ];

    // ===== SHAMAN =====
    // Tab 0: Elemental, Tab 1: Enhancement, Tab 2: Restoration
    // Enhancement positions: 0=AncestralKnowledge, 1=ShieldSpec, 2=Guardian, 3=Thundering,
    //   4=ImpGhostWolf, 5=ImpLS, 6=TwoHandAxes, 7=Anticipation, 8=Flurry,
    //   9=ImpStrengthTotem, 10=SpiritWeapons, 11=ElementalWeapons, 12=Parry,
    //   13=WeaponMastery, 14=Stormstrike

    // Enhancement Shaman — 0/30/21 leveling
    private static readonly (uint, uint)[] EnhancementShaman =
    [
        // Level 10-14: Ancestral Knowledge 5/5
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 15-19: Shield Specialization 5/5
        (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
        // Level 20-24: Thundering Strikes 5/5
        (1, 3), (1, 3), (1, 3), (1, 3), (1, 3),
        // Level 25-26: Improved Ghost Wolf 2/2
        (1, 4), (1, 4),
        // Level 27-28: Two-Handed Axes and Maces 1/1 + Anticipation 1/5
        (1, 6), (1, 7),
        // Level 29-30: Anticipation 2-3/5
        (1, 7), (1, 7),
        // Level 31-35: Flurry 5/5
        (1, 8), (1, 8), (1, 8), (1, 8), (1, 8),
        // Level 36-37: Spirit Weapons 1/1 + Elemental Weapons 1/3
        (1, 10), (1, 11),
        // Level 38-39: Elemental Weapons 2-3/3
        (1, 11), (1, 11),
        // Level 40: Stormstrike 1/1
        (1, 14),
        // Dip into Restoration
        // Level 41-45: Improved Healing Wave 5/5 (Tab 2, pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 46-50: Tidal Focus 5/5 (pos 1)
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 51-55: Totemic Focus 5/5 (pos 3)
        (2, 3), (2, 3), (2, 3), (2, 3), (2, 3),
        // Level 56-58: Nature's Guidance 3/3 (pos 5)
        (2, 5), (2, 5), (2, 5),
        // Level 59-60: Healing Focus 2/5 (pos 2)
        (2, 2), (2, 2),
    ];

    // Elemental Shaman — 30/0/21 leveling
    private static readonly (uint, uint)[] ElementalShaman =
    [
        // Level 10-14: Convection 5/5 (Tab 0, pos 0)
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
        // Level 15-19: Concussion 5/5 (pos 1)
        (0, 1), (0, 1), (0, 1), (0, 1), (0, 1),
        // Level 20-22: Call of Flame 3/3 (pos 2)
        (0, 2), (0, 2), (0, 2),
        // Level 23-24: Elemental Focus 1/1 + Reverberation 1/5 (pos 5, 4)
        (0, 5), (0, 4),
        // Level 25-28: Reverberation 2-5/5
        (0, 4), (0, 4), (0, 4), (0, 4),
        // Level 29-30: Call of Thunder 2/5 (pos 6)
        (0, 6), (0, 6),
        // Level 31-33: Call of Thunder 3-5/5
        (0, 6), (0, 6), (0, 6),
        // Level 34-35: Improved Fire Totems 2/2 (pos 7)
        (0, 7), (0, 7),
        // Level 36-38: Eye of the Storm 3/3 (pos 8)
        (0, 8), (0, 8), (0, 8),
        // Level 39: Elemental Fury 1/1 (pos 10)
        (0, 10),
        // Level 40: Lightning Mastery 1/5 (pos 11)
        (0, 11),
        // Dip into Restoration
        // Level 41-45: Improved Healing Wave 5/5
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 46-50: Tidal Focus 5/5
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 51-55: Totemic Focus 5/5
        (2, 3), (2, 3), (2, 3), (2, 3), (2, 3),
        // Level 56-58: Nature's Guidance 3/3
        (2, 5), (2, 5), (2, 5),
        // Level 59-60: Healing Focus 2/5
        (2, 2), (2, 2),
    ];

    // Restoration Shaman — 0/5/46 leveling
    private static readonly (uint, uint)[] RestorationShaman =
    [
        // Level 10-14: Improved Healing Wave 5/5
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 15-19: Tidal Focus 5/5
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 20-24: Totemic Focus 5/5
        (2, 3), (2, 3), (2, 3), (2, 3), (2, 3),
        // Level 25-27: Nature's Guidance 3/3
        (2, 5), (2, 5), (2, 5),
        // Level 28-32: Healing Focus 5/5
        (2, 2), (2, 2), (2, 2), (2, 2), (2, 2),
        // Level 33-37: Restorative Totems 5/5 (pos 6)
        (2, 6), (2, 6), (2, 6), (2, 6), (2, 6),
        // Level 38-39: Tidal Mastery 2/5 (pos 7)
        (2, 7), (2, 7),
        // Level 40: Nature's Swiftness 1/1 (pos 8)
        (2, 8),
        // Level 41-43: Tidal Mastery 3-5/5
        (2, 7), (2, 7), (2, 7),
        // Level 44-48: Purification 5/5 (pos 9)
        (2, 9), (2, 9), (2, 9), (2, 9), (2, 9),
        // Level 49-50: Mana Tide Totem 1/1 + Healing Way 1/3 (pos 11, 10)
        (2, 11), (2, 10),
        // Enhancement dip
        // Level 51-55: Ancestral Knowledge 5/5
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 56-60: Healing Way 2-3, etc.
        (2, 10), (2, 10), (1, 1), (1, 1), (1, 1),
    ];

    // ===== MAGE =====
    // Tab 0: Arcane, Tab 1: Fire, Tab 2: Frost

    // Frost Mage — 0/0/51 leveling (most common mage leveling spec)
    private static readonly (uint, uint)[] FrostMage =
    [
        // Level 10-14: Improved Frostbolt 5/5 (Tab 2, pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 15-17: Elemental Precision 3/3 (pos 1)
        (2, 1), (2, 1), (2, 1),
        // Level 18-19: Frostbite 2/3 (pos 2)
        (2, 2), (2, 2),
        // Level 20-22: Improved Frost Nova 2/2 + Permafrost 1/3 (pos 3, 4)
        (2, 3), (2, 3), (2, 4),
        // Level 23-24: Permafrost 2-3/3
        (2, 4), (2, 4),
        // Level 25-27: Piercing Ice 3/3 (pos 5)
        (2, 5), (2, 5), (2, 5),
        // Level 28-29: Cold Snap 1/1 + Improved Blizzard 1/3 (pos 6, 7)
        (2, 6), (2, 7),
        // Level 30: Frostbite 3/3
        (2, 2),
        // Level 31-33: Shatter 3/5 (pos 8)
        (2, 8), (2, 8), (2, 8),
        // Level 34-35: Shatter 4-5/5
        (2, 8), (2, 8),
        // Level 36-38: Ice Block 1/1 + Improved Cone of Cold 2/3 (pos 9, 10)
        (2, 9), (2, 10), (2, 10),
        // Level 39: Improved Cone of Cold 3/3
        (2, 10),
        // Level 40: Ice Barrier 1/1 (pos 13)
        (2, 13),
        // Level 41-43: Arctic Reach 2/2 + Frost Channeling 1/3 (pos 11, 12)
        (2, 11), (2, 11), (2, 12),
        // Level 44-45: Frost Channeling 2-3/3
        (2, 12), (2, 12),
        // Level 46-47: Improved Blizzard 2-3/3
        (2, 7), (2, 7),
        // Level 48-49: Winter's Chill 2/5 (pos 14)
        (2, 14), (2, 14),
        // Level 50-51: Winter's Chill 3-4/5
        (2, 14), (2, 14),
    ];

    // Fire Mage — 0/31/20 leveling
    private static readonly (uint, uint)[] FireMage =
    [
        // Level 10-14: Improved Fireball 5/5 (Tab 1, pos 0)
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 15-17: Ignite 3/5 (pos 2)
        (1, 2), (1, 2), (1, 2),
        // Level 18-19: Ignite 4-5/5
        (1, 2), (1, 2),
        // Level 20-22: Flame Throwing 2/2 + Impact 1/5 (pos 1, 3)
        (1, 1), (1, 1), (1, 3),
        // Level 23-24: Impact 2-3/5
        (1, 3), (1, 3),
        // Level 25-29: Pyroblast 1/1 + Burning Soul 2/2 + Improved Scorch 2/3 (pos 5, 4, 6)
        (1, 5), (1, 4), (1, 4), (1, 6), (1, 6),
        // Level 30-34: Master of Elements 3/3 + Critical Mass 2/3 (pos 8, 7)
        (1, 8), (1, 8), (1, 8), (1, 7), (1, 7),
        // Level 35-37: Critical Mass 3/3 + Fire Power 2/5 (pos 7, 9)
        (1, 7), (1, 9), (1, 9),
        // Level 38-39: Fire Power 3-4/5
        (1, 9), (1, 9),
        // Level 40: Combustion 1/1 (pos 11)
        (1, 11),
        // Dip into Frost
        // Level 41-45: Improved Frostbolt 5/5
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 46-48: Elemental Precision 3/3
        (2, 1), (2, 1), (2, 1),
        // Level 49-50: Frostbite 2/3
        (2, 2), (2, 2),
        // Level 51-60: remaining filler
        (1, 9), (1, 3), (1, 3), (1, 6), (2, 4), (2, 4), (2, 4), (2, 5), (2, 5), (2, 5),
    ];

    // Arcane Mage — 31/0/20 leveling
    private static readonly (uint, uint)[] ArcaneMage =
    [
        // Level 10-14: Arcane Subtlety 2/2 + Arcane Focus 3/5 (Tab 0, pos 0, 1)
        (0, 0), (0, 0), (0, 1), (0, 1), (0, 1),
        // Level 15-19: Arcane Focus 4-5/5 + Arcane Concentration 3/5 (pos 2)
        (0, 1), (0, 1), (0, 2), (0, 2), (0, 2),
        // Level 20-24: Arcane Concentration 4-5/5 + Arcane Meditation 3/3 (pos 4)
        (0, 2), (0, 2), (0, 4), (0, 4), (0, 4),
        // Level 25-29: Improved Arcane Explosion 3/3 + Arcane Resilience 1/1 + Improved Mana Shield 1/2 (pos 3, 5, 6)
        (0, 3), (0, 3), (0, 3), (0, 5), (0, 6),
        // Level 30-34: Improved Counterspell 2/2 + Arcane Mind 3/5 (pos 7, 8)
        (0, 7), (0, 7), (0, 8), (0, 8), (0, 8),
        // Level 35-39: Arcane Mind 4-5/5 + Arcane Instability 3/3 (pos 9)
        (0, 8), (0, 8), (0, 9), (0, 9), (0, 9),
        // Level 40: Arcane Power 1/1 (pos 11)
        (0, 11),
        // Dip into Frost
        // Level 41-45: Improved Frostbolt 5/5
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 46-48: Elemental Precision 3/3
        (2, 1), (2, 1), (2, 1),
        // Level 49-53: Piercing Ice 3/3 + Frostbite 2/3
        (2, 5), (2, 5), (2, 5), (2, 2), (2, 2),
        // Level 54-58: Improved Frost Nova 2/2 + Permafrost 3/3
        (2, 3), (2, 3), (2, 4), (2, 4), (2, 4),
        // Level 59-60: Improved Mana Shield 2/2 + filler
        (0, 6), (0, 10),
    ];

    // ===== ROGUE =====
    // Tab 0: Assassination, Tab 1: Combat, Tab 2: Subtlety

    // Combat Rogue — 15/31/5 leveling
    private static readonly (uint, uint)[] CombatRogue =
    [
        // Level 10-14: Improved Sinister Strike 2/2 + Lightning Reflexes 3/5 (Tab 1, pos 0, 2)
        (1, 0), (1, 0), (1, 2), (1, 2), (1, 2),
        // Level 15-16: Lightning Reflexes 4-5/5
        (1, 2), (1, 2),
        // Level 17-19: Precision 3/5 (pos 4)
        (1, 4), (1, 4), (1, 4),
        // Level 20-21: Precision 4-5/5
        (1, 4), (1, 4),
        // Level 22-24: Deflection 3/5 (pos 1)
        (1, 1), (1, 1), (1, 1),
        // Level 25-26: Deflection 4-5/5
        (1, 1), (1, 1),
        // Level 27-29: Dual Wield Specialization 3/5 (pos 5)
        (1, 5), (1, 5), (1, 5),
        // Level 30: Blade Flurry 1/1 (pos 6)
        (1, 6),
        // Level 31-33: Dual Wield Specialization 4-5/5 + Weapon Expertise 1/2 (pos 8)
        (1, 5), (1, 5), (1, 8),
        // Level 34-38: Weapon Expertise 2/2 + Aggression 3/3 (pos 9)
        (1, 8), (1, 9), (1, 9), (1, 9), (1, 10),
        // Level 39-40: Adrenaline Rush 1/1 (pos 11) + filler
        (1, 10), (1, 11),
        // Assassination dip
        // Level 41-45: Improved Eviscerate 3/3 + Malice 2/5 (Tab 0, pos 0, 1)
        (0, 0), (0, 0), (0, 0), (0, 1), (0, 1),
        // Level 46-50: Malice 3-5/5 + Ruthlessness 2/3 (pos 2)
        (0, 1), (0, 1), (0, 1), (0, 2), (0, 2),
        // Level 51-55: Ruthlessness 3/3 + Murder 2/2 + Relentless Strikes 1/1 (pos 3, 4)
        (0, 2), (0, 3), (0, 3), (0, 4), (0, 5),
        // Level 56-60: remaining (Subtlety or more Assassination)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
    ];

    // Assassination Rogue — 31/8/12
    private static readonly (uint, uint)[] AssassinationRogue =
    [
        // Level 10-14: Improved Eviscerate 3/3 + Malice 2/5
        (0, 0), (0, 0), (0, 0), (0, 1), (0, 1),
        // Level 15-19: Malice 3-5/5 + Ruthlessness 2/3
        (0, 1), (0, 1), (0, 1), (0, 2), (0, 2),
        // Level 20-24: Ruthlessness 3/3 + Murder 2/2 + Relentless Strikes 1/1
        (0, 2), (0, 3), (0, 3), (0, 4), (0, 5),
        // Level 25-29: Improved Slice and Dice 3/3 + Lethality 2/5 (pos 6, 7)
        (0, 6), (0, 6), (0, 6), (0, 7), (0, 7),
        // Level 30-34: Lethality 3-5/5 + Vile Poisons 2/5 (pos 8)
        (0, 7), (0, 7), (0, 7), (0, 8), (0, 8),
        // Level 35-39: Improved Poisons 3/5 (pos 9) + Cold Blood 1/1 (pos 10) + Seal Fate 1/5 (pos 11)
        (0, 9), (0, 9), (0, 9), (0, 10), (0, 11),
        // Level 40: Seal Fate 2/5
        (0, 11),
        // Combat dip
        // Level 41-42: Improved Sinister Strike 2/2
        (1, 0), (1, 0),
        // Level 43-45: Lightning Reflexes 3/5
        (1, 2), (1, 2), (1, 2),
        // Level 46-48: Deflection 3/5
        (1, 1), (1, 1), (1, 1),
        // Subtlety dip
        // Level 49-53: Master of Deception 5/5 (Tab 2, pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 54-58: Camouflage 5/5 (pos 1)
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 59-60: Initiative 2/3 (pos 2)
        (2, 2), (2, 2),
    ];

    // Subtlety Rogue — 8/0/43
    private static readonly (uint, uint)[] SubtletyRogue =
    [
        // Level 10-14: Master of Deception 5/5
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 15-19: Camouflage 5/5
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 20-24: Opportunity 5/5 (pos 2)
        (2, 2), (2, 2), (2, 2), (2, 2), (2, 2),
        // Level 25-29: Initiative 3/3 + Ghostly Strike 1/1 + Improved Ambush 1/3 (pos 3, 4, 5)
        (2, 3), (2, 3), (2, 3), (2, 4), (2, 5),
        // Level 30: Hemorrhage 1/1 (pos 8)
        (2, 8),
        // Level 31-35: Setup 3/3 + Improved Ambush 2-3/3 (pos 6)
        (2, 6), (2, 6), (2, 6), (2, 5), (2, 5),
        // Level 36-40: Elusiveness 2/2 + Serrated Blades 3/3 (pos 7, 9)
        (2, 7), (2, 7), (2, 9), (2, 9), (2, 9),
        // Level 41-45: Dirty Deeds 2/2 + Preparation 1/1 + Heightened Senses 2/2 (pos 10, 11, 12)
        (2, 10), (2, 10), (2, 11), (2, 12), (2, 12),
        // Level 46-48: Premeditation 1/1 + remaining filler (pos 13)
        (2, 13), (2, 14), (2, 14),
        // Level 49-51: more filler
        (2, 14), (2, 14), (2, 14),
        // Assassination dip
        // Level 52-56: Malice 5/5
        (0, 1), (0, 1), (0, 1), (0, 1), (0, 1),
        // Level 57-59: Improved Eviscerate 3/3
        (0, 0), (0, 0), (0, 0),
        // Level 60: filler
        (0, 2),
    ];

    // ===== PRIEST =====
    // Tab 0: Discipline, Tab 1: Holy, Tab 2: Shadow

    // Shadow Priest — 5/0/46 leveling
    private static readonly (uint, uint)[] ShadowPriest =
    [
        // Level 10-14: Spirit Tap 5/5 (Tab 2, pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 15-19: Blackout 5/5 (pos 1)
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 20-24: Shadow Focus 5/5 (pos 2)
        (2, 2), (2, 2), (2, 2), (2, 2), (2, 2),
        // Level 25-27: Improved Shadow Word: Pain 2/2 + Mind Flay 1/1 (pos 3, 5)
        (2, 3), (2, 3), (2, 5),
        // Level 28-29: Improved Psychic Scream 2/2 (pos 4)
        (2, 4), (2, 4),
        // Level 30-32: Vampiric Embrace 1/1 + Shadow Reach 2/3 (pos 7, 6)
        (2, 7), (2, 6), (2, 6),
        // Level 33-34: Shadow Reach 3/3 + Silence 1/1 (pos 8)
        (2, 6), (2, 8),
        // Level 35-39: Darkness 5/5 (pos 9)
        (2, 9), (2, 9), (2, 9), (2, 9), (2, 9),
        // Level 40: Shadowform 1/1 (pos 11)
        (2, 11),
        // Level 41-45: Shadow Weaving 5/5 (pos 10)
        (2, 10), (2, 10), (2, 10), (2, 10), (2, 10),
        // Discipline dip
        // Level 46-50: Wand Specialization 5/5 (Tab 0, pos 2)
        (0, 2), (0, 2), (0, 2), (0, 2), (0, 2),
        // Level 51-55: remaining Shadow filler
        (2, 12), (2, 12), (2, 12), (2, 13), (2, 13),
        // Level 56-60: Discipline fillers
        (0, 0), (0, 0), (0, 1), (0, 1), (0, 1),
    ];

    // Holy Priest — 21/30/0
    private static readonly (uint, uint)[] HolyPriest =
    [
        // Level 10-14: Healing Focus 2/2 + Holy Specialization 3/5 (Tab 1, pos 0, 1)
        (1, 0), (1, 0), (1, 1), (1, 1), (1, 1),
        // Level 15-19: Holy Specialization 4-5/5 + Divine Fury 3/5 (pos 3)
        (1, 1), (1, 1), (1, 3), (1, 3), (1, 3),
        // Level 20-24: Divine Fury 4-5/5 + Inspiration 3/3 (pos 4)
        (1, 3), (1, 3), (1, 4), (1, 4), (1, 4),
        // Level 25-29: Holy Nova 1/1 + Improved Renew 3/3 + Spirit of Redemption 1/1 (pos 5, 2, 7)
        (1, 5), (1, 2), (1, 2), (1, 2), (1, 7),
        // Level 30-34: Spiritual Healing 5/5 (pos 8)
        (1, 8), (1, 8), (1, 8), (1, 8), (1, 8),
        // Level 35-39: Spiritual Guidance 5/5 (pos 9)
        (1, 9), (1, 9), (1, 9), (1, 9), (1, 9),
        // Level 40: Lightwell 1/1 (pos 10)
        (1, 10),
        // Discipline dip
        // Level 41-43: Improved Power Word: Fortitude 2/2 + Unbreakable Will 1/5 (Tab 0, pos 0, 1)
        (0, 0), (0, 0), (0, 1),
        // Level 44-48: Wand Specialization 5/5
        (0, 2), (0, 2), (0, 2), (0, 2), (0, 2),
        // Level 49-53: Silent Resolve 5/5 (pos 3)
        (0, 3), (0, 3), (0, 3), (0, 3), (0, 3),
        // Level 54-58: Improved Power Word: Shield 3/3 + Meditation 2/3 (pos 4, 6)
        (0, 4), (0, 4), (0, 4), (0, 6), (0, 6),
        // Level 59-60: Meditation 3/3 + Mental Agility 1/5 (pos 7)
        (0, 6), (0, 7),
    ];

    // Discipline Priest — 31/20/0
    private static readonly (uint, uint)[] DisciplinePriest =
    [
        // Level 10-14: Unbreakable Will 5/5
        (0, 1), (0, 1), (0, 1), (0, 1), (0, 1),
        // Level 15-19: Wand Specialization 5/5
        (0, 2), (0, 2), (0, 2), (0, 2), (0, 2),
        // Level 20-24: Silent Resolve 5/5
        (0, 3), (0, 3), (0, 3), (0, 3), (0, 3),
        // Level 25-29: Improved PW:S 3/3 + Meditation 2/3 (pos 4, 6)
        (0, 4), (0, 4), (0, 4), (0, 6), (0, 6),
        // Level 30: Inner Focus 1/1 (pos 5)
        (0, 5),
        // Level 31-35: Mental Agility 5/5 (pos 7)
        (0, 7), (0, 7), (0, 7), (0, 7), (0, 7),
        // Level 36-38: Meditation 3/3 + Improved Mana Burn 1/2 (pos 8)
        (0, 6), (0, 8), (0, 8),
        // Level 39: Divine Spirit 1/1 (pos 9)
        (0, 9),
        // Level 40: Power Infusion 1/1 (pos 11)
        (0, 11),
        // Holy dip
        // Level 41-42: Healing Focus 2/2
        (1, 0), (1, 0),
        // Level 43-47: Holy Specialization 5/5
        (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
        // Level 48-52: Divine Fury 5/5
        (1, 3), (1, 3), (1, 3), (1, 3), (1, 3),
        // Level 53-55: Inspiration 3/3
        (1, 4), (1, 4), (1, 4),
        // Level 56-58: Improved Renew 3/3
        (1, 2), (1, 2), (1, 2),
        // Level 59-60: Spiritual Healing 2/5
        (1, 8), (1, 8),
    ];

    // ===== WARLOCK =====
    // Tab 0: Affliction, Tab 1: Demonology, Tab 2: Destruction

    // Affliction Warlock — 30/0/21 leveling
    private static readonly (uint, uint)[] AfflictionWarlock =
    [
        // Level 10-14: Improved Corruption 5/5 (Tab 0, pos 0)
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
        // Level 15-17: Suppression 3/5 (pos 1)
        (0, 1), (0, 1), (0, 1),
        // Level 18-19: Improved Curse of Agony 2/3 (pos 2)
        (0, 2), (0, 2),
        // Level 20-24: Improved Life Tap 2/2 + Amplify Curse 1/1 + Curse of Exhaustion 0 (pos 3, 4)
        (0, 3), (0, 3), (0, 4), (0, 2), (0, 5),
        // Level 25-29: Grim Reach 2/2 + Nightfall 2/2 + Improved Drain Life 1/5 (pos 6, 7, 8)
        (0, 6), (0, 6), (0, 7), (0, 7), (0, 8),
        // Level 30: Siphon Life 1/1 (pos 9)
        (0, 9),
        // Level 31-35: Shadow Mastery 5/5 (pos 10)
        (0, 10), (0, 10), (0, 10), (0, 10), (0, 10),
        // Level 36-39: Dark Pact 1/1 + Improved Drain Life 2-4/5 (pos 11)
        (0, 11), (0, 8), (0, 8), (0, 8),
        // Level 40: Unstable Affliction or filler (pos 12)
        (0, 1), // Suppression 4/5
        // Destruction dip
        // Level 41-45: Improved Shadow Bolt 5/5 (Tab 2, pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 46-48: Cataclysm 3/5 (pos 1)
        (2, 1), (2, 1), (2, 1),
        // Level 49-50: Bane 2/5 (pos 2)
        (2, 2), (2, 2),
        // Level 51-55: Bane 3-5/5 + Devastation 2/5 (pos 4)
        (2, 2), (2, 2), (2, 2), (2, 4), (2, 4),
        // Level 56-60: Devastation 3-5/5 + Shadowburn 1/1 + Intensity 1/2 (pos 5, 3)
        (2, 4), (2, 4), (2, 4), (2, 5), (2, 3),
    ];

    // Demonology Warlock — 0/30/21
    private static readonly (uint, uint)[] DemonologyWarlock =
    [
        // Level 10-14: Improved Healthstone 2/2 + Improved Imp 3/3 (Tab 1, pos 0, 1)
        (1, 0), (1, 0), (1, 1), (1, 1), (1, 1),
        // Level 15-19: Demonic Embrace 5/5 (pos 2)
        (1, 2), (1, 2), (1, 2), (1, 2), (1, 2),
        // Level 20-24: Improved Voidwalker 3/3 + Fel Intellect 2/5 (pos 3, 4)
        (1, 3), (1, 3), (1, 3), (1, 4), (1, 4),
        // Level 25-29: Fel Domination 1/1 + Fel Stamina 3/5 + Master Summoner 1/2 (pos 5, 6, 7)
        (1, 5), (1, 6), (1, 6), (1, 6), (1, 7),
        // Level 30: Demonic Sacrifice 1/1 (pos 8)
        (1, 8),
        // Level 31-35: Unholy Power 5/5 (pos 9)
        (1, 9), (1, 9), (1, 9), (1, 9), (1, 9),
        // Level 36-40: Master Demonologist 5/5 (pos 10)
        (1, 10), (1, 10), (1, 10), (1, 10), (1, 10),
        // Destruction dip
        // Level 41-45: Improved Shadow Bolt 5/5
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 46-50: Cataclysm 5/5
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 51-55: Bane 5/5
        (2, 2), (2, 2), (2, 2), (2, 2), (2, 2),
        // Level 56-60: Devastation 5/5
        (2, 4), (2, 4), (2, 4), (2, 4), (2, 4),
    ];

    // Destruction Warlock — 7/7/37
    private static readonly (uint, uint)[] DestructionWarlock =
    [
        // Level 10-14: Improved Shadow Bolt 5/5 (Tab 2, pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 15-19: Cataclysm 5/5 (pos 1)
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 20-24: Bane 5/5 (pos 2)
        (2, 2), (2, 2), (2, 2), (2, 2), (2, 2),
        // Level 25-29: Devastation 5/5 (pos 4)
        (2, 4), (2, 4), (2, 4), (2, 4), (2, 4),
        // Level 30: Shadowburn 1/1 (pos 5)
        (2, 5),
        // Level 31-33: Intensity 2/2 + Destructive Reach 1/2 (pos 3, 6)
        (2, 3), (2, 3), (2, 6),
        // Level 34-38: Improved Searing Pain 3/5 (pos 7) + Pyroclasm 2/2 (pos 8)
        (2, 7), (2, 7), (2, 7), (2, 8), (2, 8),
        // Level 39: Ruin 1/1 (pos 10)
        (2, 10),
        // Level 40: Conflagrate 1/1 (pos 12)
        (2, 12),
        // Affliction dip
        // Level 41-45: Improved Corruption 5/5
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
        // Level 46-47: Suppression 2/5
        (0, 1), (0, 1),
        // Demonology dip
        // Level 48-49: Improved Healthstone 2/2
        (1, 0), (1, 0),
        // Level 50-54: Demonic Embrace 5/5
        (1, 2), (1, 2), (1, 2), (1, 2), (1, 2),
        // Level 55-59: remaining Destruction
        (2, 6), (2, 9), (2, 9), (2, 9), (2, 9),
        // Level 60: filler
        (2, 9),
    ];

    // ===== HUNTER =====
    // Tab 0: Beast Mastery, Tab 1: Marksmanship, Tab 2: Survival

    // Beast Mastery Hunter — 31/20/0 leveling
    private static readonly (uint, uint)[] BeastMasteryHunter =
    [
        // Level 10-14: Improved Aspect of the Hawk 5/5 (Tab 0, pos 0)
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
        // Level 15-19: Endurance Training 5/5 (pos 1)
        (0, 1), (0, 1), (0, 1), (0, 1), (0, 1),
        // Level 20-24: Unleashed Fury 5/5 (pos 4)
        (0, 4), (0, 4), (0, 4), (0, 4), (0, 4),
        // Level 25-29: Improved Mend Pet 2/2 + Ferocity 3/5 (pos 3, 5)
        (0, 3), (0, 3), (0, 5), (0, 5), (0, 5),
        // Level 30: Intimidation 1/1 (pos 6)
        (0, 6),
        // Level 31-35: Ferocity 4-5/5 + Bestial Discipline 2/2 + Spirit Bond 1/2 (pos 7, 8)
        (0, 5), (0, 5), (0, 7), (0, 7), (0, 8),
        // Level 36-39: Spirit Bond 2/2 + Frenzy 2/5 (pos 9) + BW setup
        (0, 8), (0, 9), (0, 9), (0, 2),
        // Level 40: Bestial Wrath 1/1 (pos 10)
        (0, 10),
        // Marksmanship dip
        // Level 41-45: Lethal Shots 5/5 (Tab 1, pos 1)
        (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
        // Level 46-50: Efficiency 5/5 (pos 0)
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 51-55: Aimed Shot 1/1 + Improved Arcane Shot 4/5 (pos 5, 2)
        (1, 5), (1, 2), (1, 2), (1, 2), (1, 2),
        // Level 56-60: Mortal Shots 5/5 (pos 6)
        (1, 6), (1, 6), (1, 6), (1, 6), (1, 6),
    ];

    // Marksmanship Hunter — 0/31/20
    private static readonly (uint, uint)[] MarksmanshipHunter =
    [
        // Level 10-14: Lethal Shots 5/5
        (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
        // Level 15-19: Efficiency 5/5
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 20-24: Aimed Shot 1/1 + Improved Arcane Shot 4/5 (pos 5, 2)
        (1, 5), (1, 2), (1, 2), (1, 2), (1, 2),
        // Level 25-29: Mortal Shots 5/5 (pos 6)
        (1, 6), (1, 6), (1, 6), (1, 6), (1, 6),
        // Level 30-34: Barrage 3/3 + Ranged Weapon Specialization 2/5 (pos 7, 8)
        (1, 7), (1, 7), (1, 7), (1, 8), (1, 8),
        // Level 35-39: Ranged Weapon Spec 3-5/5 + Scatter Shot 1/1 (pos 9) + filler
        (1, 8), (1, 8), (1, 8), (1, 9), (1, 3),
        // Level 40: Trueshot Aura 1/1 (pos 11)
        (1, 11),
        // Survival dip
        // Level 41-45: Monster Slaying 3/3 + Humanoid Slaying 2/3 (Tab 2, pos 0, 1)
        (2, 0), (2, 0), (2, 0), (2, 1), (2, 1),
        // Level 46-50: Humanoid Slaying 3/3 + Deflection 2/5 (pos 2)
        (2, 1), (2, 2), (2, 2), (2, 3), (2, 3),
        // Level 51-55: Savage Strikes 2/2 + Improved Wing Clip 3/5 + Surefooted 1/3 (pos 4, 5)
        (2, 3), (2, 3), (2, 3), (2, 4), (2, 4),
        // Level 56-60: Surefooted 2-3/3 + remaining
        (2, 5), (2, 5), (2, 5), (2, 6), (2, 6),
    ];

    // Survival Hunter — 0/20/31
    private static readonly (uint, uint)[] SurvivalHunter =
    [
        // Level 10-14: Monster Slaying 3/3 + Humanoid Slaying 2/3
        (2, 0), (2, 0), (2, 0), (2, 1), (2, 1),
        // Level 15-19: Humanoid Slaying 3/3 + Deflection 2/5
        (2, 1), (2, 2), (2, 2), (2, 3), (2, 3),
        // Level 20-24: Savage Strikes 2/2 + Entrapment 3/5 (pos 3, 4)
        (2, 3), (2, 4), (2, 4), (2, 4), (2, 5),
        // Level 25-29: Surefooted 3/3 + Improved FD 2/2 + Trap Mastery 1/2 (pos 5, 6, 7)
        (2, 5), (2, 5), (2, 6), (2, 6), (2, 7),
        // Level 30: Deterrence 1/1 (pos 8)
        (2, 8),
        // Level 31-35: Trap Mastery 2/2 + Killer Instinct 3/3 + Lightning Reflexes 1/5 (pos 9, 10)
        (2, 7), (2, 9), (2, 9), (2, 9), (2, 10),
        // Level 36-40: Lightning Reflexes 2-5/5 + Wyvern Sting 1/1 (pos 12)
        (2, 10), (2, 10), (2, 10), (2, 10), (2, 12),
        // Marksmanship dip
        // Level 41-45: Lethal Shots 5/5
        (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
        // Level 46-50: Efficiency 5/5
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 51-55: Aimed Shot 1/1 + Improved Arcane Shot 4/5
        (1, 5), (1, 2), (1, 2), (1, 2), (1, 2),
        // Level 56-60: Mortal Shots 5/5
        (1, 6), (1, 6), (1, 6), (1, 6), (1, 6),
    ];

    // ===== PALADIN =====
    // Tab 0: Holy, Tab 1: Protection, Tab 2: Retribution

    // Retribution Paladin — 0/10/41 leveling
    private static readonly (uint, uint)[] RetributionPaladin =
    [
        // Level 10-14: Improved Blessing of Might 5/5 (Tab 2, pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 15-19: Benediction 5/5 (pos 1)
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 20-24: Deflection 5/5 (pos 2)
        (2, 2), (2, 2), (2, 2), (2, 2), (2, 2),
        // Level 25-29: Vindication 3/3 + Conviction 2/5 (pos 4, 3)
        (2, 4), (2, 4), (2, 4), (2, 3), (2, 3),
        // Level 30: Seal of Command 1/1 (pos 5)
        (2, 5),
        // Level 31-35: Conviction 3-5/5 + Two-Handed Weapon Spec 2/3 (pos 6)
        (2, 3), (2, 3), (2, 3), (2, 6), (2, 6),
        // Level 36-40: Two-Handed Weapon Spec 3/3 + Vengeance 3/5 (pos 8) + Sanctity Aura 1/1 (pos 7)
        (2, 6), (2, 8), (2, 8), (2, 8), (2, 7),
        // Level 41-45: Vengeance 4-5/5 + Improved Retribution Aura 2/2 + Repentance 1/1 (pos 9, 10)
        (2, 8), (2, 8), (2, 9), (2, 9), (2, 10),
        // Protection dip
        // Level 46-50: Improved Devotion Aura 5/5 (Tab 1, pos 0)
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 51-55: Precision 3/3 + Guardian's Favor 2/2 (pos 2, 1)
        (1, 2), (1, 2), (1, 2), (1, 1), (1, 1),
        // Level 56-60: remaining filler in ret
        (2, 11), (2, 11), (2, 11), (2, 11), (2, 11),
    ];

    // Holy Paladin — 35/11/5
    private static readonly (uint, uint)[] HolyPaladin =
    [
        // Level 10-14: Divine Strength 5/5 (Tab 0, pos 0)
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
        // Level 15-19: Divine Intellect 5/5 (pos 1)
        (0, 1), (0, 1), (0, 1), (0, 1), (0, 1),
        // Level 20-24: Spiritual Focus 5/5 (pos 2)
        (0, 2), (0, 2), (0, 2), (0, 2), (0, 2),
        // Level 25-29: Healing Light 3/3 + Illumination 2/5 (pos 4, 5)
        (0, 4), (0, 4), (0, 4), (0, 5), (0, 5),
        // Level 30-34: Illumination 3-5/5 + Improved LoH 2/2 (pos 3)
        (0, 5), (0, 5), (0, 5), (0, 3), (0, 3),
        // Level 35-39: Divine Favor 1/1 + Holy Power 5/5 (pos 7, 8)
        (0, 7), (0, 8), (0, 8), (0, 8), (0, 8),
        // Level 40: Holy Shock 1/1 (pos 10)
        (0, 10),
        // Level 41-43: Holy Power 5/5 + Lasting Judgement 1/3 (pos 9)
        (0, 8), (0, 9), (0, 9),
        // Protection dip
        // Level 44-48: Improved Devotion Aura 5/5
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 49-51: Precision 3/3
        (1, 2), (1, 2), (1, 2),
        // Level 52-54: Guardian's Favor 2/2 + Toughness 1/5 (pos 1, 3)
        (1, 1), (1, 1), (1, 3),
        // Retribution dip
        // Level 55-59: Improved Blessing of Might 5/5
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 60: Lasting Judgement 3/3
        (0, 9),
    ];

    // Protection Paladin — 0/41/10
    private static readonly (uint, uint)[] ProtectionPaladin =
    [
        // Level 10-14: Improved Devotion Aura 5/5
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 15-17: Precision 3/3
        (1, 2), (1, 2), (1, 2),
        // Level 18-19: Guardian's Favor 2/2
        (1, 1), (1, 1),
        // Level 20-24: Toughness 5/5 (pos 3)
        (1, 3), (1, 3), (1, 3), (1, 3), (1, 3),
        // Level 25-29: Improved Righteous Fury 3/3 + Shield Specialization 2/3 (pos 4, 5)
        (1, 4), (1, 4), (1, 4), (1, 5), (1, 5),
        // Level 30: Blessing of Sanctuary 1/1 (pos 6)
        (1, 6),
        // Level 31-35: Shield Specialization 3/3 + Reckoning 4/5 (pos 7)
        (1, 5), (1, 7), (1, 7), (1, 7), (1, 7),
        // Level 36-40: Reckoning 5/5 + One-Handed Weapon Spec 3/5 + Holy Shield 1/1 (pos 8, 10)
        (1, 7), (1, 8), (1, 8), (1, 8), (1, 10),
        // Level 41-43: One-Handed Weapon Spec 4-5/5 + Improved Concentration Aura 1/3 (pos 9)
        (1, 8), (1, 8), (1, 9),
        // Level 44-46: Improved Concentration Aura 2-3/3
        (1, 9), (1, 9), (1, 11),
        // Level 47-50: Redoubt filler + more prot
        (1, 11), (1, 11), (1, 11), (1, 11),
        // Retribution dip
        // Level 51-55: Improved Blessing of Might 5/5
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 56-60: Benediction 5/5
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
    ];

    // ===== DRUID =====
    // Tab 0: Balance, Tab 1: Feral Combat, Tab 2: Restoration

    // Feral Combat Druid — 0/30/21 or 14/32/5 leveling
    private static readonly (uint, uint)[] FeralDruid =
    [
        // Level 10-14: Ferocity 5/5 (Tab 1, pos 0)
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 15-19: Feral Aggression 5/5 (pos 1)
        (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
        // Level 20-24: Feral Instinct 5/5 (pos 2)
        (1, 2), (1, 2), (1, 2), (1, 2), (1, 2),
        // Level 25-27: Sharpened Claws 3/3 (pos 4)
        (1, 4), (1, 4), (1, 4),
        // Level 28-29: Improved Shred 2/2 (pos 3)
        (1, 3), (1, 3),
        // Level 30: Feral Charge 1/1 (pos 7)
        (1, 7),
        // Level 31-33: Predatory Strikes 3/3 (pos 5)
        (1, 5), (1, 5), (1, 5),
        // Level 34-35: Blood Frenzy 2/2 (pos 6)
        (1, 6), (1, 6),
        // Level 36-40: Heart of the Wild 5/5 (pos 8)
        (1, 8), (1, 8), (1, 8), (1, 8), (1, 8),
        // Level 41: Leader of the Pack 1/1 (pos 9)
        (1, 9),
        // Restoration dip
        // Level 42-46: Furor 5/5 (Tab 2, pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 47-51: Improved Mark of the Wild 5/5 (pos 1) — (or Nature's Focus)
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 52-54: Natural Shapeshifter 3/3 (pos 2)
        (2, 2), (2, 2), (2, 2),
        // Level 55-56: Improved Enrage 2/2 (or Omen of Clarity) (pos 3)
        (2, 3), (2, 3),
        // Level 57-60: Omen of Clarity 1/1 + Nature's Grasp fillers (pos 4)
        (2, 4), (2, 5), (2, 5), (2, 5),
    ];

    // Balance Druid — 31/0/20
    private static readonly (uint, uint)[] BalanceDruid =
    [
        // Level 10-14: Improved Wrath 5/5 (Tab 0, pos 0)
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
        // Level 15-19: Nature's Grasp 1/1 + Natural Weapons 4/5 (pos 1, 2)
        (0, 1), (0, 2), (0, 2), (0, 2), (0, 2),
        // Level 20-24: Natural Weapons 5/5 + Natural Shapeshifter 3/3 (pos 3)
        (0, 2), (0, 3), (0, 3), (0, 3), (0, 4),
        // Level 25-29: Improved Thorns 3/3 + Omen of Clarity 1/1 + Improved Moonfire 1/2 (pos 5, 6)
        (0, 4), (0, 4), (0, 5), (0, 6), (0, 6),
        // Level 30: Nature's Grace 1/1 (pos 7)
        (0, 7),
        // Level 31-35: Moonglow 3/3 + Vengeance 2/5 (pos 8, 9)
        (0, 8), (0, 8), (0, 8), (0, 9), (0, 9),
        // Level 36-40: Vengeance 3-5/5 + Moonkin Form 1/1 (pos 10)
        (0, 9), (0, 9), (0, 9), (0, 10), (0, 10),
        // Level 41: Moonkin Form
        // Actually Moonkin is not in vanilla... Let me use filler
        (0, 11),
        // Restoration dip
        // Level 42-46: Furor 5/5
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 47-51: Improved Mark of the Wild 5/5
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 52-56: Nature's Focus 5/5 (pos 2)
        (2, 2), (2, 2), (2, 2), (2, 2), (2, 2),
        // Level 57-60: Natural Shapeshifter 3/3 + Reflection 1/3 (pos 3, 4)
        (2, 3), (2, 3), (2, 3), (2, 4),
    ];

    // Restoration Druid — 0/11/40
    private static readonly (uint, uint)[] RestorationDruid =
    [
        // Level 10-14: Improved Mark of the Wild 5/5 (Tab 2, pos 1)
        (2, 1), (2, 1), (2, 1), (2, 1), (2, 1),
        // Level 15-19: Furor 5/5 (pos 0)
        (2, 0), (2, 0), (2, 0), (2, 0), (2, 0),
        // Level 20-24: Nature's Focus 5/5 (pos 2)
        (2, 2), (2, 2), (2, 2), (2, 2), (2, 2),
        // Level 25-29: Natural Shapeshifter 3/3 + Improved Healing Touch 2/5 (pos 3, 4)
        (2, 3), (2, 3), (2, 3), (2, 4), (2, 4),
        // Level 30: Insect Swarm 1/1 (pos 6)
        (2, 6),
        // Level 31-35: Improved Healing Touch 3-5/5 + Tranquil Spirit 2/5 (pos 7)
        (2, 4), (2, 4), (2, 4), (2, 7), (2, 7),
        // Level 36-40: Tranquil Spirit 3-5/5 + Gift of Nature 2/5 (pos 8)
        (2, 7), (2, 7), (2, 7), (2, 8), (2, 8),
        // Level 41-45: Gift of Nature 3-5/5 + Improved Rejuvenation 2/3 (pos 5)
        (2, 8), (2, 8), (2, 8), (2, 5), (2, 5),
        // Level 46-49: Improved Rejuvenation 3/3 + Swiftmend 1/1 (pos 9)
        (2, 5), (2, 9), (2, 10), (2, 10),
        // Level 50: Nature's Swiftness or more Resto
        (2, 10),
        // Feral dip
        // Level 51-55: Ferocity 5/5
        (1, 0), (1, 0), (1, 0), (1, 0), (1, 0),
        // Level 56-60: Feral Aggression 5/5
        (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
    ];
}
