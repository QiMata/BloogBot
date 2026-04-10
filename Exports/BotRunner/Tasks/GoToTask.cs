using BotRunner.Interfaces;
using BotRunner.Movement;
using GameData.Core.Models;
using Serilog;
using System;

namespace BotRunner.Tasks;

/// <summary>
/// Persistent GoTo task that navigates the bot to a target position.
/// Unlike the ephemeral BuildGoToSequence behavior tree node, this task
/// persists on the _botTasks stack across poll cycles, preserving its
/// NavigationPath state (waypoints, corridor, stuck detection).
///
/// Supports interruption: if a combat task pushes on top, GoToTask
/// pauses. When combat pops, GoToTask resumes from current position.
/// </summary>
public class GoToTask : BotTask, IBotTask
{
    private Position _target;
    private float _tolerance;
    private NavigationPath? _navPath;
    private DateTime? _noPathSinceUtc;
    private DateTime _lastNoPathLogUtc = DateTime.MinValue;
    private const double NoPathTimeoutSec = 30.0;
    private const double DirectFallbackAfterNoPathSec = 5.0;
    private const float PositionMatchEpsilon = 0.5f;
    private const float ToleranceMatchEpsilon = 0.1f;
    private int _lastLoggedRoutePlanVersion;

    public GoToTask(IBotContext botContext, float x, float y, float z, float tolerance = 3f)
        : base(botContext)
    {
        _target = new Position(x, y, z);
        _tolerance = tolerance > 0 ? tolerance : 3f;
    }

    internal Position Target => _target;
    internal float Tolerance => _tolerance;

    internal bool MatchesTarget(Position target, float tolerance)
    {
        return _target.DistanceTo2D(target) <= PositionMatchEpsilon
            && Math.Abs(_target.Z - target.Z) <= PositionMatchEpsilon
            && Math.Abs(_tolerance - tolerance) <= ToleranceMatchEpsilon;
    }

    internal void Retarget(Position target, float tolerance)
    {
        _target = target;
        _tolerance = tolerance > 0 ? tolerance : 3f;
        _navPath?.Clear();
        _noPathSinceUtc = null;
        _lastNoPathLogUtc = DateTime.MinValue;
        _lastLoggedRoutePlanVersion = 0;
    }

    private int _updateCount;

    public void Update()
    {
        _updateCount++;
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            if (_updateCount % 50 == 1) Log.Warning("[GOTO-TASK] Update #{Count}: player/position null", _updateCount);
            return;
        }

        if (_updateCount <= 3 || _updateCount % 100 == 0)
        {
            Log.Warning("[GOTO-TASK] Update #{Count}: pos=({X:F0},{Y:F0},{Z:F0}) target=({TX:F0},{TY:F0},{TZ:F0}) dist2D={D:F0} map={Map}",
                _updateCount, player.Position.X, player.Position.Y, player.Position.Z,
                _target.X, _target.Y, _target.Z, player.Position.DistanceTo2D(_target),
                (player as GameData.Core.Interfaces.IWoWPlayer)?.MapId ?? 0);
        }

        // Arrived?
        if (player.Position.DistanceTo2D(_target) < _tolerance)
        {
            ObjectManager.StopAllMovement();
            _navPath?.Clear();
            Log.Warning("[GOTO-TASK] Arrived at ({X:F0},{Y:F0},{Z:F0}) dist2D={Dist:F1}",
                _target.X, _target.Y, _target.Z, player.Position.DistanceTo2D(_target));
            base.PopTask("arrived");
            return;
        }

        // Create navigation path once — persists across Update() calls
        if (_navPath == null)
        {
            var pfClient = Container.PathfindingClient;
            _navPath = NavigationPathFactory.Create(
                pfClient,
                ObjectManager,
                NavigationRoutePolicy.Standard);
        }

        if (player.RunSpeed > 0)
            _navPath.UpdateCharacterSpeed(player.RunSpeed);

        // Physics wall contact hint for stuck detection
        bool hitWall = false;
        float wnx = 0f, wny = 0f, bf = 1f;
        if (ObjectManager is WoWSharpClient.WoWSharpObjectManager wsOm)
        {
            hitWall = wsOm.PhysicsHitWall;
            var wn = wsOm.PhysicsWallNormal2D;
            wnx = wn.X; wny = wn.Y;
            bf = wsOm.PhysicsBlockedFraction;
        }

        try
        {
            var now = DateTime.UtcNow;
            var noPathElapsedSec = _noPathSinceUtc.HasValue
                ? (now - _noPathSinceUtc.Value).TotalSeconds
                : 0.0;
            var allowDirectFallback = noPathElapsedSec >= DirectFallbackAfterNoPathSec;

            var waypoint = _navPath.GetNextWaypoint(
                player.Position, _target, player.MapId,
                allowDirectFallback: allowDirectFallback,
                physicsHitWall: hitWall,
                wallNormalX: wnx, wallNormalY: wny,
                blockedFraction: bf);

            if (waypoint == null)
            {
                ObjectManager.StopAllMovement();
                _noPathSinceUtc ??= now;
                EmitRouteDecisionIfUpdated();

                if (now - _lastNoPathLogUtc > TimeSpan.FromSeconds(5))
                {
                    Log.Warning("[GOTO-TASK] No path to ({X:F0},{Y:F0},{Z:F0}) for {Sec:F0}s",
                        _target.X, _target.Y, _target.Z,
                        (now - _noPathSinceUtc.Value).TotalSeconds);
                    _lastNoPathLogUtc = now;
                }

                if ((now - _noPathSinceUtc.Value).TotalSeconds > NoPathTimeoutSec)
                {
                    Log.Warning("[GOTO-TASK] No path timeout ({Sec}s) — giving up", NoPathTimeoutSec);
                    base.PopTask("no_path_timeout");
                }
                return;
            }

            _noPathSinceUtc = null;
            EmitRouteDecisionIfUpdated();

            var dx = waypoint.X - player.Position.X;
            var dy = waypoint.Y - player.Position.Y;
            var facing = MathF.Atan2(dy, dx);

            ObjectManager.MoveToward(waypoint, facing);
        }
        catch (Exception ex)
        {
            Log.Warning("[GOTO-TASK] Navigation error: {Msg}", ex.Message);
        }
    }

    private void EmitRouteDecisionIfUpdated()
    {
        if (_navPath == null)
            return;

        var trace = _navPath.TraceSnapshot;
        if (trace.PlanVersion <= 0 || trace.PlanVersion == _lastLoggedRoutePlanVersion)
            return;

        _lastLoggedRoutePlanVersion = trace.PlanVersion;
        var decision = trace.RouteDecision;
        var summary = decision.HasPath
            ? $"{(decision.IsSupported ? "supported" : "unsupported")}:{decision.MaxAffordance}:cost={decision.EstimatedCost:F1}:alt={decision.AlternateEvaluated}/{decision.AlternateSelected}:retarget={decision.EndpointRetargeted}"
            : "none";

        Log.Information(
            "[GOTO-TASK] Route plan={Plan} summary={Summary} drops={Drops} cliffs={Cliffs} vertical={Vertical}",
            trace.PlanVersion,
            summary,
            trace.Affordances.DropCount,
            trace.Affordances.CliffCount,
            trace.Affordances.VerticalCount);

        BotContext.AddDiagnosticMessage(
            $"[GOTO_ROUTE] plan={trace.PlanVersion} route={summary} drops={trace.Affordances.DropCount} cliffs={trace.Affordances.CliffCount} vertical={trace.Affordances.VerticalCount}");
    }

}
