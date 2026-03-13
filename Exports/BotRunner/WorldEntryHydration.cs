using GameData.Core.Interfaces;

namespace BotRunner;

internal static class WorldEntryHydration
{
    internal static bool IsReadyForWorldInteraction(IWoWLocalPlayer? player)
    {
        if (player == null)
            return false;

        if (player.Guid == 0 || player.Position == null)
            return false;

        return player.MaxHealth > 0;
    }
}
