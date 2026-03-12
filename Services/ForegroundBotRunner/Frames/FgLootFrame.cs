using GameData.Core.Frames;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace ForegroundBotRunner.Frames;

/// <summary>
/// Minimal FG loot-frame surface backed by Lua. This gives task-owned flows a
/// usable loot window on the injected client without relying on packet agents.
/// </summary>
public sealed class FgLootFrame(Action<string> luaCall, Func<string, string[]> luaCallWithResult) : ILootFrame
{
    public bool IsOpen => ReadInt("if LootFrame and LootFrame:IsVisible() then {0} = 1 else {0} = 0 end") == 1;

    public void Close() => luaCall("if LootFrame and LootFrame:IsVisible() then CloseLoot() end");

    public IEnumerable<LootItem> LootItems => Array.Empty<LootItem>();

    public int LootCount => ReadInt("if LootFrame and LootFrame:IsVisible() then {0} = GetNumLootItems() or 0 else {0} = 0 end");

    public ulong LootGuid => 0UL;

    public int Coins => 0;

    public ConcurrentDictionary<int, int> MissingIds { get; } = new();

    public void ItemCallback(int parItemId)
    {
    }

    public void LootSlot(int parSlotIndex)
        => luaCall($"if LootFrame and LootFrame:IsVisible() then LootSlot({parSlotIndex + 1}) end");

    public void LootAll()
        => luaCall("if LootFrame and LootFrame:IsVisible() then for i=1,GetNumLootItems() do LootSlot(i) end end");

    private int ReadInt(string lua)
    {
        var results = luaCallWithResult(lua);
        if (results.Length == 0)
            return 0;

        return int.TryParse(results[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}
