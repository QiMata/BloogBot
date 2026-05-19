using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Frames;
using WoWSharpClient.Networking.ClientComponents.I;
using Xunit;

namespace WoWSharpClient.Tests.Frames;

/// <summary>
/// Unit coverage for the S1.18 BG TaxiFrame implementation. Stubs
/// <see cref="IFlightMasterNetworkClientComponent"/> and asserts the
/// <c>ITaxiFrame</c> contract routes through the flight-master packet path.
/// The wire-up half; LiveValidation TaxiParityTests would close the
/// end-to-end half.
/// </summary>
public class NetworkTaxiFrameTests
{
    private static NetworkTaxiFrame WithAgent(IFlightMasterNetworkClientComponent? agent)
        => new(() => agent);

    private static Mock<IFlightMasterNetworkClientComponent> MockOpenTaxiMap(
        IReadOnlyList<uint>? availableNodes = null,
        uint? currentNodeId = 26u)
    {
        var mock = new Mock<IFlightMasterNetworkClientComponent>();
        mock.SetupGet(f => f.IsTaxiMapOpen).Returns(true);
        mock.SetupGet(f => f.AvailableTaxiNodes)
            .Returns(availableNodes ?? new uint[] { 26u, 11u, 17u });
        mock.SetupGet(f => f.CurrentNodeId).Returns(currentNodeId);
        mock.Setup(f => f.IsNodeAvailable(It.IsAny<uint>()))
            .Returns<uint>(id => (availableNodes ?? new uint[] { 26u, 11u, 17u }).Contains(id));
        mock.Setup(f => f.GetFlightCost(It.IsAny<uint>())).Returns((uint?)null);
        return mock;
    }

    [Fact]
    public void IsOpen_AgentNull_ReturnsFalse()
        => Assert.False(WithAgent(null).IsOpen);

    [Fact]
    public void IsOpen_AgentClosed_ReturnsFalse()
    {
        var mock = new Mock<IFlightMasterNetworkClientComponent>();
        mock.SetupGet(f => f.IsTaxiMapOpen).Returns(false);
        Assert.False(WithAgent(mock.Object).IsOpen);
    }

    [Fact]
    public void IsOpen_AgentOpen_ReturnsTrue()
        => Assert.True(WithAgent(MockOpenTaxiMap().Object).IsOpen);

    [Fact]
    public void Close_TaxiMapOpen_RoutesToCloseTaxiMapAsync()
    {
        var mock = MockOpenTaxiMap();
        mock.Setup(f => f.CloseTaxiMapAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        WithAgent(mock.Object).Close();

        mock.Verify(f => f.CloseTaxiMapAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Close_TaxiMapClosed_NoOp()
    {
        var mock = new Mock<IFlightMasterNetworkClientComponent>();
        mock.SetupGet(f => f.IsTaxiMapOpen).Returns(false);

        WithAgent(mock.Object).Close();

        mock.Verify(f => f.CloseTaxiMapAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Close_AgentNull_DoesNotThrow()
        => WithAgent(null).Close();

    [Fact]
    public void Nodes_AgentNull_ReturnsSentinelOnly()
    {
        var nodes = WithAgent(null).Nodes;
        // FG seeds index 0 with the "NONE" sentinel to keep 1-based indexing
        // consistent. Mirror that on BG.
        Assert.Single(nodes);
        Assert.Equal("NONE", nodes[0].Status);
        Assert.Equal(0, nodes[0].NodeNumber);
    }

    [Fact]
    public void Nodes_AgentClosed_ReturnsSentinelOnly()
    {
        var mock = new Mock<IFlightMasterNetworkClientComponent>();
        mock.SetupGet(f => f.IsTaxiMapOpen).Returns(false);
        mock.SetupGet(f => f.AvailableTaxiNodes).Returns(System.Array.Empty<uint>());

        var nodes = WithAgent(mock.Object).Nodes;

        Assert.Single(nodes);
        Assert.Equal("NONE", nodes[0].Status);
    }

    [Fact]
    public void Nodes_TaxiMapOpen_ReturnsSentinelPlusAvailableNodes()
    {
        var frame = WithAgent(MockOpenTaxiMap(currentNodeId: 26u).Object);
        var nodes = frame.Nodes;

        // Sentinel + 3 nodes
        Assert.Equal(4, nodes.Count);
        Assert.Equal("NONE", nodes[0].Status);
        // NodeNumber projects the DBC node id directly.
        Assert.Equal(26, nodes[1].NodeNumber);
        Assert.Equal(11, nodes[2].NodeNumber);
        Assert.Equal(17, nodes[3].NodeNumber);
    }

    [Fact]
    public void Nodes_CurrentNodeId_TaggedCurrentOthersReachable()
    {
        var frame = WithAgent(MockOpenTaxiMap(currentNodeId: 11u).Object);
        var nodes = frame.Nodes.Skip(1).ToList();

        Assert.Contains(nodes, n => n.NodeNumber == 11 && n.Status == "CURRENT");
        Assert.Contains(nodes, n => n.NodeNumber == 26 && n.Status == "REACHABLE");
        Assert.Contains(nodes, n => n.NodeNumber == 17 && n.Status == "REACHABLE");
    }

    [Fact]
    public void Nodes_FlightCostUnknown_CostProjectsZero()
    {
        // Default mock returns GetFlightCost == null -- cost should project
        // to 0 so the dispatcher's "Has Enough Gold" gate proceeds; server-
        // side CMSG_ACTIVATETAXI is the authoritative cost authority.
        var frame = WithAgent(MockOpenTaxiMap().Object);
        Assert.All(frame.Nodes.Skip(1), node => Assert.Equal(0, node.Cost));
    }

    [Fact]
    public void Nodes_FlightCostKnown_CostProjectsCopper()
    {
        var mock = MockOpenTaxiMap();
        mock.Setup(f => f.GetFlightCost(26u)).Returns(1234u);
        mock.Setup(f => f.GetFlightCost(11u)).Returns(5678u);

        var nodes = WithAgent(mock.Object).Nodes;

        Assert.Equal(1234, nodes.First(n => n.NodeNumber == 26).Cost);
        Assert.Equal(5678, nodes.First(n => n.NodeNumber == 11).Cost);
        Assert.Equal(0, nodes.First(n => n.NodeNumber == 17).Cost);
    }

    [Fact]
    public void NodesAvailable_ReflectsAvailableNodeCount()
    {
        Assert.Equal(0, WithAgent(null).NodesAvailable);
        Assert.Equal(3, WithAgent(MockOpenTaxiMap().Object).NodesAvailable);
    }

    [Fact]
    public void CurrentNodeName_NoCurrentNode_ReturnsEmpty()
    {
        var mock = MockOpenTaxiMap(currentNodeId: 0u);
        Assert.Equal(string.Empty, WithAgent(mock.Object).CurrentNodeName);
        Assert.Equal(string.Empty, WithAgent(null).CurrentNodeName);
    }

    [Fact]
    public void CurrentNodeName_HasCurrentNode_ReturnsStringifiedId()
    {
        var frame = WithAgent(MockOpenTaxiMap(currentNodeId: 26u).Object);
        // Pending agent-extension TODO -- DBC name lookup not available, so
        // name projects to the stringified DBC id.
        Assert.Equal("26", frame.CurrentNodeName);
    }

    [Fact]
    public void HasNodeUnlocked_NegativeOrZero_ReturnsFalse()
    {
        var frame = WithAgent(MockOpenTaxiMap().Object);
        Assert.False(frame.HasNodeUnlocked(0));
        Assert.False(frame.HasNodeUnlocked(-1));
    }

    [Fact]
    public void HasNodeUnlocked_AvailableNode_ReturnsTrue()
    {
        var frame = WithAgent(MockOpenTaxiMap().Object);
        Assert.True(frame.HasNodeUnlocked(26));
        Assert.True(frame.HasNodeUnlocked(11));
    }

    [Fact]
    public void HasNodeUnlocked_UnknownNode_ReturnsFalse()
    {
        var frame = WithAgent(MockOpenTaxiMap().Object);
        Assert.False(frame.HasNodeUnlocked(999));
    }

    [Fact]
    public void HasNodeUnlocked_AgentNull_ReturnsFalse()
        => Assert.False(WithAgent(null).HasNodeUnlocked(26));

    [Fact]
    public void SelectNode_NoFlightMasterSeed_NoOp()
    {
        var mock = MockOpenTaxiMap();
        // No SetActiveFlightMaster call -- packet should not be sent because
        // the activation requires a valid flight-master GUID.
        WithAgent(mock.Object).SelectNode(26);

        mock.Verify(
            f => f.ActivateFlightAsync(It.IsAny<ulong>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SelectNode_NoCurrentSourceNode_NoOp()
    {
        var mock = MockOpenTaxiMap(currentNodeId: 0u);
        mock.Setup(f => f.ActivateFlightAsync(It.IsAny<ulong>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var frame = WithAgent(mock.Object);
        frame.SetActiveFlightMaster(0xABCDUL);
        frame.SelectNode(26);

        mock.Verify(
            f => f.ActivateFlightAsync(It.IsAny<ulong>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SelectNode_RoutesToActivateFlightAsync()
    {
        var mock = MockOpenTaxiMap(currentNodeId: 11u);
        mock.Setup(f => f.ActivateFlightAsync(0xABCDUL, 11u, 26u, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var frame = WithAgent(mock.Object);
        frame.SetActiveFlightMaster(0xABCDUL);
        frame.SelectNode(26);

        mock.Verify(f => f.ActivateFlightAsync(0xABCDUL, 11u, 26u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SelectNodeByNumber_DelegatesToSelectNode()
    {
        var mock = MockOpenTaxiMap(currentNodeId: 11u);
        mock.Setup(f => f.ActivateFlightAsync(0xABCDUL, 11u, 17u, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var frame = WithAgent(mock.Object);
        frame.SetActiveFlightMaster(0xABCDUL);
        frame.SelectNodeByNumber(17);

        mock.Verify(f => f.ActivateFlightAsync(0xABCDUL, 11u, 17u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SelectNodeByName_StringifiedId_RoutesToActivateFlightAsync()
    {
        // Name lookup pending agent extension -- the BG frame currently
        // expects stringified DBC ids for SelectNodeByName.
        var mock = MockOpenTaxiMap(currentNodeId: 11u);
        mock.Setup(f => f.ActivateFlightAsync(0xABCDUL, 11u, 26u, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var frame = WithAgent(mock.Object);
        frame.SetActiveFlightMaster(0xABCDUL);
        frame.SelectNodeByName("26");

        mock.Verify(f => f.ActivateFlightAsync(0xABCDUL, 11u, 26u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SelectNodeByName_NonNumericName_NoOp()
    {
        var mock = MockOpenTaxiMap(currentNodeId: 11u);
        var frame = WithAgent(mock.Object);
        frame.SetActiveFlightMaster(0xABCDUL);

        frame.SelectNodeByName("Crossroads");
        frame.SelectNodeByName(string.Empty);

        mock.Verify(
            f => f.ActivateFlightAsync(It.IsAny<ulong>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SelectNode_AgentNull_DoesNotThrow()
        => WithAgent(null).SelectNode(26);
}
