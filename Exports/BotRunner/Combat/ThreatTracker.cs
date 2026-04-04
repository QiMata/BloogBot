using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Combat;

/// <summary>
/// Estimates threat levels for raid encounters.
/// No server-side threat API in 1.12.1 — estimates from combat log events.
/// Tanks aim to maximize, DPS throttle if approaching tank's threat.
/// </summary>
public class ThreatTracker
{
    private readonly ConcurrentDictionary<ulong, float> _threatByGuid = new();
    private const float HEALING_THREAT_MULTIPLIER = 0.5f;
    private const float DAMAGE_THREAT_MULTIPLIER = 1.0f;
    private const float TANKING_STANCE_MULTIPLIER = 1.3f; // Defensive Stance

    /// <summary>Record damage dealt to accumulate threat.</summary>
    public void RecordDamage(ulong attackerGuid, float damage, bool isTank = false)
    {
        var multiplier = isTank ? TANKING_STANCE_MULTIPLIER * DAMAGE_THREAT_MULTIPLIER : DAMAGE_THREAT_MULTIPLIER;
        _threatByGuid.AddOrUpdate(attackerGuid,
            damage * multiplier,
            (_, existing) => existing + damage * multiplier);
    }

    /// <summary>Record healing done (generates threat split across all mobs).</summary>
    public void RecordHealing(ulong healerGuid, float healAmount)
    {
        _threatByGuid.AddOrUpdate(healerGuid,
            healAmount * HEALING_THREAT_MULTIPLIER,
            (_, existing) => existing + healAmount * HEALING_THREAT_MULTIPLIER);
    }

    /// <summary>Get estimated threat for a player.</summary>
    public float GetThreat(ulong playerGuid)
        => _threatByGuid.TryGetValue(playerGuid, out var threat) ? threat : 0f;

    /// <summary>Get the highest threat player (should be the tank).</summary>
    public (ulong Guid, float Threat)? GetHighestThreat()
    {
        if (_threatByGuid.IsEmpty) return null;
        var max = _threatByGuid.OrderByDescending(kv => kv.Value).First();
        return (max.Key, max.Value);
    }

    /// <summary>
    /// Check if a DPS player should throttle (within 90% of tank's threat).
    /// In WoW 1.12.1, melee pulls aggro at 110% of tank's threat.
    /// </summary>
    public bool ShouldThrottle(ulong dpsGuid, ulong tankGuid, float safetyMargin = 0.9f)
    {
        var tankThreat = GetThreat(tankGuid);
        var dpsThreat = GetThreat(dpsGuid);
        if (tankThreat <= 0) return false;
        return dpsThreat >= tankThreat * safetyMargin;
    }

    /// <summary>Get all threat entries sorted by threat descending.</summary>
    public IReadOnlyList<(ulong Guid, float Threat)> GetThreatTable()
        => _threatByGuid.OrderByDescending(kv => kv.Value)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

    /// <summary>Reset all threat (boss transition, wipe, etc).</summary>
    public void Reset() => _threatByGuid.Clear();
}
