using BloogBot.AI.States;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Stateless;
using static BloogBot.AI.HeuristicPicker;

namespace BloogBot.AI.StateMachine;

public sealed class BotActivityStateMachine
{
    private readonly StateMachine<BotActivity, Trigger> _sm;
    public BotActivity Current => _sm.State;

    public void Fire(Trigger trigger) => _sm.Fire(trigger);

    private readonly ILogger<BotActivityStateMachine> _logger;
    private readonly IObjectManager _obj;

    public BotActivityStateMachine(
        ILoggerFactory loggerFactory,
        IObjectManager obj,
        BotActivity initial = BotActivity.Resting
    )
    {
        _logger = loggerFactory.CreateLogger<BotActivityStateMachine>();
        _sm = new(initial);
        _obj = obj;
        ConfigureResting();
        ConfigureQuesting();
        // … repeat for each activity
    }

    void ConfigureResting() =>
        _sm.Configure(BotActivity.Resting)
            .PermitDynamic(Trigger.HealthRestored, () => PickNext(_obj));

    void ConfigureQuesting() =>
        _sm.Configure(BotActivity.Questing)
            .OnEntry(ctx => _logger.LogInformation("Starting quest"))
            .Permit(Trigger.LowHealth, BotActivity.Resting);

    // Add more configurations for other activities as needed
}
