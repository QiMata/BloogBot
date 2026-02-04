using FluentAssertions;
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("s3://bucket/key")]  // S3 URI not valid for Azure
    [InlineData("http://storage.blob.core.windows.net/container/blob")]  // HTTP not HTTPS
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

    // Note: Actual Azure Blob operations (Upload, Download, List, Delete) are stubbed
    // and would require Azure.Storage.Blobs package to be installed for full testing
}
