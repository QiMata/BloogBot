using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Movement;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using System;
using System.Collections.Generic;

namespace BotRunner.Tasks;

/// <summary>
/// Base class for bot tasks providing common functionality.
/// </summary>
public abstract class BotTask(IBotContext botContext) : INavigationTraceProvider
{
    protected readonly IBotContext BotContext = botContext;

    private Microsoft.Extensions.Logging.ILogger? _logger;
    /// <summary>
    /// Per-task logger created from IBotContext.LoggerFactory. Falls back to NullLogger.
    /// </summary>
    protected Microsoft.Extensions.Logging.ILogger Logger => _logger ??= (BotContext.LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger(GetType().Name);

    /// <summary>
    /// Access to the object manager for game state.
    /// </summary>
    protected IObjectManager ObjectManager => BotContext.ObjectManager;

    /// <summary>
    /// The bot task stack.
    /// </summary>
    protected Stack<IBotTask> BotTasks => BotContext.BotTasks;

    /// <summary>
    /// Access to the dependency container.
    /// </summary>
    protected IDependencyContainer Container => BotContext.Container;

    /// <summary>
    /// Per-bot behavior configuration.
    /// </summary>
    protected BotBehaviorConfig Config => BotContext.Config;

    /// <summary>
    /// Event handler for game events (combat, parry, slam, etc.).
    /// </summary>
    protected IWoWEventHandler EventHandler => BotContext.EventHandler;

    /// <summary>
    /// Utility for tracking wait times.
    /// </summary>
    protected static WaitTracker Wait { get; } = new();

    private NavigationPath? _navPath;

    /// <summary>
    /// Exposes the cached NavigationPath for trace/diagnostic access in subclasses.
    /// Returns null if no navigation has been attempted yet.
    /// </summary>
    protected NavigationPath? NavPath => _navPath;

    /// <summary>
    /// Move toward a destination using cached pathfinding. Only re-queries the pathfinding
    /// service when the destination changes significantly or a cooldown expires.
    /// Call ClearNavigation() when switching targets.
    /// </summary>
    protected void NavigateToward(Position destination)
        => TryNavigateToward(destination);

    /// <summary>
    /// Move toward a destination using cached pathfinding and report whether a waypoint was found.
    /// </summary>
    protected bool TryNavigateToward(Position destination, bool allowDirectFallback = false)
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            Log.Warning("[NAV-DIAG] TryNavigateToward: player or position is null");
            return false;
        }

        if (_navPath == null)
        {
            _navPath = NavigationPathFactory.Create(
                Container.PathfindingClient,
                ObjectManager,
                NavigationRoutePolicy.Standard);
        }

        var wallNormal = ObjectManager.PhysicsWallNormal2D;
        var waypoint = _navPath.GetNextWaypoint(
            player.Position,
            destination,
            player.MapId,
            allowDirectFallback: allowDirectFallback,
            physicsHitWall: ObjectManager.PhysicsHitWall,
            wallNormalX: wallNormal.X,
            wallNormalY: wallNormal.Y,
            blockedFraction: ObjectManager.PhysicsBlockedFraction,
            currentTransportGuid: player.TransportGuid);

        if (_navPath.ShouldHoldPositionForTransport(player.Position, waypoint))
        {
            ObjectManager.StopAllMovement();
            return true;
        }

        if (waypoint != null)
        {
            // BotRunner owns corridor/waypoint execution. MovementController only consumes
            // the current steering target for physics/collision parity against WoW.exe.
            ObjectManager.MoveToward(waypoint);

            return true;
        }

        Log.Warning("[NAV-DIAG] TryNavigateToward: GetNextWaypoint returned null. " +
            "pos=({PosX:F1},{PosY:F1},{PosZ:F1}), dest=({DestX:F1},{DestY:F1},{DestZ:F1}), map={Map}",
            player.Position.X, player.Position.Y, player.Position.Z,
            destination.X, destination.Y, destination.Z, player.MapId);
        ObjectManager.StopAllMovement();
        return false;
    }

    /// <summary>
    /// Clear the cached navigation path. Call when the target dies or changes
    /// so the next NavigateToward() calculates a fresh path.
    /// </summary>
    protected void ClearNavigation() => _navPath?.Clear();

    /// <summary>
    /// Pop the current task with a reason code for live diagnostics.
    /// </summary>
    protected void PopTask(string reason)
    {
        if (BotTasks.Count == 0)
            return;

        var top = BotTasks.Peek();
        BotContext.AddDiagnosticMessage($"[TASK] {top.GetType().Name} pop reason={reason}");
        BotRunnerService.DiagLog($"[TASK-POP] task={top.GetType().Name} reason={reason} remaining={BotTasks.Count - 1}");
        BotTasks.Pop();
        Log.Information("[TASK-POP] task={Task} reason={Reason} remaining={Remaining}",
            top.GetType().Name, reason, BotTasks.Count);
    }

    public NavigationTraceSnapshot? GetNavigationTraceSnapshot()
        => GetDiagnosticNavigationTraceSnapshot();

    protected virtual NavigationTraceSnapshot? GetDiagnosticNavigationTraceSnapshot()
        => NavPath?.TraceSnapshot;
}

/// <summary>
/// Utility class for tracking time-based delays in bot tasks.
/// </summary>
public class WaitTracker
{
    private readonly Dictionary<string, DateTime> _waitTimes = new();

    /// <summary>
    /// Check if enough time has passed since the last call with this key.
    /// </summary>
    /// <param name="key">Unique identifier for this wait.</param>
    /// <param name="milliseconds">Minimum milliseconds between calls.</param>
    /// <param name="resetOnSuccess">Reset the timer if the wait has elapsed.</param>
    /// <returns>True if the wait period has elapsed.</returns>
    public bool For(string key, int milliseconds, bool resetOnSuccess = false)
    {
        if (!_waitTimes.TryGetValue(key, out var lastTime))
        {
            _waitTimes[key] = DateTime.Now;
            return true;
        }

        if ((DateTime.Now - lastTime).TotalMilliseconds >= milliseconds)
        {
            if (resetOnSuccess)
                _waitTimes[key] = DateTime.Now;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Remove a specific wait key.
    /// </summary>
    public void Remove(string key) => _waitTimes.Remove(key);

    /// <summary>
    /// Clear all wait trackers.
    /// </summary>
    public void RemoveAll() => _waitTimes.Clear();
}
