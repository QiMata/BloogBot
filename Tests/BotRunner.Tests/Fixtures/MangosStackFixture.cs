using Tests.Infrastructure;

namespace BotRunner.Tests.Fixtures;

/// <summary>
/// xUnit fixture that ensures the MaNGOS server stack (MySQL, realmd, mangosd) is running.
/// Auto-launches servers if not already running. Tests should use Skip.IfNot(fixture.IsAvailable, ...)
/// when the stack is optional, or let the fixture throw if the stack is required.
/// </summary>
public class MangosStackFixture : IAsyncLifetime
{
    public IntegrationTestConfig Config { get; } = IntegrationTestConfig.FromEnvironment();
    public ServiceHealthChecker Health { get; } = new();
    public bool IsAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            await MangosServerLauncher.EnsureRunningAsync(Config);
            IsAvailable = await Health.IsMangosdAvailableAsync(Config);
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
