using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Abstractions;

public class RecordedTestContext : IRecordedTestContext
{
    public RecordedTestContext(
        string testName,
        string sanitizedTestName,
        ServerInfo server,
        DateTimeOffset startedAt,
        string artifactsRootDirectory,
        string testRootDirectory,
        string testRunDirectory)
    {
        TestName = testName;
        SanitizedTestName = sanitizedTestName;
        Server = server;
        StartedAt = startedAt;
        ArtifactsRootDirectory = artifactsRootDirectory;
        TestRootDirectory = testRootDirectory;
        TestRunDirectory = testRunDirectory;
    }

    public string TestName { get; }
    public string SanitizedTestName { get; }
    public ServerInfo Server { get; }
    public DateTimeOffset StartedAt { get; }
    public string ArtifactsRootDirectory { get; }
    public string TestRootDirectory { get; }
    public string TestRunDirectory { get; }
}