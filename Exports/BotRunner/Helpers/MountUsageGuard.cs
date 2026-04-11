using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Interfaces;

namespace BotRunner.Helpers;

internal static class MountUsageGuard
{
    public static bool TryGetBlockedReasonForSpell(IObjectManager objectManager, int spellId, out string reason)
    {
        if (!SpellData.IsMountSpell(spellId))
        {
            reason = string.Empty;
            return false;
        }

        return TryGetBlockedReasonForMountAttempt(objectManager, $"spell={spellId}", out reason);
    }

    public static bool TryGetBlockedReasonForItem(IObjectManager objectManager, IWoWItem? item, out string reason)
    {
        if (!IsMountItem(item))
        {
            reason = string.Empty;
            return false;
        }

        var source = item != null && item.ItemId != 0
            ? $"item={item.ItemId}"
            : "mount-item";
        return TryGetBlockedReasonForMountAttempt(objectManager, source, out reason);
    }

    public static bool TryGetBlockedReasonForItemId(IObjectManager objectManager, uint itemId, out string reason)
    {
        if (!ItemData.IsMountItem(itemId))
        {
            reason = string.Empty;
            return false;
        }

        return TryGetBlockedReasonForMountAttempt(objectManager, $"item={itemId}", out reason);
    }

    private static bool IsMountItem(IWoWItem? item)
    {
        if (item == null)
            return false;

        return ItemData.IsMountItem(item.ItemId);
    }

    private static bool TryGetBlockedReasonForMountAttempt(IObjectManager objectManager, string source, out string reason)
    {
        if (objectManager.Player?.IsMounted == true || objectManager.PhysicsAllowsMountByEnvironment)
        {
            reason = string.Empty;
            return false;
        }

        var flags = objectManager.PhysicsEnvironmentFlags;
        reason = $"{source} indoors={flags.IsIndoors()} flags=0x{(uint)flags:X}";
        return true;
    }
}
