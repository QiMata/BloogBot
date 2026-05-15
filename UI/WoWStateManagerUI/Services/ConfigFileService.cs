using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WoWStateManagerUI.Models;

namespace WoWStateManagerUI.Services
{
    /// <summary>
    /// Reads/writes <see cref="ConfigModel"/> JSON. The on-disk schema is the
    /// new hierarchical shape:
    /// <code>
    ///   { "Name": "...", "Activities": [{ "ActivityId": "...", "Characters": [...] }] }
    /// </code>
    /// Legacy flat-list files (a bare JSON array of CharacterSettings) are
    /// auto-migrated on load into a single Activity named "Legacy" so existing
    /// settings folders keep opening without a manual step.
    /// </summary>
    public static class ConfigFileService
    {
        private const string LegacyActivityId = "legacy.imported";
        private const string LegacyActivityDisplayName = "Legacy import";

        public static string[] FindConfigs(string directory)
        {
            if (!Directory.Exists(directory))
                return [];
            return Directory.GetFiles(directory, "*.json");
        }

        public static ConfigModel Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Config file not found: {filePath}");

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new ConfigModel { Name = Path.GetFileNameWithoutExtension(filePath) };

            var token = JToken.Parse(json);

            // Legacy: top-level array of characters → wrap in a single Activity.
            if (token.Type == JTokenType.Array)
                return MigrateLegacy(filePath, token);

            // New schema: object with Activities[]
            var config = token.ToObject<ConfigModel>()
                ?? throw new InvalidDataException($"Empty config payload in {filePath}");
            if (string.IsNullOrWhiteSpace(config.Name))
                config.Name = Path.GetFileNameWithoutExtension(filePath);
            return config;
        }

        public static void Save(string filePath, ConfigModel config)
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, json);
        }

        private static ConfigModel MigrateLegacy(string filePath, JToken arrayToken)
        {
            var characters = arrayToken.ToObject<List<CharacterSettingsModel>>() ?? [];

            var activity = new ActivityModel
            {
                ActivityId = LegacyActivityId,
                DisplayName = LegacyActivityDisplayName,
                Family = "Imported",
                Location = "Unknown",
                MinPlayers = 1,
                MaxPlayers = Math.Max(characters.Count, 1),
            };
            foreach (var c in characters)
                activity.Characters.Add(c);

            var config = new ConfigModel
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Description = "Migrated from legacy flat-list settings file",
            };
            config.Activities.Add(activity);
            return config;
        }
    }
}
