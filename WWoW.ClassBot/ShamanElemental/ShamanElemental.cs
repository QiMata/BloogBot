﻿using System.ComponentModel.Composition;
using BotRunner;
using BotRunner.Interfaces;
using Communication;
using ShamanElemental.Tasks;

using BotProfiles.Common;
namespace ShamanElemental
{
    [Export(typeof(IBot))]
    internal class ShamanElemental : BotBase
    {
        public override string Name => "Elemental Shaman";

        public override string FileName => "ShamanElemental.dll";


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
