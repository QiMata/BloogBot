using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace HunterSurvival.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            BotTasks.Pop();
        }
    }
}
