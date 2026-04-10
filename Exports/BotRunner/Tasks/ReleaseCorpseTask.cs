using BotRunner.Interfaces;
using Microsoft.Extensions.Logging;

namespace BotRunner.Tasks;

/// <summary>
/// Atomic task: release spirit (ghost form) after dying.
/// Calls ObjectManager.ReleaseSpirit() and pops immediately.
/// Maps to ActionType.RELEASE_CORPSE from StateManager.
/// </summary>
public class ReleaseCorpseTask(IBotContext botContext) : BotTask(botContext), IBotTask
{
    public void Update()
    {
        ObjectManager.ReleaseSpirit();
        Logger.LogInformation("[RELEASE] Released spirit");
        PopTask("ReleaseSent");
    }
}
