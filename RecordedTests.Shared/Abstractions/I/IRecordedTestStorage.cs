namespace RecordedTests.Shared.Abstractions.I;

/// <summary>
/// Abstraction for storing test artifacts (recordings, logs, etc.) to various backends.
/// Implementations can target local filesystem, cloud storage (S3, Azure Blob), etc.
/// </summary>
public interface IRecordedTestStorage : IDisposable
{
    /// <summary>
    /// Stores all artifacts from a test run to the storage backend.
    /// </summary>
    /// <param name="context">The storage context containing test artifacts and metadata.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task StoreAsync(RecordedTestStorageContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a test artifact to the storage backend.
    /// </summary>
    /// <param name="artifact">The artifact to upload.</param>
    /// <param name="testName">Name of the test that produced the artifact.</param>
    /// <param name="timestamp">Timestamp when the test was run.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The storage location/URL where the artifact was uploaded.</returns>
    Task<string> UploadArtifactAsync(
        TestArtifact artifact,
        string testName,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a test artifact from the storage backend.
    /// </summary>
    /// <param name="storageLocation">The storage location/URL of the artifact.</param>
    /// <param name="localDestinationPath">Local path where the artifact should be saved.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task DownloadArtifactAsync(
        string storageLocation,
        string localDestinationPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all artifacts for a specific test.
    /// </summary>
    /// <param name="testName">Name of the test.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Collection of storage locations for the test's artifacts.</returns>
    Task<IReadOnlyList<string>> ListArtifactsAsync(
        string testName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an artifact from storage.
    /// </summary>
    /// <param name="storageLocation">The storage location/URL of the artifact to delete.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task DeleteArtifactAsync(
        string storageLocation,
        CancellationToken cancellationToken);
}
