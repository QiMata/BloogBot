namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Best-effort travel hint for an activity. Authoritative
    /// <c>(MapId, Position)</c> resolution is performed at runtime via
    /// <c>NamedLocationResolver</c> (see <c>Plan/Activities/travel.md</c>
    /// slot ST.6) against <c>Bot/named-locations.json</c>.
    /// </summary>
    public sealed record TravelTarget(
        uint MapId,
        float X,
        float Y,
        float Z,
        string NamedLocation);
}
