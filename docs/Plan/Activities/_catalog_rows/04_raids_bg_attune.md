# Catalog rows — Raids + Battlegrounds + Attunements (S0.9.4)

Literal `ActivityDefinition` rows for the 7 raid + 3 battleground + 5
attunement catalog ids. Each fenced block is the row a Phase 1 worker
copies into `Services/WoWStateManager/Activities/ActivityCatalog.cs`
under slot `S1.x` for the matching family.

- **Source of canonical names:** [`docs/Plan/Activities/00_INDEX.md`](../00_INDEX.md).
- **`Location` strings** match [`Bot/named-locations.json`](../../../Bot/named-locations.json)
  exactly (resolver loads `(MapId, X, Y, Z)` at request time per
  [`Spec/04_ACTIVITIES.md#location-naming-no-wowzone-enum-required`](../../Spec/04_ACTIVITIES.md#location-naming-no-wowzone-enum-required)).
- **`RequiredCapabilities`** strings match `ServerCapabilities` keys
  in [`Spec/02_STATEMANAGER.md#config-schema`](../../Spec/02_STATEMANAGER.md#config-schema)
  (`Naxx`, `AQ40`, `BWL`, `MC`, `Ony`, `ZG`, `AQ20`, plus the
  `Battlegrounds: ["WSG", "AB", "AV"]` array members for the BG rows).
- **`TaskFamily`** values are heads in
  [`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../../Spec/03_BOTRUNNER.md):
  raids → `"Raid"`, BGs → `"Bg"`, attunements → `"Questing"` (per R16,
  Attunement is not a family head — every chain is realized through
  Questing with secondary Dungeoneering / Economy dependencies).
- **`LockoutPolicy`** uses `LockoutType.PerCharacterWeekly` for
  MC/BWL/AQ40/Naxx; `LockoutType.PerCharacterDaily` for ZG/AQ20/Onyxia;
  `LockoutPolicy.None()` for BGs and attunements (the chain itself has
  no lockout — the gated raid does).
- **`RoleTemplate`** for raids matches the `tT hH dD` triples in
  [`00_INDEX.md#raids`](../00_INDEX.md#raids----see-raidsmd).
- **`HumanJoinPolicy.HumanRole`** is `Leader` for raids + attunements
  (operator drives, with `BotRaidLeader = false`); `Member` for BGs
  (operator is one of N).
- **`HumanIdleTimeout`** is 15 min for raids (long activity, human
  may go AFK between phases), 5 min for BGs (short, fast turnover),
  10 min for attunements (mid-range with dungeon legs).

## Raids

### `raid.zg` — Zul'Gurub (20)

```csharp
new ActivityDefinition
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
        MapId: 309,
        X: -11919.0f,
        Y: -1202.0f,
        Z: 92.0f,
        NamedLocation: "Zul'Gurub"),
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
}
```

### `raid.aq20` — Ruins of Ahn'Qiraj (20)

```csharp
new ActivityDefinition
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
        MapId: 509,
        X: -8417.0f,
        Y: 1500.0f,
        Z: 32.0f,
        NamedLocation: "Ruins of Ahn'Qiraj"),
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
}
```

### `raid.mc` — Molten Core (40)

```csharp
new ActivityDefinition
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
        MapId: 409,
        X: -7515.0f,
        Y: -1041.0f,
        Z: 181.0f,
        NamedLocation: "Molten Core"),
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
}
```

### `raid.onyxia` — Onyxia's Lair (40)

```csharp
new ActivityDefinition
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
        // catalog declares the OR-set (R-pending: see note below).
        RequiredAttunements = ["attune.ony-horde", "attune.ony-alliance"],
        RequiredCapabilities = ["Ony"],
        LockoutPolicy = new LockoutPolicy(LockoutType.PerCharacterDaily, "onyxia"),
    },
    TravelTarget = new TravelTarget(
        MapId: 249,
        X: -4708.0f,
        Y: -3727.0f,
        Z: 55.0f,
        NamedLocation: "Onyxia's Lair"),
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
}
```

### `raid.bwl` — Blackwing Lair (40)

```csharp
new ActivityDefinition
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
        MapId: 469,
        X: -7672.0f,
        Y: -1107.0f,
        Z: 396.0f,
        NamedLocation: "Blackwing Lair"),
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
}
```

### `raid.aq40` — Temple of Ahn'Qiraj (40)

```csharp
new ActivityDefinition
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
        // AQ40 entry is gated server-wide by the Scarab Lord opening, not
        // by a per-character attunement chain. Encoded via the AQ40 capability
        // key — server config asserts `Naxx40Implemented`-style gates here.
        RequiredCapabilities = ["AQ40"],
        LockoutPolicy = new LockoutPolicy(LockoutType.PerCharacterWeekly, "aq40"),
    },
    TravelTarget = new TravelTarget(
        MapId: 531,
        X: -8113.0f,
        Y: 1525.0f,
        Z: 3.0f,
        NamedLocation: "Temple of Ahn'Qiraj"),
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
}
```

### `raid.naxx` — Naxxramas (40)

```csharp
new ActivityDefinition
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
        MapId: 533,
        X: 3392.0f,
        Y: -3380.0f,
        Z: 261.0f,
        NamedLocation: "Naxxramas"),
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
}
```

## Battlegrounds

### `bg.wsg` — Warsong Gulch (10v10)

```csharp
new ActivityDefinition
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
        MapId: 489,
        X: 1519.0f,
        Y: 1481.0f,
        Z: 352.0f,
        NamedLocation: "Warsong Gulch"),
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
        new RewardDefinition(RewardKind.ItemId, Min: 1, Max: 3, ItemId: 20558, FactionId: null), // Mark of Honor — Warsong Gulch
    ],
    TaskFamily = "Bg",
}
```

### `bg.ab` — Arathi Basin (15v15)

```csharp
new ActivityDefinition
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
        MapId: 529,
        X: 1268.0f,
        Y: 1297.0f,
        Z: -16.0f,
        NamedLocation: "Arathi Basin"),
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
        new RewardDefinition(RewardKind.ItemId, Min: 1, Max: 3, ItemId: 20559, FactionId: null), // Mark of Honor — Arathi Basin
    ],
    TaskFamily = "Bg",
}
```

### `bg.av` — Alterac Valley (40v40)

```csharp
new ActivityDefinition
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
        MapId: 30,
        X: -1372.0f,
        Y: -331.0f,
        Z: 89.0f,
        NamedLocation: "Alterac Valley"),
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
        new RewardDefinition(RewardKind.ItemId, Min: 1, Max: 5, ItemId: 20560, FactionId: null), // Mark of Honor — Alterac Valley
    ],
    TaskFamily = "Bg",
}
```

## Attunements

Attunements claim `TaskFamily = "Questing"` per R16 (Attunement is not
a family head — every chain is realized through Questing with
secondary Dungeoneering / Economy dependencies via pushed-child tasks).
`Faction` is `Horde` for `attune.ony-horde`, `Alliance` for
`attune.ony-alliance`, `Either` for the rest.

### `attune.mc` — Molten Core attunement

```csharp
new ActivityDefinition
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
        MapId: 0,
        X: -7510.0f,
        Y: -1036.0f,
        Z: 200.0f,
        NamedLocation: "Molten Core attunement"),
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
}
```

### `attune.ony-horde` — Onyxia Horde chain

```csharp
new ActivityDefinition
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
        MapId: 1,
        X: 1856.0f,
        Y: -4408.0f,
        Z: -16.0f,
        NamedLocation: "Onyxia Horde chain"),
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
}
```

### `attune.ony-alliance` — Onyxia Alliance chain

```csharp
new ActivityDefinition
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
        MapId: 0,
        X: -8442.0f,
        Y: 337.0f,
        Z: 122.0f,
        NamedLocation: "Onyxia Alliance chain"),
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
}
```

### `attune.bwl` — Blackwing Lair attunement

```csharp
new ActivityDefinition
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
        // BWL chain requires UBRS access (Drakkisath orb-click);
        // MC attunement is a typical prereq for raid progression but the
        // chain itself only requires UBRS clear via the BWL capability.
        RequiredAttunements = ["attune.mc"],
        RequiredCapabilities = ["BWL"],
        LockoutPolicy = LockoutPolicy.None(),
    },
    TravelTarget = new TravelTarget(
        MapId: 469,
        X: -7510.0f,
        Y: -1036.0f,
        Z: 200.0f,
        NamedLocation: "Blackwing Lair attunement"),
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
}
```

### `attune.naxx` — Naxxramas attunement

```csharp
new ActivityDefinition
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
        // Attunement chain is the 60g/30g/0g tribute + crystal stockpile
        // turn-in at Light's Hope; the only hard prereq is Argent Dawn
        // standing >= Honored (lowers tribute cost from 60g to 30g, and
        // is required to receive the attunement at any tier).
        RequiredReputations = [new FactionStanding(FactionId: 529, MinStanding: ReputationStanding.Honored)],
        RequiredCapabilities = ["Naxx"],
        LockoutPolicy = LockoutPolicy.None(),
    },
    TravelTarget = new TravelTarget(
        MapId: 0,
        X: 1900.0f,
        Y: -4140.0f,
        Z: 70.0f,
        NamedLocation: "Naxxramas attunement"),
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
    Rewards =
    [
        new RewardDefinition(RewardKind.XpRange, Min: 0, Max: 0, ItemId: null, FactionId: null),
        new RewardDefinition(RewardKind.Gold, Min: 0, Max: 0, ItemId: null, FactionId: null),
    ],
    TaskFamily = "Questing",
}
```

## Questions filed for `Plan/QUESTIONS.md`

- **R-pending — Onyxia attunement OR-set semantics.** `raid.onyxia`
  declares `RequiredAttunements = ["attune.ony-horde", "attune.ony-alliance"]`
  with the expectation that the legality validator interprets it as an
  OR (resolver picks the faction-appropriate row) rather than an AND.
  Spec/04 step 4 ("for each bot candidate … check items, quests,
  reputations, attunements") does not currently specify OR vs AND
  semantics for multi-element `RequiredAttunements`. The catalog row
  is correct under the OR interpretation that matches faction-gated
  attunement chains; spec text should be clarified before the catalog
  test asserts the row.
- **R-pending — `bg.av` AV capability key absent from ServerCapabilities
  array literal example.** `Spec/02_STATEMANAGER.md#config-schema`
  shows `Battlegrounds: ["WSG", "AB", "AV"]` but the matching
  `RequiredCapabilities` value is unspecified — the catalog uses the
  flat key `"AV"` (member of the `Battlegrounds` array) which is the
  natural interpretation. If `ServerCapabilities` membership tests
  unwrap the `Battlegrounds` array, this is correct; otherwise the row
  should use `"Battlegrounds.AV"` (or similar). Same applies to
  `bg.wsg`/`bg.ab`.
- **R-pending — BWL prereq attunement vs capability.** `attune.bwl`
  declares `RequiredAttunements = ["attune.mc"]` because the chain
  step-1 NPC (Scarshield Quartermaster) is reachable only after the bot
  has run UBRS, which in practice requires MC-attunement-grade gear.
  This is a SOFT prereq, not a server-enforced gate; the legality
  validator may want to demote this to a warning. Per
  `Plan/Activities/attunements.md` the only HARD prereq for the
  Blackhand's Command pickup is being inside BRS upper hallway.
- **R-pending — Mark of Honor item IDs.** BG `Rewards` reference item
  IDs `20558` (WSG), `20559` (AB), `20560` (AV) from common WoW 1.12.1
  references. These should be verified against
  `mangos.item_template` before the Phase 2 legality test runs against
  the DB fixture; if a private-server variant uses different ids the
  rows must be patched.
