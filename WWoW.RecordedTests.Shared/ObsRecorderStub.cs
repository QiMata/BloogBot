namespace WWoW.RecordedTests.Shared;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

// A minimal OBS-style recorder stub. Replace with an implementation that controls OBS Studio
// via obs-websocket or similar when available.
public sealed class ObsRecorderStub : IScreenRecorder
{
    private bool _launched;
    private bool _recording;
    private string? _lastFilePath;

    public Task LaunchAsync(CancellationToken cancellationToken)
    {
        _launched = true;
        return Task.CompletedTask;
    }

    public Task ConfigureTargetAsync(RecordingTarget target, CancellationToken cancellationToken)
    {
        if (!_launched) throw new InvalidOperationException("Recorder not launched.");
        // Store target if needed; this is a stub
        return Task.CompletedTask;
    }

    public Task StartAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        if (!_launched) throw new InvalidOperationException("Recorder not launched.");
        _recording = true;
        // Simulate a file path where the recording would be saved when stopped
        _lastFilePath = Path.Combine(Path.GetTempPath(), $"{Sanitize(context.TestName)}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.mkv");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_recording) return; // idempotent
        // Simulate that the file now exists
        if (!string.IsNullOrEmpty(_lastFilePath))
        {
            try
            {
                await File.WriteAllTextAsync(_lastFilePath, "Dummy recording bytes", cancellationToken);
            }
            catch
            {
                // ignore
            }
        }
        _recording = false;
    }

    public async Task<TestArtifact> MoveLastRecordingAsync(string destinationDirectory, string desiredFileNameWithoutExtension, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_lastFilePath) || !File.Exists(_lastFilePath))
            throw new InvalidOperationException("No recording available to move.");

        Directory.CreateDirectory(destinationDirectory);
        var finalPath = Path.Combine(destinationDirectory, desiredFileNameWithoutExtension + ".mkv");

        // Ensure unique name
        var index = 1;
        var basePath = finalPath;
        while (File.Exists(finalPath))
        {
            finalPath = Path.Combine(destinationDirectory, $"{desiredFileNameWithoutExtension}_{index}.mkv");
            index++;
        }

        File.Move(_lastFilePath!, finalPath);
        _lastFilePath = finalPath;
        return new TestArtifact(Path.GetFileName(finalPath), finalPath);
    }

    public ValueTask DisposeAsync()
    {
        _recording = false;
        return ValueTask.CompletedTask;
    }

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
    }
}
