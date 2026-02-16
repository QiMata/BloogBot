using System;
using System.Collections.Generic;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Handles login-time initialization packets sent by the server when a character enters the world.
    /// Parses and stores: action buttons, proficiencies, bind point, factions, and tutorial flags.
    /// </summary>
    public interface ICharacterInitNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Whether the initial login data has been received (at least SMSG_ACTION_BUTTONS arrived).
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// The 120 action bar buttons received from SMSG_ACTION_BUTTONS.
        /// Each button encodes an action type (spell/item/macro) and action ID.
        /// </summary>
        IReadOnlyList<ActionButton> ActionButtons { get; }

        /// <summary>
        /// Weapon and armor proficiency data from SMSG_SET_PROFICIENCY.
        /// Key: itemClass (2=Weapon, 4=Armor), Value: subclass bitmask.
        /// </summary>
        IReadOnlyDictionary<byte, uint> Proficiencies { get; }

        /// <summary>
        /// The player's hearthstone bind point from SMSG_BINDPOINTUPDATE.
        /// </summary>
        BindPointData? BindPoint { get; }

        /// <summary>
        /// Faction/reputation data from SMSG_INITIALIZE_FACTIONS (64 entries).
        /// </summary>
        IReadOnlyList<FactionEntry> Factions { get; }

        /// <summary>
        /// Tutorial flags from SMSG_TUTORIAL_FLAGS (256 bits as 8 × uint32).
        /// </summary>
        IReadOnlyList<uint> TutorialFlags { get; }

        /// <summary>
        /// Resolves an action bar slot to a spell ID, if the slot contains a spell.
        /// Returns null if the slot is empty, contains an item, or action buttons haven't been received.
        /// </summary>
        uint? GetSpellIdForActionBarSlot(byte slot);

        /// <summary>
        /// Observable stream of action button updates (fired each time SMSG_ACTION_BUTTONS is received).
        /// </summary>
        IObservable<IReadOnlyList<ActionButton>> ActionButtonUpdates { get; }

        /// <summary>
        /// Observable stream of proficiency updates.
        /// </summary>
        IObservable<ProficiencyData> ProficiencyUpdates { get; }

        /// <summary>
        /// Observable stream of bind point updates.
        /// </summary>
        IObservable<BindPointData> BindPointUpdates { get; }
    }
}
