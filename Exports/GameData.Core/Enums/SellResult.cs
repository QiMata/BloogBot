namespace GameData.Core.Enums;

enum SellResult
{
    SELL_ERR_CANT_FIND_ITEM = 1,
    SELL_ERR_CANT_SELL_ITEM = 2,       // merchant doesn't like that item
    SELL_ERR_CANT_FIND_VENDOR = 3,       // merchant doesn't like you
    SELL_ERR_YOU_DONT_OWN_THAT_ITEM = 4,       // you don't own that item
    SELL_ERR_UNK = 5,       // nothing appears...
    SELL_ERR_ONLY_EMPTY_BAG = 6        // can only do with empty bags
}