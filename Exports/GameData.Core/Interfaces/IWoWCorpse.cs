using GameData.Core.Enums;
using GameData.Core.Models;

namespace GameData.Core.Interfaces
{
    /// <summary>
    /// Corpse game object. Used during corpse-run to locate the player's body.
    ///
    /// Corpse lifecycle contract:
    ///   - <see cref="OwnerGuid"/>: GUID of the player who owns this corpse. Match against PlayerGuid to find own corpse.
    ///   - <see cref="Type"/>: <see cref="CorpseType"/> distinguishing player corpses from bones (lootable remains).
    ///   - <see cref="CorpseFlags"/>: bitfield indicating PvP kill, lootable, etc.
    /// The <see cref="IObjectManager.RetrieveCorpse"/> call triggers CMSG_RECLAIM_CORPSE when in range.
    /// </summary>
    public interface IWoWCorpse : IWoWGameObject
    {
        /// <summary>GUID of the player who owns this corpse.</summary>
        HighGuid OwnerGuid { get; }

        uint GhostTime { get; }

        /// <summary>Player corpse vs bones (lootable skeleton remains).</summary>
        CorpseType Type { get; }

        float Angle { get; }

        /// <summary>Bitfield: PvP kill, lootable, etc.</summary>
        CorpseFlags CorpseFlags { get; }

        uint Guild { get; }
        uint[] Items { get; }
        byte[] Bytes2 { get; }
        byte[] Bytes1 { get; }

        bool IsBones();
        bool IsPvP();
    }
}