using BotRunner.Tests.Helpers;
using System;

namespace BotRunner.Tests;

/// <summary>
/// Defines an xUnit collection that serializes all infrastructure integration tests.
/// Tests in this collection will never run in parallel with each other, preventing
/// multiple StateManager processes, WoW instances, or ObjectManager singletons from
/// conflicting.
/// 
/// All tests that launch WoW or StateManager MUST be in this collection.
/// </summary>
[CollectionDefinition(Name)]
public class InfrastructureTestCollection : ICollectionFixture<InfrastructureTestGuard>
{
    public const string Name = "Infrastructure";
}

/// <summary>
/// Shared guard for the Infrastructure test collection.
/// 
/// Lifecycle:
/// - Created once when the first test in the collection runs.
/// - Kills any lingering WoWStateManager / WoW processes on construction.
/// - Disposed after the last test in the collection completes.
/// 
/// Tests receive this via constructor injection (xUnit ICollectionFixture pattern).
/// Because xunit.runner.json sets parallelizeTestCollections=false, tests within
/// this collection already run sequentially. The guard provides additional safety
/// by cleaning up stale processes between test classes.
/// </summary>
public class InfrastructureTestGuard : IDisposable
{
    private readonly object _lock = new();
    private StateManagerProcessHelper? _currentHelper;

    public InfrastructureTestGuard()
    {
        // Kill any lingering processes from previous test runs on construction.
        // This covers the case where a previous test run crashed without cleanup.
        StateManagerProcessHelper.KillLingeringProcesses(msg => Console.WriteLine($"[InfraGuard] {msg}"));
    }

    /// <summary>
    /// Ensures any previously running StateManager launched by a prior test is stopped,
    /// and kills lingering WoW/StateManager processes. Call this at the start of any test
    /// that will launch its own StateManager.
    /// </summary>
    public void EnsureCleanState(Action<string>? logger = null)
    {
        lock (_lock)
        {
            if (_currentHelper != null)
            {
                logger?.Invoke("Stopping StateManager from previous test...");
                _currentHelper.Stop();
                _currentHelper = null;
            }
        }

        // Belt-and-suspenders: kill by process name in case a test launched directly
        StateManagerProcessHelper.KillLingeringProcesses(logger);
    }

    /// <summary>
    /// Registers a StateManagerProcessHelper so the guard can clean it up
    /// if the test doesn't dispose it properly.
    /// </summary>
    public void RegisterHelper(StateManagerProcessHelper helper)
    {
        lock (_lock)
        {
            _currentHelper = helper;
        }
    }

    /// <summary>
    /// Unregisters the helper after the test has cleaned it up.
    /// </summary>
    public void UnregisterHelper()
    {
        lock (_lock)
        {
            _currentHelper = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _currentHelper?.Stop();
            _currentHelper = null;
        }

        // Final cleanup of any lingering processes
        StateManagerProcessHelper.KillLingeringProcesses(msg => Console.WriteLine($"[InfraGuard:Dispose] {msg}"));
    }
}
