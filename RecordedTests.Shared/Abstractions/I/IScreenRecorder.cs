using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Abstractions.I;

public interface IScreenRecorder : IAsyncDisposable
{
    Task LaunchAsync(CancellationToken cancellationToken);
    Task ConfigureTargetAsync(RecordingTarget target, CancellationToken cancellationToken);
    Task StartAsync(IRecordedTestContext context, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    // Move the last recorded file into destination directory and return an artifact descriptor
    Task<TestArtifact> MoveLastRecordingAsync(string destinationDirectory, string desiredFileNameWithoutExtension, CancellationToken cancellationToken);
}