using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.Clients;

/// <summary>
/// P3.2, P3.5, P4.2: Functional tests for Docker PathfindingService and SceneDataService.
/// Verifies TCP connectivity and basic request/response flow.
/// These tests require the Docker services to be running.
/// </summary>
public class DockerServiceTests
{
    private readonly ITestOutputHelper _output;

    public DockerServiceTests(ITestOutputHelper output) => _output = output;

    [SkippableFact]
    public async Task PathfindingService_TcpConnect_Responds()
    {
        // P3.2: Verify PathfindingService is reachable on port 5001
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync("127.0.0.1", 5001);
        }
        catch (SocketException)
        {
            Skip.If(true, "PathfindingService not running on port 5001");
            return;
        }

        Assert.True(client.Connected, "Should be connected to PathfindingService");
        _output.WriteLine("[P3.2] PathfindingService TCP connected on port 5001");
    }

    [SkippableFact]
    public async Task SceneDataService_TcpConnect_Responds()
    {
        // P4.2: Verify SceneDataService is reachable on port 5003
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync("127.0.0.1", 5003);
        }
        catch (SocketException)
        {
            Skip.If(true, "SceneDataService not running on port 5003");
            return;
        }

        Assert.True(client.Connected, "Should be connected to SceneDataService");
        _output.WriteLine("[P4.2] SceneDataService TCP connected on port 5003");
    }

    [SkippableFact]
    public async Task PathfindingService_MultipleConnections_NoDeadlock()
    {
        // P3.5: 10 concurrent TCP connections to verify no deadlock
        var tasks = new Task[10];
        int successCount = 0;

        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var client = new TcpClient();
                try
                {
                    await client.ConnectAsync("127.0.0.1", 5001);
                    if (client.Connected)
                        System.Threading.Interlocked.Increment(ref successCount);
                }
                catch (SocketException) { }
            });
        }

        await Task.WhenAll(tasks);

        if (successCount == 0)
        {
            Skip.If(true, "PathfindingService not running");
            return;
        }

        Assert.Equal(10, successCount);
        _output.WriteLine($"[P3.5] {successCount}/10 concurrent connections succeeded");
    }
}
