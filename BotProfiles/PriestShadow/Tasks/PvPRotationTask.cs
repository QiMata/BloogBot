using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PriestShadow.Tasks
{
    public class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        public void Update()
        {
            if (!EnsureTarget())
                return;

            if (Update(30))
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
            TryCastSpell(PsychicScream, 0, 8, player.HealthPercent < 35 || ObjectManager.Aggressors.Count() > 2);

            // Dispel Magic on self
            TryCastSpell(DispelMagic, condition: player.HasMagicDebuff, castOnSelf: true);

            // DoTs on target
            TryCastSpell(VampiricEmbrace, 0, 29, !target.HasDebuff(VampiricEmbrace));
            TryCastSpell(ShadowWordPain, 0, 29, !target.HasDebuff(ShadowWordPain));

            // Mind Blast on cooldown
            TryCastSpell(MindBlast, 0, 29);

            // Mind Flay as filler
            TryCastSpell(MindFlay, 0, 19);

            // Wand fallback at low mana
            if (player.ManaPercent < 8)
                ObjectManager.StartWandAttack();
        }
    }
}
