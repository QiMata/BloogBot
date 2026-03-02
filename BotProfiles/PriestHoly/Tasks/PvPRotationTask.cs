using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace PriestHoly.Tasks
{
    public class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        // Vanilla 1.12.1 priest base spell ranges
        private const float ShadowWordPainBaseRange = 30f;
        private const float SmiteBaseRange = 30f;
        private const float HolyFireBaseRange = 30f;

        public void Update()
        {
            if (!EnsureTarget())
                return;

            if (Update(GetSpellRange(SmiteBaseRange)))
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null)
                return;

            ObjectManager.StopAllMovement();
            ObjectManager.Face(target.Position);

            var player = ObjectManager.Player;

            // Power Word: Shield for survival (check Weakened Soul)
            TryCastSpell(PowerWordShield, condition: !player.HasDebuff(WeakenedSoul), castOnSelf: true);

            // Inner Fire for armor
            TryCastSpell(InnerFire, condition: !player.HasBuff(InnerFire), castOnSelf: true);

            // Psychic Scream when low HP or overwhelmed
            TryCastSpell(PsychicScream, 0f, 8f, player.HealthPercent < 40 || ObjectManager.Aggressors.Count() > 2);

            // Renew HoT on self
            TryCastSpell(Renew, condition: player.HealthPercent < 75 && !player.HasBuff(Renew), castOnSelf: true);

            // Heal self when critical
            TryCastSpell(Heal, condition: player.HealthPercent < 45, castOnSelf: true);

            // Dispel Magic on self
            TryCastSpell(DispelMagic, condition: player.HasMagicDebuff, castOnSelf: true);

            // Damage rotation
            TryCastSpell(HolyFire, 0f, GetSpellRange(HolyFireBaseRange));
            TryCastSpell(ShadowWordPain, 0f, GetSpellRange(ShadowWordPainBaseRange), !target.HasDebuff(ShadowWordPain));
            TryCastSpell(Smite, 0f, GetSpellRange(SmiteBaseRange));

            // Wand fallback
            if (player.ManaPercent < 10)
                ObjectManager.StartWandAttack();
        }
    }
}
