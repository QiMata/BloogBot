using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace WarriorArms.Tasks
{
    public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            BotTasks.Pop();
        }
    }
}
