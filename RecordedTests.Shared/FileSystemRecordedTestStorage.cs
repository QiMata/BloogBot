namespace RecordedTests.Shared;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;

public sealed class FileSystemRecordedTestStorage : IRecordedTestStorage
{
    private readonly string _rootDirectory;
    private readonly ITestLogger _logger;
    private bool _disposed;

    public FileSystemRecordedTestStorage(string rootDirectory, ITestLogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));
        }

        _rootDirectory = rootDirectory;
        _logger = logger ?? new NullTestLogger();
    }

    public async Task<string> UploadArtifactAsync(
        TestArtifact artifact,
        string testName,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (string.IsNullOrWhiteSpace(testName))
        {
            throw new ArgumentException("Test name must be provided.", nameof(testName));
        }

        var sanitizedName = ArtifactPathHelper.SanitizeName(testName);
        var timestampStr = timestamp.ToUniversalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var destinationDir = Path.Combine(_rootDirectory, sanitizedName, timestampStr);

        Directory.CreateDirectory(destinationDir);

        var destinationPath = Path.Combine(destinationDir, artifact.Name);
        
        if (File.Exists(artifact.FullPath))
        {
            File.Copy(artifact.FullPath, destinationPath, overwrite: true);
        }

        _logger.Info($"[Storage] Uploaded artifact '{artifact.Name}' to '{destinationPath}'.");
        return destinationPath;
    }

    public Task DownloadArtifactAsync(
        string storageLocation,
        string localDestinationPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageLocation))
        {
            throw new ArgumentException("Storage location must be provided.", nameof(storageLocation));
        }
        if (string.IsNullOrWhiteSpace(localDestinationPath))
        {
            throw new ArgumentException("Local destination path must be provided.", nameof(localDestinationPath));
        }

        if (!File.Exists(storageLocation))
        {
            throw new FileNotFoundException($"Artifact not found at '{storageLocation}'.", storageLocation);
        }

        var destinationDir = Path.GetDirectoryName(localDestinationPath);
        if (!string.IsNullOrEmpty(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        File.Copy(storageLocation, localDestinationPath, overwrite: true);
        _logger.Info($"[Storage] Downloaded artifact from '{storageLocation}' to '{localDestinationPath}'.");

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListArtifactsAsync(
        string testName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(testName))
        {
            throw new ArgumentException("Test name must be provided.", nameof(testName));
        }

        var sanitizedName = ArtifactPathHelper.SanitizeName(testName);
        var testDir = Path.Combine(_rootDirectory, sanitizedName);

        if (!Directory.Exists(testDir))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var artifacts = Directory.EnumerateFiles(testDir, "*", SearchOption.AllDirectories).ToList();
        return Task.FromResult<IReadOnlyList<string>>(artifacts);
    }

    public Task DeleteArtifactAsync(
        string storageLocation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageLocation))
        {
            throw new ArgumentException("Storage location must be provided.", nameof(storageLocation));
        }

        if (File.Exists(storageLocation))
        {
            File.Delete(storageLocation);
            _logger.Info($"[Storage] Deleted artifact at '{storageLocation}'.");
        }

        return Task.CompletedTask;
    }

    public async Task StoreAsync(RecordedTestStorageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.TestRunDirectory) || !Directory.Exists(context.TestRunDirectory))
        {
            _logger.Warn($"[Storage] Test run directory '{context.TestRunDirectory}' not found; skipping artifact persistence.");
            return;
        }

        var sanitizedName = ArtifactPathHelper.SanitizeName(context.TestName);
        var timestamp = context.CompletedAt.ToUniversalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var destinationRoot = Path.Combine(_rootDirectory, sanitizedName, timestamp);

        Directory.CreateDirectory(destinationRoot);

        _logger.Info($"[Storage] Copying artifacts for '{context.TestName}' to '{destinationRoot}'.");

        CopyDirectory(context.TestRunDirectory, destinationRoot, cancellationToken);

        if (context.RecordingArtifact is not null && File.Exists(context.RecordingArtifact.FullPath))
        {
            var destinationRecording = Path.Combine(destinationRoot, Path.GetFileName(context.RecordingArtifact.FullPath));
            if (!File.Exists(destinationRecording))
            {
                File.Copy(context.RecordingArtifact.FullPath, destinationRecording);
            }
        }

        await WriteMetadataAsync(destinationRoot, context, cancellationToken).ConfigureAwait(false);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        var sourceInfo = new DirectoryInfo(sourceDirectory);
        if (!sourceInfo.Exists)
        {
            return;
        }

        var stack = new Stack<DirectoryInfo>();
        stack.Push(sourceInfo);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            var relativePath = Path.GetRelativePath(sourceInfo.FullName, current.FullName);
            var target = relativePath == "."
                ? destinationDirectory
                : Path.Combine(destinationDirectory, relativePath);

            Directory.CreateDirectory(target);

            foreach (var file in current.GetFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destinationFile = Path.Combine(target, file.Name);
                file.CopyTo(destinationFile, overwrite: true);
            }

            foreach (var subDirectory in current.GetDirectories())
            {
                stack.Push(subDirectory);
            }
        }
    }

    private static async Task WriteMetadataAsync(string destinationRoot, RecordedTestStorageContext context, CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["testName"] = context.TestName,
            ["automationRunId"] = context.AutomationRunId,
            ["success"] = context.Success,
            ["message"] = context.Message,
            ["startedAtUtc"] = context.StartedAt.ToUniversalTime(),
            ["completedAtUtc"] = context.CompletedAt.ToUniversalTime()
        };

        if (context.Metadata is not null)
        {
            foreach (var pair in context.Metadata)
            {
                metadata[$"metadata.{pair.Key}"] = pair.Value;
            }
        }

        if (context.RecordingArtifact is not null)
        {
            metadata["recording"] = new
            {
                context.RecordingArtifact.Name,
                context.RecordingArtifact.FullPath
            };
        }

        var metadataPath = Path.Combine(destinationRoot, "run-metadata.json");
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(metadataPath, json, cancellationToken).ConfigureAwait(false);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                // No unmanaged resources to release
            }
        }
