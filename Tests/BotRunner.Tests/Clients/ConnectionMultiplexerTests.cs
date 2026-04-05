using BotRunner.Clients;

namespace BotRunner.Tests.Clients;

public class ConnectionMultiplexerTests
{
    [Fact]
    public async Task GetConnection_RoutesByHash()
    {
        using var mux = new ConnectionMultiplexer<string>(
            idx => Task.FromResult($"conn-{idx}"),
            maxConnections: 10);

        var conn = await mux.GetConnectionAsync("bot1");

        Assert.NotNull(conn);
        Assert.StartsWith("conn-", conn);
    }

    [Fact]
    public async Task SameBotSameConnection()
    {
        using var mux = new ConnectionMultiplexer<string>(
            idx => Task.FromResult($"conn-{idx}"),
            maxConnections: 10);

        var conn1 = await mux.GetConnectionAsync("bot42");
        var conn2 = await mux.GetConnectionAsync("bot42");

        Assert.Equal(conn1, conn2);
    }

    [Fact]
    public async Task InvalidateForces_ReCreate()
    {
        int createCount = 0;
        using var mux = new ConnectionMultiplexer<string>(
            idx => { createCount++; return Task.FromResult($"conn-{createCount}"); },
            maxConnections: 10);

        var conn1 = await mux.GetConnectionAsync("bot1");
        mux.InvalidateConnection("bot1");
        var conn2 = await mux.GetConnectionAsync("bot1");

        Assert.NotEqual(conn1, conn2);
    }

    [Fact]
    public async Task ActiveConnections_TracksCount()
    {
        using var mux = new ConnectionMultiplexer<string>(
            idx => Task.FromResult($"conn-{idx}"),
            maxConnections: 100);

        await mux.GetConnectionAsync("bot1");
        await mux.GetConnectionAsync("bot2");

        // Different bots may hash to same or different slots
        Assert.True(mux.ActiveConnections >= 1);
    }

    [Fact]
    public async Task RequestsRouted_Increments()
    {
        using var mux = new ConnectionMultiplexer<string>(
            idx => Task.FromResult($"conn-{idx}"),
            maxConnections: 10);

        await mux.GetConnectionAsync("bot1");
        await mux.GetConnectionAsync("bot2");
        await mux.GetConnectionAsync("bot3");

        Assert.Equal(3, mux.RequestsRouted);
    }
}
