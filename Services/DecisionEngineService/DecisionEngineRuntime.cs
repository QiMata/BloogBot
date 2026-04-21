using DecisionEngineService.Listeners;
using Microsoft.Extensions.Logging;
using System;

namespace DecisionEngineService
{
    internal sealed class DecisionEngineRuntime : IDisposable
    {
        private readonly CombatModelServiceListener _listener;
        private readonly CombatPredictionService _predictionService;
        private bool _disposed;

        public DecisionEngineRuntime(DecisionEngineRuntimeOptions options, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            DecisionEngineRuntimeInitializer.EnsureRuntimeReady(options);

            CombatPredictionService? predictionService = null;
            try
            {
                predictionService = new CombatPredictionService(
                    options.SqliteConnection,
                    options.DataDirectory,
                    options.ProcessedDirectory,
                    loggerFactory.CreateLogger<CombatPredictionService>());

                _listener = new CombatModelServiceListener(
                    options.ListenerIpAddress,
                    options.ListenerPort,
                    predictionService,
                    loggerFactory.CreateLogger<CombatModelServiceListener>());
                _predictionService = predictionService;
            }
            catch
            {
                predictionService?.Dispose();
                throw;
            }
        }

        internal CombatPredictionService PredictionService => _predictionService;
        internal CombatModelServiceListener Listener => _listener;

        public void Dispose()
        {
            if (_disposed)
                return;

            _listener.Dispose();
            _predictionService.Dispose();
            _disposed = true;
        }
    }
}
