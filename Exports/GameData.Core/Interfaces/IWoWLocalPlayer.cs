using GameData.Core.Models;

namespace GameData.Core.Interfaces
{
    /// <summary>
    /// Local player state. Extends <see cref="IWoWPlayer"/> with client-local fields
    /// (corpse lifecycle, debuff flags, stance, combat state).
    ///
    /// Corpse lifecycle contract (used by RetrieveCorpseTask and DeathCorpseRunTests):
    ///   - <see cref="CorpsePosition"/>: world position of the player's corpse after death.
    ///   - <see cref="InGhostForm"/>: true when the player is a ghost (released spirit).
    ///   - <see cref="CanResurrect"/>: true when the ghost is within reclaim range of the corpse.
    ///   - <see cref="CorpseRecoveryDelaySeconds"/>: server-enforced cooldown before corpse reclaim is allowed.
    /// Both FG and BG implementations must provide these fields for corpse-run decisions.
    /// </summary>
    public interface IWoWLocalPlayer : IWoWPlayer
    {
        /// <summary>World position of the player's corpse. Valid after death and spirit release.</summary>
        Position CorpsePosition { get; }

        /// <summary>True when the player has released spirit and is in ghost form (PLAYER_FLAGS_GHOST).</summary>
        bool InGhostForm { get; }

        bool IsCursed { get; }
        bool IsPoisoned { get; }
        int ComboPoints { get; }
        bool IsDiseased { get; }
        string CurrentStance { get; }
        bool HasMagicDebuff { get; }
        bool TastyCorpsesNearby { get; }
        bool CanRiposte { get; }
        bool MainhandIsEnchanted { get; }
        uint Copper { get; }
        bool IsAutoAttacking { get; }

        /// <summary>True when the ghost is within reclaim range of the corpse and the recovery delay has elapsed.</summary>
        bool CanResurrect { get; }

        /// <summary>Server-enforced cooldown (seconds) before the player can reclaim their corpse. Typically 30s after death.</summary>
        int CorpseRecoveryDelaySeconds { get; }

        bool InBattleground { get; }
        bool HasQuestTargets { get; }
    }
}
