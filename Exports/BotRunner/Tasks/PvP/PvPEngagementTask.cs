using BotRunner.Combat;
using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace BotRunner.Tasks.PvP;

/// <summary>
/// World PvP engagement task. When hostile player detected:
/// evaluate threat → engage if winnable, flee to nearest guard if not.
/// Uses existing combat rotation from BotProfiles for actual fighting.
/// Avoids civilian NPCs (dishonorable kills).
/// </summary>
public class PvPEngagementTask : BotTask, IBotTask
{
    private enum PvPState { Evaluate, Engage, Flee, Complete }

    private PvPState _state = PvPState.Evaluate;
    private readonly float _scanRange;

    // Known guard NPC positions (Orgrimmar/Stormwind)
    private static readonly Position OrgrimmarGuard = new(1629f, -4373f, 31f);
    private static readonly Position StormwindGuard = new(-8913f, 554f, 94f);

    private const float EngageRange = 30f;
    private const float FleeSuccessRange = 60f;

    public PvPEngagementTask(IBotContext context, float scanRange = 40f) : base(context)
    {
        _scanRange = scanRange;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case PvPState.Evaluate:
                var hostiles = HostilePlayerDetector.Scan(ObjectManager, _scanRange);
                if (hostiles.Count == 0)
                {
                    _state = PvPState.Complete;
                    return;
                }

                var primary = hostiles.First();

                // Flee from overwhelming threats or multiple enemies
                if (primary.Threat == HostilePlayerDetector.ThreatLevel.Overwhelming
                    || hostiles.Count >= 3)
                {
                    Logger.LogInformation("[PVP] Fleeing from {Count} hostiles (threat: {Threat})",
                        hostiles.Count, primary.Threat);
                    _state = PvPState.Flee;
                    return;
                }

                // Engage winnable fights
                Logger.LogInformation("[PVP] Engaging {Target} (L{Level}, {Health}% HP, threat: {Threat})",
                    primary.Player.Name, primary.Player.Level,
                    primary.Player.HealthPercent, primary.Threat);
                _state = PvPState.Engage;
                break;

            case PvPState.Engage:
                // Re-scan for hostiles
                var targets = HostilePlayerDetector.Scan(ObjectManager, _scanRange);
                if (targets.Count == 0)
                {
                    _state = PvPState.Complete;
                    return;
                }

                var target = targets.First();

                // Switch to flee if situation deteriorated
                if (targets.Count >= 4 || player.HealthPercent < 20)
                {
                    _state = PvPState.Flee;
                    return;
                }

                // Move toward target if out of range
                if (target.Distance > EngageRange)
                    ObjectManager.MoveToward(target.Player.Position);
                break;

            case PvPState.Flee:
                // Run toward nearest faction guard
                var guardPos = GetNearestGuardPosition(player);
                var guardDist = player.Position.DistanceTo(guardPos);

                if (guardDist <= FleeSuccessRange)
                {
                    Logger.LogInformation("[PVP] Reached safety near guards");
                    _state = PvPState.Complete;
                    return;
                }

                ObjectManager.MoveToward(guardPos);
                break;

            case PvPState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }

    private static Position GetNearestGuardPosition(IWoWLocalPlayer player)
    {
        // Simple heuristic: Horde → Orgrimmar guards, Alliance → Stormwind guards
        var distOrg = player.Position.DistanceTo(OrgrimmarGuard);
        var distSw = player.Position.DistanceTo(StormwindGuard);
        return distOrg < distSw ? OrgrimmarGuard : StormwindGuard;
    }
}
