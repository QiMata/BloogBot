using ForegroundBotRunner.Objects;
using GameData.Core.Enums;
using Serilog;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Creates the appropriate combat rotation for the player's class.
    /// </summary>
    public static class CombatRotationFactory
    {
        public static ICombatRotation Create(LocalPlayer player)
        {
            var playerClass = player.Class;
            Log.Information("[CombatRotation] Creating rotation for {Class}", playerClass);

            return playerClass switch
            {
                Class.Warrior => new WarriorRotation(),
                Class.Rogue => new RogueRotation(),
                Class.Warlock => new WarlockRotation(),
                Class.Mage => new MageRotation(),
                Class.Hunter => new HunterRotation(),
                Class.Priest => new PriestRotation(),
                Class.Paladin => new PaladinRotation(),
                Class.Shaman => new ShamanRotation(),
                Class.Druid => new DruidRotation(),
                _ => new AutoAttackRotation(),
            };
        }
    }
}
