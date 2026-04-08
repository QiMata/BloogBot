using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace DecisionEngineService
{
    /// <summary>
    /// Hosted worker for the decision engine. Maintains service lifetime while
    /// CombatPredictionService handles on-demand predictions via the socket listener.
    /// </summary>
    public class DecisionEngineWorker(ILogger<DecisionEngineWorker> logger) : BackgroundService
    {
        private readonly ILogger<DecisionEngineWorker> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[DecisionEngine] Service started — CombatPredictionService available for on-demand predictions");

            // CombatPredictionService handles predictions on-demand via the socket listener.
            // This worker maintains the service lifetime.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
