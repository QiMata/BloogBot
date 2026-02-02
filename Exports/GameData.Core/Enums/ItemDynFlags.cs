namespace GameData.Core.Enums;

[Flags]
public enum ItemDynFlags
{
    ITEM_DYNFLAG_BINDED = 0x00000001, // set in game at binding
    ITEM_DYNFLAG_UNK1 = 0x00000002,
    ITEM_DYNFLAG_UNLOCKED = 0x00000004, // have meaning only for item with proto->LockId, if not set show as "Locked, req. lockpicking N"
    ITEM_DYNFLAG_WRAPPED = 0x00000008, // mark item as wrapped into wrapper container
    ITEM_DYNFLAG_UNK4 = 0x00000010, // can't repeat old note: appears red icon (like when item durability==0)
    ITEM_DYNFLAG_UNK5 = 0x00000020,
    ITEM_DYNFLAG_UNK6 = 0x00000040, // ? old note: usable
    ITEM_DYNFLAG_UNK7 = 0x00000080,
    ITEM_DYNFLAG_UNK8 = 0x00000100,
    ITEM_DYNFLAG_READABLE = 0x00000200, // can be open for read, it or item proto pagetText make show "Right click to read"
    ITEM_DYNFLAG_UNK10 = 0x00000400,
    ITEM_DYNFLAG_UNK11 = 0x00000800,
    ITEM_DYNFLAG_UNK12 = 0x00001000,
    ITEM_DYNFLAG_UNK13 = 0x00002000,
    ITEM_DYNFLAG_UNK14 = 0x00004000,
    ITEM_DYNFLAG_UNK15 = 0x00008000,
    ITEM_DYNFLAG_UNK16 = 0x00010000,
    ITEM_DYNFLAG_UNK17 = 0x00020000,
}