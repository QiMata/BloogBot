using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace HunterBeastMastery.Tasks
{
    public class SummonPetTask(IBotContext botContext) : BotTask(botContext), IBotTask
    {
        public void Update()
        {
            BotTasks.Pop();
        }
    }
}
