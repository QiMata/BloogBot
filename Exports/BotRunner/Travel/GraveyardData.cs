using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Travel;

/// <summary>
/// Graveyard position cache. Loaded from MaNGOS world_safe_locs table at startup.
/// Cross-referenced with game_graveyard_zone for zone → graveyard mapping.
/// Used by RetrieveCorpseTask and spirit healer navigation.
/// </summary>
public static class GraveyardData
{
    public record GraveyardEntry(uint Id, uint MapId, Position Position, string Name);

    private static readonly List<GraveyardEntry> _graveyards = [];
    private static readonly Dictionary<uint, List<uint>> _zoneToGraveyardIds = new();

    /// <summary>All known graveyards.</summary>
    public static IReadOnlyList<GraveyardEntry> AllGraveyards => _graveyards;

    /// <summary>
    /// Load graveyard positions from world_safe_locs query results.
    /// Call at startup with DB query: SELECT id, map, x, y, z, name FROM world_safe_locs
    /// </summary>
    public static void LoadFromDatabase(IEnumerable<(uint id, uint map, float x, float y, float z, string name)> safeLocs)
    {
        _graveyards.Clear();
        foreach (var (id, map, x, y, z, name) in safeLocs)
        {
            _graveyards.Add(new GraveyardEntry(id, map, new Position(x, y, z), name));
        }
    }

    /// <summary>
    /// Load zone → graveyard mapping from game_graveyard_zone.
    /// Call with: SELECT id, ghost_zone FROM game_graveyard_zone
    /// </summary>
    public static void LoadZoneMapping(IEnumerable<(uint graveyardId, uint zoneId)> zoneMappings)
    {
        _zoneToGraveyardIds.Clear();
        foreach (var (gId, zoneId) in zoneMappings)
        {
            if (!_zoneToGraveyardIds.ContainsKey(zoneId))
                _zoneToGraveyardIds[zoneId] = [];
            _zoneToGraveyardIds[zoneId].Add(gId);
        }
    }

    /// <summary>
    /// Find the nearest graveyard on the same map.
    /// </summary>
    public static GraveyardEntry? FindNearest(uint mapId, Position position)
        => _graveyards
            .Where(g => g.MapId == mapId)
            .OrderBy(g => g.Position.DistanceTo(position))
            .FirstOrDefault();

    /// <summary>
    /// Find graveyards associated with a specific zone.
    /// </summary>
    public static IReadOnlyList<GraveyardEntry> GetForZone(uint zoneId)
    {
        if (!_zoneToGraveyardIds.TryGetValue(zoneId, out var ids))
            return [];
        return _graveyards.Where(g => ids.Contains(g.Id)).ToList();
    }
}
