using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using static BotRunner.Constants.Spellbook;

namespace HunterSurvival.Tasks
{
    internal class PvPRotationTask : CombatRotationTask, IBotTask
    {
        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            ObjectManager.Pet?.Attack();
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null)
                ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);

            if (Update(34))
                return;

            ObjectManager.Player.StopAllMovement();
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null) return;

            bool ranged = ObjectManager.Player.Position.DistanceTo(target.Position) > 5 &&
                           ObjectManager.Player.Position.DistanceTo(target.Position) < 34;
            if (ranged)
            {
                TryCastSpell(ConcussiveShot, 0, 34, !target.HasDebuff(ConcussiveShot));
                TryCastSpell(ArcaneShot, 0, 34);
                return;
            }

            TryCastSpell(WingClip, 0, 5, !target.HasDebuff(WingClip));
            TryCastSpell(RaptorStrike, 0, 5);
        }

        public override void PerformCombatRotation() => Update();
    }
}
