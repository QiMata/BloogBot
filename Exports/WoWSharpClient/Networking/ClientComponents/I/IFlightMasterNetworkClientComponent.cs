using System.Reactive;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling flight master operations in World of Warcraft.
    /// Manages taxi node interactions, flight path queries, and flight activation.
    /// Uses reactive observables (no events/subjects exposed).
    /// </summary>
    public interface IFlightMasterNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether the taxi map is currently open.
        /// </summary>
        bool IsTaxiMapOpen { get; }

        /// <summary>
        /// Gets the currently available taxi nodes, if any.
        /// </summary>
        IReadOnlyList<uint> AvailableTaxiNodes { get; }

        /// <summary>
        /// Gets the current (nearest) taxi node ID from the last SMSG_SHOWTAXINODES, or null.
        /// </summary>
        uint? CurrentNodeId { get; }

        #region Reactive Observables

        /// <summary>
        /// Observable stream emitted when the taxi map is opened and nodes are available (SMSG_SHOWTAXINODES).
        /// Tuple contains the flight master GUID and the list of available node IDs.
        /// </summary>
        IObservable<(ulong FlightMasterGuid, IReadOnlyList<uint> Nodes)> TaxiMapOpened { get; }

        /// <summary>
        /// Observable stream emitted when taxi context closes (best-effort; derived from disconnects or UI closure).
        /// </summary>
        IObservable<Unit> TaxiMapClosed { get; }

        /// <summary>
        /// Observable stream emitted when a flight is activated (SMSG_ACTIVATETAXIREPLY).
        /// Tuple contains source node, destination node, and cost in copper.
        /// </summary>
        IObservable<(uint SourceNodeId, uint DestinationNodeId, uint Cost)> FlightActivated { get; }

        /// <summary>
        /// Observable stream of taxi node status updates (SMSG_TAXINODE_STATUS).
        /// Tuple contains node id and status byte.
        /// </summary>
        IObservable<(uint NodeId, byte Status)> TaxiNodeStatus { get; }

        /// <summary>
        /// Observable stream of flight master related errors (if any).
        /// </summary>
        IObservable<string> FlightMasterErrors { get; }

        #endregion

        /// <summary>
        /// Initiates interaction with a flight master.
        /// Sends CMSG_GOSSIP_HELLO to the flight master's GUID.
        /// </summary>
        /// <param name="flightMasterGuid">The GUID of the flight master NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task HelloFlightMasterAsync(ulong flightMasterGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries the status of a specific taxi node.
        /// Sends CMSG_TAXINODE_STATUS_QUERY with the flight master's GUID.
        /// </summary>
        /// <param name="flightMasterGuid">The GUID of the flight master NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueryTaxiNodeStatusAsync(ulong flightMasterGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the list of available taxi nodes from the flight master.
        /// Sends CMSG_TAXIQUERYAVAILABLENODES with the flight master's GUID.
        /// </summary>
        /// <param name="flightMasterGuid">The GUID of the flight master NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueryAvailableNodesAsync(ulong flightMasterGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Shows all taxi nodes on the map.
        /// Sends CMSG_TAXISHOWNODES to display the complete taxi network.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowTaxiNodesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates a flight from the current location to the specified destination.
        /// Sends CMSG_ACTIVATETAXI with the flight master's GUID and destination node.
        /// </summary>
        /// <param name="flightMasterGuid">The GUID of the flight master NPC.</param>
        /// <param name="sourceNodeId">The source taxi node ID (current location).</param>
        /// <param name="destinationNodeId">The destination taxi node ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ActivateFlightAsync(ulong flightMasterGuid, uint sourceNodeId, uint destinationNodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates an express taxi (direct flight without stopping at intermediate nodes).
        /// Sends CMSG_ACTIVATETAXIEXPRESS with the flight master's GUID and destination.
        /// </summary>
        /// <param name="flightMasterGuid">The GUID of the flight master NPC.</param>
        /// <param name="sourceNodeId">The source taxi node ID (current location).</param>
        /// <param name="destinationNodeId">The destination taxi node ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ActivateExpressFlightAsync(ulong flightMasterGuid, uint sourceNodeId, uint destinationNodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all taxi nodes from the map (admin/debug function).
        /// Sends CMSG_TAXICLEARALLNODES.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ClearAllNodesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Enables all taxi nodes on the map (admin/debug function).
        /// Sends CMSG_TAXIENABLEALLNODES.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EnableAllNodesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears a specific taxi node (admin/debug function).
        /// Sends CMSG_TAXICLEARNODE with the specified node ID.
        /// </summary>
        /// <param name="nodeId">The taxi node ID to clear.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ClearNodeAsync(uint nodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enables a specific taxi node (admin/debug function).
        /// Sends CMSG_TAXIENABLENODE with the specified node ID.
        /// </summary>
        /// <param name="nodeId">The taxi node ID to enable.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EnableNodeAsync(uint nodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a specific taxi node is available for use.
        /// </summary>
        /// <param name="nodeId">The taxi node ID to check.</param>
        /// <returns>True if the node is available, false otherwise.</returns>
        bool IsNodeAvailable(uint nodeId);

        /// <summary>
        /// Gets the cost of flying to a specific destination node (if known).
        /// This information is typically received after querying available nodes.
        /// </summary>
        /// <param name="destinationNodeId">The destination node ID.</param>
        /// <returns>The cost in copper, or null if cost is unknown.</returns>
        uint? GetFlightCost(uint destinationNodeId);

        /// <summary>
        /// Performs a complete flight interaction: hello, query nodes, activate flight.
        /// This is a convenience method for quick flights.
        /// </summary>
        /// <param name="flightMasterGuid">The GUID of the flight master NPC.</param>
        /// <param name="destinationNodeId">The destination taxi node ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickFlightAsync(ulong flightMasterGuid, uint destinationNodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the taxi map if it's currently open.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseTaxiMapAsync(CancellationToken cancellationToken = default);
    }
}