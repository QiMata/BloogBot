using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace MageFrost.Tasks
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
            var distance = player.Position.DistanceTo(target.Position);

            // Interrupt casters
            TryCastSpell(Counterspell, 0, 30, target.IsCasting);

            // Ice Barrier for damage prevention
            TryCastSpell(IceBarrier, condition: !player.HasBuff(IceBarrier), castOnSelf: true);

            // Cold Snap to reset CDs when desperate
            TryCastSpell(ColdSnap, condition: player.HealthPercent < 30, castOnSelf: true);

            // Frost Nova + kite when target is in melee
            TryCastSpell(FrostNova, 0, 10, distance < 10, callback: () => StartKite(2500));

            // Cone of Cold for close range
            TryCastSpell(ConeOfCold, 0, 8, distance < 8);

            // Shatter combo on frozen/FoF targets
            TryCastSpell(DeepFreeze, 0, 30, player.HasBuff(FingersOfFrost));
            TryCastSpell(IceLance, 0, 30, player.HasBuff(FingersOfFrost));

            // Primary nuke
            TryCastSpell(Frostbolt, 0, 30);

            // Instant damage when moving
            TryCastSpell(FireBlast, 0, 20);

            // Wand fallback
            if (player.ManaPercent < 5)
                ObjectManager.StartWandAttack();
        }
    }
}
