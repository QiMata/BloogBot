﻿// Friday owns this file!

using RaidMemberBot.AI;
using RaidMemberBot.Client;
using RaidMemberBot.Game.Statics;
using RaidMemberBot.Mem;
using RaidMemberBot.Objects;
using System.Collections.Generic;
using System.Linq;

namespace BeastMasterHunterBot
{
    class PullTargetTask : BotTask, IBotTask
    {
        const string GunLuaScript = "if IsAutoRepeatAction(11) == nil then CastSpellByName('Auto Shot') end";
        const string SerpentSting = "Serpent Sting";
        const string AspectOfTheMonkey = "Aspect Of The Monkey";
        const string AspectOfTheCheetah = "Aspect Of The Cheetah";

        internal PullTargetTask(IClassContainer container, Stack<IBotTask> botTasks) : base(container, botTasks, TaskType.Pull) { }

        public void Update()
        {
            if (ObjectManager.Hostiles.Count() > 0)
            {
                WoWUnit potentialNewTarget = ObjectManager.Hostiles.First();

                if (potentialNewTarget != null && potentialNewTarget.Guid != ObjectManager.Player.TargetGuid)
                {
                    ObjectManager.Player.SetTarget(potentialNewTarget.TargetGuid);
                }
            }

            if (ObjectManager.Player.Position.DistanceTo(ObjectManager.Player.Target.Position) < 28)
            {
                ObjectManager.Player.StopAllMovement();
                Functions.LuaCall(GunLuaScript);
                BotTasks.Pop();
                BotTasks.Push(Container.CreatePvERotationTask(Container, BotTasks));
                return;
            } else
            {
                Position[] nextWaypoint = NavigationClient.Instance.CalculatePath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.Player.Target.Position, true);
                ObjectManager.Player.MoveToward(nextWaypoint[1]);
            }
        }
    }
}
