namespace GameData.Core.Enums;

[Flags]
public enum HitInfo
{
    HITINFO_NORMALSWING = 0x00000000,
    HITINFO_UNK0 = 0x00000001,               // req correct packet structure
    HITINFO_NORMALSWING2 = 0x00000002,
    HITINFO_LEFTSWING = 0x00000004,
    HITINFO_UNK3 = 0x00000008,
    HITINFO_MISS = 0x00000010,
    HITINFO_ABSORB = 0x00000020,               // plays absorb sound
    HITINFO_RESIST = 0x00000040,               // resisted atleast some damage
    HITINFO_CRITICALHIT = 0x00000080,
    HITINFO_UNK8 = 0x00000100,               // wotlk?
    HITINFO_BLOCK = 0x00000800,               // [ZERO]
    HITINFO_UNK9 = 0x00002000,               // wotlk?
    HITINFO_GLANCING = 0x00004000,
    HITINFO_CRUSHING = 0x00008000,
    HITINFO_NOACTION = 0x00010000,
    HITINFO_SWINGNOHITSOUND = 0x00080000
}