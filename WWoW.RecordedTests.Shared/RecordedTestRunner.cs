namespace WWoW.RecordedTests.Shared;

using System;
using System.Threading;
using System.Threading.Tasks;
using WWoW.RecordedTests.Shared.Abstractions;

public sealed class RecordedTestRunner
{
    private readonly RecordedTestE2EConfiguration _configuration;

    public RecordedTestRunner(RecordedTestE2EConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        if (string.IsNullOrWhiteSpace(configuration.TestName))
        {
            throw new ArgumentException("Test name must be provided.", nameof(configuration));
        }

        if (configuration.TestDescriptionOverride is null)
        {
            ArgumentNullException.ThrowIfNull(configuration.ForegroundFactory, nameof(configuration.ForegroundFactory));
            ArgumentNullException.ThrowIfNull(configuration.BackgroundFactory, nameof(configuration.BackgroundFactory));
        }

        if (configuration.ServerAvailabilityOverride is null)
        {
            ArgumentNullException.ThrowIfNull(configuration.MangosAppsClient, nameof(configuration.MangosAppsClient));

            if (configuration.ServerDefinitions is null || configuration.ServerDefinitions.Count == 0)
            {
                throw new ArgumentException("At least one server definition must be supplied when using the TrueNAS availability checker.", nameof(configuration));
            }
        }
    }

    public async Task<OrchestrationResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var logger = _configuration.Logger ?? new NullTestLogger();
        var options = _configuration.OrchestrationOptions ?? new OrchestrationOptions();

        var serverChecker = _configuration.ServerAvailabilityOverride
            ?? new TrueNasAppServerAvailabilityChecker(
                _configuration.MangosAppsClient!,
                _configuration.ServerDefinitions!,
                _configuration.ServerPollInterval,
                logger);

        var orchestrator = new RecordedTestOrchestrator(serverChecker, options, logger);

        var test = _configuration.TestDescriptionOverride
            ?? new DefaultRecordedWoWTestDescription(
                _configuration.TestName,
                _configuration.ForegroundFactory!,
                _configuration.BackgroundFactory!,
                _configuration.RecorderFactory,
                _configuration.InitialDesiredState,
                _configuration.BaseDesiredState,
                options,
                logger);

        var startedAt = DateTimeOffset.UtcNow;
        var result = await orchestrator.RunAsync(test, cancellationToken).ConfigureAwait(false);
        var completedAt = DateTimeOffset.UtcNow;

        if (_configuration.ArtifactStorage is not null && !string.IsNullOrWhiteSpace(result.TestRunDirectory))
        {
            var context = new RecordedTestStorageContext(
                _configuration.TestName,
                _configuration.AutomationRunId,
                result.Success,
                result.Message,
                result.TestRunDirectory,
                result.RecordingArtifact,
                startedAt,
                completedAt,
                _configuration.Metadata);

            await _configuration.ArtifactStorage.StoreAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
