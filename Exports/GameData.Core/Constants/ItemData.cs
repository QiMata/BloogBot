using System.Collections.Generic;

namespace GameData.Core.Constants;

public static class ItemData
{
    /// <summary>
    /// Items whose `item_template.spellid_1..5` trigger a spell that applies the mounted aura
    /// in the local VMaNGOS database.
    /// Query source:
    /// `SELECT entry FROM item_template WHERE spellid_1..5 IN (mounted-aura spell entries) GROUP BY entry ORDER BY entry;`
    /// where mounted-aura spell entries come from:
    /// `SELECT entry FROM spell_template WHERE effectApplyAuraName1 = 78 OR effectApplyAuraName2 = 78 OR effectApplyAuraName3 = 78`.
    /// </summary>
    public static readonly HashSet<uint> MountItemIds =
    [
        823, 842, 875, 901, 902, 903, 1041, 1042, 1043, 1044, 1122, 1123, 1124, 1132, 1133,
        1134, 2411, 2413, 2414, 2415, 5655, 5656, 5663, 5665, 5668, 5864, 5872, 5873, 5874,
        5875, 8563, 8583, 8586, 8588, 8589, 8590, 8591, 8592, 8595, 8627, 8628, 8629, 8630,
        8631, 8632, 8633, 12302, 12303, 12325, 12326, 12327, 12330, 12351, 12353, 12354, 13086,
        13317, 13321, 13322, 13323, 13324, 13325, 13326, 13327, 13328, 13329, 13331, 13332,
        13333, 13334, 13335, 14062, 15277, 15290, 15292, 15293, 16338, 16339, 16343, 16344,
        18063, 18241, 18242, 18243, 18244, 18245, 18246, 18247, 18248, 18766, 18767, 18768,
        18772, 18773, 18774, 18776, 18777, 18778, 18785, 18786, 18787, 18788, 18789, 18790,
        18791, 18793, 18794, 18795, 18796, 18797, 18798, 18902, 19029, 19030, 19872, 19902,
        20221, 21044, 21218, 21321, 21323, 21324, 21736, 23193, 23720
    ];

    public static bool IsMountItem(int itemId) => itemId > 0 && IsMountItem((uint)itemId);

    public static bool IsMountItem(uint itemId) => MountItemIds.Contains(itemId);
}
