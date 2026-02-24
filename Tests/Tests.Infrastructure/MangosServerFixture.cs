using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Xunit;

namespace Tests.Infrastructure;

/// <summary>
/// xUnit fixture that checks whether the MaNGOS server stack is available.
/// Checks: auth server (realmd), world server (mangosd), MySQL (credential verification).
///
/// Does NOT launch or manage any processes — that's BotServiceFixture's job.
///
/// Usage:
///   public class MyTest : IClassFixture&lt;MangosServerFixture&gt;
///   {
///       public MyTest(MangosServerFixture fixture)
///       {
///           Skip.IfNot(fixture.IsAvailable, fixture.UnavailableReason ?? "MaNGOS not available");
///       }
///   }
/// </summary>
public class MangosServerFixture : IAsyncLifetime
{
    public IntegrationTestConfig Config { get; } = IntegrationTestConfig.FromEnvironment();
    public ServiceHealthChecker Health { get; } = new();

    /// <summary>Whether all MaNGOS services (auth, world, MySQL) are available.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Individual service status for callers that need partial availability.</summary>
    public bool IsAuthAvailable { get; private set; }
    public bool IsWorldAvailable { get; private set; }
    public bool IsMySqlAvailable { get; private set; }

    /// <summary>Reason for unavailability, if any.</summary>
    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            IsAuthAvailable = await Health.IsRealmdAvailableAsync(Config);
            IsWorldAvailable = await Health.IsMangosdAvailableAsync(Config);

            if (!IsAuthAvailable || !IsWorldAvailable)
            {
                var missing = new System.Collections.Generic.List<string>();
                if (!IsAuthAvailable) missing.Add($"auth ({Config.AuthServerIp}:{Config.AuthServerPort})");
                if (!IsWorldAvailable) missing.Add($"world (127.0.0.1:{Config.WorldServerPort})");
                UnavailableReason = $"MaNGOS servers not running: {string.Join(", ", missing)}";
                IsAvailable = false;
                return;
            }

            IsMySqlAvailable = await VerifyMySqlCredentialsAsync();
            if (!IsMySqlAvailable)
            {
                UnavailableReason = $"MySQL reachable on port {Config.MySqlPort} but credential verification failed ({Config.MySqlUser}/{Config.MySqlPassword}). Override via WWOW_TEST_MYSQL_USER / WWOW_TEST_MYSQL_PASSWORD.";
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
        var connectionString = $"Server=127.0.0.1;Port={Config.MySqlPort};Uid={Config.MySqlUser};Pwd={Config.MySqlPassword};Connection Timeout=5;";
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
