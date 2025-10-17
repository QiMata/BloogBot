using System.Threading;
using System.Threading.Tasks;

namespace WWoW.RecordedTests.Shared.Abstractions.I;

public interface IBotRunner : IAsyncDisposable
{
    Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);

    Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken);
    Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken);

    Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken);

    Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken);
    Task ShutdownUiAsync(CancellationToken cancellationToken);

    Task<TResult> AcceptVisitorAsync<TResult>(IBotRunnerVisitor<TResult> visitor, CancellationToken cancellationToken)
        => visitor.VisitAsync(this, cancellationToken);
}
