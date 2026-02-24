using BotRunner.Interfaces;
using Serilog;
using System;
using System.Threading;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: skin a corpse by GUID using ObjectManager.LootTargetAsync.
/// Maps to ActionType.SKIN_CORPSE from StateManager.
/// </summary>
public class SkinCorpseTask(IBotContext botContext, ulong corpseGuid) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        try
        {
            ObjectManager.LootTargetAsync(corpseGuid, CancellationToken.None)
                .GetAwaiter().GetResult();
            Log.Information("[SKIN] Skinned {Guid:X}", corpseGuid);
        }
        catch (Exception ex)
        {
            Log.Warning("[SKIN] Skin {Guid:X} failed: {Error}", corpseGuid, ex.Message);
        }

        BotTasks.Pop();
    }
}
