using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace RogueAssassin.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            BotTasks.Pop();
        }
    }
}
