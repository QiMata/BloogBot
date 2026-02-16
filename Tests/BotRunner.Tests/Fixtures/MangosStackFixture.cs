using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;
using Tests.Infrastructure;

namespace BotRunner.Tests.Fixtures;

/// <summary>
/// xUnit fixture that checks whether the MaNGOS server stack (MySQL, realmd, mangosd) is available.
/// Does NOT attempt to launch any services — tests should use Skip.IfNot(fixture.IsAvailable, ...)
/// to skip gracefully when the stack is not running.
/// </summary>
public class MangosStackFixture : IAsyncLifetime
{
    public IntegrationTestConfig Config { get; } = IntegrationTestConfig.FromEnvironment();
    public ServiceHealthChecker Health { get; } = new();
    public bool IsAvailable { get; private set; }
    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            var authReady = await Health.IsRealmdAvailableAsync(Config);
            var worldReady = await Health.IsMangosdAvailableAsync(Config);

            if (!authReady && !worldReady)
            {
                UnavailableReason = $"MaNGOS auth ({Config.AuthServerIp}:{Config.AuthServerPort}) and world (127.0.0.1:{Config.WorldServerPort}) servers are not running.";
                IsAvailable = false;
                return;
            }
            else if (!authReady)
            {
                UnavailableReason = $"MaNGOS auth server not available at {Config.AuthServerIp}:{Config.AuthServerPort}.";
                IsAvailable = false;
                return;
            }
            else if (!worldReady)
            {
                UnavailableReason = $"MaNGOS world server not available on port {Config.WorldServerPort}.";
                IsAvailable = false;
                return;
            }

            // Verify MySQL credentials beyond just TCP connectivity
            var mysqlReady = await VerifyMySqlCredentialsAsync();
            if (!mysqlReady)
            {
                UnavailableReason = $"MySQL is reachable on port {Config.MySqlPort} but credential verification failed. Check mangos/mangos credentials.";
                IsAvailable = false;
                return;
            }

            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            UnavailableReason = $"Error checking MaNGOS availability: {ex.Message}";
        }
    }

    private async Task<bool> VerifyMySqlCredentialsAsync()
    {
        var connectionString = $"Server=127.0.0.1;Port={Config.MySqlPort};Uid=mangos;Pwd=mangos;Connection Timeout=5;";
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
