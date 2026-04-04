using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Travel;

/// <summary>
/// Meeting stone (summoning stone) positions for vanilla dungeons.
/// Extracted from <see cref="DungeonEntryData"/> definitions.
/// Source: MaNGOS gameobject table WHERE type=23 (GAMEOBJECT_TYPE_MEETINGSTONE).
/// </summary>
public static class SummoningStoneData
{
    public record SummoningStone(
        string DungeonName,
        string Abbreviation,
        uint InstanceMapId,
        uint StoneMapId,
        Position StonePosition,
        int MinLevel,
        int MaxLevel);

    /// <summary>
    /// All meeting stones from DungeonEntryData that have a MeetingStonePosition.
    /// </summary>
    public static IReadOnlyList<SummoningStone> AllStones { get; } = BuildStoneList();

    /// <summary>
    /// Find the meeting stone for a dungeon by instance map ID.
    /// Returns null if the dungeon has no meeting stone (city dungeons like RFC, Stockade).
    /// </summary>
    public static SummoningStone? GetByInstanceMapId(uint instanceMapId)
        => AllStones.FirstOrDefault(s => s.InstanceMapId == instanceMapId);

    /// <summary>
    /// Find meeting stones near a position (within searchRadius on the same map).
    /// </summary>
    public static IReadOnlyList<SummoningStone> GetNearby(uint mapId, Position position, float searchRadius = 200f)
    {
        float r2 = searchRadius * searchRadius;
        return AllStones
            .Where(s => s.StoneMapId == mapId
                && s.StonePosition.DistanceTo(position) * s.StonePosition.DistanceTo(position) <= r2)
            .ToList();
    }

    private static List<SummoningStone> BuildStoneList()
    {
        var stones = new List<SummoningStone>();
        foreach (var dungeon in DungeonEntryData.AllDungeons)
        {
            if (dungeon.MeetingStonePosition != null && dungeon.MeetingStoneMapId.HasValue)
            {
                stones.Add(new SummoningStone(
                    dungeon.Name,
                    dungeon.Abbreviation,
                    dungeon.InstanceMapId,
                    dungeon.MeetingStoneMapId.Value,
                    dungeon.MeetingStonePosition,
                    dungeon.MinLevel,
                    dungeon.MaxLevel));
            }
        }
        return stones;
    }
}
