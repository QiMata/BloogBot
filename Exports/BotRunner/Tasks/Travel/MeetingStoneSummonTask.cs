using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog; // TODO: migrate to ILogger when DI is available
using System;
using System.Linq;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Meeting stone summoning at dungeon entrances.
/// Steps:
/// 1. Navigate to meeting stone game object (GameObjectType 23).
/// 2. Interact via CMSG_MEETINGSTONE_JOIN.
/// 3. Wait for SMSG_MEETINGSTONE_SETQUEUE confirmation.
/// 4. When 3 members are at stone, summoning portal appears.
/// 5. Target absent member, confirm summon.
/// 6. Wait for SMSG_MEETINGSTONE_COMPLETE.
/// 7. Repeat for all absent members.
/// </summary>
public class MeetingStoneSummonTask : BotTask, IBotTask
{
    private enum StoneState { FindStone, NavigateToStone, Interact, WaitForQueue, SummonMembers, Complete }

    private StoneState _state = StoneState.FindStone;
    private IWoWGameObject? _meetingStone;
    private long _stateStartMs;
    private const float InteractionRange = 5.0f;
    private const int QueueTimeoutMs = 30_000;
    private const uint GAMEOBJECT_TYPE_MEETINGSTONE = 23;

    public MeetingStoneSummonTask(IBotContext context) : base(context) { }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case StoneState.FindStone:
                _meetingStone = ObjectManager.GameObjects
                    .Where(go => go.TypeId == GAMEOBJECT_TYPE_MEETINGSTONE)
                    .OrderBy(go => go.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (_meetingStone == null)
                {
                    Log.Warning("[MeetingStone] No meeting stone found nearby.");
                    BotContext.BotTasks.Pop();
                    return;
                }

                Log.Information("[MeetingStone] Found stone at ({X:F0},{Y:F0},{Z:F0}), dist {Dist:F0}y",
                    _meetingStone.Position.X, _meetingStone.Position.Y, _meetingStone.Position.Z,
                    _meetingStone.Position.DistanceTo(player.Position));
                _state = StoneState.NavigateToStone;
                break;

            case StoneState.NavigateToStone:
                if (_meetingStone == null) { Pop(); return; }
                var dist = player.Position.DistanceTo(_meetingStone.Position);
                if (dist <= InteractionRange)
                {
                    _state = StoneState.Interact;
                    return;
                }
                ObjectManager.MoveToward(_meetingStone.Position);
                break;

            case StoneState.Interact:
                if (_meetingStone == null) { Pop(); return; }
                ObjectManager.StopMovement(ControlBits.Front | ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight);
                _meetingStone.Interact();
                _stateStartMs = Environment.TickCount64;
                _state = StoneState.WaitForQueue;
                Log.Information("[MeetingStone] Interacting with meeting stone (CMSG_MEETINGSTONE_JOIN).");
                break;

            case StoneState.WaitForQueue:
                if (Environment.TickCount64 - _stateStartMs > QueueTimeoutMs)
                {
                    Log.Warning("[MeetingStone] Queue timeout after {Timeout}ms.", QueueTimeoutMs);
                    Pop();
                    return;
                }
                // TODO: Detect SMSG_MEETINGSTONE_SETQUEUE confirmation
                // When confirmed, transition to SummonMembers
                // For now, advance after 5s
                if (Environment.TickCount64 - _stateStartMs > 5000)
                {
                    _state = StoneState.SummonMembers;
                }
                break;

            case StoneState.SummonMembers:
                // TODO: Iterate absent party members and summon each
                // Each summon: target member → CMSG_SUMMON_RESPONSE → wait for arrive
                Log.Information("[MeetingStone] Summoning members (TODO: iterate absent members).");
                _state = StoneState.Complete;
                break;

            case StoneState.Complete:
                Pop();
                break;
        }
    }

    private void Pop() => BotContext.BotTasks.Pop();
}
