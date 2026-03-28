using Database;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Travel
{
    public class LocationResolver
    {
        private readonly Dictionary<string, (uint MapId, Position Position)> _locations = new(StringComparer.OrdinalIgnoreCase);

        public LocationResolver()
        {
            LoadStaticLocations();
        }

        /// <summary>
        /// Resolve a friendly location name to map ID and position.
        /// Case-insensitive. Checks static data first, then DB-loaded data.
        /// </summary>
        public (uint MapId, Position Position)? Resolve(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName))
                return null;

            if (_locations.TryGetValue(locationName, out var result))
                return result;

            return null;
        }

        /// <summary>
        /// Find the nearest named location on the same map, optionally filtered by name substring.
        /// </summary>
        public (string Name, uint MapId, Position Position)? FindNearest(uint mapId, Position from, string? filter = null)
        {
            if (from == null)
                return null;

            string? bestName = null;
            float bestDist = float.MaxValue;
            (uint MapId, Position Position) bestEntry = default;

            foreach (var kvp in _locations)
            {
                if (kvp.Value.MapId != mapId)
                    continue;

                if (filter != null && kvp.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                float dist = from.DistanceTo(kvp.Value.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestName = kvp.Key;
                    bestEntry = kvp.Value;
                }
            }

            if (bestName == null)
                return null;

            return (bestName, bestEntry.MapId, bestEntry.Position);
        }

        /// <summary>
        /// Load named locations from MaNGOS game_tele table.
        /// Call with the result of MangosRepository.GetGameTeles().
        /// DB entries do NOT overwrite static entries (static data takes priority).
        /// </summary>
        public void LoadFromDatabase(List<GameTele> gameTeles)
        {
            if (gameTeles == null)
                return;

            foreach (var tele in gameTeles)
            {
                if (string.IsNullOrWhiteSpace(tele.Name))
                    continue;

                // Static entries take priority — only add if not already present
                _locations.TryAdd(tele.Name, ((uint)tele.Map, new Position(tele.PositionX, tele.PositionY, tele.PositionZ)));
            }
        }

        /// <summary>
        /// All known location names.
        /// </summary>
        public IReadOnlyCollection<string> KnownLocations => _locations.Keys;

        private void LoadStaticLocations()
        {
            // Horde capitals
            AddStatic("Orgrimmar",              1, 1676.0f, -4315.0f, 61.0f);
            AddStatic("Thunder Bluff",          1, -1278.0f, 127.0f, 131.0f);
            AddStatic("Undercity",              0, 1586.0f, 239.0f, -52.0f);

            // Alliance capitals
            AddStatic("Stormwind",              0, -8913.0f, 554.0f, 94.0f);
            AddStatic("Ironforge",              0, -4981.0f, -881.0f, 502.0f);
            AddStatic("Darnassus",              1, 9947.0f, 2482.0f, 1316.0f);

            // Neutral quest hubs
            AddStatic("Crossroads",             1, -442.0f, -2598.0f, 96.0f);
            AddStatic("Tarren Mill",            0, -7.0f, -920.0f, 55.0f);
            AddStatic("Southshore",             0, -808.0f, -547.0f, 7.0f);
            AddStatic("Ratchet",                1, -957.0f, -3754.0f, 5.0f);
            AddStatic("Booty Bay",              0, -14354.0f, 518.0f, 22.0f);
            AddStatic("Gadgetzan",              1, -7150.0f, -3789.0f, 8.0f);
            AddStatic("Cenarion Hold",          1, -6815.0f, 730.0f, 42.0f);
            AddStatic("Light's Hope Chapel",    0, 2280.0f, -5312.0f, 82.0f);
            AddStatic("Everlook",               1, 6723.0f, -4609.0f, 720.0f);
        }

        private void AddStatic(string name, uint mapId, float x, float y, float z)
        {
            _locations[name] = (mapId, new Position(x, y, z));
        }
    }
}
