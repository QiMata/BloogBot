using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;
using System;

namespace DecisionEngineService.Listeners
{
    public class CombatModelServiceListener : ProtobufSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>
    {
        private readonly CombatPredictionService _predictionService;

        public CombatModelServiceListener(
            string ipAddress,
            int port,
            CombatPredictionService predictionService,
            ILogger logger)
            : base(ipAddress, port, logger)
        {
            _predictionService = predictionService ?? throw new ArgumentNullException(nameof(predictionService));
        }

        protected override WoWActivitySnapshot HandleRequest(WoWActivitySnapshot request)
        {
            try
            {
                var prediction = _predictionService.PredictAction(request);
                _logger.LogDebug("CombatPredictionService returned prediction for snapshot.");

                return prediction;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "CombatPredictionService not ready - returning empty response.");
                return new WoWActivitySnapshot();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CombatPredictionService prediction failed - returning empty response.");
                return new WoWActivitySnapshot();
            }
        }
    }
}
