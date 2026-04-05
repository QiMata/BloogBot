using BloogBot.AI.Observable;
using BloogBot.AI.States;
using Microsoft.SemanticKernel;

namespace BloogBot.AI.Semantic;

/// <summary>
/// Coordinates the Semantic Kernel plugins based on the current bot activity.
/// Subscribes to the state observable to automatically update plugins when activity changes.
/// </summary>
public sealed class KernelCoordinator : IDisposable
{
    private readonly Kernel _kernel;
    private readonly PluginCatalog _catalog;
    private readonly IDisposable? _subscription;
    private bool _disposed;

    /// <summary>
    /// Creates a KernelCoordinator that manually receives activity changes.
    /// Use this constructor when not using the observable pattern.
    /// </summary>
    public KernelCoordinator(Kernel kernel, PluginCatalog catalog)
    {
        _kernel = kernel;
        _catalog = catalog;
    }

    /// <summary>
    /// Creates a KernelCoordinator that automatically subscribes to state changes.
    /// Plugins are updated whenever the activity changes.
    /// </summary>
    public KernelCoordinator(Kernel kernel, PluginCatalog catalog, IBotStateObservable stateObservable)
    {
        _kernel = kernel;
        _catalog = catalog;

        // Subscribe to activity changes
        _subscription = stateObservable.ActivityChanged.Subscribe(OnStateChanged);

        // Initialize with current state
        UpdatePlugins(stateObservable.CurrentState.Activity);
    }

    /// <summary>
    /// Handles activity changes from the observable.
    /// </summary>
    private void OnStateChanged(StateChangeEvent stateChange)
    {
        UpdatePlugins(stateChange.Activity);
    }

    /// <summary>
    /// Manually notify of an activity change.
    /// Use when not using the observable pattern.
    /// </summary>
    public void OnActivityChanged(BotActivity newActivity)
    {
        UpdatePlugins(newActivity);
    }

    /// <summary>
    /// Updates the kernel plugins for the specified activity.
    /// </summary>
    private void UpdatePlugins(BotActivity activity)
    {
        _kernel.Plugins.Clear();
        foreach (var p in _catalog.For(activity))
            _kernel.Plugins.Add(p);
    }

    /// <summary>
    /// Disposes the subscription to the state observable.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _subscription?.Dispose();
    }
}
