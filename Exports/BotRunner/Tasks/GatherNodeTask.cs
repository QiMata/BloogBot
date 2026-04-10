using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: navigate to a resource node (herb/ore) and gather it.
/// Moves within interact range, sets target, then pops.
/// Maps to ActionType.GATHER_NODE from StateManager.
/// </summary>
public class GatherNodeTask(IBotContext botContext, ulong nodeGuid) : BotTask(botContext), IBotTask
{
    private DateTime _startTime = DateTime.Now;
    private bool _interacted;
    private const float GATHER_RANGE = 5f;

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            BotTasks.Pop();
            return;
        }

        // Timeout after 30 seconds
        if ((DateTime.Now - _startTime).TotalSeconds > 30)
        {
            Logger.LogWarning("[GATHER] Timed out trying to reach node {Guid:X}", nodeGuid);
            ObjectManager.StopAllMovement();
            BotTasks.Pop();
            return;
        }

        // Combat interrupts gathering
        if (player.IsInCombat)
        {
            ObjectManager.StopAllMovement();
            BotTasks.Pop();
            return;
        }

        // Swimming means pathfinding routed us into water — abort and let coordinator retry
        if (player.IsSwimming)
        {
            Logger.LogWarning("[GATHER] Entered water while approaching node {Guid:X}; aborting", nodeGuid);
            ObjectManager.StopAllMovement();
            BotTasks.Pop();
            return;
        }

        // Find the node
        var node = ObjectManager.GameObjects
            .FirstOrDefault(go => go.Guid == nodeGuid);

        if (node == null)
        {
            Logger.LogWarning("[GATHER] Node {Guid:X} not found", nodeGuid);
            BotTasks.Pop();
            return;
        }

        var distance = player.Position.DistanceTo(node.Position);

        if (distance > GATHER_RANGE)
        {
            NavigateToward(node.Position);
            return;
        }

        if (!_interacted)
        {
            ObjectManager.StopAllMovement();
            ObjectManager.SetTarget(nodeGuid);
            _interacted = true;
            Logger.LogInformation("[GATHER] Gathering node {Name} ({Guid:X})", node.Name, nodeGuid);
            return;
        }

        BotTasks.Pop();
    }
}
