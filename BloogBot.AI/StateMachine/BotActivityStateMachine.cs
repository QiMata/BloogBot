using System.Collections.Generic;
using System.Linq;
using BloogBot.AI.States;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Stateless;

namespace BloogBot.AI.StateMachine;

public sealed class BotActivityStateMachine
{
    private static readonly ActivityConfiguration[] ActivityConfigurations =
    [
        new(BotActivity.Resting, null, new[] { Trigger.HealthRestored }),
        new(BotActivity.Questing, "Starting quest", new[] { Trigger.QuestComplete, Trigger.QuestFailed }),
        new(BotActivity.Grinding, "Starting grind", new[] { Trigger.ProfessionLevelUp }),
        new(BotActivity.Professions, "Starting professions", new[] { Trigger.ProfessionLevelUp }),
        new(BotActivity.Talenting, "Starting talent management", new[] { Trigger.TalentPointsAttributed }),
        new(BotActivity.Equipping, "Starting equipment management", new[] { Trigger.EquipmentChanged }),
        new(BotActivity.Trading, "Starting trading", new[] { Trigger.TradeComplete }),
        new(BotActivity.Guilding, "Starting guild activities", new[] { Trigger.GuildingEnded }),
        new(BotActivity.Chatting, "Starting chat interaction", new[] { Trigger.ChattingEnded }),
        new(BotActivity.Helping, "Starting helping activity", new[] { Trigger.HelpingEnded }),
        new(BotActivity.Mailing, "Starting mail management", new[] { Trigger.MailingEnded }),
        new(BotActivity.Partying, "Starting party activities", new[] { Trigger.PartyEnded }),
        new(BotActivity.RolePlaying, "Starting roleplay", new[] { Trigger.RolePlayEnded }),
        new(BotActivity.Combat, "Starting combat", new[] { Trigger.CombatEnded }),
        new(BotActivity.Battlegrounding, "Starting battleground", new[] { Trigger.BattlegroundEnded }),
        new(BotActivity.Dungeoning, "Starting dungeon", new[] { Trigger.DungeonEnded }),
        new(BotActivity.Raiding, "Starting raid", new[] { Trigger.RaidEnded }),
        new(BotActivity.WorldPvPing, "Starting world PvP", new[] { Trigger.PvPEnded }),
        new(BotActivity.Camping, "Starting camping", new[] { Trigger.CampingEnded }),
        new(BotActivity.Auction, "Starting auction house", new[] { Trigger.AuctionEnded }),
        new(BotActivity.Banking, "Starting banking", new[] { Trigger.BankingEnded }),
        new(BotActivity.Vending, "Starting vending", new[] { Trigger.VendingEnded }),
        new(BotActivity.Exploring, "Starting exploration", new[] { Trigger.ExploringEnded }),
        new(BotActivity.Traveling, "Starting travel", new[] { Trigger.TravelEnded }),
        new(BotActivity.Escaping, "Starting escape", new[] { Trigger.EscapeSucceeded, Trigger.EscapeFailed }),
        new(BotActivity.Eventing, "Starting event", new[] { Trigger.EventEnded })
    ];

    private readonly StateMachine<BotActivity, Trigger> _sm;
    public BotActivity Current => _sm.State;
    public IObjectManager ObjectManager { get; private set; }
    private ILogger Logger;

    public BotActivityStateMachine(
        ILoggerFactory loggerFactory,
        IObjectManager objectManager,
        BotActivity initial = BotActivity.Resting)
    {
        Logger = loggerFactory.CreateLogger<BotActivityStateMachine>();
        ObjectManager = objectManager;
        _sm = new(initial);

        ConfigureGlobalTransitions();
        ConfigureActivities();
        DecideNextActiveState();
    }

    void ConfigureGlobalTransitions()
    {
        foreach (BotActivity activity in Enum.GetValues(typeof(BotActivity)))
        {
            _sm.Configure(activity)
                .PermitDynamic(Trigger.PartyInvite, DecideNextActiveState)
                .PermitDynamic(Trigger.GuildInvite, DecideNextActiveState)
                .PermitDynamic(Trigger.LowHealth, DecideNextActiveState)
                .PermitDynamic(Trigger.TalentPointsAvailable, DecideNextActiveState)
                .PermitDynamic(Trigger.TradeRequested, DecideNextActiveState)
                .PermitDynamic(Trigger.ChatMessageReceived, DecideNextActiveState)
                .PermitDynamic(Trigger.HelpRequested, DecideNextActiveState)
                .PermitDynamic(Trigger.MailReceived, DecideNextActiveState)
                .PermitDynamic(Trigger.RolePlayEngaged, DecideNextActiveState)
                .PermitDynamic(Trigger.CombatStarted, DecideNextActiveState)
                .PermitDynamic(Trigger.BattlegroundStarted, DecideNextActiveState)
                .PermitDynamic(Trigger.DungeonStarted, DecideNextActiveState)
                .PermitDynamic(Trigger.RaidStarted, DecideNextActiveState)
                .PermitDynamic(Trigger.PvPEngaged, DecideNextActiveState)
                .PermitDynamic(Trigger.CampingStarted, DecideNextActiveState)
                .PermitDynamic(Trigger.AuctionStarted, DecideNextActiveState)
                .PermitDynamic(Trigger.BankingNeeded, DecideNextActiveState)
                .PermitDynamic(Trigger.VendorNeeded, DecideNextActiveState)
                .PermitDynamic(Trigger.ExplorationStarted, DecideNextActiveState)
                .PermitDynamic(Trigger.TravelRequired, DecideNextActiveState)
                .PermitDynamic(Trigger.EscapeRequired, DecideNextActiveState)
                .PermitDynamic(Trigger.EventStarted, DecideNextActiveState);
        }
    }

    void ConfigureActivities()
    {
        foreach (var configuration in ActivityConfigurations)
        {
            var state = _sm.Configure(configuration.Activity);

            if (!string.IsNullOrWhiteSpace(configuration.EntryLogMessage))
            {
                state.OnEntry(_ => Logger.LogInformation(configuration.EntryLogMessage!));
            }

            foreach (var trigger in configuration.ExitTriggers)
            {
                state.PermitDynamic(trigger, DecideNextActiveState);
            }
        }
    }

    BotActivity DecideNextActiveState()
    {
        if (ObjectManager == null || !ObjectManager.HasEnteredWorld)
        {
            Logger.LogInformation("No active world session. Defaulting to Resting.");
            return BotActivity.Resting;
        }

        var player = ObjectManager.Player;
        if (player == null)
        {
            Logger.LogInformation("Player information unavailable. Defaulting to Resting.");
            return BotActivity.Resting;
        }

        if (player.InGhostForm || player.HealthPercent < 40)
        {
            Logger.LogInformation("Player requires recovery. Switching to Resting state.");
            return BotActivity.Resting;
        }

        if (ObjectManager.Aggressors.Any(a => a.TargetGuid == player.Guid))
        {
            Logger.LogInformation("Hostile units detected. Switching to Combat state.");
            return BotActivity.Combat;
        }

        if (player.InBattleground)
        {
            Logger.LogInformation("Player is in a battleground. Switching to Battlegrounding state.");
            return BotActivity.Battlegrounding;
        }

        if (ObjectManager.TradeFrame?.IsOpen ?? false)
        {
            Logger.LogInformation("Trade frame open. Switching to Trading state.");
            return BotActivity.Trading;
        }

        if (ObjectManager.MerchantFrame?.IsOpen ?? false)
        {
            Logger.LogInformation("Merchant interaction detected. Switching to Vending state.");
            return BotActivity.Vending;
        }

        if (ObjectManager.TalentFrame?.IsOpen ?? false)
        {
            Logger.LogInformation("Talent frame open. Switching to Talenting state.");
            return BotActivity.Talenting;
        }

        if ((ObjectManager.TrainerFrame?.IsOpen ?? false) || (ObjectManager.CraftFrame?.IsOpen ?? false))
        {
            Logger.LogInformation("Trainer or crafting interaction detected. Switching to Professions state.");
            return BotActivity.Professions;
        }

        if (ObjectManager.TaxiFrame?.IsOpen ?? false)
        {
            Logger.LogInformation("Taxi interface open. Switching to Traveling state.");
            return BotActivity.Traveling;
        }

        var questGreeting = ObjectManager.QuestGreetingFrame;
        if ((ObjectManager.QuestFrame?.IsOpen ?? false) || (questGreeting?.Quests?.Count > 0) || player.HasQuestTargets)
        {
            Logger.LogInformation("Quest context detected. Switching to Questing state.");
            return BotActivity.Questing;
        }

        if (ObjectManager.GossipFrame?.IsOpen ?? false)
        {
            Logger.LogInformation("Gossip interaction detected. Switching to RolePlaying state.");
            return BotActivity.RolePlaying;
        }

        var lootFrame = ObjectManager.LootFrame;
        if (lootFrame != null && (lootFrame.IsOpen || lootFrame.LootCount > 0))
        {
            Logger.LogInformation("Loot available. Remaining in Combat context for cleanup.");
            return BotActivity.Combat;
        }

        if (ObjectManager.PartyMembers.Skip(1).Any())
        {
            Logger.LogInformation("Party detected. Switching to Partying state.");
            return BotActivity.Partying;
        }

        Logger.LogInformation("Defaulting to Grinding for progression.");
        return BotActivity.Grinding;
    }

    private sealed record ActivityConfiguration(BotActivity Activity, string? EntryLogMessage, IReadOnlyList<Trigger> ExitTriggers);
}
