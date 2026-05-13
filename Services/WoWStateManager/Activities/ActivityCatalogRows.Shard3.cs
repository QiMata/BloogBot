using System;
using GameData.Core.Models.Activities;

namespace WoWStateManager.Activities
{
    internal static partial class ActivityCatalogRows
    {
        // ---- Shard 3: _catalog_rows/03_dungeons.md ----

        public static ActivityDefinition DungeonRagefireChasm { get; } = new ActivityDefinition
        {
            Id = "dungeon.ragefire-chasm",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Ragefire Chasm",
            LevelRange = new LevelRange(13, 18),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 389, X: 1812.0f, Y: -4418.0f, Z: -18.0f, NamedLocation: "Ragefire Chasm"),
            ExpectedDuration = TimeSpan.FromMinutes(45),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "13-18", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 4000, Max: 9000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 3000, Max: 8000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonWailingCaverns { get; } = new ActivityDefinition
        {
            Id = "dungeon.wailing-caverns",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Wailing Caverns",
            LevelRange = new LevelRange(17, 24),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 43, X: -741.0f, Y: -2218.0f, Z: 16.0f, NamedLocation: "Wailing Caverns"),
            ExpectedDuration = TimeSpan.FromMinutes(75),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "17-24", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 7000, Max: 16000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 5000, Max: 14000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonDeadmines { get; } = new ActivityDefinition
        {
            Id = "dungeon.deadmines",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Deadmines",
            LevelRange = new LevelRange(17, 26),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 36, X: -16.0f, Y: -384.0f, Z: 61.0f, NamedLocation: "Deadmines"),
            ExpectedDuration = TimeSpan.FromMinutes(75),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "17-26", "eastern-kingdoms"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 8000, Max: 18000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 6000, Max: 15000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonShadowfangKeep { get; } = new ActivityDefinition
        {
            Id = "dungeon.shadowfang-keep",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Shadowfang Keep",
            LevelRange = new LevelRange(22, 30),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 33, X: -234.0f, Y: 1561.0f, Z: 76.0f, NamedLocation: "Shadowfang Keep"),
            ExpectedDuration = TimeSpan.FromMinutes(60),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "22-30", "eastern-kingdoms"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 10000, Max: 22000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 7000, Max: 18000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonBlackfathomDeeps { get; } = new ActivityDefinition
        {
            Id = "dungeon.blackfathom-deeps",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Blackfathom Deeps",
            LevelRange = new LevelRange(20, 30),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 48, X: -152.0f, Y: 106.0f, Z: -39.0f, NamedLocation: "Blackfathom Deeps"),
            ExpectedDuration = TimeSpan.FromMinutes(75),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "20-30", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 10000, Max: 22000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 7000, Max: 18000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonRazorfenKraul { get; } = new ActivityDefinition
        {
            Id = "dungeon.razorfen-kraul",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Razorfen Kraul",
            LevelRange = new LevelRange(24, 34),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 47, X: -4470.0f, Y: -1677.0f, Z: 82.0f, NamedLocation: "Razorfen Kraul"),
            ExpectedDuration = TimeSpan.FromMinutes(70),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "24-34", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 12000, Max: 26000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 8000, Max: 20000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonGnomeregan { get; } = new ActivityDefinition
        {
            Id = "dungeon.gnomeregan",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Gnomeregan",
            LevelRange = new LevelRange(29, 38),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 90, X: -332.0f, Y: -2.0f, Z: -152.0f, NamedLocation: "Gnomeregan"),
            ExpectedDuration = TimeSpan.FromMinutes(75),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "29-38", "eastern-kingdoms"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 14000, Max: 30000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 9000, Max: 22000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonRazorfenDowns { get; } = new ActivityDefinition
        {
            Id = "dungeon.razorfen-downs",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Razorfen Downs",
            LevelRange = new LevelRange(35, 45),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 129, X: -4661.0f, Y: -2511.0f, Z: 81.0f, NamedLocation: "Razorfen Downs"),
            ExpectedDuration = TimeSpan.FromMinutes(75),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "35-45", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 18000, Max: 40000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 11000, Max: 28000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonUldaman { get; } = new ActivityDefinition
        {
            Id = "dungeon.uldaman",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Uldaman",
            LevelRange = new LevelRange(41, 51),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 70, X: -6071.0f, Y: -2955.0f, Z: 209.0f, NamedLocation: "Uldaman"),
            ExpectedDuration = TimeSpan.FromMinutes(80),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "41-51", "eastern-kingdoms"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 22000, Max: 48000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 13000, Max: 32000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonZulFarrak { get; } = new ActivityDefinition
        {
            Id = "dungeon.zul-farrak",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Zul'Farrak",
            LevelRange = new LevelRange(44, 54),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 209, X: -6803.0f, Y: -2891.0f, Z: 9.0f, NamedLocation: "Zul'Farrak"),
            ExpectedDuration = TimeSpan.FromMinutes(80),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "44-54", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 24000, Max: 52000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 14000, Max: 34000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonMaraudon { get; } = new ActivityDefinition
        {
            Id = "dungeon.maraudon",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Maraudon",
            LevelRange = new LevelRange(46, 55),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 349, X: -1428.0f, Y: 2607.0f, Z: 76.0f, NamedLocation: "Maraudon"),
            ExpectedDuration = TimeSpan.FromMinutes(90),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "46-55", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 26000, Max: 58000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 15000, Max: 38000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonSunkenTemple { get; } = new ActivityDefinition
        {
            Id = "dungeon.sunken-temple",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Sunken Temple",
            LevelRange = new LevelRange(50, 56),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 109, X: -10171.0f, Y: -3995.0f, Z: -111.0f, NamedLocation: "Sunken Temple"),
            ExpectedDuration = TimeSpan.FromMinutes(90),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "50-56", "eastern-kingdoms"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 30000, Max: 65000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 17000, Max: 42000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonBlackrockDepths { get; } = new ActivityDefinition
        {
            Id = "dungeon.blackrock-depths",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Blackrock Depths",
            LevelRange = new LevelRange(52, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 230, X: 472.0f, Y: 24.0f, Z: -70.0f, NamedLocation: "Blackrock Depths"),
            ExpectedDuration = TimeSpan.FromMinutes(150),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "52-60", "eastern-kingdoms"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 40000, Max: 90000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 22000, Max: 55000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonLowerBlackrockSpire { get; } = new ActivityDefinition
        {
            Id = "dungeon.lower-blackrock-spire",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Lower Blackrock Spire",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 10,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 8),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 229, X: -7527.0f, Y: -1226.0f, Z: 285.0f, NamedLocation: "Lower Blackrock Spire"),
            ExpectedDuration = TimeSpan.FromMinutes(105),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "55-60", "eastern-kingdoms", "10-man"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 45000, Max: 95000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 25000, Max: 60000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonUpperBlackrockSpire { get; } = new ActivityDefinition
        {
            Id = "dungeon.upper-blackrock-spire",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Upper Blackrock Spire",
            LevelRange = new LevelRange(58, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 10,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 8),
            EntryRequirements = new EntryRequirements
            {
                // Seal of Ascension chain — final ring (12344) plus prereq
                // chain items (12342, 12343). Q-S0.9.3-1 in QUESTIONS.md
                // captures the Phase 2 verification deferral.
                RequiredItems = [12344, 12342, 12343],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 229, X: -7527.0f, Y: -1226.0f, Z: 285.0f, NamedLocation: "Upper Blackrock Spire"),
            ExpectedDuration = TimeSpan.FromMinutes(110),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "58-60", "eastern-kingdoms", "10-man"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 50000, Max: 100000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 28000, Max: 65000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonDireMaulEast { get; } = new ActivityDefinition
        {
            Id = "dungeon.dire-maul-east",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Dire Maul East",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 429, X: -3978.0f, Y: 1130.0f, Z: 161.0f, NamedLocation: "Dire Maul East"),
            ExpectedDuration = TimeSpan.FromMinutes(75),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "55-60", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 35000, Max: 75000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 20000, Max: 50000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonDireMaulWest { get; } = new ActivityDefinition
        {
            Id = "dungeon.dire-maul-west",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Dire Maul West",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 429, X: -3980.0f, Y: 1131.0f, Z: 161.0f, NamedLocation: "Dire Maul West"),
            ExpectedDuration = TimeSpan.FromMinutes(80),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "55-60", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 35000, Max: 75000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 20000, Max: 50000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonDireMaulNorth { get; } = new ActivityDefinition
        {
            Id = "dungeon.dire-maul-north",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Dire Maul North",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 429, X: -3979.0f, Y: 1130.0f, Z: 161.0f, NamedLocation: "Dire Maul North"),
            ExpectedDuration = TimeSpan.FromMinutes(90),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "55-60", "kalimdor"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 35000, Max: 75000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 22000, Max: 55000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonScholomance { get; } = new ActivityDefinition
        {
            Id = "dungeon.scholomance",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Scholomance",
            LevelRange = new LevelRange(58, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                // Skeleton Key (13704) gate enforcement varies on private
                // servers; ships empty for Phase 0 per Q-S0.9.3-2.
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 289, X: 1267.0f, Y: -2557.0f, Z: 94.0f, NamedLocation: "Scholomance"),
            ExpectedDuration = TimeSpan.FromMinutes(105),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "58-60", "eastern-kingdoms"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 45000, Max: 90000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 25000, Max: 60000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonStratholmeUndead { get; } = new ActivityDefinition
        {
            Id = "dungeon.stratholme-undead",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Stratholme Undead",
            LevelRange = new LevelRange(58, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 329, X: 3187.0f, Y: -4063.0f, Z: 107.0f, NamedLocation: "Stratholme Undead"),
            ExpectedDuration = TimeSpan.FromMinutes(105),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "58-60", "eastern-kingdoms"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 45000, Max: 95000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 26000, Max: 62000, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Dungeoneering",
        };

        public static ActivityDefinition DungeonStratholmeLive { get; } = new ActivityDefinition
        {
            Id = "dungeon.stratholme-live",
            Family = ActivityFamily.Dungeon,
            Activity = "Dungeon",
            Location = "Stratholme Live",
            LevelRange = new LevelRange(58, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = [],
                RequiredQuests = [],
                RequiredReputations = [],
                RequiredAttunements = [],
                RequiredCapabilities = [],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 329, X: 3359.0f, Y: -3380.0f, Z: 144.0f, NamedLocation: "Stratholme Live"),
            ExpectedDuration = TimeSpan.FromMinutes(110),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Leader,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(5)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["dungeon", "58-60", "eastern-kingdoms"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 45000, Max: 95000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 26000, Max: 62000, ItemId: null, FactionId: null),
                // FactionId 529 = Argent Dawn. Full clear grants 250-500 rep.
                new RewardDefinition(RewardKind.FactionRep, Min: 250, Max: 500, ItemId: null, FactionId: 529),
            ],
            TaskFamily = "Dungeoneering",
        };
    }
}
