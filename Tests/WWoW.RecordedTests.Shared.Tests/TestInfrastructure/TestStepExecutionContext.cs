using System;
using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Tests.TestInfrastructure;

public sealed class TestStepExecutionContext
{
    public TestStepExecutionContext(IRecordedTestContext recordedTestContext, ScenarioState state, ScenarioLog log, string runnerRole)
    {
        RecordedTestContext = recordedTestContext ?? throw new ArgumentNullException(nameof(recordedTestContext));
        State = state ?? throw new ArgumentNullException(nameof(state));
        Log = log ?? throw new ArgumentNullException(nameof(log));
        RunnerRole = runnerRole ?? throw new ArgumentNullException(nameof(runnerRole));
    }

    public IRecordedTestContext RecordedTestContext { get; }

    public ScenarioState State { get; }

    public ScenarioLog Log { get; }

    public string RunnerRole { get; }

    public void LogInfo(string message)
    {
        Log.Info($"[{RunnerRole}] {message}");
    }
}
