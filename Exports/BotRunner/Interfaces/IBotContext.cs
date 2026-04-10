using BotRunner.Constants;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace BotRunner.Interfaces;

/// <summary>
/// Context provided to bot tasks containing game state and utilities.
/// </summary>
public interface IBotContext
{
    /// <summary>
    /// Logger factory for creating typed loggers in bot tasks.
    /// Returns null when DI is not configured; tasks fall back to NullLoggerFactory.
    /// </summary>
    ILoggerFactory? LoggerFactory { get; }

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

    /// <summary>
    /// Push a diagnostic message into the bot's rolling snapshot message buffer.
    /// Live tests use these markers when runtime behavior is observable but the
    /// snapshot state itself lags behind.
    /// </summary>
    void AddDiagnosticMessage(string message);
}
