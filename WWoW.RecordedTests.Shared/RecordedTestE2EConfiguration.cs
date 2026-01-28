namespace WWoW.RecordedTests.Shared;

using System;
using System.Collections.Generic;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

public sealed record RecordedTestE2EConfiguration
{
    public required string TestName { get; init; }

    public IBotRunnerFactory? ForegroundFactory { get; init; }

    public IBotRunnerFactory? BackgroundFactory { get; init; }

    public IScreenRecorderFactory? RecorderFactory { get; init; }

    public IServerDesiredState? InitialDesiredState { get; init; }

    public IServerDesiredState? BaseDesiredState { get; init; }

    public IMangosAppsClient? MangosAppsClient { get; init; }

    public IReadOnlyList<string>? ServerDefinitions { get; init; }

    public OrchestrationOptions? OrchestrationOptions { get; init; }

    public ITestLogger? Logger { get; init; }

    public TimeSpan? ServerPollInterval { get; init; }

    public IRecordedTestStorage? ArtifactStorage { get; init; }

    public ITestDescription? TestDescriptionOverride { get; init; }

    public IServerAvailabilityChecker? ServerAvailabilityOverride { get; init; }

    public string? AutomationRunId { get; init; }

    public IReadOnlyDictionary<string, string?>? Metadata { get; init; }
}
