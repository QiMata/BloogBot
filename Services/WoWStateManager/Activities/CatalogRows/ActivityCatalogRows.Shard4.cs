using System;
using GameData.Core.Models.Activities;

namespace WoWStateManager.Activities
{
    internal static partial class ActivityCatalogRows
    {
        // ---- Shard 4: _catalog_rows/04_raids_bg_attune.md ----

        public static ActivityDefinition RaidZg { get; } = new ActivityDefinition
        {
            Id = "raid.zg",
            Family = ActivityFamily.Raid,
            Activity = "Raid",
            Location = "Zul'Gurub",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 20,
            MaxPlayers = 20,
            RoleTemplate = new RoleTemplate(Tanks: 2, Healers: 5, Dps: 13),
            EntryRequirements = new EntryRequirements
            {
                RequiredCapabilities = ["ZG"],
                LockoutPolicy = new LockoutPolicy(LockoutType.PerCharacterDaily, "zg"),
            },
            TravelTarget = new TravelTarget(
                MapId: 309, X: -11919.0f, Y: -1202.0f, Z: 92.0f, NamedLocation: "Zul'Gurub"),
            ExpectedDuration = TimeSpan.FromMinutes(90),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(15),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["raid", "60", "tier-1.5", "daily"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 0, Max: 0, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 25, Max: 60, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Raid",
        };

        public static ActivityDefinition RaidAq20 { get; } = new ActivityDefinition
        {
            Id = "raid.aq20",
            Family = ActivityFamily.Raid,
            Activity = "Raid",
            Location = "Ruins of Ahn'Qiraj",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 20,
            MaxPlayers = 20,
            RoleTemplate = new RoleTemplate(Tanks: 2, Healers: 5, Dps: 13),
            EntryRequirements = new EntryRequirements
            {
                RequiredCapabilities = ["AQ20"],
                LockoutPolicy = new LockoutPolicy(LockoutType.PerCharacterDaily, "aq20"),
            },
            TravelTarget = new TravelTarget(
                MapId: 509, X: -8417.0f, Y: 1500.0f, Z: 32.0f, NamedLocation: "Ruins of Ahn'Qiraj"),
            ExpectedDuration = TimeSpan.FromMinutes(90),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(15),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["raid", "60", "tier-2.5", "daily"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 0, Max: 0, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 30, Max: 70, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Raid",
        };

        public static ActivityDefinition RaidMc { get; } = new ActivityDefinition
        {
            Id = "raid.mc",
            Family = ActivityFamily.Raid,
            Activity = "Raid",
            Location = "Molten Core",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 40,
            MaxPlayers = 40,
            RoleTemplate = new RoleTemplate(Tanks: 5, Healers: 8, Dps: 27),
            EntryRequirements = new EntryRequirements
            {
                RequiredAttunements = ["attune.mc"],
                RequiredCapabilities = ["MC"],
                LockoutPolicy = new LockoutPolicy(LockoutType.PerCharacterWeekly, "mc"),
            },
            TravelTarget = new TravelTarget(
                MapId: 409, X: -7515.0f, Y: -1041.0f, Z: 181.0f, NamedLocation: "Molten Core"),
            ExpectedDuration = TimeSpan.FromHours(4),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(15),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["raid", "60", "tier-1", "weekly"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 0, Max: 0, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 80, Max: 200, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Raid",
        };

        public static ActivityDefinition RaidOnyxia { get; } = new ActivityDefinition
        {
            Id = "raid.onyxia",
            Family = ActivityFamily.Raid,
            Activity = "Raid",
            Location = "Onyxia's Lair",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 40,
            MaxPlayers = 40,
            RoleTemplate = new RoleTemplate(Tanks: 3, Healers: 6, Dps: 31),
            EntryRequirements = new EntryRequirements
            {
                // Resolver picks faction-appropriate attunement at request time;
                // catalog declares the OR-set (R-pending per shard 4 questions).
                RequiredAttunements = ["attune.ony-horde", "attune.ony-alliance"],
                RequiredCapabilities = ["Ony"],
                LockoutPolicy = new LockoutPolicy(LockoutType.PerCharacterDaily, "onyxia"),
            },
            TravelTarget = new TravelTarget(
                MapId: 249, X: -4708.0f, Y: -3727.0f, Z: 55.0f, NamedLocation: "Onyxia's Lair"),
            ExpectedDuration = TimeSpan.FromMinutes(30),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(15),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["raid", "60", "tier-2", "daily"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 0, Max: 0, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 40, Max: 100, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Raid",
        };

        public static ActivityDefinition RaidBwl { get; } = new ActivityDefinition
        {
            Id = "raid.bwl",
            Family = ActivityFamily.Raid,
            Activity = "Raid",
            Location = "Blackwing Lair",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 40,
            MaxPlayers = 40,
            RoleTemplate = new RoleTemplate(Tanks: 5, Healers: 9, Dps: 26),
            EntryRequirements = new EntryRequirements
            {
                RequiredAttunements = ["attune.bwl"],
                RequiredCapabilities = ["BWL"],
                LockoutPolicy = new LockoutPolicy(LockoutType.PerCharacterWeekly, "bwl"),
            },
            TravelTarget = new TravelTarget(
                MapId: 469, X: -7672.0f, Y: -1107.0f, Z: 396.0f, NamedLocation: "Blackwing Lair"),
            ExpectedDuration = TimeSpan.FromHours(4),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(15),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["raid", "60", "tier-2", "weekly"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 0, Max: 0, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 100, Max: 250, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Raid",
        };

        public static ActivityDefinition RaidAq40 { get; } = new ActivityDefinition
        {
            Id = "raid.aq40",
            Family = ActivityFamily.Raid,
            Activity = "Raid",
            Location = "Temple of Ahn'Qiraj",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 40,
            MaxPlayers = 40,
            RoleTemplate = new RoleTemplate(Tanks: 5, Healers: 9, Dps: 26),
            EntryRequirements = new EntryRequirements
            {
                RequiredCapabilities = ["AQ40"],
                LockoutPolicy = new LockoutPolicy(LockoutType.PerCharacterWeekly, "aq40"),
            },
            TravelTarget = new TravelTarget(
                MapId: 531, X: -8113.0f, Y: 1525.0f, Z: 3.0f, NamedLocation: "Temple of Ahn'Qiraj"),
            ExpectedDuration = TimeSpan.FromHours(5),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(15),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["raid", "60", "tier-2.5", "weekly"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 0, Max: 0, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 120, Max: 280, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Raid",
        };

        public static ActivityDefinition RaidNaxx { get; } = new ActivityDefinition
        {
            Id = "raid.naxx",
            Family = ActivityFamily.Raid,
            Activity = "Raid",
            Location = "Naxxramas",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 40,
            MaxPlayers = 40,
            RoleTemplate = new RoleTemplate(Tanks: 5, Healers: 10, Dps: 25),
            EntryRequirements = new EntryRequirements
            {
                RequiredAttunements = ["attune.naxx"],
                RequiredReputations = [new FactionStanding(FactionId: 529, MinStanding: ReputationStanding.Honored)],
                RequiredCapabilities = ["Naxx"],
                LockoutPolicy = new LockoutPolicy(LockoutType.PerCharacterWeekly, "naxx"),
            },
            TravelTarget = new TravelTarget(
                MapId: 533, X: 3392.0f, Y: -3380.0f, Z: 261.0f, NamedLocation: "Naxxramas"),
            ExpectedDuration = TimeSpan.FromHours(7),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(15),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["raid", "60", "tier-3", "weekly"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 0, Max: 0, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 150, Max: 350, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Raid",
        };

        public static ActivityDefinition BgWsg { get; } = new ActivityDefinition
        {
            Id = "bg.wsg",
            Family = ActivityFamily.Battleground,
            Activity = "Battleground",
            Location = "Warsong Gulch",
            LevelRange = new LevelRange(10, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 10,
            MaxPlayers = 10,
            RoleTemplate = new RoleTemplate(Tanks: 2, Healers: 1, Dps: 7),
            EntryRequirements = new EntryRequirements
            {
                RequiredCapabilities = ["WSG"],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(
                MapId: 489, X: 1519.0f, Y: 1481.0f, Z: 352.0f, NamedLocation: "Warsong Gulch"),
            ExpectedDuration = TimeSpan.FromMinutes(30),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Member,
                BotRaidLeader = false,
                RequireFactionMatch = true,
                LootPriorityToHuman = false,
                HumanIdleTimeout = TimeSpan.FromMinutes(5),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["pvp", "10-60", "wsg"],
            Rewards =
            [
                new RewardDefinition(RewardKind.Honor, Min: 200, Max: 1500, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.ItemId, Min: 1, Max: 3, ItemId: 20558, FactionId: null),
            ],
            TaskFamily = "Bg",
        };

        public static ActivityDefinition BgAb { get; } = new ActivityDefinition
        {
            Id = "bg.ab",
            Family = ActivityFamily.Battleground,
            Activity = "Battleground",
            Location = "Arathi Basin",
            LevelRange = new LevelRange(20, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 15,
            MaxPlayers = 15,
            RoleTemplate = new RoleTemplate(Tanks: 3, Healers: 2, Dps: 10),
            EntryRequirements = new EntryRequirements
            {
                RequiredCapabilities = ["AB"],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(
                MapId: 529, X: 1268.0f, Y: 1297.0f, Z: -16.0f, NamedLocation: "Arathi Basin"),
            ExpectedDuration = TimeSpan.FromMinutes(22),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Member,
                BotRaidLeader = false,
                RequireFactionMatch = true,
                LootPriorityToHuman = false,
                HumanIdleTimeout = TimeSpan.FromMinutes(5),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["pvp", "20-60", "ab"],
            Rewards =
            [
                new RewardDefinition(RewardKind.Honor, Min: 200, Max: 1500, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.ItemId, Min: 1, Max: 3, ItemId: 20559, FactionId: null),
            ],
            TaskFamily = "Bg",
        };

        public static ActivityDefinition BgAv { get; } = new ActivityDefinition
        {
            Id = "bg.av",
            Family = ActivityFamily.Battleground,
            Activity = "Battleground",
            Location = "Alterac Valley",
            LevelRange = new LevelRange(51, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 40,
            MaxPlayers = 40,
            RoleTemplate = new RoleTemplate(Tanks: 8, Healers: 4, Dps: 28),
            EntryRequirements = new EntryRequirements
            {
                RequiredCapabilities = ["AV"],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(
                MapId: 30, X: -1372.0f, Y: -331.0f, Z: 89.0f, NamedLocation: "Alterac Valley"),
            ExpectedDuration = TimeSpan.FromMinutes(45),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Member,
                BotRaidLeader = false,
                RequireFactionMatch = true,
                LootPriorityToHuman = false,
                HumanIdleTimeout = TimeSpan.FromMinutes(5),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["pvp", "51-60", "av"],
            Rewards =
            [
                new RewardDefinition(RewardKind.Honor, Min: 400, Max: 2000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.ItemId, Min: 1, Max: 5, ItemId: 20560, FactionId: null),
            ],
            TaskFamily = "Bg",
        };

        public static ActivityDefinition AttuneMc { get; } = new ActivityDefinition
        {
            Id = "attune.mc",
            Family = ActivityFamily.Attunement,
            Activity = "Attunement",
            Location = "Molten Core attunement",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredCapabilities = ["MC"],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(
                MapId: 0, X: -7510.0f, Y: -1036.0f, Z: 200.0f, NamedLocation: "Molten Core attunement"),
            ExpectedDuration = TimeSpan.FromHours(2),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(10),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["attunement", "60", "mc"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 14000, Max: 14000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 5, Max: 15, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Questing",
        };

        public static ActivityDefinition AttuneOnyHorde { get; } = new ActivityDefinition
        {
            Id = "attune.ony-horde",
            Family = ActivityFamily.Attunement,
            Activity = "Attunement",
            Location = "Onyxia Horde chain",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Horde, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredCapabilities = ["Ony"],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(
                MapId: 1, X: 1856.0f, Y: -4408.0f, Z: -16.0f, NamedLocation: "Onyxia Horde chain"),
            ExpectedDuration = TimeSpan.FromHours(5),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = true,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(10),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["attunement", "60", "onyxia", "horde"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 28000, Max: 28000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 10, Max: 30, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Questing",
        };

        public static ActivityDefinition AttuneOnyAlliance { get; } = new ActivityDefinition
        {
            Id = "attune.ony-alliance",
            Family = ActivityFamily.Attunement,
            Activity = "Attunement",
            Location = "Onyxia Alliance chain",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredCapabilities = ["Ony"],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(
                MapId: 0, X: -8442.0f, Y: 337.0f, Z: 122.0f, NamedLocation: "Onyxia Alliance chain"),
            ExpectedDuration = TimeSpan.FromHours(5),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = true,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(10),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["attunement", "60", "onyxia", "alliance"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 28000, Max: 28000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 10, Max: 30, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Questing",
        };

        public static ActivityDefinition AttuneBwl { get; } = new ActivityDefinition
        {
            Id = "attune.bwl",
            Family = ActivityFamily.Attunement,
            Activity = "Attunement",
            Location = "Blackwing Lair attunement",
            LevelRange = new LevelRange(58, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                // Soft prereq — see shard 4 R-pending question.
                RequiredAttunements = ["attune.mc"],
                RequiredCapabilities = ["BWL"],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(
                MapId: 469, X: -7510.0f, Y: -1036.0f, Z: 200.0f, NamedLocation: "Blackwing Lair attunement"),
            ExpectedDuration = TimeSpan.FromHours(1),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(10),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["attunement", "60", "bwl"],
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 7000, Max: 7000, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 5, Max: 15, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Questing",
        };

        public static ActivityDefinition AttuneNaxx { get; } = new ActivityDefinition
        {
            Id = "attune.naxx",
            Family = ActivityFamily.Attunement,
            Activity = "Attunement",
            Location = "Naxxramas attunement",
            LevelRange = new LevelRange(60, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredReputations = [new FactionStanding(FactionId: 529, MinStanding: ReputationStanding.Honored)],
                RequiredCapabilities = ["Naxx"],
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(
                MapId: 0, X: 1900.0f, Y: -4140.0f, Z: 70.0f, NamedLocation: "Naxxramas attunement"),
            ExpectedDuration = TimeSpan.FromMinutes(30),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Leader,
                BotRaidLeader = false,
                RequireFactionMatch = false,
                LootPriorityToHuman = true,
                HumanIdleTimeout = TimeSpan.FromMinutes(10),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = ["attunement", "60", "naxx"],
            // R18 — every row must carry a non-empty Rewards list. attune.naxx
            // has no XP/gold yield (pure attunement) so both summary rows are
            // zero-valued, but the shape is preserved.
            Rewards =
            [
                new RewardDefinition(RewardKind.XpRange, Min: 0, Max: 0, ItemId: null, FactionId: null),
                new RewardDefinition(RewardKind.Gold, Min: 0, Max: 0, ItemId: null, FactionId: null),
            ],
            TaskFamily = "Questing",
        };
    }
}
