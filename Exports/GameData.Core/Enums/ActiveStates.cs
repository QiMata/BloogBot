namespace GameData.Core.Enums;

public enum ActiveStates
{
    ACT_PASSIVE = 0x01,                                    // 0x01 - passive
    ACT_DISABLED = 0x81,                                    // 0x80 - castable
    ACT_ENABLED = 0xC1,                                    // 0x40 | 0x80 - auto cast + castable
    ACT_COMMAND = 0x07,                                    // 0x01 | 0x02 | 0x04
    ACT_REACTION = 0x06,                                    // 0x02 | 0x04
    ACT_DECIDE = 0x00                                     // custom
}