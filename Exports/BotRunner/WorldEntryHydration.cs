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

        // Ghost state is valid for world interaction (RetrieveCorpse needs this).
        // FG memory reads for MaxHealth can be stale during ghost form, so
        // check InGhostForm explicitly rather than relying only on MaxHealth.
        if (player.InGhostForm)
            return true;

        return player.MaxHealth > 0;
    }
}
