using FluentAssertions;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using RecordedTests.Shared.Storage;
using NSubstitute;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Tests.Storage;

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    public void ParseS3Uri_InvalidUri_ThrowsFormatException(string? invalidUri)
    {
        // Act
        var act = () => S3RecordedTestStorage.ParseS3Uri(invalidUri!);

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("https://my-bucket.s3.amazonaws.com/recorded-tests/test.mkv")]
    [InlineData("http://my-bucket.s3.us-west-2.amazonaws.com/test.mkv")]
    public void ParseS3Uri_HttpsS3Url_ThrowsFormatException(string httpsUrl)
    {
        // S3 storage expects s3:// URIs, not HTTPS URLs
        // Act
        var act = () => S3RecordedTestStorage.ParseS3Uri(httpsUrl);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*s3://*");
    }

    [Fact]
    public void ParseS3Uri_ValidS3Uri_ExtractsBucketAndKey()
    {
        // Arrange
        var uri = "s3://my-bucket/recorded-tests/TestName/20250119_120000/recording.mkv";

        // Act
        var (bucket, key) = S3RecordedTestStorage.ParseS3Uri(uri);

        // Assert
        bucket.Should().Be("my-bucket");
        key.Should().Be("recorded-tests/TestName/20250119_120000/recording.mkv");
    }

    [Fact]
    public void ParseS3Uri_S3UriWithoutKey_ExtractsBucketOnly()
    {
        // Arrange
        var uri = "s3://my-bucket/";

        // Act
        var (bucket, key) = S3RecordedTestStorage.ParseS3Uri(uri);

        // Assert
        bucket.Should().Be("my-bucket");
        key.Should().BeEmpty();
    }

    [Fact]
    public void ParseS3Uri_S3UriWithoutTrailingSlash_ExtractsBucketOnly()
    {
        // Arrange
        var uri = "s3://my-bucket";

        // Act
        var (bucket, key) = S3RecordedTestStorage.ParseS3Uri(uri);

        // Assert
        bucket.Should().Be("my-bucket");
        key.Should().BeEmpty();
    }

    [Fact]
    public void GenerateS3Key_SanitizesTestName()
    {
        // Arrange
        var testName = "Test/Path\\With:Invalid*Chars?";
        var timestamp = "20250119_120000";
        var artifactName = "recording.mkv";
        var keyPrefix = "recorded-tests/";

        // Act
        var key = S3RecordedTestStorage.GenerateS3Key(keyPrefix, testName, timestamp, artifactName);

        // Assert — check structure
        key.Should().StartWith(keyPrefix);
        key.Should().EndWith($"/{timestamp}/{artifactName}");

        // Extract sanitized test name portion and verify invalid chars were replaced
        var sanitizedPortion = key.Substring(keyPrefix.Length,
            key.Length - keyPrefix.Length - $"/{timestamp}/{artifactName}".Length);
        sanitizedPortion.Should().NotContain("/");
        sanitizedPortion.Should().NotContain("\\");
        sanitizedPortion.Should().NotContain(":");
        sanitizedPortion.Should().NotContain("*");
        sanitizedPortion.Should().NotContain("?");
    }

    [Fact]
    public void GenerateS3Key_IncludesAllComponents()
    {
        // Arrange
        var testName = "ValidTestName";
        var timestamp = "20250119_120000";
        var artifactName = "recording.mkv";
        var keyPrefix = "recorded-tests/";

        // Act
        var key = S3RecordedTestStorage.GenerateS3Key(keyPrefix, testName, timestamp, artifactName);

        // Assert
        key.Should().Be("recorded-tests/ValidTestName/20250119_120000/recording.mkv");
    }

    [Fact]
    public void GenerateS3Uri_CreatesValidS3Uri()
    {
        // Arrange
        var bucketName = "my-bucket";
        var key = "recorded-tests/TestName/20250119_120000/recording.mkv";

        // Act
        var uri = S3RecordedTestStorage.GenerateS3Uri(bucketName, key);

        // Assert
        uri.Should().Be("s3://my-bucket/recorded-tests/TestName/20250119_120000/recording.mkv");
    }

    // ================================================================
    // Constructor validation
    // ================================================================

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        var act = () => new S3RecordedTestStorage(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidConfiguration_Succeeds()
    {
        var config = CreateConfig();
        var act = () => new S3RecordedTestStorage(config);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullLogger_Succeeds()
    {
        var config = CreateConfig();
        var act = () => new S3RecordedTestStorage(config, logger: null);
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
    public async Task UploadArtifactAsync_ReturnsS3Uri()
    {
        var storage = CreateStorage();
        var artifact = new TestArtifact("recording.mkv", "/tmp/recording.mkv");
        var timestamp = new DateTimeOffset(2025, 1, 19, 12, 0, 0, TimeSpan.Zero);

        var result = await storage.UploadArtifactAsync(artifact, "TestName", timestamp, CancellationToken.None);

        result.Should().StartWith("s3://test-bucket/recorded-tests/");
        result.Should().Contain("TestName");
        result.Should().Contain("recording.mkv");
        result.Should().Contain("20250119_120000");
    }

    [Fact]
    public async Task UploadArtifactAsync_WithLogger_LogsMessages()
    {
        var logger = Substitute.For<ITestLogger>();
        var storage = new S3RecordedTestStorage(CreateConfig(), logger);
        var artifact = new TestArtifact("test.mkv", "/tmp/test.mkv");

        await storage.UploadArtifactAsync(artifact, "TestName", DateTimeOffset.UtcNow, CancellationToken.None);

        logger.Received().Info(Arg.Is<string>(s => s.Contains("Uploading")));
        logger.Received().Info(Arg.Is<string>(s => s.Contains("successfully")));
    }

    // ================================================================
    // DownloadArtifactAsync parameter validation
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
        var act = async () => await storage.DownloadArtifactAsync("s3://bucket/key", localPath!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
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

    [Fact]
    public async Task DeleteArtifactAsync_ValidUri_Completes()
    {
        var storage = CreateStorage();
        var act = async () => await storage.DeleteArtifactAsync("s3://bucket/key", CancellationToken.None);
        await act.Should().NotThrowAsync();
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
    public async Task StoreAsync_WithLogger_LogsWarning()
    {
        var logger = Substitute.For<ITestLogger>();
        var storage = new S3RecordedTestStorage(CreateConfig(), logger);
        var context = new RecordedTestStorageContext(
            TestName: "Test", AutomationRunId: null,
            Success: true, Message: "ok",
            TestRunDirectory: "/tmp", RecordingArtifact: null,
            StartedAt: DateTimeOffset.UtcNow, CompletedAt: DateTimeOffset.UtcNow
        );

        await storage.StoreAsync(context, CancellationToken.None);

        logger.Received().Warn(Arg.Is<string>(s => s.Contains("StoreAsync")));
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
    // Configuration edge cases
    // ================================================================

    [Fact]
    public void S3StorageConfiguration_CustomRegion_CanBeSet()
    {
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret",
            Region = "eu-west-1"
        };
        config.Region.Should().Be("eu-west-1");
    }

    [Fact]
    public void S3StorageConfiguration_PathStyleAddressing_CanBeEnabled()
    {
        var config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret",
            UsePathStyle = true
        };
        config.UsePathStyle.Should().BeTrue();
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static S3StorageConfiguration CreateConfig() => new()
    {
        BucketName = "test-bucket",
        AccessKeyId = "test-key",
        SecretAccessKey = "test-secret"
    };

    private static S3RecordedTestStorage CreateStorage() => new(CreateConfig());
}
