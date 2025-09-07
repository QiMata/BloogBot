namespace GameData.Core.Enums;

public enum PlayerMountResult
{
    MOUNTRESULT_INVALIDMOUNTEE = 0,    // You can't mount that unit!
    MOUNTRESULT_TOOFARAWAY = 1,    // That mount is too far away!
    MOUNTRESULT_ALREADYMOUNTED = 2,    // You're already mounted!
    MOUNTRESULT_NOTMOUNTABLE = 3,    // That unit can't be mounted!
    MOUNTRESULT_NOTYOURPET = 4,    // That mount isn't your pet!
    MOUNTRESULT_OTHER = 5,    // internal
    MOUNTRESULT_LOOTING = 6,    // You can't mount while looting!
    MOUNTRESULT_RACECANTMOUNT = 7,    // You can't mount because of your race!
    MOUNTRESULT_SHAPESHIFTED = 8,    // You can't mount while shapeshifted!
    MOUNTRESULT_FORCEDDISMOUNT = 9,    // You dismount before continuing.
    MOUNTRESULT_OK = 10    // no error
}