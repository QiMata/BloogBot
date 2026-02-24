using BotRunner.Interfaces;
using Serilog;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: train a specific spell/skill from an open trainer window.
/// The trainer dialog must already be open.
/// Maps to ActionType.TRAIN_SKILL from StateManager.
/// </summary>
public class TrainSpellTask(IBotContext botContext, int spellIndex) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        var trainerFrame = ObjectManager.TrainerFrame;
        if (trainerFrame == null)
        {
            Log.Warning("[TRAIN_SPELL] No trainer frame open");
            BotTasks.Pop();
            return;
        }

        trainerFrame.TrainSpell(spellIndex);
        Log.Information("[TRAIN_SPELL] Trained spell at index {Index}", spellIndex);
        BotTasks.Pop();
    }
}
