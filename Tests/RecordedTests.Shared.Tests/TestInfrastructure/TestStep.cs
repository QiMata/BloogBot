using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Tests.TestInfrastructure;

public abstract class TestStep(string stepId, string description)
{
    public string StepId { get; } = stepId ?? throw new ArgumentNullException(nameof(stepId));

    public string Description { get; } = description ?? throw new ArgumentNullException(nameof(description));

    public virtual Task ExecuteAsync(TestStepExecutionContext context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.State.MarkCompleted(StepId);
        return Task.CompletedTask;
    }
}
