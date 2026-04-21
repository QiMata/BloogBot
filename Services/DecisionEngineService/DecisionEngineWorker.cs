using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DecisionEngineService
{
    /// <summary>
    /// Hosted worker for the decision engine runtime.
    /// </summary>
    public class DecisionEngineWorker(
        IConfiguration configuration,
        ILogger<DecisionEngineWorker> logger,
        ILoggerFactory loggerFactory) : BackgroundService
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<DecisionEngineWorker> _logger = logger;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = DecisionEngineRuntimeOptions.FromConfiguration(_configuration);
            if (!options.Enabled)
            {
                _logger.LogInformation("[DecisionEngine] Runtime disabled by configuration.");
                await WaitUntilStopped(stoppingToken);
                return;
            }

            using var runtime = new DecisionEngineRuntime(options, _loggerFactory);
            _logger.LogInformation(
                "[DecisionEngine] Runtime started. Listening at {IpAddress}:{Port}; data={DataDirectory}; processed={ProcessedDirectory}",
                options.ListenerIpAddress,
                options.ListenerPort,
                options.DataDirectory,
                options.ProcessedDirectory);

            await WaitUntilStopped(stoppingToken);

            _logger.LogInformation("[DecisionEngine] Runtime stopped.");
        }

        private static async Task WaitUntilStopped(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }
}
