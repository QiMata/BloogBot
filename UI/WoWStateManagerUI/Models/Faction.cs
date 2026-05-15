using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WoWStateManagerUI.Models
{
    /// <summary>
    /// Faction restriction on an Activity. <see cref="Either"/> means both
    /// factions can run the activity (most dungeons/raids/BGs); Alliance/Horde
    /// mark single-faction-only flows.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Faction
    {
        Either = 0,
        Alliance = 1,
        Horde = 2,
    }

    public static class FactionHelpers
    {
        /// <summary>
        /// Vanilla race-id → faction. Alliance: 1 Human, 3 Dwarf, 4 NightElf,
        /// 7 Gnome. Horde: 2 Orc, 5 Undead, 6 Tauren, 8 Troll.
        /// </summary>
        public static Faction FromRaceId(byte raceId) => raceId switch
        {
            1 or 3 or 4 or 7 => Faction.Alliance,
            2 or 5 or 6 or 8 => Faction.Horde,
            _ => Faction.Either,
        };

        /// <summary>True if the activity's faction restriction allows this character's faction.</summary>
        public static bool Allows(Faction activityFaction, Faction characterFaction)
            => activityFaction == Faction.Either
            || characterFaction == Faction.Either
            || activityFaction == characterFaction;
    }
}
