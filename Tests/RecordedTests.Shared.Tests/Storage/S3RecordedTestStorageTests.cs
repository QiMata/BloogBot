using FluentAssertions;
using RecordedTests.Shared.Storage;

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
    [InlineData("s3://bucket/key")]  // S3 URI not valid for ServiceUrl
    public void ParseS3Uri_InvalidUri_ThrowsFormatException(string? invalidUri)
    {
        // Act
        var act = () => S3RecordedTestStorage.ParseS3Uri(invalidUri!);

        // Assert
        act.Should().Throw<FormatException>();
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
    public void GenerateS3Key_SanitizesTestName()
    {
        // Arrange
        var testName = "Test/Path\\With:Invalid*Chars?";
        var timestamp = "20250119_120000";
        var artifactName = "recording.mkv";
        var keyPrefix = "recorded-tests/";

        // Act
        var key = S3RecordedTestStorage.GenerateS3Key(keyPrefix, testName, timestamp, artifactName);

        // Assert
        key.Should().NotContain("/");
        key.Should().NotContain("\\");
        key.Should().NotContain(":");
        key.Should().NotContain("*");
        key.Should().NotContain("?");
        key.Should().StartWith(keyPrefix);
        key.Should().EndWith($"/{timestamp}/{artifactName}");
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

    // Note: Actual S3 operations (Upload, Download, List, Delete) are stubbed
    // and would require AWSSDK.S3 package to be installed for full testing
}
