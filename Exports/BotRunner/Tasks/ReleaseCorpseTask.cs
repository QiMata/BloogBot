using BotRunner.Interfaces;
using Serilog; // TODO: migrate to ILogger when DI is available

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
        Log.Information("[RELEASE] Released spirit");
        PopTask("ReleaseSent");
    }
}
