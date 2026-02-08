using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Tests.TestInfrastructure;

public abstract class TestStep
{
    protected TestStep(string stepId, string description)
    {
        StepId = stepId ?? throw new ArgumentNullException(nameof(stepId));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public string StepId { get; }

    public string Description { get; }

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
