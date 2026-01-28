using System.Threading;
using System.Threading.Tasks;

namespace WWoW.RecordedTests.Shared.Abstractions.I;

public interface IBotRunnerVisitor<TResult>
{
    Task<TResult> VisitAsync(IBotRunner runner, CancellationToken cancellationToken);
}
