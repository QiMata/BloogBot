using BotRunner.Interfaces;
using Serilog;

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
            if (targetGuid != 0)
                ObjectManager.SetTarget(targetGuid);

            ObjectManager.CastSpell(spellId, castOnSelf: castOnSelf);
            _castInitiated = true;
            Log.Information("[CAST_SPELL] Cast spell {SpellId} on {Target:X}", spellId, targetGuid);
            BotTasks.Pop();
            return;
        }

        BotTasks.Pop();
    }
}
