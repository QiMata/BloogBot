using FluentAssertions;
using NSubstitute;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.Storage;

namespace WWoW.RecordedTests.Shared.Tests.Storage;

public class AzureBlobRecordedTestStorageTests
{
    [Fact]
    public void AzureBlobStorageConfiguration_Defaults_AreCorrect()
    {
        // Arrange & Act
        var config = new AzureBlobStorageConfiguration
        {
            AccountName = "testaccount",
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=key;EndpointSuffix=core.windows.net",
            ContainerName = "test-container"
        };

        // Assert
        config.BlobPrefix.Should().Be("recorded-tests/");
    }

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AzureBlobRecordedTestStorage(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidConfiguration_Succeeds()
    {
        // Arrange
        var config = new AzureBlobStorageConfiguration
        {
            AccountName = "testaccount",
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=key;EndpointSuffix=core.windows.net",
            ContainerName = "test-container"
        };

        // Act
        var act = () => new AzureBlobRecordedTestStorage(config);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task UploadArtifactAsync_ReturnsAzureBlobUri()
    {
        // Arrange
        var config = new AzureBlobStorageConfiguration
        {
            AccountName = "mystorageaccount",
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=key;EndpointSuffix=core.windows.net",
            ContainerName = "mycontainer",
            BlobPrefix = "recorded-tests/"
        };

        var storage = new AzureBlobRecordedTestStorage(config);
        var artifact = new TestArtifact("recording.mkv", "/tmp/recording.mkv");
        var timestamp = new DateTimeOffset(2025, 1, 19, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = await storage.UploadArtifactAsync(artifact, "TestName", timestamp, CancellationToken.None);

        // Assert
        result.Should().StartWith("https://mystorageaccount.blob.core.windows.net/mycontainer/recorded-tests/");
        result.Should().Contain("TestName");
        result.Should().Contain("recording.mkv");
    }

    [Fact]
    public async Task UploadArtifactAsync_NullArtifact_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new AzureBlobStorageConfiguration
        {
            AccountName = "testaccount",
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=key;EndpointSuffix=core.windows.net",
            ContainerName = "test-container"
        };

        var storage = new AzureBlobRecordedTestStorage(config);

        // Act
        var act = async () => await storage.UploadArtifactAsync(null!, "TestName", DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ListArtifactsAsync_ReturnsEmptyList()
    {
        // Arrange - Azure Blob operations are stubbed
        var config = new AzureBlobStorageConfiguration
        {
            AccountName = "testaccount",
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=key;EndpointSuffix=core.windows.net",
            ContainerName = "test-container"
        };

        var storage = new AzureBlobRecordedTestStorage(config);

        // Act
        var result = await storage.ListArtifactsAsync("TestName", CancellationToken.None);

        // Assert - Stub returns empty list
        result.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var config = new AzureBlobStorageConfiguration
        {
            AccountName = "testaccount",
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=key;EndpointSuffix=core.windows.net",
            ContainerName = "test-container"
        };

        var storage = new AzureBlobRecordedTestStorage(config);

        // Act
        var act = () => storage.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task StoreAsync_DoesNotThrow()
    {
        // Arrange
        var config = new AzureBlobStorageConfiguration
        {
            AccountName = "testaccount",
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=key;EndpointSuffix=core.windows.net",
            ContainerName = "test-container"
        };

        var storage = new AzureBlobRecordedTestStorage(config);
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

    [Fact]
    public async Task DownloadArtifactAsync_ValidUri_DoesNotThrow()
    {
        // Arrange
        var config = new AzureBlobStorageConfiguration
        {
            AccountName = "mystorageaccount",
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=key;EndpointSuffix=core.windows.net",
            ContainerName = "mycontainer"
        };

        var storage = new AzureBlobRecordedTestStorage(config);
        var tempDir = Path.Combine(Path.GetTempPath(), $"azure-test-{Guid.NewGuid()}");
        var destinationPath = Path.Combine(tempDir, "downloaded.mkv");

        try
        {
            // Act - stub implementation just logs and returns
            var act = async () => await storage.DownloadArtifactAsync(
                "https://mystorageaccount.blob.core.windows.net/mycontainer/recorded-tests/test.mkv",
                destinationPath,
                CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    // Note: Actual Azure Blob operations (Upload, Download, List, Delete) are stubbed
    // and would require Azure.Storage.Blobs package to be installed for full testing
}
