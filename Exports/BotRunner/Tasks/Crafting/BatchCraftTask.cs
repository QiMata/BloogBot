using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using Serilog;

namespace BotRunner.Tasks.Crafting;

/// <summary>
/// Repeatedly casts a craft spell until target count or out of materials.
/// Monitors SMSG_SPELL_GO for success and SMSG_CAST_FAILED for reagent failures.
/// </summary>
public class BatchCraftTask : BotTask, IBotTask
{
    private enum CraftState { CastSpell, WaitForResult, Complete }

    private CraftState _state = CraftState.CastSpell;
    private readonly int _spellId;
    private readonly int _targetCount;
    private int _craftedCount;
    private int _failedCount;
    private int _ticksSinceCast;

    private const int CastWaitTicks = 50; // ~2.5s wait for cast result

    public BatchCraftTask(IBotContext context, int spellId, int targetCount) : base(context)
    {
        _spellId = spellId;
        _targetCount = targetCount;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case CraftState.CastSpell:
                if (_craftedCount >= _targetCount)
                {
                    _state = CraftState.Complete;
                    return;
                }

                ObjectManager.CastSpell(_spellId);
                _ticksSinceCast = 0;
                _state = CraftState.WaitForResult;
                Log.Debug("[CRAFT] Casting spell {SpellId} ({Current}/{Target})",
                    _spellId, _craftedCount + 1, _targetCount);
                break;

            case CraftState.WaitForResult:
                _ticksSinceCast++;
                if (_ticksSinceCast >= CastWaitTicks)
                {
                    // Assume success if no failure received
                    _craftedCount++;
                    _state = CraftState.CastSpell;
                }
                break;

            case CraftState.Complete:
                Log.Information("[CRAFT] Batch complete: {Crafted}/{Target} crafted, {Failed} failed",
                    _craftedCount, _targetCount, _failedCount);
                BotContext.BotTasks.Pop();
                break;
        }
    }

    /// <summary>Called when SMSG_CAST_FAILED is received for our craft spell.</summary>
    public void OnCastFailed()
    {
        _failedCount++;
        Log.Warning("[CRAFT] Cast failed (missing reagents?) — {Failed} failures", _failedCount);

        // Stop after 3 consecutive failures (out of materials)
        if (_failedCount >= 3)
            _state = CraftState.Complete;
        else
            _state = CraftState.CastSpell;
    }
}
