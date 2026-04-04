using GameData.Core.Enums;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BotRunner.Progression;

/// <summary>
/// Loads pre-built BiS gear sets from Config/CharacterTemplates/ JSON files.
/// Each template's TargetGearSet field contains the BiS list for that spec.
/// </summary>
public static class PreRaidBisSets
{
    /// <summary>
    /// Load a BiS gear set from a character template JSON file.
    /// Returns the TargetGearSet entries as GearGoal records.
    /// </summary>
    public static List<GearGoal> LoadFromTemplate(string templatePath)
    {
        if (!File.Exists(templatePath))
            return [];

        var json = File.ReadAllText(templatePath);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("TargetGearSet", out var gearArray))
            return [];

        var goals = new List<GearGoal>();
        foreach (var item in gearArray.EnumerateArray())
        {
            var slotStr = item.GetProperty("Slot").GetString() ?? "";
            if (!System.Enum.TryParse<EquipSlot>(slotStr, true, out var slot))
                continue;

            goals.Add(new GearGoal(
                slot,
                item.GetProperty("ItemId").GetInt32(),
                item.GetProperty("ItemName").GetString() ?? "",
                item.GetProperty("Source").GetString() ?? "",
                item.TryGetProperty("Priority", out var prio) ? prio.GetInt32() : 2));
        }

        return goals.OrderBy(g => g.Priority).ToList();
    }

    /// <summary>
    /// Discover all template files in the Config/CharacterTemplates directory.
    /// </summary>
    public static Dictionary<string, string> DiscoverTemplates(string? baseDir = null)
    {
        var dir = baseDir ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Config", "CharacterTemplates");
        if (!Directory.Exists(dir))
            return new();

        return Directory.GetFiles(dir, "*.json")
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                path => path);
    }
}
