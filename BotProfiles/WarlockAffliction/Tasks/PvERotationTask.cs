using BotRunner.Interfaces;
using BotRunner.Tasks;
using ForegroundBotRunner.Mem;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockAffliction.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        private double GetDebuffRemaining(string debuff)
        {
            var result = Functions.LuaCallWithResult(
                $"local _,_,_,_,_,expires = UnitDebuff('target','{debuff}'); if expires then {{0}} = expires - GetTime() else {{0}} = 0 end");
            return result.Length > 0 && double.TryParse(result[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        private double GetCastTime(string spell)
        {
            var result = Functions.LuaCallWithResult($"local _,_,_,castTime = GetSpellInfo('{spell}'); if castTime then {{0}} = castTime / 1000 else {{0}} = 0 end");
            return result.Length > 0 && double.TryParse(result[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        private bool ShouldReapply(string debuff, double threshold = 3)
        {
            var castTime = GetCastTime(debuff);
            return !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(debuff) ||
                   GetDebuffRemaining(debuff) < threshold + castTime;
        }

        private bool AllDotsActive() =>
            GetDebuffRemaining(CurseOfAgony) > 3 &&
            GetDebuffRemaining(Immolate) > 3 &&
            GetDebuffRemaining(Corruption) > 3 &&
            GetDebuffRemaining(SiphonLife) > 3 &&
            GetDebuffRemaining(Haunt) > 3;

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

            var target = ObjectManager.GetTarget(ObjectManager.Player);

            // if target is low on health, turn off wand and cast drain soul
            if (target.HealthPercent <= 20)
            {
                ObjectManager.Player.StopWand();
                TryCastSpell(DrainSoul, 0, 29);
                return;
            }

            TryCastSpell(CurseOfAgony, 0, 28, ShouldReapply(CurseOfAgony) && target.HealthPercent > 90);

            TryCastSpell(Immolate, 0, 28, ShouldReapply(Immolate) && target.HealthPercent > 30);

            TryCastSpell(Corruption, 0, 28, ShouldReapply(Corruption) && target.HealthPercent > 30);

            TryCastSpell(SiphonLife, 0, 28, ShouldReapply(SiphonLife) && target.HealthPercent > 50);

            TryCastSpell(Haunt, 0, 30, ShouldReapply(Haunt));

            if (AllDotsActive())
                TryCastSpell(ShadowBolt, 0, 28, target.HealthPercent > 40);
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
