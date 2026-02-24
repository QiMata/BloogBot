using ForegroundBotRunner.Mem;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;

namespace ForegroundBotRunner.Objects
{
    public class WoWPlayer : WoWUnit, IWoWPlayer
    {
        internal WoWPlayer(
            nint pointer,
            HighGuid guid,
            WoWObjectType objectType)
            : base(pointer, guid, objectType)
        {
        }


        public uint MapId
        {
            // this is weird and throws an exception right after entering world,
            // so we catch and ignore the exception to avoid console noise
            get
            {
                try
                {
                    var objectManagerPtr = MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase);
                    return MemoryManager.ReadUint(nint.Add(objectManagerPtr, 0xCC));
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }
        public bool IsEating => HasBuff("Food");

        public bool IsDrinking => HasBuff("Drink");

        public Race Race => throw new NotImplementedException();

        public Class Class => throw new NotImplementedException();

        public HighGuid DuelArbiter => throw new NotImplementedException();

        public HighGuid ComboTarget => throw new NotImplementedException();

        public PlayerFlags PlayerFlags => (PlayerFlags)MemoryManager.ReadUint(GetDescriptorPtr() + (int)UpdateFields.EPlayerFields.PLAYER_FLAGS * 4);

        public uint GuildId => throw new NotImplementedException();

        public uint GuildRank => throw new NotImplementedException();

        public byte[] Bytes => ReadPackedByteField(UpdateFields.EPlayerFields.PLAYER_BYTES);

        public byte[] Bytes3 => ReadPackedByteField(UpdateFields.EPlayerFields.PLAYER_BYTES_3);

        public uint GuildTimestamp => throw new NotImplementedException();

        public QuestSlot[] QuestLog
        {
            get
            {
                const int questSlotCount = 20;
                var result = new QuestSlot[questSlotCount];
                var descriptorPtr = GetDescriptorPtr();
                int baseOffset = (int)UpdateFields.EPlayerFields.PLAYER_QUEST_LOG_1_1 * 4;

                for (int i = 0; i < questSlotCount; i++)
                {
                    int slotOffset = baseOffset + i * 12; // 3 uint32 fields per quest slot
                    var questId = MemoryManager.ReadUint(descriptorPtr + slotOffset);
                    var countersPacked = MemoryManager.ReadUint(descriptorPtr + slotOffset + 4);
                    var questState = MemoryManager.ReadUint(descriptorPtr + slotOffset + 8);

                    result[i] = new QuestSlot
                    {
                        QuestId = questId,
                        QuestCounters =
                        [
                            (byte)(countersPacked & 0xFF),
                            (byte)((countersPacked >> 8) & 0xFF),
                            (byte)((countersPacked >> 16) & 0xFF),
                            (byte)((countersPacked >> 24) & 0xFF)
                        ],
                        QuestState = questState
                    };
                }

                return result;
            }
        }

        public IWoWItem[] VisibleItems => throw new NotImplementedException();

        public uint[] Inventory
        {
            get
            {
                // 23 equipment+bag slots × 2 uint32 fields (low/high GUID pairs) = 46 values.
                // Read from descriptors (same pattern as PackSlots, SkillInfo, etc.).
                // The Pointer+0x2508 path was wrong — descriptors are the correct source.
                const int slotCount = 46;
                var result = new uint[slotCount];
                var descriptorPtr = GetDescriptorPtr();
                int baseOffset = (int)GameData.Core.Enums.UpdateFields.EPlayerFields.PLAYER_FIELD_INV_SLOT_HEAD * 4;
                for (int i = 0; i < slotCount; i++)
                    result[i] = MemoryManager.ReadUint(descriptorPtr + baseOffset + i * 4);
                return result;
            }
        }

        public uint[] PackSlots
        {
            get
            {
                // 16 backpack slots × 2 uint32 fields (low/high GUID pairs) = 32 values
                const int slotCount = 32;
                var result = new uint[slotCount];
                var descriptorPtr = GetDescriptorPtr();
                int baseOffset = (int)UpdateFields.EPlayerFields.PLAYER_FIELD_PACK_SLOT_1 * 4;
                for (int i = 0; i < slotCount; i++)
                    result[i] = MemoryManager.ReadUint(descriptorPtr + baseOffset + i * 4);
                return result;
            }
        }

        public uint[] BankSlots => throw new NotImplementedException();

        public uint[] BankBagSlots => throw new NotImplementedException();

        public uint[] VendorBuybackSlots => throw new NotImplementedException();

        public uint[] KeyringSlots => throw new NotImplementedException();

        public uint Farsight => throw new NotImplementedException();

        public uint XP => throw new NotImplementedException();

        public uint NextLevelXP => throw new NotImplementedException();

        public SkillInfo[] SkillInfo
        {
            get
            {
                // WoW 1.12.1 PLAYER_SKILL_INFO layout: INTERLEAVED, 3 fields per skill.
                // Each skill slot occupies 3 consecutive uint32 descriptor fields:
                //   offset + 0: SkillLine (low16) | Step (high16)   → SkillInt1
                //   offset + 1: Current (low16)   | Max (high16)    → SkillInt2
                //   offset + 2: TempBonus (low16) | PermBonus (high16) → SkillInt3
                // Total: 128 skills × 3 fields = 384 fields.
                const int skillCount = 128;
                var skills = new SkillInfo[skillCount];
                var descriptorPtr = GetDescriptorPtr();
                int baseOffset = (int)UpdateFields.EPlayerFields.PLAYER_SKILL_INFO_1_1 * 4;
                for (int i = 0; i < skillCount; i++)
                {
                    int skillOffset = baseOffset + i * 12; // 3 fields × 4 bytes each
                    skills[i] = new SkillInfo
                    {
                        SkillInt1 = MemoryManager.ReadUint(descriptorPtr + skillOffset),     // SkillLine|Step
                        SkillInt2 = MemoryManager.ReadUint(descriptorPtr + skillOffset + 4), // Current|Max
                        SkillInt3 = MemoryManager.ReadUint(descriptorPtr + skillOffset + 8), // TempBonus|Perm
                    };
                }
                return skills;
            }
        }

        public uint CharacterPoints1
        {
            get
            {
                try
                {
                    var results = Functions.LuaCallWithResult("{0} = UnitCharacterPoints(\"player\")");
                    return results.Length > 0 && uint.TryParse(results[0], out var v) ? v : 0;
                }
                catch { return 0; }
            }
        }

        public uint CharacterPoints2 => throw new NotImplementedException();

        public uint TrackCreatures => throw new NotImplementedException();

        public uint TrackResources => throw new NotImplementedException();

        public uint BlockPercentage => throw new NotImplementedException();

        public uint DodgePercentage => throw new NotImplementedException();

        public uint ParryPercentage => throw new NotImplementedException();

        public uint CritPercentage => throw new NotImplementedException();

        public uint RangedCritPercentage => throw new NotImplementedException();

        public uint[] ExploredZones => throw new NotImplementedException();

        public uint RestStateExperience => throw new NotImplementedException();

        public uint Coinage => throw new NotImplementedException();

        public uint[] StatBonusesPos => throw new NotImplementedException();

        public uint[] StatBonusesNeg => throw new NotImplementedException();

        public uint[] ResistBonusesPos => throw new NotImplementedException();

        public uint[] ResistBonusesNeg => throw new NotImplementedException();

        public uint[] ModDamageDonePos => throw new NotImplementedException();

        public uint[] ModDamageDoneNeg => throw new NotImplementedException();

        public float[] ModDamageDonePct => throw new NotImplementedException();

        public uint AmmoId => throw new NotImplementedException();

        public uint SelfResSpell => throw new NotImplementedException();

        public uint PvpMedals => throw new NotImplementedException();

        public uint[] BuybackPrices => throw new NotImplementedException();

        public uint[] BuybackTimestamps => throw new NotImplementedException();

        public uint SessionKills => throw new NotImplementedException();

        public uint YesterdayKills => throw new NotImplementedException();

        public uint LastWeekKills => throw new NotImplementedException();

        public uint ThisWeekKills => throw new NotImplementedException();

        public uint ThisWeekContribution => throw new NotImplementedException();

        public uint LifetimeHonorableKills => throw new NotImplementedException();

        public uint LifetimeDishonorableKills => throw new NotImplementedException();

        public uint WatchedFactionIndex => throw new NotImplementedException();

        public uint[] CombatRating => throw new NotImplementedException();

        public Gender Gender => throw new NotImplementedException();

        public void OfferTrade()
        {
            throw new NotImplementedException();
        }

        private byte[] ReadPackedByteField(UpdateFields.EPlayerFields field)
        {
            var descriptorPtr = GetDescriptorPtr();
            var packed = MemoryManager.ReadUint(descriptorPtr + (int)field * 4);
            return
            [
                (byte)(packed & 0xFF),
                (byte)((packed >> 8) & 0xFF),
                (byte)((packed >> 16) & 0xFF),
                (byte)((packed >> 24) & 0xFF)
            ];
        }
    }
}
