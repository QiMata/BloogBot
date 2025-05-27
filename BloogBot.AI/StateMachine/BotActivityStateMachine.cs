using BloogBot.AI.States;
using Microsoft.Extensions.Logging;
using Stateless;

namespace BloogBot.AI.StateMachine;

public sealed class BotActivityStateMachine
{
    private enum Trigger
    {
        HealthRestored,
        LowHealth
    }
    
    private readonly StateMachine<BotActivity, Trigger> _sm;
    public BotActivity Current => _sm.State;
    private readonly ILogger<BotActivityStateMachine> _logger;

    public BotActivityStateMachine(ILoggerFactory loggerFactory, Player player, BotActivity initial = BotActivity.Resting)
    {
        _logger = loggerFactory.CreateLogger<BotActivityStateMachine>();
        Player = player;
        _sm = new(initial);

        ConfigureResting();
        ConfigureQuesting();
        // … repeat for each activity
    }

    void ConfigureResting()
        => _sm.Configure(BotActivity.Resting)
            .PermitDynamic(Trigger.HealthRestored, DecideNextActiveState);

    void ConfigureQuesting()
        => _sm.Configure(BotActivity.Questing)
            .OnEntry(ctx => _logger.LogInformation("Starting quest"))
            .Permit(Trigger.LowHealth, BotActivity.Resting);

    
    // Add more configurations for other activities as needed
    
    BotActivity DecideNextActiveState() =>
        Player.InBattleground ? BotActivity.Battlegrounding : HasQuestTargets ? BotActivity.Questing : BotActivity.Grinding;
    
    
    public bool HasQuestTargets { get; set; }
    public Player Player { get; }


}

public class Player
{
    public bool InBattleground { get; set; }
    // Other player properties and methods
}