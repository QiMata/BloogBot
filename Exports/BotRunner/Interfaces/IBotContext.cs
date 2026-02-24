using BotRunner.Constants;
using GameData.Core.Interfaces;
using System.Collections.Generic;

namespace BotRunner.Interfaces;

/// <summary>
/// Context provided to bot tasks containing game state and utilities.
/// </summary>
public interface IBotContext
{
    /// <summary>
    /// Access to game objects and player state.
    /// </summary>
    IObjectManager ObjectManager { get; }

    /// <summary>
    /// Stack of bot tasks for state management.
    /// </summary>
    Stack<IBotTask> BotTasks { get; }

    /// <summary>
    /// Container providing pathfinding and other services.
    /// </summary>
    IDependencyContainer Container { get; }

    /// <summary>
    /// Per-bot behavior configuration (rest thresholds, pull range, vendor triggers, etc.).
    /// </summary>
    BotBehaviorConfig Config { get; }

    /// <summary>
    /// Event handler for game events (combat, spells, UI).
    /// </summary>
    IWoWEventHandler EventHandler { get; }
}
