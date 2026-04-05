using BotRunner.Clients;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Movement;

namespace WoWSharpClient;

/// <summary>
/// Per-bot isolation context. Encapsulates all state for a single bot instance.
/// Replaces static singletons (WoWSharpObjectManager.Instance, WoWSharpEventEmitter.Instance,
/// SplineController.Instance) with injected, per-bot instances.
///
/// This is the foundation for multi-bot-per-process (P9.7): each BotContext owns its
/// own ObjectManager, EventEmitter, MovementController, and WoWClient — no shared mutable state.
///
/// Phase 1 (this): container class with references to existing instances.
/// Phase 2 (P9.2-9.4): refactor singletons to instance-based and inject via BotContext.
/// </summary>
public class BotContext : IDisposable
{
    /// <summary>The bot's account name (e.g., "RFCBOT1").</summary>
    public string AccountName { get; }

    /// <summary>The bot's WoW protocol client (auth + world).</summary>
    public WoWClient WoWClient { get; }

    /// <summary>The bot's object manager (tracks all game objects from server updates).</summary>
    public WoWSharpObjectManager ObjectManager { get; }

    /// <summary>The bot's event emitter (game state change events).</summary>
    public WoWSharpEventEmitter EventEmitter { get; }

    /// <summary>The bot's movement controller (physics + heartbeats).</summary>
    public MovementController? MovementController { get; set; }

    /// <summary>The bot's spline controller (server-driven NPC movement).</summary>
    public SplineController SplineController { get; }

    /// <summary>The bot's pathfinding client (A* path requests).</summary>
    public PathfindingClient? PathfindingClient { get; set; }

    /// <summary>Whether this bot context has been fully initialized.</summary>
    public bool IsInitialized => ObjectManager?.Player != null;

    public BotContext(
        string accountName,
        WoWClient wowClient,
        WoWSharpObjectManager objectManager,
        WoWSharpEventEmitter eventEmitter,
        SplineController splineController)
    {
        AccountName = accountName ?? throw new ArgumentNullException(nameof(accountName));
        WoWClient = wowClient ?? throw new ArgumentNullException(nameof(wowClient));
        ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
        EventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
        SplineController = splineController ?? throw new ArgumentNullException(nameof(splineController));
    }

    /// <summary>
    /// Creates a BotContext using the current static singletons.
    /// This is the migration bridge — existing code creates a context from statics,
    /// new code creates instance-based components directly.
    /// </summary>
    public static BotContext FromCurrentSingletons(string accountName, WoWClient wowClient, SplineController splineController)
    {
        return new BotContext(
            accountName,
            wowClient,
            WoWSharpObjectManager.Instance,
            WoWSharpEventEmitter.Instance,
            splineController);
    }

    public void Dispose()
    {
        WoWClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
