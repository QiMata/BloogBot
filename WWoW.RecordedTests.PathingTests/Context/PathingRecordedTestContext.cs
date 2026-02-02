using WWoW.RecordedTests.PathingTests.Models;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;

namespace WWoW.RecordedTests.PathingTests.Context;

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
    public PathingRecordedTestContext(
        PathingTestDefinition testDefinition,
        ServerInfo serverInfo)
        : base(testDefinition.Name, serverInfo)
    {
        TestDefinition = testDefinition;
    }
}
