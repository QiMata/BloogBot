using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Combat;

/// <summary>
/// Simulates loot council /roll for contested items.
/// Tracks CMSG_LOOT_ROLL submissions and SMSG_LOOT_ROLL results.
/// Highest roll wins. Used when Master Loot is active and multiple
/// players want the same item.
/// </summary>
public class LootCouncilSimulator
{
    public record RollEntry(ulong PlayerGuid, string PlayerName, int RollValue, RollType Type, DateTime Timestamp);

    public enum RollType { MainSpec, OffSpec, Greed, Pass }

    private readonly ConcurrentDictionary<uint, List<RollEntry>> _rollsByItem = new();
    private readonly Random _rng = new();

    /// <summary>Record a roll for an item.</summary>
    public void RecordRoll(uint itemId, ulong playerGuid, string playerName, RollType type)
    {
        var rollValue = type == RollType.Pass ? 0 : _rng.Next(1, 101);
        var entry = new RollEntry(playerGuid, playerName, rollValue, type, DateTime.UtcNow);

        _rollsByItem.AddOrUpdate(itemId,
            [entry],
            (_, list) => { list.Add(entry); return list; });

        Log.Information("[LOOT] {Player} rolled {Roll} ({Type}) for item {ItemId}",
            playerName, rollValue, type, itemId);
    }

    /// <summary>Determine the winner for an item. MainSpec > OffSpec > Greed. Highest roll wins within tier.</summary>
    public RollEntry? GetWinner(uint itemId)
    {
        if (!_rollsByItem.TryGetValue(itemId, out var rolls) || rolls.Count == 0)
            return null;

        // Filter out passes
        var validRolls = rolls.Where(r => r.Type != RollType.Pass).ToList();
        if (validRolls.Count == 0) return null;

        // Priority: MainSpec > OffSpec > Greed, then highest roll
        return validRolls
            .OrderBy(r => r.Type) // MainSpec=0, OffSpec=1, Greed=2
            .ThenByDescending(r => r.RollValue)
            .First();
    }

    /// <summary>Check if all expected players have rolled for an item.</summary>
    public bool AllRollsIn(uint itemId, int expectedRollers)
    {
        if (!_rollsByItem.TryGetValue(itemId, out var rolls))
            return false;
        return rolls.Count >= expectedRollers;
    }

    /// <summary>Get all rolls for an item.</summary>
    public IReadOnlyList<RollEntry> GetRolls(uint itemId)
    {
        return _rollsByItem.TryGetValue(itemId, out var rolls) ? rolls : [];
    }

    /// <summary>Clear rolls for an item after distribution.</summary>
    public void ClearItem(uint itemId)
    {
        _rollsByItem.TryRemove(itemId, out _);
    }

    /// <summary>Clear all roll tracking.</summary>
    public void Reset() => _rollsByItem.Clear();
}
