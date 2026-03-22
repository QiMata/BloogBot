using BotRunner.Interfaces;
using BotRunner.Tasks;
<<<<<<< HEAD
=======
using GameData.Core.Models;
>>>>>>> cpp_physics_system
using static BotRunner.Constants.Spellbook;

namespace MageFrost.Tasks
{
    public class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        // Vanilla 1.12.1 mage base spell ranges
        private const float FrostboltBaseRange = 30f;
        private const float FireBlastBaseRange = 20f;
        private const float CounterspellBaseRange = 30f;
        private const float IceLanceBaseRange = 30f;
        private const float DeepFreezeBaseRange = 30f;

        public void Update()
        {
            if (!EnsureTarget())
                return;

            if (IsKiting)
                return;

<<<<<<< HEAD
            if (Update(30))
=======
            if (Update(GetSpellRange(FrostboltBaseRange)))
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
            var distance = player.Position.DistanceTo(target.Position);

            // Interrupt casters
<<<<<<< HEAD
            TryCastSpell(Counterspell, 0, 30, target.IsCasting);
=======
            TryCastSpell(Counterspell, 0f, GetSpellRange(CounterspellBaseRange), target.IsCasting);
>>>>>>> cpp_physics_system

            // Ice Barrier for damage prevention
            TryCastSpell(IceBarrier, condition: !player.HasBuff(IceBarrier), castOnSelf: true);

            // Cold Snap to reset CDs when desperate
            TryCastSpell(ColdSnap, condition: player.HealthPercent < 30, castOnSelf: true);

            // Frost Nova + kite when target is in melee
<<<<<<< HEAD
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
=======
            TryCastSpell(FrostNova, 0f, 10f, distance < 10, callback: () => StartKite(2500));

            // Cone of Cold for close range
            TryCastSpell(ConeOfCold, 0f, 8f, distance < 8);

            // Shatter combo on frozen/FoF targets
            TryCastSpell(DeepFreeze, 0f, GetSpellRange(DeepFreezeBaseRange), player.HasBuff(FingersOfFrost));
            TryCastSpell(IceLance, 0f, GetSpellRange(IceLanceBaseRange), player.HasBuff(FingersOfFrost));

            // Primary nuke
            TryCastSpell(Frostbolt, 0f, GetSpellRange(FrostboltBaseRange));

            // Instant damage when moving
            TryCastSpell(FireBlast, 0f, GetSpellRange(FireBlastBaseRange));
>>>>>>> cpp_physics_system

            // Wand fallback
            if (player.ManaPercent < 5)
                ObjectManager.StartWandAttack();
        }
    }
}
