using BotRunner.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: loot a corpse by GUID using ObjectManager.LootTargetAsync.
/// Maps to ActionType.LOOT_CORPSE from StateManager.
/// </summary>
public class LootCorpseTask(IBotContext botContext, ulong corpseGuid) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        try
        {
            ObjectManager.LootTargetAsync(corpseGuid, CancellationToken.None)
                .GetAwaiter().GetResult();
            Logger.LogInformation("[LOOT] Looted {Guid:X}", corpseGuid);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[LOOT] Loot {Guid:X} failed: {Error}", corpseGuid, ex.Message);
        }

        BotTasks.Pop();
    }
}
