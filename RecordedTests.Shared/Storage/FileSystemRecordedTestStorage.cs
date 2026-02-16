using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Storage;

/// <summary>
/// File system-based storage implementation for test artifacts.
/// Stores artifacts in a hierarchical directory structure organized by test name and timestamp.
/// </summary>
public sealed class FileSystemRecordedTestStorage : IRecordedTestStorage
{
    private readonly string _rootDirectory;
    private readonly ITestLogger? _logger;

    /// <summary>
    /// Creates a new filesystem storage instance.
    /// </summary>
    /// <param name="rootDirectory">Root directory for storing all test artifacts.</param>
    /// <param name="logger">Optional logger for storage operations.</param>
    public FileSystemRecordedTestStorage(string rootDirectory, ITestLogger? logger = null)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        _logger = logger;

        Directory.CreateDirectory(_rootDirectory);
        }

        public async Task StoreAsync(RecordedTestStorageContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            var sanitizedTestName = SanitizeFileName(context.TestName);
            var timestampFolder = context.CompletedAt.ToString("yyyyMMdd_HHmmss");
            var testDirectory = Path.Combine(_rootDirectory, sanitizedTestName, timestampFolder);

            Directory.CreateDirectory(testDirectory);

            _logger?.Info($"Storing test artifacts for '{context.TestName}' in {testDirectory}");

            // Copy test run directory contents if specified
            if (!string.IsNullOrWhiteSpace(context.TestRunDirectory) && Directory.Exists(context.TestRunDirectory))
            {
                foreach (var file in Directory.GetFiles(context.TestRunDirectory))
                {
                    var destPath = Path.Combine(testDirectory, Path.GetFileName(file));
                    File.Copy(file, destPath, overwrite: true);
                }
            }

            // Copy recording artifact if present
            if (context.RecordingArtifact is not null && File.Exists(context.RecordingArtifact.FullPath))
            {
                var destPath = Path.Combine(testDirectory, context.RecordingArtifact.Name);
                File.Copy(context.RecordingArtifact.FullPath, destPath, overwrite: true);
            }

            // Write metadata
            var metadataPath = Path.Combine(testDirectory, "metadata.json");
            var metadata = new Dictionary<string, object?>
            {
                ["testName"] = context.TestName,
                ["automationRunId"] = context.AutomationRunId,
                ["success"] = context.Success,
                ["message"] = context.Message,
                ["startedAt"] = context.StartedAt,
                ["completedAt"] = context.CompletedAt
            };

            var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataPath, json, cancellationToken);

            _logger?.Info($"Test artifacts stored successfully");
        }

        public Task<string> UploadArtifactAsync(
        TestArtifact artifact,
        string testName,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        var sanitizedTestName = SanitizeFileName(testName);
        var timestampFolder = timestamp.ToString("yyyyMMdd_HHmmss");
        var testDirectory = Path.Combine(_rootDirectory, sanitizedTestName, timestampFolder);

        Directory.CreateDirectory(testDirectory);

        var destinationPath = Path.Combine(testDirectory, artifact.Name);

        _logger?.Info($"Uploading artifact '{artifact.Name}' to {destinationPath}");

        // Copy the file to the destination (overwrite if exists)
        File.Copy(artifact.FullPath, destinationPath, overwrite: true);

        _logger?.Info($"Artifact uploaded successfully");

        // Return relative path from root as the storage location
        var relativePath = Path.GetRelativePath(_rootDirectory, destinationPath);
        return Task.FromResult(relativePath);
    }

    public Task DownloadArtifactAsync(
        string storageLocation,
        string localDestinationPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageLocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDestinationPath);

        var sourcePath = Path.Combine(_rootDirectory, storageLocation);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Artifact not found: {sourcePath}");
        }

        _logger?.Info($"Downloading artifact from {sourcePath} to {localDestinationPath}");

        var destinationDirectory = Path.GetDirectoryName(localDestinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, localDestinationPath, overwrite: true);

        _logger?.Info("Artifact downloaded successfully");

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListArtifactsAsync(
        string testName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        var sanitizedTestName = SanitizeFileName(testName);
        var testDirectory = Path.Combine(_rootDirectory, sanitizedTestName);

        if (!Directory.Exists(testDirectory))
        {
            _logger?.Info($"No artifacts found for test '{testName}'");
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        _logger?.Info($"Listing artifacts for test '{testName}'");

        // Find all files in all timestamp subdirectories
        var artifacts = Directory.GetFiles(testDirectory, "*", SearchOption.AllDirectories)
            .Select(fullPath => Path.GetRelativePath(_rootDirectory, fullPath))
            .ToArray();

        _logger?.Info($"Found {artifacts.Length} artifact(s)");

        return Task.FromResult<IReadOnlyList<string>>(artifacts);
    }

    public Task DeleteArtifactAsync(
        string storageLocation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageLocation);

        var fullPath = Path.Combine(_rootDirectory, storageLocation);

        if (!File.Exists(fullPath))
        {
            _logger?.Warn($"Artifact not found, cannot delete: {fullPath}");
            return Task.CompletedTask;
        }

        _logger?.Info($"Deleting artifact: {fullPath}");

        File.Delete(fullPath);

        _logger?.Info("Artifact deleted successfully");

        // Clean up empty directories
        var directory = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(directory) &&
               directory.StartsWith(_rootDirectory) &&
               Directory.Exists(directory) &&
               !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            _logger?.Info($"Removing empty directory: {directory}");
            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // No resources to dispose for filesystem storage
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}
