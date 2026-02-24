using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace RogueSubtlety.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            BotTasks.Pop();
        }
    }
}
