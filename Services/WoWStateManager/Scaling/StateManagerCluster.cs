using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WoWStateManager.Scaling;

/// <summary>
/// Partitioned StateManager for horizontal scaling.
/// Each instance manages a shard of bots by zone/map.
/// Cross-zone coordination via inter-StateManager gossip protocol.
/// Target: 3000 bots across M instances, each handling 300-1000 bots.
/// </summary>
public class StateManagerCluster : IDisposable
{
    public record ClusterNode(string NodeId, string Host, int Port, int GossipPort);
    public record BotAssignment(string AccountName, string NodeId, uint MapId);

    private readonly ClusterNode _self;
    private readonly ConcurrentDictionary<string, ClusterNode> _peers = new();
    private readonly ConcurrentDictionary<string, BotAssignment> _assignments = new();
    private readonly UdpClient? _gossipListener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _gossipTask;

    /// <summary>This node's identity.</summary>
    public ClusterNode Self => _self;

    /// <summary>All known cluster nodes (including self).</summary>
    public IReadOnlyList<ClusterNode> AllNodes
    {
        get
        {
            var nodes = _peers.Values.ToList();
            nodes.Add(_self);
            return nodes;
        }
    }

    /// <summary>Bot assignments for this node.</summary>
    public IReadOnlyDictionary<string, BotAssignment> LocalAssignments
        => _assignments;

    public StateManagerCluster(string nodeId, string host, int port, int gossipPort)
    {
        _self = new ClusterNode(nodeId, host, port, gossipPort);
        try
        {
            _gossipListener = new UdpClient(gossipPort);
        }
        catch (SocketException)
        {
            // Gossip port in use — single-node mode
        }
    }

    /// <summary>Register a peer node.</summary>
    public void AddPeer(ClusterNode peer)
    {
        if (peer.NodeId != _self.NodeId)
            _peers[peer.NodeId] = peer;
    }

    /// <summary>Assign a bot to this node.</summary>
    public void AssignBot(string accountName, uint mapId)
    {
        _assignments[accountName] = new BotAssignment(accountName, _self.NodeId, mapId);
    }

    /// <summary>Remove a bot assignment.</summary>
    public void UnassignBot(string accountName)
    {
        _assignments.TryRemove(accountName, out _);
    }

    /// <summary>
    /// Determine which node should handle a bot based on zone/map.
    /// Uses map ID to shard: bots on the same map go to the same node.
    /// </summary>
    public ClusterNode GetNodeForMap(uint mapId)
    {
        var allNodes = AllNodes;
        var index = (int)(mapId % (uint)allNodes.Count);
        return allNodes[index];
    }

    /// <summary>Start gossip protocol for peer discovery and health checks.</summary>
    public void StartGossip()
    {
        if (_gossipListener == null) return;
        _gossipTask = Task.Run(() => GossipLoop(_cts.Token));
    }

    /// <summary>Send a gossip heartbeat to all peers.</summary>
    public async Task SendHeartbeatAsync()
    {
        var message = $"HEARTBEAT|{_self.NodeId}|{_self.Host}|{_self.Port}|{_assignments.Count}";
        var data = Encoding.UTF8.GetBytes(message);

        foreach (var peer in _peers.Values)
        {
            try
            {
                using var client = new UdpClient();
                await client.SendAsync(data, data.Length, peer.Host, peer.GossipPort);
            }
            catch { /* peer may be down */ }
        }
    }

    private async Task GossipLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _gossipListener != null)
        {
            try
            {
                var result = await _gossipListener.ReceiveAsync(ct);
                var message = Encoding.UTF8.GetString(result.Buffer);
                ProcessGossip(message);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore malformed gossip */ }
        }
    }

    private void ProcessGossip(string message)
    {
        var parts = message.Split('|');
        if (parts.Length < 4 || parts[0] != "HEARTBEAT") return;

        var nodeId = parts[1];
        var host = parts[2];
        if (!int.TryParse(parts[3], out var port)) return;

        if (nodeId != _self.NodeId)
        {
            AddPeer(new ClusterNode(nodeId, host, port, 0));
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _gossipTask?.Wait(TimeSpan.FromSeconds(2));
        _gossipListener?.Dispose();
        _cts.Dispose();
    }
}
