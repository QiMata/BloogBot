using BotRunner.Movement;
using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: run to corpse using pathfinding and reclaim when eligible.
/// </summary>
public class RetrieveCorpseTask(IBotContext botContext, Position corpsePosition) : BotTask(botContext), IBotTask
{
    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

    // Corpse runback follows the navmesh path faithfully.
    // Disable probe heuristics/pruning so corners are not skipped into walls.
    // strictPathValidation is OFF because long outdoor corpse runs (460y+) have
    // segments where collision-based LOS rejects valid navmesh paths.
    private readonly NavigationPath _navPath = CreateCorpseNavPath(botContext);

    private static NavigationPath CreateCorpseNavPath(IBotContext ctx)
        => NavigationPathFactory.Create(
            ctx.Container.PathfindingClient,
            ctx.ObjectManager,
            NavigationRoutePolicy.CorpseRun);
    private DateTime _startTime = DateTime.UtcNow;
    private DateTime _lastReclaimAttempt = DateTime.MinValue;
    private DateTime _lastCooldownLog = DateTime.MinValue;
    private DateTime? _noPathSinceUtc;
    private bool _loggedPathfindingMode;
    private DateTime? _nonGhostSinceUtc;
    private DateTime _lastWaypointDriveLogUtc = DateTime.MinValue;
    private DateTime _lastTickDiagUtc = DateTime.MinValue;
    private bool _stoppedForRetrieval;
    private bool _triedSpiritHealer;

    // MaNGOS CORPSE_RECLAIM_RADIUS = 39y (3D distance). We compute a dynamic 2D
    // approach distance from the current Z delta so the bot walks close enough in
    // 3D even in multi-level areas like Orgrimmar where the graveyard is 20+ yards
    // above/below the corpse.
    private const float ServerReclaimRadius3D = 39f;
    private const float ReclaimSafetyMargin = 5f; // stay 5y inside the 39y sphere
    private const float MinRetrieveRange2D = 5f;   // never stop further than this minimum
    private static readonly TimeSpan TaskTimeout = TimeSpan.FromMinutes(12);
    private static readonly TimeSpan NoPathTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReclaimRetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CooldownLogInterval = TimeSpan.FromSeconds(5);
    private const float MaxCorpseZDeltaForNavigation = 120f;
    private const float MinimumDriveWaypointDistance = 3f;
    private const int TraceSummaryWaypointLimit = 4;
    private const int TraceSummarySampleLimit = 3;

    private static bool HasGhostFlag(IWoWLocalPlayer player)
    {
        try { return (((uint)player.PlayerFlags) & PlayerFlagGhost) != 0; }
        catch { return false; }
    }

    private static bool IsStandStateDeadFlag(IWoWLocalPlayer player)
    {
        try
        {
            var bytes1 = player.Bytes1;
            return bytes1 != null
                && bytes1.Length > 0
                && (bytes1[0] & StandStateMask) == StandStateDead;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsStrictAlive(IWoWLocalPlayer player)
        => player.Health > 0 && !HasGhostFlag(player) && !IsStandStateDeadFlag(player);

    private static bool IsGhostState(IWoWLocalPlayer player)
    {
        if (HasGhostFlag(player))
            return true;

        try { return player.InGhostForm; }
        catch { return false; }
    }

    private static bool IsDeadOrGhostState(IWoWLocalPlayer player)
        => player.Health == 0 || HasGhostFlag(player) || IsStandStateDeadFlag(player) || IsGhostState(player);

    internal static string FormatNavigationTraceSummary(NavigationTraceSnapshot trace)
    {
        return $"plan={trace.PlanVersion} reason={trace.LastReplanReason ?? "none"} resolution={trace.LastResolution ?? "none"} " +
               $"idx={trace.CurrentWaypointIndex} active={FormatTracePosition(trace.ActiveWaypoint)} short={trace.IsShortRoute} " +
               $"smooth={trace.SmoothPath} overlay={trace.NearbyObjectCount} route={FormatRouteDecision(trace.RouteDecision)} " +
               $"request={FormatTracePosition(trace.RequestedStart)}->{FormatTracePosition(trace.RequestedDestination)} " +
               $"service={FormatTracePath(trace.ServiceWaypoints, TraceSummaryWaypointLimit)} " +
               $"planned={FormatTracePath(trace.PlannedWaypoints, TraceSummaryWaypointLimit)} " +
               $"samples={FormatTraceSamples(trace.ExecutionSamples, TraceSummarySampleLimit)}";
    }

    private string GetNavigationTraceSummary()
        => FormatNavigationTraceSummary(_navPath.TraceSnapshot);

    private static string FormatTracePath(Position[] path, int limit)
    {
        if (path.Length == 0)
            return "[]";

        var displayCount = Math.Min(path.Length, limit);
        var parts = new string[displayCount + (path.Length > limit ? 1 : 0)];
        for (var i = 0; i < displayCount; i++)
            parts[i] = FormatTracePosition(path[i]);

        if (path.Length > limit)
            parts[^1] = $"+{path.Length - limit} more";

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string FormatTraceSamples(NavigationExecutionSample[] samples, int limit)
    {
        if (samples.Length == 0)
            return "[]";

        var startIndex = Math.Max(0, samples.Length - limit);
        var displayCount = samples.Length - startIndex;
        var parts = new string[displayCount + (startIndex > 0 ? 1 : 0)];
        var offset = 0;
        if (startIndex > 0)
        {
            parts[0] = $"+{startIndex} earlier";
            offset = 1;
        }

        for (var i = 0; i < displayCount; i++)
        {
            var sample = samples[startIndex + i];
            parts[offset + i] = $"p{sample.PlanVersion}:{sample.Resolution}:idx{sample.WaypointIndex}:{FormatTracePosition(sample.CurrentPosition)}->{FormatTracePosition(sample.ReturnedWaypoint)}";
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static string FormatTracePosition(Position? position)
        => position == null
            ? "null"
            : $"({position.X:F1},{position.Y:F1},{position.Z:F1})";

    private static string FormatRouteDecision(NavigationRouteDecision decision)
    {
        if (!decision.HasPath)
            return "none";

        var support = decision.IsSupported ? "supported" : "unsupported";
        return $"{support}:{decision.MaxAffordance}:cost={decision.EstimatedCost:F1}:alt={decision.AlternateEvaluated}/{decision.AlternateSelected}:retarget={decision.EndpointRetargeted}";
    }

    private static Position BuildCorpseNavigationTarget(Position currentPosition, Position corpsePosition)
    {
        var corpseDeltaZ = MathF.Abs(currentPosition.Z - corpsePosition.Z);
        if (corpseDeltaZ <= MaxCorpseZDeltaForNavigation)
            return corpsePosition;

        // Some server builds can report stale corpse Z during ghost transitions.
        // Keep corpse X/Y authoritative while clamping target Z to current ghost Z
        // so pathfinding stays horizontal and reclaim gating can proceed by range.
        return new Position(corpsePosition.X, corpsePosition.Y, currentPosition.Z);
    }

    protected override NavigationTraceSnapshot? GetDiagnosticNavigationTraceSnapshot()
        => _navPath.TraceSnapshot;

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            BotRunnerService.DiagLog($"[RETRIEVE_CORPSE] Update: PlayerUnavailable player={player != null} pos={player?.Position != null}");
            PopTask("PlayerUnavailable");
            return;
        }

        if (DateTime.UtcNow - _startTime > TaskTimeout)
        {
            Log.Warning("[RETRIEVE_CORPSE] Timed out navigating to corpse");
            BotRunnerService.DiagLog(
                $"[RETRIEVE_CORPSE] timeout corpse=({corpsePosition.X:F1},{corpsePosition.Y:F1},{corpsePosition.Z:F1}) trace={GetNavigationTraceSummary()}");
            ObjectManager.StopAllMovement();
            PopTask("Timeout");
            return;
        }

        // P21.25: Spirit healer auto-navigation — if corpse is very far away (>200y)
        // and a spirit healer NPC is nearby (<50y), use spirit healer resurrection
        // instead of running back (accepts 25% durability loss + rez sickness).
        var corpseHorizontalDistance = player.Position.DistanceTo2D(corpsePosition);
        if (corpseHorizontalDistance > 200f && !_triedSpiritHealer)
        {
            var spiritHealer = ObjectManager.Units
                .FirstOrDefault(u => u.Name != null
                    && u.Name.Contains("Spirit Healer", StringComparison.OrdinalIgnoreCase)
                    && u.Position.DistanceTo(player.Position) < 50f);

            if (spiritHealer != null)
            {
                Log.Information("[RETRIEVE_CORPSE] Corpse is {Dist:F0}y away. Using nearby spirit healer instead.",
                    corpseHorizontalDistance);
                spiritHealer.Interact(); // CMSG_SPIRIT_HEALER_ACTIVATE
                _triedSpiritHealer = true;
                // Wait a tick for resurrection to process
                return;
            }
        }
        var corpseDeltaZ = MathF.Abs(player.Position.Z - corpsePosition.Z);
        var corpseNavTarget = BuildCorpseNavigationTarget(player.Position, corpsePosition);

        if (DateTime.UtcNow - _lastTickDiagUtc >= TimeSpan.FromSeconds(3))
        {
            var isGhostNow = IsGhostState(player);
            BotRunnerService.DiagLog(
                $"[RETRIEVE_CORPSE] tick pos=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1}) corpse=({corpsePosition.X:F1},{corpsePosition.Y:F1},{corpsePosition.Z:F1}) dist2D={corpseHorizontalDistance:F1} zDelta={corpseDeltaZ:F1} ghost={isGhostNow} hp={player.Health}/{player.MaxHealth} flags=0x{(uint)player.PlayerFlags:X} noPath={_noPathSinceUtc != null} elapsed={(DateTime.UtcNow - _startTime).TotalSeconds:F0}s");
            _lastTickDiagUtc = DateTime.UtcNow;
        }

        // Dynamic 2D approach range: solve for max 2D dist such that 3D dist < (39 - 5) = 34y.
        // In flat terrain (zDelta≈0) this yields ~34y. In Orgrimmar (zDelta≈22y) → ~25y.
        // If zDelta alone exceeds the safe radius, walk as close as physically possible.
        var effectiveRadius = ServerReclaimRadius3D - ReclaimSafetyMargin;
        var maxDist2DSquared = effectiveRadius * effectiveRadius - corpseDeltaZ * corpseDeltaZ;
        var retrieveRange = maxDist2DSquared > 0
            ? MathF.Max(MathF.Sqrt(maxDist2DSquared), MinRetrieveRange2D)
            : MinRetrieveRange2D;

        if (corpseHorizontalDistance > retrieveRange)
        {
            _stoppedForRetrieval = false;
            if (!_loggedPathfindingMode)
            {
                Log.Information("[RETRIEVE_CORPSE] Using pathfinding toward corpse at ({X:F0}, {Y:F0}, {Z:F0})",
                    corpsePosition.X, corpsePosition.Y, corpsePosition.Z);
                _loggedPathfindingMode = true;
            }

            var now = DateTime.UtcNow;
            var waypoint = _navPath.GetNextWaypoint(
                player.Position,
                corpseNavTarget,
                player.MapId,
                allowDirectFallback: false,
                minWaypointDistance: MinimumDriveWaypointDistance);

            if (waypoint == null)
            {
                _noPathSinceUtc ??= now;
                if (now - _lastCooldownLog >= CooldownLogInterval)
                {
                    var stalledSeconds = (int)(now - _noPathSinceUtc.Value).TotalSeconds;
                    Log.Warning("[RETRIEVE_CORPSE] No pathfinding route for corpse target ({X:F1}, {Y:F1}, {Z:F1}) stalledFor={Seconds}s distance2D={Distance2D:F1} zDelta={ZDelta:F1} trace={Trace}",
                        corpseNavTarget.X, corpseNavTarget.Y, corpseNavTarget.Z, stalledSeconds, corpseHorizontalDistance, corpseDeltaZ, GetNavigationTraceSummary());
                    BotRunnerService.DiagLog(
                        $"[RETRIEVE_CORPSE] no_path corpse=({corpseNavTarget.X:F1},{corpseNavTarget.Y:F1},{corpseNavTarget.Z:F1}) stalledFor={stalledSeconds}s distance2D={corpseHorizontalDistance:F1} zDelta={corpseDeltaZ:F1} trace={GetNavigationTraceSummary()}");
                    _lastCooldownLog = now;
                }

                if (now - _noPathSinceUtc.Value > NoPathTimeout)
                {
                    Log.Warning("[RETRIEVE_CORPSE] No pathfinding route after {Seconds}s; aborting corpse run task. trace={Trace}",
                        (int)NoPathTimeout.TotalSeconds, GetNavigationTraceSummary());
                    BotRunnerService.DiagLog(
                        $"[RETRIEVE_CORPSE] no_path_timeout seconds={(int)NoPathTimeout.TotalSeconds} trace={GetNavigationTraceSummary()}");
                    ObjectManager.StopAllMovement();
                    PopTask("NoPathTimeout");
                    return;
                }

                ObjectManager.StopAllMovement();
                return;
            }

            _noPathSinceUtc = null;
            if (now - _lastWaypointDriveLogUtc >= TimeSpan.FromSeconds(2))
            {
                var waypointDistance = player.Position.DistanceTo(waypoint);
                var trace = _navPath.TraceSnapshot;
                Log.Information("[RETRIEVE_CORPSE] Driving path waypoint ({X:F1}, {Y:F1}, {Z:F1}) waypointDist={WaypointDist:F1} corpseDist2D={CorpseDist2D:F1} plan={Plan} idx={Index} reason={Reason}",
                    waypoint.X, waypoint.Y, waypoint.Z, waypointDistance, corpseHorizontalDistance, trace.PlanVersion, trace.CurrentWaypointIndex, trace.LastReplanReason ?? "none");
                BotRunnerService.DiagLog(
                    $"[RETRIEVE_CORPSE] waypoint=({waypoint.X:F1},{waypoint.Y:F1},{waypoint.Z:F1}) wpDist={waypointDistance:F1} corpse2D={corpseHorizontalDistance:F1} plan={trace.PlanVersion} idx={trace.CurrentWaypointIndex} facing={player.Facing:F3} reason={trace.LastReplanReason ?? "none"}");
                _lastWaypointDriveLogUtc = now;
            }
            // Drive the active waypoint continuously; MoveToward already normalizes direction flags.
            BotRunnerService.DiagLog(
                $"[RETRIEVE_CORPSE] MoveToward wp=({waypoint.X:F1},{waypoint.Y:F1},{waypoint.Z:F1}) from=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1}) facing={player.Facing:F3}");
            ObjectManager.MoveToward(waypoint);
            return;
        }

        _noPathSinceUtc = null;

        // Only send ForceStopImmediate once when entering retrieve range.
        // Calling it every 100ms tick floods MSG_MOVE_STOP packets (~170 in 27s)
        // and triggers VMaNGOS anti-cheat disconnect.
        if (!_stoppedForRetrieval)
        {
            ObjectManager.ForceStopImmediate();
            _stoppedForRetrieval = true;
        }

        if (IsStrictAlive(player))
        {
            Log.Information("[RETRIEVE_CORPSE] Player no longer in ghost form; retrieval complete.");
            PopTask("AliveAfterRetrieve");
            return;
        }

        var isGhost = IsGhostState(player);
        if (!isGhost)
        {
            _nonGhostSinceUtc ??= DateTime.UtcNow;
            if (DateTime.UtcNow - _nonGhostSinceUtc.Value > TimeSpan.FromSeconds(2))
            {
                var isDeadOrGhost = IsDeadOrGhostState(player);
                Log.Information("[RETRIEVE_CORPSE] Ghost state unavailable (deadOrGhost={DeadOrGhost}, health={Health}); yielding.",
                    isDeadOrGhost, player.Health);
                PopTask(isDeadOrGhost ? "GhostStateUnavailable" : "NoLongerDeadOrGhost");
            }
            return;
        }

        _nonGhostSinceUtc = null;

        var reclaimDelay = player.CorpseRecoveryDelaySeconds;
        if (reclaimDelay > 0)
        {
            if (DateTime.UtcNow - _lastCooldownLog >= CooldownLogInterval)
            {
                Log.Information("[RETRIEVE_CORPSE] Waiting for corpse reclaim cooldown: {Seconds}s remaining", reclaimDelay);
                _lastCooldownLog = DateTime.UtcNow;
            }
            return;
        }

        if (DateTime.UtcNow - _lastReclaimAttempt < ReclaimRetryInterval)
            return;

        _lastReclaimAttempt = DateTime.UtcNow;
        ObjectManager.RetrieveCorpse();
        var dist3D = MathF.Sqrt(corpseHorizontalDistance * corpseHorizontalDistance + corpseDeltaZ * corpseDeltaZ);
        Log.Information("[RETRIEVE_CORPSE] Sent reclaim request dist2D={Distance2D:F1} zDelta={ZDelta:F1} dist3D={Dist3D:F1} retrieveRange={Range:F1}",
            corpseHorizontalDistance, corpseDeltaZ, dist3D, retrieveRange);
    }
}

