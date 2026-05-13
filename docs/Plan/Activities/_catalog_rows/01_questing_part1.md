# S0.9.1 — Questing catalog rows (part 1)

This shard owns 16 catalog rows for the questing families: 6 starter
questing rows (one per starting zone, levels 1-10) and the first 10
zone-questing rows in the Westfall → Hillsbrad Foothills cohort. The
lead reconciles this shard, the four sibling S0.9.x shards, and any
follow-ups into `docs/Plan/Activities/01_CATALOG_ROWS.md`. Field shape
follows the `ActivityDefinition` record in `docs/Spec/04_ACTIVITIES.md`;
`Location` strings match `docs/Plan/Activities/00_INDEX.md` exactly and
each `TravelTarget` is sourced from `Bot/named-locations.json` (S0.12
seed). `TaskFamily` is `"Questing"` per the family-head list in
`docs/Spec/03_BOTRUNNER.md#catalog-of-task-families`. XP-range rewards
are best-effort estimates anchored to the cumulative-XP bracket; they
will be tightened in Phase 2 when reward-shape validation runs against
the MaNGOS DB fixture.

## Starter questing

```csharp
new ActivityDefinition
{
    Id = "quest.starter.elwynn-forest",
    Family = ActivityFamily.StarterQuesting,
    Activity = "Starter questing",
    Location = "Elwynn Forest",
    LevelRange = new LevelRange(1, 10),
    FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: -8932.0f, Y: -157.0f, Z: 82.0f,
        NamedLocation: "Elwynn Forest"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "1-10", "alliance-starter", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 35000, Max: 55000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.starter.dun-morogh",
    Family = ActivityFamily.StarterQuesting,
    Activity = "Starter questing",
    Location = "Dun Morogh",
    LevelRange = new LevelRange(1, 10),
    FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: -6240.0f, Y: 332.0f, Z: 383.0f,
        NamedLocation: "Dun Morogh"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "1-10", "alliance-starter", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 35000, Max: 55000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.starter.teldrassil",
    Family = ActivityFamily.StarterQuesting,
    Activity = "Starter questing",
    Location = "Teldrassil",
    LevelRange = new LevelRange(1, 10),
    FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 1, X: 10311.0f, Y: 832.0f, Z: 1326.0f,
        NamedLocation: "Teldrassil"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "1-10", "alliance-starter", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 35000, Max: 55000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.starter.durotar",
    Family = ActivityFamily.StarterQuesting,
    Activity = "Starter questing",
    Location = "Durotar",
    LevelRange = new LevelRange(1, 10),
    FactionPolicy = new FactionPolicy(FactionRequirement.Horde, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 1, X: -618.0f, Y: -4253.0f, Z: 38.0f,
        NamedLocation: "Durotar"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "1-10", "horde-starter", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 35000, Max: 55000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.starter.tirisfal-glades",
    Family = ActivityFamily.StarterQuesting,
    Activity = "Starter questing",
    Location = "Tirisfal Glades",
    LevelRange = new LevelRange(1, 10),
    FactionPolicy = new FactionPolicy(FactionRequirement.Horde, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: 1676.0f, Y: 1678.0f, Z: 121.0f,
        NamedLocation: "Tirisfal Glades"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "1-10", "horde-starter", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 35000, Max: 55000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.starter.mulgore",
    Family = ActivityFamily.StarterQuesting,
    Activity = "Starter questing",
    Location = "Mulgore",
    LevelRange = new LevelRange(1, 10),
    FactionPolicy = new FactionPolicy(FactionRequirement.Horde, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 1, X: -2917.0f, Y: -257.0f, Z: 53.0f,
        NamedLocation: "Mulgore"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "1-10", "horde-starter", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 35000, Max: 55000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

## Zone questing (part 1)

```csharp
new ActivityDefinition
{
    Id = "quest.zone.westfall",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "Westfall",
    LevelRange = new LevelRange(9, 18),
    FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: -10663.0f, Y: 1037.0f, Z: 32.0f,
        NamedLocation: "Westfall"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "10-20", "alliance-mid", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 80000, Max: 140000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.zone.loch-modan",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "Loch Modan",
    LevelRange = new LevelRange(10, 19),
    FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: -4843.0f, Y: -3475.0f, Z: 305.0f,
        NamedLocation: "Loch Modan"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "10-20", "alliance-mid", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 90000, Max: 150000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.zone.darkshore",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "Darkshore",
    LevelRange = new LevelRange(10, 20),
    FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 1, X: 6303.0f, Y: 491.0f, Z: 14.0f,
        NamedLocation: "Darkshore"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "10-20", "alliance-mid", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 100000, Max: 170000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.zone.silverpine-forest",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "Silverpine Forest",
    LevelRange = new LevelRange(10, 20),
    FactionPolicy = new FactionPolicy(FactionRequirement.Horde, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: -360.0f, Y: 1517.0f, Z: 56.0f,
        NamedLocation: "Silverpine Forest"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "10-20", "horde-mid", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 100000, Max: 170000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.zone.the-barrens",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "The Barrens",
    LevelRange = new LevelRange(10, 25),
    FactionPolicy = new FactionPolicy(FactionRequirement.Horde, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 1, X: -443.0f, Y: -2649.0f, Z: 96.0f,
        NamedLocation: "The Barrens"),
    ExpectedDuration = TimeSpan.FromHours(4),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "10-25", "horde-mid", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 180000, Max: 320000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.zone.redridge-mountains",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "Redridge Mountains",
    LevelRange = new LevelRange(15, 25),
    FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: -9248.0f, Y: -2244.0f, Z: 67.0f,
        NamedLocation: "Redridge Mountains"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "10-25", "alliance-mid", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 130000, Max: 220000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.zone.ashenvale",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "Ashenvale",
    LevelRange = new LevelRange(18, 30),
    FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 1, X: 2728.0f, Y: -377.0f, Z: 107.0f,
        NamedLocation: "Ashenvale"),
    ExpectedDuration = TimeSpan.FromHours(4),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "20-30", "contested", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 180000, Max: 300000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.zone.duskwood",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "Duskwood",
    LevelRange = new LevelRange(18, 30),
    FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: -10473.0f, Y: -1156.0f, Z: 36.0f,
        NamedLocation: "Duskwood"),
    ExpectedDuration = TimeSpan.FromHours(4),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "20-30", "alliance-mid", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 180000, Max: 300000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.zone.wetlands",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "Wetlands",
    LevelRange = new LevelRange(20, 30),
    FactionPolicy = new FactionPolicy(FactionRequirement.Alliance, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: -3792.0f, Y: -782.0f, Z: 9.0f,
        NamedLocation: "Wetlands"),
    ExpectedDuration = TimeSpan.FromHours(3),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "20-30", "alliance-mid", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 180000, Max: 290000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```

```csharp
new ActivityDefinition
{
    Id = "quest.zone.hillsbrad-foothills",
    Family = ActivityFamily.ZoneQuesting,
    Activity = "Zone questing",
    Location = "Hillsbrad Foothills",
    LevelRange = new LevelRange(20, 30),
    FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
    MinPlayers = 1,
    MaxPlayers = 5,
    RoleTemplate = new RoleTemplate(Tanks: 0, Healers: 0, Dps: 1),
    EntryRequirements = new EntryRequirements
    {
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None()
    },
    TravelTarget = new TravelTarget(
        MapId: 0, X: -852.0f, Y: -592.0f, Z: 22.0f,
        NamedLocation: "Hillsbrad Foothills"),
    ExpectedDuration = TimeSpan.FromHours(4),
    HumanJoinPolicy = new HumanJoinPolicy
    {
        HumanCanInitiate = true,
        HumanRole = HumanGroupRole.Member,
        RequireFactionMatch = true,
        LootPriorityToHuman = false,
        HumanIdleTimeout = TimeSpan.FromMinutes(5)
    },
    BotSelectionPolicy = new BotSelectionPolicy(),
    ProgressionTags = ["levelling", "20-30", "contested", "zone-questing"],
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 180000, Max: 290000, ItemId: null, FactionId: null)
    ],
    TaskFamily = "Questing"
},
```
