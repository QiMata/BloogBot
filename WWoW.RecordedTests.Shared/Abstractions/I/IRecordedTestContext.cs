namespace WWoW.RecordedTests.Shared.Abstractions.I;

public interface IRecordedTestContext
{
    string TestName { get; }
    ServerInfo Server { get; }
    DateTimeOffset StartedAt { get; }
}