using GameData.Core.Frames;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ForegroundBotRunner.Frames;

public sealed class FgTalentFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult,
    Func<int, string?> spellNameProvider,
    Func<string, IReadOnlyList<uint>> spellIdsByNameProvider) : ITalentFrame
{
    private const string TalentVisibleLua =
        "if TalentFrame and TalentFrame:IsVisible() then {0} = 1 else {0} = 0 end";
    private const string TalentTabCountLua =
        "if TalentFrame and TalentFrame:IsVisible() then {0} = GetNumTalentTabs() or 0 else {0} = 0 end";
    private const string PlayerLevelLua =
        "{0} = UnitLevel('player') or 0";

    private static readonly FieldInfo? TalentTabNameField =
        typeof(TalentTab).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? TalentEntryNameField =
        typeof(TalentEntry).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? TalentEntryRanksField =
        typeof(TalentEntry).GetField("<Ranks>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? TalentEntryTotalRanksAvailableField =
        typeof(TalentEntry).GetField("<TotalRanksAvailable>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? TalentEntryNextRankSpellIdField =
        typeof(TalentEntry).GetField("<NextRankSpellId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    public bool IsOpen => FrameLuaReader.ReadBool(luaCallWithResult, TalentVisibleLua);

    public void Close() => luaCall("if TalentFrame and TalentFrame:IsVisible() then HideUIPanel(TalentFrame) end");

    public IEnumerable<TalentTab> Tabs
        => Enumerable.Range(1, FrameLuaReader.ReadInt(luaCallWithResult, TalentTabCountLua))
            .Select(CreateTab)
            .ToArray();

    public int TalentPointsAll => Math.Max(TalentPointsSpent, Math.Max(0, FrameLuaReader.ReadInt(luaCallWithResult, PlayerLevelLua) - 9));

    public int TalentPointsAvailable => Math.Max(0, TalentPointsAll - TalentPointsSpent);

    public int TalentPointsSpent
        => Enumerable.Range(1, FrameLuaReader.ReadInt(luaCallWithResult, TalentTabCountLua))
            .Sum(GetPointsSpentForTab);

    public void LearnTalent(int spellId)
    {
        if (!IsOpen)
            return;

        string? spellName = spellNameProvider(spellId);
        if (string.IsNullOrWhiteSpace(spellName))
            return;

        int tabCount = FrameLuaReader.ReadInt(luaCallWithResult, TalentTabCountLua);
        for (int tabIndex = 1; tabIndex <= tabCount; tabIndex++)
        {
            int talentCount = GetTalentCount(tabIndex);
            for (int talentIndex = 1; talentIndex <= talentCount; talentIndex++)
            {
                string[] result = ReadTalentInfo(tabIndex, talentIndex);
                string candidateName = result.Length > 0 ? result[0] : string.Empty;
                int currentRank = ParseInt(result, 4);
                int maxRank = ParseInt(result, 5);

                if (!string.Equals(candidateName, spellName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (currentRank >= maxRank)
                    return;

                luaCall($"if TalentFrame and TalentFrame:IsVisible() then LearnTalent({tabIndex}, {talentIndex}) end");
                return;
            }
        }
    }

    private TalentTab CreateTab(int tabIndex)
    {
        string[] tabInfo = luaCallWithResult($"{{0}}, {{1}}, {{2}}, {{3}} = GetTalentTabInfo({tabIndex})");
        string name = tabInfo.Length > 0 ? tabInfo[0] : string.Empty;
        int talentCount = GetTalentCount(tabIndex);

        var tab = new FgTalentTab(name);
        for (int rowIndex = 0; rowIndex < tab.Rows.Length; rowIndex++)
            tab.Rows[rowIndex] = new TalentRow();

        for (int talentIndex = 1; talentIndex <= talentCount; talentIndex++)
        {
            string[] talentInfo = ReadTalentInfo(tabIndex, talentIndex);
            string talentName = talentInfo.Length > 0 ? talentInfo[0] : string.Empty;
            int row = Math.Clamp(ParseInt(talentInfo, 2), 1, tab.Rows.Length) - 1;
            int column = Math.Clamp(ParseInt(talentInfo, 3), 1, tab.Rows[row].Talents.Length) - 1;
            int currentRank = ParseInt(talentInfo, 4);
            int maxRank = ParseInt(talentInfo, 5);
            int nextRankSpellId = ResolveNextRankSpellId(talentName, currentRank);

            tab.Rows[row].Talents[column] = new FgTalentEntry(talentName, currentRank, maxRank, nextRankSpellId);
        }

        return tab;
    }

    private int GetPointsSpentForTab(int tabIndex)
    {
        string[] result = luaCallWithResult($"{{0}}, {{1}}, {{2}}, {{3}} = GetTalentTabInfo({tabIndex})");
        return ParseInt(result, 2);
    }

    private int GetTalentCount(int tabIndex)
    {
        string[] result = luaCallWithResult($"{{0}} = GetNumTalents({tabIndex}) or 0");
        return ParseInt(result, 0);
    }

    private string[] ReadTalentInfo(int tabIndex, int talentIndex)
        => luaCallWithResult($"{{0}}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}} = GetTalentInfo({tabIndex}, {talentIndex})");

    private int ResolveNextRankSpellId(string talentName, int currentRank)
    {
        IReadOnlyList<uint> spellIds = spellIdsByNameProvider(talentName);
        if (spellIds.Count == 0 || currentRank < 0 || currentRank >= spellIds.Count)
            return 0;

        return unchecked((int)spellIds[currentRank]);
    }

    private static int ParseInt(string[] values, int index)
    {
        if (index < 0 || index >= values.Length)
            return 0;

        return int.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
    }

    private sealed class FgTalentTab : TalentTab
    {
        internal FgTalentTab(string name)
        {
            TalentTabNameField?.SetValue(this, name);
        }
    }

    private sealed class FgTalentEntry : TalentEntry
    {
        internal FgTalentEntry(string name, int ranks, int totalRanksAvailable, int nextRankSpellId)
        {
            TalentEntryNameField?.SetValue(this, name);
            TalentEntryRanksField?.SetValue(this, ranks);
            TalentEntryTotalRanksAvailableField?.SetValue(this, totalRanksAvailable);
            TalentEntryNextRankSpellIdField?.SetValue(this, nextRankSpellId);
        }
    }
}
