namespace BloogBot.AI.States;

/// <summary>
/// Centralized definitions of all valid minor states per BotActivity.
/// Ensures consistent naming and prevents ad-hoc state creation.
/// All minor states use verb-based naming for clarity.
/// </summary>
public static class MinorStateDefinitions
{
    /// <summary>Combat activity minor states.</summary>
    public static class Combat
    {
        public static readonly MinorState Approaching = new(BotActivity.Combat, "Approaching", "Moving toward target");
        public static readonly MinorState Engaging = new(BotActivity.Combat, "Engaging", "Active combat engagement");
        public static readonly MinorState Casting = new(BotActivity.Combat, "Casting", "Casting a spell or ability");
        public static readonly MinorState Looting = new(BotActivity.Combat, "Looting", "Looting defeated enemies");
        public static readonly MinorState Fleeing = new(BotActivity.Combat, "Fleeing", "Executing tactical retreat");
        public static readonly MinorState Recovering = new(BotActivity.Combat, "Recovering", "Recovering health/mana between pulls");
    }

    /// <summary>Questing activity minor states.</summary>
    public static class Questing
    {
        public static readonly MinorState Accepting = new(BotActivity.Questing, "Accepting", "Accepting a quest from NPC");
        public static readonly MinorState Navigating = new(BotActivity.Questing, "Navigating", "Traveling to quest objective");
        public static readonly MinorState Completing = new(BotActivity.Questing, "Completing", "Working on quest objective");
        public static readonly MinorState TurningIn = new(BotActivity.Questing, "TurningIn", "Turning in completed quest");
        public static readonly MinorState Reading = new(BotActivity.Questing, "Reading", "Reading quest text or objectives");
    }

    /// <summary>Grinding activity minor states.</summary>
    public static class Grinding
    {
        public static readonly MinorState Searching = new(BotActivity.Grinding, "Searching", "Searching for targets");
        public static readonly MinorState Pulling = new(BotActivity.Grinding, "Pulling", "Pulling a target");
        public static readonly MinorState Fighting = new(BotActivity.Grinding, "Fighting", "In combat with target");
        public static readonly MinorState Resting = new(BotActivity.Grinding, "Resting", "Resting between kills");
    }

    /// <summary>Professions activity minor states.</summary>
    public static class Professions
    {
        public static readonly MinorState Gathering = new(BotActivity.Professions, "Gathering", "Gathering resources");
        public static readonly MinorState Crafting = new(BotActivity.Professions, "Crafting", "Crafting items");
        public static readonly MinorState Training = new(BotActivity.Professions, "Training", "Learning new recipes or skills");
        public static readonly MinorState Searching = new(BotActivity.Professions, "Searching", "Searching for nodes or materials");
    }

    /// <summary>Talenting activity minor states.</summary>
    public static class Talenting
    {
        public static readonly MinorState Reviewing = new(BotActivity.Talenting, "Reviewing", "Reviewing available talents");
        public static readonly MinorState Assigning = new(BotActivity.Talenting, "Assigning", "Assigning talent points");
        public static readonly MinorState Confirming = new(BotActivity.Talenting, "Confirming", "Confirming talent selections");
    }

    /// <summary>Equipping activity minor states.</summary>
    public static class Equipping
    {
        public static readonly MinorState Comparing = new(BotActivity.Equipping, "Comparing", "Comparing equipment stats");
        public static readonly MinorState Swapping = new(BotActivity.Equipping, "Swapping", "Swapping equipment");
        public static readonly MinorState Organizing = new(BotActivity.Equipping, "Organizing", "Organizing inventory");
    }

    /// <summary>Trading activity minor states.</summary>
    public static class Trading
    {
        public static readonly MinorState Initiating = new(BotActivity.Trading, "Initiating", "Initiating trade with player");
        public static readonly MinorState Negotiating = new(BotActivity.Trading, "Negotiating", "Negotiating trade terms");
        public static readonly MinorState Confirming = new(BotActivity.Trading, "Confirming", "Confirming trade");
        public static readonly MinorState Completing = new(BotActivity.Trading, "Completing", "Completing trade transaction");
    }

    /// <summary>Guilding activity minor states.</summary>
    public static class Guilding
    {
        public static readonly MinorState Chatting = new(BotActivity.Guilding, "Chatting", "Chatting in guild");
        public static readonly MinorState Recruiting = new(BotActivity.Guilding, "Recruiting", "Recruiting new members");
        public static readonly MinorState Managing = new(BotActivity.Guilding, "Managing", "Managing guild settings");
    }

    /// <summary>Chatting activity minor states.</summary>
    public static class Chatting
    {
        public static readonly MinorState Reading = new(BotActivity.Chatting, "Reading", "Reading incoming messages");
        public static readonly MinorState Composing = new(BotActivity.Chatting, "Composing", "Composing a response");
        public static readonly MinorState Responding = new(BotActivity.Chatting, "Responding", "Sending response");
    }

    /// <summary>Helping activity minor states.</summary>
    public static class Helping
    {
        public static readonly MinorState Listening = new(BotActivity.Helping, "Listening", "Listening to request");
        public static readonly MinorState Assisting = new(BotActivity.Helping, "Assisting", "Providing assistance");
        public static readonly MinorState Following = new(BotActivity.Helping, "Following", "Following another player");
    }

    /// <summary>Mailing activity minor states.</summary>
    public static class Mailing
    {
        public static readonly MinorState Reading = new(BotActivity.Mailing, "Reading", "Reading mail");
        public static readonly MinorState Composing = new(BotActivity.Mailing, "Composing", "Composing mail");
        public static readonly MinorState Sending = new(BotActivity.Mailing, "Sending", "Sending mail");
        public static readonly MinorState Collecting = new(BotActivity.Mailing, "Collecting", "Collecting attachments");
    }

    /// <summary>Partying activity minor states.</summary>
    public static class Partying
    {
        public static readonly MinorState Joining = new(BotActivity.Partying, "Joining", "Joining a party");
        public static readonly MinorState Coordinating = new(BotActivity.Partying, "Coordinating", "Coordinating with party");
        public static readonly MinorState Following = new(BotActivity.Partying, "Following", "Following party leader");
        public static readonly MinorState Assisting = new(BotActivity.Partying, "Assisting", "Assisting party members");
    }

    /// <summary>RolePlaying activity minor states.</summary>
    public static class RolePlaying
    {
        public static readonly MinorState Conversing = new(BotActivity.RolePlaying, "Conversing", "In roleplay conversation");
        public static readonly MinorState Emoting = new(BotActivity.RolePlaying, "Emoting", "Performing emotes");
        public static readonly MinorState Acting = new(BotActivity.RolePlaying, "Acting", "Acting out a scene");
    }

    /// <summary>Battlegrounding activity minor states.</summary>
    public static class Battlegrounding
    {
        public static readonly MinorState Queuing = new(BotActivity.Battlegrounding, "Queuing", "In battleground queue");
        public static readonly MinorState Entering = new(BotActivity.Battlegrounding, "Entering", "Entering battleground");
        public static readonly MinorState Fighting = new(BotActivity.Battlegrounding, "Fighting", "Fighting in battleground");
        public static readonly MinorState Capturing = new(BotActivity.Battlegrounding, "Capturing", "Capturing objective");
        public static readonly MinorState Defending = new(BotActivity.Battlegrounding, "Defending", "Defending objective");
    }

    /// <summary>Dungeoning activity minor states.</summary>
    public static class Dungeoning
    {
        public static readonly MinorState Forming = new(BotActivity.Dungeoning, "Forming", "Forming dungeon group");
        public static readonly MinorState Traveling = new(BotActivity.Dungeoning, "Traveling", "Traveling to dungeon");
        public static readonly MinorState Clearing = new(BotActivity.Dungeoning, "Clearing", "Clearing trash mobs");
        public static readonly MinorState BossFighting = new(BotActivity.Dungeoning, "BossFighting", "Fighting boss");
    }

    /// <summary>Raiding activity minor states.</summary>
    public static class Raiding
    {
        public static readonly MinorState Preparing = new(BotActivity.Raiding, "Preparing", "Preparing for raid");
        public static readonly MinorState Buffing = new(BotActivity.Raiding, "Buffing", "Buffing raid members");
        public static readonly MinorState Pulling = new(BotActivity.Raiding, "Pulling", "Pulling boss or trash");
        public static readonly MinorState Executing = new(BotActivity.Raiding, "Executing", "Executing raid strategy");
    }

    /// <summary>WorldPvPing activity minor states.</summary>
    public static class WorldPvPing
    {
        public static readonly MinorState Hunting = new(BotActivity.WorldPvPing, "Hunting", "Hunting enemy players");
        public static readonly MinorState Engaging = new(BotActivity.WorldPvPing, "Engaging", "Engaging in PvP combat");
        public static readonly MinorState Escaping = new(BotActivity.WorldPvPing, "Escaping", "Escaping from PvP");
    }

    /// <summary>Camping activity minor states.</summary>
    public static class Camping
    {
        public static readonly MinorState Positioning = new(BotActivity.Camping, "Positioning", "Taking position");
        public static readonly MinorState Watching = new(BotActivity.Camping, "Watching", "Watching for target");
        public static readonly MinorState Waiting = new(BotActivity.Camping, "Waiting", "Waiting for spawn or player");
    }

    /// <summary>Auction activity minor states.</summary>
    public static class Auction
    {
        public static readonly MinorState Browsing = new(BotActivity.Auction, "Browsing", "Browsing auction listings");
        public static readonly MinorState Bidding = new(BotActivity.Auction, "Bidding", "Placing bids");
        public static readonly MinorState Listing = new(BotActivity.Auction, "Listing", "Listing items for sale");
        public static readonly MinorState Collecting = new(BotActivity.Auction, "Collecting", "Collecting sold items or gold");
    }

    /// <summary>Banking activity minor states.</summary>
    public static class Banking
    {
        public static readonly MinorState Depositing = new(BotActivity.Banking, "Depositing", "Depositing items");
        public static readonly MinorState Withdrawing = new(BotActivity.Banking, "Withdrawing", "Withdrawing items");
        public static readonly MinorState Organizing = new(BotActivity.Banking, "Organizing", "Organizing bank");
    }

    /// <summary>Vending activity minor states.</summary>
    public static class Vending
    {
        public static readonly MinorState Buying = new(BotActivity.Vending, "Buying", "Buying from vendor");
        public static readonly MinorState Selling = new(BotActivity.Vending, "Selling", "Selling to vendor");
        public static readonly MinorState Repairing = new(BotActivity.Vending, "Repairing", "Repairing equipment");
    }

    /// <summary>Exploring activity minor states.</summary>
    public static class Exploring
    {
        public static readonly MinorState Wandering = new(BotActivity.Exploring, "Wandering", "Wandering to discover areas");
        public static readonly MinorState Mapping = new(BotActivity.Exploring, "Mapping", "Mapping new territory");
        public static readonly MinorState Investigating = new(BotActivity.Exploring, "Investigating", "Investigating points of interest");
    }

    /// <summary>Traveling activity minor states.</summary>
    public static class Traveling
    {
        public static readonly MinorState Walking = new(BotActivity.Traveling, "Walking", "Walking to destination");
        public static readonly MinorState Mounting = new(BotActivity.Traveling, "Mounting", "Mounting up");
        public static readonly MinorState Flying = new(BotActivity.Traveling, "Flying", "Taking flight path");
        public static readonly MinorState Porting = new(BotActivity.Traveling, "Porting", "Using portal or hearthstone");
    }

    /// <summary>Escaping activity minor states.</summary>
    public static class Escaping
    {
        public static readonly MinorState Running = new(BotActivity.Escaping, "Running", "Running away");
        public static readonly MinorState Hiding = new(BotActivity.Escaping, "Hiding", "Finding cover");
        public static readonly MinorState Evading = new(BotActivity.Escaping, "Evading", "Evading pursuit");
    }

    /// <summary>Resting activity minor states.</summary>
    public static class Resting
    {
        public static readonly MinorState Sitting = new(BotActivity.Resting, "Sitting", "Sitting to rest");
        public static readonly MinorState Eating = new(BotActivity.Resting, "Eating", "Eating food");
        public static readonly MinorState Drinking = new(BotActivity.Resting, "Drinking", "Drinking water");
        public static readonly MinorState Regenerating = new(BotActivity.Resting, "Regenerating", "Regenerating health/mana");
        public static readonly MinorState AFK = new(BotActivity.Resting, "AFK", "Away from keyboard");
    }

    /// <summary>Eventing activity minor states.</summary>
    public static class Eventing
    {
        public static readonly MinorState Participating = new(BotActivity.Eventing, "Participating", "Participating in event");
        public static readonly MinorState Collecting = new(BotActivity.Eventing, "Collecting", "Collecting event items");
        public static readonly MinorState Celebrating = new(BotActivity.Eventing, "Celebrating", "Celebrating with others");
    }

    private static readonly Dictionary<BotActivity, IReadOnlyList<MinorState>> _allStates = BuildRegistry();

    private static Dictionary<BotActivity, IReadOnlyList<MinorState>> BuildRegistry()
    {
        return new Dictionary<BotActivity, IReadOnlyList<MinorState>>
        {
            [BotActivity.Combat] = new[] { Combat.Approaching, Combat.Engaging, Combat.Casting, Combat.Looting, Combat.Fleeing, Combat.Recovering },
            [BotActivity.Questing] = new[] { Questing.Accepting, Questing.Navigating, Questing.Completing, Questing.TurningIn, Questing.Reading },
            [BotActivity.Grinding] = new[] { Grinding.Searching, Grinding.Pulling, Grinding.Fighting, Grinding.Resting },
            [BotActivity.Professions] = new[] { Professions.Gathering, Professions.Crafting, Professions.Training, Professions.Searching },
            [BotActivity.Talenting] = new[] { Talenting.Reviewing, Talenting.Assigning, Talenting.Confirming },
            [BotActivity.Equipping] = new[] { Equipping.Comparing, Equipping.Swapping, Equipping.Organizing },
            [BotActivity.Trading] = new[] { Trading.Initiating, Trading.Negotiating, Trading.Confirming, Trading.Completing },
            [BotActivity.Guilding] = new[] { Guilding.Chatting, Guilding.Recruiting, Guilding.Managing },
            [BotActivity.Chatting] = new[] { Chatting.Reading, Chatting.Composing, Chatting.Responding },
            [BotActivity.Helping] = new[] { Helping.Listening, Helping.Assisting, Helping.Following },
            [BotActivity.Mailing] = new[] { Mailing.Reading, Mailing.Composing, Mailing.Sending, Mailing.Collecting },
            [BotActivity.Partying] = new[] { Partying.Joining, Partying.Coordinating, Partying.Following, Partying.Assisting },
            [BotActivity.RolePlaying] = new[] { RolePlaying.Conversing, RolePlaying.Emoting, RolePlaying.Acting },
            [BotActivity.Battlegrounding] = new[] { Battlegrounding.Queuing, Battlegrounding.Entering, Battlegrounding.Fighting, Battlegrounding.Capturing, Battlegrounding.Defending },
            [BotActivity.Dungeoning] = new[] { Dungeoning.Forming, Dungeoning.Traveling, Dungeoning.Clearing, Dungeoning.BossFighting },
            [BotActivity.Raiding] = new[] { Raiding.Preparing, Raiding.Buffing, Raiding.Pulling, Raiding.Executing },
            [BotActivity.WorldPvPing] = new[] { WorldPvPing.Hunting, WorldPvPing.Engaging, WorldPvPing.Escaping },
            [BotActivity.Camping] = new[] { Camping.Positioning, Camping.Watching, Camping.Waiting },
            [BotActivity.Auction] = new[] { Auction.Browsing, Auction.Bidding, Auction.Listing, Auction.Collecting },
            [BotActivity.Banking] = new[] { Banking.Depositing, Banking.Withdrawing, Banking.Organizing },
            [BotActivity.Vending] = new[] { Vending.Buying, Vending.Selling, Vending.Repairing },
            [BotActivity.Exploring] = new[] { Exploring.Wandering, Exploring.Mapping, Exploring.Investigating },
            [BotActivity.Traveling] = new[] { Traveling.Walking, Traveling.Mounting, Traveling.Flying, Traveling.Porting },
            [BotActivity.Escaping] = new[] { Escaping.Running, Escaping.Hiding, Escaping.Evading },
            [BotActivity.Resting] = new[] { Resting.Sitting, Resting.Eating, Resting.Drinking, Resting.Regenerating, Resting.AFK },
            [BotActivity.Eventing] = new[] { Eventing.Participating, Eventing.Collecting, Eventing.Celebrating }
        };
    }

    /// <summary>
    /// Gets all valid minor states for the specified activity.
    /// </summary>
    public static IReadOnlyList<MinorState> ForActivity(BotActivity activity) =>
        _allStates.TryGetValue(activity, out var states) ? states : Array.Empty<MinorState>();

    /// <summary>
    /// Validates that a minor state is valid for the given activity.
    /// </summary>
    public static bool IsValidForActivity(MinorState minorState, BotActivity activity) =>
        minorState.ParentActivity == activity && ForActivity(activity).Contains(minorState);

    /// <summary>
    /// Gets all registered activities with their minor states.
    /// </summary>
    public static IReadOnlyDictionary<BotActivity, IReadOnlyList<MinorState>> GetAllDefinitions() =>
        _allStates;
}
