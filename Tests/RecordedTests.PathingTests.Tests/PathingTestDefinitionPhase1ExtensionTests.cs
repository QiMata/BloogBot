using System;
using FluentAssertions;
using GameData.Core.Models;
using RecordedTests.PathingTests.Models;

namespace RecordedTests.PathingTests.Tests;

/// <summary>
/// Coverage for the Phase 1 schema additions (Waypoints, Status, StatusReason,
/// NamedWaypoint, TestStatus, Validate). See
/// <c>docs/Plan/Pathfinding/COMPREHENSIVE_TEST_PLAN.md</c>.
/// </summary>
public class PathingTestDefinitionPhase1ExtensionTests
{
    [Fact]
    public void Defaults_Status_IsStable()
    {
        var def = CreateMinimal(endPosition: new Position(10f, 10f, 10f));
        def.Status.Should().Be(TestStatus.Stable);
    }

    [Fact]
    public void Defaults_StatusReason_IsNull()
    {
        var def = CreateMinimal(endPosition: new Position(10f, 10f, 10f));
        def.StatusReason.Should().BeNull();
    }

    [Fact]
    public void Defaults_Waypoints_IsNull()
    {
        var def = CreateMinimal(endPosition: new Position(10f, 10f, 10f));
        def.Waypoints.Should().BeNull();
    }

    [Fact]
    public void Defaults_IsMultiSegment_IsFalseForSingleSegment()
    {
        var def = CreateMinimal(endPosition: new Position(10f, 10f, 10f));
        def.IsMultiSegment.Should().BeFalse();
    }

    [Fact]
    public void IsMultiSegment_IsTrueWhenWaypointsHasTwoOrMore()
    {
        var def = CreateMinimal(
            endPosition: null,
            waypoints: new[]
            {
                new NamedWaypoint("A", new Position(0f, 0f, 0f)),
                new NamedWaypoint("B", new Position(10f, 0f, 0f)),
            });
        def.IsMultiSegment.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsBothEndPositionAndWaypoints()
    {
        var def = CreateMinimal(
            endPosition: new Position(10f, 10f, 10f),
            waypoints: new[]
            {
                new NamedWaypoint("A", new Position(0f, 0f, 0f)),
                new NamedWaypoint("B", new Position(10f, 0f, 0f)),
            });

        Action act = () => def.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*mutually exclusive*");
    }

    [Fact]
    public void Validate_RejectsNeitherEndPositionNorWaypoints()
    {
        var def = CreateMinimal(endPosition: null, waypoints: null);

        Action act = () => def.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must set either*");
    }

    [Fact]
    public void Validate_RejectsSingleWaypointChain()
    {
        var def = CreateMinimal(
            endPosition: null,
            waypoints: new[]
            {
                new NamedWaypoint("OnlyOne", new Position(0f, 0f, 0f)),
            });

        Action act = () => def.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 2 entries*");
    }

    [Fact]
    public void Validate_AcceptsSingleSegment()
    {
        var def = CreateMinimal(endPosition: new Position(10f, 10f, 10f));
        Action act = () => def.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_AcceptsTwoWaypointChain()
    {
        var def = CreateMinimal(
            endPosition: null,
            waypoints: new[]
            {
                new NamedWaypoint("A", new Position(0f, 0f, 0f)),
                new NamedWaypoint("B", new Position(10f, 0f, 0f)),
            });
        Action act = () => def.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_AcceptsLongChain()
    {
        var def = CreateMinimal(
            endPosition: null,
            waypoints: new[]
            {
                new NamedWaypoint("Start", new Position(0f, 0f, 0f)),
                new NamedWaypoint("Boss1", new Position(10f, 0f, 0f)),
                new NamedWaypoint("Boss2", new Position(20f, 0f, 0f)),
                new NamedWaypoint("Boss3", new Position(30f, 0f, 0f)),
                new NamedWaypoint("End",   new Position(40f, 0f, 0f)),
            });
        Action act = () => def.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void NamedWaypoint_PreservesNameAndPosition()
    {
        var pos = new Position(1f, 2f, 3f);
        var wp = new NamedWaypoint("Boss1", pos);
        wp.Name.Should().Be("Boss1");
        wp.Position.Should().Be(pos);
    }

    [Fact]
    public void TestStatus_AllValues_AreDefined()
    {
        Enum.GetValues<TestStatus>().Should().HaveCount(4);
        Enum.IsDefined(TestStatus.Stable).Should().BeTrue();
        Enum.IsDefined(TestStatus.Experimental).Should().BeTrue();
        Enum.IsDefined(TestStatus.BakeBlocked).Should().BeTrue();
        Enum.IsDefined(TestStatus.Skipped).Should().BeTrue();
    }

    [Fact]
    public void Status_RoundTripsThroughConstructor()
    {
        var def = CreateMinimal(
            endPosition: new Position(10f, 10f, 10f),
            status: TestStatus.Experimental,
            statusReason: "Phase 2 will author boss waypoints.");

        def.Status.Should().Be(TestStatus.Experimental);
        def.StatusReason.Should().Be("Phase 2 will author boss waypoints.");
    }

    [Fact]
    public void AllExistingDefinitions_PassValidate()
    {
        // Regression guard: every row in PathingTestDefinitions.All must satisfy Validate().
        foreach (var def in PathingTestDefinitions.All)
        {
            Action act = () => def.Validate();
            act.Should().NotThrow($"row '{def.Name}' has invalid route shape");
        }
    }

    [Fact]
    public void DefaultBuild_StableRowCount_IsUnchangedFromBaseline()
    {
        // Regression guard: Phase 1 must NOT change the Stable row count. New rows
        // ship as Experimental until Phase 2 promotes them.
        // Baseline = 20 inline rows in PathingTestDefinitions.All at HEAD before Phase 1
        // (Basic 3, Transport 4, Cave 3, Terrain 3, Advanced 3, EdgeCase 4).
        const int Phase0Baseline = 20;
        var stableCount = 0;
        foreach (var def in PathingTestDefinitions.All)
        {
            if (def.Status == TestStatus.Stable) stableCount++;
        }
        stableCount.Should().Be(Phase0Baseline, "Phase 1 must add Experimental rows only; Stable baseline is preserved");
    }

    [Fact]
    public void Phase1_SeededExperimentalRows_AreAtLeastForty()
    {
        // Sanity: the seed batch added ~51 Experimental rows. Bound loosely to allow
        // small Phase 2 promotions to trim this without breaking the test.
        var experimentalCount = 0;
        foreach (var def in PathingTestDefinitions.All)
        {
            if (def.Status == TestStatus.Experimental) experimentalCount++;
        }
        experimentalCount.Should().BeGreaterThanOrEqualTo(40, "Phase 1 seed inventory should be present");
    }

    private static PathingTestDefinition CreateMinimal(
        Position? endPosition = null,
        IReadOnlyList<NamedWaypoint>? waypoints = null,
        TestStatus status = TestStatus.Stable,
        string? statusReason = null)
    {
        return new PathingTestDefinition(
            Name: "TestPath",
            Category: "Smoke",
            Description: "A test path",
            MapId: 1u,
            StartPosition: new Position(0f, 0f, 0f),
            EndPosition: endPosition,
            SetupCommands: Array.Empty<string>(),
            TeardownCommands: Array.Empty<string>(),
            ExpectedDuration: TimeSpan.FromMinutes(1),
            Transport: TransportMode.None,
            IntermediateWaypoint: null,
            EndMapId: null,
            Waypoints: waypoints,
            Status: status,
            StatusReason: statusReason);
    }
}
