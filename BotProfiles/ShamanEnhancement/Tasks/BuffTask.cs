using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace ShamanEnhancement.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            BotTasks.Pop();
        }
    }
}
