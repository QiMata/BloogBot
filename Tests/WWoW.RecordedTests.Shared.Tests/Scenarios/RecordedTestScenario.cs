using System.Collections.Generic;
using WWoW.RecordedTests.Shared.Tests.TestInfrastructure;

namespace WWoW.RecordedTests.Shared.Tests.Scenarios;

public abstract class RecordedTestScenario
{
    protected RecordedTestScenario(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public abstract BotScript CreateForegroundScript(ScenarioState state, ScenarioLog log);

    public abstract BotScript CreateBackgroundScript(ScenarioState state, ScenarioLog log);

    public abstract IReadOnlyCollection<string> ExpectedStepIds { get; }
}
