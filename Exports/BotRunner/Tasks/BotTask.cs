using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Movement;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Tasks;

/// <summary>
/// Base class for bot tasks providing common functionality. Implements the
/// Phase 1 <see cref="IBotTask"/> contract via a synchronous shim per R25:
/// <see cref="TickAsync"/> forwards to a virtual <see cref="OnTick"/> whose
/// default body invokes the legacy <c>void Update()</c> defined on the
/// concrete subclass (cached via reflection). Family slots (S1.4..S1.13) may
/// override <see cref="TickAsync"/> directly when migrating a representative
/// task to the native async contract.
/// </summary>
public abstract class BotTask(IBotContext botContext) : INavigationTraceProvider, IBotTask
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
    private NavigationRoutePolicy _navPathPolicy = NavigationRoutePolicy.Standard;

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
    protected bool TryNavigateToward(
        Position destination,
        bool allowDirectFallback = false,
        NavigationRoutePolicy routePolicy = NavigationRoutePolicy.Standard,
        float minWaypointDistance = 0f,
        bool allowDirectRecovery = false)
    {
        var traceImmediateNavigation = routePolicy == NavigationRoutePolicy.LongTravel;
        if (traceImmediateNavigation)
        {
            BotContext.AddImmediateDiagnostic(
                $"[NAV_EXEC] try-enter route={routePolicy} dest=({destination.X:F1},{destination.Y:F1},{destination.Z:F1})");
        }

        var player = ObjectManager.Player;
        if (traceImmediateNavigation)
        {
            var playerSummary = player?.Position == null
                ? "null"
                : $"map={player.MapId} pos=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1})";
            BotContext.AddImmediateDiagnostic($"[NAV_EXEC] player-ready {playerSummary}");
        }

        if (player?.Position == null)
        {
            Log.Warning("[NAV-DIAG] TryNavigateToward: player or position is null");
            return false;
        }

        if (_navPath == null || _navPathPolicy != routePolicy)
        {
            if (traceImmediateNavigation)
            {
                BotContext.AddImmediateDiagnostic(
                    $"[NAV_EXEC] navpath-create enter cached={_navPath != null} policy={_navPathPolicy} next={routePolicy}");
            }

            _navPath = NavigationPathFactory.Create(
                Container.PathfindingClient,
                ObjectManager,
                routePolicy,
                diagnosticSink: traceImmediateNavigation
                    ? BotContext.AddImmediateDiagnostic
                    : BotContext.AddDiagnosticMessage);
            _navPathPolicy = routePolicy;

            if (traceImmediateNavigation)
            {
                BotContext.AddImmediateDiagnostic(
                    $"[NAV_EXEC] navpath-create exit policy={_navPathPolicy} created={_navPath != null}");
            }
        }

        if (traceImmediateNavigation)
        {
            BotContext.AddImmediateDiagnostic("[NAV_EXEC] physics-read enter");
        }
        var wallNormal = ObjectManager.PhysicsWallNormal2D;
        if (traceImmediateNavigation)
        {
            BotContext.AddImmediateDiagnostic(
                $"[NAV_EXEC] physics-read exit hitWall={ObjectManager.PhysicsHitWall} " +
                $"blocked={ObjectManager.PhysicsBlockedFraction:F2} normal=({wallNormal.X:F2},{wallNormal.Y:F2})");
            BotContext.AddImmediateDiagnostic(
                $"[NAV_EXEC] waypoint-query enter map={player.MapId} start=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1}) " +
                $"dest=({destination.X:F1},{destination.Y:F1},{destination.Z:F1})");
        }

        var waypointStopwatch = traceImmediateNavigation ? Stopwatch.StartNew() : null;
        var waypoint = _navPath.GetNextWaypoint(
            player.Position,
            destination,
            player.MapId,
            allowDirectFallback: allowDirectFallback,
            minWaypointDistance: minWaypointDistance,
            physicsHitWall: ObjectManager.PhysicsHitWall,
            wallNormalX: wallNormal.X,
            wallNormalY: wallNormal.Y,
            blockedFraction: ObjectManager.PhysicsBlockedFraction,
            currentTransportGuid: player.TransportGuid,
            allowDirectRecovery: allowDirectRecovery);
        waypointStopwatch?.Stop();
        if (traceImmediateNavigation)
        {
            var waypointSummary = waypoint == null
                ? "null"
                : $"({waypoint.X:F1},{waypoint.Y:F1},{waypoint.Z:F1})";
            BotContext.AddImmediateDiagnostic(
                $"[NAV_EXEC] waypoint-query exit elapsedMs={waypointStopwatch!.ElapsedMilliseconds} waypoint={waypointSummary}");
        }

        if (_navPath.ShouldHoldPositionForTransport(player.Position, waypoint))
        {
            ObjectManager.StopAllMovement();
            return true;
        }

        if (waypoint != null)
        {
            // BotRunner owns corridor/waypoint execution. MovementController only consumes
            // the current steering target for physics/collision parity against WoW.exe.
            var facing = player.GetFacingForPosition(waypoint);
            ObjectManager.MoveToward(waypoint, facing);

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

    // -----------------------------------------------------------------
    // Phase 1 IBotTask shim per slot S1.0 (R25 — shim-only migration).
    // Subclasses keep their existing `public void Update()` body unchanged.
    // -----------------------------------------------------------------

    /// <inheritdoc/>
    public virtual string Name => GetType().Name;

    /// <inheritdoc/>
    public BotTaskStatus Status { get; protected set; } = BotTaskStatus.Running;

    /// <summary>
    /// Mark the task as <see cref="BotTaskStatus.Complete"/>. The runner pops
    /// the task on the next tick and fires <see cref="OnPoppedAsync"/>.
    /// </summary>
    protected void MarkComplete() => Status = BotTaskStatus.Complete;

    /// <summary>
    /// Mark the task as <see cref="BotTaskStatus.Failed"/>. The runner pops
    /// the task and consults the parent's <see cref="OnChildFailedAsync"/>
    /// to decide whether to escalate (R24).
    /// </summary>
    protected void MarkFailed() => Status = BotTaskStatus.Failed;

    /// <summary>
    /// Internal escalation hook used by <see cref="TaskStackDriver"/> when a
    /// child task fails and the parent's <see cref="OnChildFailedAsync"/>
    /// returns <c>false</c> (R24 default). Not part of the public task API —
    /// concrete tasks should call <see cref="MarkComplete"/> or
    /// <see cref="MarkFailed"/> instead.
    /// </summary>
    internal void RequestStatusForEscalation(BotTaskStatus status) => Status = status;

    /// <summary>
    /// Per-type reflection cache for the legacy <c>void Update()</c> body.
    /// One <see cref="MethodInfo"/> lookup per concrete subclass; subsequent
    /// invocations are direct-dispatch <see cref="MethodInfo.Invoke(object?,object?[])"/>.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Action<BotTask>> _legacyUpdateInvokers = new();

    /// <inheritdoc/>
    public virtual Task TickAsync(BotTaskContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OnTick(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Default sync tick body — dispatches to the legacy <c>void Update()</c>
    /// declared on the concrete subclass. Family slots overriding
    /// <see cref="TickAsync"/> directly bypass this path entirely.
    /// </summary>
    protected virtual void OnTick(BotTaskContext context)
    {
        var invoker = _legacyUpdateInvokers.GetOrAdd(GetType(), CreateLegacyUpdateInvoker);
        invoker(this);
    }

    private static Action<BotTask> CreateLegacyUpdateInvoker(Type runtimeType)
    {
        var mi = runtimeType.GetMethod(
            "Update",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (mi == null)
            return static _ => { };
        return target => mi.Invoke(target, parameters: null);
    }

    /// <inheritdoc/>
    public virtual Task OnPushedAsync(BotTaskContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal)
        => Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>
    /// R24 — base-class default returns <c>false</c>: parent escalates
    /// (fails too) when a child fails. Override to absorb specific failure
    /// classes.
    /// </remarks>
    public virtual Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason)
        => Task.FromResult(false);
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
