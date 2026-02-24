using BotRunner.Combat;
using static BotRunner.Combat.FlightPathData;

namespace BotRunner.Tests.Combat;

public class FlightPathDataTests
{
    [Fact]
    public void Nodes_ContainsExpectedCount()
    {
        // Vanilla 1.12.1 has ~70+ taxi nodes, we should have a substantial set
        Assert.True(Nodes.Count >= 50, $"Expected at least 50 nodes, got {Nodes.Count}");
    }

    [Fact]
    public void Nodes_AllHaveValidMapId()
    {
        foreach (var (id, node) in Nodes)
        {
            Assert.True(node.MapId == 0 || node.MapId == 1,
                $"Node {id} ({node.Name}) has invalid MapId {node.MapId}");
        }
    }

    [Fact]
    public void Nodes_AllHaveNonEmptyNames()
    {
        foreach (var (id, node) in Nodes)
        {
            Assert.False(string.IsNullOrWhiteSpace(node.Name),
                $"Node {id} has empty name");
        }
    }

    [Fact]
    public void Nodes_NodeIdMatchesDictionaryKey()
    {
        foreach (var (id, node) in Nodes)
        {
            Assert.Equal(id, node.NodeId);
        }
    }

    [Fact]
    public void Nodes_ContainsKeyFactionCities()
    {
        // Alliance cities
        Assert.True(Nodes.ContainsKey(2), "Missing Stormwind");
        Assert.True(Nodes.ContainsKey(6), "Missing Ironforge");

        // Horde cities
        Assert.True(Nodes.ContainsKey(11), "Missing Undercity");
        Assert.True(Nodes.ContainsKey(22), "Missing Thunder Bluff");
        Assert.True(Nodes.ContainsKey(23), "Missing Orgrimmar");
    }

    [Fact]
    public void Nodes_ContainsNeutralNodes()
    {
        var neutralNodes = Nodes.Values.Where(n => n.NodeFaction == Faction.Neutral).ToList();
        Assert.True(neutralNodes.Count >= 5,
            $"Expected at least 5 neutral nodes, got {neutralNodes.Count}");
    }

    [Fact]
    public void Nodes_HasBothMaps()
    {
        var ek = Nodes.Values.Where(n => n.MapId == 0).ToList();
        var kal = Nodes.Values.Where(n => n.MapId == 1).ToList();

        Assert.True(ek.Count >= 15, $"Expected at least 15 EK nodes, got {ek.Count}");
        Assert.True(kal.Count >= 20, $"Expected at least 20 Kalimdor nodes, got {kal.Count}");
    }

    [Fact]
    public void FindNearestNode_ReturnsStormwindForElwynn()
    {
        // Elwynn Forest coordinates near Goldshire
        var result = FindNearestNode(0, -9464f, 62f, 56f, Faction.Alliance);
        Assert.NotNull(result);
        Assert.Equal("Stormwind", result.Name);
    }

    [Fact]
    public void FindNearestNode_ReturnsOrgrimmarForDurotar()
    {
        // Durotar coordinates near Orgrimmar
        var result = FindNearestNode(1, 1630f, -4400f, 20f, Faction.Horde);
        Assert.NotNull(result);
        Assert.Equal("Orgrimmar", result.Name);
    }

    [Fact]
    public void FindNearestNode_RespectsMapId()
    {
        // Search on map 1 (Kalimdor) should not return EK nodes
        var result = FindNearestNode(1, -8840f, 497f, 109f, Faction.Alliance);
        Assert.True(result == null || result.MapId == 1,
            "Should not return Eastern Kingdoms node when searching Kalimdor");
    }

    [Fact]
    public void FindNearestNode_RespectsFaction_AllianceCantUseHorde()
    {
        // Search near Orgrimmar as Alliance
        var result = FindNearestNode(1, 1677f, -4315f, 62f, Faction.Alliance);
        if (result != null)
        {
            Assert.NotEqual(Faction.Horde, result.NodeFaction);
        }
    }

    [Fact]
    public void FindNearestNode_NeutralNodesAvailableToBothFactions()
    {
        // Gadgetzan is neutral, both factions should find it
        var allianceResult = FindNearestNode(1, -7117f, -3828f, 10f, Faction.Alliance);
        var hordeResult = FindNearestNode(1, -7117f, -3828f, 10f, Faction.Horde);

        Assert.NotNull(allianceResult);
        Assert.NotNull(hordeResult);
        Assert.Contains("Gadgetzan", allianceResult.Name);
        Assert.Contains("Gadgetzan", hordeResult.Name);
    }

    [Fact]
    public void FindNearestNode_WithDiscoveredFilter()
    {
        // Only discovered nodes should be returned
        var discovered = new HashSet<uint> { 6 }; // Only Ironforge discovered
        var result = FindNearestNode(0, -8840f, 497f, 109f, Faction.Alliance, discovered);

        Assert.NotNull(result);
        Assert.Equal(6u, result.NodeId); // Must be Ironforge since it's the only discovered one
    }

    [Fact]
    public void FindNearestNode_EmptyDiscoveredReturnsNull()
    {
        var discovered = new HashSet<uint>();
        var result = FindNearestNode(0, -8840f, 497f, 109f, Faction.Alliance, discovered);
        Assert.Null(result);
    }

    [Fact]
    public void FindNearestNodeToDestination_FindsClosestAvailable()
    {
        // Available nodes: Stormwind (2), Ironforge (6)
        var available = new List<uint> { 2, 6 };

        // Target near Stormwind
        var result = FindNearestNodeToDestination(0, -8840f, 497f, 109f, Faction.Alliance, available);
        Assert.NotNull(result);
        Assert.Equal("Stormwind", result.Name);

        // Target near Ironforge
        result = FindNearestNodeToDestination(0, -4821f, -1155f, 502f, Faction.Alliance, available);
        Assert.NotNull(result);
        Assert.Equal("Ironforge", result.Name);
    }

    [Fact]
    public void FindNearestNodeToDestination_EmptyAvailableReturnsNull()
    {
        var result = FindNearestNodeToDestination(0, 0f, 0f, 0f, Faction.Alliance, new List<uint>());
        Assert.Null(result);
    }

    [Fact]
    public void GetNodesForFaction_ReturnsCorrectFactions()
    {
        var allianceNodes = GetNodesForFaction(0, Faction.Alliance).ToList();
        foreach (var node in allianceNodes)
        {
            Assert.True(node.NodeFaction == Faction.Alliance || node.NodeFaction == Faction.Neutral,
                $"Node {node.Name} has wrong faction {node.NodeFaction} for Alliance query");
        }

        var hordeNodes = GetNodesForFaction(0, Faction.Horde).ToList();
        foreach (var node in hordeNodes)
        {
            Assert.True(node.NodeFaction == Faction.Horde || node.NodeFaction == Faction.Neutral,
                $"Node {node.Name} has wrong faction {node.NodeFaction} for Horde query");
        }
    }

    [Fact]
    public void GetDistanceBetweenNodes_ValidNodes()
    {
        // Distance between Stormwind (2) and Ironforge (6) should be positive
        var dist = GetDistanceBetweenNodes(2, 6);
        Assert.True(dist > 0 && dist < float.MaxValue);
    }

    [Fact]
    public void GetDistanceBetweenNodes_InvalidNodeReturnsMax()
    {
        var dist = GetDistanceBetweenNodes(999, 6);
        Assert.Equal(float.MaxValue, dist);
    }

    [Fact]
    public void GetDistanceBetweenNodes_SameNodeReturnsZero()
    {
        var dist = GetDistanceBetweenNodes(2, 2);
        Assert.Equal(0f, dist);
    }
}
