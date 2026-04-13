using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using WoWStateManagerUI.Models;

namespace WoWStateManagerUI.Services
{
    public static class SettingsFileService
    {
        public static List<CharacterSettingsModel> LoadSettings(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Settings file not found: {filePath}");

            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<CharacterSettingsModel>>(json) ?? [];
        }

        public static void SaveSettings(string filePath, IEnumerable<CharacterSettingsModel> characters)
        {
            var json = JsonConvert.SerializeObject(characters, Formatting.Indented);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Lists all .json files in a directory that look like settings profiles.
        /// </summary>
        public static string[] FindProfiles(string directory)
        {
            if (!Directory.Exists(directory))
                return [];
            return Directory.GetFiles(directory, "*.json");
        }
    }
}
