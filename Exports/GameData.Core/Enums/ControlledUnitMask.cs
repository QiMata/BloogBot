namespace GameData.Core.Enums;

public enum ControlledUnitMask
{
    CONTROLLED_PET = 0x01,
    CONTROLLED_MINIPET = 0x02,
    CONTROLLED_GUARDIANS = 0x04,                            // including PROTECTOR_PET
    CONTROLLED_CHARM = 0x08,
    CONTROLLED_TOTEMS = 0x10,
}