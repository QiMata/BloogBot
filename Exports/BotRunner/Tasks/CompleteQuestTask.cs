using BotRunner.Interfaces;
using Serilog;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: complete (turn in) a quest from an open quest dialog.
/// Optionally selects a reward by index before completing.
/// Maps to ActionType.COMPLETE_QUEST from StateManager.
/// </summary>
public class CompleteQuestTask(IBotContext botContext, int rewardIndex = 0) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        var questFrame = ObjectManager.QuestFrame;
        if (questFrame == null)
        {
            Log.Warning("[COMPLETE_QUEST] No quest frame open");
            BotTasks.Pop();
            return;
        }

        questFrame.CompleteQuest(rewardIndex);
        Log.Information("[COMPLETE_QUEST] Completed quest (reward index {Index})", rewardIndex);
        BotTasks.Pop();
    }
}
