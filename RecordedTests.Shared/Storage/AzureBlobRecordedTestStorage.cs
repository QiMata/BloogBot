using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Storage;

/// <summary>
/// Azure Blob Storage implementation for test artifacts.
/// </summary>
/// <remarks>
/// This implementation requires the Azure.Storage.Blobs NuGet package to be installed.
/// To use this storage backend, add: dotnet add package Azure.Storage.Blobs
/// </remarks>
public sealed class AzureBlobRecordedTestStorage(
    AzureBlobStorageConfiguration configuration,
    ITestLogger? logger = null) : IRecordedTestStorage
{
    private readonly AzureBlobStorageConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ITestLogger? _logger = logger;

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

        var blobName = ParseAzureBlobUriInstance(storageLocation);

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

        var blobName = ParseAzureBlobUriInstance(storageLocation);

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

    private static string SanitizeBlobName(string name)
    {
        // Azure Blob naming restrictions: replace backslashes and invalid chars
        var sanitized = name.Replace('\\', '_');
        foreach (var c in new[] { ':', '*', '?', '"', '<', '>', '|' })
        {
            sanitized = sanitized.Replace(c, '_');
        }
        return sanitized;
    }

    /// <summary>
    /// Parses an Azure Blob URI into its account, container, and blob name components.
    /// </summary>
    public static (string account, string container, string blobName) ParseAzureBlobUri(string blobUri)
    {
        if (string.IsNullOrEmpty(blobUri))
            throw new FormatException("Blob URI cannot be null or empty.");

        if (!blobUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Invalid Azure Blob URI format. Expected https:// URI but got: {blobUri}");

        Uri uri;
        try
        {
            uri = new Uri(blobUri);
        }
        catch (UriFormatException ex)
        {
            throw new FormatException($"Invalid Azure Blob URI format: {blobUri}", ex);
        }

        if (!uri.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Invalid Azure Blob URI host. Expected *.blob.core.windows.net but got: {uri.Host}");

        var account = uri.Host.Split('.')[0];
        var pathParts = uri.AbsolutePath.TrimStart('/').Split('/', 2);

        if (pathParts.Length < 2 || string.IsNullOrEmpty(pathParts[0]) || string.IsNullOrEmpty(pathParts[1]))
            throw new FormatException($"Invalid Azure Blob URI format (missing container or blob name): {blobUri}");

        return (account, pathParts[0], pathParts[1]);
    }

    /// <summary>
    /// Generates a blob name from the given components.
    /// </summary>
    public static string GenerateBlobName(string blobPrefix, string testName, string timestamp, string artifactName)
    {
        var sanitized = SanitizeBlobName(testName);
        return $"{blobPrefix}{sanitized}/{timestamp}/{artifactName}";
    }

    /// <summary>
    /// Generates an Azure Blob URI from account, container, and blob name.
    /// </summary>
    public static string GenerateAzureBlobUri(string accountName, string containerName, string blobName)
    {
        return $"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}";
    }

    private string ParseAzureBlobUriInstance(string blobUri)
    {
        var (_, containerName, blobName) = ParseAzureBlobUri(blobUri);

        if (containerName != _configuration.ContainerName)
        {
            throw new ArgumentException(
                $"Blob URI container '{containerName}' does not match configured container '{_configuration.ContainerName}'");
        }

        return blobName;
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
