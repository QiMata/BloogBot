using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace WarriorProtection.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!EnsureTarget())
                return;

            if (Update(5))
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
            ObjectManager.StartMeleeAttack();

            string stance = ObjectManager.Player.CurrentStance;

            TryCastSpell(DefensiveStance, 0, int.MaxValue, stance != DefensiveStance);

            TryUseAbility(BerserkerRage, condition: stance == BerserkerStance);

            TryUseAbility(ShieldBash, 10, target.IsCasting);

            TryUseAbility(ConcussionBlow, 15, !target.IsStunned);

            TryUseAbility(Revenge, 5, stance == DefensiveStance);

            TryUseAbility(ShieldSlam, 20);

            TryUseAbility(Hamstring, 10, !target.HasDebuff(Hamstring));

            TryUseAbility(IntimidatingShout, 25, ObjectManager.Aggressors.Count() > 1);
        }
    }
}
