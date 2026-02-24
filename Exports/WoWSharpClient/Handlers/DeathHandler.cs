using GameData.Core.Enums;
using Serilog;
using System;
using System.IO;
using WoWSharpClient.Models;

namespace WoWSharpClient.Handlers
{
    public static class DeathHandler
    {
        public static void HandleCorpseReclaimDelay(Opcode opcode, byte[] data)
        {
            if (data.Length < 4)
            {
                Log.Warning("[DeathHandler] {Opcode} payload too short ({Length} bytes)", opcode, data.Length);
                return;
            }

            try
            {
                using var reader = new BinaryReader(new MemoryStream(data));
                uint rawDelay = reader.ReadUInt32();
                int normalizedSeconds = NormalizeDelayToSeconds(rawDelay);

                if (WoWSharpObjectManager.Instance.Player is WoWLocalPlayer player)
                    player.CorpseRecoveryDelaySeconds = normalizedSeconds;

                Log.Information("[DeathHandler] {Opcode} rawDelay={RawDelay} normalizedDelay={Delay}s",
                    opcode, rawDelay, normalizedSeconds);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[DeathHandler] Failed to parse {Opcode}", opcode);
            }
        }

        private static int NormalizeDelayToSeconds(uint rawDelay)
        {
            if (rawDelay == 0)
                return 0;

            // Different server builds represent this as milliseconds or seconds.
            // Values above one hour are treated as milliseconds.
            if (rawDelay > 3600)
                return (int)Math.Ceiling(rawDelay / 1000.0);

            return (int)rawDelay;
        }
    }
}
