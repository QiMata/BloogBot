using BotRunner.Interfaces;
using BotRunner.Tasks;
<<<<<<< HEAD
=======
using GameData.Core.Models;
>>>>>>> cpp_physics_system
using static BotRunner.Constants.Spellbook;

namespace PriestShadow.Tasks
{
    public class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        // Vanilla 1.12.1 priest base spell ranges
        private const float ShadowWordPainBaseRange = 30f;
        private const float MindBlastBaseRange = 30f;
        private const float MindFlayBaseRange = 20f;
        private const float VampiricEmbraceBaseRange = 30f;

        public void Update()
        {
            if (!EnsureTarget())
                return;

<<<<<<< HEAD
            if (Update(30))
=======
            if (Update(GetSpellRange(ShadowWordPainBaseRange)))
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

            // Maintain Shadowform
            TryCastSpell(ShadowForm, condition: !player.HasBuff(ShadowForm), castOnSelf: true);

            // Power Word: Shield for survival (check Weakened Soul)
            TryCastSpell(PowerWordShield, condition: player.HealthPercent < 60 && !player.HasDebuff(WeakenedSoul), castOnSelf: true);

            // Psychic Scream when overwhelmed or low HP
<<<<<<< HEAD
            TryCastSpell(PsychicScream, 0, 8, player.HealthPercent < 35 || ObjectManager.Aggressors.Count() > 2);
=======
            TryCastSpell(PsychicScream, 0f, 8f, player.HealthPercent < 35 || ObjectManager.Aggressors.Count() > 2);
>>>>>>> cpp_physics_system

            // Dispel Magic on self
            TryCastSpell(DispelMagic, condition: player.HasMagicDebuff, castOnSelf: true);

            // DoTs on target
<<<<<<< HEAD
            TryCastSpell(VampiricEmbrace, 0, 29, !target.HasDebuff(VampiricEmbrace));
            TryCastSpell(ShadowWordPain, 0, 29, !target.HasDebuff(ShadowWordPain));

            // Mind Blast on cooldown
            TryCastSpell(MindBlast, 0, 29);

            // Mind Flay as filler
            TryCastSpell(MindFlay, 0, 19);
=======
            TryCastSpell(VampiricEmbrace, 0f, GetSpellRange(VampiricEmbraceBaseRange), !target.HasDebuff(VampiricEmbrace));
            TryCastSpell(ShadowWordPain, 0f, GetSpellRange(ShadowWordPainBaseRange), !target.HasDebuff(ShadowWordPain));

            // Mind Blast on cooldown
            TryCastSpell(MindBlast, 0f, GetSpellRange(MindBlastBaseRange));

            // Mind Flay as filler
            TryCastSpell(MindFlay, 0f, GetSpellRange(MindFlayBaseRange));
>>>>>>> cpp_physics_system

            // Wand fallback at low mana
            if (player.ManaPercent < 8)
                ObjectManager.StartWandAttack();
        }
    }
}
