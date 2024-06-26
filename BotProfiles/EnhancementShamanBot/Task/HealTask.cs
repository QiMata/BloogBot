﻿using RaidMemberBot.AI;
using RaidMemberBot.Game.Statics;
using RaidMemberBot.Mem;
using System.Collections.Generic;

namespace EnhancementShamanBot
{
    class HealTask : BotTask, IBotTask
    {
        const string WarStomp = "War Stomp";
        const string HealingWave = "Healing Wave";
        public HealTask(IClassContainer container, Stack<IBotTask> botTasks) : base(container, botTasks, TaskType.Heal) { }

        public void Update()
        {
            if (ObjectManager.Player.IsCasting) return;

            if (ObjectManager.Player.HealthPercent > 70 || ObjectManager.Player.Mana < ObjectManager.Player.GetManaCost(HealingWave))
            {
                Container.State.Action = "Done Healing";
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.Player.IsMoving)
                ObjectManager.Player.StopAllMovement();

            if (ObjectManager.Player.IsSpellReady(WarStomp))
                Functions.LuaCall($"CastSpellByName('{WarStomp}')");

            Container.State.Action = "Healing";
            Functions.LuaCall($"CastSpellByName('{HealingWave}',1)");
        }
    }
}
