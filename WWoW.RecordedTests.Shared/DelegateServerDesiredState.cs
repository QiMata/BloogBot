using System;
using System.Threading;
using System.Threading.Tasks;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared;

public sealed class DelegateServerDesiredState : IServerDesiredState
{
    private readonly Func<IBotRunner, IRecordedTestContext, CancellationToken, Task> _applyAsync;

    public DelegateServerDesiredState(string name, Func<IBotRunner, IRecordedTestContext, CancellationToken, Task> applyAsync)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("State name is required.", nameof(name))
            : name;
        _applyAsync = applyAsync ?? throw new ArgumentNullException(nameof(applyAsync));
    }

    public string Name { get; }

    public Task ApplyAsync(IBotRunner gmRunner, IRecordedTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gmRunner);
        ArgumentNullException.ThrowIfNull(context);

        return _applyAsync(gmRunner, context, cancellationToken);
    }
}
