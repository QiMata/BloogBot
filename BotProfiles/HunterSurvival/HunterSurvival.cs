﻿using System.ComponentModel.Composition;
using BotRunner;
using BotRunner.Interfaces;
using Communication;
using HunterSurvival.Tasks;

using BotProfiles.Common;
namespace HunterSurvival
{
    [Export(typeof(IBot))]
    internal class HunterSurvival : BotBase
    {
        public override string Name => "Survival Hunter";

        public override string FileName => "HunterSurvival.dll";


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
