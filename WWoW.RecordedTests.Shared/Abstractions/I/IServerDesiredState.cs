namespace WWoW.RecordedTests.Shared.Abstractions.I;

/// <summary>
/// Represents a desired server state that can be applied or reverted.
/// Used to prepare server conditions before a test and reset them afterward.
/// </summary>
public interface IServerDesiredState
{
    /// <summary>
    /// Gets the name of this desired state for logging and identification.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Applies this desired state to the server using the provided bot runner.
    /// </summary>
    /// <param name="runner">The bot runner to use for applying state changes.</param>
    /// <param name="context">The test execution context.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ApplyAsync(IBotRunner runner, IRecordedTestContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Reverts this desired state, returning the server to its original condition.
    /// </summary>
    /// <param name="runner">The bot runner to use for reverting state changes.</param>
    /// <param name="context">The test execution context.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task RevertAsync(IBotRunner runner, IRecordedTestContext context, CancellationToken cancellationToken);
}
