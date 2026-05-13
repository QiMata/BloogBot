# Activities — Dungeons

26 catalog rows. Group of 5 (1T 1H 3D), level-bracketed.
Coordinator exists; needs run-plan + boss-encounter scripts per dungeon.

## Required task families

| Task | Status |
|---|---|
| `DungeoneeringTask` | done — `Exports/BotRunner/Tasks/Dungeoneering/` |
| `PullStrategyTask` | partial — line-of-sight pulls work, marked-target focus needed |
| `BossEncounterTask` | per-dungeon; mostly not-started |
| `GroupLootTask` | partial — NeedBeforeGreed works; master-loot needs Phase 3 raid foundation |

## Coordinator

`Services/WoWStateManager/Coordination/DungeoneeringCoordinator.cs` —
needs upgrade to `IActivityCoordinator` (S2.9).

Responsibilities:

- Form party of 5 with correct roles.
- Travel all 5 to entrance (uses [`travel.md`](travel.md)).
- Initiate `DungeoneeringTask` on all 5.
- Track encounter completion via boss-kill snapshot.
- Loot policy honored; final boss loot distributed per
  `BotSelectionPolicy.LootPriority` (default: need-before-greed,
  human first).
- Lease release at instance exit.

## Task specifications

The four tasks listed in `docs/Spec/03_BOTRUNNER.md#catalog-of-task-families`
for the Dungeoneering family. Phase 1 workers implement against these
anchors; per-dungeon `BossEncounterTask` subclasses live under
`Encounters/<dungeon-id>/`.

### DungeoneeringTask

1. **Class declaration** — `class DungeoneeringTask : BotTask, IBotTask`
   in namespace `BotRunner.Tasks.Dungeoneering`. File:
   `Exports/BotRunner/Tasks/Dungeoneering/DungeoneeringTask.cs`.

2. **Public surface — current shipped**
   - Constructor: `DungeoneeringTask(IBotContext botContext, bool isLeader, IReadOnlyList<Position>? waypoints = null, uint targetMapId = 0)`.
   - Property: `bool IsLeader { get; }`.
   - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete body lives in `Update()`. Per-family async refactor in S1.7 Dungeons (S1.0/R25, shim-only).
   - Constants (internal contract): `HostilePullRange = 25f`,
     `FollowDistance = 15f`, `WaypointReachDistance = 3f`,
     `StuckTimeoutMs = 8000`, `CombatRepushCooldownSec = 20.0`.
   - Inherited from `BotTask`: `BotTasks` stack, `ObjectManager`,
     `Container`, `Logger`, `ClearNavigation()`, `TryNavigateToward()`,
     `PopTask(reason)`.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).

3. **Snapshot contract**
   - Reads (from `IObjectManager` via `BotTaskContext`):
     `Player.Position`, `Player.HealthPercent`, `Player.MapId`,
     `Player.IsMoving`, `Hostiles`, `Aggressors`, `PartyLeader`.
   - Writes (effects observable in `WoWActivitySnapshot`):
     `currentMapId` (when traversing instance portal),
     `movementData` (position/flags from leader/follower navigation),
     `partyLeaderGuid` (follower mode reads/relies on it),
     `recentChatMessages` (chat-system errors during chat-driven prep),
     no direct `loadout_status` mutation. The leader's skull-marked
     target propagates via `nearbyUnits` raid-target metadata.
   - Sub-tasks pushed: `Container.ClassContainer.CreatePvERotationTask`
     (aggressor branch) and `Container.ClassContainer.CreatePullTargetTask`
     (leader pull branch). Both inherit the snapshot writes of those
     tasks.

4. **BG protocol footprint**
   - `Opcode.MSG_RAID_TARGET_UPDATE` — via
     `ObjectManager.SetRaidTarget(aggressor, TargetMarker.Skull)`
     (`Exports/WoWSharpClient/WoWSharpObjectManager.cs:1199`).
   - `Opcode.CMSG_SET_SELECTION` — via `ObjectManager.SetTarget(guid)`
     when the leader locks a target before the pull/pull-strategy push.
   - Movement opcodes (`MSG_MOVE_*`) — via the movement subsystem
     reached through `TryNavigateToward` / `ObjectManager.StopAllMovement`.
   - Downstream opcodes from pushed children (`CMSG_CAST_SPELL`,
     `CMSG_ATTACKSWING`) are owned by `PvERotationTask` /
     `PullTargetTask`; not emitted directly by `DungeoneeringTask`.

5. **FG memory footprint**
   - `IObjectManager.Player` (Position/HealthPercent/MapId/IsMoving).
   - `IObjectManager.Hostiles`, `IObjectManager.Aggressors`,
     `IObjectManager.PartyLeader`.
   - `IObjectManager.StopAllMovement()`, `IObjectManager.SetTarget(guid)`,
     `IObjectManager.SetRaidTarget(unit, marker)`.
   - `player.InLosWith(unit)` (line-of-sight read, see
     `IWoWGameObject.InLosWith`).
   - Container indirection:
     `Container.ClassContainer.CreatePvERotationTask`,
     `Container.ClassContainer.CreatePullTargetTask`.
   - No direct `LuaCall(...)` invocations from
     `DungeoneeringTask.cs`. Lua-bound side effects (skull marker,
     party assist) flow through `IObjectManager` so FG and BG share
     the same call shape.

6. **Test anchor** —
   `Tests/BotRunner.Tests/LiveValidation/IntegrationValidationTests.cs::V3_1_EncounterMechanics_StartDungeoneering_SnapshotsUpdate`
   (dispatches `ActionType.StartDungeoneering` at the RFC entrance and
   asserts the post-dispatch snapshot is readable). Filter:
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~IntegrationValidationTests.V3_1_EncounterMechanics_StartDungeoneering_SnapshotsUpdate"`.
   Per-dungeon entry tests live in
   `Tests/BotRunner.Tests/LiveValidation/Dungeons/DungeonEntryTests.cs`
   (e.g. `SFK_GroupFormAndEnter`, `WC_GroupFormAndEnterDungeon` in
   `WailingCavernsTests.cs`).

7. **Catalog `TaskFamily` claim** — `Dungeoneering`. Serves every
   dungeon row in [`00_INDEX.md`](00_INDEX.md): `dungeon.ragefire-chasm`,
   `dungeon.wailing-caverns`, `dungeon.deadmines`,
   `dungeon.shadowfang-keep`, `dungeon.blackfathom-deeps`,
   `dungeon.razorfen-kraul`, `dungeon.gnomeregan`,
   `dungeon.razorfen-downs`, `dungeon.uldaman`, `dungeon.zul-farrak`,
   `dungeon.maraudon`, `dungeon.sunken-temple`,
   `dungeon.blackrock-depths`, `dungeon.lower-blackrock-spire`,
   `dungeon.upper-blackrock-spire`, `dungeon.dire-maul-east`,
   `dungeon.dire-maul-west`, `dungeon.dire-maul-north`,
   `dungeon.scholomance`, `dungeon.stratholme-undead`,
   `dungeon.stratholme-live`. (Scarlet Monastery has a test fixture +
   collection but no `00_INDEX.md` row yet — see
   `QUESTIONS.md` entry below.)

### PullStrategyTask

1. **Class declaration** — **Planned anchor:**
   `Exports/BotRunner/Tasks/Dungeoneering/PullStrategyTask.cs`,
   namespace `BotRunner.Tasks.Dungeoneering`. Status: `not-started`.
   No `PullStrategyTask.cs` exists today; the current implementation
   uses per-class `PullTargetTask` in `BotProfiles/<ClassSpec>/Tasks/`
   (e.g. `BotProfiles/WarriorProtection/Tasks/PullTargetTask.cs`)
   resolved through `IClassContainer.CreatePullTargetTask`
   (`Exports/BotRunner/Interfaces/IClassContainer.cs:34`). The
   Dungeoneering family slot must add the strategy-level wrapper that
   sequences "skull mark → assist focus → pull → tank-spot regroup".

2. **Public surface — current shipped:** none — planned anchor: `Exports/BotRunner/Tasks/Dungeoneering/PullStrategyTask.cs`.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).

   **Public surface (planned task-specific shape):**
   - Constructor: `PullStrategyTask(IBotContext botContext, IWoWUnit primaryTarget, IReadOnlyList<IWoWUnit>? linkedPullables = null, PullPlan plan = PullPlan.LosPull)`.
   - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete body lives in `Update()`. Per-family async refactor in S1.7 Dungeons (S1.0/R25, shim-only).
   - Cooperates with `DungeoneeringTask`'s leader branch via the
     `_lastCombatPush` cooldown surface; replaces direct
     `BotTasks.Push(CreatePullTargetTask(...))` call sites in
     `DungeoneeringTask.Update()` lines 144-145.
   - Internal contract aligned with `WarriorProtection.PullTargetTask`:
     tank-spot determination, ranged-weapon pull-shot fallback, target
     loss / LOS-timeout pop semantics.

3. **Snapshot contract** — **Planned:**
   - Reads: `Player.Position`, `Player.IsCasting`, `Player.IsMoving`,
     `ObjectManager.GetTarget(player)`, `ObjectManager.Aggressors`,
     `Player.InLosWith(target)`, `Items` (for thrown/bow/gun reagent
     checks).
   - Writes (snapshot-observable side effects): raid-target icon on
     `nearbyUnits.targetMarker = SKULL`, current selection
     (`movementData` standstill once distance is in pull-range),
     `recentChatMessages` (additem reagent restocks if reused),
     `currentAction = StartMeleeAttack`/`CastSpell` downstream of
     pushed `PvERotationTask`.

4. **BG protocol footprint** — **Planned:**
   - `Opcode.MSG_RAID_TARGET_UPDATE` (skull-marker on primary target;
     see `WoWSharpObjectManager.cs:1199`).
   - `Opcode.CMSG_SET_SELECTION` (focus assist).
   - `Opcode.CMSG_CAST_SPELL` (Throw / Shoot Bow / Shoot Gun / Shoot
     Crossbow pull; see
     `Exports/WoWSharpClient/Networking/ClientComponents/SpellCastingNetworkClientComponent.cs:213`).
   - `Opcode.CMSG_ATTACKSWING` (melee-pull fallback; see
     `Exports/WoWSharpClient/Networking/ClientComponents/AttackNetworkClientComponent.cs:127`).
   - Movement (`MSG_MOVE_*`) via `TryNavigateToward` /
     `StopAllMovement`.

5. **FG memory footprint** — **Planned:**
   - `IObjectManager.GetTarget(IWoWPlayer)`,
     `IObjectManager.SetTarget(guid)`,
     `IObjectManager.SetRaidTarget(unit, marker)`.
   - `IObjectManager.IsSpellReady(name)`,
     `IObjectManager.CastSpell(name)`,
     `IObjectManager.StopAllMovement()`,
     `IObjectManager.GetEquippedItem(EquipSlot.Ranged)`,
     `IObjectManager.Items`, `IObjectManager.SendChatMessage(...)`.
   - `player.InLosWith(target)`, `player.Position.DistanceTo(...)`.
   - No direct `LuaCall(...)` expected; everything flows through
     `IObjectManager` for FG/BG parity.

6. **Test anchor** — **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/Dungeons/PullStrategyTests.cs::PullStrategy_SkullMarkAndAssist_PullsSingleTarget`.
   Until that file lands, the closest live coverage is
   `Tests/BotRunner.Tests/LiveValidation/RagefireChasmTests.cs::RFC_FullDungeonRun`
   (asserts the leader skull-marks + pulls). Filter:
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~RagefireChasmTests.RFC_FullDungeonRun"`.

7. **Catalog `TaskFamily` claim** — `Dungeoneering`. Same dungeon rows
   as `DungeoneeringTask` above (all 21 `dungeon.*` rows in
   [`00_INDEX.md`](00_INDEX.md)). Trash pulls dominate every dungeon
   run, so this task is universally claimed.

### BossEncounterTask

1. **Class declaration** — **Planned anchor:**
   `Exports/BotRunner/Tasks/Dungeoneering/BossEncounterTask.cs`
   (family-level base), namespace `BotRunner.Tasks.Dungeoneering`.
   Status: `not-started`. Per-dungeon overrides land under
   `Exports/BotRunner/Tasks/Dungeoneering/Encounters/<dungeon-id>/<BossName>Encounter.cs`
   per the per-dungeon slot template (line 50 below). No such files
   exist today.

2. **Public surface — current shipped:** none — planned anchor: `Exports/BotRunner/Tasks/Dungeoneering/BossEncounterTask.cs`.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).

   **Public surface (planned task-specific shape):**
   - Constructor:
     `BossEncounterTask(IBotContext botContext, EncounterPlan plan)`
     where `EncounterPlan` carries: bossUnitEntry, role-specific
     stance positions, phase-trigger predicates (hp%, aura, add-spawn),
     interrupt priority list, dispel priority list.
   - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete body lives in `Update()`. Per-family async refactor in S1.7 Dungeons (S1.0/R25, shim-only).
   - Virtual hooks: `OnPhaseEnter(int phase)`,
     `OnAddSpawn(IWoWUnit add)`, `OnEncounterEnd(EncounterResult)`.
   - Concrete per-boss classes override the hooks.

3. **Snapshot contract** — **Planned:**
   - Reads: `Player.Position`, `Player.HealthPercent`,
     `Player.ManaPercent`, `Aggressors`, `Hostiles`, `PartyLeader`,
     `ObjectManager.GetTarget(player).HealthPercent`,
     auras on player and target.
   - Writes: `currentAction = ...` mirroring the active sub-task;
     `recentChatMessages` for raid-warn cues; bossfight progress is
     observable via `nearbyUnits[bossGuid].health` decrement and
     `aggressors.Count` deltas. No new top-level fields required;
     boss-kill detection is `boss in nearbyUnits with health == 0`.
   - Sub-tasks pushed: `PvERotationTask` (DPS/heal), `MovementTask` /
     `GoToTask` (positioning), `LootCorpseTask` (post-kill).

4. **BG protocol footprint** — **Planned:**
   - `Opcode.MSG_RAID_TARGET_UPDATE` (skull/X/square assignments per
     phase).
   - `Opcode.CMSG_CAST_SPELL` (encounter rotation, interrupt,
     dispel).
   - `Opcode.CMSG_ATTACKSWING` (melee).
   - `Opcode.CMSG_SET_SELECTION` (focus swaps).
   - Movement opcodes via `TryNavigateToward` /
     `StopAllMovement`. Loot opcodes inherited from `LootCorpseTask`
     when boss dies.

5. **FG memory footprint** — **Planned:**
   - `IObjectManager.Hostiles.FirstOrDefault(u => u.Entry == bossEntry)`,
     `IObjectManager.Aggressors`, `IObjectManager.PartyLeader`.
   - `IObjectManager.SetTarget`, `IObjectManager.SetRaidTarget`,
     `IObjectManager.CastSpell`, `IObjectManager.StopAllMovement`,
     `IObjectManager.GetTarget(player)`.
   - Aura reads: `unit.Auras` / `unit.HasAura(spellId)` via
     `IWoWUnit`.
   - No direct `LuaCall(...)` expected; if a per-encounter override
     needs frame data (e.g. `RaidGroup` membership), it must come
     through `IObjectManager` to keep FG/BG parity.

6. **Test anchor** — **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/Dungeons/<DungeonId>BossTests.cs::<BossName>_FullEncounter_Cleared`
   (one per named boss, instantiated through the per-dungeon
   fixture/collection in `DungeonCollections.cs`). Existing run-through
   coverage in
   `Tests/BotRunner.Tests/LiveValidation/RagefireChasmTests.cs::RFC_FullDungeonRun`
   already exercises Taragaman, Jergosh, and Bazzalan implicitly;
   per-boss tests are deferred until `BossEncounterTask` lands.
   Filter: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~RagefireChasmTests.RFC_FullDungeonRun"`.

7. **Catalog `TaskFamily` claim** — `Dungeoneering`. Required by every
   `dungeon.*` row in [`00_INDEX.md`](00_INDEX.md): per the per-dungeon
   slot template below, a slot is `done` only when "`BossEncounterTask`
   exists for each named boss". Initial implementation order follows
   the slot list (RFC first → WC → Deadmines → SFK → BFD → RFK →
   Gnomeregan → RFD → Uldaman → ZF → Maraudon → ST → BRD → LBRS →
   UBRS → DM-E → DM-W → DM-N → Scholo → Strat-UD → Strat-Live).

### LootCorpseTask (group-loot path)

1. **Class declaration** — `class LootCorpseTask : BotTask, IBotTask`
   in namespace `BotRunner.Tasks`. File:
   `Exports/BotRunner/Tasks/LootCorpseTask.cs`. Atomic single-corpse
   path is implemented; the group-loot extension (need-before-greed
   roll arbitration) is **planned** at the same anchor (extend
   constructor + dispatch through `CMSG_LOOT_ROLL`). Status of base
   task: done; status of group-loot extension: `not-started`.

2. **Public surface — current shipped**
   - Constructor (existing):
     `LootCorpseTask(IBotContext botContext, ulong corpseGuid)`.
   - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete body lives in `Update()`. Per-family async refactor in S1.7 Dungeons (S1.0/R25, shim-only).

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).

   **Public surface (planned task-specific extension):**
   - **Planned extension:** add overload
     `LootCorpseTask(IBotContext botContext, ulong corpseGuid, LootPolicy policy)`
     where `LootPolicy` (default `NeedBeforeGreed`) drives the
     `CMSG_LOOT_ROLL` rollType decision per item-quality / class-need
     match. The policy enum mirrors
     `BotSelectionPolicy.LootPriority` referenced by the coordinator
     section above.

3. **Snapshot contract**
   - Reads: `ObjectManager.LootTargetAsync(corpseGuid, ct)` result;
     no direct snapshot reads in the body.
   - Writes: bag deltas (observable through `Player.Inventory` /
     `nearbyObjects` removal of corpse), `recentChatMessages` on
     "You receive loot:" lines, `recentErrors` on loot failure
     (e.g. inventory full). Single-frame task; pops itself on the
     same tick. The group-loot extension adds `currentAction` =
     `LootRoll` while a roll window is open.

4. **BG protocol footprint** —
   `Exports/WoWSharpClient/Networking/ClientComponents/LootingNetworkClientComponent.cs`:
   - `Opcode.CMSG_LOOT` (line 166) — open the loot window.
   - `Opcode.CMSG_AUTOSTORE_LOOT_ITEM` (line 214) — claim each slot.
   - `Opcode.CMSG_LOOT_MONEY` (line 189) — claim copper/silver/gold.
   - `Opcode.CMSG_LOOT_RELEASE` (line 266) — close the loot window.
   - **Group-loot extension:** `Opcode.CMSG_LOOT_ROLL` (line 295)
     for need-before-greed rolls.
   - Coordinator-side master-loot path:
     `Opcode.CMSG_LOOT_METHOD` (line 509) and
     `Opcode.CMSG_LOOT_MASTER_GIVE` (line 480) — invoked from
     `PartyNetworkClientComponent` / `DungeoneeringCoordinator`, not
     from `LootCorpseTask` itself.

5. **FG memory footprint**
   - `IObjectManager.LootTargetAsync(targetGuid, ct)` — single entry
     point (`Exports/GameData.Core/Interfaces/IObjectManager.cs:252`,
     dispatched in `WoWSharpObjectManager.Inventory.cs:53` →
     `InventoryManager.LootTargetAsync` →
     `LootingNetworkClientComponent` opcodes above).
   - No direct `LuaCall(...)`. FG and BG converge on the same
     `ILootingNetworkClientComponent` flow because `InventoryManager`
     is shared (the FG bot stages loot via opcodes, not via
     `LootSlot()`/`CloseLoot()` Lua, to preserve parity).

6. **Test anchor** —
   `Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs::Loot_KillAndLootMob_InventoryChanges`
   (single-corpse path; see line 40 of that file). Filter:
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~LootCorpseTests.Loot_KillAndLootMob_InventoryChanges"`.
   **Planned anchor test for the group-loot extension:**
   `Tests/BotRunner.Tests/LiveValidation/Dungeons/GroupLootTests.cs::Loot_NeedBeforeGreed_RollResolvedPerPolicy`.

7. **Catalog `TaskFamily` claim** — `Dungeoneering` (group-loot
   path). Serves every `dungeon.*` row in [`00_INDEX.md`](00_INDEX.md):
   `dungeon.ragefire-chasm`, `dungeon.wailing-caverns`,
   `dungeon.deadmines`, `dungeon.shadowfang-keep`,
   `dungeon.blackfathom-deeps`, `dungeon.razorfen-kraul`,
   `dungeon.gnomeregan`, `dungeon.razorfen-downs`, `dungeon.uldaman`,
   `dungeon.zul-farrak`, `dungeon.maraudon`, `dungeon.sunken-temple`,
   `dungeon.blackrock-depths`, `dungeon.lower-blackrock-spire`,
   `dungeon.upper-blackrock-spire`, `dungeon.dire-maul-east`,
   `dungeon.dire-maul-west`, `dungeon.dire-maul-north`,
   `dungeon.scholomance`, `dungeon.stratholme-undead`,
   `dungeon.stratholme-live`. The atomic loot path is also reused by
   solo-quest kill loot (Questing family) and gathering-corpse loot
   (Gathering family), but those reuses are tracked in their own
   activity files.

## Slots — per-dungeon

For each of the 26 dungeons, create a slot below following the
template. Mark the slot done when:

- `BossEncounterTask` exists for each named boss.
- Run plan in `Bot/dungeons/<id>.json` includes waypoints, optional
  trash skips, key gate ack, and boss order.
- LiveValidation test green for at least one full clear.

### Template per dungeon

```
### SD.<id> — <dungeon name>
- **Owner:** monorepo-worker
- **Status:** not-started
- **Catalog row:** `dungeon.<id>` in ActivityCatalog
- **Owned paths:**
  - `Bot/dungeons/<id>.json`
  - `Exports/BotRunner/Tasks/Dungeoneering/Encounters/<id>/**`
  - `Tests/BotRunner.Tests/LiveValidation/Dungeons/<id>Tests.cs`
- **Read-only paths:**
  - `docs/leveling-guide/dungeons/<id>.md` (if exists)
- **Goal:** Form 5-person, clear, loot, leave. LiveValidation green.
```

## Dungeon slots

- `SD.ragefire-chasm` — Ragefire Chasm (13-18) — not-started
- `SD.wailing-caverns` — Wailing Caverns (17-24) — not-started
- `SD.deadmines` — Deadmines (17-26) — not-started
- `SD.shadowfang-keep` — Shadowfang Keep (22-30) — not-started
- `SD.blackfathom-deeps` — Blackfathom Deeps (20-30) — not-started
- `SD.razorfen-kraul` — Razorfen Kraul (24-34) — not-started
- `SD.gnomeregan` — Gnomeregan (29-38) — not-started
- `SD.razorfen-downs` — Razorfen Downs (35-45) — not-started
- `SD.uldaman` — Uldaman (41-51) — not-started
- `SD.zul-farrak` — Zul'Farrak (44-54) — not-started
- `SD.maraudon` — Maraudon (46-55) — not-started
- `SD.sunken-temple` — Sunken Temple (50-56) — not-started
- `SD.blackrock-depths` — Blackrock Depths (52-60) — not-started **(crash cluster — see Plan/09)**
- `SD.lower-blackrock-spire` — Lower Blackrock Spire (55-60) — not-started **(crash cluster — see Plan/09)**
- `SD.upper-blackrock-spire` — Upper Blackrock Spire (58-60) — not-started **(bake gap — see Plan/09)**
- `SD.dire-maul-east` — Dire Maul East (55-60) — not-started
- `SD.dire-maul-west` — Dire Maul West (55-60) — not-started
- `SD.dire-maul-north` — Dire Maul North (55-60) — not-started
- `SD.scholomance` — Scholomance (58-60) — not-started
- `SD.stratholme-undead` — Stratholme Undead (58-60) — not-started
- `SD.stratholme-live` — Stratholme Live (58-60) — not-started

## Failure recovery

- **Wipe** → corpse run, regroup at instance entrance, restart from
  last-cleared encounter waypoint.
- **Bot disconnect mid-run** → coordinator decides: continue 4-bot
  with a replacement summoned in via warlock summon, or wipe and
  retry.
- **Stuck on boss encounter** → `task_timeout` after 3× expected
  encounter duration; coordinator decides to retry or fail.
- **Loot not received** → metric `wwow.activity.loot_missed_total{boss=...}`;
  no retry (loot was already distributed).
