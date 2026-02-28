using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;

namespace DecisionEngineService.Listeners
{
    public class CombatModelServiceListener(string ipAddress, int port, ILogger logger)
        : ProtobufSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>(ipAddress, port, logger)
    {
        protected override WoWActivitySnapshot HandleRequest(WoWActivitySnapshot request)
        {
            try
            {
                var actions = DecisionEngine.GetNextActions(request);
                logger.LogDebug(
                    "DecisionEngine predicted {ActionCount} actions for snapshot.",
                    actions.Count);

                // Return the input snapshot — callers use it for round-trip acknowledgment.
                // Action results are consumed by the training pipeline, not returned inline.
                return request;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DecisionEngine prediction failed — returning empty response.");
                return new WoWActivitySnapshot();
            }
        }
    }
}
