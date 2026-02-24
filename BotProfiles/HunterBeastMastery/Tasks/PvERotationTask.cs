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

            if (Update(28))
                return;

            ObjectManager.StopAllMovement();

            IWoWItem gun = ObjectManager.GetEquippedItem(EquipSlot.Ranged);
            bool canUseRanged = gun != null && ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) > 5 && ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) < 34;
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
                var target = ObjectManager.GetTarget(ObjectManager.Player);
                TryCastSpell(HuntersMark, 0, 34, !target.HasDebuff(HuntersMark));
                TryCastSpell(ConcussiveShot, 0, 34, !target.HasDebuff(ConcussiveShot));
                if (!target.HasDebuff(SerpentSting))
                    TryCastSpell(SerpentSting, 0, 34);
                else if (ObjectManager.Aggressors.Count() > 1)
                    TryCastSpell(MultiShot, 0, 34);
                else if (ObjectManager.Player.ManaPercent > 60)
                    TryCastSpell(ArcaneShot, 0, 34);

                TryCastSpell(RapidFire, 0, int.MaxValue, target.HealthPercent > 80);
                return;
            }
            else
            {
                // melee — apply Wing Clip then kite back to ranged distance
                if (gun != null && TryCastSpell(WingClip, 0, 5, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(WingClip), callback: () => StartKite(1500)))
                    return;
                TryCastSpell(MongooseBite, 0, 5);
                TryCastSpell(RaptorStrike, 0, 5);
            }
        }
    }
}
