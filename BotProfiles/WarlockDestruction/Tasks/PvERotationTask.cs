using BotRunner.Interfaces;
using BotRunner.Tasks;
using ForegroundBotRunner.Mem;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockDestruction.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {

        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            AssignDPSTarget();

            if (ObjectManager.GetTarget(ObjectManager.Player) == null) return;

            //if (Container.State.TankInPosition)
            //{
            //    if (MoveTowardsTarget())
            //        return;

            //    PerformCombatRotation();
            //}
            //else if (MoveBehindTankSpot(15))
            //    return;
            //else
            //    ObjectManager.Player.StopAllMovement();
        }
        public override void PerformCombatRotation()
        {
            ObjectManager.Player.StopAllMovement();
            ObjectManager.Player.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);
            ObjectManager.Pet?.Attack();

            UseCooldowns();

            if (ObjectManager.Pet != null)
            {
                if (ObjectManager.Player.HealthPercent < 40 && ObjectManager.Pet.CanUse(Sacrifice))
                    ObjectManager.Pet.Cast(Sacrifice);

                if (ObjectManager.Pet.CanUse(Torment))
                    ObjectManager.Pet.Cast(Torment);
            }

            TryCastSpell(LifeTap, 0, int.MaxValue, ObjectManager.Player.HealthPercent > 85 && ObjectManager.Player.ManaPercent < 80);

            // if target is low on health, turn off wand and cast drain soul
            if (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 20)
            {
                ObjectManager.Player.StopWand();
                TryCastSpell(DrainSoul, 0, 29);
            }
            else
            {
                TryCastSpell(CurseOfAgony, 0, 28, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(CurseOfAgony) && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 90);

                TryCastSpell(Immolate, 0, 28, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Immolate) && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 30);

                TryCastSpell(Conflagrate, 0, 28, ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Immolate));

                TryCastSpell(Corruption, 0, 28, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Corruption) && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 30);

                TryCastSpell(SiphonLife, 0, 28, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(SiphonLife) && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50);

                TryCastSpell(ShadowBolt, 0, 28, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 40);
            }
        }

        private void UseCooldowns()
        {
            var trinket1 = ObjectManager.GetEquippedItem(EquipSlot.Trinket1);
            var trinket2 = ObjectManager.GetEquippedItem(EquipSlot.Trinket2);

            if (ItemReady(trinket1))
                trinket1.Use();

            if (ItemReady(trinket2))
                trinket2.Use();

            if (ObjectManager.Player.IsSpellReady(BloodFury))
                ObjectManager.Player.CastSpell(BloodFury);

            if (ObjectManager.Player.IsSpellReady(Berserking))
                ObjectManager.Player.CastSpell(Berserking);
        }

        private bool ItemReady(IWoWItem? item)
        {
            if (item == null)
                return false;

            var result = Functions.LuaCallWithResult($"startTime, duration, enable = GetItemCooldown({item.ItemId}); {{0}} = duration;");
            return result.Length > 0 && result[0] == "0";
        }
    }
}
