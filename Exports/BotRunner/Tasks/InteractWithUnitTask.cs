using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using Serilog;
using System;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: navigate to and interact with a unit by GUID.
/// Moves within interact range, sets target, then pops.
/// The caller (StateManager) is responsible for follow-up actions
/// (e.g., SelectGossip, AcceptQuest) once the NPC dialog opens.
/// Maps to ActionType.INTERACT_WITH from StateManager.
/// </summary>
public class InteractWithUnitTask(IBotContext botContext, ulong targetGuid) : BotTask(botContext), IBotTask
{
    private DateTime _startTime = DateTime.Now;
    private bool _interacted;

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
            Log.Warning("[INTERACT] Timed out trying to reach {Guid:X}", targetGuid);
            ObjectManager.StopAllMovement();
            BotTasks.Pop();
            return;
        }

        // Find the target unit
        var target = ObjectManager.Units
            .FirstOrDefault(u => u.Guid == targetGuid);

        if (target == null)
        {
            Log.Warning("[INTERACT] Target {Guid:X} not found in visible units", targetGuid);
            BotTasks.Pop();
            return;
        }

        var distance = player.Position.DistanceTo(target.Position);

        // Move closer if out of interact range
        if (distance > Config.NpcInteractRange)
        {
            NavigateToward(target.Position);
            return;
        }

        // In range — stop and interact
        if (!_interacted)
        {
            ObjectManager.StopAllMovement();
            ObjectManager.SetTarget(targetGuid);
            _interacted = true;
            Log.Information("[INTERACT] Interacting with {Name} ({Guid:X})", target.Name, targetGuid);
            return;
        }

        // Done — pop and let StateManager handle the dialog
        BotTasks.Pop();
    }
}
