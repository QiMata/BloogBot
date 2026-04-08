using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;

namespace DecisionEngineService.Listeners
{
    public class CombatModelServiceListener(string ipAddress, int port, CombatPredictionService predictionService, ILogger logger)
        : ProtobufSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>(ipAddress, port, logger)
    {
        protected override WoWActivitySnapshot HandleRequest(WoWActivitySnapshot request)
        {
            try
            {
                var prediction = predictionService.PredictAction(request);
                logger.LogDebug("CombatPredictionService returned prediction for snapshot.");

                // Return the prediction result to the caller.
                return prediction;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "CombatPredictionService not ready — returning empty response.");
                return new WoWActivitySnapshot();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CombatPredictionService prediction failed — returning empty response.");
                return new WoWActivitySnapshot();
            }
        }
    }
}
