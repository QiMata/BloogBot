﻿using RaidMemberBot.Client;
using RaidMemberBot.Game;
using RaidMemberBot.Game.Statics;
using RaidMemberBot.Objects;
using System.Collections.Generic;

namespace RaidMemberBot.AI.SharedStates
{
    public class UseItemOnUnitTask : BotTask, IBotTask
    {
        readonly WoWItem usableItem;

        bool itemUsed;

        public UseItemOnUnitTask(IClassContainer container, Stack<IBotTask> botTasks, WoWItem wowItem) : base(container, botTasks, TaskType.Ordinary)
        {
            usableItem = wowItem;
            itemUsed = false;
        }

        public void Update()
        {
            if (ObjectManager.Player.Position.DistanceTo(ObjectManager.Player.Target.Position) < 3)
            {
                if (!itemUsed)
                {
                    ObjectManager.Player.SetTarget(ObjectManager.Player.TargetGuid);
                    ObjectManager.Player.StopMovement(Constants.Enums.ControlBits.Nothing);
                    usableItem.Use();

                    itemUsed = true;
                }
                else if (Wait.For("ItemUseAnim", 1000))
                {
                    BotTasks.Pop();
                    return;
                }
            }
            else
            {
                Position[] nextWaypoint = NavigationClient.Instance.CalculatePath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.Player.Target.Position, true);
                ObjectManager.Player.MoveToward(nextWaypoint[0]);
            }

        }
    }
}
