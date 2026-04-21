using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Battlegrounds;

/// <summary>
/// Alterac Valley objective task: tower assault/defense, graveyard capture,
/// general kill coordination.
/// Horde pushes south toward Vanndar Stormpike; Alliance pushes north toward Drek'Thar.
/// </summary>
public class AvObjectiveTask : BotTask, IBotTask
{
    private enum AvState { SelectObjective, MoveToObjective, AssaultObjective, DefendObjective, PushToGeneral, Complete }

    private AvState _state = AvState.SelectObjective;
    private readonly bool _isHorde;
    private Position _targetPosition;
    private string _targetName = "";
    private int _objectivesCompleted;

    // AV tower/bunker positions
    public static readonly Dictionary<string, Position> HordeTargets = new()
    {
        ["Stonehearth Bunker"] = new(-48f, -293f, 57f),
        ["Icewing Bunker"] = new(203f, -360f, 56f),
        ["Dun Baldar North Bunker"] = new(569f, -94f, 52f),
        ["Dun Baldar South Bunker"] = new(568f, -48f, 52f),
        ["Stormpike Graveyard"] = new(669f, -294f, 30f),
        ["Vanndar Stormpike"] = new(722f, -10f, 50f),
    };

    public static readonly Dictionary<string, Position> AllianceTargets = new()
    {
        ["Tower Point"] = new(-755f, -355f, 68f),
        ["Iceblood Tower"] = new(-572f, -262f, 75f),
        ["East Frostwolf Tower"] = new(-1302f, -316f, 91f),
        ["West Frostwolf Tower"] = new(-1297f, -266f, 114f),
        ["Frostwolf Graveyard"] = new(-1082f, -340f, 55f),
        ["Drek'Thar"] = new(-1370f, -219f, 99f),
    };

    private const float ObjectiveInteractRange = 8f;
    private const float GeneralRange = 30f;

    public AvObjectiveTask(IBotContext context, bool isHorde) : base(context)
    {
        _isHorde = isHorde;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case AvState.SelectObjective:
                var targets = _isHorde ? HordeTargets : AllianceTargets;
                var targetList = targets.ToList();

                // Progress through objectives in order; push to general last
                if (_objectivesCompleted >= targetList.Count - 1)
                {
                    // All towers/GYs done — push to enemy general
                    var general = targetList.Last();
                    _targetPosition = general.Value;
                    _targetName = general.Key;
                    _state = AvState.PushToGeneral;
                }
                else
                {
                    var next = targetList[_objectivesCompleted];
                    _targetPosition = next.Value;
                    _targetName = next.Key;
                    _state = AvState.MoveToObjective;
                }

                Logger.LogInformation("[AV] Targeting {Objective} at ({X:F0},{Y:F0})",
                    _targetName, _targetPosition.X, _targetPosition.Y);
                break;

            case AvState.MoveToObjective:
                var dist = player.Position.DistanceTo(_targetPosition);
                if (dist <= ObjectiveInteractRange)
                {
                    _state = AvState.AssaultObjective;
                    return;
                }
                ObjectManager.MoveToward(_targetPosition);
                break;

            case AvState.AssaultObjective:
                // Find the banner/flag game object at the objective
                var banner = ObjectManager.GameObjects
                    .Where(go => go.Position.DistanceTo(_targetPosition) < 20f)
                    .OrderBy(go => go.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (banner != null)
                {
                    ObjectManager.ForceStopImmediate();
                    banner.Interact();
                    Logger.LogInformation("[AV] Assaulting {Objective}!", _targetName);
                }
                _state = AvState.DefendObjective;
                break;

            case AvState.DefendObjective:
                // Stay near objective briefly, then move to next
                if (player.Position.DistanceTo(_targetPosition) > 25f)
                    ObjectManager.MoveToward(_targetPosition);

                _objectivesCompleted++;
                _state = AvState.SelectObjective;
                break;

            case AvState.PushToGeneral:
                var generalDist = player.Position.DistanceTo(_targetPosition);
                if (generalDist <= GeneralRange)
                {
                    // In range of general — engage in combat (handled by combat rotation)
                    Logger.LogInformation("[AV] Engaging {General}!", _targetName);
                    _state = AvState.Complete;
                    return;
                }
                ObjectManager.MoveToward(_targetPosition);
                break;

            case AvState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
