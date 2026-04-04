using GameData.Core.Models;
using System.Collections.Generic;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Player faction for travel route filtering.
/// </summary>
public enum TravelFaction { Horde, Alliance }

/// <summary>
/// Options for cross-world travel planning. Passed to TravelTask and CrossMapRouter
/// to determine the optimal route based on available travel modes.
/// </summary>
public record TravelOptions
{
    /// <summary>Allow using hearthstone if it speeds up the route.</summary>
    public bool AllowHearthstone { get; init; } = true;

    /// <summary>Allow class-specific teleports (Mage Teleport, Warlock summon).</summary>
    public bool AllowClassTeleport { get; init; } = true;

    /// <summary>Allow using flight paths (taxi) for long-distance legs.</summary>
    public bool AllowFlightPath { get; init; } = true;

    /// <summary>Player faction — determines which transports, flight paths, and cities are accessible.</summary>
    public TravelFaction PlayerFaction { get; init; } = TravelFaction.Horde;

    /// <summary>Set of discovered flight path node IDs. Only discovered nodes can be used.</summary>
    public IReadOnlyCollection<uint> DiscoveredFlightNodes { get; init; } = [];

    /// <summary>Map ID of the hearthstone bind point. Null if unknown.</summary>
    public uint? HearthstoneBindMapId { get; init; }

    /// <summary>Position of the hearthstone bind point. Null if unknown.</summary>
    public Position? HearthstoneBindPosition { get; init; }

    /// <summary>Remaining cooldown on the hearthstone in seconds. 0 = ready.</summary>
    public float HearthstoneCooldownRemainingSec { get; init; }
}
