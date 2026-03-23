using GameData.Core.Frames;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ForegroundBotRunner.Frames;

public sealed class FgTrainerFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult) : ITrainerFrame
{
    private const string TrainerVisibleLua =
        "local n = GetNumTrainerServices(); " +
        "if (ClassTrainerFrame and ClassTrainerFrame:IsVisible()) or (TrainerFrame and TrainerFrame:IsVisible()) or (n and n > 0) then {0} = 1 else {0} = 0 end";
    private const string TrainerCountLua =
        "local n = GetNumTrainerServices(); if n then {0} = n else {0} = 0 end";

    private static readonly FieldInfo? NameField =
        typeof(TrainerSpellItem).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? RankField =
        typeof(TrainerSpellItem).GetField("<Rank>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? IndexField =
        typeof(TrainerSpellItem).GetField("<Index>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CanLearnField =
        typeof(TrainerSpellItem).GetField("<CanLearn>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CostField =
        typeof(TrainerSpellItem).GetField("<Cost>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    public bool IsOpen => FrameLuaReader.ReadBool(luaCallWithResult, TrainerVisibleLua);

    public void Close() => luaCall("CloseTrainer()");

    public IEnumerable<TrainerSpellItem> Spells
        => Enumerable.Range(1, FrameLuaReader.ReadInt(luaCallWithResult, TrainerCountLua))
            .Select(CreateSpell)
            .ToArray();

    public void TrainSpell(int spellIndex)
    {
        int trainerIndex = Math.Max(0, spellIndex) + 1;
        luaCall($"if GetNumTrainerServices() and GetNumTrainerServices() >= {trainerIndex} then BuyTrainerService({trainerIndex}) end");
    }

    public void Update()
    {
    }

    private TrainerSpellItem CreateSpell(int trainerIndex)
    {
        string[] result = luaCallWithResult(
            $"{{0}}, {{1}}, {{2}} = GetTrainerServiceInfo({trainerIndex}); {{3}} = GetTrainerServiceCost({trainerIndex}) or 0");

        string name = result.Length > 0 ? result[0] : string.Empty;
        string rankText = result.Length > 1 ? result[1] : string.Empty;
        string availability = result.Length > 2 ? result[2] : string.Empty;
        int cost = result.Length > 3 && int.TryParse(result[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCost)
            ? parsedCost
            : 0;

        return new FgTrainerSpell(
            name,
            ParseRank(rankText),
            trainerIndex - 1,
            string.Equals(availability, "available", StringComparison.OrdinalIgnoreCase),
            cost);
    }

    private static int ParseRank(string rankText)
    {
        if (string.IsNullOrWhiteSpace(rankText))
            return 0;

        string digits = new string(rankText.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rank)
            ? rank
            : 0;
    }

    private sealed class FgTrainerSpell : TrainerSpellItem
    {
        internal FgTrainerSpell(string name, int rank, int index, bool canLearn, int cost)
        {
            NameField?.SetValue(this, name);
            RankField?.SetValue(this, rank);
            IndexField?.SetValue(this, index);
            CanLearnField?.SetValue(this, canLearn);
            CostField?.SetValue(this, cost);
        }
    }
}
