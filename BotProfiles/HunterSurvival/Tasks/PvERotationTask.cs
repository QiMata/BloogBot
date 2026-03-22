using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace HunterSurvival.Tasks
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
<<<<<<< HEAD
            IWoWItem rangedWeapon = ObjectManager.GetEquippedItem(EquipSlot.Ranged);
            bool canShoot = rangedWeapon != null && ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) > 5 &&
                             ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) < 34;
=======
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            var meleeRange = GetMeleeRange(target);
            IWoWItem rangedWeapon = ObjectManager.GetEquippedItem(EquipSlot.Ranged);
            var distanceToTarget = ObjectManager.Player.Position.DistanceTo(target.Position);
            bool canShoot = rangedWeapon != null && distanceToTarget > HunterDeadZone && distanceToTarget < rangedRange;
>>>>>>> cpp_physics_system

            if (canShoot)
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
<<<<<<< HEAD
            if (rangedWeapon != null && TryCastSpell(WingClip, 0, 5, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(WingClip), callback: () => StartKite(1500)))
                return;
            TryCastSpell(MongooseBite, 0, 5);
            TryCastSpell(RaptorStrike, 0, 5);
=======
            if (rangedWeapon != null && TryCastSpell(WingClip, 0f, meleeRange, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(WingClip), callback: () => StartKite(1500)))
                return;
            TryCastSpell(MongooseBite, 0f, meleeRange);
            TryCastSpell(RaptorStrike, 0f, meleeRange);
>>>>>>> cpp_physics_system
        }

        public override void PerformCombatRotation() => Update();
    }
}
