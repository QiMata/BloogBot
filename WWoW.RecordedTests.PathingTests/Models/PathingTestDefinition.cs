using GameData.Core.Models;

namespace WWoW.RecordedTests.PathingTests.Models;

/// <summary>
/// Defines a single pathing test including setup/teardown commands, start/end positions, and transport requirements.
/// </summary>
public record PathingTestDefinition(
    string Name,
    string Category,
    string Description,
    uint MapId,
    Position StartPosition,
    Position? EndPosition,
    string[] SetupCommands,
    string[] TeardownCommands,
    TimeSpan ExpectedDuration,
    TransportMode Transport = TransportMode.None,
    string? IntermediateWaypoint = null,
    uint? EndMapId = null  // Defaults to MapId if null, used for cross-continent tests
);

/// <summary>
/// Specifies the type of transport required for a pathing test.
/// </summary>
public enum TransportMode
{
    None,
    Boat,
    Zeppelin
}
