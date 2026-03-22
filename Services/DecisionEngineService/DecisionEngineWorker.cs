using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DecisionEngineService
{
    /// <summary>
    /// Hosted worker for the decision engine. Currently logs lifecycle events.
    /// Full listener/prediction wiring requires configuration (port, SQLite path,
    /// training data directory) — see DES-MISS-002 in TASKS.md.
    /// </summary>
    public class DecisionEngineWorker(ILogger<DecisionEngineWorker> logger) : BackgroundService
    {
        private readonly ILogger<DecisionEngineWorker> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DecisionEngineWorker started. Prediction service is not yet wired (DES-MISS-002).");

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
            }

            _logger.LogInformation("DecisionEngineWorker stopped.");
        }
    }
}
