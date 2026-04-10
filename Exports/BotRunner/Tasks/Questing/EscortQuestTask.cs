using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace BotRunner.Tasks.Questing;

/// <summary>
/// Handles escort quest objectives. After accepting an escort quest,
/// follows the NPC to its destination while defending from attackers.
/// Completes when the NPC reaches its destination or dies.
/// </summary>
public class EscortQuestTask : BotTask, IBotTask
{
    private enum EscortState { FindNpc, FollowNpc, DefendNpc, Complete }

    private EscortState _state = EscortState.FindNpc;
    private readonly uint _npcEntry;
    private readonly float _followDistance;
    private ulong _npcGuid;

    private const float MaxFollowDistance = 30f;
    private const float DefendRange = 15f;

    public EscortQuestTask(IBotContext context, uint npcEntry, float followDistance = 5f)
        : base(context)
    {
        _npcEntry = npcEntry;
        _followDistance = followDistance;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case EscortState.FindNpc:
                var npc = ObjectManager.Units
                    .Where(u => u.Entry == _npcEntry && u.Health > 0)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (npc != null)
                {
                    _npcGuid = npc.Guid;
                    _state = EscortState.FollowNpc;
                    Logger.LogInformation("[ESCORT] Found NPC {Entry} at ({X:F0},{Y:F0})",
                        _npcEntry, npc.Position.X, npc.Position.Y);
                }
                break;

            case EscortState.FollowNpc:
                var escortNpc = ObjectManager.Units.FirstOrDefault(u => u.Guid == _npcGuid);
                if (escortNpc == null || escortNpc.Health <= 0)
                {
                    Logger.LogWarning("[ESCORT] NPC lost or dead — escort failed");
                    _state = EscortState.Complete;
                    return;
                }

                // Check for attackers targeting the NPC
                var attacker = ObjectManager.Units
                    .FirstOrDefault(u => u.TargetGuid == _npcGuid && u.Health > 0 && u.IsInCombat);

                if (attacker != null)
                {
                    _state = EscortState.DefendNpc;
                    return;
                }

                // Follow the NPC
                var dist = player.Position.DistanceTo(escortNpc.Position);
                if (dist > _followDistance)
                {
                    if (dist > MaxFollowDistance)
                    {
                        Logger.LogWarning("[ESCORT] Too far from NPC ({Dist:F0}y) — rushing to catch up", dist);
                    }
                    ObjectManager.MoveToward(escortNpc.Position);
                }
                break;

            case EscortState.DefendNpc:
                var npcDefend = ObjectManager.Units.FirstOrDefault(u => u.Guid == _npcGuid);
                if (npcDefend == null || npcDefend.Health <= 0)
                {
                    _state = EscortState.Complete;
                    return;
                }

                // Check if threats are cleared
                var threats = ObjectManager.Units
                    .Where(u => u.TargetGuid == _npcGuid && u.Health > 0)
                    .ToList();

                if (threats.Count == 0)
                {
                    _state = EscortState.FollowNpc;
                    return;
                }

                // Move toward nearest threat to the NPC
                var nearestThreat = threats
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .First();

                if (nearestThreat.Position.DistanceTo(player.Position) > DefendRange)
                    ObjectManager.MoveToward(nearestThreat.Position);
                break;

            case EscortState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
