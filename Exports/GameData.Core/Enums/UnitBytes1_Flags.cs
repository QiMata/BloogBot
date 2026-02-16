using System;

namespace GameData.Core.Enums;

[Flags]
public enum UnitBytes1_Flags
{
    UNIT_BYTE1_FLAG_ALWAYS_STAND = 0x01,
    UNIT_BYTE1_FLAGS_CREEP = 0x02,
    UNIT_BYTE1_FLAG_UNTRACKABLE = 0x04,
    UNIT_BYTE1_FLAG_ALL = 0xFF
}