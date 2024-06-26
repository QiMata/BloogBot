﻿using RaidMemberBot.Game.Frames;
using RaidMemberBot.Game.Statics;
using RaidMemberBot.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaidMemberBot.AI.SharedStates
{
    public class PickUpQuestFromNpcTask : BotTask, IBotTask
    {
        readonly string npcName;
        readonly LocalPlayer player;
        readonly int startTime = Environment.TickCount;
        readonly int currentQuestLogSize;

        WoWUnit npc;
        DialogFrame dialogFrame;

        public PickUpQuestFromNpcTask(IClassContainer container, Stack<IBotTask> botTasks, string npcName) : base(container, botTasks, TaskType.Ordinary)
        {
            this.npcName = npcName;
            player = ObjectManager.Player;

            npc = ObjectManager
                .Units
                .First(x => x.Name == npcName);
        }

        public void Update()
        {
            if (ObjectManager.Player.IsInCombat || (Environment.TickCount - startTime > 5000))
            {
                BotTasks.Pop();
                return;
            }
        }
    }
}
