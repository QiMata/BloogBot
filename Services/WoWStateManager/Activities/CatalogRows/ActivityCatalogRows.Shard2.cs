using System;
using System.Collections.Generic;
using GameData.Core.Models.Activities;

namespace WoWStateManager.Activities
{
    internal static partial class ActivityCatalogRows
    {
        // ---- Shard 2: _catalog_rows/02_questing_part2.md ----

        public static ActivityDefinition QuestZoneStonetalonMountains { get; } = new ActivityDefinition
        {
            Id = "quest.zone.stonetalon-mountains",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Stonetalon Mountains",
            LevelRange = new LevelRange(16, 27),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 919.0f, Y: 940.0f, Z: 105.0f, NamedLocation: "Stonetalon Mountains"),
            ExpectedDuration = TimeSpan.FromHours(3),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "16-27", "stonetalon-mountains" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 80000, Max: 130000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneThousandNeedles { get; } = new ActivityDefinition
        {
            Id = "quest.zone.thousand-needles",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Thousand Needles",
            LevelRange = new LevelRange(25, 35),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: -4709.0f, Y: -1860.0f, Z: 88.0f, NamedLocation: "Thousand Needles"),
            ExpectedDuration = TimeSpan.FromHours(3),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "25-35", "thousand-needles" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 150000, Max: 220000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneDesolace { get; } = new ActivityDefinition
        {
            Id = "quest.zone.desolace",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Desolace",
            LevelRange = new LevelRange(28, 38),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: -1411.0f, Y: 2936.0f, Z: 88.0f, NamedLocation: "Desolace"),
            ExpectedDuration = TimeSpan.FromHours(3),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "28-38", "desolace" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 180000, Max: 260000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneArathiHighlands { get; } = new ActivityDefinition
        {
            Id = "quest.zone.arathi-highlands",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Arathi Highlands",
            LevelRange = new LevelRange(30, 40),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -1747.0f, Y: -1693.0f, Z: 60.0f, NamedLocation: "Arathi Highlands"),
            ExpectedDuration = TimeSpan.FromHours(3),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "30-40", "arathi-highlands" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 220000, Max: 320000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneStranglethornVale { get; } = new ActivityDefinition
        {
            Id = "quest.zone.stranglethorn-vale",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Stranglethorn Vale",
            LevelRange = new LevelRange(30, 45),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -14441.0f, Y: 553.0f, Z: 22.0f, NamedLocation: "Stranglethorn Vale"),
            ExpectedDuration = TimeSpan.FromHours(4),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "30-45", "stranglethorn-vale" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 320000, Max: 450000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneDustwallowMarsh { get; } = new ActivityDefinition
        {
            Id = "quest.zone.dustwallow-marsh",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Dustwallow Marsh",
            LevelRange = new LevelRange(35, 45),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: -3825.0f, Y: -4502.0f, Z: 9.0f, NamedLocation: "Dustwallow Marsh"),
            ExpectedDuration = TimeSpan.FromHours(4),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "35-45", "dustwallow-marsh" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 280000, Max: 400000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneBadlands { get; } = new ActivityDefinition
        {
            Id = "quest.zone.badlands",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Badlands",
            LevelRange = new LevelRange(35, 45),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -6826.0f, Y: -2890.0f, Z: 242.0f, NamedLocation: "Badlands"),
            ExpectedDuration = TimeSpan.FromHours(4),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "35-45", "badlands" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 260000, Max: 380000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneTanaris { get; } = new ActivityDefinition
        {
            Id = "quest.zone.tanaris",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Tanaris",
            LevelRange = new LevelRange(40, 50),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: -7177.0f, Y: -3779.0f, Z: 9.0f, NamedLocation: "Tanaris"),
            ExpectedDuration = TimeSpan.FromHours(4),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "40-50", "tanaris" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 360000, Max: 500000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneFeralas { get; } = new ActivityDefinition
        {
            Id = "quest.zone.feralas",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Feralas",
            LevelRange = new LevelRange(40, 50),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: -4400.0f, Y: 252.0f, Z: 36.0f, NamedLocation: "Feralas"),
            ExpectedDuration = TimeSpan.FromHours(4),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "40-50", "feralas" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 360000, Max: 500000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneSearingGorge { get; } = new ActivityDefinition
        {
            Id = "quest.zone.searing-gorge",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Searing Gorge",
            LevelRange = new LevelRange(43, 50),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -6562.0f, Y: -1167.0f, Z: 184.0f, NamedLocation: "Searing Gorge"),
            ExpectedDuration = TimeSpan.FromHours(4),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "43-50", "searing-gorge" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 300000, Max: 420000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneAzshara { get; } = new ActivityDefinition
        {
            Id = "quest.zone.azshara",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Azshara",
            LevelRange = new LevelRange(45, 55),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 3372.0f, Y: -4665.0f, Z: 79.0f, NamedLocation: "Azshara"),
            ExpectedDuration = TimeSpan.FromHours(5),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "45-55", "azshara" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 380000, Max: 540000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneTheHinterlands { get; } = new ActivityDefinition
        {
            Id = "quest.zone.the-hinterlands",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "The Hinterlands",
            LevelRange = new LevelRange(30, 45),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -3666.0f, Y: -2929.0f, Z: 165.0f, NamedLocation: "The Hinterlands"),
            ExpectedDuration = TimeSpan.FromHours(4),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "30-45", "the-hinterlands" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 280000, Max: 410000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneFelwood { get; } = new ActivityDefinition
        {
            Id = "quest.zone.felwood",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Felwood",
            LevelRange = new LevelRange(48, 55),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 5408.0f, Y: -749.0f, Z: 339.0f, NamedLocation: "Felwood"),
            ExpectedDuration = TimeSpan.FromHours(5),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "48-55", "felwood" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 360000, Max: 520000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneUngoroCrater { get; } = new ActivityDefinition
        {
            Id = "quest.zone.ungoro-crater",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Un'Goro Crater",
            LevelRange = new LevelRange(48, 55),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: -6086.0f, Y: -1102.0f, Z: -180.0f, NamedLocation: "Un'Goro Crater"),
            ExpectedDuration = TimeSpan.FromHours(5),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "48-55", "ungoro-crater" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 380000, Max: 540000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneWesternPlaguelands { get; } = new ActivityDefinition
        {
            Id = "quest.zone.western-plaguelands",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Western Plaguelands",
            LevelRange = new LevelRange(50, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: 1781.0f, Y: -1581.0f, Z: 60.0f, NamedLocation: "Western Plaguelands"),
            ExpectedDuration = TimeSpan.FromHours(6),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "50-60", "western-plaguelands" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 480000, Max: 680000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneEasternPlaguelands { get; } = new ActivityDefinition
        {
            Id = "quest.zone.eastern-plaguelands",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Eastern Plaguelands",
            LevelRange = new LevelRange(53, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: 2275.0f, Y: -5346.0f, Z: 88.0f, NamedLocation: "Eastern Plaguelands"),
            ExpectedDuration = TimeSpan.FromHours(6),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "53-60", "eastern-plaguelands" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 460000, Max: 660000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneBurningSteppes { get; } = new ActivityDefinition
        {
            Id = "quest.zone.burning-steppes",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Burning Steppes",
            LevelRange = new LevelRange(50, 58),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 0u, X: -7997.0f, Y: -1462.0f, Z: 137.0f, NamedLocation: "Burning Steppes"),
            ExpectedDuration = TimeSpan.FromHours(6),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "50-58", "burning-steppes" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 420000, Max: 600000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneWinterspring { get; } = new ActivityDefinition
        {
            Id = "quest.zone.winterspring",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Winterspring",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: 6717.0f, Y: -4655.0f, Z: 722.0f, NamedLocation: "Winterspring"),
            ExpectedDuration = TimeSpan.FromHours(6),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "55-60", "winterspring" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 500000, Max: 700000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };

        public static ActivityDefinition QuestZoneSilithus { get; } = new ActivityDefinition
        {
            Id = "quest.zone.silithus",
            Family = ActivityFamily.ZoneQuesting,
            Activity = "Zone questing",
            Location = "Silithus",
            LevelRange = new LevelRange(55, 60),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = 1,
            MaxPlayers = 5,
            RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1, Support: 0),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = new List<int>(),
                RequiredQuests = new List<int>(),
                RequiredReputations = new List<FactionStanding>(),
                RequiredAttunements = new List<string>(),
                RequiredCapabilities = new List<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(MapId: 1u, X: -6817.0f, Y: 824.0f, Z: 51.0f, NamedLocation: "Silithus"),
            ExpectedDuration = TimeSpan.FromHours(6),
            HumanJoinPolicy = new HumanJoinPolicy(
                HumanCanInitiate: true,
                HumanRole: HumanGroupRole.Member,
                RequireFactionMatch: true,
                LootPriorityToHuman: true,
                HumanIdleTimeout: TimeSpan.FromMinutes(10)),
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = new[] { "questing", "zone-questing", "55-60", "silithus" },
            Rewards = new[]
            {
                new RewardDefinition(RewardKind.XpRange, Min: 480000, Max: 700000, ItemId: null, FactionId: null),
            },
            TaskFamily = "Questing",
        };
    }
}
