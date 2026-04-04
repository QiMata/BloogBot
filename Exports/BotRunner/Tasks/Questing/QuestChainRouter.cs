using BotRunner.Progression;
using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;
using static BotRunner.Progression.QuestChainData;

namespace BotRunner.Tasks.Questing;

/// <summary>
/// Routes bots through quest chains by determining the next quest step
/// based on current quest log state. Resolves quest giver NPC positions
/// for travel planning.
/// </summary>
public class QuestChainRouter
{
    /// <summary>
    /// Given a chain ID and current completed quest IDs, find the next quest to accept.
    /// </summary>
    public static QuestStep? GetNextStep(string chainId, IReadOnlySet<uint> completedQuestIds)
    {
        var chain = QuestChainData.GetChain(chainId);
        if (chain == null) return null;

        foreach (var step in chain.Steps)
        {
            if (!completedQuestIds.Contains(step.QuestId))
                return step;
        }

        return null; // Chain complete
    }

    /// <summary>
    /// Find all active quest chains that have remaining steps.
    /// Returns the next step for each chain, sorted by priority.
    /// </summary>
    public static IReadOnlyList<(string ChainId, QuestStep NextStep)> GetActiveChainSteps(
        IReadOnlyList<string> chainIds,
        IReadOnlySet<uint> completedQuestIds)
    {
        return chainIds
            .Select(id => (ChainId: id, NextStep: GetNextStep(id, completedQuestIds)))
            .Where(x => x.NextStep != null)
            .Select(x => (x.ChainId, x.NextStep!))
            .ToList();
    }

    /// <summary>
    /// Get the nearest quest giver position for any active chain step.
    /// Used by TravelTask to navigate the bot to the right NPC.
    /// </summary>
    public static (string ChainId, QuestStep Step, float Distance)? GetNearestQuestGiver(
        IReadOnlyList<string> chainIds,
        IReadOnlySet<uint> completedQuestIds,
        Position playerPosition)
    {
        var activeSteps = GetActiveChainSteps(chainIds, completedQuestIds);
        if (activeSteps.Count == 0) return null;

        return activeSteps
            .Select(x => (x.ChainId, x.NextStep, Distance: new Position(x.NextStep.X, x.NextStep.Y, x.NextStep.Z).DistanceTo(playerPosition)))
            .OrderBy(x => x.Distance)
            .First();
    }
}
