using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundBotRunner;

/// <summary>
/// P9.23 / P24.11: Hosts multiple BotContext instances in a single process.
/// Instead of 1 OS process per bot (3000 processes), runs 50-100 bots per process.
/// Each bot gets its own tick loop on a dedicated Task.
/// Navigation.dll is loaded once and shared. Scene data is shared.
/// 3000 bots = 30-60 processes instead of 3000.
///
/// Usage: Set WWOW_MULTI_BOT_COUNT=50 and WWOW_MULTI_BOT_START_INDEX=0
/// to run bots LOADBOT0 through LOADBOT49 in this process.
/// </summary>
public class MultiBotHostWorker : BackgroundService
{
    private readonly ILogger<MultiBotHostWorker> _logger;
    private readonly int _botCount;
    private readonly int _startIndex;
    private readonly string _accountPrefix;
    private readonly List<Task> _botTasks = new();
    private readonly CancellationTokenSource _shutdownCts = new();

    public MultiBotHostWorker(ILogger<MultiBotHostWorker> logger)
    {
        _logger = logger;
        _botCount = int.TryParse(Environment.GetEnvironmentVariable("WWOW_MULTI_BOT_COUNT"), out var c) ? c : 10;
        _startIndex = int.TryParse(Environment.GetEnvironmentVariable("WWOW_MULTI_BOT_START_INDEX"), out var s) ? s : 0;
        _accountPrefix = Environment.GetEnvironmentVariable("WWOW_MULTI_BOT_PREFIX") ?? "LOADBOT";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[MULTIBOT] Starting {Count} bots ({Prefix}{Start}-{End})",
            _botCount, _accountPrefix, _startIndex, _startIndex + _botCount - 1);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _shutdownCts.Token);

        // Stagger bot launches to avoid thundering herd
        for (int i = 0; i < _botCount; i++)
        {
            var botIndex = _startIndex + i;
            var accountName = $"{_accountPrefix}{botIndex}";

            _botTasks.Add(RunBotAsync(accountName, botIndex, linkedCts.Token));

            // 200ms stagger between launches
            await Task.Delay(200, linkedCts.Token);
        }

        _logger.LogInformation("[MULTIBOT] All {Count} bots launched", _botCount);

        // Wait for all bots to complete (or cancellation)
        try
        {
            await Task.WhenAll(_botTasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[MULTIBOT] Shutdown requested");
        }

        _logger.LogInformation("[MULTIBOT] All bots stopped");
    }

    private async Task RunBotAsync(string accountName, int index, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("[MULTIBOT] Bot {Account} starting (index {Index})", accountName, index);

            // Each bot runs its own isolated tick loop
            // In full implementation, this creates a BotContext with:
            // - Shared Navigation.dll (P/Invoke is thread-safe)
            // - Own WoWClient connection
            // - Own WoWSharpObjectManager instance (requires P9.2)
            // - Own BotRunnerService tick loop

            // Placeholder tick loop — full implementation depends on P9.2 singleton removal
            while (!ct.IsCancellationRequested)
            {
                // Bot tick (~50ms interval = 20 ticks/sec)
                await Task.Delay(50, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MULTIBOT] Bot {Account} crashed", accountName);
        }
        finally
        {
            _logger.LogInformation("[MULTIBOT] Bot {Account} stopped", accountName);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _shutdownCts.Cancel();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _shutdownCts.Dispose();
        base.Dispose();
    }
}
