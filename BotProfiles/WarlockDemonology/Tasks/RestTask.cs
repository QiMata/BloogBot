using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace WarlockDemonology.Tasks
{
    public class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            if (ObjectManager.Pet != null && ObjectManager.Pet.HealthPercent < 60 && ObjectManager.Pet.CanUse(ConsumeShadows) && !ObjectManager.Pet.IsCasting && ObjectManager.Pet.ChannelingId == 0)
                ObjectManager.Pet.Cast(ConsumeShadows);

            if (InCombat || (HealthOk && ManaOk))
            {
                if (!ObjectManager.Player.IsCasting && ObjectManager.Player.ChannelingId == 0)
                    ObjectManager.DoEmote(Emote.EMOTE_STATE_STAND);

                if (InCombat || PetHealthOk)
                {
                    ObjectManager.Pet?.FollowPlayer();
                    BotTasks.Pop();

                    BotTasks.Push(new SummonPetTask(BotContext));
                }
                else
                {
                    if (ObjectManager.Player.ChannelingId == 0 && !ObjectManager.Player.IsCasting && ObjectManager.IsSpellReady(HealthFunnel) && ObjectManager.Player.HealthPercent > 30)
                        ObjectManager.CastSpell(HealthFunnel);
                }

                return;
            }
            else
                ObjectManager.StopAllMovement();

            TryCastSpell(LifeTap, 0, int.MaxValue, ObjectManager.Player.HealthPercent > 85 && ObjectManager.Player.ManaPercent < 80);

            // Eat food if available and not already eating
            if (!ObjectManager.Player.IsEating && ObjectManager.Player.HealthPercent < 80 && Wait.For("EatFoodDelay", 3000, true))
            {
                var foodItem = ConsumableData.FindBestFood(ObjectManager);
                foodItem?.Use();
            }

            // Use best available drink from inventory
            IWoWItem? drinkItem = ConsumableData.FindBestDrink(ObjectManager);

            if (drinkItem != null && !ObjectManager.Player.IsDrinking && ObjectManager.Player.ManaPercent < 60 && Wait.For("DrinkDelay", 500, true))
                drinkItem.Use();
        }

        private bool HealthOk => ObjectManager.Player.HealthPercent >= 90 || (ObjectManager.Player.HealthPercent >= 70 && !ObjectManager.Player.IsEating);

        private bool PetHealthOk => ObjectManager.Pet == null || ObjectManager.Pet.HealthPercent >= 80;

        private bool ManaOk => (ObjectManager.Player.Level < 6 && ObjectManager.Player.ManaPercent > 50) || ObjectManager.Player.ManaPercent >= 90 || (ObjectManager.Player.ManaPercent >= 55 && !ObjectManager.Player.IsDrinking);

        private bool InCombat => ObjectManager.Player.IsInCombat || ObjectManager.Units.Any(u => u.TargetGuid == ObjectManager.Player.Guid || u.TargetGuid == ObjectManager.Pet?.Guid);

        public void TryCastSpell(string name, int minRange, int maxRange, bool condition = true, Action callback = null, bool castOnSelf = false) =>
            TryCastSpellInternal(name, minRange, maxRange, condition, callback, castOnSelf);

        public void TryCastSpell(string name, bool condition = true, Action callback = null, bool castOnSelf = false) =>
            TryCastSpellInternal(name, 0, int.MaxValue, condition, callback, castOnSelf);

        private void TryCastSpellInternal(string name, int minRange, int maxRange, bool condition = true, Action callback = null, bool castOnSelf = false)
        {
            if (ObjectManager.IsSpellReady(name) && condition && !ObjectManager.Player.IsStunned && ((!ObjectManager.Player.IsCasting && ObjectManager.Player.ChannelingId == 0) || ObjectManager.Player.Class == Class.Warrior))
            {
                ObjectManager.CastSpell(name, castOnSelf: castOnSelf);
                callback?.Invoke();
            }
        }
    }
}
