using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Combat;

/// <summary>
/// Coordinates raid-wide cooldowns to avoid overlap.
/// Tracks: Innervate, Power Infusion, Tranquility, Divine Shield, etc.
/// Sequences usage so multiple healers don't pop mana CDs simultaneously.
/// </summary>
public class RaidCooldownCoordinator
{
    public record CooldownEntry(string SpellName, ulong OwnerGuid, DateTime UsedAt, float DurationSec, float CooldownSec);

    private readonly ConcurrentDictionary<string, List<CooldownEntry>> _usageLog = new();

    /// <summary>Record a cooldown usage.</summary>
    public void RecordUsage(string spellName, ulong ownerGuid, float durationSec, float cooldownSec)
    {
        var entry = new CooldownEntry(spellName, ownerGuid, DateTime.UtcNow, durationSec, cooldownSec);
        _usageLog.AddOrUpdate(spellName,
            [entry],
            (_, list) => { list.Add(entry); return list; });
    }

    /// <summary>Check if a cooldown is safe to use (no overlap with same type).</summary>
    public bool IsSafeToUse(string spellName, float minGapSec = 5f)
    {
        if (!_usageLog.TryGetValue(spellName, out var usages))
            return true;

        var lastUsage = usages.MaxBy(u => u.UsedAt);
        if (lastUsage == null) return true;

        var timeSince = (DateTime.UtcNow - lastUsage.UsedAt).TotalSeconds;
        return timeSince >= lastUsage.DurationSec + minGapSec;
    }

    /// <summary>Get the next available owner for a specific cooldown type.</summary>
    public ulong? GetNextAvailableOwner(string spellName, IReadOnlyList<ulong> owners)
    {
        if (!_usageLog.TryGetValue(spellName, out var usages))
            return owners.FirstOrDefault();

        // Find owner who hasn't used this CD recently (or used it longest ago)
        return owners
            .OrderBy(guid =>
            {
                var lastUse = usages.Where(u => u.OwnerGuid == guid).MaxBy(u => u.UsedAt);
                if (lastUse == null) return DateTime.MinValue;
                return lastUse.UsedAt;
            })
            .FirstOrDefault();
    }

    /// <summary>Known raid cooldowns for vanilla 1.12.1.</summary>
    public static readonly Dictionary<string, (float Duration, float Cooldown)> KnownCooldowns = new()
    {
        ["Innervate"] = (20f, 360f),
        ["Power Infusion"] = (15f, 180f),
        ["Tranquility"] = (10f, 300f),
        ["Divine Shield"] = (12f, 300f),
        ["Lay on Hands"] = (0f, 3600f),
        ["Rebirth"] = (0f, 1800f),
        ["Soulstone Resurrection"] = (0f, 1800f),
    };
}
