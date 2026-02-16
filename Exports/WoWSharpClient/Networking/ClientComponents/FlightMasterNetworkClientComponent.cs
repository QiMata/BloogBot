using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of flight master network agent that handles flight operations in World of Warcraft.
    /// Manages taxi node interactions, flight path queries, and flight activation using the Mangos protocol.
    /// Uses opcode-backed observables (no events/subjects exposed).
    /// </summary>
    public class FlightMasterNetworkClientComponent : NetworkClientComponent, IFlightMasterNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<FlightMasterNetworkClientComponent> _logger;
        private bool _isTaxiMapOpen;
        private readonly List<uint> _availableTaxiNodes = [];
        private readonly Dictionary<uint, uint> _flightCosts = new();
        private bool _disposed;
        private uint _currentNodeId;

        // Reactive opcode-backed observables
        private readonly IObservable<(ulong FlightMasterGuid, IReadOnlyList<uint> Nodes)> _taxiMapOpened;
        private readonly IObservable<Unit> _taxiMapClosed;
        private readonly IObservable<(uint SourceNodeId, uint DestinationNodeId, uint Cost)> _flightActivated;
        private readonly IObservable<(uint NodeId, byte Status)> _taxiNodeStatus;
        private readonly IObservable<string> _flightMasterErrors;

        /// <summary>
        /// Initializes a new instance of the FlightMasterNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public FlightMasterNetworkClientComponent(IWorldClient worldClient, ILogger<FlightMasterNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Build reactive streams from opcode handlers lazily so test setups applied after construction are respected
            _taxiMapOpened = Observable.Defer(() =>
                    SafeOpcodeStream(Opcode.SMSG_SHOWTAXINODES)
                        .Select(ParseShowTaxiNodes)
                        .Do(tuple =>
                        {
                            _isTaxiMapOpen = true;
                            _availableTaxiNodes.Clear();
                            _availableTaxiNodes.AddRange(tuple.Nodes);
                            _logger.LogDebug("SMSG_SHOWTAXINODES parsed; {Count} nodes available", tuple.Nodes.Count);
                        })
                )
                .Publish()
                .RefCount();

            // Best-effort close signal (no explicit close opcode in 1.12). Use disconnect as a proxy.
            _taxiMapClosed = Observable.Defer(() =>
                    (_worldClient.WhenDisconnected ?? Observable.Empty<Exception?>())
                        .Select(_ => Unit.Default)
                        .Do(_ =>
                        {
                            _isTaxiMapOpen = false;
                            _availableTaxiNodes.Clear();
                            _flightCosts.Clear();
                            _logger.LogDebug("Taxi context closed (disconnect)");
                        })
                )
                .Publish()
                .RefCount();

            _flightActivated = Observable.Defer(() =>
                    SafeOpcodeStream(Opcode.SMSG_ACTIVATETAXIREPLY)
                        .Select(ParseActivateTaxiReply)
                        .Do(data => _logger.LogDebug("SMSG_ACTIVATETAXIREPLY: {Src}->{Dst} cost {Cost}", data.SourceNodeId, data.DestinationNodeId, data.Cost))
                )
                .Publish()
                .RefCount();

            _taxiNodeStatus = Observable.Defer(() =>
                    SafeOpcodeStream(Opcode.SMSG_TAXINODE_STATUS)
                        .Select(ParseTaxiNodeStatus)
                        .Do(s => _logger.LogDebug("SMSG_TAXINODE_STATUS: node {Node} status {Status}", s.NodeId, s.Status))
                )
                .Publish()
                .RefCount();

            // No dedicated error opcode known; expose a never stream to satisfy interface
            _flightMasterErrors = Observable.Never<string>();
        }

        #region Properties

        /// <inheritdoc />
        public bool IsTaxiMapOpen => _isTaxiMapOpen;

        /// <inheritdoc />
        public IReadOnlyList<uint> AvailableTaxiNodes => _availableTaxiNodes.AsReadOnly();

        /// <summary>Gets the current (nearest) taxi node ID from the last SMSG_SHOWTAXINODES.</summary>
        public uint? CurrentNodeId => _currentNodeId > 0 ? _currentNodeId : null;

        // Explicit interface implementation to expose observables
        IObservable<(ulong FlightMasterGuid, IReadOnlyList<uint> Nodes)> IFlightMasterNetworkClientComponent.TaxiMapOpened => _taxiMapOpened;
        IObservable<Unit> IFlightMasterNetworkClientComponent.TaxiMapClosed => _taxiMapClosed;
        IObservable<(uint SourceNodeId, uint DestinationNodeId, uint Cost)> IFlightMasterNetworkClientComponent.FlightActivated => _flightActivated;
        IObservable<(uint NodeId, byte Status)> IFlightMasterNetworkClientComponent.TaxiNodeStatus => _taxiNodeStatus;
        IObservable<string> IFlightMasterNetworkClientComponent.FlightMasterErrors => _flightMasterErrors;

        #endregion

        #region Operations (CMSG)

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
                await Task.Delay(100, cancellationToken);
                await QueryAvailableNodesAsync(flightMasterGuid, cancellationToken);
                await Task.Delay(200, cancellationToken);

                if (!IsNodeAvailable(destinationNodeId))
                {
                    throw new InvalidOperationException($"Destination node {destinationNodeId} is not available");
                }

                if (_currentNodeId == 0)
                {
                    throw new InvalidOperationException("Source taxi node not determined. Ensure SMSG_SHOWTAXINODES was received.");
                }
                await ActivateFlightAsync(flightMasterGuid, _currentNodeId, destinationNodeId, cancellationToken);

                _logger.LogInformation("Quick flight initiated to node {DestinationNode} via flight master: {FlightMasterGuid:X}", destinationNodeId, flightMasterGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick flight failed to node {DestinationNode} via flight master: {FlightMasterGuid:X}", destinationNodeId, flightMasterGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseTaxiMapAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing taxi map (local state)");
                _isTaxiMapOpen = false;
                _availableTaxiNodes.Clear();
                _flightCosts.Clear();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close taxi map");
                throw;
            }
        }

        #endregion

        #region Server Response Handlers (Compat)

        /// <summary>
        /// Handles server responses for taxi map opening. For legacy compatibility.
        /// </summary>
        public void HandleTaxiMapOpened(ulong flightMasterGuid, IReadOnlyList<uint> availableNodes)
        {
            _isTaxiMapOpen = true;
            _availableTaxiNodes.Clear();
            _availableTaxiNodes.AddRange(availableNodes);
            _logger.LogDebug("Taxi map opened for flight master: {FlightMasterGuid:X} with {NodeCount} available nodes", flightMasterGuid, availableNodes.Count);
        }

        /// <summary>
        /// Handles server responses for flight activation. For legacy compatibility.
        /// </summary>
        public void HandleFlightActivated(uint sourceNodeId, uint destinationNodeId, uint cost)
        {
            _logger.LogDebug("Flight activated from node {SourceNode} to node {DestinationNode} (cost: {Cost})", sourceNodeId, destinationNodeId, cost);
        }

        /// <summary>
        /// Handles server responses for taxi node status. For legacy compatibility.
        /// </summary>
        public void HandleTaxiNodeStatus(uint nodeId, byte status)
        {
            _logger.LogDebug("Taxi node status received for node {NodeId}: {Status}", nodeId, status);
        }

        /// <summary>
        /// Updates the flight cost for a specific destination. For legacy compatibility.
        /// </summary>
        public void UpdateFlightCost(uint destinationNodeId, uint cost)
        {
            _flightCosts[destinationNodeId] = cost;
            _logger.LogDebug("Flight cost updated for node {NodeId}: {Cost}", destinationNodeId, cost);
        }

        /// <summary>
        /// Handles server responses for flight master operation failures. For legacy compatibility.
        /// </summary>
        public void HandleFlightMasterError(string errorMessage)
        {
            _logger.LogWarning("Flight master operation failed: {Error}", errorMessage);
        }

        #endregion

        #region Parsing Helpers

        /// <summary>
        /// Parses SMSG_SHOWTAXINODES payload.
        /// MaNGOS format: uint32(1) + ObjectGuid(8) + uint32(curloc) + bitmask[].
        /// Each uint32 in bitmask represents 32 taxi nodes as bits.
        /// </summary>
        public static (ulong FlightMasterGuid, uint CurrentNodeId, IReadOnlyList<uint> Nodes) ParseShowTaxiNodesPacket(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 16) return (0UL, 0, Array.Empty<uint>());

                int offset = 0;
                // uint32 "show" flag (always 1), skip
                offset += 4;
                // ObjectGuid(8) - flight master GUID
                ulong guid = BitConverter.ToUInt64(span.Slice(offset, 8));
                offset += 8;
                // uint32 current (nearest) taxi node ID
                uint currentNodeId = BitConverter.ToUInt32(span.Slice(offset, 4));
                offset += 4;

                // Remaining bytes are the taxi bitmask (AppendTaximaskTo)
                List<uint> nodes = new();
                int bitmaskIndex = 0;
                while (offset + 4 <= span.Length)
                {
                    uint mask = BitConverter.ToUInt32(span.Slice(offset, 4));
                    offset += 4;
                    for (int bit = 0; bit < 32; bit++)
                    {
                        if ((mask & (1u << bit)) != 0)
                        {
                            uint nodeId = (uint)(bitmaskIndex * 32 + bit);
                            if (nodeId > 0) // node 0 is not a valid taxi node
                                nodes.Add(nodeId);
                        }
                    }
                    bitmaskIndex++;
                }

                return (guid, currentNodeId, nodes.AsReadOnly());
            }
            catch
            {
                return (0UL, 0, Array.Empty<uint>());
            }
        }

        private (ulong FlightMasterGuid, IReadOnlyList<uint> Nodes) ParseShowTaxiNodes(ReadOnlyMemory<byte> payload)
        {
            var (guid, curNode, nodes) = ParseShowTaxiNodesPacket(payload);
            _currentNodeId = curNode;
            return (guid, nodes);
        }

        /// <summary>
        /// Parses SMSG_ACTIVATETAXIREPLY payload.
        /// MaNGOS format: uint32 result code (0=OK, 1=ServerError, 2=NoPath, 3=NoMoney, etc.).
        /// </summary>
        public static (uint SourceNodeId, uint DestinationNodeId, uint Cost) ParseActivateTaxiReply(ReadOnlyMemory<byte> payload)
        {
            try
            {
                // MaNGOS only sends uint32 result code; map it to SourceNodeId for compatibility
                uint result = payload.Length >= 4 ? BitConverter.ToUInt32(payload.Span[0..4]) : 0u;
                return (result, 0u, 0u);
            }
            catch
            {
                return (0u, 0u, 0u);
            }
        }

        /// <summary>
        /// Parses SMSG_TAXINODE_STATUS payload.
        /// MaNGOS format: ObjectGuid(8) + uint8(knownNode) = 9 bytes.
        /// </summary>
        public static (uint NodeId, byte Status) ParseTaxiNodeStatus(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                // ObjectGuid(8) - flight master GUID (store lower 32 bits as NodeId for compat)
                uint nodeCompat = span.Length >= 4 ? BitConverter.ToUInt32(span[0..4]) : 0u;
                byte status = span.Length >= 9 ? span[8] : (byte)0;
                return (nodeCompat, status);
            }
            catch
            {
                return (0u, 0);
            }
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the flight master network client component and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing FlightMasterNetworkClientComponent");

            _disposed = true;
            _logger.LogDebug("FlightMasterNetworkClientComponent disposed");
        }

        #endregion
    }
}