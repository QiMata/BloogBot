using FluentAssertions;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using RecordedTests.Shared.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Tests.Storage;

public sealed class RecordedTestStorageProviderParityTests : IDisposable
{
    private readonly string _rootDirectory;

    public RecordedTestStorageProviderParityTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"RecordedStorageParity_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Providers_UploadDownloadListAndDelete_WithEquivalentSemantics()
    {
        using var artifact = new TempArtifactFile("artifact.txt", "provider parity content");
        var timestamp = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);

        foreach (var provider in CreateStorageCases())
        {
            using (provider)
            {
                var location = await provider.Storage.UploadArtifactAsync(
                    artifact.Artifact,
                    "Parity/Test:Name",
                    timestamp,
                    CancellationToken.None);

                var artifacts = await provider.Storage.ListArtifactsAsync("Parity/Test:Name", CancellationToken.None);
                artifacts.Should().Contain(location, provider.Name);

                var downloadPath = Path.Combine(_rootDirectory, provider.Name, "downloaded.txt");
                await provider.Storage.DownloadArtifactAsync(location, downloadPath, CancellationToken.None);
                File.ReadAllText(downloadPath).Should().Be("provider parity content", provider.Name);

                await provider.Storage.DeleteArtifactAsync(location, CancellationToken.None);
                var artifactsAfterDelete = await provider.Storage.ListArtifactsAsync("Parity/Test:Name", CancellationToken.None);
                artifactsAfterDelete.Should().NotContain(location, provider.Name);

                var secondDelete = async () => await provider.Storage.DeleteArtifactAsync(location, CancellationToken.None);
                await secondDelete.Should().NotThrowAsync(provider.Name);
            }
        }
    }

    [Fact]
    public async Task Providers_DownloadMissingArtifact_ThrowsFileNotFoundException()
    {
        foreach (var provider in CreateStorageCases())
        {
            using (provider)
            {
                var destination = Path.Combine(_rootDirectory, provider.Name, "missing.txt");

                var act = async () => await provider.Storage.DownloadArtifactAsync(
                    provider.MissingArtifactLocation,
                    destination,
                    CancellationToken.None);

                await act.Should().ThrowAsync<FileNotFoundException>(provider.Name);
            }
        }
    }

    [Fact]
    public async Task Providers_RespectCanceledTokenBeforeBackendWork()
    {
        using var artifact = new TempArtifactFile("artifact.txt", "provider parity content");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        foreach (var provider in CreateStorageCases())
        {
            using (provider)
            {
                await AssertCanceled(
                    () => provider.Storage.UploadArtifactAsync(artifact.Artifact, "CanceledTest", DateTimeOffset.UtcNow, cts.Token),
                    provider.Name);

                await AssertCanceled(
                    () => provider.Storage.DownloadArtifactAsync(provider.MissingArtifactLocation, Path.Combine(_rootDirectory, provider.Name, "missing.txt"), cts.Token),
                    provider.Name);

                await AssertCanceled(
                    () => provider.Storage.ListArtifactsAsync("CanceledTest", cts.Token),
                    provider.Name);

                await AssertCanceled(
                    () => provider.Storage.DeleteArtifactAsync(provider.MissingArtifactLocation, cts.Token),
                    provider.Name);
            }
        }
    }

    [Fact]
    public async Task Providers_StoreAsync_WritesRunMetadataArtifact()
    {
        var completedAt = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        var context = new RecordedTestStorageContext(
            TestName: "StoreParity",
            AutomationRunId: "run-42",
            Success: true,
            Message: "ok",
            TestRunDirectory: null,
            RecordingArtifact: null,
            StartedAt: completedAt.AddMinutes(-1),
            CompletedAt: completedAt,
            Metadata: new Dictionary<string, string?> { ["map"] = "1" });

        foreach (var provider in CreateStorageCases())
        {
            using (provider)
            {
                await provider.Storage.StoreAsync(context, CancellationToken.None);

                var artifacts = await provider.Storage.ListArtifactsAsync("StoreParity", CancellationToken.None);
                artifacts.Should().Contain(location => location.Contains("run-metadata.json"), provider.Name);
            }
        }
    }

    private IReadOnlyList<StorageCase> CreateStorageCases()
    {
        var s3Config = new S3StorageConfiguration
        {
            BucketName = "test-bucket",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret"
        };

        var azureConfig = new AzureBlobStorageConfiguration
        {
            AccountName = "testaccount",
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            ContainerName = "test-container"
        };

        return new[]
        {
            new StorageCase(
                "filesystem",
                new FileSystemRecordedTestStorage(Path.Combine(_rootDirectory, "filesystem")),
                "missing/file.txt"),
            new StorageCase(
                "s3",
                new S3RecordedTestStorage(s3Config, new InMemoryS3StorageBackend()),
                "s3://test-bucket/recorded-tests/Missing/file.txt"),
            new StorageCase(
                "azure",
                new AzureBlobRecordedTestStorage(azureConfig, new InMemoryAzureBlobStorageBackend()),
                "https://testaccount.blob.core.windows.net/test-container/recorded-tests/Missing/file.txt")
        };
    }

    private static async Task AssertCanceled(Func<Task> action, string providerName)
    {
        await action.Should().ThrowAsync<OperationCanceledException>(providerName);
    }

    private sealed record StorageCase(
        string Name,
        IRecordedTestStorage Storage,
        string MissingArtifactLocation) : IDisposable
    {
        public void Dispose()
        {
            Storage.Dispose();
        }
    }

    private sealed class TempArtifactFile : IDisposable
    {
        private readonly string _path;

        public TempArtifactFile(string artifactName, string content)
        {
            _path = Path.Combine(Path.GetTempPath(), $"wwow-storage-parity-{Guid.NewGuid():N}-{artifactName}");
            File.WriteAllText(_path, content);
            Artifact = new TestArtifact(artifactName, _path);
        }

        public TestArtifact Artifact { get; }

        public void Dispose()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }
}
