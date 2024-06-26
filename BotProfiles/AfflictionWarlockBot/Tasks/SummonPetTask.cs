﻿using RaidMemberBot.AI;
using RaidMemberBot.Game.Statics;
using RaidMemberBot.Mem;
using System.Collections.Generic;

namespace AfflictionWarlockBot
{
    class SummonPetTask : BotTask, IBotTask
    {
        const string SummonImp = "Summon Imp";
        const string SummonVoidwalker = "Summon Voidwalker";

        public SummonPetTask(IClassContainer container, Stack<IBotTask> botTasks) : base(container, botTasks, TaskType.Buff) { }

        public void Update()
        {
            if (ObjectManager.Player.IsCasting)
                return;

            ObjectManager.Player.Stand();

            if ((!ObjectManager.Player.IsSpellReady(SummonImp) && !ObjectManager.Player.IsSpellReady(SummonVoidwalker)) || ObjectManager.Pet != null)
            {
                BotTasks.Pop();
                BotTasks.Push(new BuffTask(Container, BotTasks));
                return;
            }

            if (ObjectManager.Player.IsSpellReady(SummonImp))
                Functions.LuaCall($"CastSpellByName('{SummonImp}')");
            else
                Functions.LuaCall($"CastSpellByName('{SummonVoidwalker}')");
        }
    }
}
