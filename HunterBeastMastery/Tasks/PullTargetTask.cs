﻿using BotRunner.Interfaces;
using BotRunner.Tasks;
using PathfindingService.Models;

namespace HunterBeastMastery.Tasks
{
    internal class PullTargetTask : BotTask, IBotTask
    {

        internal PullTargetTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (ObjectManager.Hostiles.Any())
            {
                IWoWUnit potentialNewTarget = ObjectManager.Hostiles.First();

                if (potentialNewTarget != null && potentialNewTarget.Guid != ObjectManager.Player.TargetGuid)
                {
                    ObjectManager.Player.SetTarget(potentialNewTarget.TargetGuid);
                }
            }

            if (ObjectManager.Player.Position.DistanceTo(ObjectManager.Player.Target.Position) < 28)
            {
                ObjectManager.Player.StopAllMovement();
                ObjectManager.Player.StartRangedAttack();

                BotTasks.Pop();
                BotTasks.Push(Container.CreatePvERotationTask(BotContext));
                return;
            } else
            {
                Position[] nextWaypoint = Container.PathfindingClient.GetPath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.Player.Target.Position, true);
                ObjectManager.Player.MoveToward(nextWaypoint[1]);
            }
        }
    }
}
