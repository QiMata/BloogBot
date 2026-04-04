using GameData.Core.Models;
using Serilog;
using System.Collections.Generic;

namespace BotRunner.Tasks.Crafting;

/// <summary>
/// Schedules trainer visits when profession skill reaches tier thresholds.
/// Tiers: Apprentice (1-75), Journeyman (75-150), Expert (150-225), Artisan (225-300).
/// When a skill hits a threshold, triggers navigation to the appropriate trainer.
/// </summary>
public static class ProfessionTrainerScheduler
{
    public record TrainerTier(int Threshold, string TierName, int RequiredLevel);

    public static readonly List<TrainerTier> Tiers =
    [
        new(75, "Journeyman", 10),
        new(150, "Expert", 20),
        new(225, "Artisan", 35),
    ];

    public record ProfessionTrainerInfo(string ProfessionName, uint TrainerEntry, Position Position, uint MapId, string Faction);

    // Orgrimmar profession trainers
    public static readonly List<ProfessionTrainerInfo> HordeTrainers =
    [
        new("Mining", 3357, new(1839f, -4539f, 29f), 1, "Horde"),
        new("Herbalism", 3404, new(1850f, -4430f, 27f), 1, "Horde"),
        new("Skinning", 7089, new(1852f, -4562f, 24f), 1, "Horde"),
        new("Blacksmithing", 3355, new(1846f, -4560f, 24f), 1, "Horde"),
        new("Leatherworking", 7088, new(1852f, -4562f, 24f), 1, "Horde"),
        new("Tailoring", 3363, new(1802f, -4569f, 24f), 1, "Horde"),
        new("Alchemy", 3347, new(1610f, -4392f, 13f), 1, "Horde"),
        new("Enchanting", 3345, new(1917f, -4434f, 25f), 1, "Horde"),
        new("Engineering", 3494, new(1679f, -4455f, 28f), 1, "Horde"),
        new("Cooking", 3399, new(1766f, -4267f, 8f), 1, "Horde"),
        new("First Aid", 3373, new(1555f, -4184f, 45f), 1, "Horde"),
        new("Fishing", 3332, new(-952f, -3775f, 6f), 1, "Horde"), // Ratchet
    ];

    // Stormwind profession trainers
    public static readonly List<ProfessionTrainerInfo> AllianceTrainers =
    [
        new("Mining", 5392, new(-8434f, 609f, 95f), 0, "Alliance"),
        new("Herbalism", 5566, new(-8771f, 788f, 97f), 0, "Alliance"),
        new("Skinning", 7087, new(-8757f, 695f, 97f), 0, "Alliance"),
        new("Blacksmithing", 5164, new(-8424f, 613f, 95f), 0, "Alliance"),
        new("Leatherworking", 7086, new(-8757f, 695f, 97f), 0, "Alliance"),
        new("Tailoring", 1346, new(-8938f, 800f, 99f), 0, "Alliance"),
        new("Alchemy", 1215, new(-8820f, 593f, 94f), 0, "Alliance"),
        new("Enchanting", 1317, new(-8855f, 803f, 99f), 0, "Alliance"),
        new("Engineering", 5174, new(-8347f, 641f, 95f), 0, "Alliance"),
        new("Cooking", 1355, new(-8560f, 826f, 107f), 0, "Alliance"),
        new("First Aid", 5150, new(-8765f, 768f, 97f), 0, "Alliance"),
        new("Fishing", 5493, new(-9428f, -72f, 56f), 0, "Alliance"), // Goldshire
    ];

    /// <summary>
    /// Check if a profession needs a trainer visit based on current skill level.
    /// Returns the tier to train, or null if no training needed.
    /// </summary>
    public static TrainerTier? NeedsTraining(int currentSkill, int maxSkill, int characterLevel)
    {
        foreach (var tier in Tiers)
        {
            // Skill is near the current cap and below the tier threshold
            if (currentSkill >= maxSkill - 5 && maxSkill < tier.Threshold + 75
                && characterLevel >= tier.RequiredLevel && currentSkill < tier.Threshold + 75)
            {
                return tier;
            }
        }
        return null;
    }

    /// <summary>
    /// Get the trainer for a specific profession and faction.
    /// </summary>
    public static ProfessionTrainerInfo? GetTrainer(string professionName, bool isHorde)
    {
        var trainers = isHorde ? HordeTrainers : AllianceTrainers;
        return trainers.Find(t => t.ProfessionName == professionName);
    }
}
