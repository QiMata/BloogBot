namespace RecordedTests.Shared.Abstractions.I;

/// <summary>
/// Provides the capability to execute GM commands on a WoW server.
/// Typically implemented by foreground bot runners that have GM-level access.
/// </summary>
public interface IGmCommandExecutor
{
    /// <summary>
    /// Executes a GM command on the server.
    /// </summary>
    /// <param name="command">The GM command to execute (e.g., ".character level 10")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
}
