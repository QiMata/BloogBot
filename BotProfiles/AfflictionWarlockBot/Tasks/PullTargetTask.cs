﻿using RaidMemberBot.AI;
using RaidMemberBot.Client;
using RaidMemberBot.Game;
using RaidMemberBot.Game.Statics;
using RaidMemberBot.Mem;
using RaidMemberBot.Objects;
using System.Collections.Generic;
using static RaidMemberBot.Constants.Enums;

namespace AfflictionWarlockBot
{
    class PullTargetTask : BotTask, IBotTask
    {
        const string SummonImp = "Summon Imp";
        const string SummonVoidwalker = "Summon Voidwalker";
        const string CurseOfAgony = "Curse of Agony";
        const string ShadowBolt = "Shadow Bolt";

        readonly string pullingSpell;
        Position currentWaypoint;

        internal PullTargetTask(IClassContainer container, Stack<IBotTask> botTasks) : base(container, botTasks, TaskType.Pull)
        {
            if (ObjectManager.Player.IsSpellReady(CurseOfAgony))
                pullingSpell = CurseOfAgony;
            else
                pullingSpell = ShadowBolt;
        }

        public void Update()
        {

            if (ObjectManager.Pet == null && (ObjectManager.Player.IsSpellReady(SummonImp) || ObjectManager.Player.IsSpellReady(SummonVoidwalker)))
            {
                ObjectManager.Player.StopAllMovement();
                BotTasks.Push(new SummonPetTask(Container, BotTasks));
                return;
            }

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(ObjectManager.Player.Target.Position);
            if (distanceToTarget < 27 && ObjectManager.Player.IsCasting && ObjectManager.Player.IsSpellReady(pullingSpell))
            {
                if (ObjectManager.Player.MovementFlags != MovementFlags.MOVEFLAG_NONE)
                    ObjectManager.Player.StopAllMovement();

                if (Wait.For("AfflictionWarlockPullDelay", 250))
                {
                    ObjectManager.Player.StopAllMovement();
                    Functions.LuaCall($"CastSpellByName('{pullingSpell}')");
                    BotTasks.Pop();
                    BotTasks.Push(new PvERotationTask(Container, BotTasks));
                }

                return;
            }

            Position[] nextWaypoint = NavigationClient.Instance.CalculatePath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.Player.Target.Position, true);
            if (nextWaypoint.Length > 1)
            {
                currentWaypoint = nextWaypoint[1];
            }
            else
            {
                BotTasks.Pop();
                return;
            }

            ObjectManager.Player.MoveToward(currentWaypoint);
        }
    }
}
