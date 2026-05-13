# Activities — World Bosses

3 catalog rows. Outdoor 40-man encounters.

## Catalog rows

- `boss.azuregos` — Azshara.
- `boss.kazzak` — Blasted Lands.
- `boss.emerald-dragons` — rotating between Ashenvale, Duskwood,
  Hinterlands, Feralas.

## Task family

| Task | Status |
|---|---|
| `WorldBossEngagementTask` | not-started |
| Reuses `RaidEncounterTask` from `raids.md` |

## Slots

### SWB.1 — Spawn detection

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Bot snapshot exposes nearby world boss spawn. On spawn,
  emit metric `wwow.activity.world_boss_spawn_total{boss=...}`.

### SWB.2 — Azuregos encounter

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Bot/world-bosses/azuregos.json`

### SWB.3 — Kazzak encounter

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Bot/world-bosses/kazzak.json`

### SWB.4 — Emerald Dragon encounter

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Bot/world-bosses/emerald-dragons.json`
- **Goal:** Single encounter spec, dragon-specific portal location
  computed at spawn.

### SWB.5 — LiveValidation

- **Owner:** `monorepo-test-runner`
- **Status:** open

## Task specifications

Each task below follows the Phase 0 precision contract: class
declaration, public surface (current shipped + Phase 1 target per R19),
snapshot reads/writes, BG opcode footprint, FG memory footprint, test
anchor, and catalog `TaskFamily` claim. Where no source exists yet,
`**Planned anchor:**` records the file path a Phase 1 / Phase 2 worker
should create together with the slot id. Catalog references resolve
through [`Plan/Activities/00_INDEX.md`](00_INDEX.md).

A world-boss kill is a composite activity: the **engagement** itself is
a Combat-family responsibility (rotation, pull, threat, mechanics
response on the boss unit), while the **formation, ready-check,
master-loot, and 40-man composition** are reused verbatim from the Raid
family in [`raids.md`](raids.md), and **travel to the outdoor spawn**
is delegated to the Travel family in [`travel.md`](travel.md). The
orchestrator `WorldBossEngagementTask` sequences these pieces; the
per-boss subclasses parameterize spawn coordinates, mechanics, and the
specific boss entry id consumed by `EncounterDefinition`.

### WorldBossEngagementTask

- **Class declaration** — `public sealed class WorldBossEngagementTask : BotTask, IBotTask`
  in namespace `BotRunner.Tasks.WorldEvent` (sibling to
  `StvFishingExtravaganzaTask` per Spec/03 family table). Per-boss
  subclasses live in `BotRunner.Tasks.WorldEvent.Bosses`.
  **Planned anchor:** `Exports/BotRunner/Tasks/WorldEvent/WorldBossEngagementTask.cs`
  with per-boss subclasses under
  `Exports/BotRunner/Tasks/WorldEvent/Bosses/<Boss>EngagementTask.cs`.
  Status: `not-started`. No file exists in the tree today; grep for
  `WorldBoss`, `Azuregos`, `Kazzak`, `EmeraldDragon` returns zero
  source-code matches across `Exports/`, `Services/`, `BotProfiles/`,
  and `Tests/`. The only repo asset for this family today is
  `Config/activities/boss.azuregos.json` (the activity definition
  carrying loadout / role template / staging location).
- **Public surface — current shipped surface:** none. The
  `IBotTask` substrate is now the Phase 1 contract from
  `Exports/BotRunner/Interfaces/IBotTask.cs` (`TickAsync` +
  `OnPushedAsync` + `OnPoppedAsync` + `OnChildFailedAsync`, plus
  `Name`/`Status`), implemented via the `BotTask` async shim
  (S1.0/R25 — `TickAsync` → `OnTick` → legacy `Update()` body). The
  base `BotTask(IBotContext botContext)` lives at
  `Exports/BotRunner/Tasks/BotTask.cs` with its `PopTask(string reason)`
  helper.
  **Public surface — Phase 1 target (per Spec/03 + R19):**
  ```csharp
  WorldBossEngagementTask(
      IBotContext context,
      WorldBossDefinition boss,           // spawn map+coords, boss entry id, mechanic table
      RaidFormationPlan formation,        // reused from raids.md
      RaidCompositionService.RaidRole role,
      ulong playerGuid)
  ```
  Standard `IBotTask` overrides `Name`, `Status`, `IsComplete`,
  `IsFailed`, `TickAsync`, `OnPushedAsync`, `OnPoppedAsync`,
  `OnChildFailedAsync`. Internal state machine: `Awaiting Spawn` →
  `Traveling` → `Forming Raid` → `Ready Check` → `Engaging` →
  `Looting` → `Complete`. Pushes (in order): `TravelTask` (to staging
  location from `Config/activities/boss.<id>.json::StagingLocation`),
  `ReadyCheckTask` (raid-leader bot only, role=Initiator;
  raid-members role=Responder), `RaidPositioningTask`,
  `PullTargetTask` (raid leader pulls; per-profile PvE form from
  [`combat.md`](combat.md)), `PvERotationTask` for the duration of the
  fight, `MasterLootTask` on boss death (raid leader only). No public
  methods beyond the `IBotTask` shape.
- **Snapshot reads/writes** — Reads
  `WoWActivitySnapshot.Player.Unit.GameObject.Base.Position` and
  `currentMapId` (field 20) to gate the "Traveling" state's exit
  condition; `NearbyUnits[*]` (field 7) filtered by the boss creature
  entry id from `WorldBossDefinition` to detect the spawned boss;
  `PartyLeaderGuid` (field 11) to drive raid-leader-only branches
  (ready-check initiation, pull, master-loot); `previousAction` /
  `currentAction` (fields 4-5) to confirm the staging
  `ActionType.Travel` action acked before pushing the raid pipeline;
  `recentChatMessages` (field 12) for `.spawn` / `.respawn` GM-command
  echoes during testing.
  **Writes:** none directly into the proto. Per Spec/03's "tasks
  never touch `WoWActivitySnapshot` directly" rule, intent is
  inferred from `IObjectManager`-driven state transitions in
  subsequent ticks. Per slot **SWB.1**, the metric
  `wwow.activity.world_boss_spawn_total{boss=...}` is emitted via the
  metrics sink on `BotTaskContext` (`IMetricsSink`) when the task
  observes the boss creature in `NearbyUnits` for the first time;
  this is a metric write, not a snapshot field write.
- **BG opcodes** — The task itself emits no opcodes. Subtasks reach
  the wire as follows: `TravelTask` →
  `MSG_MOVE_HEARTBEAT = 0x0EE`, `MSG_MOVE_START_FORWARD`,
  `MSG_MOVE_STOP` (movement); `ReadyCheckTask` →
  `MSG_RAID_READY_CHECK = 0x322` (initiate, leader-only),
  `MSG_RAID_READY_CHECK_CONFIRM = 0x3AE` (respond),
  `MSG_RAID_READY_CHECK_FINISHED = 0x3C5` (completion);
  `PullTargetTask` / `PvERotationTask` →
  `CMSG_CAST_SPELL = 0x12E` and `CMSG_ATTACKSWING = 0x141` (plus
  SMSG variants `SMSG_ATTACKSWING_NOTINRANGE = 0x145`,
  `SMSG_ATTACKSWING_BADFACING = 0x146` etc. observed via incoming
  stream); `MasterLootTask` → `CMSG_LOOT = 0x15D`,
  `CMSG_LOOT_MASTER_GIVE = 0x2A3`, `CMSG_LOOT_RELEASE = 0x15F`,
  `CMSG_LOOT_METHOD = 0x07A` (set once at formation). Opcode
  constants live at `Exports/GameData.Core/Enums/Opcode.cs`.
- **FG calls** — Reads via `IObjectManager` only — no direct memory
  reads. Specifically: `IObjectManager.Units` filtered by the
  `WorldBossDefinition.BossEntry` for spawn detection;
  `IObjectManager.Player` for leader-identity and position;
  `IObjectManager.Players` for raid-member presence/health while
  positioning. Movement, ready-check, combat, and loot are delegated
  to the standard subtasks (see `RaidPositioningTask`,
  `ReadyCheckTask`, `PullTargetTask`, `PvERotationTask`,
  `MasterLootTask` task specs in [`raids.md`](raids.md) and
  [`combat.md`](combat.md)). No Lua bridge required at this level;
  raid-target marking (`SetRaidTarget`) is leader-only and lives in
  `RaidPositioningTask` not here.
- **Test anchor** — **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/WorldBosses/WorldBossEngagementTests.cs::WorldBoss_Engagement_RaidFormsTravelsPullsAndLoots`.
  Filter: `dotnet test --filter "FullyQualifiedName~WorldBossEngagementTests"
  --configuration Release`. Status: `not-started`. No matching test
  exists today; `Tests/BotRunner.Tests/LiveValidation/Raids/` contains
  only `RaidEntryTests.cs`, `RaidCoordinationTests.cs`, and
  `RaidCollections.cs` — none reference world bosses. Per slot
  **SWB.5**, the LiveValidation harness must `.spawn` (or
  `.npc add`) the target boss creature at the staging location
  before dispatching `ActionType.AssignActivity` with the
  `boss.<id>` activity id; the full encounter is gated on Phase 2
  OnDemand spawn/gear infrastructure per
  [`Spec/04_ACTIVITIES.md`](../../Spec/04_ACTIVITIES.md) for the same
  reason `RaidEncounterTask` is in [`raids.md`](raids.md).
- **Catalog `TaskFamily` claim** — `Combat` (the engagement is a
  Combat-family responsibility per the slot brief); secondary
  coordination is `Raid` (formation, ready-check, master-loot). Per
  R16 both claims are members of the fixed family-head list in
  [`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../../Spec/03_BOTRUNNER.md#catalog-of-task-families).
  Note: Spec/03's family table currently lists
  `WorldBossEngagementTask` under the `World-event` row alongside
  `StvFishingExtravaganzaTask`; that placement is the code-locality
  hint (shared `BotRunner.Tasks.WorldEvent` namespace), while the
  catalog row's `TaskFamily` is the activity-classification hint
  used by the catalog test and the DecisionEngine. Cross-referenced
  rows from [`Plan/Activities/00_INDEX.md`](00_INDEX.md):
  `boss.azuregos`, `boss.kazzak`, `boss.emerald-dragons`.

### AzuregosEngagementTask

- **Class declaration** — `public sealed class AzuregosEngagementTask : WorldBossEngagementTask`
  in namespace `BotRunner.Tasks.WorldEvent.Bosses`.
  **Planned anchor:** `Exports/BotRunner/Tasks/WorldEvent/Bosses/AzuregosEngagementTask.cs`
  per slot **SWB.2**. Status: `not-started`. No file exists today.
- **Public surface — current shipped surface:** none.
  **Phase 1 target:** subclass constructor
  `AzuregosEngagementTask(IBotContext context, RaidFormationPlan formation, RaidCompositionService.RaidRole role, ulong playerGuid)`
  that materializes a fixed `WorldBossDefinition` for Azuregos:
  spawn map = Azeroth (mapId 0), spawn position ≈ `(3300, -4750,
  174)` in Azshara, boss entry id and mechanic table TBD by slot
  SWB.2 (creature entry id is the Azuregos row in
  `mangos.creature_template`). All other behavior inherits from
  `WorldBossEngagementTask`.
- **Snapshot reads/writes** — inherited. The Azuregos-specific reads
  are scoped via the parent's `WorldBossDefinition.BossEntry` filter
  on `NearbyUnits` and the parent's distance-to-staging gate on
  `Player.Unit.GameObject.Base.Position` against `(3300, -4750, 174)`
  in mapId 0.
- **BG opcodes** — inherited (no boss-specific opcodes). The
  staging-location `TravelTask` cross-zone resolution may push a
  `TakeFlightPathTask` from the nearest Horde/Alliance flightmaster
  to Azshara, which emits taxi-related opcodes documented in
  [`travel.md`](travel.md).
- **FG calls** — inherited. Boss-specific mechanic response (frost
  breath positioning, mass-polymorph dispel/cleanse) is encoded as
  data in the inherited `EncounterDefinition`, not as Azuregos-specific
  FG calls.
- **Test anchor** — **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/WorldBosses/AzuregosEngagementTests.cs::Azuregos_Engagement_RaidKillsBossAndLoots`.
  Filter: `dotnet test --filter "FullyQualifiedName~AzuregosEngagementTests"`.
  Status: `not-started`. Existing scaffold for activity config:
  `Config/activities/boss.azuregos.json` (this file ships today and
  carries `StagingLocation: "Azuregos (Azshara)"`, `LootPolicy:
  "MasterLoot"`, 3T/8H/29D role template, and the prot-warrior
  loadout for tank bots).
- **Catalog `TaskFamily` claim** — `Combat` + secondary `Raid`,
  inherited from the orchestrator. Catalog row:
  `boss.azuregos` in [`Plan/Activities/00_INDEX.md`](00_INDEX.md).

### KazzakEngagementTask

- **Class declaration** — `public sealed class KazzakEngagementTask : WorldBossEngagementTask`
  in namespace `BotRunner.Tasks.WorldEvent.Bosses`.
  **Planned anchor:** `Exports/BotRunner/Tasks/WorldEvent/Bosses/KazzakEngagementTask.cs`
  per slot **SWB.3**. Status: `not-started`. No file exists today.
  Activity config `Bot/world-bosses/kazzak.json` (per the SWB.3
  owned-paths fragment) does not exist in the repo today either —
  only `Config/activities/boss.azuregos.json` is present.
- **Public surface — current shipped surface:** none.
  **Phase 1 target:** subclass constructor
  `KazzakEngagementTask(IBotContext context, RaidFormationPlan formation, RaidCompositionService.RaidRole role, ulong playerGuid)`
  that materializes a fixed `WorldBossDefinition` for Lord Kazzak:
  spawn map = Azeroth (mapId 0), spawn position ≈ `(-11900, -3210,
  -8)` in the Tainted Scar, Blasted Lands; boss entry id is the
  Lord Kazzak row in `mangos.creature_template` (TBD by slot
  SWB.3).
- **Snapshot reads/writes** — inherited. Kazzak-specific reads
  scoped via the parent's `BossEntry` filter on `NearbyUnits` and
  the parent's distance gate against `(-11900, -3210, -8)` in
  mapId 0. Tainted Scar is a sub-zone of Blasted Lands; the parent
  task's `currentMapId` (field 20) check covers the map; the
  position gate enforces sub-zone proximity.
- **BG opcodes** — inherited. Kazzak's Void Bolt / Mark of Kazzak /
  Shadow Volley mechanic responses are dispatched as
  `CMSG_CAST_SPELL = 0x12E` from the rotation, not as
  Kazzak-specific opcodes.
- **FG calls** — inherited. Mark-of-Kazzak dispel branching is
  encoded in the inherited `EncounterDefinition` mechanic table per
  `EncounterMechanicsTask` (see
  [`raids.md#raidencountertask`](raids.md#raidencountertask)).
- **Test anchor** — **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/WorldBosses/KazzakEngagementTests.cs::Kazzak_Engagement_RaidKillsBossAndLoots`.
  Filter: `dotnet test --filter "FullyQualifiedName~KazzakEngagementTests"`.
  Status: `not-started`. No `Bot/world-bosses/kazzak.json` exists
  yet; activity-config authoring is part of slot **SWB.3**.
- **Catalog `TaskFamily` claim** — `Combat` + secondary `Raid`,
  inherited. Catalog row: `boss.kazzak` in
  [`Plan/Activities/00_INDEX.md`](00_INDEX.md).

### EmeraldDragonEngagementTask

- **Class declaration** — `public sealed class EmeraldDragonEngagementTask : WorldBossEngagementTask`
  in namespace `BotRunner.Tasks.WorldEvent.Bosses`.
  **Planned anchor:** `Exports/BotRunner/Tasks/WorldEvent/Bosses/EmeraldDragonEngagementTask.cs`
  per slot **SWB.4**. Status: `not-started`. No file exists today.
- **Public surface — current shipped surface:** none.
  **Phase 1 target:** subclass constructor
  `EmeraldDragonEngagementTask(IBotContext context, RaidFormationPlan formation, RaidCompositionService.RaidRole role, ulong playerGuid)`.
  Unlike the single-spawn bosses Azuregos/Kazzak, this subclass
  resolves the active dragon and its spawn portal **at task
  push-time** by querying the server for the currently-up dragon
  (one of Lethon, Emeriss, Taerar, Ysondre) via the
  `mangos.creature` table (Phase 2 DB hint) or by polling
  `IObjectManager.Units` across the four candidate zones during the
  parent's `Awaiting Spawn` state. Candidate spawn portals (mapId 0
  for all):
  - **Duskwood** — Twilight Grove portal, near `(-10100, -1550, 40)`.
  - **Ashenvale** — Bough Shadow portal, near `(3300, 600, 6)`.
  - **The Hinterlands** — Seradane portal, near `(40, -3500, 130)`.
  - **Feralas** — Dream Bough portal, near `(-3260, 1900, 25)`.
  Exact spawn coordinates are TBD by slot SWB.4 from the live
  `mangos.creature` row at runtime. The active dragon's identity and
  mechanic table populate the inherited `WorldBossDefinition` and
  `EncounterDefinition` once detected.
- **Snapshot reads/writes** — inherited. The Emerald-Dragon subclass
  additionally polls `NearbyUnits[*]` filtered by the four candidate
  creature entry ids (Lethon, Emeriss, Taerar, Ysondre) across the
  four candidate zones during the `Awaiting Spawn` state; the
  metric emitted under slot **SWB.1**
  (`wwow.activity.world_boss_spawn_total{boss=...}`) carries the
  resolved dragon name as the `boss` label. Per slot SWB.4, the
  dragon-specific portal location is **computed at spawn**, not
  hardcoded in the subclass.
- **BG opcodes** — inherited. The Sleep / Noxious Breath / Aura of
  Nature / Acid Breath mechanic responses dispatch via the same
  `CMSG_CAST_SPELL = 0x12E` rotation path; dispel-Sleep branching
  is encoded in the inherited mechanic table.
- **FG calls** — inherited. Cross-zone travel during the
  spawn-resolution poll is a Travel-family responsibility (see
  [`travel.md`](travel.md)) — the bot does not roam between
  candidate zones until a dragon is detected; instead it stages at
  the activity row's `StagingLocation` (TBD per `Bot/world-bosses/emerald-dragons.json`)
  and the StateManager-side spawn-detection signal (slot SWB.1)
  drives the activity-id selection per-dragon.
- **Test anchor** — **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/WorldBosses/EmeraldDragonEngagementTests.cs::EmeraldDragon_Engagement_DetectsActiveDragonAndKills`.
  Filter: `dotnet test --filter "FullyQualifiedName~EmeraldDragonEngagementTests"`.
  Status: `not-started`. No `Bot/world-bosses/emerald-dragons.json`
  exists yet; the single-encounter-spec / per-dragon-portal-resolution
  design is part of slot **SWB.4**.
- **Catalog `TaskFamily` claim** — `Combat` + secondary `Raid`,
  inherited. Catalog row: `boss.emerald-dragons` in
  [`Plan/Activities/00_INDEX.md`](00_INDEX.md). The single catalog
  row covers all four dragons; per-dragon dispatch is a runtime
  branch on the spawn-detection signal, not a separate catalog row.

## Cross-references

- **Combat** — [`combat.md`](combat.md). The engagement itself
  (rotation, pull, threat, mechanic response on the boss unit)
  reuses `PullTargetTask`, `PvERotationTask`, `RestTask`, `BuffTask`,
  `HealTask` per the 27 class/spec profiles documented in
  `combat.md::Task specifications`.
- **Raid** — [`raids.md`](raids.md). World-boss formation,
  ready-check, master-loot, and 40-man composition reuse
  `RaidPositioningTask`, `RaidEncounterTask`, `MasterLootTask`,
  `ReadyCheckTask` from `raids.md::Task specifications`. Slot
  `SR.common.1` (`RaidCompositionService`) supplies the role
  template; world-boss role templates live in
  `Config/activities/boss.<id>.json::RoleOverrides`.
- **Travel** — [`travel.md`](travel.md). Cross-zone movement to the
  boss staging location uses `TravelTask`, `MountAndGoToTask`,
  `TakeFlightPathTask`, and `BoardTransportTask` per `travel.md`.
  Activity row `StagingLocation` string resolves through the named
  locations table referenced by Spec/03's Activity resolver.

## Failure recovery

- **Other faction contesting** → world PvP rules from `pvp.md`.
- **Wipe** → corpse run, reset, retry once if boss still up.
