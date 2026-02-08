namespace RecordedTests.Shared.Abstractions;

public sealed record OrchestrationResult(
    bool Success,
    string Message,
    TestArtifact? RecordingArtifact = null,
    string? TestRunDirectory = null);