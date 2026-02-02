namespace GameData.Core.Enums;

public enum PlayerFieldByte2Flags
{
    PLAYER_FIELD_BYTE2_NONE = 0x00,
    PLAYER_FIELD_BYTE2_DETECT_AMORE_0 = 0x02,            // SPELL_AURA_DETECT_AMORE, not used as value and maybe not relcted to, but used in code as base for mask apply
    PLAYER_FIELD_BYTE2_DETECT_AMORE_1 = 0x04,            // SPELL_AURA_DETECT_AMORE value 1
    PLAYER_FIELD_BYTE2_DETECT_AMORE_2 = 0x08,            // SPELL_AURA_DETECT_AMORE value 2
    PLAYER_FIELD_BYTE2_DETECT_AMORE_3 = 0x10,            // SPELL_AURA_DETECT_AMORE value 3
    PLAYER_FIELD_BYTE2_STEALTH = 0x20,
    PLAYER_FIELD_BYTE2_INVISIBILITY_GLOW = 0x40
}