# Activities — World Events

Single catalog row in the initial release: STV Fishing Extravaganza.
Holiday/seasonal events are deferred to a later spec PR.

## Catalog row

- `event.stv-fishing-extravaganza` — Sundays, 14:00–16:00 local.

## Family scope vs. world bosses

The `WorldEvent` family in
[`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../../Spec/03_BOTRUNNER.md#catalog-of-task-families)
enumerates two tasks: `StvFishingExtravaganzaTask` and
`WorldBossEngagementTask`. The two tasks share a family because both
gate on a transient world-state change (scheduled event window /
perpetual outdoor spawn) rather than a per-character objective.

The split with [`world-bosses.md`](world-bosses.md) is:

- **World events** (this file): **scheduled** activities tied to
  `mangos.game_event` rows with a calendar window. STV Fishing
  Extravaganza is the only Phase-1 row. Holiday events
  (Lunar Festival, Midsummer, Hallow's End, Brewfest, Winter Veil,
  Children's Week, Noblegarden, Love-is-in-the-Air, Harvest Festival)
  are deferred.
- **World bosses** (`world-bosses.md`): **perpetual** outdoor 40-man
  spawns (Azuregos in Azshara, Lord Kazzak in Blasted Lands, the four
  rotating Emerald Dragons). No `mangos.game_event` calendar gate;
  availability is driven by respawn timers + faction contest.

`WorldBossEngagementTask` is the shared task that orchestrates the
encounter itself (positioning + engage + loot) once a perpetual spawn
is up. **Its canonical home is this file** (`world-events.md`) because
the family enum is `WorldEvent` per Spec/03 and the task lives under
`Exports/BotRunner/Tasks/WorldEvent/`. `world-bosses.md` continues to
own the per-boss spawn-detection + per-encounter spec slots
(SWB.1–SWB.5) and references this file's `WorldBossEngagementTask`
specification as the canonical task contract. Per-boss
`WorldBossEngagementTask` subclasses
(`AzuregosEngagement`, `KazzakEngagement`,
`EmeraldDragonEngagement`) live under
`Exports/BotRunner/Tasks/WorldEvent/Bosses/<boss-id>/` to keep all
WorldEvent-family code under one folder while the per-boss catalog
rows stay in `world-bosses.md`.

## Task family

| Task | Status |
|---|---|
| `StvFishingExtravaganzaTask` | not-started |
| `WorldBossEngagementTask` | not-started (shared with `world-bosses.md`) |
| Reuses `FishingTask` from `professions-gathering.md` |
| Reuses `TurnInQuestTask` (`CompleteQuestTask`) from `quests.md` |
| Reuses `RaidEncounterTask` / `RaidPositioningTask` / `MasterLootTask` from `raids.md` (for `WorldBossEngagementTask`) |

## Task specifications

Each task below follows the Phase 0 precision contract per
[`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../../Spec/03_BOTRUNNER.md#catalog-of-task-families):
class declaration, public surface (current shipped + target per R19),
snapshot contract, BG protocol footprint, FG memory/Lua footprint, test
anchor, and catalog `TaskFamily` claim. Both tasks in this family are
`not-started`; every surface below is recorded as
`**Planned anchor:**`. Catalog references resolve through
[`00_INDEX.md`](00_INDEX.md).

### StvFishingExtravaganzaTask

- **Class declaration** — `public sealed class StvFishingExtravaganzaTask : BotTask, IBotTask`
  in namespace `BotRunner.Tasks.WorldEvent`.
  **Planned anchor:** `Exports/BotRunner/Tasks/WorldEvent/StvFishingExtravaganzaTask.cs`
  per slot `SWE.1` above. Status: `not-started`. No file exists today.
  The directory `Exports/BotRunner/Tasks/WorldEvent/` itself has not
  been created yet; the SWE.1 worker creates it alongside the task
  class. Closest existing surfaces the task composes are
  `Exports/BotRunner/Tasks/FishingTask.cs:19` (`public class FishingTask : BotTask, IBotTask`)
  and `Exports/BotRunner/Tasks/CompleteQuestTask.cs:11`
  (`public class CompleteQuestTask(IBotContext botContext, int rewardIndex = 0) : BotTask(botContext), IBotTask`).
- **Public surface**
  - **Current shipped surface:** none — task does not exist. The
    `event.stv-fishing-extravaganza` catalog row exists at
    `Config/activities/event.stv-fishing-extravaganza.json` with the
    Phase-1 loadout (`TargetLevel: 45`, `Skill 356 = 225/300`,
    `Pole 6256`, supplemental `6529 6532`); no task class consumes
    that activity definition yet.
  - **Target surface (Phase 1 per R19):**
    `public sealed class StvFishingExtravaganzaTask : IBotTask` with
    `Name { get; }` = `"StvFishingExtravaganza"`, `Status { get; }`,
    `TickAsync(BotTaskContext, CancellationToken)`,
    `OnPushedAsync(BotTaskContext, CancellationToken)`,
    `OnPoppedAsync(BotTaskContext, BotTaskStatus)`,
    `OnChildFailedAsync(BotTaskContext, IBotTask, string)` per
    [`Spec/03_BOTRUNNER.md#ibottask-interface`](../../Spec/03_BOTRUNNER.md#ibottask-interface).
    Constructor:
    `StvFishingExtravaganzaTask(IBotContext context, FactionSide faction, DateTimeOffset eventStart, DateTimeOffset eventEnd, int targetTastyfishCount = 40)`.
    Internal state machine: `AwaitEventOpen → TravelToStv → FishLoop →
    TravelToTurnIn → TurnInTastyfish → AwaitReward → Complete`. Pushes
    `TravelTask` (`Booty Bay`), `FishingTask` (targeting Tastyfish
    pool entry 180658 — the Schools-of-Tastyfish GO spawned only
    while the event is live), and `CompleteQuestTask` (turn-in to
    Riggle Bassbait — `Riggle Bassbait quest 8193, Master Angler trophy quest`).
- **Snapshot contract** — **Planned:**
  - Reads (via `BotTaskContext.ObjectManager`): `Player.Position`,
    `Player.MapId` (assert `MapId == 0` for Eastern Kingdoms before
    routing into STV), `Player.LearnedSpells` (gate on Fishing spell
    7732 — same gate `FishingTask` uses today), `Items[poleId]` (gate
    on equipped pole 6256), `NearbyObjects` filtered to fishing-pool
    GO entry **180658** (Schools of Tastyfish), `QuestLog` (track
    Tastyfish stack `count >= 40` once collected),
    `recentChatMessages` (event-broadcast strings from
    `mangos.game_event_string`).
  - Writes (effects observable in `WoWActivitySnapshot`):
    `currentMapId` (unchanged — STV is on map 0), `movementData`
    (delegated to pushed `TravelTask` + `FishingTask`),
    `currentAction = StartFishing` then `CompleteQuest`,
    `loadout_status` (only if the SWE.1 worker chooses to reuse
    `LoadoutTask` to acquire pole/lures rather than relying on the
    activity-definition loadout being applied upstream),
    `recentChatMessages` (turn-in confirmation, reward selection).
  - Sub-tasks pushed: `TravelTask` (to Booty Bay), `FishingTask`
    (constructor `FishingTask(botContext, searchWaypoints,
    location:"STV Fishing Extravaganza", useGmCommands:false,
    masterPoolId:null)`; the SWE.1 worker is responsible for
    populating Tastyfish-specific search waypoints around the STV
    coastline), `CompleteQuestTask(botContext, rewardIndex)` — reward
    index resolved by `IRewardSelector.SelectQuestReward` per the
    "always picks" invariant in
    [`Spec/03_BOTRUNNER.md#reward-selection`](../../Spec/03_BOTRUNNER.md#reward-selection).
- **BG protocol footprint** — **Planned:**
  - `CMSG_CAST_SPELL` (spell 7732 Fishing) — owned by pushed
    `FishingTask`, not emitted directly.
  - `CMSG_GAMEOBJ_USE` (loot the schools-of-Tastyfish pool / bobber)
    — owned by pushed `FishingTask` via
    `SpellCastingNetworkClientComponent` and the GO-interaction path.
  - Movement opcodes (`MSG_MOVE_*`) — owned by the pushed `TravelTask`
    + `FishingTask` movement legs.
  - `CMSG_QUESTGIVER_HELLO = 0x182` / `CMSG_QUESTGIVER_QUERY_QUEST = 0x186`
    / `CMSG_QUESTGIVER_COMPLETE_QUEST = 0x18A` /
    `CMSG_QUESTGIVER_CHOOSE_REWARD = 0x191` for the Riggle Bassbait
    turn-in. Wire layer is the existing
    `Exports/WoWSharpClient/Networking/ClientComponents/QuestingNetworkClientComponent.cs`
    path used by `CompleteQuestTask`.
  - No event-state opcode exists on the client side; event-window
    detection is server-driven via `mangos.game_event` row activation
    and reaches the bot as ambient `SMSG_NOTIFICATION` /
    `SMSG_MESSAGECHAT` (`MONSTER_YELL` from event NPCs). The task
    polls the server-side state via the existing snapshot's
    `recentChatMessages` plus the SWE.2
    `WorldEventCoordinator` API (S3.6) rather than handshaking
    directly with the server.
- **FG memory footprint** — **Planned:**
  - `IObjectManager.Player` (Position/MapId/Health),
    `IObjectManager.Items`, `IObjectManager.LearnedSpells`,
    `IObjectManager.NearbyGameObjects` filtered to entry 180658,
    `IObjectManager.QuestLog`, `IObjectManager.QuestFrame`,
    `IObjectManager.SendChatMessage(...)`.
  - Fishing primitives (`CastSpell`, `InteractWith(IWoWGameObject)`,
    `OpenLootWindow`) are exercised via the pushed `FishingTask`; the
    parent task itself does not call them directly.
  - Quest turn-in primitives (`QuestFrame.CompleteQuest(rewardIndex)`
    at `Exports/BotRunner/Tasks/CompleteQuestTask.cs:23`) are
    exercised via the pushed `CompleteQuestTask`.
  - No direct `LuaCall(...)` invocations. FG/BG parity holds because
    all event-time behavior flows through `IObjectManager`.
- **Test anchor** — **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/WorldEvents/StvFishingExtravaganzaTests.cs::StvFishing_EventWindowOpens_BotFishesAndTurnsIn`
  per slot `SWE.3`. Filter:
  `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~StvFishingExtravaganzaTests"
  --configuration Release`. Status: `not-started`. No matching test
  exists today; the directory `Tests/BotRunner.Tests/LiveValidation/WorldEvents/`
  has not been created. Adjacent live coverage that the SWE.3 worker
  composes against:
  `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  (fishing loop) and the quest turn-in assertions in
  `IntegrationValidationTests.cs`. The test must drive the event
  window via SOAP / `mangos.game_event` activation rather than
  waiting for the calendar Sunday — the fixture forces the event row
  active, then forces it inactive at test end to keep cross-test
  state clean (R8).
- **Catalog `TaskFamily` claim** — `WorldEvent`. Cross-referenced
  rows from [`00_INDEX.md`](00_INDEX.md):
  `event.stv-fishing-extravaganza` (sole row). Holiday-event rows are
  deferred per the "Future" section below and per
  [`Spec/04_ACTIVITIES.md`](../../Spec/04_ACTIVITIES.md) which
  records "World event | 1 (STV Fishing Extravaganza)" in the
  Phase-1 catalog total.

### WorldBossEngagementTask

- **Class declaration** — `public sealed class WorldBossEngagementTask : BotTask, IBotTask`
  in namespace `BotRunner.Tasks.WorldEvent` (per-boss subclasses
  under `BotRunner.Tasks.WorldEvent.Bosses.<Boss>`).
  **Planned anchor:** `Exports/BotRunner/Tasks/WorldEvent/WorldBossEngagementTask.cs`
  (orchestrator) plus per-boss subclasses under
  `Exports/BotRunner/Tasks/WorldEvent/Bosses/<boss-id>/<BossName>Engagement.cs`
  for slots `SWB.2` (Azuregos), `SWB.3` (Kazzak),
  `SWB.4` (Emerald Dragons). Status: `not-started`. No file exists
  today. The closest existing surface is the data-driven
  `EncounterMechanicsTask` at
  `Exports/BotRunner/Tasks/Raid/EncounterMechanicsTask.cs` (per-tick
  mechanic responder) — `WorldBossEngagementTask` composes that task
  for per-tick mechanic handling rather than reimplementing it.
- **Public surface**
  - **Current shipped surface:** none — task does not exist. Per-boss
    encounter JSON specs at `Bot/world-bosses/azuregos.json`,
    `kazzak.json`, `emerald-dragons.json` are likewise `not-started`
    per `world-bosses.md` slots SWB.2–SWB.4.
  - **Target surface (Phase 1 per R19):**
    `public sealed class WorldBossEngagementTask : IBotTask` with the
    standard `Name`, `Status`, `TickAsync`, `OnPushedAsync`,
    `OnPoppedAsync`, `OnChildFailedAsync` per
    [`Spec/03_BOTRUNNER.md#ibottask-interface`](../../Spec/03_BOTRUNNER.md#ibottask-interface).
    Constructor:
    `WorldBossEngagementTask(IBotContext context, WorldBossDefinition boss, RaidCompositionService.RaidRole role, ulong playerGuid)`
    where `WorldBossDefinition(uint BossEntry, uint MapId, Position SpawnPoint, RewardDefinition Reward, EncounterDefinition Encounter)`
    is a new record created by the SWB.2/3/4 worker and lives under
    `Exports/BotRunner/Tasks/WorldEvent/WorldBossDefinition.cs`.
    Internal state machine: `AwaitSpawnDetection → TravelToSpawnZone →
    RaidPositioning → Engage → MasterLoot → Complete`. Pushes
    `TravelTask`, `RaidPositioningTask`, `RaidEncounterTask` (reuses
    the `raids.md` Encounter task with a `WorldBossDefinition`-derived
    `EncounterDefinition`), and `MasterLootTask`.
- **Snapshot contract** — **Planned:**
  - Reads (via `BotTaskContext.ObjectManager`): `Player.Position`,
    `Player.MapId` (must match `boss.MapId` — Azshara/Blasted-Lands/
    Ashenvale/Duskwood/Hinterlands/Feralas), `NearbyUnits[*]`
    filtered to `boss.BossEntry`, `PartyLeaderGuid`,
    `WoWActivitySnapshot.Player.Unit.{Health,Mana,Buffs}`,
    `Hostiles` (for opposing-faction contest detection per
    `world-bosses.md` "Other faction contesting" recovery).
  - Writes (snapshot-observable side effects): the SWB.1 spawn-detection
    slot emits metric
    `wwow.activity.world_boss_spawn_total{boss=...}` on first sighting
    via `BotMetricsService`; this is the task's only direct telemetry
    write. Per-encounter state propagates through pushed
    `RaidEncounterTask` (boss-health deltas) and `MasterLootTask`
    (loot-window state) — neither extends the proto.
  - Sub-tasks pushed: `TravelTask`, `RaidPositioningTask`,
    `RaidEncounterTask`, `MasterLootTask`. Wipe/retry semantics:
    on `RaidEncounterTask.Status == Failed` and boss still alive,
    push `RetrieveCorpseTask` + retry once per `world-bosses.md`
    failure-recovery rules.
- **BG protocol footprint** — **Planned:**
  - Movement opcodes (`MSG_MOVE_*`) — owned by pushed `TravelTask`
    + `RaidPositioningTask`.
  - `CMSG_CAST_SPELL = 0x12E` / `CMSG_ATTACKSWING` — owned by the
    `PvERotationTask` pushed by `RaidEncounterTask` per
    [`raids.md`](raids.md#raidencountertask).
  - `MSG_RAID_READY_CHECK = 0x322` / `CMSG_SET_SELECTION` /
    `MSG_RAID_TARGET_UPDATE` — owned by the leader-side
    `ReadyCheckTask` / `PullStrategyTask` indirections.
  - `CMSG_LOOT = 0x15D` / `CMSG_LOOT_MASTER_GIVE = 0x2A3` /
    `CMSG_LOOT_RELEASE = 0x15F` — owned by pushed `MasterLootTask`
    per [`raids.md`](raids.md#masterloottask).
  - Spawn-detection is passive: snapshot-poll on
    `NearbyUnits[*].entry == boss.BossEntry` driven by ambient
    `SMSG_UPDATE_OBJECT` streams. No outbound spawn-query opcode.
- **FG memory footprint** — **Planned:**
  - `IObjectManager.Units` (filtered to `boss.BossEntry`),
    `IObjectManager.Player`, `IObjectManager.Players` (raid-member
    state), `IObjectManager.PartyLeader`,
    `IObjectManager.SetTarget(guid)`, `IObjectManager.SetRaidTarget`,
    `IObjectManager.LootFrame` (for the master-loot path),
    `IObjectManager.AssignLoot(itemId, playerGuid)` (declared at
    `Exports/GameData.Core/Interfaces/IObjectManager.cs:172`).
  - `IWoWUnit.IsCasting` / `ChannelingId` for boss-cast observation
    (already consumed by `EncounterMechanicsTask`).
  - No direct `LuaCall(...)` required; raid-target Lua flows through
    `IObjectManager.SetRaidTarget` (FG path at
    `ObjectManager.Interaction.cs:97`) which the leader-side
    `PullStrategyTask` invokes.
- **Test anchor** — **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/WorldEvents/WorldBossEngagementTests.cs::WorldBoss_AzuregosSpawned_RaidEngagesAndLoots`
  (and analogous `Kazzak`, `EmeraldDragon_<zone>` variants) per slot
  `SWB.5`. Filter:
  `dotnet test --filter "FullyQualifiedName~WorldBossEngagementTests"
  --configuration Release`. Status: `not-started`. No matching file
  or directory exists today; nearest live coverage that the SWB.5
  worker composes against is
  `Tests/BotRunner.Tests/LiveValidation/Raids/RaidEntryTests.cs`
  (raid formation + instance entry) and
  `RaidCoordinationTests.cs::Raid_LootRules_CorrectDistribution`
  (loot assignment plumbing). Per
  [`Spec/04_ACTIVITIES.md`](../../Spec/04_ACTIVITIES.md), encounter
  end-to-end tests are Phase-2-gated on OnDemand spawn/gear; the
  SWB.5 LiveValidation must SOAP-spawn the boss
  (`.npc add <entry>` at `boss.SpawnPoint`) at test setup and clean
  up on teardown (R8).
- **Catalog `TaskFamily` claim** — `WorldEvent` (shared with
  `StvFishingExtravaganzaTask`). Cross-referenced rows from
  [`00_INDEX.md`](00_INDEX.md): `boss.azuregos`, `boss.kazzak`,
  `boss.emerald-dragons`. Per-boss spawn-detection + encounter
  catalog details remain owned by [`world-bosses.md`](world-bosses.md)
  slots SWB.1–SWB.5; this entry is the canonical task contract those
  slots implement against.

## Slots

### SWE.1 — STV Fishing Extravaganza

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/WorldEvent/StvFishingExtravaganzaTask.cs`
- **Goal:** Detect event window via `mangos.game_event`. Travel to
  Stranglethorn Vale. Fish Tastyfish pools. Turn in 40 fish to NPC
  for the prize.

### SWE.2 — `WorldEventCoordinator` (also S3.6)

- **Owner:** `monorepo-worker`
- **Status:** open

### SWE.3 — LiveValidation

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Goal:** Schedule a mock event window; verify bot travels, fishes,
  turns in.

## Future (out of scope for initial release)

- Lunar Festival, Midsummer Fire Festival, Hallow's End, Brewfest,
  Winter Veil, Children's Week, Noblegarden, Love is in the Air,
  Harvest Festival. Each is a separate catalog row in a future
  spec PR.
