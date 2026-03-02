using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace HunterBeastMastery.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        // Vanilla 1.12.1 hunter base spell ranges
        private const float RangedAttackRange = 35f;  // Auto Shot, Arcane Shot, Multi-Shot, etc.
        private const float HunterDeadZone = 8f;      // Minimum range for ranged attacks

        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public override void PerformCombatRotation()
        {

        }

        public void Update()
        {
            if (IsKiting)
                return;

            // ensure our pet is helping and alive
            ObjectManager.Pet?.Attack();
            if (ObjectManager.Pet == null && ObjectManager.IsSpellReady(CallPet))
                ObjectManager.CastSpell(CallPet);
            else if (ObjectManager.Pet != null && ObjectManager.Pet.HealthPercent < 40)
                TryCastSpell(MendPet, castOnSelf: true);

            if (!EnsureTarget())
                return;

            var rangedRange = GetSpellRange(RangedAttackRange);
            if (Update(rangedRange))
                return;

            ObjectManager.StopAllMovement();

            var target = ObjectManager.GetTarget(ObjectManager.Player);
            var meleeRange = GetMeleeRange(target);
            IWoWItem gun = ObjectManager.GetEquippedItem(EquipSlot.Ranged);
            var distanceToTarget = ObjectManager.Player.Position.DistanceTo(target.Position);
            bool canUseRanged = gun != null && distanceToTarget > HunterDeadZone && distanceToTarget < rangedRange;
            if (gun == null)
            {
                ObjectManager.StartMeleeAttack();
            }
            else if (canUseRanged && ObjectManager.Player.ManaPercent < 60)
            {
                ObjectManager.StartRangedAttack();
            }
            else if (canUseRanged)
            {
                TryCastSpell(HuntersMark, 0f, rangedRange, !target.HasDebuff(HuntersMark));
                TryCastSpell(ConcussiveShot, 0f, rangedRange, !target.HasDebuff(ConcussiveShot));
                if (!target.HasDebuff(SerpentSting))
                    TryCastSpell(SerpentSting, 0f, rangedRange);
                else if (ObjectManager.Aggressors.Count() > 1)
                    TryCastSpell(MultiShot, 0f, rangedRange);
                else if (ObjectManager.Player.ManaPercent > 60)
                    TryCastSpell(ArcaneShot, 0f, rangedRange);

                TryCastSpell(RapidFire, 0f, float.MaxValue, target.HealthPercent > 80);
                return;
            }
            else
            {
                // melee — apply Wing Clip then kite back to ranged distance
                if (gun != null && TryCastSpell(WingClip, 0f, meleeRange, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(WingClip), callback: () => StartKite(1500)))
                    return;
                TryCastSpell(MongooseBite, 0f, meleeRange);
                TryCastSpell(RaptorStrike, 0f, meleeRange);
            }
        }
    }
}
