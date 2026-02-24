using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BotRunner.Tasks;

/// <summary>
/// Task that walks to a nearby flight master NPC and discovers available flight paths.
/// Pushed by StateManager when a flight master is detected nearby that hasn't been visited.
/// </summary>
public class FlightMasterVisitTask : BotTask, IBotTask
{
    // Session-level tracking of visited flight masters (shared across task instances)
    private static readonly HashSet<ulong> _visitedFlightMasters = new();

    private enum FMState { FindFlightMaster, MoveToFlightMaster, DiscoverNodes, Done }
    private FMState _state = FMState.FindFlightMaster;

    private IWoWUnit? _fmUnit;
    private ulong _fmGuid;
    private DateTime _stateEnteredAt = DateTime.Now;
    private int _actionAttempts;
    // Uses Config.NpcInteractRange and Config.StuckTimeoutMs

    public FlightMasterVisitTask(IBotContext botContext) : base(botContext) { }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            Pop();
            return;
        }

        // Abort if in combat
        if (ObjectManager.Aggressors.Any())
        {
            Log.Information("[FLIGHTMASTER] Combat detected, aborting flight master visit");
            ObjectManager.StopAllMovement();
            Pop();
            return;
        }

        // Timeout
        if ((DateTime.Now - _stateEnteredAt).TotalMilliseconds > Config.StuckTimeoutMs)
        {
            Log.Warning("[FLIGHTMASTER] Timed out in {State}, aborting", _state);
            ObjectManager.StopAllMovement();
            Pop();
            return;
        }

        switch (_state)
        {
            case FMState.FindFlightMaster:
                FindFlightMaster(player);
                break;
            case FMState.MoveToFlightMaster:
                MoveToFlightMaster(player);
                break;
            case FMState.DiscoverNodes:
                DiscoverNodes();
                break;
            case FMState.Done:
                Pop();
                break;
        }
    }

    private void FindFlightMaster(IWoWUnit player)
    {
        _fmUnit = ObjectManager.Units
            .Where(u => u.Health > 0
                && u.Position != null
                && (u.NpcFlags & NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER) != 0
                && !_visitedFlightMasters.Contains(u.Guid))
            .OrderBy(u => player.Position.DistanceTo(u.Position))
            .FirstOrDefault();

        if (_fmUnit == null)
        {
            Log.Debug("[FLIGHTMASTER] No unvisited flight master found nearby");
            Pop();
            return;
        }

        _fmGuid = _fmUnit.Guid;
        var dist = player.Position.DistanceTo(_fmUnit.Position);
        Log.Information("[FLIGHTMASTER] Found flight master: {Name} ({Dist:F0}y away)", _fmUnit.Name, dist);
        SetState(FMState.MoveToFlightMaster);
    }

    private void MoveToFlightMaster(IWoWUnit player)
    {
        if (_fmUnit == null || _fmUnit.Position == null)
        {
            SetState(FMState.FindFlightMaster);
            return;
        }

        var dist = player.Position.DistanceTo(_fmUnit.Position);
        if (dist <= Config.NpcInteractRange)
        {
            ObjectManager.StopAllMovement();
            SetState(FMState.DiscoverNodes);
            return;
        }

        // Pathfind toward flight master (NavigationPath caches + throttles internally)
        NavigateToward(_fmUnit.Position);
    }

    private void DiscoverNodes()
    {
        if (!Wait.For("fm_discover", 1500, true))
            return;

        _actionAttempts++;
        if (_actionAttempts > 3)
        {
            Log.Warning("[FLIGHTMASTER] Too many discover attempts, marking as visited and aborting");
            _visitedFlightMasters.Add(_fmGuid);
            SetState(FMState.Done);
            return;
        }

        try
        {
            ObjectManager.SetTarget(_fmGuid);

            var nodes = ObjectManager.DiscoverTaxiNodesAsync(_fmGuid, CancellationToken.None)
                .GetAwaiter().GetResult();

            _visitedFlightMasters.Add(_fmGuid);

            Log.Information("[FLIGHTMASTER] Discovered {Count} flight paths from {Name}",
                nodes.Count, _fmUnit?.Name ?? "unknown");

            SetState(FMState.Done);
        }
        catch (Exception ex)
        {
            Log.Warning("[FLIGHTMASTER] Discovery failed: {Error}", ex.Message);
        }
    }

    private void SetState(FMState newState)
    {
        if (_state != newState)
        {
            _state = newState;
            _stateEnteredAt = DateTime.Now;
            _actionAttempts = 0;
        }
    }

    private void Pop()
    {
        Wait.Remove("fm_move");
        Wait.Remove("fm_discover");
        BotTasks.Pop();
    }
}
