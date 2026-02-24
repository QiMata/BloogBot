using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace HunterMarksmanship.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }


        public void Update()
        {
            if (IsKiting)
                return;

            ObjectManager.Pet?.Attack();
            if (!EnsureTarget())
                return;

            if (Update(34))
                return;

            ObjectManager.StopAllMovement();

            IWoWItem bow = ObjectManager.GetEquippedItem(EquipSlot.Ranged);
            bool ranged = bow != null && ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) > 5 &&
                           ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) < 34;

            if (ranged)
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

            // melee — apply Wing Clip then kite back to ranged distance
            if (bow != null && TryCastSpell(WingClip, 0, 5, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(WingClip), callback: () => StartKite(1500)))
                return;
            TryCastSpell(MongooseBite, 0, 5);
            TryCastSpell(RaptorStrike, 0, 5);
        }
        public override void PerformCombatRotation() => Update();
    }
}
