using System.Collections.Generic;
using System.Linq;
using Database;
using GameData.Core.Interfaces;

namespace BotRunner.Progression;

/// <summary>
/// Evaluates the gap between currently equipped gear and a target gear set,
/// and compares items using stat-weighted scoring.
/// </summary>
public class GearEvaluationService
{
    /// <summary>
    /// Compares the character's currently equipped items against a list of gear goals
    /// and returns ordered gaps (missing or non-matching slots).
    /// </summary>
    /// <param name="objectManager">The object manager providing access to equipped items.</param>
    /// <param name="targetGearSet">The desired gear goals for each slot.</param>
    /// <param name="weights">Stat weight profile for the character's spec (unused for gap detection, available for scoring).</param>
    /// <returns>A list of gear gaps ordered by priority (1=immediate first).</returns>
    public List<GearGap> EvaluateGaps(
        IObjectManager objectManager,
        List<GearGoal> targetGearSet,
        StatWeightProfile weights)
    {
        var gaps = new List<GearGap>();

        foreach (var goal in targetGearSet)
        {
            var equipped = objectManager.GetEquippedItem(goal.Slot);

            // If nothing is equipped or the equipped item doesn't match the target
            if (equipped == null || equipped.ItemId != (uint)goal.TargetItemId)
            {
                gaps.Add(new GearGap(
                    goal.Slot,
                    equipped != null ? (int)equipped.ItemId : 0,
                    equipped?.Name ?? "(empty)",
                    goal.TargetItemId,
                    goal.ItemName,
                    goal.Source,
                    goal.Priority));
            }
        }

        return gaps.OrderBy(g => g.Priority).ToList();
    }

    /// <summary>
    /// Determines whether a candidate item is a stat-weighted upgrade over the current item
    /// for the given spec.
    /// </summary>
    /// <param name="candidate">The potential replacement item template.</param>
    /// <param name="current">The currently equipped item template (null treated as score 0).</param>
    /// <param name="weights">Stat weight profile for the character's spec.</param>
    /// <returns>True if the candidate scores higher than the current item.</returns>
    public bool IsUpgrade(ItemTemplate candidate, ItemTemplate? current, StatWeightProfile weights)
    {
        return ItemScorer.ScoreItem(candidate, weights) > ItemScorer.ScoreItem(current, weights);
    }

    /// <summary>
    /// Scores a single item against the given spec weights. Convenience wrapper around
    /// <see cref="ItemScorer.ScoreItem"/>.
    /// </summary>
    public float ScoreItem(ItemTemplate item, StatWeightProfile weights)
    {
        return ItemScorer.ScoreItem(item, weights);
    }
}
