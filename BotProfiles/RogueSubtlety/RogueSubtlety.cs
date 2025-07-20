﻿using System.ComponentModel.Composition;
using BotRunner;
using BotRunner.Interfaces;
using Communication;
using RogueSubtlety.Tasks;

using BotProfiles.Common;
namespace RogueSubtlety
{
    [Export(typeof(IBot))]
    internal class RogueSubtlety : BotBase
    {
        public override string Name => "Subtlety Rogue";

        public override string FileName => "RogueSubtlety.dll";


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
