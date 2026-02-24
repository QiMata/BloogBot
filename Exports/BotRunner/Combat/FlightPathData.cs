using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Combat;

/// <summary>
/// Static database of vanilla 1.12.1 taxi (flight path) nodes.
/// Maps node IDs to names, positions, factions, and map IDs.
/// Used by FlightMasterService for route selection.
/// </summary>
public static class FlightPathData
{
    public enum Faction { Alliance, Horde, Neutral }

    public record TaxiNodeInfo(
        uint NodeId,
        string Name,
        int MapId,       // 0 = Eastern Kingdoms, 1 = Kalimdor
        float X,
        float Y,
        float Z,
        Faction NodeFaction);

    /// <summary>
    /// All vanilla 1.12.1 taxi nodes. Node IDs match the server bitmask positions.
    /// Positions are world coordinates from the MaNGOS taxi_nodes table.
    /// </summary>
    public static readonly IReadOnlyDictionary<uint, TaxiNodeInfo> Nodes = new Dictionary<uint, TaxiNodeInfo>
    {
        // ===== EASTERN KINGDOMS — ALLIANCE =====
        [2]  = new(2,  "Stormwind",           0, -8840.0f,  497.0f,  109.6f, Faction.Alliance),
        [4]  = new(4,  "Sentinel Hill",       0, -10628.0f,1036.0f,   34.0f, Faction.Alliance),  // Westfall
        [5]  = new(5,  "Lakeshire",           0, -9430.0f, -2231.0f,  68.0f, Faction.Alliance),  // Redridge
        [6]  = new(6,  "Ironforge",           0, -4821.0f, -1155.0f, 502.0f, Faction.Alliance),
        [7]  = new(7,  "Menethil Harbor",     0, -3793.0f, -782.0f,    9.0f, Faction.Alliance),  // Wetlands
        [8]  = new(8,  "Thelsamar",           0, -5421.0f, -2930.0f, 347.0f, Faction.Alliance),  // Loch Modan
        [12] = new(12, "Darkshire",           0, -10514.0f,-1262.0f,  41.0f, Faction.Alliance),  // Duskwood
        [14] = new(14, "Southshore",          0, -832.0f,  -559.0f,   12.0f, Faction.Alliance),  // Hillsbrad
        [16] = new(16, "Refuge Pointe",       0, -1240.0f, -2515.0f,  22.0f, Faction.Alliance),  // Arathi
        [19] = new(19, "Booty Bay",           0, -14443.0f, 509.0f,   26.0f, Faction.Neutral),
        [43] = new(43, "Aerie Peak",          0, 334.0f,  -2068.0f, 124.0f, Faction.Alliance),   // Hinterlands
        [45] = new(45, "Nethergarde Keep",    0, -10456.0f,-3278.0f,  22.0f, Faction.Alliance),  // Blasted Lands
        [66] = new(66, "Chillwind Camp",      0, 928.0f,  -1430.0f,  65.0f, Faction.Alliance),   // Western Plaguelands
        [67] = new(67, "Light's Hope Chapel", 0, 2286.0f,  -5323.0f,  82.0f, Faction.Neutral),  // Eastern Plaguelands
        [71] = new(71, "Morgan's Vigil",      0, -7586.0f, -2189.0f, 165.0f, Faction.Alliance),  // Burning Steppes
        [74] = new(74, "Thorium Point",       0, -6554.0f, -1168.0f, 310.0f, Faction.Alliance),  // Searing Gorge

        // ===== EASTERN KINGDOMS — HORDE =====
        [10] = new(10, "The Sepulcher",       0, 473.0f,   1533.0f, 131.0f, Faction.Horde),     // Silverpine
        [11] = new(11, "Undercity",           0, 1567.0f,  267.0f,  -43.0f, Faction.Horde),
        [13] = new(13, "Tarren Mill",         0, -9.0f,    -860.0f,   55.0f, Faction.Horde),     // Hillsbrad
        [17] = new(17, "Hammerfall",          0, -1573.0f, -2620.0f,  52.0f, Faction.Horde),     // Arathi
        [18] = new(18, "Booty Bay",           0, -14443.0f, 509.0f,   26.0f, Faction.Neutral),   // same location, Horde entry
        [20] = new(20, "Grom'gol",            0, -12417.0f, 145.0f,    3.0f, Faction.Horde),     // Stranglethorn
        [21] = new(21, "Kargath",             0, -6632.0f, -3462.0f, 244.0f, Faction.Horde),     // Badlands
        [56] = new(56, "Stonard",             0, -10455.0f,-3279.0f,  22.0f, Faction.Horde),     // Swamp of Sorrows
        [68] = new(68, "Light's Hope Chapel", 0, 2286.0f,  -5323.0f,  82.0f, Faction.Neutral),   // EP Horde entry
        [76] = new(76, "Revantusk Village",   0, -140.0f,  -2541.0f,  22.0f, Faction.Horde),     // Hinterlands
        [75] = new(75, "Flame Crest",         0, -7511.0f, -2188.0f, 165.0f, Faction.Horde),     // Burning Steppes

        // ===== KALIMDOR — ALLIANCE =====
        [26] = new(26, "Auberdine",           1, 6342.0f,   554.0f,   17.0f, Faction.Alliance),  // Darkshore
        [27] = new(27, "Rut'theran Village",  1, 8643.0f,   841.0f,   23.0f, Faction.Alliance),  // Teldrassil
        [28] = new(28, "Astranaar",           1, 2681.0f,  -481.0f,  109.0f, Faction.Alliance),  // Ashenvale
        [31] = new(31, "Thalanaar",           1, -4491.0f, -779.0f,  -40.0f, Faction.Alliance),  // Feralas
        [32] = new(32, "Theramore",           1, -3826.0f, -4515.0f,  10.0f, Faction.Alliance),  // Dustwallow
        [33] = new(33, "Stonetalon Peak",     1, 1558.0f,   32.0f,   -8.0f, Faction.Alliance),   // Stonetalon
        [39] = new(39, "Gadgetzan",           1, -7117.0f, -3828.0f,  10.0f, Faction.Neutral),   // Tanaris
        [41] = new(41, "Feathermoon",         1, -4369.0f,  318.0f,   25.0f, Faction.Alliance),  // Feralas
        [42] = new(42, "Nijel's Point",       1, -621.0f,  -416.0f,   46.0f, Faction.Alliance),  // Desolace
        [49] = new(49, "Moonglade",           1, 7466.0f,  -2122.0f, 492.0f, Faction.Neutral),
        [52] = new(52, "Everlook",            1, 6801.0f,  -4611.0f, 711.0f, Faction.Neutral),   // Winterspring
        [62] = new(62, "Nighthaven",          1, 7461.0f,  -2123.0f, 493.0f, Faction.Neutral),   // Moonglade
        [64] = new(64, "Talrendis Point",     1, 3338.0f,  -4689.0f,  11.0f, Faction.Alliance),  // Azshara
        [65] = new(65, "Talonbranch Glade",   1, 5067.0f,  -1264.0f, 368.0f, Faction.Alliance),  // Felwood
        [73] = new(73, "Cenarion Hold",       1, -6807.0f,  833.0f,   51.0f, Faction.Neutral),   // Silithus
        [79] = new(79, "Marshal's Refuge",    1, -6111.0f, -1143.0f,  -7.0f, Faction.Neutral),   // Un'Goro

        // ===== KALIMDOR — HORDE =====
        [22] = new(22, "Thunder Bluff",       1, -1197.0f,  29.0f,  177.0f, Faction.Horde),
        [23] = new(23, "Orgrimmar",           1, 1677.0f,  -4315.0f,  62.0f, Faction.Horde),
        [25] = new(25, "Crossroads",          1, -437.0f,  -2596.0f,  96.0f, Faction.Horde),     // Barrens
        [29] = new(29, "Sun Rock Retreat",    1, 969.0f,    1008.0f, 104.0f, Faction.Horde),     // Stonetalon
        [30] = new(30, "Freewind Post",       1, -5407.0f, -2414.0f,  90.0f, Faction.Horde),     // Thousand Needles
        [34] = new(34, "Camp Taurajo",        1, -2372.0f, -1993.0f,  96.0f, Faction.Horde),     // Barrens
        [35] = new(35, "Brackenwall Village", 1, -3147.0f, -2841.0f,  34.0f, Faction.Horde),     // Dustwallow
        [37] = new(37, "Splintertree Post",   1, 2305.0f,  -2524.0f, 104.0f, Faction.Horde),     // Ashenvale
        [38] = new(38, "Shadowprey Village",  1, -1766.0f,  3262.0f,   5.0f, Faction.Horde),     // Desolace
        [40] = new(40, "Gadgetzan",           1, -7117.0f, -3828.0f,  10.0f, Faction.Neutral),   // Tanaris Horde entry
        [44] = new(44, "Valormok",            1, 3370.0f,  -4678.0f,  10.0f, Faction.Horde),     // Azshara
        [48] = new(48, "Bloodvenom Post",     1, 5064.0f,  -1261.0f, 368.0f, Faction.Horde),     // Felwood
        [53] = new(53, "Everlook",            1, 6801.0f,  -4611.0f, 711.0f, Faction.Neutral),   // Winterspring Horde
        [55] = new(55, "Moonglade",           1, 7466.0f,  -2122.0f, 492.0f, Faction.Neutral),   // Moonglade Horde
        [69] = new(69, "Cenarion Hold",       1, -6807.0f,  833.0f,   51.0f, Faction.Neutral),   // Silithus Horde
        [72] = new(72, "Zoram'gar Outpost",   1, 3382.0f,   1003.0f,   5.0f, Faction.Horde),     // Ashenvale
        [77] = new(77, "Camp Mojache",        1, -4413.0f,  203.0f,   25.0f, Faction.Horde),     // Feralas
        [80] = new(80, "Marshal's Refuge",    1, -6111.0f, -1143.0f,  -7.0f, Faction.Neutral),   // Un'Goro Horde
    };

    /// <summary>
    /// Find the taxi node nearest to a world position on the given map.
    /// Optionally filter by faction (Alliance, Horde, or Neutral).
    /// Only considers nodes the player has discovered (if discoveredNodeIds is provided).
    /// </summary>
    public static TaxiNodeInfo? FindNearestNode(
        int mapId, float x, float y, float z,
        Faction playerFaction,
        IReadOnlyCollection<uint>? discoveredNodeIds = null)
    {
        TaxiNodeInfo? best = null;
        float bestDist = float.MaxValue;

        foreach (var (nodeId, node) in Nodes)
        {
            if (node.MapId != mapId) continue;

            // Faction check: player can use own faction + neutral
            if (node.NodeFaction != Faction.Neutral && node.NodeFaction != playerFaction)
                continue;

            // If we have a discovered list, only consider discovered nodes
            if (discoveredNodeIds != null && !discoveredNodeIds.Contains(nodeId))
                continue;

            float dx = node.X - x;
            float dy = node.Y - y;
            float dz = node.Z - z;
            float dist = dx * dx + dy * dy + dz * dz; // squared distance is fine for comparison

            if (dist < bestDist)
            {
                bestDist = dist;
                best = node;
            }
        }

        return best;
    }

    /// <summary>
    /// Find the taxi node nearest to a target position on the given map.
    /// Used to determine which flight destination to pick when traveling to a zone.
    /// </summary>
    public static TaxiNodeInfo? FindNearestNodeToDestination(
        int mapId, float targetX, float targetY, float targetZ,
        Faction playerFaction,
        IReadOnlyCollection<uint> availableNodeIds)
    {
        TaxiNodeInfo? best = null;
        float bestDist = float.MaxValue;

        foreach (var nodeId in availableNodeIds)
        {
            if (!Nodes.TryGetValue(nodeId, out var node)) continue;
            if (node.MapId != mapId) continue;

            // Faction check
            if (node.NodeFaction != Faction.Neutral && node.NodeFaction != playerFaction)
                continue;

            float dx = node.X - targetX;
            float dy = node.Y - targetY;
            float dz = node.Z - targetZ;
            float dist = dx * dx + dy * dy + dz * dz;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = node;
            }
        }

        return best;
    }

    /// <summary>
    /// Get all nodes for a faction on a given map.
    /// </summary>
    public static IEnumerable<TaxiNodeInfo> GetNodesForFaction(int mapId, Faction playerFaction)
    {
        return Nodes.Values.Where(n =>
            n.MapId == mapId &&
            (n.NodeFaction == Faction.Neutral || n.NodeFaction == playerFaction));
    }

    /// <summary>
    /// Get the distance between two taxi nodes (straight line, 2D).
    /// </summary>
    public static float GetDistanceBetweenNodes(uint nodeA, uint nodeB)
    {
        if (!Nodes.TryGetValue(nodeA, out var a) || !Nodes.TryGetValue(nodeB, out var b))
            return float.MaxValue;

        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}
