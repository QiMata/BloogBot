using System;

namespace GameData.Core.Enums;

[Flags]
public enum NPCFlags
{
    UNIT_NPC_FLAG_NONE = 0x00000000,
    UNIT_NPC_FLAG_GOSSIP = 0x00000001,       ///< 100%
    UNIT_NPC_FLAG_QUESTGIVER = 0x00000002,       ///< 100%
    UNIT_NPC_FLAG_VENDOR = 0x00000004,       ///< 100%
    UNIT_NPC_FLAG_FLIGHTMASTER = 0x00000008,       ///< 100%
    UNIT_NPC_FLAG_TRAINER = 0x00000010,       ///< 100%
    UNIT_NPC_FLAG_SPIRITHEALER = 0x00000020,       ///< guessed
    UNIT_NPC_FLAG_SPIRITGUIDE = 0x00000040,       ///< guessed
    UNIT_NPC_FLAG_INNKEEPER = 0x00000080,       ///< 100%
    UNIT_NPC_FLAG_BANKER = 0x00000100,       ///< 100%
    UNIT_NPC_FLAG_PETITIONER = 0x00000200,       ///< 100% 0xC0000 = guild petitions
    UNIT_NPC_FLAG_TABARDDESIGNER = 0x00000400,       ///< 100%
    UNIT_NPC_FLAG_BATTLEMASTER = 0x00000800,       ///< 100%
    UNIT_NPC_FLAG_AUCTIONEER = 0x00001000,       ///< 100%
    UNIT_NPC_FLAG_STABLEMASTER = 0x00002000,       ///< 100%
    UNIT_NPC_FLAG_REPAIR = 0x00004000,       ///< 100%
    UNIT_NPC_FLAG_SPELLCLICK = 0x01000000,       // cause client to send 1015 opcode (spell click), dynamic, set at loading and don't must be set in DB
    UNIT_NPC_FLAG_OUTDOORPVP = 0x20000000        ///< custom flag for outdoor pvp creatures || Custom flag
}