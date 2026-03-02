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
        // Vanilla 1.12.1 hunter base spell ranges
        private const float RangedAttackRange = 35f;
        private const float HunterDeadZone = 8f;

        internal PvERotationTask(IBotContext botContext) : base(botContext) { }


        public void Update()
        {
            if (IsKiting)
                return;

            ObjectManager.Pet?.Attack();
            if (!EnsureTarget())
                return;

            var rangedRange = GetSpellRange(RangedAttackRange);
            if (Update(rangedRange))
                return;

            ObjectManager.StopAllMovement();

            var target = ObjectManager.GetTarget(ObjectManager.Player);
            var meleeRange = GetMeleeRange(target);
            IWoWItem bow = ObjectManager.GetEquippedItem(EquipSlot.Ranged);
            var distanceToTarget = ObjectManager.Player.Position.DistanceTo(target.Position);
            bool ranged = bow != null && distanceToTarget > HunterDeadZone && distanceToTarget < rangedRange;

            if (ranged)
            {
                TryCastSpell(HuntersMark, 0f, rangedRange, !target.HasDebuff(HuntersMark));
                TryCastSpell(ConcussiveShot, 0f, rangedRange, !target.HasDebuff(ConcussiveShot));
                if (!target.HasDebuff(SerpentSting))
                    TryCastSpell(SerpentSting, 0f, rangedRange);
                else if (ObjectManager.Aggressors.Count() > 1)
                    TryCastSpell(MultiShot, 0f, rangedRange);
                else if (ObjectManager.Player.ManaPercent > 60)
                    TryCastSpell(ArcaneShot, 0f, rangedRange);
                return;
            }

            // melee — apply Wing Clip then kite back to ranged distance
            if (bow != null && TryCastSpell(WingClip, 0f, meleeRange, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(WingClip), callback: () => StartKite(1500)))
                return;
            TryCastSpell(MongooseBite, 0f, meleeRange);
            TryCastSpell(RaptorStrike, 0f, meleeRange);
        }
        public override void PerformCombatRotation() => Update();
    }
}
