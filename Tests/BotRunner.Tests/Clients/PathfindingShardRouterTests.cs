using BotRunner.Clients;

namespace BotRunner.Tests.Clients;

public class PathfindingShardRouterTests
{
    [Fact]
    public void GetShard_ConsistentHash()
    {
        var router = PathfindingShardRouter.CreateLocal(4);

        var shard1 = router.GetShard("TESTBOT1");
        var shard2 = router.GetShard("TESTBOT1");

        Assert.Equal(shard1, shard2);
    }

    [Fact]
    public void CreateLocal_GeneratesNShards()
    {
        var router = PathfindingShardRouter.CreateLocal(4, basePort: 6000);

        Assert.Equal(4, router.AllShards.Count);
        Assert.Equal(6000, router.AllShards[0].Port);
        Assert.Equal(6001, router.AllShards[1].Port);
        Assert.Equal(6002, router.AllShards[2].Port);
        Assert.Equal(6003, router.AllShards[3].Port);
    }

    [Fact]
    public void ShardCount_Correct()
    {
        var router = PathfindingShardRouter.CreateLocal(8);

        Assert.Equal(8, router.ShardCount);
    }

    [Fact]
    public void Constructor_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new PathfindingShardRouter(Array.Empty<PathfindingShardRouter.ShardEndpoint>()));
    }

    [Fact]
    public void GetShardByIndex_WrapsAround()
    {
        var router = PathfindingShardRouter.CreateLocal(3);

        // Index 5 % 3 = 2
        var shard = router.GetShardByIndex(5);
        Assert.Equal(2, shard.ShardId);
    }

    [Fact]
    public void AllShards_HostIsLocalhost()
    {
        var router = PathfindingShardRouter.CreateLocal(2);

        Assert.All(router.AllShards, s => Assert.Equal("127.0.0.1", s.Host));
    }
}
