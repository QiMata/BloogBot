using RecordedTests.Shared.Abstractions.I;

namespace RecordedTests.Shared.Factories;

/// <summary>
/// Factory interface for creating IBotRunner instances.
/// Implementations should handle the complexity of constructing and configuring bot runners.
/// </summary>
public interface IBotRunnerFactory
{
    /// <summary>
    /// Creates a new IBotRunner instance.
    /// </summary>
    /// <returns>A newly created bot runner instance.</returns>
    IBotRunner Create();
}
