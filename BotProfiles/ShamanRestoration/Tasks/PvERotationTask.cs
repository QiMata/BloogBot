using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace ShamanRestoration.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        // Vanilla 1.12.1 shaman base spell ranges
        private const float LightningBoltBaseRange = 30f;
        private const float FlameShockBaseRange = 20f;
        private const float HealBaseRange = 40f;

        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!EnsureTarget())
                return;

<<<<<<< HEAD
            if (Update(12))
=======
            // Stay close for totem effectiveness and melee fallback
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (Update(target != null ? GetMeleeRange(target) * 2f : 12f))
>>>>>>> cpp_physics_system
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            // Group healing: heal party members before DPS
            if (IsInGroup)
            {
<<<<<<< HEAD
                if (TryCastHeal(HealingWave, 60, 40)) return;
            }
            else
            {
                TryCastSpell(HealingWave, 0, int.MaxValue, ObjectManager.Player.HealthPercent < 50, castOnSelf: true);
=======
                if (TryCastHeal(HealingWave, 60, GetSpellRange(HealBaseRange))) return;
            }
            else
            {
                TryCastSpell(HealingWave, condition: ObjectManager.Player.HealthPercent < 50, castOnSelf: true);
>>>>>>> cpp_physics_system
            }

            TryCastSpell(ManaSpringTotem, condition: !ObjectManager.Units.Any(u => u.Position.DistanceTo(ObjectManager.Player.Position) < 19 && u.HealthPercent > 0 && u.Name.Contains(ManaSpringTotem)), castOnSelf: true);

            TryCastSpell(GroundingTotem, condition: ObjectManager.Aggressors.Any(a => a.IsCasting && ObjectManager.GetTarget(ObjectManager.Player).Mana > 0), castOnSelf: true);

            TryCastSpell(LightningBolt, 0f, GetSpellRange(LightningBoltBaseRange), ObjectManager.Player.ManaPercent > 20);

            TryCastSpell(FlameShock, 0f, GetSpellRange(FlameShockBaseRange), !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(FlameShock));
        }
    }
}
