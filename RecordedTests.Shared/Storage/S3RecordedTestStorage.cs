using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Storage;

/// <summary>
/// S3-compatible storage implementation for test artifacts.
/// Supports AWS S3 and S3-compatible services (MinIO, DigitalOcean Spaces, etc.).
/// </summary>
public sealed class S3RecordedTestStorage : IRecordedTestStorage
{
    private readonly S3StorageConfiguration _configuration;
    private readonly IS3StorageBackend _backend;
    private readonly ITestLogger? _logger;
    private bool _disposed;

    public S3RecordedTestStorage(
        S3StorageConfiguration configuration,
        ITestLogger? logger = null)
        : this(configuration, new AwsS3StorageBackend(configuration), logger)
    {
    }

    internal S3RecordedTestStorage(
        S3StorageConfiguration configuration,
        IS3StorageBackend backend,
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

        var sanitizedTestName = SanitizeKey(context.TestName);
        var timestampFolder = context.CompletedAt.ToString("yyyyMMdd_HHmmss");
        var baseKey = $"{_configuration.KeyPrefix}{sanitizedTestName}/{timestampFolder}";

        _logger?.Info($"Storing test artifacts for '{context.TestName}' in S3 bucket '{_configuration.BucketName}' under '{baseKey}'");

        if (!string.IsNullOrWhiteSpace(context.TestRunDirectory) && Directory.Exists(context.TestRunDirectory))
        {
            foreach (var file in Directory.GetFiles(context.TestRunDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = $"{baseKey}/{Path.GetFileName(file)}";
                await _backend.UploadFileAsync(_configuration.BucketName, key, file, cancellationToken).ConfigureAwait(false);
            }
        }

        if (context.RecordingArtifact is not null && File.Exists(context.RecordingArtifact.FullPath))
        {
            var key = $"{baseKey}/{context.RecordingArtifact.Name}";
            await _backend.UploadFileAsync(_configuration.BucketName, key, context.RecordingArtifact.FullPath, cancellationToken).ConfigureAwait(false);
        }

        var metadataKey = $"{baseKey}/run-metadata.json";
        var metadata = CreateMetadata(context);
        await _backend.UploadBytesAsync(_configuration.BucketName, metadataKey, metadata, cancellationToken).ConfigureAwait(false);

        _logger?.Info("Test artifacts stored successfully in S3");
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

        var sanitizedTestName = SanitizeKey(testName);
        var timestampFolder = timestamp.ToString("yyyyMMdd_HHmmss");
        var s3Key = $"{_configuration.KeyPrefix}{sanitizedTestName}/{timestampFolder}/{artifact.Name}";

        _logger?.Info($"Uploading artifact '{artifact.Name}' to S3 bucket '{_configuration.BucketName}' with key '{s3Key}'");

        await _backend.UploadFileAsync(_configuration.BucketName, s3Key, artifact.FullPath, cancellationToken).ConfigureAwait(false);

        _logger?.Info("Artifact uploaded successfully to S3");

        return GenerateS3Uri(_configuration.BucketName, s3Key);
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

        var (bucket, key) = ParseConfiguredS3ObjectUri(storageLocation);

        _logger?.Info($"Downloading artifact from S3 bucket '{bucket}' key '{key}' to {localDestinationPath}");

        var destinationDirectory = Path.GetDirectoryName(localDestinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await _backend.DownloadFileAsync(bucket, key, localDestinationPath, cancellationToken).ConfigureAwait(false);

        _logger?.Info("Artifact downloaded successfully from S3");
    }

    public async Task<IReadOnlyList<string>> ListArtifactsAsync(
        string testName,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        cancellationToken.ThrowIfCancellationRequested();

        var sanitizedTestName = SanitizeKey(testName);
        var prefix = $"{_configuration.KeyPrefix}{sanitizedTestName}/";

        _logger?.Info($"Listing artifacts for test '{testName}' in S3 bucket '{_configuration.BucketName}'");

        var keys = await _backend.ListKeysAsync(_configuration.BucketName, prefix, cancellationToken).ConfigureAwait(false);
        var artifacts = keys
            .Select(key => GenerateS3Uri(_configuration.BucketName, key))
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

        var (bucket, key) = ParseConfiguredS3ObjectUri(storageLocation);

        _logger?.Info($"Deleting artifact from S3 bucket '{bucket}' key '{key}'");

        await _backend.DeleteObjectAsync(bucket, key, cancellationToken).ConfigureAwait(false);

        _logger?.Info("Artifact deleted successfully from S3");
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

    private (string bucket, string key) ParseConfiguredS3ObjectUri(string s3Uri)
    {
        var (bucket, key) = ParseS3Uri(s3Uri);

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new FormatException($"Invalid S3 URI format (missing key): {s3Uri}");
        }

        if (!string.Equals(bucket, _configuration.BucketName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"S3 URI bucket '{bucket}' does not match configured bucket '{_configuration.BucketName}'");
        }

        return (bucket, key);
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

    private static string SanitizeKey(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars().Concat(Enumerable.Range(0, 32).Select(i => (char)i)).Distinct())
        {
            name = name.Replace(c, '_');
        }

        return name.Replace('\\', '_').Replace('/', '_');
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Parses an S3 URI (s3://bucket/key) into its bucket and key components.
    /// </summary>
    public static (string bucket, string key) ParseS3Uri(string s3Uri)
    {
        if (string.IsNullOrEmpty(s3Uri) || !s3Uri.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Invalid S3 URI format. Expected s3://bucket/key but got: {s3Uri}");
        }

        var path = s3Uri[5..];
        var slashIndex = path.IndexOf('/');
        if (slashIndex < 0)
        {
            return (path, string.Empty);
        }

        return (path[..slashIndex], path[(slashIndex + 1)..]);
    }

    /// <summary>
    /// Generates an S3 key from the given components.
    /// </summary>
    public static string GenerateS3Key(string keyPrefix, string testName, string timestamp, string artifactName)
    {
        var sanitized = SanitizeKey(testName);
        return $"{keyPrefix}{sanitized}/{timestamp}/{artifactName}";
    }

    /// <summary>
    /// Generates an S3 URI from a bucket name and key.
    /// </summary>
    public static string GenerateS3Uri(string bucketName, string key)
    {
        return $"s3://{bucketName}/{key}";
    }
}

internal interface IS3StorageBackend : IDisposable
{
    Task UploadFileAsync(string bucketName, string key, string sourcePath, CancellationToken cancellationToken);

    Task UploadBytesAsync(string bucketName, string key, byte[] content, CancellationToken cancellationToken);

    Task DownloadFileAsync(string bucketName, string key, string destinationPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListKeysAsync(string bucketName, string prefix, CancellationToken cancellationToken);

    Task DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken);
}

internal sealed class AwsS3StorageBackend : IS3StorageBackend
{
    private readonly IAmazonS3 _s3Client;

    public AwsS3StorageBackend(S3StorageConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _s3Client = CreateClient(configuration);
    }

    public async Task UploadFileAsync(string bucketName, string key, string sourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Artifact not found: {sourcePath}", sourcePath);
        }

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            FilePath = sourcePath,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };

        await _s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadBytesAsync(string bucketName, string key, byte[] content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new MemoryStream(content, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };

        await _s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadFileAsync(string bucketName, string key, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var response = await _s3Client.GetObjectAsync(bucketName, key, cancellationToken).ConfigureAwait(false);
            await using var responseStream = response.ResponseStream;
            await using var fileStream = File.Create(destinationPath);
            await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            throw new FileNotFoundException($"Artifact not found: s3://{bucketName}/{key}", key, ex);
        }
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string bucketName, string prefix, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix
        };

        var keys = new List<string>();
        ListObjectsV2Response response;
        do
        {
            response = await _s3Client.ListObjectsV2Async(request, cancellationToken).ConfigureAwait(false);
            keys.AddRange(response.S3Objects.Select(obj => obj.Key));
            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated == true);

        return keys;
    }

    public async Task DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new DeleteObjectRequest
        {
            BucketName = bucketName,
            Key = key
        };

        await _s3Client.DeleteObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _s3Client.Dispose();
    }

    private static IAmazonS3 CreateClient(S3StorageConfiguration configuration)
    {
        var clientConfig = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(configuration.Region)
        };

        if (!string.IsNullOrWhiteSpace(configuration.ServiceUrl))
        {
            clientConfig.ServiceURL = configuration.ServiceUrl;
            clientConfig.ForcePathStyle = configuration.UsePathStyle;
            clientConfig.AuthenticationRegion = configuration.Region;
        }

        var hasAccessKey = !string.IsNullOrWhiteSpace(configuration.AccessKeyId);
        var hasSecretKey = !string.IsNullOrWhiteSpace(configuration.SecretAccessKey);
        if (hasAccessKey != hasSecretKey)
        {
            throw new ArgumentException("S3 access key and secret key must either both be supplied or both be omitted.");
        }

        if (hasAccessKey)
        {
            var credentials = new BasicAWSCredentials(configuration.AccessKeyId, configuration.SecretAccessKey);
            return new AmazonS3Client(credentials, clientConfig);
        }

        return new AmazonS3Client(clientConfig);
    }

    private static bool IsNotFound(AmazonS3Exception ex)
    {
        return ex.StatusCode == HttpStatusCode.NotFound ||
               string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ex.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Configuration for S3-compatible storage.
/// </summary>
public sealed class S3StorageConfiguration
{
    /// <summary>
    /// S3 bucket name where artifacts will be stored.
    /// </summary>
    public string BucketName { get; init; } = string.Empty;

    /// <summary>
    /// AWS Access Key ID or equivalent for S3-compatible services.
    /// </summary>
    public string AccessKeyId { get; init; } = string.Empty;

    /// <summary>
    /// AWS Secret Access Key or equivalent for S3-compatible services.
    /// </summary>
    public string SecretAccessKey { get; init; } = string.Empty;

    /// <summary>
    /// S3 service URL. For AWS S3, leave null to use default. For S3-compatible services (MinIO, etc.), provide custom endpoint.
    /// </summary>
    public string? ServiceUrl { get; init; }

    /// <summary>
    /// AWS region. Default: us-east-1.
    /// </summary>
    public string Region { get; init; } = "us-east-1";

    /// <summary>
    /// Key prefix for all uploaded artifacts. Useful for organizing artifacts within a shared bucket.
    /// </summary>
    public string KeyPrefix { get; init; } = "recorded-tests/";

    /// <summary>
    /// Use path-style addressing (required for some S3-compatible services). Default: false.
    /// </summary>
    public bool UsePathStyle { get; init; } = false;
}
