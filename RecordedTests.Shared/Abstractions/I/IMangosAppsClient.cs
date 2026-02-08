namespace RecordedTests.Shared.Abstractions.I;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IMangosAppsClient : IDisposable
{
    Task<TrueNasAppRelease?> GetReleaseAsync(string releaseName, CancellationToken cancellationToken);

    Task StartReleaseAsync(string releaseName, CancellationToken cancellationToken);
}
