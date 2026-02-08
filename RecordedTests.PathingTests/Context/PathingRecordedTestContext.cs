using RecordedTests.PathingTests.Models;
using RecordedTests.Shared;
using RecordedTests.Shared.Abstractions;

namespace RecordedTests.PathingTests.Context;

/// <summary>
/// Test execution context for pathing tests, extending the base RecordedTestContext with pathing-specific data.
/// </summary>
public class PathingRecordedTestContext : RecordedTestContext
{
    /// <summary>
    /// Gets the pathing test definition containing setup/teardown commands, positions, and test metadata.
    /// </summary>
    public PathingTestDefinition TestDefinition { get; }

    /// <summary>
    /// Initializes a new instance of the PathingRecordedTestContext.
    /// </summary>
    /// <param name="testDefinition">The pathing test definition</param>
    /// <param name="serverInfo">The server connection information</param>
    /// <param name="artifactsRootDirectory">Root directory for test artifacts</param>
    public PathingRecordedTestContext(
        PathingTestDefinition testDefinition,
        ServerInfo serverInfo,
        string? artifactsRootDirectory = null)
        : base(
            testDefinition.Name,
            ArtifactPathHelper.SanitizeName(testDefinition.Name),
            serverInfo,
            DateTimeOffset.UtcNow,
            artifactsRootDirectory ?? Path.Combine(Path.GetTempPath(), "PathingTests"),
            Path.Combine(artifactsRootDirectory ?? Path.Combine(Path.GetTempPath(), "PathingTests"), ArtifactPathHelper.SanitizeName(testDefinition.Name)),
            Path.Combine(artifactsRootDirectory ?? Path.Combine(Path.GetTempPath(), "PathingTests"), ArtifactPathHelper.SanitizeName(testDefinition.Name), DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss")))
    {
        TestDefinition = testDefinition;
    }
}
