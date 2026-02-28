using FluentAssertions;
using NSubstitute;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Tests.Recording;

public class ObsRecorderStubTests
{
    private static IRecordedTestContext CreateTestContext(string testName = "TestRecording")
    {
        var ctx = Substitute.For<IRecordedTestContext>();
        ctx.TestName.Returns(testName);
        ctx.SanitizedTestName.Returns(testName);
        ctx.Server.Returns(new ServerInfo("localhost", 3724));
        ctx.StartedAt.Returns(DateTimeOffset.UtcNow);
        ctx.ArtifactsRootDirectory.Returns(Path.GetTempPath());
        ctx.TestRootDirectory.Returns(Path.GetTempPath());
        ctx.TestRunDirectory.Returns(Path.GetTempPath());
        return ctx;
    }

    [Fact]
    public async Task StartAsync_BeforeLaunch_ThrowsInvalidOperationException()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        var ctx = CreateTestContext();

        // Act
        var act = async () => await recorder.StartAsync(ctx, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*launch*");
    }

    [Fact]
    public async Task ConfigureTargetAsync_BeforeLaunch_ThrowsInvalidOperationException()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        var target = new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW");

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
        var ctx = CreateTestContext();

        // Act
        await recorder.LaunchAsync(CancellationToken.None);

        // Assert - subsequent calls should not throw
        var target = new RecordingTarget(RecordingTargetType.WindowByTitle, "WoW");
        await recorder.ConfigureTargetAsync(target, CancellationToken.None);
        var act = async () => await recorder.StartAsync(ctx, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_IdempotentBehavior_CanCallMultipleTimes()
    {
        // Arrange
        var recorder = new ObsRecorderStub();
        var ctx = CreateTestContext();
        await recorder.LaunchAsync(CancellationToken.None);
        await recorder.StartAsync(ctx, CancellationToken.None);

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
        var ctx = CreateTestContext();
        var tempDir = Path.Combine(Path.GetTempPath(), $"obs-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await recorder.LaunchAsync(CancellationToken.None);
            await recorder.StartAsync(ctx, CancellationToken.None);
            await recorder.StopAsync(CancellationToken.None);

            // Create a collision - manually create the target file
            var targetPath1 = Path.Combine(tempDir, "recording.mkv");
            File.WriteAllText(targetPath1, "existing");

            // Act - move should create recording_1.mkv
            var artifact1 = await recorder.MoveLastRecordingAsync(tempDir, "recording", CancellationToken.None);

            // Start another recording
            await recorder.StartAsync(ctx, CancellationToken.None);
            await recorder.StopAsync(CancellationToken.None);

            // Move again - should create recording_2.mkv
            var artifact2 = await recorder.MoveLastRecordingAsync(tempDir, "recording", CancellationToken.None);

            // Assert
            artifact1.FullPath.Should().EndWith(".mkv");
            artifact2.FullPath.Should().EndWith(".mkv");
            File.Exists(artifact1.FullPath).Should().BeTrue();
            File.Exists(artifact2.FullPath).Should().BeTrue();
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
        var ctx = CreateTestContext();
        var tempDir = Path.Combine(Path.GetTempPath(), $"obs-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await recorder.LaunchAsync(CancellationToken.None);
            await recorder.StartAsync(ctx, CancellationToken.None);
            await recorder.StopAsync(CancellationToken.None);

            // Act
            var artifact = await recorder.MoveLastRecordingAsync(tempDir, "recording", CancellationToken.None);

            // Assert
            artifact.Should().NotBeNull();
            artifact.FullPath.Should().EndWith(".mkv");
            artifact.Name.Should().NotBeNullOrWhiteSpace();
            File.Exists(artifact.FullPath).Should().BeTrue();
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
        var ctx = CreateTestContext();
        await recorder.LaunchAsync(CancellationToken.None);

        // Act
        await recorder.StartAsync(ctx, CancellationToken.None);
        await recorder.StopAsync(CancellationToken.None);

        // The temp file path is internal, but we can verify move works
        var tempDir = Path.Combine(Path.GetTempPath(), $"obs-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var artifact = await recorder.MoveLastRecordingAsync(tempDir, "recording", CancellationToken.None);

            // Assert
            File.Exists(artifact.FullPath).Should().BeTrue();
            File.ReadAllText(artifact.FullPath).Should().Contain("Dummy recording bytes");
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
        var ctx = CreateTestContext();
        await recorder.LaunchAsync(CancellationToken.None);
        await recorder.StartAsync(ctx, CancellationToken.None);

        // Act
        await recorder.DisposeAsync();

        // Assert - should not throw
        var act = async () => await recorder.DisposeAsync();
        await act.Should().NotThrowAsync();
    }
}
