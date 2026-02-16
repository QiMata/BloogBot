using System;
namespace GameData.Core.Enums;

[Flags]
public enum UnitBytes2_Flags
{
    UNIT_BYTE2_FLAG_UNK0 = 0x01,
    UNIT_BYTE2_FLAG_UNK1 = 0x02,
    UNIT_BYTE2_FLAG_UNK2 = 0x04,
    UNIT_BYTE2_FLAG_UNK3 = 0x08,
    UNIT_BYTE2_FLAG_AURAS = 0x10,                     // show positive auras as positive, and allow its dispel
    UNIT_BYTE2_FLAG_UNK5 = 0x20,
    UNIT_BYTE2_FLAG_UNK6 = 0x40,
    UNIT_BYTE2_FLAG_UNK7 = 0x80
}