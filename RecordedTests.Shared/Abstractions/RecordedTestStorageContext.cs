namespace RecordedTests.Shared.Abstractions;

using System;
using System.Collections.Generic;

public sealed record RecordedTestStorageContext(
    string TestName,
    string? AutomationRunId,
    bool Success,
    string Message,
    string? TestRunDirectory,
    TestArtifact? RecordingArtifact,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyDictionary<string, string?>? Metadata = null);
