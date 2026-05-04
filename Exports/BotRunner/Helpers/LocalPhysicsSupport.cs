using GameData.Core.Interfaces;
using System;
using WoWSharpClient;

namespace BotRunner.Helpers;

internal static class LocalPhysicsSupport
{
    public static bool SupportsReliableQueries(IObjectManager? objectManager)
    {
        if (objectManager == null)
            return true;

        var objectManagerNamespace = objectManager.GetType().Namespace;
        if (objectManagerNamespace?.StartsWith("ForegroundBotRunner", StringComparison.Ordinal) == true
            || objectManagerNamespace?.StartsWith("Castle.Proxies", StringComparison.Ordinal) == true)
            return false;

        if (objectManager is WoWSharpObjectManager wowSharpObjectManager)
            return wowSharpObjectManager.SupportsNativeLocalPhysicsQueries;

        return true;
    }
}
