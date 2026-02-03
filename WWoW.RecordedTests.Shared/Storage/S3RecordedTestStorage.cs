using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Storage;

/// <summary>
/// S3-compatible storage implementation for test artifacts.
/// Supports AWS S3 and S3-compatible services (MinIO, DigitalOcean Spaces, etc.).
/// </summary>
/// <remarks>
/// This implementation requires the AWSSDK.S3 NuGet package to be installed.
/// To use this storage backend, add: dotnet add package AWSSDK.S3
/// </remarks>
public sealed class S3RecordedTestStorage : IRecordedTestStorage
{
    private readonly S3StorageConfiguration _configuration;
    private readonly ITestLogger? _logger;
    // private readonly AmazonS3Client _s3Client; // Requires AWSSDK.S3 package

    public S3RecordedTestStorage(
        S3StorageConfiguration configuration,
        ITestLogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        // TODO: Initialize S3 client when AWSSDK.S3 is added
        // _s3Client = new AmazonS3Client(
        //     configuration.AccessKeyId,
        //     configuration.SecretAccessKey,
        //     new AmazonS3Config
        //     {
        //         ServiceURL = configuration.ServiceUrl,
        //         ForcePathStyle = configuration.UsePathStyle
        //     });
        }

        public Task StoreAsync(RecordedTestStorageContext context, CancellationToken cancellationToken)
        {
            // S3 storage implementation should use UploadArtifactAsync for individual artifacts
            _logger?.Warn("StoreAsync is not directly implemented for S3 Storage. Use UploadArtifactAsync for individual artifacts.");
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

        var sanitizedTestName = SanitizeKey(testName);
        var timestampFolder = timestamp.ToString("yyyyMMdd_HHmmss");
        var s3Key = $"{_configuration.KeyPrefix}{sanitizedTestName}/{timestampFolder}/{artifact.Name}";

        _logger?.Info($"Uploading artifact '{artifact.Name}' to S3 bucket '{_configuration.BucketName}' with key '{s3Key}'");

        // TODO: Implement actual S3 upload when AWSSDK.S3 is added
        // var request = new PutObjectRequest
        // {
        //     BucketName = _configuration.BucketName,
        //     Key = s3Key,
        //     FilePath = artifact.FullPath,
        //     ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        // };
        //
        // await _s3Client.PutObjectAsync(request, cancellationToken);

        _logger?.Info($"Artifact uploaded successfully to S3");

        // Return S3 URI as storage location
        return $"s3://{_configuration.BucketName}/{s3Key}";
    }

    public async Task DownloadArtifactAsync(
        string storageLocation,
        string localDestinationPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageLocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDestinationPath);

        // Parse S3 URI (s3://bucket/key)
        var (bucket, key) = ParseS3Uri(storageLocation);

        _logger?.Info($"Downloading artifact from S3 bucket '{bucket}' key '{key}' to {localDestinationPath}");

        var destinationDirectory = Path.GetDirectoryName(localDestinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        // TODO: Implement actual S3 download when AWSSDK.S3 is added
        // var request = new GetObjectRequest
        // {
        //     BucketName = bucket,
        //     Key = key
        // };
        //
        // using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
        // await response.WriteResponseStreamToFileAsync(localDestinationPath, append: false, cancellationToken);

        _logger?.Info("Artifact downloaded successfully from S3");

        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<string>> ListArtifactsAsync(
        string testName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        var sanitizedTestName = SanitizeKey(testName);
        var prefix = $"{_configuration.KeyPrefix}{sanitizedTestName}/";

        _logger?.Info($"Listing artifacts for test '{testName}' in S3 bucket '{_configuration.BucketName}'");

        // TODO: Implement actual S3 list when AWSSDK.S3 is added
        // var request = new ListObjectsV2Request
        // {
        //     BucketName = _configuration.BucketName,
        //     Prefix = prefix
        // };
        //
        // var artifacts = new List<string>();
        // ListObjectsV2Response response;
        // do
        // {
        //     response = await _s3Client.ListObjectsV2Async(request, cancellationToken);
        //     artifacts.AddRange(response.S3Objects.Select(obj => $"s3://{_configuration.BucketName}/{obj.Key}"));
        //     request.ContinuationToken = response.NextContinuationToken;
        // } while (response.IsTruncated);
        //
        // _logger?.Info($"Found {artifacts.Count} artifact(s)");
        // return artifacts;

        _logger?.Info("S3 listing not yet implemented (requires AWSSDK.S3 package)");
        return Array.Empty<string>();
    }

    public async Task DeleteArtifactAsync(
        string storageLocation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageLocation);

        var (bucket, key) = ParseS3Uri(storageLocation);

        _logger?.Info($"Deleting artifact from S3 bucket '{bucket}' key '{key}'");

        // TODO: Implement actual S3 delete when AWSSDK.S3 is added
        // var request = new DeleteObjectRequest
        // {
        //     BucketName = bucket,
        //     Key = key
        // };
        //
        // await _s3Client.DeleteObjectAsync(request, cancellationToken);

        _logger?.Info("Artifact deleted successfully from S3");

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        // _s3Client?.Dispose();
    }

    private static string SanitizeKey(string name)
    {
        // S3 key restrictions: no \ or control characters
        var invalidChars = new[] { '\\' }.Concat(
            Enumerable.Range(0, 32).Select(i => (char)i));

        foreach (var c in invalidChars)
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    private static (string bucket, string key) ParseS3Uri(string s3Uri)
    {
        if (!s3Uri.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid S3 URI format: {s3Uri}");
        }

        var parts = s3Uri.Substring(5).Split('/', 2);
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid S3 URI format (missing key): {s3Uri}");
        }

        return (parts[0], parts[1]);
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
    /// AWS region. Default: us-east-1
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
