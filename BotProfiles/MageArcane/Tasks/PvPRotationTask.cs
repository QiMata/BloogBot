using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace MageArcane.Tasks
{
    public class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        public void Update()
        {
            if (!EnsureTarget())
                return;

            if (IsKiting)
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

            // Interrupt casters
            TryCastSpell(Counterspell, 0, 29, target.IsCasting);

            // Emergency mana shield
            TryCastSpell(ManaShield, condition: player.HealthPercent < 25 && player.ManaPercent > 20, castOnSelf: true);

            // Frost Nova + kite when target is close
            var distance = player.Position.DistanceTo(target.Position);
            TryCastSpell(FrostNova, 0, 10, distance < 10, callback: () => StartKite(2000));

            // Burst cooldowns
            TryCastSpell(PresenceOfMind, condition: true, castOnSelf: true);
            TryCastSpell(ArcanePower, condition: target.HealthPercent > 40, castOnSelf: true);

            // AoE for multiple aggressors
            TryCastSpell(ArcaneExplosion, 0, 10, ObjectManager.Aggressors.Count() > 2);

            // Primary rotation
            TryCastSpell(ArcaneBarrage, 0, 30);
            TryCastSpell(ArcaneMissiles, 0, 29);
            TryCastSpell(FireBlast, 0, 20);

            // Wand fallback at very low mana
            if (player.ManaPercent < 5)
                ObjectManager.StartWandAttack();
        }
    }
}
