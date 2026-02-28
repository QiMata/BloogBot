using GameData.Core.Enums;
using WoWSharpClient.Utils;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;

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

                var spells = new List<GameData.Core.Models.Spell>(spellCount);
                for (int i = 0; i < spellCount; i++)
                {
                    ushort spellID = reader.ReadUInt16();
                    short unknown = reader.ReadInt16(); // Possible additional data
                    spells.Add(new GameData.Core.Models.Spell(spellID, 0, "", "", ""));
                }

                WoWSharpObjectManager.Instance.Spells = spells;
                Log.Information($"[SpellHandler] Loaded {spellCount} spells from SMSG_INITIAL_SPELLS");

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

        /// <summary>
        /// Parses SMSG_LEARNED_SPELL (0x12B).
        /// Format: uint32 spellId
        /// Adds the newly learned spell to the local spell list.
        /// </summary>
        public static void HandleLearnedSpell(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                uint spellId = reader.ReadUInt32();
                var existing = WoWSharpObjectManager.Instance.Spells;
                if (existing != null && !existing.Exists(s => s.Id == spellId))
                {
                    existing.Add(new GameData.Core.Models.Spell(spellId, 0, "", "", ""));
                    Log.Information("[SpellHandler] Learned new spell: {SpellId} (total: {Count})", spellId, existing.Count);
                }
            }
            catch (EndOfStreamException) { }
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
                var hitGuids = new List<ulong>(hitCount);
                for (int i = 0; i < hitCount; i++)
                {
                    ulong hitGuid = ReaderUtils.ReadPackedGuid(reader);
                    hitGuids.Add(hitGuid);
                    if (i == 0) firstHitTarget = hitGuid;
                }

                // Miss targets
                byte missCount = reader.ReadByte();
                var missGuids = new List<(ulong guid, byte condition)>(missCount);
                for (int i = 0; i < missCount; i++)
                {
                    ulong missGuid = ReaderUtils.ReadPackedGuid(reader);
                    byte missCondition = reader.ReadByte();
                    missGuids.Add((missGuid, missCondition));
                    if (missCondition == 11) // SPELL_MISS_REFLECT
                        reader.ReadByte(); // reflect result
                }

                // SpellCastTargets (variable structure) - skip for event purposes
                // We've extracted the key data we need

                // Clear SpellcastId when cast completes so IsCasting becomes false
                if (WoWSharpObjectManager.Instance.GetObjectByGuid(casterGuid) is Models.WoWUnit casterUnit)
                    casterUnit.SpellcastId = 0;

                // Detailed logging for gathering spell diagnostics
                var hitStr = string.Join(", ", hitGuids.Select(g => $"0x{g:X}"));
                var missStr = string.Join(", ", missGuids.Select(m => $"0x{m.guid:X}(cond={m.condition})"));
                Log.Information("[SpellHandler] SPELL_GO: caster=0x{Caster:X} spell={SpellId} hits=[{Hits}] misses=[{Misses}] castFlags=0x{CastFlags:X}",
                    casterGuid, spellId, hitStr, missStr, castFlags);

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

                Log.Information("[SpellHandler] SPELL_START: caster=0x{Caster:X} spell={SpellId} castTime={CastTime}ms target=0x{Target:X}",
                    casterGuid, spellId, castTime, targetGuid);

                // Update SpellcastId on the caster's WoWUnit model so IsCasting works
                if (WoWSharpObjectManager.Instance.GetObjectByGuid(casterGuid) is Models.WoWUnit casterUnit)
                    casterUnit.SpellcastId = spellId;

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

        /// <summary>
        /// Parses SMSG_CAST_FAILED (0x130) for vanilla 1.12.1.
        /// Format: uint32 spellId, uint8 status(always 2), uint8 failReason, [optional extra data]
        /// The status byte is SimpleSpellCastResult (0=detailed, 2=simple failure).
        /// </summary>
        public static void HandleCastFailed(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                uint spellId = reader.ReadUInt32();
                byte status = reader.ReadByte(); // SimpleSpellCastResult: 0=detailed, 2=simple failure

                // SMSG_CAST_FAILED is always about the local player — clear their SpellcastId
                if (WoWSharpObjectManager.Instance.Player is Models.WoWUnit playerUnit)
                    playerUnit.SpellcastId = 0;

                if (status == 2 && reader.BaseStream.Position >= reader.BaseStream.Length)
                {
                    // Simple failure with no details
                    Console.WriteLine($"[CAST_FAILED] spell={spellId} status=FAILURE (no details) raw={BitConverter.ToString(data)}");
                    Log.Warning("[SpellHandler] CAST_FAILED: spell={SpellId} status=FAILURE (no details)", spellId);
                    return;
                }

                byte reason = reader.ReadByte();
                var reasonName = GetCastFailureReasonName(reason);
                Console.WriteLine($"[CAST_FAILED] spell={spellId} reason=0x{reason:X2} ({reasonName}) raw={BitConverter.ToString(data)}");
                Log.Warning("[SpellHandler] CAST_FAILED: spell={SpellId} reason=0x{Reason:X2} ({ReasonName})", spellId, reason, reasonName);
            }
            catch (EndOfStreamException) { }
        }

        /// <summary>
        /// Maps vanilla 1.12.1 CastFailureReason wire values to human-readable names.
        /// </summary>
        private static string GetCastFailureReasonName(byte reason) => reason switch
        {
            0x00 => "AFFECTING_COMBAT",
            0x01 => "ALREADY_AT_FULL_HEALTH",
            0x02 => "ALREADY_AT_FULL_POWER",
            0x03 => "ALREADY_BEING_TAMED",
            0x04 => "ALREADY_HAVE_CHARM",
            0x05 => "ALREADY_HAVE_SUMMON",
            0x06 => "ALREADY_OPEN",
            0x07 => "AURA_BOUNCED",
            0x08 => "AUTOTRACK_INTERRUPTED",
            0x09 => "BAD_IMPLICIT_TARGETS",
            0x0A => "BAD_TARGETS",
            0x0B => "CANT_BE_CHARMED",
            0x0C => "CANT_BE_DISENCHANTED",
            0x0D => "CANT_BE_PROSPECTED",
            0x0E => "CANT_CAST_ON_TAPPED",
            0x12 => "CASTER_AURASTATE",
            0x13 => "CASTER_DEAD",
            0x14 => "CHARMED",
            0x16 => "CONFUSED",
            0x17 => "DONT_REPORT",
            0x18 => "EQUIPPED_ITEM",
            0x19 => "EQUIPPED_ITEM_CLASS",
            0x1C => "ERROR",
            0x1D => "FIZZLE",
            0x1E => "FLEEING",
            0x1F => "FOOD_LOWLEVEL",
            0x22 => "IMMUNE",
            0x23 => "INTERRUPTED",
            0x24 => "INTERRUPTED_COMBAT",
            0x27 => "ITEM_NOT_FOUND",
            0x28 => "ITEM_NOT_READY",
            0x2A => "LINE_OF_SIGHT",
            0x2E => "MOVING",
            0x32 => "NOPATH",
            0x33 => "NOT_BEHIND",
            0x34 => "NOT_FISHABLE",
            0x35 => "NOT_HERE",
            0x36 => "NOT_INFRONT",
            0x37 => "NOT_IN_CONTROL",
            0x38 => "NOT_KNOWN",
            0x39 => "NOT_MOUNTED",
            0x3C => "NOT_READY",
            0x3E => "NOT_STANDING",
            0x43 => "NO_AMMO",
            0x44 => "NO_CHARGES_REMAIN",
            0x47 => "NO_DUELING",
            0x49 => "NO_FISH",
            0x4B => "NO_MOUNTS_ALLOWED",
            0x4C => "NO_PET",
            0x4D => "NO_POWER",
            0x50 => "ONLY_ABOVEWATER",
            0x51 => "ONLY_DAYTIME",
            0x52 => "ONLY_INDOORS",
            0x53 => "ONLY_MOUNTED",
            0x54 => "ONLY_NIGHTTIME",
            0x55 => "ONLY_OUTDOORS",
            0x56 => "ONLY_SHAPESHIFT",
            0x57 => "ONLY_STEALTHED",
            0x58 => "ONLY_UNDERWATER",
            0x59 => "OUT_OF_RANGE",
            0x5A => "PACIFIED",
            0x5D => "REQUIRES_AREA",
            0x5E => "REQUIRES_SPELL_FOCUS",
            0x5F => "ROOTED",
            0x60 => "SILENCED",
            0x61 => "SPELL_IN_PROGRESS",
            0x64 => "STUNNED",
            0x65 => "TARGETS_DEAD",
            0x76 => "TOO_CLOSE",
            0x7B => "UNIT_NOT_BEHIND",
            0x7C => "UNIT_NOT_INFRONT",
            _ => $"UNKNOWN_0x{reason:X2}"
        };

        /// <summary>
        /// Parses SMSG_SPELL_FAILURE (0x133).
        /// Format: PackGUID caster, uint32 spellId, byte reason
        /// Broadcast to nearby players when a spell fails after SPELL_START.
        /// </summary>
        public static void HandleSpellFailure(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong casterGuid = ReaderUtils.ReadPackedGuid(reader);
                uint spellId = reader.ReadUInt32();
                byte reason = reader.ReadByte();

                // Clear SpellcastId on the caster
                if (WoWSharpObjectManager.Instance.GetObjectByGuid(casterGuid) is Models.WoWUnit casterUnit)
                    casterUnit.SpellcastId = 0;

                Log.Warning("[SpellHandler] SPELL_FAILURE: caster=0x{Caster:X} spell={SpellId} reason={Reason}",
                    casterGuid, spellId, reason);
            }
            catch (EndOfStreamException) { }
        }

        /// <summary>
        /// Parses SMSG_SPELLHEALLOG (0x150).
        /// Format: PackGUID target, PackGUID caster, uint32 spellId, uint32 healAmount, byte critical
        /// Logs heal amounts for combat coordination feedback.
        /// </summary>
        public static void HandleSpellHealLog(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong targetGuid = ReaderUtils.ReadPackedGuid(reader);
                ulong casterGuid = ReaderUtils.ReadPackedGuid(reader);
                uint spellId = reader.ReadUInt32();
                uint healAmount = reader.ReadUInt32();
                byte critical = reader.ReadByte();

                Log.Information("[SpellHandler] HEAL_LOG: caster=0x{Caster:X} target=0x{Target:X} spell={SpellId} healed={Amount}{Crit}",
                    casterGuid, targetGuid, spellId, healAmount, critical != 0 ? " CRIT" : "");
            }
            catch (EndOfStreamException) { }
        }

        /// <summary>
        /// SMSG_LOG_XPGAIN (0x1D0) — fired when player gains XP from kills or quests.
        /// </summary>
        public static void HandleLogXpGain(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong victimGuid = reader.ReadUInt64(); // 0 for quest/explore XP
                uint xpAmount = reader.ReadUInt32();
                byte type = reader.ReadByte(); // 0 = kill, 1 = non-kill (quest)

                WoWSharpEventEmitter.Instance.FireOnXpGain((int)xpAmount);
            }
            catch (EndOfStreamException) { }
        }

        /// <summary>
        /// SMSG_LEVELUP_INFO (0x1D4) — fired when player levels up.
        /// </summary>
        public static void HandleLevelUpInfo(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                uint newLevel = reader.ReadUInt32();
                // Remaining fields: health delta, mana delta, stat deltas — not needed for event
                WoWSharpEventEmitter.Instance.FireLevelUp();
                Log.Information("[SpellHandler] LEVELUP: Player reached level {Level}", newLevel);
            }
            catch (EndOfStreamException) { }
        }
        /// <summary>
        /// SMSG_ATTACKSTART (0x143) — server confirms melee auto-attack has started.
        /// Format: uint64 attackerGuid, uint64 targetGuid
        /// </summary>
        public static void HandleAttackStart(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong attackerGuid = reader.ReadUInt64();
                ulong targetGuid = reader.ReadUInt64();

                var player = WoWSharpObjectManager.Instance.Player;
                if (player is Models.WoWLocalPlayer localPlayer && attackerGuid == localPlayer.Guid)
                    localPlayer.IsAutoAttacking = true;
            }
            catch (EndOfStreamException) { }
        }

        /// <summary>
        /// SMSG_ATTACKSTOP (0x144) — server confirms melee auto-attack has stopped.
        /// Format: PackGUID attacker, PackGUID target, uint32 unknown
        /// </summary>
        public static void HandleAttackStop(Opcode opcode, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong attackerGuid = ReaderUtils.ReadPackedGuid(reader);
                ulong targetGuid = ReaderUtils.ReadPackedGuid(reader);

                var player = WoWSharpObjectManager.Instance.Player;
                if (player is Models.WoWLocalPlayer localPlayer && attackerGuid == localPlayer.Guid)
                    localPlayer.IsAutoAttacking = false;
            }
            catch (EndOfStreamException) { }
        }

        /// <summary>
        /// Handles SMSG_GAMEOBJECT_CUSTOM_ANIM (0x0B3).
        /// Format: uint64 guid, uint32 anim
        /// For fishing bobbers, anim 0 signals a fish bite. We auto-interact (CMSG_GAMEOBJ_USE)
        /// so the catch happens instantly without requiring external polling.
        /// </summary>
        public static void HandleGameObjectCustomAnim(Opcode opcode, byte[] data)
        {
            if (data.Length < 12) return;

            using var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                ulong guid = reader.ReadUInt64();
                uint anim = reader.ReadUInt32();

                var om = WoWSharpObjectManager.Instance;
                var obj = om.GetObjectByGuid(guid);

                Log.Information("[CustomAnim] Guid=0x{Guid:X} Anim={Anim} ObjFound={Found} ObjType={Type}",
                    guid, anim, obj != null, obj?.GetType().Name ?? "null");

                if (obj is not Models.WoWGameObject go)
                {
                    // Dump tracked game objects for diagnosis
                    var gameObjs = om.Objects.OfType<Models.WoWGameObject>().ToArray();
                    Log.Warning("[CustomAnim] Guid=0x{Guid:X} not found as WoWGameObject. Tracked GOs: {Count}", guid, gameObjs.Length);
                    foreach (var tracked in gameObjs.Take(10))
                        Log.Warning("[CustomAnim]   GO 0x{Guid:X} DisplayId={DisplayId} TypeId={TypeId}", tracked.Guid, tracked.DisplayId, tracked.TypeId);
                    return;
                }

                // Fish bite: anim 0 on a bobber created by our player
                Log.Information("[CustomAnim] GO match: DisplayId={DisplayId} CreatedBy=0x{CreatedBy:X} PlayerGuid=0x{PlayerGuid:X}",
                    go.DisplayId, go.CreatedBy.FullGuid, om.PlayerGuid.FullGuid);

                if (anim == 0 && go.CreatedBy.FullGuid == om.PlayerGuid.FullGuid)
                {
                    Log.Information("[FishBite] Bobber 0x{Guid:X} — auto-interacting", guid);
                    om.InteractWithGameObject(guid);

                    // Auto-loot the catch after server processes interaction
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1500);
                        Log.Information("[FishBite] Auto-looting slot 0...");
                        om.AutoStoreLootItem(0);
                        await Task.Delay(500);
                        om.ReleaseLoot(guid);
                        Log.Information("[FishBite] Released loot for bobber 0x{Guid:X}", guid);
                    });
                }
            }
            catch (EndOfStreamException) { }
        }
    }
}
