namespace GameData.Core.Enums;

enum PetModeFlags
{
    PET_MODE_UNKNOWN_0 = 0x0000001,
    PET_MODE_UNKNOWN_2 = 0x0000100,
    PET_MODE_DISABLE_ACTIONS = 0x8000000,

    // autoset in client at summon
    PET_MODE_DEFAULT = PET_MODE_UNKNOWN_0 | PET_MODE_UNKNOWN_2,
}