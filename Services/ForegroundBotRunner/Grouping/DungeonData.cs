using GameData.Core.Models;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForegroundBotRunner.Grouping
{
    /// <summary>
    /// Data model for a dungeon instance. Loaded from JSON config files.
    /// Contains route waypoints, boss positions, and encounter metadata.
    /// </summary>
    public class DungeonData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("mapId")]
        public uint MapId { get; set; }

        [JsonPropertyName("minLevel")]
        public int MinLevel { get; set; }

        [JsonPropertyName("maxLevel")]
        public int MaxLevel { get; set; }

        [JsonPropertyName("entrance")]
        public DungeonPosition? Entrance { get; set; }

        [JsonPropertyName("route")]
        public DungeonWaypoint[] Route { get; set; } = [];

        [JsonPropertyName("bosses")]
        public BossEncounter[] Bosses { get; set; } = [];
    }

    public class DungeonPosition
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }

        public Position ToPosition() => new(X, Y, Z);
    }

    public class DungeonWaypoint
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; } = "move"; // "move", "clear", "wait", "boss"

        [JsonPropertyName("bossName")]
        public string? BossName { get; set; }

        public Position ToPosition() => new(X, Y, Z);
    }

    public class BossEncounter
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("creatureId")]
        public uint CreatureId { get; set; }

        [JsonPropertyName("position")]
        public DungeonPosition? Position { get; set; }

        [JsonPropertyName("tankPosition")]
        public DungeonPosition? TankPosition { get; set; }

        [JsonPropertyName("rangedPosition")]
        public DungeonPosition? RangedPosition { get; set; }
    }

    /// <summary>
    /// Registry of all known dungeon configs. Loads from JSON files in Documents/BloogBot/Dungeons/.
    /// </summary>
    public static class DungeonRegistry
    {
        private static readonly Dictionary<uint, DungeonData> _dungeons = new();
        private static bool _loaded;

        /// <summary>
        /// Get dungeon data by map ID. Returns null if no config found.
        /// </summary>
        public static DungeonData? GetByMapId(uint mapId)
        {
            EnsureLoaded();
            return _dungeons.GetValueOrDefault(mapId);
        }

        /// <summary>
        /// Check if a map ID is a known dungeon.
        /// </summary>
        public static bool IsDungeon(uint mapId)
        {
            EnsureLoaded();
            return _dungeons.ContainsKey(mapId);
        }

        /// <summary>
        /// Returns all loaded dungeon configs.
        /// </summary>
        public static IReadOnlyCollection<DungeonData> All
        {
            get
            {
                EnsureLoaded();
                return _dungeons.Values;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            var searchPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BloogBot", "Dungeons"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dungeons"),
                Path.Combine(Directory.GetCurrentDirectory(), "Dungeons")
            };

            foreach (var dir in searchPaths)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var dungeon = JsonSerializer.Deserialize<DungeonData>(json);
                        if (dungeon != null && dungeon.MapId > 0)
                        {
                            _dungeons[dungeon.MapId] = dungeon;
                            Log.Information("[DungeonRegistry] Loaded {Name} (mapId={MapId}, {RouteCount} waypoints, {BossCount} bosses)",
                                dungeon.Name, dungeon.MapId, dungeon.Route.Length, dungeon.Bosses.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[DungeonRegistry] Failed to load {File}", file);
                    }
                }
            }

            Log.Information("[DungeonRegistry] Loaded {Count} dungeon configs from {Paths}",
                _dungeons.Count, string.Join(", ", searchPaths.Where(Directory.Exists)));
        }
    }
}
