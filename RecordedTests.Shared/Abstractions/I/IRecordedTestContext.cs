namespace RecordedTests.Shared.Abstractions.I;

public interface IRecordedTestContext
{
    string TestName { get; }
    string SanitizedTestName { get; }
    ServerInfo Server { get; }
    DateTimeOffset StartedAt { get; }
    string ArtifactsRootDirectory { get; }
    string TestRootDirectory { get; }
    string TestRunDirectory { get; }
}