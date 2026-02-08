namespace RecordedTests.Shared.Abstractions.I;

public interface IBotRunnerVisitor<TResult>
{
    Task<TResult> VisitAsync(IBotRunner runner, CancellationToken cancellationToken);
}
