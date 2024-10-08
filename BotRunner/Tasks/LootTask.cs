﻿using BotRunner.Constants;
using BotRunner.Interfaces;
using PathfindingService.Models;

namespace BotRunner.Tasks
{
    public class LootTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        private readonly int startTime = Environment.TickCount;
        private readonly int stuckCount;
        private readonly ILootFrame lootFrame;
        private int lootIndex;
        private LootStates currentState;

        public void Update()
        {
            if (ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) >= 5)
            {
                Position[] nextWaypoint = Container.PathfindingClient.GetPath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.GetTarget(ObjectManager.Player).Position, true);
                ObjectManager.Player.MoveToward(nextWaypoint[0]);
            }

            if (ObjectManager.GetTarget(ObjectManager.Player).CanBeLooted && currentState == LootStates.Initial && ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) < 5)
            {
                ObjectManager.Player.StopAllMovement();

                if (Wait.For("StartLootDelay", 200))
                {
                    ObjectManager.GetTarget(ObjectManager.Player).Interact();
                    currentState = LootStates.RightClicked;
                    return;
                }
            }

            // State Transition Conditions:
            //  - target can't be looted (no items to loot)
            //  - loot frame is open, but we've already looted everything we want
            //  - stuck count is greater than 5 (perhaps the corpse is in an awkward position the character can't reach)
            //  - we've been in the loot state for over 10 seconds (again, perhaps the corpse is unreachable. most common example of this is when a mob dies on a cliff that we can't climb)
            if (currentState == LootStates.Initial && !ObjectManager.GetTarget(ObjectManager.Player).CanBeLooted || lootFrame != null && lootIndex == lootFrame.LootItems.Count() || stuckCount > 5 || Environment.TickCount - startTime > 10000)
            {
                ObjectManager.Player.StopAllMovement();
                BotTasks.Pop();
                BotTasks.Push(new EquipBagsTask(BotContext));
                if (ObjectManager.Player.IsSwimming)
                {

                }
                return;
            }

            if (currentState == LootStates.RightClicked && Wait.For("LootFrameDelay", 1000))
            {

                currentState = LootStates.LootFrameReady;
            }

            if (currentState == LootStates.LootFrameReady && Wait.For("LootDelay", 150))
            {
                IWoWItem itemToLoot = lootFrame.LootItems.ElementAt(lootIndex);
                ItemQuality itemQuality = itemToLoot.Info.Quality;

                bool poorQualityCondition = itemToLoot.IsCoins || itemQuality == ItemQuality.Poor;
                bool commonQualityCondition = itemToLoot.IsCoins || itemQuality == ItemQuality.Common;
                bool uncommonQualityCondition = itemToLoot.IsCoins || itemQuality == ItemQuality.Uncommon;
                bool other = itemQuality != ItemQuality.Poor && itemQuality != ItemQuality.Common && itemQuality != ItemQuality.Uncommon;

                //if (itemQuality == ItemQuality.Rare || itemQuality == ItemQuality.Epic)
                //    DiscordClientWrapper.SendItemNotification(ObjectManager.Player.Name, itemQuality, itemToLoot.ItemId);

                if (itemToLoot.IsCoins || poorQualityCondition || commonQualityCondition || uncommonQualityCondition || other)
                {
                    itemToLoot.Loot();
                }

                lootIndex++;
            }
        }
    }

    internal enum LootStates
    {
        Initial,
        RightClicked,
        LootFrameReady
    }
}
