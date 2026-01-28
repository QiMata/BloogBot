using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.DesiredState;

/// <summary>
/// Server desired state implementation that executes GM commands to prepare and reset server state.
/// Requires a GM-capable bot runner with appropriate permissions.
/// </summary>
public sealed class GmCommandServerDesiredState : IServerDesiredState
{
    private readonly string[] _setupCommands;
    private readonly string[] _teardownCommands;
    private readonly ITestLogger? _logger;
    private IGmCommandExecutor? _executor;

    /// <summary>
    /// Creates a new GM command-based desired state.
    /// </summary>
    /// <param name="setupCommands">GM commands to execute during setup/apply phase.</param>
    /// <param name="teardownCommands">GM commands to execute during teardown/revert phase.</param>
    /// <param name="logger">Optional logger for command execution.</param>
    public GmCommandServerDesiredState(
        string[] setupCommands,
        string[] teardownCommands,
        ITestLogger? logger = null)
    {
        _setupCommands = setupCommands ?? Array.Empty<string>();
        _teardownCommands = teardownCommands ?? Array.Empty<string>();
        _logger = logger;
    }

    /// <summary>
    /// Sets the GM command executor. Must be called before ApplyAsync or RevertAsync.
    /// </summary>
    /// <param name="executor">The executor capable of running GM commands</param>
    public void SetExecutor(IGmCommandExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task ApplyAsync(IBotRunner runner, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        if (_executor == null)
            throw new InvalidOperationException(
                "IGmCommandExecutor not set. Call SetExecutor() before ApplyAsync(). " +
                "The foreground bot runner should implement IGmCommandExecutor and be passed to SetExecutor().");

        _logger?.Info($"Applying GM commands for test '{context.TestName}' (setup phase)");

        foreach (var command in _setupCommands)
        {
            _logger?.Info($"  Executing: {command}");
            await _executor.ExecuteCommandAsync(command, cancellationToken);
            // Brief delay to allow command to take effect on server
            await Task.Delay(500, cancellationToken);
        }

        _logger?.Info($"Setup complete for test '{context.TestName}'");
    }

    public async Task RevertAsync(IBotRunner runner, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        if (_executor == null)
            throw new InvalidOperationException(
                "IGmCommandExecutor not set. Call SetExecutor() before RevertAsync(). " +
                "The foreground bot runner should implement IGmCommandExecutor and be passed to SetExecutor().");

        _logger?.Info($"Reverting GM commands for test '{context.TestName}' (teardown phase)");

        foreach (var command in _teardownCommands)
        {
            _logger?.Info($"  Executing: {command}");
            await _executor.ExecuteCommandAsync(command, cancellationToken);
            // Brief delay to allow command to take effect on server
            await Task.Delay(500, cancellationToken);
        }

        _logger?.Info($"Teardown complete for test '{context.TestName}'");
    }
}
