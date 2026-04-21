using RecordedTests.Shared.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Tests.Storage;

internal sealed class InMemoryS3StorageBackend : IS3StorageBackend
{
    private readonly Dictionary<(string Bucket, string Key), byte[]> _objects = new();

    public void Seed(string bucketName, string key, string content)
    {
        _objects[(bucketName, key)] = Encoding.UTF8.GetBytes(content);
    }

    public string ReadText(string bucketName, string key)
    {
        return Encoding.UTF8.GetString(_objects[(bucketName, key)]);
    }

    public bool Contains(string bucketName, string key)
    {
        return _objects.ContainsKey((bucketName, key));
    }

    public Task UploadFileAsync(string bucketName, string key, string sourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _objects[(bucketName, key)] = File.ReadAllBytes(sourcePath);
        return Task.CompletedTask;
    }

    public Task UploadBytesAsync(string bucketName, string key, byte[] content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _objects[(bucketName, key)] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task DownloadFileAsync(string bucketName, string key, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_objects.TryGetValue((bucketName, key), out var content))
        {
            throw new FileNotFoundException($"Artifact not found: s3://{bucketName}/{key}", key);
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(destinationPath, content);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(string bucketName, string prefix, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var keys = _objects.Keys
            .Where(entry => entry.Bucket == bucketName && entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    public Task DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _objects.Remove((bucketName, key));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}

internal sealed class InMemoryAzureBlobStorageBackend : IAzureBlobStorageBackend
{
    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public void Seed(string blobName, string content)
    {
        _blobs[blobName] = Encoding.UTF8.GetBytes(content);
    }

    public string ReadText(string blobName)
    {
        return Encoding.UTF8.GetString(_blobs[blobName]);
    }

    public bool Contains(string blobName)
    {
        return _blobs.ContainsKey(blobName);
    }

    public Task UploadFileAsync(string blobName, string sourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _blobs[blobName] = File.ReadAllBytes(sourcePath);
        return Task.CompletedTask;
    }

    public Task UploadBytesAsync(string blobName, byte[] content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _blobs[blobName] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task DownloadFileAsync(string blobName, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_blobs.TryGetValue(blobName, out var content))
        {
            throw new FileNotFoundException($"Artifact not found: {blobName}", blobName);
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(destinationPath, content);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListBlobNamesAsync(string prefix, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var blobNames = _blobs.Keys
            .Where(blobName => blobName.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(blobName => blobName, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(blobNames);
    }

    public Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _blobs.Remove(blobName);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
