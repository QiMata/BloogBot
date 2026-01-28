using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Abstractions;

public class RecordedTestContext : IRecordedTestContext
{
    public RecordedTestContext(string testName, ServerInfo server)
    {
        TestName = testName;
        Server = server;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public string TestName { get; }
    public ServerInfo Server { get; }
    public DateTimeOffset StartedAt { get; }
}