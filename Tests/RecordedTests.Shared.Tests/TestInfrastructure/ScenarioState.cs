using System;
using System.Collections.Generic;

namespace RecordedTests.Shared.Tests.TestInfrastructure;

public sealed class ScenarioState
{
    private readonly HashSet<string> _completedSteps = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> CompletedSteps => _completedSteps;

    public void MarkCompleted(string stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            throw new ArgumentException("Step identifier is required.", nameof(stepId));
        }

        _completedSteps.Add(stepId);
    }
}
