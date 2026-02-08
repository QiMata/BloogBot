using GameData.Core.Enums;
using WoWSharpClient.Utils;
using Serilog;

namespace WoWSharpClient.Handlers
{
    public static class SpellHandler
    {
        public static void HandleInitialSpells(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                byte talentSpec = reader.ReadByte();
                ushort spellCount = reader.ReadUInt16();

                for (int i = 0; i < spellCount; i++)
                {
                    ushort spellID = reader.ReadUInt16();
                    short unknown = reader.ReadInt16(); // Possible additional data
                }

                ushort cooldownCount = reader.ReadUInt16();

                for (int i = 0; i < cooldownCount; i++)
                {
                    ushort cooldownSpellID = reader.ReadUInt16();
                    ushort cooldownItemID = reader.ReadUInt16();
                    ushort cooldownSpellCategory = reader.ReadUInt16();
                    int cooldownTime = reader.ReadInt32();
                    uint cooldownCategoryTime = reader.ReadUInt32();
                }

                WoWSharpEventEmitter.Instance.FireOnInitialSpellsLoaded();
            }
            catch (EndOfStreamException)
            {
                Log.Warning("[SpellHandler] Truncated SMSG_INITIAL_SPELLS packet");
            }
        }

        public static void HandleSpellLogMiss(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                uint spellId = reader.ReadUInt32();
                ulong casterGUID = reader.ReadUInt64();
                ulong targetGUID = reader.ReadUInt64();
                uint missType = reader.ReadUInt32();

                WoWSharpEventEmitter.Instance.FireOnSpellLogMiss(spellId, casterGUID, targetGUID, missType);
            }
            catch (EndOfStreamException)
            {
                Log.Warning("[SpellHandler] Truncated SMSG_SPELLLOGMISS packet");
            }
        }

        /// <summary>
        /// Parses SMSG_SPELL_GO (0x132).
        /// Format: PackGUID casterItem/caster, PackGUID caster, uint32 spellId, uint16 castFlags,
        ///         uint8 hitCount, PackGUID[] hitTargets, uint8 missCount, [miss entries...],
        ///         SpellCastTargets, [ammo info if CAST_FLAG_AMMO]
        /// </summary>
        public static void HandleSpellGo(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong casterItemOrCaster = ReaderUtils.ReadPackedGuid(reader);
                ulong casterGuid = ReaderUtils.ReadPackedGuid(reader);
                uint spellId = reader.ReadUInt32();
                ushort castFlags = reader.ReadUInt16();

                // Hit targets
                byte hitCount = reader.ReadByte();
                ulong firstHitTarget = 0;
                for (int i = 0; i < hitCount; i++)
                {
                    ulong hitGuid = ReaderUtils.ReadPackedGuid(reader);
                    if (i == 0) firstHitTarget = hitGuid;
                }

                // Miss targets
                byte missCount = reader.ReadByte();
                for (int i = 0; i < missCount; i++)
                {
                    ulong missGuid = ReaderUtils.ReadPackedGuid(reader);
                    byte missCondition = reader.ReadByte();
                    if (missCondition == 11) // SPELL_MISS_REFLECT
                        reader.ReadByte(); // reflect result
                }

                // SpellCastTargets (variable structure) - skip for event purposes
                // We've extracted the key data we need

                WoWSharpEventEmitter.Instance.FireOnSpellGo(spellId, casterGuid, firstHitTarget);
            }
            catch (EndOfStreamException)
            {
                // Packet may be truncated but we extracted what we could
            }
            catch (Exception ex)
            {
                Log.Warning($"[SpellHandler] Error parsing SMSG_SPELL_GO: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses SMSG_SPELL_START (0x131).
        /// Format: PackGUID casterItem/caster, PackGUID caster, uint32 spellId, uint16 castFlags,
        ///         uint32 castTime, SpellCastTargets, [ammo info if CAST_FLAG_AMMO]
        /// </summary>
        public static void HandleSpellStart(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong casterItemOrCaster = ReaderUtils.ReadPackedGuid(reader);
                ulong casterGuid = ReaderUtils.ReadPackedGuid(reader);
                uint spellId = reader.ReadUInt32();
                ushort castFlags = reader.ReadUInt16();
                uint castTime = reader.ReadUInt32();

                // SpellCastTargets follows but we extract key info for the event
                ulong targetGuid = 0;
                if (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    ushort targetMask = reader.ReadUInt16();
                    // TARGET_FLAG_UNIT = 0x0002
                    if ((targetMask & 0x0002) != 0)
                        targetGuid = ReaderUtils.ReadPackedGuid(reader);
                }

                WoWSharpEventEmitter.Instance.FireOnSpellStart(spellId, casterGuid, targetGuid, castTime);
            }
            catch (EndOfStreamException)
            {
                // Packet may be truncated
            }
            catch (Exception ex)
            {
                Log.Warning($"[SpellHandler] Error parsing SMSG_SPELL_START: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses SMSG_ATTACKERSTATEUPDATE (0x14A).
        /// Format: uint32 hitInfo, PackGUID attacker, PackGUID target, uint32 totalDamage,
        ///         uint8 subDamageCount, [subDamage entries...], uint32 targetState,
        ///         uint32 reserved, uint32 spellId, uint32 blockedAmount
        /// </summary>
        public static void HandleAttackerStateUpdate(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                uint hitInfo = reader.ReadUInt32();
                ulong attackerGuid = ReaderUtils.ReadPackedGuid(reader);
                ulong targetGuid = ReaderUtils.ReadPackedGuid(reader);
                uint totalDamage = reader.ReadUInt32();

                byte subDamageCount = reader.ReadByte();
                for (int i = 0; i < subDamageCount; i++)
                {
                    uint damageSchool = reader.ReadUInt32();
                    float damageFloat = reader.ReadSingle();
                    uint damageInt = reader.ReadUInt32();
                    uint absorb = reader.ReadUInt32();
                    int resist = reader.ReadInt32();
                }

                uint targetState = reader.ReadUInt32();
                reader.ReadUInt32(); // reserved, always 0
                uint spellId = reader.ReadUInt32();
                uint blockedAmount = reader.ReadUInt32();

                WoWSharpEventEmitter.Instance.FireOnAttackerStateUpdate(
                    hitInfo, attackerGuid, targetGuid, totalDamage, spellId, blockedAmount);
            }
            catch (EndOfStreamException)
            {
                // Packet may be truncated
            }
            catch (Exception ex)
            {
                Log.Warning($"[SpellHandler] Error parsing SMSG_ATTACKERSTATEUPDATE: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses SMSG_DESTROY_OBJECT (0xAA).
        /// Format: uint64 guid (full 8-byte, NOT packed)
        /// </summary>
        public static void HandleDestroyObject(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong guid = reader.ReadUInt64();

                WoWSharpObjectManager.Instance.QueueUpdate(
                    new WoWSharpObjectManager.ObjectStateUpdate(
                        guid,
                        WoWSharpObjectManager.ObjectUpdateOperation.Remove,
                        WoWObjectType.None,
                        null,
                        []
                    )
                );
            }
            catch (EndOfStreamException)
            {
                Log.Warning("[SpellHandler] Truncated SMSG_DESTROY_OBJECT packet");
            }
        }
    }
}
