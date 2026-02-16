using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Abstractions.I;

/// <summary>
/// Interface for bot runners that execute test scenarios.
/// Provides default implementations for backward compatibility with legacy runners.
/// </summary>
public interface IBotRunner : IAsyncDisposable
{
    /// <summary>
    /// Connects to the game server.
    /// </summary>
    Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken);

    /// <summary>
    /// Disconnects from the game server.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Prepares the server state before test execution (e.g., teleport player, spawn NPCs).
    /// </summary>
    Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Resets the server state after test execution (e.g., despawn NPCs, reset player state).
    /// </summary>
    Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the recording target for screen capture.
    /// </summary>
    Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes the main test logic.
    /// </summary>
    Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Shuts down any UI components.
    /// </summary>
    Task ShutdownUiAsync(CancellationToken cancellationToken);

    #region Default implementations for backward compatibility

    /// <summary>
    /// Legacy method without context - delegates to context-aware version.
    /// Default implementation creates a minimal context.
    /// </summary>
    Task PrepareServerStateAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Legacy method without context - delegates to context-aware version.
    /// Default implementation does nothing.
    /// </summary>
    Task ResetServerStateAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Legacy method without context - delegates to context-aware version.
    /// Default implementation does nothing.
    /// </summary>
    Task RunTestAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    #endregion
}
