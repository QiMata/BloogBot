﻿using Serilog;

namespace ForegroundBotRunner.Mem
{
    internal static class HackManager
    {
        static internal IList<Hack> Hacks { get; } = [];

        static internal void AddHack(Hack hack)
        {
            Log.Information($"[HACK MANAGER] Adding hack {hack.Name}");
            Hacks.Add(hack);
            EnableHack(hack);
        }

        static internal void EnableHack(Hack hack) => MemoryManager.WriteBytes(hack.Address, hack.NewBytes);

        static internal void DisableHack(Hack hack) => MemoryManager.WriteBytes(hack.Address, hack.OriginalBytes);
    }
}
