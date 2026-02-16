using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using RecordedTests.Shared.Tests.TestInfrastructure;

namespace RecordedTests.Shared.Tests;

public sealed class RecordedTestRunnerTests
{
    [Fact]
    public async Task RunAsync_PersistsArtifactsAndMetadata()
    {
        using var tempDir = new TempDirectory();
        var storageRoot = Path.Combine(tempDir.Path, "storage");

        var storage = new SpyStorage(new FileSystemRecordedTestStorage(storageRoot));

        var configuration = new RecordedTestE2EConfiguration
        {
            TestName = "Recorded.HoggerScenario",
            ForegroundFactory = new DelegateBotRunnerFactory(() => new StubBotRunner("Foreground")),
            BackgroundFactory = new DelegateBotRunnerFactory(() => new StubBotRunner("Background", createArtifact: true)),
            OrchestrationOptions = new OrchestrationOptions
            {
                ArtifactsRootDirectory = tempDir.Path,
                DoubleStopRecorderForSafety = false
            },
            ArtifactStorage = storage,
            ServerAvailabilityOverride = new ImmediateServerAvailability(new ServerInfo("127.0.0.1", 3724, "TestRealm")),
            AutomationRunId = "run-123",
            Metadata = new Dictionary<string, string?>
            {
                ["build"] = "1.2.3"
            }
        };

        var runner = new RecordedTestRunner(configuration);

        var result = await runner.RunAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.TestRunDirectory);

        var captured = Assert.IsType<RecordedTestStorageContext>(storage.CapturedContext);
        Assert.Equal(configuration.TestName, captured.TestName);
        Assert.Equal(configuration.AutomationRunId, captured.AutomationRunId);
        Assert.Equal(result.TestRunDirectory, captured.TestRunDirectory);
        Assert.False(string.IsNullOrWhiteSpace(captured.Message));

        Assert.True(Directory.Exists(storageRoot));
        var sanitizedName = ArtifactPathHelper.SanitizeName(configuration.TestName);
        var scenarioRoot = Path.Combine(storageRoot, sanitizedName);
        Assert.True(Directory.Exists(scenarioRoot));
        var runDirectories = Directory.GetDirectories(scenarioRoot);
        Assert.Single(runDirectories);
        var storedRunDirectory = runDirectories.Single();
        Assert.True(File.Exists(Path.Combine(storedRunDirectory, "Background.txt")));

        var metadataPath = Path.Combine(storedRunDirectory, "run-metadata.json");
        Assert.True(File.Exists(metadataPath));
        using var metadataDocument = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath));
        Assert.True(metadataDocument.RootElement.TryGetProperty("metadata.build", out var buildValue));
        Assert.Equal("1.2.3", buildValue.GetString());
    }

    private sealed class ImmediateServerAvailability(ServerInfo serverInfo) : IServerAvailabilityChecker
    {
        private readonly ServerInfo _serverInfo = serverInfo;

        public Task<ServerInfo?> WaitForAvailableAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult<ServerInfo?>(_serverInfo);
        }
    }

    private sealed class StubBotRunner(string name, bool createArtifact = false) : IBotRunner
    {
        private readonly string _name = name;
        private readonly bool _createArtifact = createArtifact;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new RecordingTarget(RecordingTargetType.WindowByTitle, _name));
        }

        public Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken)
        {
            if (_createArtifact)
            {
                Directory.CreateDirectory(context.TestRunDirectory);
                var path = Path.Combine(context.TestRunDirectory, $"{_name}.txt");
                await File.WriteAllTextAsync(path, _name, cancellationToken).ConfigureAwait(false);
            }
        }

        public Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ShutdownUiAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SpyStorage(IRecordedTestStorage inner) : IRecordedTestStorage
        {
            private readonly IRecordedTestStorage _inner = inner;

        public RecordedTestStorageContext? CapturedContext { get; private set; }

            public async Task StoreAsync(RecordedTestStorageContext context, CancellationToken cancellationToken)
            {
                CapturedContext = context;
                await _inner.StoreAsync(context, cancellationToken).ConfigureAwait(false);
            }

            public Task<string> UploadArtifactAsync(TestArtifact artifact, string testName, DateTimeOffset timestamp, CancellationToken cancellationToken)
                => _inner.UploadArtifactAsync(artifact, testName, timestamp, cancellationToken);

            public Task DownloadArtifactAsync(string storageLocation, string localDestinationPath, CancellationToken cancellationToken)
                => _inner.DownloadArtifactAsync(storageLocation, localDestinationPath, cancellationToken);

            public Task<IReadOnlyList<string>> ListArtifactsAsync(string testName, CancellationToken cancellationToken)
                => _inner.ListArtifactsAsync(testName, cancellationToken);

            public Task DeleteArtifactAsync(string storageLocation, CancellationToken cancellationToken)
                => _inner.DeleteArtifactAsync(storageLocation, cancellationToken);

            public void Dispose() => _inner.Dispose();
        }
    }
