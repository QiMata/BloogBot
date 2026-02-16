using System;
namespace GameData.Core.Enums;

[Flags]
public enum SpellAuraInterruptFlags
{
    AURA_INTERRUPT_FLAG_UNK0 = 0x00000001,   // 0    removed when getting hit by a negative spell?
    AURA_INTERRUPT_FLAG_DAMAGE = 0x00000002,   // 1    removed by any damage
    AURA_INTERRUPT_FLAG_UNK2 = 0x00000004,   // 2
    AURA_INTERRUPT_FLAG_MOVE = 0x00000008,   // 3    removed by any movement
    AURA_INTERRUPT_FLAG_TURNING = 0x00000010,   // 4    removed by any turning
    AURA_INTERRUPT_FLAG_ENTER_COMBAT = 0x00000020,   // 5    removed by entering combat
    AURA_INTERRUPT_FLAG_NOT_MOUNTED = 0x00000040,   // 6    removed by unmounting
    AURA_INTERRUPT_FLAG_NOT_ABOVEWATER = 0x00000080,   // 7    removed by entering water
    AURA_INTERRUPT_FLAG_NOT_UNDERWATER = 0x00000100,   // 8    removed by leaving water
    AURA_INTERRUPT_FLAG_NOT_SHEATHED = 0x00000200,   // 9    removed by unsheathing
    AURA_INTERRUPT_FLAG_UNK10 = 0x00000400,   // 10
    AURA_INTERRUPT_FLAG_UNK11 = 0x00000800,   // 11
    AURA_INTERRUPT_FLAG_MELEE_ATTACK = 0x00001000,   // 12   removed by melee attacks
    AURA_INTERRUPT_FLAG_UNK13 = 0x00002000,   // 13
    AURA_INTERRUPT_FLAG_UNK14 = 0x00004000,   // 14
    AURA_INTERRUPT_FLAG_UNK15 = 0x00008000,   // 15   removed by casting a spell?
    AURA_INTERRUPT_FLAG_UNK16 = 0x00010000,   // 16
    AURA_INTERRUPT_FLAG_MOUNTING = 0x00020000,   // 17   removed by mounting
    AURA_INTERRUPT_FLAG_NOT_SEATED = 0x00040000,   // 18   removed by standing up (used by food and drink mostly and sleep/Fake Death like)
    AURA_INTERRUPT_FLAG_CHANGE_MAP = 0x00080000,   // 19   leaving map/getting teleported
    AURA_INTERRUPT_FLAG_IMMUNE_OR_LOST_SELECTION = 0x00100000,   // 20   removed by auras that make you invulnerable, or make other to loose selection on you
    AURA_INTERRUPT_FLAG_UNK21 = 0x00200000,   // 21
    AURA_INTERRUPT_FLAG_UNK22 = 0x00400000,   // 22
    AURA_INTERRUPT_FLAG_ENTER_PVP_COMBAT = 0x00800000,   // 23   removed by entering pvp combat
    AURA_INTERRUPT_FLAG_DIRECT_DAMAGE = 0x01000000    // 24   removed by any direct damage
};