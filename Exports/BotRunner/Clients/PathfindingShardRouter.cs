using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Clients;

/// <summary>
/// Routes pathfinding requests to sharded PathfindingService instances.
/// Each shard loads full map data. Bots are assigned to shards by hash.
/// Target: 16-core machine → 4 shards → 256 concurrent handlers.
/// </summary>
public class PathfindingShardRouter
{
    public record ShardEndpoint(string Host, int Port, int ShardId);

    private readonly List<ShardEndpoint> _shards;

    public PathfindingShardRouter(IEnumerable<ShardEndpoint> shards)
    {
        _shards = shards.ToList();
        if (_shards.Count == 0)
            throw new ArgumentException("At least one shard endpoint required");
    }

    /// <summary>
    /// Get the shard endpoint for a specific bot.
    /// Uses consistent hashing to ensure the same bot always routes to the same shard.
    /// </summary>
    public ShardEndpoint GetShard(string accountName)
    {
        var shardIndex = Math.Abs(accountName.GetHashCode()) % _shards.Count;
        return _shards[shardIndex];
    }

    /// <summary>
    /// Get the shard endpoint by index.
    /// </summary>
    public ShardEndpoint GetShardByIndex(int index)
    {
        return _shards[index % _shards.Count];
    }

    /// <summary>Number of configured shards.</summary>
    public int ShardCount => _shards.Count;

    /// <summary>All configured shard endpoints.</summary>
    public IReadOnlyList<ShardEndpoint> AllShards => _shards;

    /// <summary>
    /// Create a default shard configuration for local machine.
    /// Generates N shards on sequential ports starting from basePort.
    /// </summary>
    public static PathfindingShardRouter CreateLocal(int shardCount, int basePort = 5001)
    {
        var shards = Enumerable.Range(0, shardCount)
            .Select(i => new ShardEndpoint("127.0.0.1", basePort + i, i))
            .ToList();
        return new PathfindingShardRouter(shards);
    }
}
