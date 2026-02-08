using RecordedTests.Shared.Abstractions.I;

namespace RecordedTests.Shared.DesiredState;

/// <summary>
/// Server desired state implementation that uses delegate callbacks for setup and teardown.
/// Provides maximum flexibility for complex state management scenarios.
/// </summary>
public sealed class DelegateServerDesiredState : IServerDesiredState
{
    private readonly Func<IBotRunner, IRecordedTestContext, CancellationToken, Task>? _applyAsync;
    private readonly Func<IBotRunner, IRecordedTestContext, CancellationToken, Task>? _revertAsync;

    /// <summary>
    /// Gets the name of this desired state for logging and identification.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates a new delegate-based desired state.
    /// </summary>
    /// <param name="name">Name of this desired state for logging.</param>
    /// <param name="applyAsync">Delegate to execute during apply phase. Can be null for no-op.</param>
    /// <param name="revertAsync">Delegate to execute during revert phase. Can be null for no-op.</param>
    public DelegateServerDesiredState(
        string name,
        Func<IBotRunner, IRecordedTestContext, CancellationToken, Task>? applyAsync = null,
        Func<IBotRunner, IRecordedTestContext, CancellationToken, Task>? revertAsync = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _applyAsync = applyAsync;
        _revertAsync = revertAsync;
    }

    public async Task ApplyAsync(IBotRunner runner, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        if (_applyAsync != null)
        {
            await _applyAsync(runner, context, cancellationToken);
        }
    }

    public async Task RevertAsync(IBotRunner runner, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        if (_revertAsync != null)
        {
            await _revertAsync(runner, context, cancellationToken);
        }
    }
}
