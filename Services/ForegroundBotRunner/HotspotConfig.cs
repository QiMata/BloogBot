using GameData.Core.Models;
using Serilog;
using System.Text.Json;

namespace ForegroundBotRunner
{
    /// <summary>
    /// Loads grind hotspot positions from a JSON config file.
    /// The bot patrols between these positions, killing mobs along the way.
    ///
    /// File format (grind_hotspots.json):
    /// { "hotspots": [ { "x": 1234.5, "y": 678.9, "z": 100.0 }, ... ] }
    ///
    /// Search order:
    /// 1. Documents/BloogBot/grind_hotspots.json
    /// 2. WoW directory/grind_hotspots.json
    /// </summary>
    public static class HotspotConfig
    {
        private const string FILENAME = "grind_hotspots.json";

        public static Position[] Load()
        {
            var paths = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BloogBot", FILENAME),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FILENAME),
                Path.Combine(Directory.GetCurrentDirectory(), FILENAME)
            };

            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    var json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<HotspotFile>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (config?.Hotspots == null || config.Hotspots.Length == 0)
                    {
                        Log.Warning("[HotspotConfig] File {Path} has no hotspots", path);
                        continue;
                    }

                    var positions = config.Hotspots
                        .Select(h => new Position(h.X, h.Y, h.Z))
                        .ToArray();

                    Log.Information("[HotspotConfig] Loaded {Count} hotspots from {Path}", positions.Length, path);
                    return positions;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[HotspotConfig] Failed to load {Path}", path);
                }
            }

            Log.Debug("[HotspotConfig] No hotspot file found, bot will grind in place");
            return [];
        }

        private class HotspotFile
        {
            public HotspotEntry[] Hotspots { get; set; } = [];
        }

        private class HotspotEntry
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
        }
    }
}
