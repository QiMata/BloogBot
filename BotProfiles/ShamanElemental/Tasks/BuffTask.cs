using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace ShamanElemental.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            BotTasks.Pop();
        }
    }
}
