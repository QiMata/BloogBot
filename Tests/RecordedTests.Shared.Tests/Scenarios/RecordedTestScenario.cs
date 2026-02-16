using System.Collections.Generic;
using RecordedTests.Shared.Tests.TestInfrastructure;

namespace RecordedTests.Shared.Tests.Scenarios;

public abstract class RecordedTestScenario(string name)
{
    public string Name { get; } = name;

    public abstract BotScript CreateForegroundScript(ScenarioState state, ScenarioLog log);

    public abstract BotScript CreateBackgroundScript(ScenarioState state, ScenarioLog log);

    public abstract IReadOnlyCollection<string> ExpectedStepIds { get; }
}
