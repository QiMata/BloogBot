namespace RecordedTests.Shared;

using System;
using System.Threading;
using System.Threading.Tasks;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;

public sealed class RecordedTestOrchestrator
{
    private readonly IServerAvailabilityChecker _serverChecker;
    private readonly ITestLogger _logger;
    private readonly OrchestrationOptions _options;

    public RecordedTestOrchestrator(
        IServerAvailabilityChecker serverChecker,
        OrchestrationOptions? options = null,
        ITestLogger? logger = null)
    {
        _serverChecker = serverChecker ?? throw new ArgumentNullException(nameof(serverChecker));
        _options = options ?? new OrchestrationOptions();
        _logger = logger ?? new NullTestLogger();
    }

    // The orchestrator's single responsibility: find an available server, then delegate execution to the test description.
    public async Task<OrchestrationResult> RunAsync(ITestDescription test, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(test);

        _logger.Info($"[Orchestrator] Waiting for server availability (timeout: {_options.ServerAvailabilityTimeout}).");
        var server = await _serverChecker.WaitForAvailableAsync(_options.ServerAvailabilityTimeout, cancellationToken);
        if (server is null)
        {
            var msg = "Server not available within timeout.";
            _logger.Error(msg);
            return new OrchestrationResult(false, msg);
        }

        _logger.Info($"[Orchestrator] Server available at {server.Host}:{server.Port}. Delegating to test description '{test.Name}'.");

        var startedAt = DateTimeOffset.UtcNow;
        ArtifactPathHelper.ArtifactPathInfo artifactPaths;
        try
        {
            artifactPaths = ArtifactPathHelper.PrepareArtifactDirectories(_options.ArtifactsRootDirectory, test.Name, startedAt);
        }
        catch (Exception ex)
        {
            var msg = $"Failed to prepare artifact directories: {ex.Message}";
            _logger.Error(msg, ex);
            return new OrchestrationResult(false, msg);
        }

        _logger.Info($"[Orchestrator] Artifacts will be stored under '{artifactPaths.TestRunDirectory}'.");

        var context = new RecordedTestContext(
            test.Name,
            artifactPaths.SanitizedTestName,
            server,
            startedAt,
            artifactPaths.ArtifactsRootDirectory,
            artifactPaths.TestRootDirectory,
            artifactPaths.TestRunDirectory);

        try
        {
            var result = await test.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            return string.IsNullOrEmpty(result.TestRunDirectory)
                ? result with { TestRunDirectory = context.TestRunDirectory }
                : result;
        }
        catch (OperationCanceledException)
        {
            var msg = "Orchestration canceled by caller.";
            _logger.Warn(msg);
            return new OrchestrationResult(false, msg, TestRunDirectory: context.TestRunDirectory);
        }
        catch (Exception ex)
        {
            var msg = $"Test execution failed: {ex.Message}";
            _logger.Error(msg, ex);
            return new OrchestrationResult(false, msg, TestRunDirectory: context.TestRunDirectory);
        }
    }
}
