namespace GameData.Core.Enums;

enum ItemLootUpdateState
{
    ITEM_LOOT_NONE = 0,      // loot not generated
    ITEM_LOOT_TEMPORARY = 1,      // generated loot is temporary (will deleted at loot window close)
    ITEM_LOOT_UNCHANGED = 2,
    ITEM_LOOT_CHANGED = 3,
    ITEM_LOOT_NEW = 4,
    ITEM_LOOT_REMOVED = 5
}