using BotRunner.Helpers;
using BotRunner.Interfaces;
using Microsoft.Extensions.Logging;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: cast a spell by ID on a target GUID.
/// Sets the target, casts the spell, then pops.
/// Maps to ActionType.CAST_SPELL from StateManager.
/// </summary>
public class CastSpellTask(IBotContext botContext, int spellId, ulong targetGuid = 0, bool castOnSelf = false) : BotTask(botContext), IBotTask
{
    private bool _castInitiated;

    public void Update()
    {
        if (!_castInitiated)
        {
            if (MountUsageGuard.TryGetBlockedReasonForSpell(ObjectManager, spellId, out var blockReason))
            {
                Logger.LogInformation("[CAST_SPELL] Skipped mount spell {SpellId}: {Reason}", spellId, blockReason);
                BotContext.AddDiagnosticMessage($"[MOUNT-BLOCK] spell={spellId} {blockReason}");
                BotTasks.Pop();
                return;
            }

            if (targetGuid != 0)
                ObjectManager.SetTarget(targetGuid);

            ObjectManager.CastSpell(spellId, castOnSelf: castOnSelf);
            _castInitiated = true;
            Logger.LogInformation("[CAST_SPELL] Cast spell {SpellId} on {Target:X}", spellId, targetGuid);
            BotTasks.Pop();
            return;
        }

        BotTasks.Pop();
    }
}
