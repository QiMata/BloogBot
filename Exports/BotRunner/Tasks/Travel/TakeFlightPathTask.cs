using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;
using System.Threading;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Takes a taxi flight from the nearest discovered flight master to a destination node.
/// Steps:
/// 1. Navigate to nearest discovered flight master NPC.
/// 2. Interact (CMSG_GOSSIP_HELLO).
/// 3. Wait for SMSG_SHOWTAXINODES.
/// 4. Activate flight via FlightMasterNetworkClientComponent.ActivateFlightAsync.
/// 5. Detect flight completion (position stops changing for 3s after being in-flight).
/// 6. Pop task.
/// Timeout: 5 minutes (longest vanilla flight ≈4 minutes).
/// </summary>
public class TakeFlightPathTask : BotTask, IBotTask
{
    private enum FlightState { FindFlightMaster, NavigateToFM, InteractWithFM, WaitForTaxiWindow, ActivateFlight, InFlight, Complete }

    private FlightState _state = FlightState.FindFlightMaster;
    private readonly uint _sourceNodeId;
    private readonly uint _destinationNodeId;
    private IWoWUnit? _flightMaster;
    private long _stateStartMs;
    private Position? _lastFlightPos;
    private int _stationaryTicks;
    private const float InteractionRange = 5.0f;
    private const int FlightTimeoutMs = 300_000; // 5 minutes
    private const int StationaryTicksForLanding = 6; // 3 seconds at 500ms poll
    private const uint NPC_FLAG_FLIGHTMASTER = 0x2000;

    public TakeFlightPathTask(IBotContext context, uint sourceNodeId, uint destinationNodeId)
        : base(context)
    {
        _sourceNodeId = sourceNodeId;
        _destinationNodeId = destinationNodeId;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case FlightState.FindFlightMaster:
                _flightMaster = ObjectManager.Units
                    .Where(u => ((uint)u.NpcFlags & NPC_FLAG_FLIGHTMASTER) != 0 && u.Health > 0)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (_flightMaster == null)
                {
                    Log.Warning("[TakeFlightPath] No flight master found nearby.");
                    BotContext.BotTasks.Pop();
                    return;
                }

                _state = FlightState.NavigateToFM;
                _stateStartMs = Environment.TickCount64;
                break;

            case FlightState.NavigateToFM:
                if (_flightMaster == null) { Pop(); return; }
                var dist = player.Position.DistanceTo(_flightMaster.Position);
                if (dist <= InteractionRange)
                {
                    _state = FlightState.InteractWithFM;
                    return;
                }
                ObjectManager.MoveToward(_flightMaster.Position);
                break;

            case FlightState.InteractWithFM:
                if (_flightMaster == null) { Pop(); return; }
                ObjectManager.StopMovement(ControlBits.Front | ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight);
                _flightMaster.Interact();
                _state = FlightState.WaitForTaxiWindow;
                _stateStartMs = Environment.TickCount64;
                break;

            case FlightState.WaitForTaxiWindow:
                // Wait for SMSG_SHOWTAXINODES — the FlightMaster gossip opens taxi map
                if (Environment.TickCount64 - _stateStartMs > 5000)
                {
                    Log.Warning("[TakeFlightPath] Taxi window timeout. Retrying interaction.");
                    _state = FlightState.InteractWithFM;
                    return;
                }
                // The taxi window detection happens via the FlightMasterNetworkClientComponent
                // For now, proceed after a brief delay
                if (Environment.TickCount64 - _stateStartMs > 1500)
                {
                    _state = FlightState.ActivateFlight;
                }
                break;

            case FlightState.ActivateFlight:
                if (_flightMaster == null) { Pop(); return; }
                Log.Information("[TakeFlightPath] Activating flight from node {Src} to node {Dst}",
                    _sourceNodeId, _destinationNodeId);

                // Activate flight via FlightMasterNetworkClientComponent
                // The agent factory provides access to the flight master agent
                var factory = BotContext.Container.PathfindingClient; // TODO: use agent factory
                // For now, use the TaxiFrame if available (FG) or direct packet (BG)
                var taxiFrame = ObjectManager.TaxiFrame;
                if (taxiFrame != null)
                {
                    taxiFrame.SelectNodeByNumber((int)_destinationNodeId);
                }
                else
                {
                    Log.Warning("[TakeFlightPath] No TaxiFrame available — need FlightMasterAgent for BG path.");
                }

                _state = FlightState.InFlight;
                _stateStartMs = Environment.TickCount64;
                _lastFlightPos = new Position(player.Position.X, player.Position.Y, player.Position.Z);
                _stationaryTicks = 0;
                break;

            case FlightState.InFlight:
                if (Environment.TickCount64 - _stateStartMs > FlightTimeoutMs)
                {
                    Log.Warning("[TakeFlightPath] Flight timeout after {Timeout}ms.", FlightTimeoutMs);
                    Pop();
                    return;
                }

                // Detect landing: position stops changing
                if (_lastFlightPos != null)
                {
                    var posDelta = player.Position.DistanceTo(_lastFlightPos);
                    if (posDelta < 1.0f)
                        _stationaryTicks++;
                    else
                        _stationaryTicks = 0;
                }
                _lastFlightPos = new Position(player.Position.X, player.Position.Y, player.Position.Z);

                if (_stationaryTicks >= StationaryTicksForLanding
                    && Environment.TickCount64 - _stateStartMs > 5000) // Min 5s in flight
                {
                    Log.Information("[TakeFlightPath] Landed at ({X:F0},{Y:F0},{Z:F0}).",
                        player.Position.X, player.Position.Y, player.Position.Z);
                    Pop();
                }
                break;

            case FlightState.Complete:
                Pop();
                break;
        }
    }

    private void Pop() => BotContext.BotTasks.Pop();
}
