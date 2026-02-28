using FluentAssertions;
using NSubstitute;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.Storage;

namespace WWoW.RecordedTests.Shared.Tests.Storage;

public class S3RecordedTestStorageTests
{
    [Fact]
    public void S3StorageConfiguration_Defaults_AreCorrect()
    {
        // Arrange & Act
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret"
        };

        // Assert
        config.Region.Should().Be("us-east-1");
        config.KeyPrefix.Should().Be("recorded-tests/");
        config.UsePathStyle.Should().BeFalse();
    }

    [Fact]
    public void S3StorageConfiguration_CustomServiceUrl_CanBeSet()
    {
        // Arrange & Act
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret",
            ServiceUrl = "https://minio.local:9000"
        };

        // Assert
        config.ServiceUrl.Should().Be("https://minio.local:9000");
    }

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new S3RecordedTestStorage(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidConfiguration_Succeeds()
    {
        // Arrange
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret"
        };

        // Act
        var act = () => new S3RecordedTestStorage(config);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task UploadArtifactAsync_ReturnsS3Uri()
    {
        // Arrange
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret",
            KeyPrefix = "recorded-tests/"
        };

        var storage = new S3RecordedTestStorage(config);
        var artifact = new TestArtifact("recording.mkv", "/tmp/recording.mkv");
        var timestamp = new DateTimeOffset(2025, 1, 19, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = await storage.UploadArtifactAsync(artifact, "TestName", timestamp, CancellationToken.None);

        // Assert
        result.Should().StartWith("s3://test-bucket/recorded-tests/");
        result.Should().Contain("TestName");
        result.Should().Contain("recording.mkv");
    }

    [Fact]
    public async Task UploadArtifactAsync_NullArtifact_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret"
        };

        var storage = new S3RecordedTestStorage(config);

        // Act
        var act = async () => await storage.UploadArtifactAsync(null!, "TestName", DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ListArtifactsAsync_ReturnsEmptyList()
    {
        // Arrange - S3 operations are stubbed
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret"
        };

        var storage = new S3RecordedTestStorage(config);

        // Act
        var result = await storage.ListArtifactsAsync("TestName", CancellationToken.None);

        // Assert - Stub returns empty list
        result.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret"
        };

        var storage = new S3RecordedTestStorage(config);

        // Act
        var act = () => storage.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task StoreAsync_DoesNotThrow()
    {
        // Arrange
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret"
        };

        var storage = new S3RecordedTestStorage(config);
        var storeContext = new RecordedTestStorageContext(
            TestName: "Test",
            AutomationRunId: null,
            Success: true,
            Message: "ok",
            TestRunDirectory: "/tmp",
            RecordingArtifact: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow
        );

        // Act
        var act = async () => await storage.StoreAsync(storeContext, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // Note: Actual S3 operations (Upload, Download, List, Delete) are stubbed
    // and would require AWSSDK.S3 package to be installed for full testing
}
