using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Tests.Handlers;

namespace WoWSharpClient.Tests;

[Collection("Sequential ObjectManager tests")]
public class ObjectManagerTaxiTests
{
    private readonly ObjectManagerFixture _fixture;

    public ObjectManagerTaxiTests(ObjectManagerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DiscoverTaxiNodesAsync_WhenHelloOnlyGetsGossip_FallsBackToQueryAvailableNodes()
    {
        ResetObjectManager();

        const ulong flightMasterGuid = 0xF130000CEE0019C8;
        var objectManager = WoWSharpObjectManager.Instance;
        var flightMasterAgent = new Mock<IFlightMasterNetworkClientComponent>();
        var agentFactory = new Mock<IAgentFactory>();

        bool isTaxiMapOpen = false;
        uint? currentNodeId = null;
        IReadOnlyList<uint> availableNodes = Array.Empty<uint>();

        flightMasterAgent.SetupGet(x => x.IsTaxiMapOpen).Returns(() => isTaxiMapOpen);
        flightMasterAgent.SetupGet(x => x.CurrentNodeId).Returns(() => currentNodeId);
        flightMasterAgent.SetupGet(x => x.AvailableTaxiNodes).Returns(() => availableNodes);
        flightMasterAgent.Setup(x => x.HelloFlightMasterAsync(flightMasterGuid, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        flightMasterAgent.Setup(x => x.QueryAvailableNodesAsync(flightMasterGuid, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                isTaxiMapOpen = true;
                currentNodeId = 23;
                availableNodes = new[] { 23u, 25u };
            })
            .Returns(Task.CompletedTask);
        flightMasterAgent.Setup(x => x.CloseTaxiMapAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                isTaxiMapOpen = false;
                currentNodeId = null;
                availableNodes = Array.Empty<uint>();
            })
            .Returns(Task.CompletedTask);

        agentFactory.SetupGet(x => x.FlightMasterAgent).Returns(flightMasterAgent.Object);
        objectManager.SetAgentFactoryAccessor(() => agentFactory.Object);

        var discoveredNodes = await objectManager.DiscoverTaxiNodesAsync(flightMasterGuid);

        Assert.Equal(new[] { 23u, 25u }, discoveredNodes);
        flightMasterAgent.Verify(x => x.HelloFlightMasterAsync(flightMasterGuid, It.IsAny<CancellationToken>()), Times.Once);
        flightMasterAgent.Verify(x => x.QueryAvailableNodesAsync(flightMasterGuid, It.IsAny<CancellationToken>()), Times.Once);
        flightMasterAgent.Verify(x => x.CloseTaxiMapAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateFlightAsync_WhenHelloOnlyGetsGossip_FallsBackToQueryAvailableNodes()
    {
        ResetObjectManager();

        const ulong flightMasterGuid = 0xF130000CEE0019C8;
        const uint sourceNodeId = 23;
        const uint destinationNodeId = 25;
        var objectManager = WoWSharpObjectManager.Instance;
        var flightMasterAgent = new Mock<IFlightMasterNetworkClientComponent>();
        var agentFactory = new Mock<IAgentFactory>();

        bool isTaxiMapOpen = false;
        uint? currentNode = null;
        IReadOnlyList<uint> availableNodes = Array.Empty<uint>();

        flightMasterAgent.SetupGet(x => x.IsTaxiMapOpen).Returns(() => isTaxiMapOpen);
        flightMasterAgent.SetupGet(x => x.CurrentNodeId).Returns(() => currentNode);
        flightMasterAgent.SetupGet(x => x.AvailableTaxiNodes).Returns(() => availableNodes);
        flightMasterAgent.Setup(x => x.IsNodeAvailable(destinationNodeId))
            .Returns(() => availableNodes.Contains(destinationNodeId));
        flightMasterAgent.Setup(x => x.HelloFlightMasterAsync(flightMasterGuid, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        flightMasterAgent.Setup(x => x.QueryAvailableNodesAsync(flightMasterGuid, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                isTaxiMapOpen = true;
                currentNode = sourceNodeId;
                availableNodes = new[] { sourceNodeId, destinationNodeId };
            })
            .Returns(Task.CompletedTask);
        flightMasterAgent.Setup(x => x.ActivateFlightAsync(flightMasterGuid, sourceNodeId, destinationNodeId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        agentFactory.SetupGet(x => x.FlightMasterAgent).Returns(flightMasterAgent.Object);
        objectManager.SetAgentFactoryAccessor(() => agentFactory.Object);

        var activated = await objectManager.ActivateFlightAsync(flightMasterGuid, destinationNodeId);

        Assert.True(activated);
        flightMasterAgent.Verify(x => x.HelloFlightMasterAsync(flightMasterGuid, It.IsAny<CancellationToken>()), Times.Once);
        flightMasterAgent.Verify(x => x.QueryAvailableNodesAsync(flightMasterGuid, It.IsAny<CancellationToken>()), Times.Once);
        flightMasterAgent.Verify(x => x.ActivateFlightAsync(flightMasterGuid, sourceNodeId, destinationNodeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void ResetObjectManager()
    {
        WoWSharpObjectManager.Instance.Initialize(
            _fixture._woWClient.Object,
            _fixture._pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance,
            useLocalPhysics: true);
        WoWSharpObjectManager.Instance.SetAgentFactoryAccessor(() => throw new InvalidOperationException("AgentFactoryAccessor not configured for test."));
    }
}
