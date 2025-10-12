namespace WWoW.RecordedTests.Shared.Abstractions.I;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface ITrueNasAppsClient : IDisposable
{
    Task<TrueNasAppRelease?> GetReleaseAsync(string releaseName, CancellationToken cancellationToken);

    Task StartReleaseAsync(string releaseName, CancellationToken cancellationToken);
}
