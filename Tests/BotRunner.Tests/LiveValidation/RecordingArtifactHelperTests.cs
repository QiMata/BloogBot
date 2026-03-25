using System;
using System.IO;

namespace BotRunner.Tests.LiveValidation;

public sealed class RecordingArtifactHelperTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"wwow-recording-helper-{Guid.NewGuid():N}");

    public RecordingArtifactHelperTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void FindLatestRecordingFile_PrefersStableFileOverLegacyCopies()
    {
        var stablePath = Path.Combine(_tempDir, "transform_TESTBOT2.csv");
        var legacyPath = Path.Combine(_tempDir, "transform_TESTBOT2_20260325_100000.csv");
        File.WriteAllText(legacyPath, "legacy");
        File.WriteAllText(stablePath, "stable");

        var result = RecordingArtifactHelper.FindLatestRecordingFile(_tempDir, "transform", "TESTBOT2", "csv");

        Assert.Equal(stablePath, result);
    }

    [Fact]
    public void DeleteRecordingArtifacts_RemovesStableAndLegacyFiles()
    {
        var stableTransform = Path.Combine(_tempDir, "transform_TESTBOT2.csv");
        var legacyTransform = Path.Combine(_tempDir, "transform_TESTBOT2_20260325_100000.csv");
        var stableNavTrace = Path.Combine(_tempDir, "navtrace_TESTBOT2.json");
        File.WriteAllText(stableTransform, "stable");
        File.WriteAllText(legacyTransform, "legacy");
        File.WriteAllText(stableNavTrace, "{}");

        RecordingArtifactHelper.DeleteRecordingArtifacts(_tempDir, "TESTBOT2", "transform", "navtrace");

        Assert.False(File.Exists(stableTransform));
        Assert.False(File.Exists(legacyTransform));
        Assert.False(File.Exists(stableNavTrace));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
        }
    }
}
