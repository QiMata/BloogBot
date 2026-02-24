using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PriestHoly.Tasks
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

            // Power Word: Shield for survival (check Weakened Soul)
            TryCastSpell(PowerWordShield, condition: !player.HasDebuff(WeakenedSoul), castOnSelf: true);

            // Inner Fire for armor
            TryCastSpell(InnerFire, condition: !player.HasBuff(InnerFire), castOnSelf: true);

            // Psychic Scream when low HP or overwhelmed
            TryCastSpell(PsychicScream, 0, 8, player.HealthPercent < 40 || ObjectManager.Aggressors.Count() > 2);

            // Renew HoT on self
            TryCastSpell(Renew, condition: player.HealthPercent < 75 && !player.HasBuff(Renew), castOnSelf: true);

            // Heal self when critical
            TryCastSpell(Heal, condition: player.HealthPercent < 45, castOnSelf: true);

            // Dispel Magic on self
            TryCastSpell(DispelMagic, condition: player.HasMagicDebuff, castOnSelf: true);

            // Damage rotation
            TryCastSpell(HolyFire, 0, 29);
            TryCastSpell(ShadowWordPain, 0, 29, !target.HasDebuff(ShadowWordPain));
            TryCastSpell(Smite, 0, 29);

            // Wand fallback
            if (player.ManaPercent < 10)
                ObjectManager.StartWandAttack();
        }
    }
}
