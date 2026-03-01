using System;
using FluentAssertions;
using GameData.Core.Models;
using WWoW.RecordedTests.PathingTests.Models;

namespace WWoW.RecordedTests.PathingTests.Tests;

public class PathingTestDefinitionTests
{
    [Fact]
    public void Constructor_PreservesName()
    {
        var def = CreateMinimal();
        def.Name.Should().Be("TestPath");
    }

    [Fact]
    public void Constructor_PreservesCategory()
    {
        var def = CreateMinimal();
        def.Category.Should().Be("Smoke");
    }

    [Fact]
    public void Constructor_PreservesDescription()
    {
        var def = CreateMinimal();
        def.Description.Should().Be("A test path");
    }

    [Fact]
    public void Constructor_PreservesMapId()
    {
        var def = CreateMinimal();
        def.MapId.Should().Be(1u);
    }

    [Fact]
    public void Constructor_PreservesStartPosition()
    {
        var start = new Position(100f, 200f, 300f);
        var def = CreateMinimal(startPosition: start);
        def.StartPosition.Should().Be(start);
    }

    [Fact]
    public void Constructor_EndPosition_CanBeNull()
    {
        var def = CreateMinimal(endPosition: null);
        def.EndPosition.Should().BeNull();
    }

    [Fact]
    public void Constructor_EndPosition_PreservesValue()
    {
        var end = new Position(400f, 500f, 600f);
        var def = CreateMinimal(endPosition: end);
        def.EndPosition.Should().Be(end);
    }

    [Fact]
    public void Defaults_Transport_IsNone()
    {
        var def = CreateMinimal();
        def.Transport.Should().Be(TransportMode.None);
    }

    [Fact]
    public void Constructor_Transport_CanBeBoat()
    {
        var def = CreateMinimal(transport: TransportMode.Boat);
        def.Transport.Should().Be(TransportMode.Boat);
    }

    [Fact]
    public void Constructor_Transport_CanBeZeppelin()
    {
        var def = CreateMinimal(transport: TransportMode.Zeppelin);
        def.Transport.Should().Be(TransportMode.Zeppelin);
    }

    [Fact]
    public void Defaults_IntermediateWaypoint_IsNull()
    {
        var def = CreateMinimal();
        def.IntermediateWaypoint.Should().BeNull();
    }

    [Fact]
    public void Defaults_EndMapId_IsNull()
    {
        var def = CreateMinimal();
        def.EndMapId.Should().BeNull();
    }

    [Fact]
    public void Constructor_EndMapId_PreservesValue()
    {
        var def = CreateMinimal(endMapId: 0u);
        def.EndMapId.Should().Be(0u);
    }

    [Fact]
    public void WithExpression_ChangesName()
    {
        var a = CreateMinimal();
        var b = a with { Name = "OtherPath" };
        b.Name.Should().Be("OtherPath");
        b.Category.Should().Be(a.Category);
    }

    [Fact]
    public void TransportMode_AllValues_AreDefined()
    {
        Enum.GetValues<TransportMode>().Should().HaveCount(3);
        Enum.IsDefined(TransportMode.None).Should().BeTrue();
        Enum.IsDefined(TransportMode.Boat).Should().BeTrue();
        Enum.IsDefined(TransportMode.Zeppelin).Should().BeTrue();
    }

    private static PathingTestDefinition CreateMinimal(
        Position? startPosition = null,
        Position? endPosition = null,
        TransportMode transport = TransportMode.None,
        uint? endMapId = null)
    {
        return new PathingTestDefinition(
            "TestPath",
            "Smoke",
            "A test path",
            1u,
            startPosition ?? new Position(0f, 0f, 0f),
            endPosition,
            Array.Empty<string>(),
            Array.Empty<string>(),
            TimeSpan.FromMinutes(1),
            transport,
            null,
            endMapId);
    }
}
