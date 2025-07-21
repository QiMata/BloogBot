﻿using System.ComponentModel.Composition;
using BotRunner;
using BotRunner.Interfaces;
using Communication;
using PriestShadow.Tasks;

using BotProfiles.Common;
namespace PriestShadow
{
    [Export(typeof(IBot))]
    internal class PriestShadow : BotBase
    {
        public override string Name => "Shadow Priest";

        public override string FileName => "PriestShadow.dll";


        public override IBotTask CreateRestTask(IBotContext botContext) =>
            new RestTask(botContext);

        public override IBotTask CreateMoveToTargetTask(IBotContext botContext) =>
            new PullTargetTask(botContext);

        public override IBotTask CreateBuffTask(IBotContext botContext) =>
            new BuffTask(botContext);

        public override IBotTask CreatePvERotationTask(IBotContext botContext) =>
            new PvERotationTask(botContext);

        public override IBotTask CreatePvPRotationTask(IBotContext botContext) =>
            new PvERotationTask(botContext);
    }
}
