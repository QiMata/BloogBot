using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog; // TODO: migrate to ILogger when DI is available
using System;
using System.Linq;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Uses the hearthstone to teleport to the character's bind point.
/// Steps:
/// 1. Find hearthstone in inventory (item ID 6948).
/// 2. Stop all movement.
/// 3. Use hearthstone (CMSG_USE_ITEM or ObjectManager.UseItem).
/// 4. Wait for 10s cast bar (spell 8690 "Hearthstone").
/// 5. Detect teleport: position delta >100y or mapId change.
/// 6. Pop task.
/// Cancel if interrupted (combat).
/// </summary>
public class UseHearthstoneTask : BotTask, IBotTask
{
    private enum HearthState { FindItem, StopAndCast, WaitForCast, DetectTeleport, Complete }

    private HearthState _state = HearthState.FindItem;
    private long _castStartMs;
    private Position? _startPosition;
    private uint _startMapId;
    private const int HearthstoneItemId = 6948;
    private const int CastDurationMs = 10_000;
    private const int TeleportDetectTimeoutMs = 15_000;
    private const float TeleportDistanceThreshold = 100f;

    public UseHearthstoneTask(IBotContext context) : base(context) { }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        // Cancel if in combat
        if (player.IsInCombat)
        {
            Log.Warning("[UseHearthstone] Cancelled — entered combat.");
            BotContext.BotTasks.Pop();
            return;
        }

        switch (_state)
        {
            case HearthState.FindItem:
                // Check if hearthstone exists in inventory
                var hasHearthstone = ObjectManager.Items
                    .Any(i => i.ItemId == HearthstoneItemId);

                if (!hasHearthstone)
                {
                    Log.Warning("[UseHearthstone] No hearthstone found in inventory.");
                    BotContext.BotTasks.Pop();
                    return;
                }

                _startPosition = new Position(player.Position.X, player.Position.Y, player.Position.Z);
                _startMapId = player.MapId;
                _state = HearthState.StopAndCast;
                break;

            case HearthState.StopAndCast:
                ObjectManager.StopMovement(ControlBits.Front | ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight);

                // Use hearthstone by casting spell "Hearthstone" (same as clicking the item)
                ObjectManager.CastSpell("Hearthstone");
                _castStartMs = Environment.TickCount64;
                _state = HearthState.WaitForCast;
                Log.Information("[UseHearthstone] Casting Hearthstone...");
                break;

            case HearthState.WaitForCast:
                // Wait for cast to complete (10s)
                if (Environment.TickCount64 - _castStartMs > CastDurationMs)
                {
                    _state = HearthState.DetectTeleport;
                }

                // Check if cast was interrupted
                if (!player.IsChanneling && !player.IsCasting
                    && Environment.TickCount64 - _castStartMs > 2000)
                {
                    Log.Warning("[UseHearthstone] Cast interrupted.");
                    BotContext.BotTasks.Pop();
                    return;
                }
                break;

            case HearthState.DetectTeleport:
                if (_startPosition == null)
                {
                    _state = HearthState.Complete;
                    return;
                }

                var dist = player.Position.DistanceTo(_startPosition);
                var mapChanged = player.MapId != _startMapId;

                if (dist > TeleportDistanceThreshold || mapChanged)
                {
                    Log.Information("[UseHearthstone] Teleported! Distance={Dist:F0}y, mapChanged={MapChanged}",
                        dist, mapChanged);
                    _state = HearthState.Complete;
                    return;
                }

                if (Environment.TickCount64 - _castStartMs > TeleportDetectTimeoutMs)
                {
                    Log.Warning("[UseHearthstone] Teleport detection timeout after {Timeout}ms.", TeleportDetectTimeoutMs);
                    _state = HearthState.Complete;
                }
                break;

            case HearthState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
