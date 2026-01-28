using FluentAssertions;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;

namespace WWoW.RecordedTests.Shared.Tests.Recording;

public class ObsRecorderStubTests
{
    [Fact]
    public async Task StartAsync_BeforeLaunch_ThrowsInvalidOperationException()
    {
        // Arrange
        var recorder = new ObsRecorderStub();

        // Act
        var act = async () => await recorder.StartAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*launch*");
    }

    [Fact]
    public async Task ConfigureTargetAsync_BeforeLaunch_ThrowsInvalidOperationException()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        var target = new RecordingTarget(RecordingTargetType.WindowTitle, "WoW");

        // Act
        var act = async () => await recorder.ConfigureTargetAsync(target, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*launch*");
    }

    [Fact]
    public async Task LaunchAsync_SetsLaunchedState()
    {
        // Arrange
        var recorder = new ObsRecorderStub();

        // Act
        await recorder.LaunchAsync(CancellationToken.None);

        // Assert - subsequent calls should not throw
        var target = new RecordingTarget(RecordingTargetType.WindowTitle, "WoW");
        await recorder.ConfigureTargetAsync(target, CancellationToken.None);
        var act = async () => await recorder.StartAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_IdempotentBehavior_CanCallMultipleTimes()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        await recorder.LaunchAsync(CancellationToken.None);
        await recorder.StartAsync(CancellationToken.None);

        // Act - stop multiple times
        await recorder.StopAsync(CancellationToken.None);
        await recorder.StopAsync(CancellationToken.None);
        await recorder.StopAsync(CancellationToken.None);

        // Assert - should not throw
        var act = async () => await recorder.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        await recorder.LaunchAsync(CancellationToken.None);

        // Act - stop without starting
        var act = async () => await recorder.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MoveLastRecordingAsync_CreatesUniqueFilename_WhenCollision()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        var tempDir = Path.Combine(Path.GetTempPath(), $"obs-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await recorder.LaunchAsync(CancellationToken.None);
            await recorder.StartAsync(CancellationToken.None);
            await recorder.StopAsync(CancellationToken.None);

            // Create a collision - manually create the target file
            var targetPath1 = Path.Combine(tempDir, "recording.mkv");
            File.WriteAllText(targetPath1, "existing");

            // Act - move should create recording_1.mkv
            var artifact1 = await recorder.MoveLastRecordingAsync(tempDir, CancellationToken.None);

            // Start another recording
            await recorder.StartAsync(CancellationToken.None);
            await recorder.StopAsync(CancellationToken.None);

            // Move again - should create recording_2.mkv
            var artifact2 = await recorder.MoveLastRecordingAsync(tempDir, CancellationToken.None);

            // Assert
            artifact1.FilePath.Should().EndWith("recording_1.mkv");
            artifact2.FilePath.Should().EndWith("recording_2.mkv");
            File.Exists(artifact1.FilePath).Should().BeTrue();
            File.Exists(artifact2.FilePath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MoveLastRecordingAsync_ReturnsTestArtifact_WithCorrectProperties()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        var tempDir = Path.Combine(Path.GetTempPath(), $"obs-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await recorder.LaunchAsync(CancellationToken.None);
            await recorder.StartAsync(CancellationToken.None);
            await recorder.StopAsync(CancellationToken.None);

            // Act
            var artifact = await recorder.MoveLastRecordingAsync(tempDir, CancellationToken.None);

            // Assert
            artifact.Should().NotBeNull();
            artifact.FilePath.Should().EndWith(".mkv");
            artifact.ContentType.Should().Be("video/x-matroska");
            artifact.SizeBytes.Should().BeGreaterThan(0);
            File.Exists(artifact.FilePath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StartAsync_CreatesTempRecordingFile()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        await recorder.LaunchAsync(CancellationToken.None);

        // Act
        await recorder.StartAsync(CancellationToken.None);
        await recorder.StopAsync(CancellationToken.None);

        // The temp file path is internal, but we can verify move works
        var tempDir = Path.Combine(Path.GetTempPath(), $"obs-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var artifact = await recorder.MoveLastRecordingAsync(tempDir, CancellationToken.None);

            // Assert
            File.Exists(artifact.FilePath).Should().BeTrue();
            File.ReadAllText(artifact.FilePath).Should().Contain("OBS recording stub");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DisposeAsync_CleansUpResources()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        await recorder.LaunchAsync(CancellationToken.None);
        await recorder.StartAsync(CancellationToken.None);

        // Act
        await recorder.DisposeAsync();

        // Assert - should not throw
        var act = async () => await recorder.DisposeAsync();
        await act.Should().NotThrowAsync();
    }
}
