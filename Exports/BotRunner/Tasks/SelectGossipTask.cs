using BotRunner.Interfaces;
using Microsoft.Extensions.Logging;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: select a gossip option by index on an open NPC dialog.
/// Requires that an NPC gossip dialog is already open.
/// Maps to ActionType.SELECT_GOSSIP from StateManager.
/// </summary>
public class SelectGossipTask(IBotContext botContext, int optionIndex) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        var gossipFrame = ObjectManager.GossipFrame;
        if (gossipFrame == null)
        {
            Logger.LogWarning("[SELECT_GOSSIP] No gossip frame open");
            BotTasks.Pop();
            return;
        }

        gossipFrame.SelectGossipOption(optionIndex);
        Logger.LogInformation("[SELECT_GOSSIP] Selected gossip option {Index}", optionIndex);
        BotTasks.Pop();
    }
}
