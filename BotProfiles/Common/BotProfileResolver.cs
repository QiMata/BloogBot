using System;
using System.Collections.Generic;
using GameData.Core.Enums;

namespace BotProfiles.Common
{
    /// <summary>
    /// Resolves a BotProfile spec name string (e.g., "WarriorFury") to a BotBase instance.
    /// Used by both ForegroundBotRunner and BackgroundBotRunner to support configurable spec selection
    /// via the WWOW_CHARACTER_SPEC environment variable.
    /// </summary>
    public static class BotProfileResolver
    {
        /// <summary>
        /// All known spec name → BotBase factory mappings. Keys are case-insensitive.
        /// </summary>
        private static readonly Dictionary<string, Func<BotBase>> SpecFactories =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Warrior
                ["WarriorArms"] = () => new WarriorArms.WarriorArms(),
                ["WarriorFury"] = () => new WarriorFury.WarriorFury(),
                ["WarriorProtection"] = () => new WarriorProtection.WarriorProtection(),

                // Paladin
                ["PaladinHoly"] = () => new PaladinHoly.PaladinHoly(),
                ["PaladinProtection"] = () => new PaladinProtection.PaladinProtection(),
                ["PaladinRetribution"] = () => new PaladinRetribution.PaladinRetribution(),

                // Rogue
                ["RogueAssassin"] = () => new RogueAssassin.RogueAssassin(),
                ["RogueCombat"] = () => new RogueCombat.RogueCombat(),
                ["RogueSubtlety"] = () => new RogueSubtlety.RogueSubtlety(),

                // Hunter
                ["HunterBeastMastery"] = () => new HunterBeastMastery.HunterBeastMastery(),
                ["HunterMarksmanship"] = () => new HunterMarksmanship.HunterMarksmanship(),
                ["HunterSurvival"] = () => new HunterSurvival.HunterSurvival(),

                // Priest
                ["PriestDiscipline"] = () => new PriestDiscipline.PriestDiscipline(),
                ["PriestHoly"] = () => new PriestHoly.PriestHoly(),
                ["PriestShadow"] = () => new PriestShadow.PriestShadow(),

                // Shaman
                ["ShamanElemental"] = () => new ShamanElemental.ShamanElemental(),
                ["ShamanEnhancement"] = () => new ShamanEnhancement.ShamanEnhancement(),
                ["ShamanRestoration"] = () => new ShamanRestoration.ShamanRestoration(),

                // Mage
                ["MageArcane"] = () => new MageArcane.MageArcane(),
                ["MageFire"] = () => new MageFire.MageFire(),
                ["MageFrost"] = () => new MageFrost.MageFrost(),

                // Warlock
                ["WarlockAffliction"] = () => new WarlockAffliction.WarlockAffliction(),
                ["WarlockDemonology"] = () => new WarlockDemonology.WarlockDemonology(),
                ["WarlockDestruction"] = () => new WarlockDestruction.WarlockDestruction(),

                // Druid
                ["DruidBalance"] = () => new DruidBalance.DruidBalance(),
                ["DruidFeral"] = () => new DruidFeral.DruidFeral(),
                ["DruidRestoration"] = () => new DruidRestoration.DruidRestoration(),
            };

        /// <summary>
        /// Attempts to resolve a spec name string to a BotBase instance.
        /// Returns null if the spec name is not recognized.
        /// </summary>
        public static BotBase? TryResolve(string specName)
        {
            if (string.IsNullOrWhiteSpace(specName))
                return null;

            return SpecFactories.TryGetValue(specName, out var factory) ? factory() : null;
        }

        /// <summary>
        /// Returns the default BotBase for a given class. Matches the existing hardcoded defaults
        /// in CreateClassContainer methods.
        /// </summary>
        public static BotBase GetDefaultForClass(Class @class) => @class switch
        {
            Class.Warrior => new WarriorArms.WarriorArms(),
            Class.Paladin => new PaladinRetribution.PaladinRetribution(),
            Class.Rogue => new RogueCombat.RogueCombat(),
            Class.Hunter => new HunterBeastMastery.HunterBeastMastery(),
            Class.Priest => new PriestDiscipline.PriestDiscipline(),
            Class.Shaman => new ShamanEnhancement.ShamanEnhancement(),
            Class.Mage => new MageArcane.MageArcane(),
            Class.Warlock => new WarlockAffliction.WarlockAffliction(),
            Class.Druid => new DruidRestoration.DruidRestoration(),
            _ => new WarriorArms.WarriorArms()
        };

        /// <summary>
        /// Resolves the BotProfile to use: if specName is set and valid, use it; otherwise fall back to class default.
        /// </summary>
        public static BotBase Resolve(string? specName, Class @class)
        {
            if (!string.IsNullOrWhiteSpace(specName))
            {
                var profile = TryResolve(specName);
                if (profile != null)
                    return profile;
            }

            return GetDefaultForClass(@class);
        }
    }
}
