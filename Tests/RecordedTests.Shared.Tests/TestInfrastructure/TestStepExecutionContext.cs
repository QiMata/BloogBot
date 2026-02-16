using System;
using RecordedTests.Shared.Abstractions.I;

namespace RecordedTests.Shared.Tests.TestInfrastructure;

public sealed class TestStepExecutionContext(IRecordedTestContext recordedTestContext, ScenarioState state, ScenarioLog log, string runnerRole)
{
    public IRecordedTestContext RecordedTestContext { get; } = recordedTestContext ?? throw new ArgumentNullException(nameof(recordedTestContext));

    public ScenarioState State { get; } = state ?? throw new ArgumentNullException(nameof(state));

    public ScenarioLog Log { get; } = log ?? throw new ArgumentNullException(nameof(log));

    public string RunnerRole { get; } = runnerRole ?? throw new ArgumentNullException(nameof(runnerRole));

    public void LogInfo(string message)
    {
        Log.Info($"[{RunnerRole}] {message}");
    }
}
