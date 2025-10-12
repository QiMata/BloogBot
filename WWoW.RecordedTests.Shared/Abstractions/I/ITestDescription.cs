namespace WWoW.RecordedTests.Shared.Abstractions.I;

public interface ITestDescription
{
    string Name { get; }
    Task<OrchestrationResult> ExecuteAsync(ServerInfo server, CancellationToken cancellationToken);
}