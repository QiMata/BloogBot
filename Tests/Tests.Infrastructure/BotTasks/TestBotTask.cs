namespace Tests.Infrastructure.BotTasks;

/// <summary>
/// Base class for test bot tasks. These represent discrete, verifiable actions
/// that can be executed in a test context. Each task is self-contained with
/// its own completion and failure tracking.
/// </summary>
public abstract class TestBotTask
{
    public string Name { get; }
    public bool IsComplete { get; protected set; }
    public bool HasFailed { get; protected set; }
    public string? FailureReason { get; protected set; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    protected TestBotTask(string name) => Name = name;

    /// <summary>
    /// Execute one tick of the task. Called repeatedly until IsComplete is true.
    /// For single-shot tasks, call Complete() or Fail() and return.
    /// </summary>
    public abstract void Update();

    protected void Complete() => IsComplete = true;

    protected void Fail(string reason)
    {
        HasFailed = true;
        FailureReason = reason;
        IsComplete = true;
    }

    /// <summary>
    /// Assert that the task completed successfully. Use from xUnit test methods.
    /// </summary>
    public void AssertSuccess()
    {
        if (!IsComplete)
            throw new Xunit.Sdk.XunitException($"Task '{Name}' did not complete.");
        if (HasFailed)
            throw new Xunit.Sdk.XunitException($"Task '{Name}' failed: {FailureReason}");
    }
}
