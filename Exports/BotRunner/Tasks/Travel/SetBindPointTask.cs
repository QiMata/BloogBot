using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Interacts with an innkeeper NPC to set the character's hearthstone bind point.
/// Steps:
/// 1. Find nearest innkeeper NPC (UNIT_NPC_FLAG_INNKEEPER = 0x80).
/// 2. Navigate within interaction range.
/// 3. Interact (right-click / CMSG_GOSSIP_HELLO).
/// 4. Select "Make this inn your home" gossip option (Binder type).
/// 5. Wait for SMSG_BINDPOINTUPDATE confirming new bind location.
/// 6. Pop task.
/// </summary>
public class SetBindPointTask : BotTask, IBotTask
{
    private enum BindState { FindInnkeeper, NavigateToInnkeeper, Interact, WaitForBind, Complete }

    private BindState _state = BindState.FindInnkeeper;
    private IWoWUnit? _innkeeper;
    private long _interactTimeMs;
    private const float InteractionRange = 5.0f;
    private const int BindTimeoutMs = 10_000;
    private const uint NPC_FLAG_INNKEEPER = 0x80;

    public SetBindPointTask(IBotContext context) : base(context) { }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case BindState.FindInnkeeper:
                _innkeeper = ObjectManager.Units
                    .Where(u => ((uint)u.NpcFlags & NPC_FLAG_INNKEEPER) != 0)
                    .Where(u => u.Health > 0)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (_innkeeper == null)
                {
                    Logger.LogWarning("[SetBindPoint] No innkeeper found nearby.");
                    BotContext.BotTasks.Pop();
                    return;
                }

                Logger.LogInformation("[SetBindPoint] Found innkeeper '{Name}' at ({X:F0},{Y:F0},{Z:F0}), distance {Dist:F0}y",
                    _innkeeper.Name, _innkeeper.Position.X, _innkeeper.Position.Y, _innkeeper.Position.Z,
                    _innkeeper.Position.DistanceTo(player.Position));
                _state = BindState.NavigateToInnkeeper;
                break;

            case BindState.NavigateToInnkeeper:
                if (_innkeeper == null || _innkeeper.Health <= 0)
                {
                    _state = BindState.FindInnkeeper;
                    return;
                }

                var dist = player.Position.DistanceTo(_innkeeper.Position);
                if (dist <= InteractionRange)
                {
                    _state = BindState.Interact;
                    return;
                }

                ObjectManager.MoveToward(_innkeeper.Position);
                break;

            case BindState.Interact:
                if (_innkeeper == null) { _state = BindState.FindInnkeeper; return; }

                ObjectManager.StopMovement(ControlBits.Front | ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight);
                _innkeeper.Interact();
                _interactTimeMs = Environment.TickCount64;
                _state = BindState.WaitForBind;
                Logger.LogInformation("[SetBindPoint] Interacting with innkeeper to set bind point.");
                break;

            case BindState.WaitForBind:
                // The gossip interaction + bind selection happens via the gossip frame.
                // For BG bots without gossip frames, the innkeeper interaction automatically
                // triggers the bind point update after CMSG_BINDER_ACTIVATE.
                if (Environment.TickCount64 - _interactTimeMs > BindTimeoutMs)
                {
                    Logger.LogWarning("[SetBindPoint] Bind point timeout after {Timeout}ms.", BindTimeoutMs);
                    _state = BindState.Complete;
                    return;
                }

                // Check if gossip frame opened — select Binder option if available
                var gossipFrame = ObjectManager.GossipFrame;
                if (gossipFrame != null && gossipFrame.IsOpen)
                {
                    // GossipTypes.Binder = 5
                    gossipFrame.SelectGossipOption(5);
                    _state = BindState.Complete;
                    Logger.LogInformation("[SetBindPoint] Selected binder gossip option. Bind point should be updated.");
                    return;
                }
                break;

            case BindState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
