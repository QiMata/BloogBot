using System;
using GameData.Core.Models.Activities;

namespace WoWStateManager.Activities
{
    internal static partial class ActivityCatalogRows
    {
        // ---- Shard 5: _catalog_rows/05_misc.md ----

        public static ActivityDefinition ProfMiningRoute { get; } = new ActivityDefinition
        {
            Id = "prof.mining-route",
            Family = ActivityFamily.ProfessionGathering,
            Activity = "Profession farming",
            Location = "Mining route",
            LevelRange = new LevelRange(1, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 1,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 1633.0f, Y: -4439.0f, Z: 38.0f, NamedLocation: "Mining route"),
            ExpectedDuration = TimeSpan.FromMinutes(60),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Observer,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(15)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["profession", "gathering", "mining"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 30000, Max: 75000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Gathering",
        };

        public static ActivityDefinition ProfHerbalismRoute { get; } = new ActivityDefinition
        {
            Id = "prof.herbalism-route",
            Family = ActivityFamily.ProfessionGathering,
            Activity = "Profession farming",
            Location = "Herbalism route",
            LevelRange = new LevelRange(1, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 1,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 1633.0f, Y: -4439.0f, Z: 38.0f, NamedLocation: "Herbalism route"),
            ExpectedDuration = TimeSpan.FromMinutes(60),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Observer,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(15)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["profession", "gathering", "herbalism"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 30000, Max: 75000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Gathering",
        };

        public static ActivityDefinition ProfSkinningRoute { get; } = new ActivityDefinition
        {
            Id = "prof.skinning-route",
            Family = ActivityFamily.ProfessionGathering,
            Activity = "Profession farming",
            Location = "Skinning route",
            LevelRange = new LevelRange(1, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 1,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 1633.0f, Y: -4439.0f, Z: 38.0f, NamedLocation: "Skinning route"),
            ExpectedDuration = TimeSpan.FromMinutes(60),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Observer,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(15)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["profession", "gathering", "skinning"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 20000, Max: 45000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Gathering",
        };

        public static ActivityDefinition ProfCityTrainerLoop { get; } = new ActivityDefinition
        {
            Id = "prof.city-trainer-loop",
            Family = ActivityFamily.ProfessionLeveling,
            Activity = "Profession leveling",
            Location = "City trainer + recipe loop",
            LevelRange = new LevelRange(5, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 1,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 1633.0f, Y: -4439.0f, Z: 38.0f, NamedLocation: "City trainer + recipe loop"),
            ExpectedDuration = TimeSpan.FromMinutes(20),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Observer,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["profession", "leveling", "trainer"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 0, Max: 0, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Crafting",
        };

        public static ActivityDefinition EconAhRestock { get; } = new ActivityDefinition
        {
            Id = "econ.ah-restock",
            Family = ActivityFamily.Economy,
            Activity = "Economy",
            Location = "Auction house restock",
            LevelRange = new LevelRange(1, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 1,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 1671.0f, Y: -4346.0f, Z: 60.0f, NamedLocation: "Auction house restock"),
            ExpectedDuration = TimeSpan.FromMinutes(20),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Observer,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(15)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["economy", "auction-house", "restock"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 10000, Max: 200000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Economy",
        };

        public static ActivityDefinition EconVendorLoop { get; } = new ActivityDefinition
        {
            Id = "econ.vendor-loop",
            Family = ActivityFamily.Economy,
            Activity = "Economy",
            Location = "Vendor + repair + bank + mail loop",
            LevelRange = new LevelRange(1, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 1,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 1633.0f, Y: -4439.0f, Z: 38.0f, NamedLocation: "Vendor + repair + bank + mail loop"),
            ExpectedDuration = TimeSpan.FromMinutes(10),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Observer,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["economy", "vendor", "repair", "bank", "mail"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 500, Max: 5000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Economy",
        };

        public static ActivityDefinition RepTimbermawHold { get; } = new ActivityDefinition
        {
            Id = "rep.timbermaw-hold",
            Family = ActivityFamily.Reputation,
            Activity = "Reputation grind",
            Location = "Timbermaw Hold",
            LevelRange = new LevelRange(48, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 6757.0f, Y: -480.0f, Z: 511.0f, NamedLocation: "Timbermaw Hold"),
            ExpectedDuration = TimeSpan.FromHours(2),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(20)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["reputation", "timbermaw-hold", "felwood"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.FactionRep, Min: 200, Max: 1500, ItemId: null, FactionId: 576),
            ],
            TaskFamily = "Questing",
        };

        public static ActivityDefinition RepArgentDawn { get; } = new ActivityDefinition
        {
            Id = "rep.argent-dawn",
            Family = ActivityFamily.Reputation,
            Activity = "Reputation grind",
            Location = "Argent Dawn",
            LevelRange = new LevelRange(50, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: 2278.0f, Y: -5267.0f, Z: 88.0f, NamedLocation: "Argent Dawn"),
            ExpectedDuration = TimeSpan.FromHours(2),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(20)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["reputation", "argent-dawn", "plaguelands"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.FactionRep, Min: 200, Max: 1500, ItemId: null, FactionId: 529),
            ],
            TaskFamily = "Combat",
        };

        public static ActivityDefinition RepCenarionCircle { get; } = new ActivityDefinition
        {
            Id = "rep.cenarion-circle",
            Family = ActivityFamily.Reputation,
            Activity = "Reputation grind",
            Location = "Cenarion Circle",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: -6817.0f, Y: 824.0f, Z: 51.0f, NamedLocation: "Cenarion Circle"),
            ExpectedDuration = TimeSpan.FromHours(2),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(20)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["reputation", "cenarion-circle", "silithus"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.FactionRep, Min: 200, Max: 1500, ItemId: null, FactionId: 609),
            ],
            TaskFamily = "Questing",
        };

        public static ActivityDefinition RepThoriumBrotherhood { get; } = new ActivityDefinition
        {
            Id = "rep.thorium-brotherhood",
            Family = ActivityFamily.Reputation,
            Activity = "Reputation grind",
            Location = "Thorium Brotherhood",
            LevelRange = new LevelRange(50, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -6562.0f, Y: -1167.0f, Z: 184.0f, NamedLocation: "Thorium Brotherhood"),
            ExpectedDuration = TimeSpan.FromHours(2),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(20)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["reputation", "thorium-brotherhood", "blackrock-depths"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.FactionRep, Min: 200, Max: 1500, ItemId: null, FactionId: 59),
            ],
            TaskFamily = "Combat",
        };

        public static ActivityDefinition RepZandalarTribe { get; } = new ActivityDefinition
        {
            Id = "rep.zandalar-tribe",
            Family = ActivityFamily.Reputation,
            Activity = "Reputation grind",
            Location = "Zandalar Tribe",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -11912.0f, Y: -1612.0f, Z: 9.0f, NamedLocation: "Zandalar Tribe"),
            ExpectedDuration = TimeSpan.FromHours(2),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(20)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["reputation", "zandalar-tribe", "zul-gurub"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.FactionRep, Min: 200, Max: 1500, ItemId: null, FactionId: 270),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition EventStvFishingExtravaganza { get; } = new ActivityDefinition
        {
            Id = "event.stv-fishing-extravaganza",
            Family = ActivityFamily.WorldEvent,
            Activity = "World event",
            Location = "STV Fishing Extravaganza",
            LevelRange = new LevelRange(30, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 1,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -14336.0f, Y: 506.0f, Z: 22.0f, NamedLocation: "STV Fishing Extravaganza"),
            ExpectedDuration = TimeSpan.FromHours(2),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Observer,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(15)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["world-event", "fishing", "stranglethorn-vale"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 10000, Max: 100000, ItemId: null, FactionId: null),
                new RewardDefinition(Kind: RewardKind.ItemId, Min: 0, Max: 1, ItemId: 19970, FactionId: null),
            ],
            TaskFamily = "WorldEvent",
        };

        public static ActivityDefinition BossAzuregos { get; } = new ActivityDefinition
        {
            Id = "boss.azuregos",
            Family = ActivityFamily.WorldBoss,
            Activity = "World boss",
            Location = "Azuregos (Azshara)",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 20,
            MaxPlayers = 40,
            RoleTemplate = new RoleTemplate(Tanks: 2, Healers: 4, Dps: 14, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 3325.0f, Y: -4647.0f, Z: 100.0f, NamedLocation: "Azuregos (Azshara)"),
            ExpectedDuration = TimeSpan.FromMinutes(20),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(20)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["world-boss", "azuregos", "azshara"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 5000, Max: 25000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Combat",
        };

        public static ActivityDefinition BossKazzak { get; } = new ActivityDefinition
        {
            Id = "boss.kazzak",
            Family = ActivityFamily.WorldBoss,
            Activity = "World boss",
            Location = "Lord Kazzak (Blasted Lands)",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 20,
            MaxPlayers = 40,
            RoleTemplate = new RoleTemplate(Tanks: 2, Healers: 4, Dps: 14, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -11785.0f, Y: -3203.0f, Z: -25.0f, NamedLocation: "Lord Kazzak (Blasted Lands)"),
            ExpectedDuration = TimeSpan.FromMinutes(20),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(20)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["world-boss", "kazzak", "blasted-lands"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 5000, Max: 25000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Combat",
        };

        public static ActivityDefinition BossEmeraldDragons { get; } = new ActivityDefinition
        {
            Id = "boss.emerald-dragons",
            Family = ActivityFamily.WorldBoss,
            Activity = "World boss",
            Location = "Emerald Dragons (rotating)",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(Requirement: FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 20,
            MaxPlayers = 40,
            RoleTemplate = new RoleTemplate(Tanks: 2, Healers: 4, Dps: 14, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 2767.0f, Y: -1672.0f, Z: 91.0f, NamedLocation: "Emerald Dragons (rotating)"),
            ExpectedDuration = TimeSpan.FromMinutes(20),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: false,
                LootPriorityToHuman: false,
                HumanIdleTimeout: TimeSpan.FromMinutes(20)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["world-boss", "emerald-dragons", "rotating"],
            Rewards =
            [
                new RewardDefinition(Kind: RewardKind.Gold, Min: 5000, Max: 25000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Combat",
        };
    }
}
