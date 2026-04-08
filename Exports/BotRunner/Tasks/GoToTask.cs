using BotRunner.Constants;
using BotRunner.Helpers;
using BotRunner.Interfaces;
using BotRunner.Movement;
using GameData.Core.Constants;
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
    private readonly Position _target;
    private readonly float _tolerance;
    private NavigationPath? _navPath;
    private DateTime? _noPathSinceUtc;
    private DateTime _lastNoPathLogUtc = DateTime.MinValue;
    private const double NoPathTimeoutSec = BotTaskTimeouts.NoPathTimeoutSec;

    public GoToTask(IBotContext botContext, float x, float y, float z, float tolerance = 3f)
        : base(botContext)
    {
        _target = new Position(x, y, z);
        _tolerance = tolerance > 0 ? tolerance : 3f;
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
            PopTask("arrived");
            return;
        }

        // Create navigation path once — persists across Update() calls
        if (_navPath == null)
        {
            _navPath = NavigationPathFactory.Create(Container.PathfindingClient, player, ObjectManager);
        }

        if (player.RunSpeed > 0)
            _navPath.UpdateCharacterSpeed(player.RunSpeed);

        // Physics wall contact hint for stuck detection
        var physics = PhysicsStateHelper.GetPhysicsState(ObjectManager);

        try
        {
            var waypoint = _navPath.GetNextWaypoint(
                player.Position, _target, player.MapId,
                allowDirectFallback: false,
                physicsHitWall: physics.HitWall,
                wallNormalX: physics.NormalX, wallNormalY: physics.NormalY,
                blockedFraction: physics.BlockedFraction);

            if (waypoint == null)
            {
                ObjectManager.StopAllMovement();
                _noPathSinceUtc ??= DateTime.UtcNow;

                if (DateTime.UtcNow - _lastNoPathLogUtc > TimeSpan.FromSeconds(5))
                {
                    Log.Warning("[GOTO-TASK] No path to ({X:F0},{Y:F0},{Z:F0}) for {Sec:F0}s",
                        _target.X, _target.Y, _target.Z,
                        (DateTime.UtcNow - _noPathSinceUtc.Value).TotalSeconds);
                    _lastNoPathLogUtc = DateTime.UtcNow;
                }

                if ((DateTime.UtcNow - _noPathSinceUtc.Value).TotalSeconds > NoPathTimeoutSec)
                {
                    Log.Warning("[GOTO-TASK] No path timeout ({Sec}s) — giving up", NoPathTimeoutSec);
                    PopTask("no_path_timeout");
                }
                return;
            }

            _noPathSinceUtc = null;

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

    private void PopTask(string reason)
    {
        Log.Debug("[GOTO-TASK] Popping: {Reason}", reason);
        BotTasks.Pop();
    }
}
