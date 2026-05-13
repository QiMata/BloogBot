# S0.9.5 — Misc catalog rows (professions, economy, reputations, world events, world bosses)

15 `ActivityDefinition` literals authored per the record shape in
[`Spec/04_ACTIVITIES.md`](../../../Spec/04_ACTIVITIES.md). Every `Id`,
`Location`, and `LevelRange` matches the corresponding row in
[`00_INDEX.md`](../00_INDEX.md). Every `Location` resolves to a non-empty
entry in [`Bot/named-locations.json`](../../../../Bot/named-locations.json).
Every `TaskFamily` is in the fixed family-head list per R16
(`Spec/03_BOTRUNNER.md#catalog-of-task-families`).

Field rules sourced from the S0.9.5 slot brief:

- **Professions (4)** — `Family = ProfessionGathering | ProfessionLeveling`,
  `HumanRole = Observer`, `LockoutPolicy.None()`, `TaskFamily =
  Gathering | Crafting`.
- **Economy (2)** — `Family = Economy`, `TaskFamily = "Economy"`,
  `HumanRole = Observer`, `LockoutPolicy.None()`.
- **Reputations (5)** — `Family = Reputation`, `HumanRole = Member`,
  `LockoutPolicy.None()`, `TaskFamily` per the reputation→family-head
  dispatch table in [`reputations.md`](../reputations.md#catalog-taskfamily-claim-per-row).
- **World events (1)** — `Family = WorldEvent`, `HumanRole = Observer`,
  `TaskFamily = "WorldEvent"`, `LockoutPolicy.None()`.
- **World bosses (3)** — `Family = WorldBoss`, `HumanRole = Leader`,
  `BotRaidLeader = false`, `TaskFamily = "Combat"`,
  `LockoutPolicy.None()` (world bosses use respawn timers, not personal
  lockouts).

Faction ids used (with verification status):

| Catalog row | Faction | Id | Verification |
|---|---|---|---|
| `rep.timbermaw-hold` | Timbermaw Hold | 576 | Verified — appears in `Config/activities/rep.timbermaw-hold.json:15`. |
| `rep.argent-dawn` | Argent Dawn | 529 | Verified — appears in `Config/CharacterTemplates/HolyPriestMCReady.json:13`, `Tests/BotRunner.Tests/Progression/ReputationTrackingTests.cs:70`, `docs/Plan/Activities/attunements.md:272`. |
| `rep.cenarion-circle` | Cenarion Circle | 609 | ⚠ Unverified in repo — flagged in `Plan/QUESTIONS.md` as `Q-S0.9.5-1`. |
| `rep.thorium-brotherhood` | Thorium Brotherhood | 59 | ⚠ Unverified in repo — flagged in `Plan/QUESTIONS.md` as `Q-S0.9.5-2`. Prompt itself acknowledged uncertainty; `577` is Brood of Nozdormu (a different faction). |
| `rep.zandalar-tribe` | Zandalar Tribe | 270 | ⚠ Unverified in repo — flagged in `Plan/QUESTIONS.md` as `Q-S0.9.5-3`. |

All `TravelTarget` coords are duplicate hints from `Bot/named-locations.json`
per S0.9 step 5; S0.12 is authoritative.

## Professions

### `prof.mining-route`

```csharp
new ActivityDefinition
{
    Id = "prof.mining-route",
    Family = ActivityFamily.ProfessionGathering,
    Activity = "Profession farming",
    Location = "Mining route",
    LevelRange = new LevelRange(1, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: 1633.0f,
        Y: -4439.0f,
        Z: 38.0f,
        NamedLocation: "Mining route"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 30000,
            Max: 75000,
            ItemId: null,
            FactionId: null),
    ],
    TaskFamily = "Gathering",
}
```

### `prof.herbalism-route`

```csharp
new ActivityDefinition
{
    Id = "prof.herbalism-route",
    Family = ActivityFamily.ProfessionGathering,
    Activity = "Profession farming",
    Location = "Herbalism route",
    LevelRange = new LevelRange(1, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: 1633.0f,
        Y: -4439.0f,
        Z: 38.0f,
        NamedLocation: "Herbalism route"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 30000,
            Max: 75000,
            ItemId: null,
            FactionId: null),
    ],
    TaskFamily = "Gathering",
}
```

### `prof.skinning-route`

```csharp
new ActivityDefinition
{
    Id = "prof.skinning-route",
    Family = ActivityFamily.ProfessionGathering,
    Activity = "Profession farming",
    Location = "Skinning route",
    LevelRange = new LevelRange(1, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: 1633.0f,
        Y: -4439.0f,
        Z: 38.0f,
        NamedLocation: "Skinning route"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 20000,
            Max: 45000,
            ItemId: null,
            FactionId: null),
    ],
    TaskFamily = "Gathering",
}
```

### `prof.city-trainer-loop`

```csharp
new ActivityDefinition
{
    Id = "prof.city-trainer-loop",
    Family = ActivityFamily.ProfessionLeveling,
    Activity = "Profession leveling",
    Location = "City trainer + recipe loop",
    LevelRange = new LevelRange(5, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: 1633.0f,
        Y: -4439.0f,
        Z: 38.0f,
        NamedLocation: "City trainer + recipe loop"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 0,
            Max: 0,
            ItemId: null,
            FactionId: null),
    ],
    TaskFamily = "Crafting",
}
```

## Economy

### `econ.ah-restock`

```csharp
new ActivityDefinition
{
    Id = "econ.ah-restock",
    Family = ActivityFamily.Economy,
    Activity = "Economy",
    Location = "Auction house restock",
    LevelRange = new LevelRange(1, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: 1671.0f,
        Y: -4346.0f,
        Z: 60.0f,
        NamedLocation: "Auction house restock"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 10000,
            Max: 200000,
            ItemId: null,
            FactionId: null),
    ],
    TaskFamily = "Economy",
}
```

### `econ.vendor-loop`

```csharp
new ActivityDefinition
{
    Id = "econ.vendor-loop",
    Family = ActivityFamily.Economy,
    Activity = "Economy",
    Location = "Vendor + repair + bank + mail loop",
    LevelRange = new LevelRange(1, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: 1633.0f,
        Y: -4439.0f,
        Z: 38.0f,
        NamedLocation: "Vendor + repair + bank + mail loop"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 500,
            Max: 5000,
            ItemId: null,
            FactionId: null),
    ],
    TaskFamily = "Economy",
}
```

## Reputations

### `rep.timbermaw-hold`

```csharp
new ActivityDefinition
{
    Id = "rep.timbermaw-hold",
    Family = ActivityFamily.Reputation,
    Activity = "Reputation grind",
    Location = "Timbermaw Hold",
    LevelRange = new LevelRange(48, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: 6757.0f,
        Y: -480.0f,
        Z: 511.0f,
        NamedLocation: "Timbermaw Hold"),
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
        new RewardDefinition(
            Kind: RewardKind.FactionRep,
            Min: 200,
            Max: 1500,
            ItemId: null,
            FactionId: 576),
    ],
    TaskFamily = "Questing",
}
```

### `rep.argent-dawn`

```csharp
new ActivityDefinition
{
    Id = "rep.argent-dawn",
    Family = ActivityFamily.Reputation,
    Activity = "Reputation grind",
    Location = "Argent Dawn",
    LevelRange = new LevelRange(50, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 0u,
        X: 2278.0f,
        Y: -5267.0f,
        Z: 88.0f,
        NamedLocation: "Argent Dawn"),
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
        new RewardDefinition(
            Kind: RewardKind.FactionRep,
            Min: 200,
            Max: 1500,
            ItemId: null,
            FactionId: 529),
    ],
    TaskFamily = "Combat",
}
```

### `rep.cenarion-circle`

```csharp
new ActivityDefinition
{
    Id = "rep.cenarion-circle",
    Family = ActivityFamily.Reputation,
    Activity = "Reputation grind",
    Location = "Cenarion Circle",
    LevelRange = new LevelRange(55, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: -6817.0f,
        Y: 824.0f,
        Z: 51.0f,
        NamedLocation: "Cenarion Circle"),
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
        new RewardDefinition(
            Kind: RewardKind.FactionRep,
            Min: 200,
            Max: 1500,
            ItemId: null,
            FactionId: 609),
    ],
    TaskFamily = "Questing",
}
```

### `rep.thorium-brotherhood`

```csharp
new ActivityDefinition
{
    Id = "rep.thorium-brotherhood",
    Family = ActivityFamily.Reputation,
    Activity = "Reputation grind",
    Location = "Thorium Brotherhood",
    LevelRange = new LevelRange(50, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 0u,
        X: -6562.0f,
        Y: -1167.0f,
        Z: 184.0f,
        NamedLocation: "Thorium Brotherhood"),
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
        new RewardDefinition(
            Kind: RewardKind.FactionRep,
            Min: 200,
            Max: 1500,
            ItemId: null,
            FactionId: 59),
    ],
    TaskFamily = "Combat",
}
```

### `rep.zandalar-tribe`

```csharp
new ActivityDefinition
{
    Id = "rep.zandalar-tribe",
    Family = ActivityFamily.Reputation,
    Activity = "Reputation grind",
    Location = "Zandalar Tribe",
    LevelRange = new LevelRange(60, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 0u,
        X: -11912.0f,
        Y: -1612.0f,
        Z: 9.0f,
        NamedLocation: "Zandalar Tribe"),
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
        new RewardDefinition(
            Kind: RewardKind.FactionRep,
            Min: 200,
            Max: 1500,
            ItemId: null,
            FactionId: 270),
    ],
    TaskFamily = "Dungeoneering",
}
```

## World events

### `event.stv-fishing-extravaganza`

```csharp
new ActivityDefinition
{
    Id = "event.stv-fishing-extravaganza",
    Family = ActivityFamily.WorldEvent,
    Activity = "World event",
    Location = "STV Fishing Extravaganza",
    LevelRange = new LevelRange(30, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 0u,
        X: -14336.0f,
        Y: 506.0f,
        Z: 22.0f,
        NamedLocation: "STV Fishing Extravaganza"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 10000,
            Max: 100000,
            ItemId: null,
            FactionId: null),
        new RewardDefinition(
            Kind: RewardKind.ItemId,
            Min: 0,
            Max: 1,
            ItemId: 19970,
            FactionId: null),
    ],
    TaskFamily = "WorldEvent",
}
```

## World bosses

### `boss.azuregos`

```csharp
new ActivityDefinition
{
    Id = "boss.azuregos",
    Family = ActivityFamily.WorldBoss,
    Activity = "World boss",
    Location = "Azuregos (Azshara)",
    LevelRange = new LevelRange(60, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: 3325.0f,
        Y: -4647.0f,
        Z: 100.0f,
        NamedLocation: "Azuregos (Azshara)"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 5000,
            Max: 25000,
            ItemId: null,
            FactionId: null),
    ],
    TaskFamily = "Combat",
}
```

### `boss.kazzak`

```csharp
new ActivityDefinition
{
    Id = "boss.kazzak",
    Family = ActivityFamily.WorldBoss,
    Activity = "World boss",
    Location = "Lord Kazzak (Blasted Lands)",
    LevelRange = new LevelRange(60, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 0u,
        X: -11785.0f,
        Y: -3203.0f,
        Z: -25.0f,
        NamedLocation: "Lord Kazzak (Blasted Lands)"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 5000,
            Max: 25000,
            ItemId: null,
            FactionId: null),
    ],
    TaskFamily = "Combat",
}
```

### `boss.emerald-dragons`

```csharp
new ActivityDefinition
{
    Id = "boss.emerald-dragons",
    Family = ActivityFamily.WorldBoss,
    Activity = "World boss",
    Location = "Emerald Dragons (rotating)",
    LevelRange = new LevelRange(60, 60),
    FactionPolicy = new FactionPolicy(
        Requirement: FactionRequirement.Either,
        AllowCrossFaction: false),
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
    TravelTarget = new TravelTarget(
        MapId: 1u,
        X: 2767.0f,
        Y: -1672.0f,
        Z: 91.0f,
        NamedLocation: "Emerald Dragons (rotating)"),
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
        new RewardDefinition(
            Kind: RewardKind.Gold,
            Min: 5000,
            Max: 25000,
            ItemId: null,
            FactionId: null),
    ],
    TaskFamily = "Combat",
}
```
