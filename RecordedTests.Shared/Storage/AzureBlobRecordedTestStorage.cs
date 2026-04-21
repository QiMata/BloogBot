using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Storage;

/// <summary>
/// Azure Blob Storage implementation for test artifacts.
/// </summary>
public sealed class AzureBlobRecordedTestStorage : IRecordedTestStorage
{
    private readonly AzureBlobStorageConfiguration _configuration;
    private readonly IAzureBlobStorageBackend _backend;
    private readonly ITestLogger? _logger;
    private bool _disposed;

    public AzureBlobRecordedTestStorage(
        AzureBlobStorageConfiguration configuration,
        ITestLogger? logger = null)
        : this(configuration, new AzureSdkBlobStorageBackend(configuration), logger)
    {
    }

    internal AzureBlobRecordedTestStorage(
        AzureBlobStorageConfiguration configuration,
        IAzureBlobStorageBackend backend,
        ITestLogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _logger = logger;
    }

    public async Task StoreAsync(RecordedTestStorageContext context, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var sanitizedTestName = SanitizeBlobName(context.TestName);
        var timestampFolder = context.CompletedAt.ToString("yyyyMMdd_HHmmss");
        var baseBlobName = $"{_configuration.BlobPrefix}{sanitizedTestName}/{timestampFolder}";

        _logger?.Info($"Storing test artifacts for '{context.TestName}' in Azure Blob container '{_configuration.ContainerName}' under '{baseBlobName}'");

        if (!string.IsNullOrWhiteSpace(context.TestRunDirectory) && Directory.Exists(context.TestRunDirectory))
        {
            foreach (var file in Directory.GetFiles(context.TestRunDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var blobName = $"{baseBlobName}/{Path.GetFileName(file)}";
                await _backend.UploadFileAsync(blobName, file, cancellationToken).ConfigureAwait(false);
            }
        }

        if (context.RecordingArtifact is not null && File.Exists(context.RecordingArtifact.FullPath))
        {
            var blobName = $"{baseBlobName}/{context.RecordingArtifact.Name}";
            await _backend.UploadFileAsync(blobName, context.RecordingArtifact.FullPath, cancellationToken).ConfigureAwait(false);
        }

        var metadataBlobName = $"{baseBlobName}/run-metadata.json";
        var metadata = CreateMetadata(context);
        await _backend.UploadBytesAsync(metadataBlobName, metadata, cancellationToken).ConfigureAwait(false);

        _logger?.Info("Test artifacts stored successfully in Azure Blob Storage");
    }

    public async Task<string> UploadArtifactAsync(
        TestArtifact artifact,
        string testName,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(artifact.FullPath))
        {
            throw new FileNotFoundException($"Artifact not found: {artifact.FullPath}", artifact.FullPath);
        }

        var sanitizedTestName = SanitizeBlobName(testName);
        var timestampFolder = timestamp.ToString("yyyyMMdd_HHmmss");
        var blobName = $"{_configuration.BlobPrefix}{sanitizedTestName}/{timestampFolder}/{artifact.Name}";

        _logger?.Info($"Uploading artifact '{artifact.Name}' to Azure Blob container '{_configuration.ContainerName}' with name '{blobName}'");

        await _backend.UploadFileAsync(blobName, artifact.FullPath, cancellationToken).ConfigureAwait(false);

        _logger?.Info("Artifact uploaded successfully to Azure Blob Storage");

        return GenerateAzureBlobUri(_configuration.AccountName, _configuration.ContainerName, blobName);
    }

    public async Task DownloadArtifactAsync(
        string storageLocation,
        string localDestinationPath,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(storageLocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDestinationPath);
        cancellationToken.ThrowIfCancellationRequested();

        var blobName = ParseAzureBlobUriInstance(storageLocation);

        _logger?.Info($"Downloading artifact from Azure Blob '{blobName}' to {localDestinationPath}");

        var destinationDirectory = Path.GetDirectoryName(localDestinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await _backend.DownloadFileAsync(blobName, localDestinationPath, cancellationToken).ConfigureAwait(false);

        _logger?.Info("Artifact downloaded successfully from Azure Blob Storage");
    }

    public async Task<IReadOnlyList<string>> ListArtifactsAsync(
        string testName,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        cancellationToken.ThrowIfCancellationRequested();

        var sanitizedTestName = SanitizeBlobName(testName);
        var prefix = $"{_configuration.BlobPrefix}{sanitizedTestName}/";

        _logger?.Info($"Listing artifacts for test '{testName}' in Azure Blob container '{_configuration.ContainerName}'");

        var blobNames = await _backend.ListBlobNamesAsync(prefix, cancellationToken).ConfigureAwait(false);
        var artifacts = blobNames
            .Select(blobName => GenerateAzureBlobUri(_configuration.AccountName, _configuration.ContainerName, blobName))
            .ToArray();

        _logger?.Info($"Found {artifacts.Length} artifact(s)");
        return artifacts;
    }

    public async Task DeleteArtifactAsync(
        string storageLocation,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(storageLocation);
        cancellationToken.ThrowIfCancellationRequested();

        var blobName = ParseAzureBlobUriInstance(storageLocation);

        _logger?.Info($"Deleting artifact from Azure Blob '{blobName}'");

        await _backend.DeleteIfExistsAsync(blobName, cancellationToken).ConfigureAwait(false);

        _logger?.Info("Artifact deleted successfully from Azure Blob Storage");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _backend.Dispose();
    }

    private static string SanitizeBlobName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }).Distinct())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    private static byte[] CreateMetadata(RecordedTestStorageContext context)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["testName"] = context.TestName,
            ["automationRunId"] = context.AutomationRunId,
            ["success"] = context.Success,
            ["message"] = context.Message,
            ["startedAt"] = context.StartedAt,
            ["completedAt"] = context.CompletedAt
        };

        if (context.Metadata is not null)
        {
            foreach (var (key, value) in context.Metadata)
            {
                metadata[$"metadata.{key}"] = value;
            }
        }

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Parses an Azure Blob URI into its account, container, and blob name components.
    /// </summary>
    public static (string account, string container, string blobName) ParseAzureBlobUri(string blobUri)
    {
        if (string.IsNullOrEmpty(blobUri))
        {
            throw new FormatException("Blob URI cannot be null or empty.");
        }

        if (!blobUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Invalid Azure Blob URI format. Expected https:// URI but got: {blobUri}");
        }

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
        {
            throw new FormatException($"Invalid Azure Blob URI host. Expected *.blob.core.windows.net but got: {uri.Host}");
        }

        var account = uri.Host.Split('.')[0];
        var pathParts = uri.AbsolutePath.TrimStart('/').Split('/', 2);

        if (pathParts.Length < 2 || string.IsNullOrEmpty(pathParts[0]) || string.IsNullOrEmpty(pathParts[1]))
        {
            throw new FormatException($"Invalid Azure Blob URI format (missing container or blob name): {blobUri}");
        }

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

internal interface IAzureBlobStorageBackend : IDisposable
{
    Task UploadFileAsync(string blobName, string sourcePath, CancellationToken cancellationToken);

    Task UploadBytesAsync(string blobName, byte[] content, CancellationToken cancellationToken);

    Task DownloadFileAsync(string blobName, string destinationPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListBlobNamesAsync(string prefix, CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken);
}

internal sealed class AzureSdkBlobStorageBackend : IAzureBlobStorageBackend
{
    private readonly BlobContainerClient _containerClient;

    public AzureSdkBlobStorageBackend(AzureBlobStorageConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.ConnectionString))
        {
            throw new ArgumentException("Azure Blob Storage connection string is required.", nameof(configuration));
        }

        if (string.IsNullOrWhiteSpace(configuration.ContainerName))
        {
            throw new ArgumentException("Azure Blob Storage container name is required.", nameof(configuration));
        }

        _containerClient = new BlobContainerClient(configuration.ConnectionString, configuration.ContainerName);
    }

    public async Task UploadFileAsync(string blobName, string sourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Artifact not found: {sourcePath}", sourcePath);
        }

        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var blobClient = _containerClient.GetBlobClient(blobName);
        await using var fileStream = File.OpenRead(sourcePath);
        await blobClient.UploadAsync(fileStream, overwrite: true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadBytesAsync(string blobName, byte[] content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var blobClient = _containerClient.GetBlobClient(blobName);
        using var stream = new MemoryStream(content, writable: false);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadFileAsync(string blobName, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var blobClient = _containerClient.GetBlobClient(blobName);
        try
        {
            await blobClient.DownloadToAsync(destinationPath, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"Artifact not found: {blobClient.Uri}", blobName, ex);
        }
    }

    public async Task<IReadOnlyList<string>> ListBlobNamesAsync(string prefix, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var blobNames = new List<string>();
        try
        {
            await foreach (var blobItem in _containerClient.GetBlobsAsync(
                               BlobTraits.None,
                               BlobStates.None,
                               prefix,
                               cancellationToken))
            {
                blobNames.Add(blobItem.Name);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Array.Empty<string>();
        }

        return blobNames;
    }

    public async Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var blobClient = _containerClient.GetBlobClient(blobName);
        try
        {
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Match filesystem/S3 delete semantics: deleting a missing artifact is idempotent.
        }
    }

    public void Dispose()
    {
        // Azure SDK clients are thread-safe and do not require disposal.
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
