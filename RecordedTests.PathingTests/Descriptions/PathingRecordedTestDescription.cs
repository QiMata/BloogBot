using RecordedTests.PathingTests.Context;
using RecordedTests.PathingTests.Models;
using RecordedTests.Shared;
using RecordedTests.Shared.Abstractions.I;

namespace RecordedTests.PathingTests.Descriptions;

/// <summary>
/// Test description for pathing tests, extending the default recorded test orchestration
/// with pathing-specific configuration and GM command execution.
/// </summary>
public class PathingRecordedTestDescription : DefaultRecordedWoWTestDescription
{
    private readonly PathingTestDefinition _testDefinition;

    /// <summary>
    /// Initializes a new instance of the PathingRecordedTestDescription.
    /// </summary>
    /// <param name="context">The pathing test context</param>
    /// <param name="foregroundRunner">The foreground bot runner (GM-capable)</param>
    /// <param name="backgroundRunner">The background bot runner (test execution)</param>
    /// <param name="recorder">Optional screen recorder</param>
    /// <param name="logger">Test logger</param>
    public PathingRecordedTestDescription(
        PathingRecordedTestContext context,
        IBotRunner foregroundRunner,
        IBotRunner backgroundRunner,
        IScreenRecorder? recorder,
        ITestLogger logger)
        : base(
            context,
            foregroundRunner,
            backgroundRunner,
            CreateDesiredState(context.TestDefinition, foregroundRunner, logger),
            recorder,
            logger)
    {
        _testDefinition = context.TestDefinition;
    }

    /// <summary>
    /// Creates the server desired state with GM command execution wired up.
    /// </summary>
    private static IServerDesiredState CreateDesiredState(
        PathingTestDefinition testDefinition,
        IBotRunner foregroundRunner,
        ITestLogger logger)
    {
        var desiredState = new Shared.DesiredState.GmCommandServerDesiredState(
        $"PathingTest_{testDefinition.Name}",
        testDefinition.SetupCommands,
        testDefinition.TeardownCommands,
        logger);

        // Wire up the GM command executor if the foreground runner implements it
        if (foregroundRunner is IGmCommandExecutor executor)
        {
            desiredState.SetExecutor(executor);
        }

        return desiredState;
    }
}
