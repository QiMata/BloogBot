using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace HunterSurvival.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            ObjectManager.Pet?.Attack();
            if (!EnsureTarget())
                return;

            if (Update(34))
                return;

            ObjectManager.StopAllMovement();
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
