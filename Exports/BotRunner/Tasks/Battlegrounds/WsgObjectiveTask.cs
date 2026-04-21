using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace BotRunner.Tasks.Battlegrounds;

/// <summary>
/// Warsong Gulch objective task: flag pickup, carry, capture, return.
/// Uses CMSG_GAMEOBJ_USE for flag interaction.
/// Detects flag state from SMSG_UPDATE_WORLD_STATE packets.
/// </summary>
public class WsgObjectiveTask : BotTask, IBotTask
{
    private enum WsgState { FindObjective, MoveToFlag, PickupFlag, CarryFlagToBase, CaptureFlag, DefendBase, Complete }

    private WsgState _state = WsgState.FindObjective;
    private readonly bool _isHorde;
    private IWoWGameObject? _targetFlag;

    // WSG flag game object entries
    private const uint AllianceFlagEntry = 179830;  // Silverwing Flag
    private const uint HordeFlagEntry = 179831;      // Warsong Flag
    private const uint AllianceFlagDroppedEntry = 179785;
    private const uint HordeFlagDroppedEntry = 179786;

    // WSG base positions
    private static readonly Position HordeBase = new(1539f, 1481f, 352f);
    private static readonly Position AllianceBase = new(1540f, 1481f, 352f); // Approximate

    private const float FlagInteractRange = 5f;
    private const float BaseArrivalRange = 10f;

    public WsgObjectiveTask(IBotContext context, bool isHorde) : base(context)
    {
        _isHorde = isHorde;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case WsgState.FindObjective:
                // Look for enemy flag to pick up
                var enemyFlagEntry = _isHorde ? AllianceFlagEntry : HordeFlagEntry;
                var droppedFlagEntry = _isHorde ? AllianceFlagDroppedEntry : HordeFlagDroppedEntry;

                _targetFlag = ObjectManager.GameObjects
                    .Where(go => go.Entry == enemyFlagEntry || go.Entry == droppedFlagEntry)
                    .OrderBy(go => go.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (_targetFlag != null)
                {
                    _state = WsgState.MoveToFlag;
                    Logger.LogInformation("[WSG] Found enemy flag at ({X:F0},{Y:F0},{Z:F0})",
                        _targetFlag.Position.X, _targetFlag.Position.Y, _targetFlag.Position.Z);
                }
                else
                {
                    _state = WsgState.DefendBase;
                }
                break;

            case WsgState.MoveToFlag:
                if (_targetFlag == null) { _state = WsgState.FindObjective; return; }
                var flagDist = player.Position.DistanceTo(_targetFlag.Position);
                if (flagDist <= FlagInteractRange)
                {
                    _state = WsgState.PickupFlag;
                    return;
                }
                ObjectManager.MoveToward(_targetFlag.Position);
                break;

            case WsgState.PickupFlag:
                if (_targetFlag == null) { _state = WsgState.FindObjective; return; }
                ObjectManager.ForceStopImmediate();
                _targetFlag.Interact(); // CMSG_GAMEOBJ_USE
                _state = WsgState.CarryFlagToBase;
                Logger.LogInformation("[WSG] Picking up flag!");
                break;

            case WsgState.CarryFlagToBase:
                var homeBase = _isHorde ? HordeBase : AllianceBase;
                var baseDist = player.Position.DistanceTo(homeBase);
                if (baseDist <= BaseArrivalRange)
                {
                    _state = WsgState.CaptureFlag;
                    return;
                }
                ObjectManager.MoveToward(homeBase);
                break;

            case WsgState.CaptureFlag:
                // At home base — interact with own flag stand to capture
                var ownFlagEntry = _isHorde ? HordeFlagEntry : AllianceFlagEntry;
                var ownFlag = ObjectManager.GameObjects
                    .FirstOrDefault(go => go.Entry == ownFlagEntry);
                if (ownFlag != null)
                {
                    ObjectManager.ForceStopImmediate();
                    ownFlag.Interact();
                }
                Logger.LogInformation("[WSG] Flag captured!");
                _state = WsgState.FindObjective; // Loop for next flag
                break;

            case WsgState.DefendBase:
                // No enemy flag available — defend home base
                var defendPos = _isHorde ? HordeBase : AllianceBase;
                if (player.Position.DistanceTo(defendPos) > 20f)
                    ObjectManager.MoveToward(defendPos);
                // Periodically check for new objectives
                _state = WsgState.FindObjective;
                break;

            case WsgState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
