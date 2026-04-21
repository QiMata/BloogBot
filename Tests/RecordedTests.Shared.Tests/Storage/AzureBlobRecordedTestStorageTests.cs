using FluentAssertions;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using RecordedTests.Shared.Storage;
using NSubstitute;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Tests.Storage;

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("s3://bucket/key")]
    public void ParseAzureBlobUri_InvalidUri_ThrowsFormatException(string? invalidUri)
    {
        // Act
        var act = () => AzureBlobRecordedTestStorage.ParseAzureBlobUri(invalidUri!);

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseAzureBlobUri_ValidAzureUri_ExtractsAccountContainerAndBlob()
    {
        // Arrange
        var uri = "https://mystorageaccount.blob.core.windows.net/mycontainer/recorded-tests/TestName/20250119_120000/recording.mkv";

        // Act
        var (account, container, blobName) = AzureBlobRecordedTestStorage.ParseAzureBlobUri(uri);

        // Assert
        account.Should().Be("mystorageaccount");
        container.Should().Be("mycontainer");
        blobName.Should().Be("recorded-tests/TestName/20250119_120000/recording.mkv");
    }

    [Fact]
    public void ParseAzureBlobUri_UriWithoutBlobName_ThrowsFormatException()
    {
        // Arrange - only account and container
        var uri = "https://mystorageaccount.blob.core.windows.net/mycontainer/";

        // Act
        var act = () => AzureBlobRecordedTestStorage.ParseAzureBlobUri(uri);

        // Assert - Azure blob URIs must include a blob name
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseAzureBlobUri_UriWithOnlyAccount_ThrowsFormatException()
    {
        // Arrange
        var uri = "https://mystorageaccount.blob.core.windows.net/";

        // Act
        var act = () => AzureBlobRecordedTestStorage.ParseAzureBlobUri(uri);

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void GenerateBlobName_SanitizesTestName()
    {
        // Arrange
        var testName = "Test/Path\\With:Invalid*Chars?";
        var timestamp = "20250119_120000";
        var artifactName = "recording.mkv";
        var blobPrefix = "recorded-tests/";

        // Act
        var blobName = AzureBlobRecordedTestStorage.GenerateBlobName(blobPrefix, testName, timestamp, artifactName);

        // Assert
        blobName.Should().NotContain("\\");
        blobName.Should().NotContain(":");
        blobName.Should().NotContain("*");
        blobName.Should().NotContain("?");
        blobName.Should().StartWith(blobPrefix);
        blobName.Should().EndWith($"/{timestamp}/{artifactName}");
    }

    [Fact]
    public void GenerateBlobName_IncludesAllComponents()
    {
        // Arrange
        var testName = "ValidTestName";
        var timestamp = "20250119_120000";
        var artifactName = "recording.mkv";
        var blobPrefix = "recorded-tests/";

        // Act
        var blobName = AzureBlobRecordedTestStorage.GenerateBlobName(blobPrefix, testName, timestamp, artifactName);

        // Assert
        blobName.Should().Be("recorded-tests/ValidTestName/20250119_120000/recording.mkv");
    }

    [Fact]
    public void GenerateAzureBlobUri_CreatesValidUri()
    {
        // Arrange
        var accountName = "mystorageaccount";
        var containerName = "mycontainer";
        var blobName = "recorded-tests/TestName/20250119_120000/recording.mkv";

        // Act
        var uri = AzureBlobRecordedTestStorage.GenerateAzureBlobUri(accountName, containerName, blobName);

        // Assert
        uri.Should().Be("https://mystorageaccount.blob.core.windows.net/mycontainer/recorded-tests/TestName/20250119_120000/recording.mkv");
    }

    [Fact]
    public void GenerateBlobName_AllowsForwardSlashes_InBlobHierarchy()
    {
        // Azure blobs support forward slashes for virtual directories
        // Arrange
        var testName = "ValidTestName";
        var timestamp = "20250119_120000";
        var artifactName = "subfolder/recording.mkv";
        var blobPrefix = "recorded-tests/";

        // Act
        var blobName = AzureBlobRecordedTestStorage.GenerateBlobName(blobPrefix, testName, timestamp, artifactName);

        // Assert
        blobName.Should().Contain("/");
        blobName.Should().Be("recorded-tests/ValidTestName/20250119_120000/subfolder/recording.mkv");
    }

    // ================================================================
    // Constructor validation
    // ================================================================

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        var act = () => new AzureBlobRecordedTestStorage(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidConfiguration_Succeeds()
    {
        var act = () => new AzureBlobRecordedTestStorage(CreateConfig());
        act.Should().NotThrow();
    }

    // ================================================================
    // UploadArtifactAsync
    // ================================================================

    [Fact]
    public async Task UploadArtifactAsync_NullArtifact_ThrowsArgumentNullException()
    {
        var storage = CreateStorage();
        var act = async () => await storage.UploadArtifactAsync(null!, "TestName", DateTimeOffset.UtcNow, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadArtifactAsync_InvalidTestName_ThrowsArgumentException(string? testName)
    {
        var storage = CreateStorage();
        var artifact = new TestArtifact("recording.mkv", "/tmp/recording.mkv");
        var act = async () => await storage.UploadArtifactAsync(artifact, testName!, DateTimeOffset.UtcNow, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadArtifactAsync_ReturnsAzureBlobUri()
    {
        var storage = CreateStorage();
        using var artifact = new TempArtifactFile("recording.mkv");
        var timestamp = new DateTimeOffset(2025, 1, 19, 12, 0, 0, TimeSpan.Zero);

        var result = await storage.UploadArtifactAsync(artifact.Artifact, "TestName", timestamp, CancellationToken.None);

        result.Should().Be("https://testaccount.blob.core.windows.net/test-container/recorded-tests/TestName/20250119_120000/recording.mkv");
    }

    [Fact]
    public async Task UploadArtifactAsync_StoresArtifactContent()
    {
        var backend = new InMemoryAzureBlobStorageBackend();
        var storage = CreateStorage(backend);
        using var artifact = new TempArtifactFile("recording.mkv", "azure upload content");
        var timestamp = new DateTimeOffset(2025, 1, 19, 12, 0, 0, TimeSpan.Zero);

        await storage.UploadArtifactAsync(artifact.Artifact, "TestName", timestamp, CancellationToken.None);

        backend.ReadText("recorded-tests/TestName/20250119_120000/recording.mkv")
            .Should().Be("azure upload content");
    }

    [Fact]
    public async Task UploadArtifactAsync_WithLogger_LogsMessages()
    {
        var logger = Substitute.For<ITestLogger>();
        var storage = CreateStorage(new InMemoryAzureBlobStorageBackend(), logger);
        using var artifact = new TempArtifactFile("test.mkv");

        await storage.UploadArtifactAsync(artifact.Artifact, "TestName", DateTimeOffset.UtcNow, CancellationToken.None);

        logger.Received().Info(Arg.Is<string>(s => s.Contains("Uploading")));
        logger.Received().Info(Arg.Is<string>(s => s.Contains("successfully")));
    }

    // ================================================================
    // DownloadArtifactAsync
    // ================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadArtifactAsync_InvalidStorageLocation_ThrowsArgumentException(string? location)
    {
        var storage = CreateStorage();
        var act = async () => await storage.DownloadArtifactAsync(location!, "/tmp/dest.mkv", CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadArtifactAsync_InvalidLocalPath_ThrowsArgumentException(string? localPath)
    {
        var storage = CreateStorage();
        var act = async () => await storage.DownloadArtifactAsync(ValidBlobUri, localPath!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("s3://bucket/key")]
    [InlineData("https://example.com/container/blob")]
    public async Task DownloadArtifactAsync_InvalidAzureBlobUri_ThrowsFormatException(string storageLocation)
    {
        var storage = CreateStorage();
        var destination = Path.Combine(Path.GetTempPath(), "wwow-azure-tests", "recording.mkv");

        var act = async () => await storage.DownloadArtifactAsync(storageLocation, destination, CancellationToken.None);

        await act.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task DownloadArtifactAsync_ContainerMismatch_ThrowsArgumentException()
    {
        var storage = CreateStorage();
        var uri = "https://testaccount.blob.core.windows.net/other-container/recorded-tests/TestName/recording.mkv";

        var act = async () => await storage.DownloadArtifactAsync(uri, "/tmp/dest.mkv", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*other-container*test-container*");
    }

    [Fact]
    public async Task DownloadArtifactAsync_ValidUri_DownloadsFile()
    {
        var backend = new InMemoryAzureBlobStorageBackend();
        backend.Seed("recorded-tests/TestName/recording.mkv", "downloaded content");
        var storage = CreateStorage(backend);
        var root = Path.Combine(Path.GetTempPath(), "wwow-azure-tests", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "nested", "recording.mkv");

        try
        {
            await storage.DownloadArtifactAsync(ValidBlobUri, destination, CancellationToken.None);

            Directory.Exists(Path.GetDirectoryName(destination)!).Should().BeTrue();
            File.Exists(destination).Should().BeTrue();
            File.ReadAllText(destination).Should().Be("downloaded content");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadArtifactAsync_MissingBlob_ThrowsFileNotFoundException()
    {
        var storage = CreateStorage();
        var destination = Path.Combine(Path.GetTempPath(), "wwow-azure-tests", Guid.NewGuid().ToString("N"), "recording.mkv");

        var act = async () => await storage.DownloadArtifactAsync(ValidBlobUri, destination, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task DownloadArtifactAsync_WithLogger_LogsParsedBlobAndStubSuccess()
    {
        var logger = Substitute.For<ITestLogger>();
        var backend = new InMemoryAzureBlobStorageBackend();
        backend.Seed("recorded-tests/TestName/recording.mkv", "downloaded content");
        var storage = CreateStorage(backend, logger);
        var destination = Path.Combine(Path.GetTempPath(), "wwow-azure-tests", Guid.NewGuid().ToString("N"), "recording.mkv");

        await storage.DownloadArtifactAsync(ValidBlobUri, destination, CancellationToken.None);

        logger.Received().Info(Arg.Is<string>(s => s.Contains("recorded-tests/TestName/recording.mkv")));
        logger.Received().Info(Arg.Is<string>(s => s.Contains("downloaded successfully")));
    }

    // ================================================================
    // ListArtifactsAsync
    // ================================================================

    [Fact]
    public async Task ListArtifactsAsync_ReturnsEmptyList()
    {
        var storage = CreateStorage();
        var result = await storage.ListArtifactsAsync("TestName", CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListArtifactsAsync_InvalidTestName_ThrowsArgumentException(string? testName)
    {
        var storage = CreateStorage();
        var act = async () => await storage.ListArtifactsAsync(testName!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ListArtifactsAsync_WithLogger_LogsListingAndCount()
    {
        var logger = Substitute.For<ITestLogger>();
        var storage = CreateStorage(new InMemoryAzureBlobStorageBackend(), logger);

        var result = await storage.ListArtifactsAsync("TestName", CancellationToken.None);

        result.Should().BeEmpty();
        logger.Received().Info(Arg.Is<string>(s => s.Contains("Listing artifacts for test 'TestName'")));
        logger.Received().Info(Arg.Is<string>(s => s.Contains("Found 0 artifact")));
    }

    [Fact]
    public async Task ListArtifactsAsync_ReturnsUploadedArtifactUris()
    {
        var backend = new InMemoryAzureBlobStorageBackend();
        backend.Seed("recorded-tests/TestName/20250119_120000/recording.mkv", "one");
        backend.Seed("recorded-tests/OtherTest/20250119_120000/recording.mkv", "two");
        var storage = CreateStorage(backend);

        var result = await storage.ListArtifactsAsync("TestName", CancellationToken.None);

        result.Should().Equal("https://testaccount.blob.core.windows.net/test-container/recorded-tests/TestName/20250119_120000/recording.mkv");
    }

    // ================================================================
    // DeleteArtifactAsync
    // ================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteArtifactAsync_InvalidStorageLocation_ThrowsArgumentException(string? location)
    {
        var storage = CreateStorage();
        var act = async () => await storage.DeleteArtifactAsync(location!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("s3://bucket/key")]
    [InlineData("https://example.com/container/blob")]
    public async Task DeleteArtifactAsync_InvalidAzureBlobUri_ThrowsFormatException(string storageLocation)
    {
        var storage = CreateStorage();

        var act = async () => await storage.DeleteArtifactAsync(storageLocation, CancellationToken.None);

        await act.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task DeleteArtifactAsync_ContainerMismatch_ThrowsArgumentException()
    {
        var storage = CreateStorage();
        var uri = "https://testaccount.blob.core.windows.net/other-container/recorded-tests/TestName/recording.mkv";

        var act = async () => await storage.DeleteArtifactAsync(uri, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*other-container*test-container*");
    }

    [Fact]
    public async Task DeleteArtifactAsync_ValidUri_Completes()
    {
        var storage = CreateStorage();
        var act = async () => await storage.DeleteArtifactAsync(ValidBlobUri, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteArtifactAsync_ExistingBlob_RemovesBlob()
    {
        var backend = new InMemoryAzureBlobStorageBackend();
        backend.Seed("recorded-tests/TestName/recording.mkv", "content");
        var storage = CreateStorage(backend);

        await storage.DeleteArtifactAsync(ValidBlobUri, CancellationToken.None);

        backend.Contains("recorded-tests/TestName/recording.mkv").Should().BeFalse();
    }

    [Fact]
    public async Task DeleteArtifactAsync_WithLogger_LogsParsedBlobAndStubSuccess()
    {
        var logger = Substitute.For<ITestLogger>();
        var storage = CreateStorage(new InMemoryAzureBlobStorageBackend(), logger);

        await storage.DeleteArtifactAsync(ValidBlobUri, CancellationToken.None);

        logger.Received().Info(Arg.Is<string>(s => s.Contains("recorded-tests/TestName/recording.mkv")));
        logger.Received().Info(Arg.Is<string>(s => s.Contains("deleted successfully")));
    }

    // ================================================================
    // StoreAsync
    // ================================================================

    [Fact]
    public async Task StoreAsync_DoesNotThrow()
    {
        var storage = CreateStorage();
        var context = new RecordedTestStorageContext(
            TestName: "Test",
            AutomationRunId: null,
            Success: true,
            Message: "ok",
            TestRunDirectory: "/tmp",
            RecordingArtifact: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow
        );

        var act = async () => await storage.StoreAsync(context, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StoreAsync_WithLogger_LogsStorageMessages()
    {
        var logger = Substitute.For<ITestLogger>();
        var storage = CreateStorage(new InMemoryAzureBlobStorageBackend(), logger);
        var context = new RecordedTestStorageContext(
            TestName: "Test", AutomationRunId: null,
            Success: true, Message: "ok",
            TestRunDirectory: "/tmp", RecordingArtifact: null,
            StartedAt: DateTimeOffset.UtcNow, CompletedAt: DateTimeOffset.UtcNow
        );

        await storage.StoreAsync(context, CancellationToken.None);

        logger.Received().Info(Arg.Is<string>(s => s.Contains("Storing test artifacts")));
        logger.Received().Info(Arg.Is<string>(s => s.Contains("stored successfully")));
    }

    [Fact]
    public async Task StoreAsync_UploadsMetadata()
    {
        var backend = new InMemoryAzureBlobStorageBackend();
        var storage = CreateStorage(backend);
        var completedAt = new DateTimeOffset(2025, 1, 19, 12, 0, 0, TimeSpan.Zero);
        var context = new RecordedTestStorageContext(
            TestName: "Test",
            AutomationRunId: "run-1",
            Success: true,
            Message: "ok",
            TestRunDirectory: null,
            RecordingArtifact: null,
            StartedAt: completedAt.AddMinutes(-1),
            CompletedAt: completedAt
        );

        await storage.StoreAsync(context, CancellationToken.None);

        backend.ReadText("recorded-tests/Test/20250119_120000/run-metadata.json")
            .Should().Contain("\"automationRunId\": \"run-1\"");
    }

    // ================================================================
    // Dispose
    // ================================================================

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var storage = CreateStorage();
        var act = () => storage.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var storage = CreateStorage();
        storage.Dispose();
        var act = () => storage.Dispose();
        act.Should().NotThrow();
    }

    // ================================================================
    // Helpers
    // ================================================================

    private const string ValidBlobUri = "https://testaccount.blob.core.windows.net/test-container/recorded-tests/TestName/recording.mkv";

    private static AzureBlobStorageConfiguration CreateConfig() => new()
    {
        AccountName = "testaccount",
        ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
        ContainerName = "test-container"
    };

    private static AzureBlobRecordedTestStorage CreateStorage(
        InMemoryAzureBlobStorageBackend? backend = null,
        ITestLogger? logger = null,
        AzureBlobStorageConfiguration? config = null)
    {
        return new AzureBlobRecordedTestStorage(config ?? CreateConfig(), backend ?? new InMemoryAzureBlobStorageBackend(), logger);
    }

    private sealed class TempArtifactFile : IDisposable
    {
        private readonly string _path;

        public TempArtifactFile(string artifactName, string content = "test content")
        {
            _path = Path.Combine(Path.GetTempPath(), $"wwow-azure-{Guid.NewGuid():N}-{artifactName}");
            File.WriteAllText(_path, content);
            Artifact = new TestArtifact(artifactName, _path);
        }

        public TestArtifact Artifact { get; }

        public void Dispose()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }
}
