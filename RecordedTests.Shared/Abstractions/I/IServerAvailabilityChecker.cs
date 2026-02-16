using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Abstractions.I;

public interface IServerAvailabilityChecker
{
    Task<ServerInfo?> WaitForAvailableAsync(TimeSpan timeout, CancellationToken cancellationToken);
}