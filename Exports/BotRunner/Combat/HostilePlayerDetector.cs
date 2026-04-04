using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Combat;

/// <summary>
/// Scans for enemy faction PvP-flagged players in range.
/// Evaluates threat level based on level difference and health.
/// Used by PvPEngagementTask to decide fight-or-flee.
/// </summary>
public class HostilePlayerDetector
{
    /// <summary>
    /// Detected hostile player with threat assessment.
    /// </summary>
    public record HostilePlayer(
        IWoWPlayer Player,
        float Distance,
        ThreatLevel Threat);

    public enum ThreatLevel { Low, Medium, High, Overwhelming }

    /// <summary>
    /// Scan for hostile PvP-flagged players near the local player.
    /// </summary>
    public static IReadOnlyList<HostilePlayer> Scan(
        IObjectManager objectManager,
        float scanRange = 40f)
    {
        var player = objectManager.Player;
        if (player == null) return [];

        var playerFaction = GetFactionSide(player.FactionTemplate);
        if (playerFaction == FactionSide.Unknown) return [];

        return objectManager.Players
            .Where(p => p.Guid != player.Guid
                && p.Health > 0
                && IsPvPFlagged(p)
                && IsEnemyFaction(p.FactionTemplate, playerFaction)
                && p.Position.DistanceTo(player.Position) <= scanRange)
            .Select(p => new HostilePlayer(
                p,
                p.Position.DistanceTo(player.Position),
                AssessThreat(player, p)))
            .OrderBy(h => h.Distance)
            .ToList();
    }

    /// <summary>Check if a unit has the PvP flag set.</summary>
    public static bool IsPvPFlagged(IWoWUnit unit)
        => unit.UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_PVP);

    /// <summary>Check if a unit is a civilian NPC (dishonorable kill target).</summary>
    public static bool IsCivilian(IWoWUnit unit)
        => unit.UnitFlags.HasFlag(UnitFlags.UNIT_FLAG_PASSIVE);

    private static ThreatLevel AssessThreat(IWoWLocalPlayer self, IWoWPlayer enemy)
    {
        var levelDiff = enemy.Level - self.Level;

        // 5+ levels above = overwhelming
        if (levelDiff >= 5) return ThreatLevel.Overwhelming;
        // 3-4 levels above = high
        if (levelDiff >= 3) return ThreatLevel.High;
        // Similar level — check health
        if (enemy.HealthPercent > 80) return ThreatLevel.Medium;
        // Lower level or hurt = low threat
        return ThreatLevel.Low;
    }

    private enum FactionSide { Unknown, Horde, Alliance }

    private static FactionSide GetFactionSide(uint factionTemplate)
    {
        // WoW 1.12.1 faction template ranges
        // Horde player factions: 2 (Orc), 5 (Undead), 6 (Tauren), 116 (Troll)
        // Alliance player factions: 1 (Human), 3 (Dwarf), 4 (Night Elf), 115 (Gnome)
        return factionTemplate switch
        {
            1 or 3 or 4 or 115 => FactionSide.Alliance,
            2 or 5 or 6 or 116 => FactionSide.Horde,
            _ => FactionSide.Unknown
        };
    }

    private static bool IsEnemyFaction(uint targetFaction, FactionSide selfFaction)
    {
        var targetSide = GetFactionSide(targetFaction);
        if (targetSide == FactionSide.Unknown) return false;
        return targetSide != selfFaction;
    }
}
