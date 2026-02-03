namespace WWoW.RecordedTests.Shared.Abstractions.I;

public interface IGmCommandHost
{
    Task<GmCommandExecutionResult> ExecuteGmCommandAsync(string command, CancellationToken cancellationToken);
}
