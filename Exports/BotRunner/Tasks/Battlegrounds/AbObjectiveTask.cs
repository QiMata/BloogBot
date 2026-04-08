using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog; // TODO: migrate to ILogger when DI is available
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Battlegrounds;

/// <summary>
/// Arathi Basin objective task: node assault, defense, and status tracking.
/// 5 nodes: Stables, Farm, Blacksmith, Lumber Mill, Gold Mine.
/// Interact with banner game object to assault/defend.
/// </summary>
public class AbObjectiveTask : BotTask, IBotTask
{
    private enum AbState { SelectNode, MoveToNode, AssaultNode, DefendNode, Complete }

    private AbState _state = AbState.SelectNode;
    private readonly bool _isHorde;
    private Position _targetNode;
    private string _targetNodeName = "";

    // AB node positions (approximate center of capture area)
    public static readonly Dictionary<string, Position> NodePositions = new()
    {
        ["Stables"] = new(1166f, 1200f, -56f),
        ["Farm"] = new(803f, 874f, -55f),
        ["Blacksmith"] = new(977f, 1046f, -44f),
        ["Lumber Mill"] = new(856f, 1148f, 11f),
        ["Gold Mine"] = new(1146f, 848f, -110f),
    };

    private const float NodeInteractRange = 8f;

    public AbObjectiveTask(IBotContext context, bool isHorde) : base(context)
    {
        _isHorde = isHorde;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case AbState.SelectNode:
                // Pick nearest uncontrolled node
                var nearest = NodePositions
                    .OrderBy(kv => kv.Value.DistanceTo(player.Position))
                    .First();
                _targetNode = nearest.Value;
                _targetNodeName = nearest.Key;
                _state = AbState.MoveToNode;
                Log.Information("[AB] Targeting {Node} at ({X:F0},{Y:F0})",
                    _targetNodeName, _targetNode.X, _targetNode.Y);
                break;

            case AbState.MoveToNode:
                var dist = player.Position.DistanceTo(_targetNode);
                if (dist <= NodeInteractRange)
                {
                    _state = AbState.AssaultNode;
                    return;
                }
                ObjectManager.MoveToward(_targetNode);
                break;

            case AbState.AssaultNode:
                // Find the banner game object at the node
                var banner = ObjectManager.GameObjects
                    .Where(go => go.Position.DistanceTo(_targetNode) < 15f)
                    .OrderBy(go => go.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (banner != null)
                {
                    banner.Interact();
                    Log.Information("[AB] Assaulting {Node}!", _targetNodeName);
                }
                _state = AbState.DefendNode;
                break;

            case AbState.DefendNode:
                // Stay near the node to defend
                if (player.Position.DistanceTo(_targetNode) > 20f)
                    ObjectManager.MoveToward(_targetNode);
                // After defending for a while, look for next objective
                _state = AbState.SelectNode;
                break;

            case AbState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
