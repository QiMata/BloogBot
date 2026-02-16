using RecordedTests.Shared.Abstractions.I;
using System;

namespace RecordedTests.Shared.Abstractions;

public class RecordedTestContext(
    string testName,
    string sanitizedTestName,
    ServerInfo server,
    DateTimeOffset startedAt,
    string artifactsRootDirectory,
    string testRootDirectory,
    string testRunDirectory) : IRecordedTestContext
{
    public string TestName { get; } = testName;
    public string SanitizedTestName { get; } = sanitizedTestName;
    public ServerInfo Server { get; } = server;
    public DateTimeOffset StartedAt { get; } = startedAt;
    public string ArtifactsRootDirectory { get; } = artifactsRootDirectory;
    public string TestRootDirectory { get; } = testRootDirectory;
    public string TestRunDirectory { get; } = testRunDirectory;
}