using GameData.Core.Interfaces;

namespace BotRunner.Helpers;

/// <summary>
/// Consolidated ghost / corpse / dead state checks previously duplicated
/// across BotRunnerService, RetrieveCorpseTask and TeleportTask.
/// </summary>
public static class DeathStateDetection
{
    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7;     // UNIT_STAND_STATE_DEAD

    /// <summary>True when the PLAYER_FLAGS_GHOST bit is set.</summary>
    public static bool HasGhostFlag(IWoWLocalPlayer player)
    {
        try { return (((uint)player.PlayerFlags) & PlayerFlagGhost) != 0; }
        catch { return false; }
    }

    /// <summary>True when UNIT_STAND_STATE_DEAD is active in Bytes1.</summary>
    public static bool IsStandStateDead(IWoWLocalPlayer player)
    {
        try
        {
            var bytes1 = player.Bytes1;
            return bytes1 != null
                && bytes1.Length > 0
                && (bytes1[0] & StandStateMask) == StandStateDead;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// True when the player is in ghost form (released spirit).
    /// Checks both the player flag and the InGhostForm property.
    /// </summary>
    public static bool IsGhost(IWoWLocalPlayer player)
    {
        if (HasGhostFlag(player))
            return true;

        try { return player.InGhostForm; }
        catch { return false; }
    }

    /// <summary>
    /// True when the player is a corpse (dead but not yet released).
    /// A ghost is NOT a corpse.
    /// </summary>
    public static bool IsCorpse(IWoWLocalPlayer player)
    {
        if (IsGhost(player)) return false;
        // Require stand-state-dead flag from the server, not just HP==0.
        // GM level 6 accounts sit at HP 0/1 under damage without actually dying -
        // the server never sets UNIT_STAND_STATE_DEAD for them.
        // HP==0 alone is NOT sufficient to declare death.
        return IsStandStateDead(player);
    }

    /// <summary>
    /// True when it is safe to attempt a spirit release.
    /// Some server builds expose a short corpse window as HP==0 before UNIT_STAND_STATE_DEAD
    /// lands in the snapshot, but CMSG_REPOP_REQUEST is already valid in that window.
    /// </summary>
    public static bool CanReleaseSpirit(IWoWLocalPlayer player)
    {
        if (IsGhost(player))
            return false;

        if (IsStandStateDead(player))
            return true;

        // The Health==0 fallback covers the brief window where the corpse state has
        // landed but UNIT_STAND_STATE_DEAD hasn't propagated yet. It must not fire
        // for an uninitialized snapshot — a freshly-hydrating BG bot reports
        // Health==0 alongside MaxHealth==0 before the server's UPDATE_OBJECT
        // packet arrives, and a corpse-recovery push at that point traps the bot
        // in CharacterSelect because the player object is still half-loaded.
        try { return player.Health == 0 && player.MaxHealth > 0; }
        catch { return false; }
    }

    /// <summary>True when the player is dead or a ghost (any death state).</summary>
    public static bool IsDeadOrGhost(IWoWLocalPlayer player)
        => IsGhost(player) || IsCorpse(player);

    /// <summary>
    /// Broad dead-or-ghost check used by RetrieveCorpseTask.
    /// Also returns true when HP == 0 regardless of flags.
    /// </summary>
    public static bool IsDeadOrGhostBroad(IWoWLocalPlayer player)
        => player.Health == 0 || HasGhostFlag(player) || IsStandStateDead(player) || IsGhost(player);

    /// <summary>
    /// Strict alive check: HP &gt; 0, no ghost flag, no stand-state-dead.
    /// </summary>
    public static bool IsStrictAlive(IWoWLocalPlayer player)
        => player.Health > 0 && !HasGhostFlag(player) && !IsStandStateDead(player);
}
