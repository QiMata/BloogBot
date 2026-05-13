# Activities — Raids

7 catalog rows (ZG, AQ20, MC, Onyxia, BWL, AQ40, Naxx). 20-man + 40-man.
Attunement gates. Master loot. Long-running activities (1.5 - 5h).

## Required task families

| Task | Status |
|---|---|
| `RaidPositioningTask` | not-started — per-encounter positioning per role |
| `RaidEncounterTask` | per-boss; all not-started |
| `MasterLootTask` | not-started — group leader distributes loot |
| `ReadyCheckTask` | not-started — synchronized start gate |
| `WorldBuffPickupTask` | not-started — DM tribute, Songflower, Onyxia/Nef head, Rend |

Phase 1 scope per
[`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md#s114`](../02_PHASE1_ACTION_TASK_FOUNDATION.md):
the Raid family lands as **formation + ready-check only, no encounter
scripts**. Encounter task families wait until Phase 2 OnDemand-grade
spawn/gear is in place; without GM-applied gear and attunement, encounter
fights aren't testable end-to-end. The four task specifications below
are therefore expected to be `not-started` until Phase 2 in the case of
`RaidEncounterTask` and `RaidPositioningTask`, and `not-started` in
Phase 1 for `MasterLootTask` / `ReadyCheckTask` (slots SR.common.2 /
SR.common.3 are the Phase 1 work).

## Task specifications

Each task below follows the Phase 0 precision contract: class
declaration, public surface, snapshot contract, BG protocol footprint,
FG memory footprint, test anchor, and catalog `TaskFamily` claim. Where
no source exists yet, `**Planned anchor:**` records the file path a
Phase 1 / Phase 2 worker should create, together with the slot id.
Catalog references resolve through
[`Plan/Activities/00_INDEX.md`](00_INDEX.md).

### RaidPositioningTask

- **Class declaration** — `public sealed class RaidPositioningTask : BotTask, IBotTask` in
  namespace `BotRunner.Tasks.Raid`.
  **Planned anchor:** `Exports/BotRunner/Tasks/Raid/RaidPositioningTask.cs`.
  Status: `not-started`. No file exists in the tree today; adjacent
  `EncounterMechanicsTask` lives in the same folder but is a separate
  per-mechanic responder, not a positioning task. Phase 1 S1.14
  explicitly defers positioning until encounter scripts begin.
- **Public surface — current shipped:** none — planned anchor: `Exports/BotRunner/Tasks/Raid/RaidPositioningTask.cs`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Public surface (planned task-specific shape):**
  `RaidPositioningTask(IBotContext context, RaidFormationPlan plan, RaidCompositionService.RaidRole role, ulong playerGuid)`;
  the standard `IBotTask` overrides `Name`, `Status`, `IsComplete`,
  `IsFailed`, `TickAsync`, `OnPushedAsync`, `OnPoppedAsync`,
  `OnChildFailedAsync` per [`Spec/03_BOTRUNNER.md#ibottask-interface`](../../Spec/03_BOTRUNNER.md#ibottask-interface);
  may push `GoToTask` for long-distance repositioning. No new public
  methods beyond the IBotTask shape.
- **Snapshot contract** — reads `WoWActivitySnapshot.PartyLeaderGuid`,
  `Player.Unit.GameObject.Base.Position`, `NearbyUnits[*]` (for melee
  vs ranged separation checks), and `RaidRole` derived from
  `RaidCompositionService.AssignRoles`. Writes nothing directly to
  the snapshot proto; positioning intent is observed through the bot's
  own `Position` delta in subsequent ticks. The task does not extend
  the proto; per [`Spec/13_TESTING.md`](../../Spec/13_TESTING.md) tests
  assert via position polling.
- **BG protocol footprint** — emits no opcodes itself. Movement is
  delegated to `MovementController` which already issues
  `MSG_MOVE_*` opcodes (`MSG_MOVE_HEARTBEAT`,
  `MSG_MOVE_START_FORWARD`, `MSG_MOVE_STOP`). If the formation plan
  embeds raid-target marks for spread anchors, mark setting is the
  responsibility of the raid leader bot via `CMSG_RAID_TARGET_UPDATE`;
  this task only reads marks, never writes them.
- **FG memory footprint** — `IObjectManager.MoveToward(Position pos)`
  and `IObjectManager.Face(Position pos)`
  (`Services/ForegroundBotRunner/Statics/ObjectManager.PlayerState.cs`)
  drive locomotion. Reads `IObjectManager.Players` for ally
  proximity. No Lua bridge required for positioning; raid-mark Lua
  (`SetRaidTarget` in `ObjectManager.Interaction.cs:97`) is leader-only
  and not invoked here.
- **Test anchor** — **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/Raids/RaidPositioningTests.cs::Raid_Positioning_RoleAssignedSlotReached`.
  Filter: `dotnet test --filter "FullyQualifiedName~RaidPositioningTests"
  --configuration Release`. Status: `not-started`. No matching test
  exists today; `RaidCoordinationTests.cs` covers ready-check / subgroup
  / mark / loot-rule prerequisites only, not positioning.
- **Catalog `TaskFamily` claim** — `Raid`. Cross-referenced rows from
  [`Plan/Activities/00_INDEX.md`](00_INDEX.md): `raid.zg`, `raid.aq20`,
  `raid.mc`, `raid.onyxia`, `raid.bwl`, `raid.aq40`, `raid.naxx`.

### RaidEncounterTask

- **Class declaration** — `public sealed class RaidEncounterTask : BotTask, IBotTask`
  in namespace `BotRunner.Tasks.Raid` (or `BotRunner.Tasks.Raid.Encounters.<Boss>`
  for per-boss subclasses).
  **Planned anchor:** `Exports/BotRunner/Tasks/Raid/RaidEncounterTask.cs`
  (orchestrator) plus per-boss encounter classes under
  `Exports/BotRunner/Tasks/Raid/Encounters/<Wing>/<Boss>EncounterTask.cs`
  matching the owned-paths fragment declared by slot `SR.zg`
  (`Exports/BotRunner/Tasks/Raid/Encounters/Zg/**`). Status:
  `not-started`. The closest existing file is the data-driven
  `EncounterMechanicsTask` at
  `Exports/BotRunner/Tasks/Raid/EncounterMechanicsTask.cs`, which is a
  per-tick mechanic responder, not the encounter-scope orchestrator
  this slot specifies.
- **Public surface — current shipped:** none — planned anchor: `Exports/BotRunner/Tasks/Raid/RaidEncounterTask.cs` (orchestrator) plus per-boss encounter classes under `Exports/BotRunner/Tasks/Raid/Encounters/<Wing>/<Boss>EncounterTask.cs`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Public surface (planned task-specific shape):**
  `RaidEncounterTask(IBotContext context, EncounterDefinition encounter, RaidCompositionService.RaidRole role, ulong playerGuid)`;
  standard `IBotTask` overrides. Internally pushes `RaidPositioningTask`,
  `EncounterMechanicsTask`, and `PvERotationTask` per phase. No public
  methods beyond the IBotTask shape; per-encounter phase tables loaded
  from the `EncounterDefinition` record already declared in
  `EncounterMechanicsTask.cs`.
- **Snapshot contract** — reads
  `WoWActivitySnapshot.Player.Unit.{Health,Mana,Buffs,Debuffs}`,
  `NearbyUnits[*]` to find the boss by `EncounterDefinition.BossEntry`,
  `PartyLeaderGuid` to confirm raid leadership, and party-member health
  via `NearbyUnits` filtered to the player faction. Writes no new
  snapshot fields directly; encounter progress is inferred from boss
  presence/health deltas across ticks. Per
  [`Spec/04_ACTIVITIES.md`](../../Spec/04_ACTIVITIES.md) the encounter
  task uses the existing `WoWActivitySnapshot` shape — encounter-level
  schema additions are a separate spec PR.
- **BG protocol footprint** — does not emit raid-specific opcodes
  directly. Combat actions delegate to `PvERotationTask` (spell casts
  via `CMSG_CAST_SPELL = 0x12E` from `BotProfiles/`). Movement delegates
  through `MovementController`. Loot release at encounter end is
  handled by the downstream `MasterLootTask` via `CMSG_LOOT = 0x15D` /
  `CMSG_LOOT_MASTER_GIVE = 0x2A3`. Reading raid-target icons set by the
  leader uses passive `SMSG_RAID_TARGET_UPDATE` observation; no
  outbound traffic.
- **FG memory footprint** — `IObjectManager.Units` (filtered to boss
  entry), `IObjectManager.Players` (raid member health/position),
  `IObjectManager.Player` (local state). Spell invocation is via the
  combat rotation, not direct Lua. Boss-cast observation uses the
  `IWoWUnit.IsCasting` / `ChannelingId` properties already consumed
  by `EncounterMechanicsTask`.
- **Test anchor** — **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/Raids/RaidEncounterTests.cs::Raid_Encounter_BossDefeatedFromCleanPull`.
  Filter: `dotnet test --filter "FullyQualifiedName~RaidEncounterTests"
  --configuration Release`. Status: `not-started`. Today
  `Tests/BotRunner.Tests/LiveValidation/Raids/RaidEntryTests.cs` only
  validates `*_RaidFormAndEnter` (formation + instance entry) via the
  shared `DungeonEntryTestRunner`; no encounter-level test exists yet.
  Encounter tests gated on Phase 2 OnDemand spawn/gear per
  [`Spec/04_ACTIVITIES.md`](../../Spec/04_ACTIVITIES.md).
- **Catalog `TaskFamily` claim** — `Raid`. Cross-referenced rows from
  [`Plan/Activities/00_INDEX.md`](00_INDEX.md): `raid.zg`, `raid.aq20`,
  `raid.mc`, `raid.onyxia`, `raid.bwl`, `raid.aq40`, `raid.naxx`.

### MasterLootTask

- **Class declaration** — `public sealed class MasterLootTask : BotTask, IBotTask`
  in namespace `BotRunner.Tasks.Raid`.
  **Planned anchor:** `Exports/BotRunner/Tasks/Raid/MasterLootTask.cs`
  per slot `SR.common.2`. Status: `not-started`. A closely related
  implementation exists today as `MasterLootDistributionTask` at
  `Exports/BotRunner/Tasks/Raid/MasterLootDistributionTask.cs` — that
  class encodes the priority-list distribution loop. The Phase 1 slot
  calls for the canonical `MasterLootTask` name; whether to rename
  `MasterLootDistributionTask` or wrap it is the implementing worker's
  call within slot `SR.common.2`'s owned-paths fragment
  (`Exports/BotRunner/Tasks/Raid/MasterLootTask.cs`).
- **Public surface — current shipped:** related implementation exists as `MasterLootDistributionTask` at `Exports/BotRunner/Tasks/Raid/MasterLootDistributionTask.cs` exposing constructor + `Update()` (legacy task shape). No `MasterLootTask` class today — planned anchor: `Exports/BotRunner/Tasks/Raid/MasterLootTask.cs`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Public surface (planned task-specific shape):**
  `MasterLootTask(IBotContext context, IReadOnlyList<LootPriorityEntry> priorityList)`;
  standard `IBotTask` overrides. The existing
  `MasterLootDistributionTask` exposes only the constructor + `Update()`
  (legacy task shape). The new task must conform to the
  `TickAsync`/`OnPushedAsync`/`OnPoppedAsync`/`OnChildFailedAsync`
  contract in [`Spec/03_BOTRUNNER.md`](../../Spec/03_BOTRUNNER.md).
  `LootPriorityEntry(uint ItemId, ulong PlayerGuid, int Priority, string Reason)`
  is reused from `MasterLootDistributionTask.cs`.
- **Snapshot contract** — reads `WoWActivitySnapshot.PartyLeaderGuid`
  (must equal local player GUID to act as master looter),
  `NearbyUnits[*].Loot` (when populated) for loot-window content, and
  the existing `Player.Unit.GameObject.Base.Guid` for self-identity.
  Writes nothing new; the loot-window state is read through
  `IObjectManager.LootFrame` rather than a snapshot field. The existing
  proto field `partyLeaderGuid` (field 11 in
  `Exports/BotCommLayer/Models/ProtoDef/communication.proto`) is the
  authority for "am I the loot manager?" checks.
- **BG protocol footprint** — `CMSG_LOOT = 0x15D` to open the corpse
  loot window, `CMSG_LOOT_MASTER_GIVE = 0x2A3` to assign an item to a
  recipient, `CMSG_LOOT_RELEASE = 0x15F` on completion. Loot-method
  changes (e.g. ensuring the group is in master-loot mode) use
  `CMSG_LOOT_METHOD = 0x07A` and are typically applied once at raid
  formation, not per-encounter. Action enums `ASSIGN_LOOT = 22`,
  `PROMOTE_LOOT_MANAGER = 20`, `SET_GROUP_LOOT = 21` in
  `Exports/BotCommLayer/Models/ProtoDef/communication.proto`. Wire
  layer: `LootingNetworkClientComponent` in
  `Exports/WoWSharpClient/Networking/ClientComponents/`.
- **FG memory footprint** — `IObjectManager.LootFrame` (read of
  `ILootFrame.IsOpen`, `LootCount`, `LootItems`) and
  `IObjectManager.AssignLoot(int itemId, ulong playerGuid)` (declared
  at `Exports/GameData.Core/Interfaces/IObjectManager.cs:172`). No
  Lua bridge required — the FG `AssignLoot` implementation invokes
  the in-process master-give path directly.
- **Test anchor** —
  `Tests/BotRunner.Tests/LiveValidation/Raids/RaidCoordinationTests.cs::Raid_LootRules_CorrectDistribution`.
  Filter: `dotnet test --filter "FullyQualifiedName~RaidCoordinationTests"
  --configuration Release`. Today this test exercises `ActionType.AssignLoot`
  with the group-loot setting only — it asserts loot-method assignment,
  not full master-loot distribution. **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/Raids/MasterLootTests.cs::MasterLoot_DistributeBossLoot_PriorityHonored`
  for the full distribution loop once a boss can be downed (Phase 2).
- **Catalog `TaskFamily` claim** — `Raid`. Cross-referenced rows from
  [`Plan/Activities/00_INDEX.md`](00_INDEX.md): `raid.zg`, `raid.aq20`,
  `raid.mc`, `raid.onyxia`, `raid.bwl`, `raid.aq40`, `raid.naxx`.

### ReadyCheckTask

- **Class declaration** — `public sealed class ReadyCheckTask : BotTask, IBotTask`
  in namespace `BotRunner.Tasks.Raid`.
  **Planned anchor:** `Exports/BotRunner/Tasks/Raid/ReadyCheckTask.cs`
  per slot `SR.common.3`. Status: `not-started`. No file exists today.
  The wire-level plumbing
  (`PartyNetworkClientComponent.InitiateReadyCheckAsync` and
  `RespondToReadyCheckAsync` in
  `Exports/WoWSharpClient/Networking/ClientComponents/PartyNetworkClientComponent.cs:587-618`)
  is already in place; this task is the IBotTask wrapper that drives
  it from the raid leader (initiate) and from raid members (auto-respond).
- **Public surface — current shipped:** none — planned anchor: `Exports/BotRunner/Tasks/Raid/ReadyCheckTask.cs`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Public surface (planned task-specific shape):**
  `ReadyCheckTask(IBotContext context, ReadyCheckRole role, TimeSpan timeout)`
  where `ReadyCheckRole = Initiator | Responder`; standard `IBotTask`
  overrides. Internal state machine: `Initiator` → send opcode → wait
  for `MSG_RAID_READY_CHECK_FINISHED` → emit completion;
  `Responder` → wait for `MSG_RAID_READY_CHECK` → respond with the
  bot's own readiness (health-full + buffs-applied predicate) → wait
  for `MSG_RAID_READY_CHECK_FINISHED`.
- **Snapshot contract** — reads `WoWActivitySnapshot.PartyLeaderGuid`
  (gates Initiator role on leader identity), the player's
  `Player.Unit.Health` / `Mana` (auto-readiness predicate), and
  `Player.Unit.Buffs` (key consumables/world-buffs applied). Writes
  no new snapshot field; ready-check completion is observed in the
  test via the existing wire-event stream and post-check group state
  (raid still intact, no one was kicked).
- **BG protocol footprint** — `MSG_RAID_READY_CHECK = 0x322` (initiate;
  leader-only), `MSG_RAID_READY_CHECK_CONFIRM = 0x3AE` (respond),
  `MSG_RAID_READY_CHECK_FINISHED = 0x3C5` (completion event observed),
  `SMSG_RAID_READY_CHECK_ERROR = 0x407` (failure path). Opcode constants
  defined in `Exports/GameData.Core/Enums/Opcode.cs:807,854,858,891`.
  Wire implementation:
  `PartyNetworkClientComponent.InitiateReadyCheckAsync` /
  `RespondToReadyCheckAsync` (`Exports/WoWSharpClient/Networking/ClientComponents/PartyNetworkClientComponent.cs:587,609`)
  with the observable streams `ReadyCheckRequests`, `ReadyCheckResponses`,
  `ReadyCheckFinished` exposed on `IPartyNetworkClientComponent`.
- **FG memory footprint** — no FG-specific Lua or memory bridge needed
  for the network exchange (the WoWSharpClient path is used by both
  modes). Today `RaidCoordinationTests.Raid_ReadyCheck_AllBotsRespond`
  drives the FG path via GM chat (`.readycheck`) rather than via a
  task; the `ReadyCheckTask` is the IBotTask form of the same flow.
  No `IObjectManager` calls required beyond identity reads.
- **Test anchor** —
  `Tests/BotRunner.Tests/LiveValidation/Raids/RaidCoordinationTests.cs::Raid_ReadyCheck_AllBotsRespond`.
  Filter: `dotnet test --filter "FullyQualifiedName~RaidCoordinationTests"
  --configuration Release`. Today this test asserts post-readycheck
  raid integrity via `PartyLeaderGuid != 0` on both bots; once
  `ReadyCheckTask` exists the assertion can tighten to "task reports
  Complete with all-ready result." **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/Raids/ReadyCheckTaskTests.cs::ReadyCheck_AllRespondersConfirmReady`
  if the slot author prefers a task-scoped suite over extending the
  coordination test.
- **Catalog `TaskFamily` claim** — `Raid`. Cross-referenced rows from
  [`Plan/Activities/00_INDEX.md`](00_INDEX.md): `raid.zg`, `raid.aq20`,
  `raid.mc`, `raid.onyxia`, `raid.bwl`, `raid.aq40`, `raid.naxx`.

## Coordinator: `RaidCoordinator`

Per [`Plan/04_PHASE3_BOT_LEASE_SCHEDULER.md#s34--raidcoordinator`](../04_PHASE3_BOT_LEASE_SCHEDULER.md#s34--raidcoordinator).

Responsibilities:

- Form raid of 20 or 40 with correct role composition per
  `RaidCompositionService` (existing).
- Sub-group assignment (8 subgroups of 5).
- World-buff pickup window (T-90 → T-15 of pull) — optional,
  configurable.
- Travel to instance entrance via `TravelTask`.
- Ready check synchronization.
- Per-boss encounter dispatch.
- Master loot policy per `BotSelectionPolicy.LootPriority`.
- Lease release on raid end (zone exit or wipe-out).

## Slots — per raid

### SR.zg — Zul'Gurub (20-man)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `raid.zg`
- **Bosses:** High Priest Venoxis, High Priestess Jeklik, High Priestess Mar'li,
  Bloodlord Mandokir, High Priest Thekal, High Priestess Arlokk, Hakkar,
  optional Edge of Madness mini-bosses.
- **Owned paths:**
  - `Bot/raids/zg.json`
  - `Exports/BotRunner/Tasks/Raid/Encounters/Zg/**`
  - `Tests/BotRunner.Tests/LiveValidation/Raids/ZulGurubTests.cs`

### SR.aq20 — Ruins of Ahn'Qiraj (20-man)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `raid.aq20`
- **Bosses:** Kurinnaxx, General Rajaxx, Moam, Buru, Ayamiss the Hunter,
  Ossirian the Unscarred.

### SR.mc — Molten Core (40-man)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `raid.mc`
- **Bosses:** Lucifron, Magmadar, Gehennas, Garr, Shazzrah, Baron Geddon,
  Sulfuron Harbinger, Golemagg, Majordomo Executus, Ragnaros.
- **Attunement:** required (`attune.mc`).

### SR.onyxia — Onyxia's Lair (40-man)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `raid.onyxia`
- **Bosses:** Onyxia (single boss).
- **Attunement:** required (`attune.ony-horde` or `attune.ony-alliance`).

### SR.bwl — Blackwing Lair (40-man)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `raid.bwl`
- **Bosses:** Razorgore, Vaelastrasz, Broodlord Lashlayer, Firemaw, Ebonroc,
  Flamegor, Chromaggus, Nefarian.
- **Attunement:** required (`attune.bwl`).

### SR.aq40 — Temple of Ahn'Qiraj (40-man)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `raid.aq40`
- **Bosses:** Prophet Skeram, Bug Trio, Battleguard Sartura, Fankriss the Unyielding,
  Princess Huhuran, Twin Emperors, Ouro, C'Thun.
- **Attunement:** server-wide gates (Scarab Lord chain) — may be gated by
  capability `AQ40OpeningComplete`.

### SR.naxx — Naxxramas (40-man)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `raid.naxx`
- **Bosses:** Anub'Rekhan, Faerlina, Maexxna (Spider Wing); Patchwerk, Grobbulus,
  Gluth, Thaddius (Construct); Noth, Heigan, Loatheb (Plague); Razuvious,
  Gothik, Four Horsemen (Death Knight); Sapphiron, Kel'Thuzad (Frostwyrm Lair).
- **Attunement:** required (`attune.naxx`).
- **Capability check:** `Naxx` in `ServerCapabilities`.

## Slots — common

### SR.common.1 — `RaidCompositionService` upgrade

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Services/WoWStateManager/Progression/RaidCompositionService.cs`
- **Goal:** Implement role-composition policy per raid. Lookup table
  per `raid.<id>` returns `RoleTemplate`.

### SR.common.2 — `MasterLootTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Raid/MasterLootTask.cs`
- **Goal:** Group leader (or raid leader) bot distributes loot per
  policy. Tests with mock loot table.

### SR.common.3 — `ReadyCheckTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Raid/ReadyCheckTask.cs`

### SR.common.4 — World buffs

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Buffs/WorldBuffPickupTask.cs`
- **Goal:** Optional pickup at T-90 → T-15 of pull. Catalog flag
  `WorldBuffsEnabled` on the activity.

## Failure recovery

- **Wipe** → corpse run, regroup at instance entrance, restart from
  last cleared boss.
- **3× wipe on same boss** → coordinator cancels raid, releases leases.
- **Critical role disconnect** (tank, main healer) → coordinator tries
  summon-replacement; if no replacement, cancels.
