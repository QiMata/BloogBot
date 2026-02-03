using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared;

public sealed class DelegateServerDesiredState : IServerDesiredState
{
    private readonly Func<IBotRunner, IRecordedTestContext, CancellationToken, Task> _applyAsync;
    private readonly Func<IBotRunner, IRecordedTestContext, CancellationToken, Task>? _revertAsync;

    public DelegateServerDesiredState(
        string name,
        Func<IBotRunner, IRecordedTestContext, CancellationToken, Task> applyAsync,
        Func<IBotRunner, IRecordedTestContext, CancellationToken, Task>? revertAsync = null)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("State name is required.", nameof(name))
            : name;
        _applyAsync = applyAsync ?? throw new ArgumentNullException(nameof(applyAsync));
        _revertAsync = revertAsync;
    }

    public string Name { get; }

    public Task ApplyAsync(IBotRunner gmRunner, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gmRunner);
        ArgumentNullException.ThrowIfNull(context);

        return _applyAsync(gmRunner, context, cancellationToken);
    }

    public Task RevertAsync(IBotRunner runner, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(context);

        return _revertAsync is not null
            ? _revertAsync(runner, context, cancellationToken)
            : Task.CompletedTask;
    }
}
