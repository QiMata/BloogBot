using System.Threading;
using System.Threading.Tasks;

namespace WWoW.RecordedTests.Shared.Abstractions.I;

public interface IServerDesiredState
{
    string Name { get; }

    Task ApplyAsync(IBotRunner gmRunner, IRecordedTestContext context, CancellationToken cancellationToken);
}
