using GameData.Core.Models;
using System;
using System.Collections.Generic;

namespace RecordedTests.PathingTests.Models;

/// <summary>
/// Defines a single pathing test including setup/teardown commands, start/end positions, and transport requirements.
/// </summary>
/// <remarks>
/// Two route shapes are supported:
/// <list type="bullet">
/// <item><b>Single-segment</b> (legacy): set <see cref="StartPosition"/> + <see cref="EndPosition"/>. The runner
/// asks PathfindingService for one path Start→End and follows it.</item>
/// <item><b>Multi-segment chain</b>: set <see cref="Waypoints"/> with at least 2 entries. The runner iterates
/// segments <c>Waypoints[i] → Waypoints[i+1]</c>, asserting per-segment arrival. Used for boss-to-boss
/// dungeon chains and capital-interior loops.</item>
/// </list>
/// Exactly one of <see cref="EndPosition"/> or <see cref="Waypoints"/> must be set, enforced by
/// <see cref="Validate"/>.
/// </remarks>
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
    uint? EndMapId = null,
    IReadOnlyList<NamedWaypoint>? Waypoints = null,
    TestStatus Status = TestStatus.Stable,
    string? StatusReason = null)
{
    /// <summary>
    /// Validates the route shape invariants. Throws <see cref="InvalidOperationException"/> if violated.
    /// Idempotent; safe to call repeatedly.
    /// </summary>
    public void Validate()
    {
        var hasEnd = EndPosition is not null;
        var hasWaypoints = Waypoints is { Count: > 0 };

        if (hasEnd && hasWaypoints)
            throw new InvalidOperationException(
                $"Test '{Name}': EndPosition and Waypoints are mutually exclusive. Set exactly one.");

        if (!hasEnd && !hasWaypoints)
            throw new InvalidOperationException(
                $"Test '{Name}': must set either EndPosition (single-segment) or Waypoints (multi-segment chain).");

        if (hasWaypoints && Waypoints!.Count < 2)
            throw new InvalidOperationException(
                $"Test '{Name}': Waypoints requires at least 2 entries (start and end). Got {Waypoints.Count}.");
    }

    /// <summary>
    /// True when this test uses a multi-segment Waypoints chain rather than a single Start→End path.
    /// </summary>
    public bool IsMultiSegment => Waypoints is { Count: >= 2 };
}

/// <summary>
/// Specifies the type of transport required for a pathing test.
/// </summary>
public enum TransportMode
{
    None,
    Boat,
    Zeppelin
}

/// <summary>
/// A named position along a multi-segment route chain. Used for boss-to-boss dungeon
/// chains and capital-interior loops; the <see cref="Name"/> appears in per-segment
/// log lines, screenshot filenames, and artifact dumps so partial failures are diagnosable.
/// </summary>
public record NamedWaypoint(string Name, Position Position);

/// <summary>
/// Gating status for a <see cref="PathingTestDefinition"/>. Default-filtered by
/// <c>Program.FilterTests</c> so the green build only runs <see cref="Stable"/> rows.
/// </summary>
public enum TestStatus
{
    /// <summary>Green in prod-data sweep; included by default.</summary>
    Stable,

    /// <summary>Authored and runnable, but not yet verified green; excluded unless --include-experimental.</summary>
    Experimental,

    /// <summary>Depends on a map that has not been baked yet; excluded unless --include-bake-blocked.</summary>
    BakeBlocked,

    /// <summary>Explicitly retired or quarantined; only runnable via --status Skipped.</summary>
    Skipped,
}
