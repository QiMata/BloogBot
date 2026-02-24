using BotRunner.Interfaces;
using Serilog;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: accept a quest from an open quest dialog.
/// Requires that a quest accept dialog is already showing.
/// Maps to ActionType.ACCEPT_QUEST from StateManager.
/// </summary>
public class AcceptQuestTask(IBotContext botContext) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        var questFrame = ObjectManager.QuestFrame;
        if (questFrame == null)
        {
            Log.Warning("[ACCEPT_QUEST] No quest frame open");
            BotTasks.Pop();
            return;
        }

        questFrame.AcceptQuest();
        Log.Information("[ACCEPT_QUEST] Accepted quest");
        BotTasks.Pop();
    }
}
