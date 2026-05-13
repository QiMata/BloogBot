# Catalog Rows — Dungeons (S0.9.3)

> Companion artifact to [`Plan/Activities/00_INDEX.md`](../00_INDEX.md) and
> [`Spec/04_ACTIVITIES.md`](../../../Spec/04_ACTIVITIES.md). These 21
> `ActivityDefinition` C# literals are the source-text for the
> `dungeon.*` rows that ship in
> `Services/WoWStateManager/Activities/ActivityCatalog.cs` (S1.0.x).
>
> Each row is presented as a stand-alone fenced block. Phase 0 catalog
> tests assert: unique `Id`, `Location` resolves in
> `Bot/named-locations.json`, `LevelRange ⊆ [1,60]`, role-template sums
> within `[MinPlayers, MaxPlayers]`, `TaskFamily ∈ fixed list`,
> `Family ∈ ActivityFamily`.
>
> **Conventions used throughout this file**
> - `Family = ActivityFamily.Dungeon`.
> - `Activity = "Dungeon"`.
> - `TaskFamily = "Dungeoneering"`.
> - `TravelTarget` mirrors `Bot/named-locations.json` exactly. The
>   `MapId` for non-orgrimmar-attached dungeon entries is the **instance
>   map id** as seeded today; per `R14` the seed itself is the runtime
>   resolution target. The five entries whose `mapId` is `0` or `1`
>   (Ragefire Chasm seeded as Orgrimmar coord, plus any seeded as outdoor)
>   come through as authored. See the per-row comment when the seed
>   represents an outdoor portal vs an instance-interior anchor.
> - `FactionPolicy.Requirement = Either` and `AllowCrossFaction = false`
>   for every dungeon (cross-faction groups are not formed at this
>   scale — humans select same-faction bots from the pool).
> - `LockoutPolicy = LockoutPolicy.None()` for every Vanilla 5/10-man
>   dungeon. Vanilla dungeons used the 5-instance-per-hour soft cap, not
>   a raid-style daily/weekly bind. The cap is enforced by MaNGOS
>   server-side and surfaces as a transient legality failure, not as a
>   catalog-declared lockout.
> - `HumanJoinPolicy` is uniform: `HumanCanInitiate=true`,
>   `HumanRole=Leader`, `RequireFactionMatch=true`,
>   `LootPriorityToHuman=true`, `HumanIdleTimeout=TimeSpan.FromMinutes(5)`.
> - `BotSelectionPolicy = new BotSelectionPolicy()` — homogeneous
>   defaults per 2026-05-12 decision; per-family tuning waits for
>   measured data.
> - `Rewards` carry an `XpRange` plus a `Gold` band (copper units) sized
>   to a full clear at the activity's level-band midpoint. Numbers are
>   approximations from the leveling guide and are revisable once
>   Phase 2 telemetry lands.
> - `ProgressionTags` follow `["dungeon", "<level-band>", "<continent>"]`
>   where `<continent>` is `eastern-kingdoms` (Map 0) or `kalimdor`
>   (Map 1). `dungeon.lower-blackrock-spire` and
>   `dungeon.upper-blackrock-spire` add the `"10-man"` tag.

## Dungeons

```csharp
new ActivityDefinition
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
    // Seed encodes the Cleft of Shadow portal coord (Orgrimmar overworld);
    // mapId=389 names the instance for the runtime resolver per R14.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the WC outdoor portal in northern Barrens; mapId=43 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the Moonbrook mine outdoor portal; mapId=36 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the SFK courtyard portal in Silverpine; mapId=33 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the Zoram Strand portal in Ashenvale (note: interior coord
    // captured in named-locations.json); mapId=48 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the RFK outdoor portal in southern Barrens; mapId=47 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed captures the Gnomeregan interior anchor (the outdoor portal is in
    // Dun Morogh); mapId=90 is the instance. Per R14 the resolver decides.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the RFD outdoor portal in southern Barrens; mapId=129 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the Uldaman outdoor portal in Badlands; mapId=70 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the ZF outdoor portal in northwestern Tanaris; mapId=209 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the Maraudon outdoor portal in southern Desolace; mapId=349 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the Atal'Hakkar outdoor portal in Swamp of Sorrows; mapId=109 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the BRD entrance inside Blackrock Mountain; mapId=230 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the LBRS entrance inside Blackrock Mountain; mapId=229 is the shared BRS instance.
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
}
```

```csharp
new ActivityDefinition
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
        // Seal of Ascension chain — the final ring (itemId 12344) grants
        // direct UBRS entry. The Unadorned Seal (12342) and Forged Seal
        // (12343) are the prerequisite chain items the chain produces;
        // they are listed so the legality validator can recognise an
        // in-progress chain. See docs/leveling-guide/attunements/seal-of-ascension.md.
        // Itemid mapping is the assumption captured in QUESTIONS.md Q-S0.9.3-1
        // (final Wowhead-ID confirmation deferred to Phase 2 DB fixture).
        RequiredItems = [12344, 12342, 12343],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None(),
    },
    // Seed encodes the UBRS entrance inside Blackrock Mountain (Father Flame
    // attunement chamber); mapId=229 is the shared BRS instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the DM east wing portal in Feralas; mapId=429 is the shared Dire Maul instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the DM west wing portal in Feralas; mapId=429 is the shared Dire Maul instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the DM north wing portal in Feralas; mapId=429 is the shared Dire Maul instance.
    // DM-North traditionally has a tribute-run path; the catalog row treats it as
    // a single activity with the run-plan deciding tribute vs full clear.
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
}
```

```csharp
new ActivityDefinition
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
        // Scholomance gate originally required Skeleton Key (item 13704)
        // forged via the quest chain at Western Plaguelands; private
        // servers vary on whether the key is enforced. Phase 2's
        // legality validator confirms via DB fixture. Captured in
        // QUESTIONS.md Q-S0.9.3-2; ship empty for Phase 0 to match the
        // dungeon-family default (no per-row item gate).
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None(),
    },
    // Seed encodes the Caer Darrow island portal in Western Plaguelands;
    // mapId=289 is the instance.
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
}
```

```csharp
new ActivityDefinition
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
    // Seed encodes the Stratholme service-entrance portal (undead side) in
    // Eastern Plaguelands; mapId=329 is the shared Strat instance.
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
}
```

```csharp
new ActivityDefinition
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
        // Strat-Live grants substantial Argent Dawn rep on every named-mob
        // kill but does NOT require AD rep to enter — the gate is purely
        // the main-gate door. Per the slot brief: no rep requirement.
        RequiredItems = [],
        RequiredQuests = [],
        RequiredReputations = [],
        RequiredAttunements = [],
        RequiredCapabilities = [],
        LockoutPolicy = LockoutPolicy.None(),
    },
    // Seed encodes the Stratholme main-gate portal (live side) in Eastern
    // Plaguelands; mapId=329 is the shared Strat instance.
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
        // FactionId 529 = Argent Dawn (Vanilla). Strat-Live grants ~250-500
        // AD rep per full clear (Baron + ramp named).
        new RewardDefinition(RewardKind.FactionRep, Min: 250, Max: 500, ItemId: null, FactionId: 529),
    ],
    TaskFamily = "Dungeoneering",
}
```
