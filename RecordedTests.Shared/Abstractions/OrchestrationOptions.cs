using System;

namespace RecordedTests.Shared.Abstractions;

public sealed class OrchestrationOptions
{
    public TimeSpan ServerAvailabilityTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public string ArtifactsRootDirectory { get; init; } = ".\\TestLogs";
    // If true, we'll attempt to stop the recorder both after test run and after cleanup (idempotent Stop)
    public bool DoubleStopRecorderForSafety { get; init; } = true;
}