using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace HunterSurvival.Tasks
{
    public class SummonPetTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            BotTasks.Pop();
        }
    }
}
