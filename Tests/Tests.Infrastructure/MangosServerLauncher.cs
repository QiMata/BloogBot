using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Tests.Infrastructure;

/// <summary>
/// Checks availability of the MaNGOS server stack (MySQL, realmd, mangosd)
/// for integration tests. Does NOT launch any processes — services must be
/// started manually or by external tooling before running tests.
/// </summary>
public static class MangosServerLauncher
{
    /// <summary>
    /// Checks that MySQL, realmd, and mangosd are all reachable via TCP.
    /// Returns without error if all are available; logs which are missing.
    /// </summary>
    public static async Task EnsureRunningAsync(
        IntegrationTestConfig config,
        ITestOutputHelper? output = null)
    {
        var mysqlOk = await IsPortOpenAsync("127.0.0.1", config.MySqlPort);
        var authOk = await IsPortOpenAsync(config.AuthServerIp, config.AuthServerPort);
        var worldOk = await IsPortOpenAsync("127.0.0.1", config.WorldServerPort);

        output?.WriteLine($"MySQL (port {config.MySqlPort}): {(mysqlOk ? "available" : "NOT available")}");
        output?.WriteLine($"realmd ({config.AuthServerIp}:{config.AuthServerPort}): {(authOk ? "available" : "NOT available")}");
        output?.WriteLine($"mangosd (port {config.WorldServerPort}): {(worldOk ? "available" : "NOT available")}");

        if (!mysqlOk || !authOk || !worldOk)
        {
            output?.WriteLine("One or more MaNGOS services are not running. " +
                "Start them manually before running integration tests.");
        }
    }

    private static async Task<bool> IsPortOpenAsync(string ip, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(500));
            return ReferenceEquals(completed, connectTask) && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
