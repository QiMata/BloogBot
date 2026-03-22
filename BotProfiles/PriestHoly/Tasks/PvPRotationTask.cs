using BotRunner.Interfaces;
using BotRunner.Tasks;
<<<<<<< HEAD
=======
using GameData.Core.Models;
>>>>>>> cpp_physics_system
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

<<<<<<< HEAD
            if (Update(30))
=======
            if (Update(GetSpellRange(SmiteBaseRange)))
>>>>>>> cpp_physics_system
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
<<<<<<< HEAD
            TryCastSpell(PsychicScream, 0, 8, player.HealthPercent < 40 || ObjectManager.Aggressors.Count() > 2);
=======
            TryCastSpell(PsychicScream, 0f, 8f, player.HealthPercent < 40 || ObjectManager.Aggressors.Count() > 2);
>>>>>>> cpp_physics_system

            // Renew HoT on self
            TryCastSpell(Renew, condition: player.HealthPercent < 75 && !player.HasBuff(Renew), castOnSelf: true);

            // Heal self when critical
            TryCastSpell(Heal, condition: player.HealthPercent < 45, castOnSelf: true);

            // Dispel Magic on self
            TryCastSpell(DispelMagic, condition: player.HasMagicDebuff, castOnSelf: true);

            // Damage rotation
<<<<<<< HEAD
            TryCastSpell(HolyFire, 0, 29);
            TryCastSpell(ShadowWordPain, 0, 29, !target.HasDebuff(ShadowWordPain));
            TryCastSpell(Smite, 0, 29);
=======
            TryCastSpell(HolyFire, 0f, GetSpellRange(HolyFireBaseRange));
            TryCastSpell(ShadowWordPain, 0f, GetSpellRange(ShadowWordPainBaseRange), !target.HasDebuff(ShadowWordPain));
            TryCastSpell(Smite, 0f, GetSpellRange(SmiteBaseRange));
>>>>>>> cpp_physics_system

            // Wand fallback
            if (player.ManaPercent < 10)
                ObjectManager.StartWandAttack();
        }
    }
}
