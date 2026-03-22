using GameData.Core.Enums;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

namespace WoWSharpClient.Handlers
{
    public static class PetHandler
    {
        private const int MAX_ACTION_BAR_INDEX = 10;

        /// <summary>
        /// Handles SMSG_PET_SPELLS — sent when a pet is summoned or charm applied.
        /// Packet format (MaNGOS 1.12.1):
        ///   petGuid (8) | unknown (4) | reactState (1) | commandState (1) |
        ///   unknown (1) | enabledFlag (1) | actionBar (10 × uint32) |
        ///   spellCount (1) | spells (N × uint32) | cooldowns (variable)
        /// </summary>
        public static void HandlePetSpells(Opcode opcode, byte[] data)
        {
            if (data.Length < 16) // Minimum: guid(8) + header(8)
            {
                // Empty packet = pet dismissed (server sends guid=0 on dismiss)
                var om = WoWSharpObjectManager.Instance;
                om?.ClearPetSpells();
                Log.Information("[PetHandler] Pet dismissed (empty/short SMSG_PET_SPELLS)");
                return;
            }

            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong petGuid = reader.ReadUInt64();

                if (petGuid == 0)
                {
                    // Pet dismissed
                    WoWSharpObjectManager.Instance?.ClearPetSpells();
                    Log.Information("[PetHandler] Pet dismissed (petGuid=0)");
                    return;
                }

                uint unknown = reader.ReadUInt32(); // Always 0
                byte reactState = reader.ReadByte();
                byte commandState = reader.ReadByte();
                byte unknown2 = reader.ReadByte();
                byte enabledFlag = reader.ReadByte();

                // Read 10 action bar entries (packed uint32: lower 24 bits = ID, upper 8 bits = type)
                var actionBar = new List<(uint SpellId, byte ActionType)>(MAX_ACTION_BAR_INDEX);
                for (int i = 0; i < MAX_ACTION_BAR_INDEX; i++)
                {
                    uint packed = reader.ReadUInt32();
                    uint spellId = packed & 0x00FFFFFF;
                    byte actionType = (byte)(packed >> 24);
                    if (spellId != 0)
                        actionBar.Add((spellId, actionType));
                }

                // Read additional spell list
                var petSpells = new List<uint>();
                if (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    byte spellCount = reader.ReadByte();
                    for (int i = 0; i < spellCount; i++)
                    {
                        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) break;
                        uint packed = reader.ReadUInt32();
                        uint spellId = packed & 0x00FFFFFF;
                        if (spellId != 0)
                            petSpells.Add(spellId);
                    }
                }

                // Store on ObjectManager
                var om = WoWSharpObjectManager.Instance;
                om?.SetPetSpells(petGuid, actionBar, petSpells);

                Log.Information("[PetHandler] Pet 0x{Guid:X}: {ActionBarCount} action bar entries, {SpellCount} spells, react={React}, cmd={Cmd}",
                    petGuid, actionBar.Count, petSpells.Count, reactState, commandState);
            }
            catch (EndOfStreamException)
            {
                Log.Warning("[PetHandler] Truncated SMSG_PET_SPELLS packet ({Len} bytes)", data.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PetHandler] Failed to parse SMSG_PET_SPELLS");
            }
        }
    }
}
