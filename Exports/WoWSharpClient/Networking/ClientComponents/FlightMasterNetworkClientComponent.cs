using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of flight master network agent that handles flight operations in World of Warcraft.
    /// Manages taxi node interactions, flight path queries, and flight activation using the Mangos protocol.
    /// </summary>
    public class FlightMasterNetworkClientComponent : NetworkClientComponent, IFlightMasterNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<FlightMasterNetworkClientComponent> _logger;
        private bool _isTaxiMapOpen;
        private readonly List<uint> _availableTaxiNodes = [];
        private readonly Dictionary<uint, uint> _flightCosts = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the FlightMasterNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public FlightMasterNetworkClientComponent(IWorldClient worldClient, ILogger<FlightMasterNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsTaxiMapOpen => _isTaxiMapOpen;

        /// <inheritdoc />
        public IReadOnlyList<uint> AvailableTaxiNodes => _availableTaxiNodes.AsReadOnly();

        /// <inheritdoc />
        public event Action<ulong, IReadOnlyList<uint>>? TaxiMapOpened;

        /// <inheritdoc />
        public event Action? TaxiMapClosed;

        /// <inheritdoc />
        public event Action<uint, uint, uint>? FlightActivated;

        /// <inheritdoc />
        public event Action<uint, byte>? TaxiNodeStatusReceived;

        /// <inheritdoc />
        public event Action<string>? FlightMasterError;

        /// <inheritdoc />
        public async Task HelloFlightMasterAsync(ulong flightMasterGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Initiating conversation with flight master: {FlightMasterGuid:X}", flightMasterGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(flightMasterGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                _logger.LogInformation("Flight master interaction initiated with: {FlightMasterGuid:X}", flightMasterGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate conversation with flight master: {FlightMasterGuid:X}", flightMasterGuid);
                FlightMasterError?.Invoke($"Failed to interact with flight master: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QueryTaxiNodeStatusAsync(ulong flightMasterGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Querying taxi node status from flight master: {FlightMasterGuid:X}", flightMasterGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(flightMasterGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TAXINODE_STATUS_QUERY, payload, cancellationToken);

                _logger.LogInformation("Taxi node status query sent to flight master: {FlightMasterGuid:X}", flightMasterGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query taxi node status from flight master: {FlightMasterGuid:X}", flightMasterGuid);
                FlightMasterError?.Invoke($"Failed to query taxi node status: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QueryAvailableNodesAsync(ulong flightMasterGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Querying available taxi nodes from flight master: {FlightMasterGuid:X}", flightMasterGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(flightMasterGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TAXIQUERYAVAILABLENODES, payload, cancellationToken);

                _logger.LogInformation("Available taxi nodes query sent to flight master: {FlightMasterGuid:X}", flightMasterGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query available taxi nodes from flight master: {FlightMasterGuid:X}", flightMasterGuid);
                FlightMasterError?.Invoke($"Failed to query available nodes: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task ShowTaxiNodesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting to show all taxi nodes");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TAXISHOWNODES, [], cancellationToken);

                _logger.LogInformation("Show taxi nodes request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show taxi nodes");
                FlightMasterError?.Invoke($"Failed to show taxi nodes: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ActivateFlightAsync(ulong flightMasterGuid, uint sourceNodeId, uint destinationNodeId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Activating flight from node {SourceNode} to node {DestinationNode} via flight master: {FlightMasterGuid:X}", sourceNodeId, destinationNodeId, flightMasterGuid);

                var payload = new byte[16];
                BitConverter.GetBytes(flightMasterGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(sourceNodeId).CopyTo(payload, 8);
                BitConverter.GetBytes(destinationNodeId).CopyTo(payload, 12);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ACTIVATETAXI, payload, cancellationToken);

                _logger.LogInformation("Flight activation request sent from node {SourceNode} to node {DestinationNode} via flight master: {FlightMasterGuid:X}", sourceNodeId, destinationNodeId, flightMasterGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate flight from node {SourceNode} to node {DestinationNode}", sourceNodeId, destinationNodeId);
                FlightMasterError?.Invoke($"Failed to activate flight: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ActivateExpressFlightAsync(ulong flightMasterGuid, uint sourceNodeId, uint destinationNodeId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Activating express flight from node {SourceNode} to node {DestinationNode} via flight master: {FlightMasterGuid:X}", sourceNodeId, destinationNodeId, flightMasterGuid);

                var payload = new byte[16];
                BitConverter.GetBytes(flightMasterGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(sourceNodeId).CopyTo(payload, 8);
                BitConverter.GetBytes(destinationNodeId).CopyTo(payload, 12);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ACTIVATETAXIEXPRESS, payload, cancellationToken);

                _logger.LogInformation("Express flight activation request sent from node {SourceNode} to node {DestinationNode} via flight master: {FlightMasterGuid:X}", sourceNodeId, destinationNodeId, flightMasterGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate express flight from node {SourceNode} to node {DestinationNode}", sourceNodeId, destinationNodeId);
                FlightMasterError?.Invoke($"Failed to activate express flight: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ClearAllNodesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Clearing all taxi nodes (admin function)");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TAXICLEARALLNODES, [], cancellationToken);

                _logger.LogInformation("Clear all taxi nodes request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear all taxi nodes");
                FlightMasterError?.Invoke($"Failed to clear all nodes: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task EnableAllNodesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Enabling all taxi nodes (admin function)");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TAXIENABLEALLNODES, [], cancellationToken);

                _logger.LogInformation("Enable all taxi nodes request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable all taxi nodes");
                FlightMasterError?.Invoke($"Failed to enable all nodes: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ClearNodeAsync(uint nodeId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Clearing taxi node {NodeId} (admin function)", nodeId);

                var payload = new byte[4];
                BitConverter.GetBytes(nodeId).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TAXICLEARNODE, payload, cancellationToken);

                _logger.LogInformation("Clear taxi node {NodeId} request sent", nodeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear taxi node {NodeId}", nodeId);
                FlightMasterError?.Invoke($"Failed to clear node: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task EnableNodeAsync(uint nodeId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Enabling taxi node {NodeId} (admin function)", nodeId);

                var payload = new byte[4];
                BitConverter.GetBytes(nodeId).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TAXIENABLENODE, payload, cancellationToken);

                _logger.LogInformation("Enable taxi node {NodeId} request sent", nodeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable taxi node {NodeId}", nodeId);
                FlightMasterError?.Invoke($"Failed to enable node: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public bool IsNodeAvailable(uint nodeId)
        {
            return _availableTaxiNodes.Contains(nodeId);
        }

        /// <inheritdoc />
        public uint? GetFlightCost(uint destinationNodeId)
        {
            return _flightCosts.TryGetValue(destinationNodeId, out var cost) ? cost : null;
        }

        /// <inheritdoc />
        public async Task QuickFlightAsync(ulong flightMasterGuid, uint destinationNodeId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick flight to node {DestinationNode} via flight master: {FlightMasterGuid:X}", destinationNodeId, flightMasterGuid);

                await HelloFlightMasterAsync(flightMasterGuid, cancellationToken);
                
                // Small delay to allow interaction to establish
                await Task.Delay(100, cancellationToken);
                
                await QueryAvailableNodesAsync(flightMasterGuid, cancellationToken);
                
                // Small delay to allow nodes to be populated
                await Task.Delay(200, cancellationToken);
                
                if (!IsNodeAvailable(destinationNodeId))
                {
                    throw new InvalidOperationException($"Destination node {destinationNodeId} is not available");
                }

                // For quick flight, we assume the flight master's node is the source
                // In a real implementation, you'd need to determine the current node ID
                uint sourceNodeId = 0; // This should be determined from game state
                
                await ActivateFlightAsync(flightMasterGuid, sourceNodeId, destinationNodeId, cancellationToken);

                _logger.LogInformation("Quick flight initiated to node {DestinationNode} via flight master: {FlightMasterGuid:X}", destinationNodeId, flightMasterGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick flight failed to node {DestinationNode} via flight master: {FlightMasterGuid:X}", destinationNodeId, flightMasterGuid);
                FlightMasterError?.Invoke($"Quick flight failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseTaxiMapAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing taxi map");

                // Taxi maps typically close automatically
                // But we can update our internal state
                _isTaxiMapOpen = false;
                _availableTaxiNodes.Clear();
                _flightCosts.Clear();
                TaxiMapClosed?.Invoke();

                _logger.LogInformation("Taxi map closed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close taxi map");
                FlightMasterError?.Invoke($"Failed to close taxi map: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles server responses for taxi map opening.
        /// This method should be called when SMSG_SHOWTAXINODES is received.
        /// </summary>
        /// <param name="flightMasterGuid">The GUID of the flight master.</param>
        /// <param name="availableNodes">List of available taxi node IDs.</param>
        public void HandleTaxiMapOpened(ulong flightMasterGuid, IReadOnlyList<uint> availableNodes)
        {
            _isTaxiMapOpen = true;
            _availableTaxiNodes.Clear();
            _availableTaxiNodes.AddRange(availableNodes);
            TaxiMapOpened?.Invoke(flightMasterGuid, availableNodes);
            _logger.LogDebug("Taxi map opened for flight master: {FlightMasterGuid:X} with {NodeCount} available nodes", flightMasterGuid, availableNodes.Count);
        }

        /// <summary>
        /// Handles server responses for flight activation.
        /// This method should be called when SMSG_ACTIVATETAXIREPLY is received.
        /// </summary>
        /// <param name="sourceNodeId">The source taxi node ID.</param>
        /// <param name="destinationNodeId">The destination taxi node ID.</param>
        /// <param name="cost">The flight cost in copper.</param>
        public void HandleFlightActivated(uint sourceNodeId, uint destinationNodeId, uint cost)
        {
            FlightActivated?.Invoke(sourceNodeId, destinationNodeId, cost);
            _logger.LogDebug("Flight activated from node {SourceNode} to node {DestinationNode} (cost: {Cost})", sourceNodeId, destinationNodeId, cost);
        }

        /// <summary>
        /// Handles server responses for taxi node status.
        /// This method should be called when SMSG_TAXINODE_STATUS is received.
        /// </summary>
        /// <param name="nodeId">The taxi node ID.</param>
        /// <param name="status">The status of the node.</param>
        public void HandleTaxiNodeStatus(uint nodeId, byte status)
        {
            TaxiNodeStatusReceived?.Invoke(nodeId, status);
            _logger.LogDebug("Taxi node status received for node {NodeId}: {Status}", nodeId, status);
        }

        /// <summary>
        /// Updates the flight cost for a specific destination.
        /// This is typically called when cost information is received from the server.
        /// </summary>
        /// <param name="destinationNodeId">The destination node ID.</param>
        /// <param name="cost">The flight cost in copper.</param>
        public void UpdateFlightCost(uint destinationNodeId, uint cost)
        {
            _flightCosts[destinationNodeId] = cost;
            _logger.LogDebug("Flight cost updated for node {NodeId}: {Cost}", destinationNodeId, cost);
        }

        /// <summary>
        /// Handles server responses for flight master operation failures.
        /// This method should be called when flight-related error responses are received.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        public void HandleFlightMasterError(string errorMessage)
        {
            FlightMasterError?.Invoke(errorMessage);
            _logger.LogWarning("Flight master operation failed: {Error}", errorMessage);
        }

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the flight master network client component and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing FlightMasterNetworkClientComponent");

            // Clear events to prevent memory leaks
            TaxiMapOpened = null;
            TaxiMapClosed = null;
            FlightActivated = null;
            TaxiNodeStatusReceived = null;
            FlightMasterError = null;

            _disposed = true;
            _logger.LogDebug("FlightMasterNetworkClientComponent disposed");
        }

        #endregion
    }
}