# Activities — Reputations

5 reputation catalog rows. Each is a long-running grind activity that
returns the bot's `ReputationGoal` to `Exalted`.

Reputation grinds are **not** a top-level `TaskFamily` head in
[`docs/Spec/03_BOTRUNNER.md#catalog-of-task-families`](../../Spec/03_BOTRUNNER.md#catalog-of-task-families).
Under R16 (TaskFamily existence rule) the catalog `TaskFamily` claim for
every `rep.*` row must come from the fixed family-head list — for
reputation work that resolves to one or more of `Combat` (kill grinds),
`Questing` (quest chains and quest-driven turn-ins), and `Gathering`
(material farming for turn-ins). The reputation family file therefore
documents an orchestrator that **dispatches** into those families plus
the activity-specific turn-in flows the orchestrator drives; it does
**not** introduce a `Reputation` family head.

## Catalog rows

- `rep.timbermaw-hold` — Felwood / Winterspring furbolg killing.
- `rep.argent-dawn` — Plaguelands undead, cloth donations.
- `rep.cenarion-circle` — Silithus + AQ20.
- `rep.thorium-brotherhood` — Thorium ore + Dark Iron Residue
  donations.
- `rep.zandalar-tribe` — Zul'Gurub bijoux + coins.

## Catalog `TaskFamily` claim per row

| Catalog row | Primary path | Catalog `TaskFamily` (R16-compliant) |
|---|---|---|
| `rep.timbermaw-hold` | Deadwood / Winterfall furbolg kill grind + Timbermaw quest chain | `Combat` + `Questing` |
| `rep.argent-dawn` | Plaguelands undead kill grind + Argent Dawn quest chain + Runecloth/Light's Hope donations | `Combat` + `Questing` |
| `rep.cenarion-circle` | Silithus quest chain + AQ20 raid contribution + twilight cultist drops | `Questing` + `Raid` (raid lockout is owned by [`raids.md`](raids.md); reputation file does not redefine raid tasks) |
| `rep.thorium-brotherhood` | Thorium ore / Dark Iron Residue / Core Leather gathering + Lokhtos turn-ins (treated as `TurnInQuestTask` against repeatable quests) | `Gathering` + `Questing` |
| `rep.zandalar-tribe` | Zul'Gurub bijoux / coins / pristine hides drops + Yojamba Isle turn-ins | `Raid` (drops sourced from `raid.zg`) + `Questing` |

The orchestrator (`ReputationGrindTask`, defined below) decides per-bot
which underlying family to dispatch each tick; it never claims a
`Reputation` family head of its own.

## Required task families

| Task | Status | Anchor |
|---|---|---|
| `ReputationGrindTask` (orchestrator) | not-started | no file at `Exports/BotRunner/Tasks/Progression/ReputationGrindTask.cs` |
| `MobKillRepTask` | not-started — kill X mobs in faction-yielding zone (proxy over `Combat` family) | not-started |
| `DonationRepTask` | not-started — repeatable vendor/quest turn-in to faction NPC | not-started |
| `BijouCoinTurnInTask` | not-started — ZG bijou/coin/hide turn-in chain | not-started |
| `TurnInQuestTask` (reused) | reused from `Questing` family — see [`quests.md`](quests.md) | `Exports/BotRunner/Tasks/CompleteQuestTask.cs` (shipped, atomic) |
| `PullTargetTask` / `PvERotationTask` (reused) | reused from `Combat` family — see [`combat.md`](combat.md) | per-class `BotProfiles/<ClassSpec>/Tasks/PullTargetTask.cs` (shipped) |
| `GatheringRouteTask` (reused for material grinds) | reused from `Gathering` family — see [`professions-gathering.md`](professions-gathering.md) | `Exports/BotRunner/Tasks/GatheringRouteTask.cs` (shipped) |

## Coordinator

No standalone `ReputationCoordinator`. Per-bot reputation goals are
expressed as `ReputationGoal` rows in `CharacterBuildConfig`
(`Exports/BotRunner/Progression/ReputationGoal.cs`) and scheduled
through `AutomatedModeHandler` → `ReputationGrindTask` once the bot
finishes its current `AssignedActivity`. Multi-bot orchestration for
the AQ20 / ZG raid feeders is owned by the existing `RaidCoordinator`
(see [`raids.md`](raids.md)); the reputations slot only ensures
`ReputationProgress` is read back into the snapshot when a raid grants
faction standing.

## Task specifications

The `Reputation` family is realized through one orchestrator plus
reuse of `Combat`, `Questing`, and `Gathering` family tasks. The
orchestrator is documented per R19 (current shipped surface + target
Phase 1 surface); the underlying family tasks are cross-referenced to
their owning family file rather than redefined.

### ReputationGrindTask (orchestrator)

1. **Class declaration** — **Planned anchor:**
   `Exports/BotRunner/Tasks/Progression/ReputationGrindTask.cs`,
   namespace `BotRunner.Tasks.Progression`. Status: `not-started`. No
   `ReputationGrindTask.cs` file exists today; the closest related
   files are `Exports/BotRunner/Progression/ReputationGoal.cs` (data
   record for `(FactionId, FactionName, TargetStanding, GrindMethod)`
   plus `ReputationProgress`) and `Exports/BotRunner/Tasks/Progression/`
   (sibling orchestrators `FarmBossTask.cs`, `LevelUpTrainerTask.cs`,
   `MountAcquisitionTask.cs` follow the same `BotTask`-derived
   pattern this task will adopt).

2. **Public surface**
   - **Current shipped surface:** none. The atomic step `CompleteQuestTask`
     exists (`Exports/BotRunner/Tasks/CompleteQuestTask.cs:11` —
     `CompleteQuestTask(IBotContext botContext, int rewardIndex = 0)`),
     but no orchestrator that selects between kill / quest / donation /
     turn-in flows per faction.
   - **Target surface (Phase 1):** `class ReputationGrindTask : BotTask, IBotTask`.
     - Constructor: `ReputationGrindTask(IBotContext botContext, ReputationGoal goal)`
       where `goal.GrindMethod` selects the sub-flow
       (`"Quests"` | `"Dungeon:<id>"` | `"Turnin:<itemTag>"` |
       `"Mob:<creatureTag>"`, matching the comment on
       `ReputationGoal.cs:19`).
     - Override per `IBotTask` Phase 1 contract:
       `TickAsync(BotTaskContext context, CancellationToken ct)`,
       `OnPushedAsync`, `OnPoppedAsync`, `OnChildFailedAsync`. Until
       per-family async refactor lands, this task may inherit the
       `BotTask` async shim (S1.0/R25) and put its body in legacy
       `Update()`.
     - Properties: `string Name => $"ReputationGrindTask:{Goal.FactionName}->{Goal.TargetStanding}"`,
       `ReputationGoal Goal { get; }`.
     - Internal state: cached `ReputationProgress`, last-tick standing
       delta, child sub-task reference (one of `MobKillRepTask`,
       `DonationRepTask`, `BijouCoinTurnInTask`, or `TurnInQuestTask`).

3. **Snapshot contract** — **Planned:**
   - Reads (from `IObjectManager` via `BotTaskContext`):
     `Player.Position`, `Player.Level`, `Player.Reputations` (the
     per-faction standing table — see SMSG_INITIALIZE_FACTIONS handler
     in `Exports/WoWSharpClient/`), `Player.QuestLog` (active
     repeatable quests for the faction), `Player.Inventory` (turn-in
     materials).
   - Writes (effects observable in `WoWActivitySnapshot`):
     - `loadoutStatus` is **not** mutated (orchestrator is not part of
       initial loadout).
     - A new `reputationProgress` snapshot field surfaces
       `ReputationProgress` for the active goal (currentStanding,
       currentRep, isComplete). This field does not exist in
       `Communication.proto` today — adding it is part of the slot's
       scope.
     - `currentAction` mirrors the active sub-task
       (`PullTarget` / `CastSpell` / `Interact` / `CompleteQuest`).
     - `nearbyUnits` and `nearbyObjects` are read through, not written.
   - Sub-tasks pushed (one at a time, mutually exclusive):
     - `PullTargetTask` + `PvERotationTask` (Combat path, e.g.
       Timbermaw furbolg kill).
     - `AcceptQuestTask` + `KillObjectiveTask`/`CollectObjectiveTask`
       + `CompleteQuestTask` (Questing path, e.g. Argent Dawn quests).
     - `GatheringRouteTask` + `CompleteQuestTask` (Gathering →
       turn-in path, e.g. Thorium Brotherhood ore).
     - `InteractWithUnitTask` + `CompleteQuestTask` against the ZG
       turn-in NPC (`BijouCoinTurnInTask` sub-flow).

4. **BG protocol footprint** — **Planned:**
   - `Opcode.SMSG_INITIALIZE_FACTIONS = 0x122` and
     `Opcode.SMSG_SET_FACTION_STANDING = 0x124`
     (`Exports/GameData.Core/Enums/Opcode.cs:295-297`) — read by the
     reputation-tracking subsystem; the orchestrator consumes the
     resulting `Player.Reputations` view.
   - `Opcode.SMSG_SET_FACTION_VISIBLE = 0x123` — gates whether a
     faction is even known to the bot before grinding.
   - Sub-flow opcodes are emitted by the pushed child tasks, not by
     `ReputationGrindTask` itself:
     - Combat path: `CMSG_SET_SELECTION`, `CMSG_CAST_SPELL`,
       `CMSG_ATTACKSWING` (via `Combat` family).
     - Quest path: `CMSG_QUESTGIVER_HELLO` (`0x183` region;
       `QuestNetworkClientComponent.cs:160`),
       `CMSG_QUESTGIVER_ACCEPT_QUEST` (`QuestNetworkClientComponent.cs:219`),
       `CMSG_QUESTGIVER_COMPLETE_QUEST` (`QuestNetworkClientComponent.cs:248`),
       `CMSG_QUESTGIVER_CHOOSE_REWARD` (`QuestNetworkClientComponent.cs:308`)
       — owned by `AcceptQuestTask` / `CompleteQuestTask`.
     - Gathering path: `CMSG_GAMEOBJ_USE` and loot opcodes via
       `Gathering` family.
   - No new opcodes are introduced by the orchestrator.

5. **FG memory footprint** — **Planned:**
   - `IObjectManager.Player` (Position, Level, QuestLog, Inventory).
   - Reputation table reads — `IWoWLocalPlayer` extension covering the
     SMSG_INITIALIZE_FACTIONS-derived standing list. The
     `ReputationStanding` enum
     (`Exports/BotRunner/Progression/ReputationGoal.cs:3-13`) is the
     domain type; the FG memory-reader path that surfaces it is
     **planned** (no `IWoWLocalPlayer.Reputations` property exists in
     `Exports/GameData.Core/Interfaces/IWoWLocalPlayer.cs` today).
   - Child-task indirection via
     `Container.ClassContainer.CreatePvERotationTask`,
     `Container.ClassContainer.CreatePullTargetTask`,
     `Container.CreateTurnInQuestTask` (planned helper).
   - No direct `LuaCall(...)`. The `GetFactionInfoByID` Lua call is
     intentionally avoided so FG and BG read reputations through the
     same `IObjectManager` surface (FG via memory, BG via the
     SMSG_INITIALIZE_FACTIONS handler).
   - `.modify reputation` GM verb (test-mode only, per
     [`Spec/03_BOTRUNNER.md#loadout`](../../Spec/03_BOTRUNNER.md#loadout))
     is invoked by `LoadoutTask`, **not** by `ReputationGrindTask`
     itself; the orchestrator only drives the automated-mode path.

6. **Test anchor** — **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/Reputation/ReputationGrindTaskTests.cs::ReputationGrind_TimbermawHold_FromNeutralToFriendly`
   (per-faction smoke; one test per catalog row,
   five tests total). Status: `not-started`. No reputation- or
   faction-named test file exists today under
   `Tests/BotRunner.Tests/LiveValidation/` (the existing
   `Tests/BotRunner.Tests/Combat/FactionDataTests.cs` is unit-level
   `FactionData.GetReaction` coverage only). Until the new test lands,
   the closest live coverage is the unit-level reputation tracking in
   `Tests/BotRunner.Tests/Progression/ReputationTrackingTests.cs`
   (asserts `ReputationProgress` math against synthetic standing
   deltas). Filter (once the LiveValidation test exists):
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~ReputationGrindTaskTests"`.

7. **Catalog `TaskFamily` claim** — varies per row (R16 family-head
   resolution table above):
   - `rep.timbermaw-hold` → `Combat` + `Questing`.
   - `rep.argent-dawn` → `Combat` + `Questing`.
   - `rep.cenarion-circle` → `Questing` + `Raid`.
   - `rep.thorium-brotherhood` → `Gathering` + `Questing`.
   - `rep.zandalar-tribe` → `Raid` + `Questing`.
   `ReputationGrindTask` itself never claims a `Reputation` family
   head; the orchestrator records the active dispatch family in
   `currentAction` snapshot writes (see bullet 3) so the catalog
   contract remains R16-valid at every tick.

### TurnInQuestTask (reused — Questing family)

1. **Class declaration** — `class CompleteQuestTask : BotTask, IBotTask`
   in namespace `BotRunner.Tasks`. File:
   `Exports/BotRunner/Tasks/CompleteQuestTask.cs`. Status: shipped
   (atomic); the reputation-family slot does not redefine it. The
   `TurnInQuestTask` name used by
   [`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../../Spec/03_BOTRUNNER.md#catalog-of-task-families)
   maps onto the existing `CompleteQuestTask` for atomic turn-ins; a
   higher-level wrapper that chains `InteractWithUnitTask` →
   `CompleteQuestTask` for repeatable faction turn-ins is **planned**
   under `Exports/BotRunner/Tasks/Progression/FactionTurnInTask.cs`
   (not yet present).

2. **Public surface** — see [`quests.md`](quests.md) §`TurnInQuestTask`
   for the full eight-bullet spec. Reputation reuse:
   - Current shipped: `CompleteQuestTask(IBotContext botContext, int rewardIndex = 0)`
     (`CompleteQuestTask.cs:11`); inherits the `BotTask` async shim
     (S1.0/R25 — `TickAsync` → `OnTick` → legacy `Update()`).
   - The reputation orchestrator selects `rewardIndex` for repeatable
     faction quests (Rune Cloth → Argent Dawn, Iron Bar → Thorium
     Brotherhood, Bijou/Coin → Zandalar Tribe). Quest IDs and reward
     indices live in `Bot/reputations/<faction>.json` per slot SRep.1
     (below).

3-7. **Snapshot contract / BG protocol / FG memory / test anchor /
catalog claim** — see [`quests.md`](quests.md) §`TurnInQuestTask`.
Reputation rows extend the catalog claim with the `Questing` family
membership recorded in the table above.

### MobKillRepTask (proxy over Combat family)

1. **Class declaration** — **Planned anchor:**
   `Exports/BotRunner/Tasks/Progression/MobKillRepTask.cs`,
   namespace `BotRunner.Tasks.Progression`. Status: `not-started`.
   Implemented as a thin sequencer over the `Combat` family's
   `PullTargetTask` + `PvERotationTask` + `LootCorpseTask`, scoped to
   a faction-yielding creature set
   (`mangos.creature_onkill_reputation`). See [`combat.md`](combat.md)
   for the underlying tasks.

2. **Public surface** — **Planned:**
   - Constructor: `MobKillRepTask(IBotContext botContext, FactionGrindZone zone, int killBudget)`
     where `FactionGrindZone` carries (zoneId, creature entry list,
     centroid + radius, expected rep yield per kill from
     `mangos.creature_onkill_reputation`).
   - Override per Phase 1 `IBotTask`: `TickAsync`, lifecycle hooks.
   - Behavior: loops `GoToTask(nextNearestHostile)` → push
     `Container.ClassContainer.CreatePullTargetTask` → push
     `CreatePvERotationTask` → push `LootCorpseTask` → repeat until
     `killBudget` consumed or `Goal.IsComplete`.

3. **Snapshot contract** — **Planned:** identical to `Combat` family
   (see [`combat.md`](combat.md)) plus `reputationProgress` write on
   each `SMSG_SET_FACTION_STANDING` packet observed.

4. **BG protocol footprint** — **Planned:** all opcodes inherited from
   `Combat` family children (`CMSG_SET_SELECTION`, `CMSG_CAST_SPELL`,
   `CMSG_ATTACKSWING`, loot opcodes). The reputation-side signal is
   `SMSG_SET_FACTION_STANDING = 0x124` (read-only consumption).

5. **FG memory footprint** — **Planned:** inherited from `Combat`
   family; no new FG memory reads.

6. **Test anchor** — **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/Reputation/MobKillRepTaskTests.cs::MobKillRep_Timbermaw_DeadwoodFurbolg_GainsRep`.
   Status: `not-started`. Filter (once present):
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~MobKillRepTaskTests"`.

7. **Catalog `TaskFamily` claim** — `Combat`. Serves the Combat-grind
   half of `rep.timbermaw-hold` and `rep.argent-dawn` (see family
   table above). No standalone reputation family head is claimed.

### DonationRepTask (proxy over Questing/Economy)

1. **Class declaration** — **Planned anchor:**
   `Exports/BotRunner/Tasks/Progression/DonationRepTask.cs`,
   namespace `BotRunner.Tasks.Progression`. Status: `not-started`. The
   Argent Dawn cloth donation and Thorium Brotherhood ore turn-in
   flows are realized as repeatable quests on vmangos (e.g.
   `mangos.quest_template` rows for "Rune Cloth", "Mageweave to the
   Argent Dawn"), so the task is a faction-aware wrapper around
   `InteractWithUnitTask` → `AcceptQuestTask` → `CompleteQuestTask`.

2. **Public surface** — **Planned:**
   - Constructor: `DonationRepTask(IBotContext botContext, FactionDonationPlan plan)`
     where `FactionDonationPlan` carries (turnInNpcEntry, repeatable
     questId, requiredItemId, requiredCount, expectedRepPerTurnIn).
   - Override per Phase 1 `IBotTask`: `TickAsync`, lifecycle hooks.
   - Behavior: verify inventory count → `GoToTask(turnInNpc.Position)`
     → push `InteractWithUnitTask(turnInNpc)` → push
     `AcceptQuestTask(questId)` → push `CompleteQuestTask` → repeat
     until inventory exhausted or `Goal.IsComplete`.

3. **Snapshot contract** — **Planned:**
   - Reads: `Player.Inventory` for the required item count,
     `Player.QuestLog` to detect already-active turn-in quest,
     `Player.Reputations` for the active faction's standing.
   - Writes: `currentAction = Interact` / `AcceptQuest` /
     `CompleteQuest` per phase; `reputationProgress` after each
     turn-in.

4. **BG protocol footprint** — **Planned:** inherited from the
   `Questing` family — `CMSG_QUESTGIVER_HELLO`,
   `CMSG_QUESTGIVER_ACCEPT_QUEST`, `CMSG_QUESTGIVER_COMPLETE_QUEST`,
   `CMSG_QUESTGIVER_CHOOSE_REWARD`
   (`QuestNetworkClientComponent.cs:160,219,248,308`). No new opcodes.

5. **FG memory footprint** — **Planned:** `IObjectManager.Items`,
   `IObjectManager.QuestFrame`, `IObjectManager.GossipFrame`
   (for some turn-in NPCs that route through gossip first).

6. **Test anchor** — **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/Reputation/DonationRepTaskTests.cs::DonationRep_ArgentDawn_RuneClothTurnIn_GainsRep`.
   Status: `not-started`.

7. **Catalog `TaskFamily` claim** — `Questing`. Serves the donation
   half of `rep.argent-dawn` and `rep.thorium-brotherhood`. Material
   sourcing for donations is delegated to the `Gathering` family
   (`GatheringRouteTask`) and is documented under
   [`professions-gathering.md`](professions-gathering.md), not here.

### BijouCoinTurnInTask (ZG-specific, proxy over Questing)

1. **Class declaration** — **Planned anchor:**
   `Exports/BotRunner/Tasks/Progression/BijouCoinTurnInTask.cs`,
   namespace `BotRunner.Tasks.Progression`. Status: `not-started`.
   Structurally identical to `DonationRepTask`; broken out because
   ZG bijoux/coins/pristine hides are sourced from `raid.zg`
   (Raid family, see [`raids.md`](raids.md)) rather than from
   gathering routes — the task's `Depends on: SR.zg` declaration
   below reflects this drop-source coupling.

2. **Public surface** — **Planned:**
   - Constructor: `BijouCoinTurnInTask(IBotContext botContext, ZandalarTurnInPlan plan)`
     where `ZandalarTurnInPlan` carries the bijou/coin/hide item ID
     set, the Yojamba Isle turn-in NPC entries, and the per-item
     repeatable quest IDs.
   - Override per Phase 1 `IBotTask`: `TickAsync`, lifecycle hooks.
   - Behavior mirrors `DonationRepTask` (inventory check → travel →
     interact → turn-in) but tolerates multi-item inventories
     (mixed bijoux/coins) by dispatching the correct repeatable quest
     per item.

3. **Snapshot contract** — **Planned:** same shape as
   `DonationRepTask`; `reputationProgress` writes after each turn-in.

4. **BG protocol footprint** — **Planned:** same as `DonationRepTask`
   (Questing family opcodes; no new opcodes).

5. **FG memory footprint** — **Planned:** same as `DonationRepTask`.

6. **Test anchor** — **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/Reputation/BijouCoinTurnInTaskTests.cs::BijouCoinTurnIn_Zandalar_PrimalBijou_GainsRep`.
   Status: `not-started`. Live execution depends on a stocked ZG raid
   loot table for the test bot.

7. **Catalog `TaskFamily` claim** — `Questing` for the turn-in step
   itself; the drop step is owned by `Raid` (`raid.zg` row in
   [`00_INDEX.md`](00_INDEX.md)) and is not redefined here. Serves
   `rep.zandalar-tribe`.

## Slots

### SRep.1 — Per-faction grind specs

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Bot/reputations/<faction>.json`
- **Goal:** Per faction: mob list with rep yield, donation table,
  optional quest chain. From `mangos.creature_onkill_reputation` and
  `mangos.reputation_reward_rate`.

### SRep.2 — `ReputationGrindTask` orchestrator

- **Owner:** `monorepo-worker`
- **Status:** open

### SRep.3 — Donation rep (Argent Dawn cloth, Thorium ore)

- **Owner:** `monorepo-worker`
- **Status:** open

### SRep.4 — Bijou/coin turn-in (Zandalar)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** SR.zg (raid drops the bijoux/coins)

### SRep.5 — LiveValidation per faction

- **Owner:** `monorepo-test-runner`
- **Status:** open

## Failure recovery

- **Faction standing decreased** (Timbermaw enemy faction kills) →
  expected; rep tasks track net standing.
- **No mobs left in pull radius** → walk to next sub-route.
