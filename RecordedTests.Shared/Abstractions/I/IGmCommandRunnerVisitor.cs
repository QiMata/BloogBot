using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Abstractions.I;

public interface IGmCommandRunnerVisitor<TResult> : IBotRunnerVisitor<TResult>
{
    Task<TResult> VisitAsync(IGmCommandHost runner, CancellationToken cancellationToken);
}
