using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using static BotRunner.Constants.Spellbook;

namespace HunterSurvival.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            ObjectManager.Pet?.Attack();
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
                ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);

            if (Update(34))
                return;

            ObjectManager.Player.StopAllMovement();
            IWoWItem ranged = ObjectManager.GetEquippedItem(EquipSlot.Ranged);
            bool canShoot = ranged != null && ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) > 5 &&
                             ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) < 34;

            if (canShoot)
            {
                var target = ObjectManager.GetTarget(ObjectManager.Player);
                TryCastSpell(HuntersMark, 0, 34, !target.HasDebuff(HuntersMark));
                TryCastSpell(ConcussiveShot, 0, 34, !target.HasDebuff(ConcussiveShot));
                if (!target.HasDebuff(SerpentSting))
                    TryCastSpell(SerpentSting, 0, 34);
                else if (ObjectManager.Aggressors.Count() > 1)
                    TryCastSpell(MultiShot, 0, 34);
                else if (ObjectManager.Player.ManaPercent > 60)
                    TryCastSpell(ArcaneShot, 0, 34);
                return;
            }

            TryCastSpell(MongooseBite, 0, 5);
            TryCastSpell(WingClip, 0, 5, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(WingClip));
            TryCastSpell(RaptorStrike, 0, 5);
        }

        public override void PerformCombatRotation() => Update();
    }
}
