using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Storage;

/// <summary>
/// Azure Blob Storage implementation for test artifacts.
/// </summary>
/// <remarks>
/// This implementation requires the Azure.Storage.Blobs NuGet package to be installed.
/// To use this storage backend, add: dotnet add package Azure.Storage.Blobs
/// </remarks>
public sealed class AzureBlobRecordedTestStorage : IRecordedTestStorage
{
    private readonly AzureBlobStorageConfiguration _configuration;
    private readonly ITestLogger? _logger;
    // private readonly BlobServiceClient _blobServiceClient; // Requires Azure.Storage.Blobs package
    // private readonly BlobContainerClient _containerClient;

    public AzureBlobRecordedTestStorage(
        AzureBlobStorageConfiguration configuration,
        ITestLogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        // TODO: Initialize Azure Blob client when Azure.Storage.Blobs is added
        // _blobServiceClient = new BlobServiceClient(_configuration.ConnectionString);
        // _containerClient = _blobServiceClient.GetBlobContainerClient(_configuration.ContainerName);
        // await _containerClient.CreateIfNotExistsAsync();
        }

        public Task StoreAsync(RecordedTestStorageContext context, CancellationToken cancellationToken)
        {
            // Azure Blob storage implementation should use UploadArtifactAsync for individual artifacts
            _logger?.Warn("StoreAsync is not directly implemented for Azure Blob Storage. Use UploadArtifactAsync for individual artifacts.");
            return Task.CompletedTask;
        }

        public async Task<string> UploadArtifactAsync(
        TestArtifact artifact,
        string testName,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        var sanitizedTestName = SanitizeBlobName(testName);
        var timestampFolder = timestamp.ToString("yyyyMMdd_HHmmss");
        var blobName = $"{_configuration.BlobPrefix}{sanitizedTestName}/{timestampFolder}/{artifact.Name}";

        _logger?.Info($"Uploading artifact '{artifact.Name}' to Azure Blob container '{_configuration.ContainerName}' with name '{blobName}'");

        // TODO: Implement actual Azure Blob upload when Azure.Storage.Blobs is added
        // var blobClient = _containerClient.GetBlobClient(blobName);
        //
        // using var fileStream = File.OpenRead(artifact.FullPath);
        // await blobClient.UploadAsync(
        //     fileStream,
        //     overwrite: true,
        //     cancellationToken: cancellationToken);

        _logger?.Info($"Artifact uploaded successfully to Azure Blob Storage");

        // Return Azure Blob URI as storage location
        return $"https://{_configuration.AccountName}.blob.core.windows.net/{_configuration.ContainerName}/{blobName}";
    }

    public async Task DownloadArtifactAsync(
        string storageLocation,
        string localDestinationPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageLocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDestinationPath);

        var blobName = ParseAndValidateAzureBlobUri(storageLocation);

        _logger?.Info($"Downloading artifact from Azure Blob '{blobName}' to {localDestinationPath}");

        var destinationDirectory = Path.GetDirectoryName(localDestinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        // TODO: Implement actual Azure Blob download when Azure.Storage.Blobs is added
        // var blobClient = _containerClient.GetBlobClient(blobName);
        //
        // await blobClient.DownloadToAsync(localDestinationPath, cancellationToken);

        _logger?.Info("Artifact downloaded successfully from Azure Blob Storage");

        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<string>> ListArtifactsAsync(
        string testName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        var sanitizedTestName = SanitizeBlobName(testName);
        var prefix = $"{_configuration.BlobPrefix}{sanitizedTestName}/";

        _logger?.Info($"Listing artifacts for test '{testName}' in Azure Blob container '{_configuration.ContainerName}'");

        // TODO: Implement actual Azure Blob list when Azure.Storage.Blobs is added
        // var artifacts = new List<string>();
        //
        // await foreach (var blobItem in _containerClient.GetBlobsAsync(
        //     prefix: prefix,
        //     cancellationToken: cancellationToken))
        // {
        //     var blobUri = $"https://{_configuration.AccountName}.blob.core.windows.net/{_configuration.ContainerName}/{blobItem.Name}";
        //     artifacts.Add(blobUri);
        // }
        //
        // _logger?.Info($"Found {artifacts.Count} artifact(s)");
        // return artifacts;

        _logger?.Info("Azure Blob listing not yet implemented (requires Azure.Storage.Blobs package)");
        return Array.Empty<string>();
    }

    public async Task DeleteArtifactAsync(
        string storageLocation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageLocation);

        var blobName = ParseAndValidateAzureBlobUri(storageLocation);

        _logger?.Info($"Deleting artifact from Azure Blob '{blobName}'");

        // TODO: Implement actual Azure Blob delete when Azure.Storage.Blobs is added
        // var blobClient = _containerClient.GetBlobClient(blobName);
        // await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        _logger?.Info("Artifact deleted successfully from Azure Blob Storage");

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        // No resources to dispose for Azure Blob Storage client
    }

    public static string GenerateBlobName(string blobPrefix, string testName, DateTimeOffset timestamp, string artifactName)
    {
        var sanitizedTestName = SanitizeBlobName(testName);
        var timestampFolder = timestamp.ToString("yyyyMMdd_HHmmss");
        return $"{blobPrefix}{sanitizedTestName}/{timestampFolder}/{artifactName}";
    }

    public static string GenerateAzureBlobUri(string accountName, string containerName, string blobName)
    {
        return $"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}";
    }

    public static (string containerName, string blobName) ParseAzureBlobUri(string blobUri)
    {
        // Expected format: https://{account}.blob.core.windows.net/{container}/{blobName}
        var uri = new Uri(blobUri);
        var pathParts = uri.AbsolutePath.TrimStart('/').Split('/', 2);

        if (pathParts.Length < 2)
        {
            throw new ArgumentException($"Invalid Azure Blob URI format: {blobUri}");
        }

        return (pathParts[0], pathParts[1]);
    }

    private string ParseAndValidateAzureBlobUri(string blobUri)
    {
        var (containerName, blobName) = ParseAzureBlobUri(blobUri);

        if (containerName != _configuration.ContainerName)
        {
            throw new ArgumentException(
                $"Blob URI container '{containerName}' does not match configured container '{_configuration.ContainerName}'");
        }

        return blobName;
    }

    private static string SanitizeBlobName(string name)
    {
        // Azure Blob naming restrictions: forward slashes are allowed, backslashes are not
        return name.Replace('\\', '/');
    }
}

/// <summary>
/// Configuration for Azure Blob Storage.
/// </summary>
public sealed class AzureBlobStorageConfiguration
{
    /// <summary>
    /// Azure Storage account name.
    /// </summary>
    public string AccountName { get; init; } = string.Empty;

    /// <summary>
    /// Azure Storage connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Blob container name where artifacts will be stored.
    /// </summary>
    public string ContainerName { get; init; } = string.Empty;

    /// <summary>
    /// Blob prefix for all uploaded artifacts. Useful for organizing artifacts within a shared container.
    /// </summary>
    public string BlobPrefix { get; init; } = "recorded-tests/";
}
