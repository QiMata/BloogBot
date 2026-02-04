using BotRunner.Interfaces;
using GameData.Core.Interfaces;

namespace BotRunner.Tasks;

/// <summary>
/// Base class for bot tasks providing common functionality.
/// </summary>
public abstract class BotTask
{
    protected readonly IBotContext BotContext;

    protected BotTask(IBotContext botContext)
    {
        BotContext = botContext;
    }

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
    /// Utility for tracking wait times.
    /// </summary>
    protected static WaitTracker Wait { get; } = new();
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
