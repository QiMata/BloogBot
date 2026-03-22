using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace HunterSurvival.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        // Vanilla 1.12.1 hunter base spell ranges
        private const float RangedAttackRange = 35f;
        private const float HunterDeadZone = 8f;

        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            ObjectManager.Pet?.Attack();
            if (!EnsureTarget())
                return;

<<<<<<< HEAD
            if (Update(34))
=======
            var rangedRange = GetSpellRange(RangedAttackRange);
            if (Update(rangedRange))
>>>>>>> cpp_physics_system
                return;

            ObjectManager.StopAllMovement();
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null) return;

            var meleeRange = GetMeleeRange(target);
            var distanceToTarget = ObjectManager.Player.Position.DistanceTo(target.Position);
            bool ranged = distanceToTarget > HunterDeadZone && distanceToTarget < rangedRange;
            if (ranged)
            {
                TryCastSpell(ConcussiveShot, 0f, rangedRange, !target.HasDebuff(ConcussiveShot));
                TryCastSpell(ArcaneShot, 0f, rangedRange);
                return;
            }

            TryCastSpell(WingClip, 0f, meleeRange, !target.HasDebuff(WingClip));
            TryCastSpell(RaptorStrike, 0f, meleeRange);
        }

        public override void PerformCombatRotation() => Update();
    }
}
