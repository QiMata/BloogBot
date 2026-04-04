using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Clients;

/// <summary>
/// Multiplexes N bots over M connections to StateManager.
/// Instead of 1 TCP connection per bot (3000 connections),
/// routes requests by bot ID over a pool of shared connections (target: ~100).
/// Uses AsyncRequest.Id for routing responses back to the correct bot.
/// </summary>
public class ConnectionMultiplexer<TConnection> : IDisposable where TConnection : class
{
    private readonly ConcurrentDictionary<int, TConnection> _connections = new();
    private readonly Func<int, Task<TConnection>> _connectionFactory;
    private readonly int _maxConnections;
    private int _nextConnectionId;
    private int _disposed;

    /// <summary>Active connection count.</summary>
    public int ActiveConnections => _connections.Count;

    /// <summary>Total requests routed.</summary>
    public long RequestsRouted { get; private set; }

    public ConnectionMultiplexer(
        Func<int, Task<TConnection>> connectionFactory,
        int maxConnections = 100)
    {
        _connectionFactory = connectionFactory;
        _maxConnections = maxConnections;
    }

    /// <summary>
    /// Get a connection for a specific bot. Bots are assigned to connections
    /// by hash to ensure the same bot always uses the same connection.
    /// </summary>
    public async Task<TConnection> GetConnectionAsync(string botId, CancellationToken ct = default)
    {
        var connectionIndex = Math.Abs(botId.GetHashCode()) % _maxConnections;

        if (_connections.TryGetValue(connectionIndex, out var existing))
        {
            Interlocked.Increment(ref _nextConnectionId); // track routing
            RequestsRouted++;
            return existing;
        }

        // Create new connection for this slot
        var connection = await _connectionFactory(connectionIndex);
        _connections[connectionIndex] = connection;
        RequestsRouted++;
        return connection;
    }

    /// <summary>
    /// Remove a dead connection, forcing re-creation on next use.
    /// </summary>
    public void InvalidateConnection(string botId)
    {
        var connectionIndex = Math.Abs(botId.GetHashCode()) % _maxConnections;
        _connections.TryRemove(connectionIndex, out _);
    }

    /// <summary>Get all active connections.</summary>
    public IReadOnlyList<TConnection> GetAllConnections()
        => _connections.Values.ToList();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        foreach (var conn in _connections.Values)
        {
            if (conn is IDisposable disposable)
                disposable.Dispose();
        }
        _connections.Clear();
    }
}
