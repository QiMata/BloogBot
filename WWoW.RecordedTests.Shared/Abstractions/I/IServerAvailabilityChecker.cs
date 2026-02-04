namespace WWoW.RecordedTests.Shared.Abstractions.I;

public interface IServerAvailabilityChecker
{
    Task<ServerInfo?> WaitForAvailableAsync(TimeSpan timeout, CancellationToken cancellationToken);
}