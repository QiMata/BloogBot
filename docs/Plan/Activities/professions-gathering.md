# Activities — Gathering Professions

3 catalog rows for gathering routes: Mining, Herbalism, Skinning.
Plus secondary Fishing.

## Task families (existing)

| Task | Status | Anchor |
|---|---|---|
| `GatheringRouteTask` | done | `Exports/BotRunner/Tasks/GatheringRouteTask.cs` |
| `GatherNodeTask` | done (mining/herbalism) | `Exports/BotRunner/Tasks/GatherNodeTask.cs` |
| `FishingTask` | done — Ratchet pier proven green | `Exports/BotRunner/Tasks/FishingTask.cs` |
| `SkinningTask` | partial — shipped today as `SkinCorpseTask` | `Exports/BotRunner/Tasks/SkinCorpseTask.cs` |

## Task specifications

> Phase 0 / S0.8.7 precision blocks. One entry per task in the family
> table above per `Spec/03_BOTRUNNER.md#catalog-of-task-families`.
>
> **Interface drift note (R19).** `Spec/03_BOTRUNNER.md` documents
> `IBotTask` as the async `TickAsync` / `OnPushedAsync` /
> `OnPoppedAsync` / `OnChildFailedAsync` contract. The shipped
> interface at `Exports/BotRunner/Interfaces/IBotTask.cs` is now the
> Phase 1 target contract (`TickAsync` / `OnPushedAsync` /
> `OnPoppedAsync` / `OnChildFailedAsync` + `Name` + `Status`). The
> `BotTask` base class (`Exports/BotRunner/Tasks/BotTask.cs`) ships
> the S1.0 shim per R25: `TickAsync` → `OnTick` → legacy `Update()`
> body, with `PopTask(string reason)` retained. Existing gathering
> tasks keep their `Update()` body unchanged; per-family async
> refactor lands under S1.9 Gathering. Public-surface bullets below
> report the current shipped shape (post-S1.0 shim) and the target
> shape per Spec/03.
>
> **Snapshot-contract scope.** Tasks do not directly mutate
> `WoWActivitySnapshot`; that proto is built by `SnapshotBuilder` from
> `IObjectManager` state + the top of the task stack. "Reads" lists
> snapshot fields the task consumes (via the equivalent
> `IObjectManager` property today). "Writes" lists fields whose value
> changes as a side effect of the task running (so tests poll the
> right field).
>
> **Loot opcode chain.** All four gathering tasks ultimately drive
> the same vanilla 1.12.1 loot exchange:
> `Opcode.CMSG_LOOT = 0x15D` (open the corpse/node loot window),
> `Opcode.CMSG_AUTOSTORE_LOOT_ITEM = 0x108` (move each slot into
> the player's bags), and `Opcode.CMSG_LOOT_RELEASE = 0x15F` (close
> the window). The shipped BG path routes through
> `WoWSharpObjectManager.LootTargetAsync` →
> `InventoryManager.LootTargetAsync` at
> `Exports/WoWSharpClient/InventoryManager.cs:406` which sends
> `CMSG_LOOT` directly when no `LootingAgent` is wired, otherwise it
> delegates to `LootingAgent.QuickLootAsync` (see
> `Exports/WoWSharpClient/Networking/ClientComponents/LootingNetworkClientComponent.cs`).

### GatheringRouteTask

- **Class declaration:** `BotRunner.Tasks.GatheringRouteTask` at
  `Exports/BotRunner/Tasks/GatheringRouteTask.cs`. Inherits
  `BotTask` and implements `IBotTask`. **Status:** done.
  **Target surface (per R19):** the same logic re-shaped against
  `TickAsync(BotTaskContext, CancellationToken)` /
  `OnPushedAsync` / `OnPoppedAsync` / `OnChildFailedAsync` after
  S1.0 lands; the nine internal `GatheringState` transitions
  (`BuildRoute → MoveToCandidate → SearchVisibleNode →
  MoveToVisibleNode → AwaitGatherCast → AwaitGatherChannel →
  AwaitGatherRetry → LootNode → PostGatherCooldown`) become an
  internal field driven from `TickAsync`. The combat-pause branch
  (`PauseForCombat` / `ResumeAfterCombat`) becomes a child-task
  push (`PvERotationTask`) gated by
  `OnChildFailedAsync(child, reason) → resume`.
- **Public surface (current):**
  - `public GatheringRouteTask(IBotContext botContext,
    IReadOnlyList<Position> routeCandidates,
    IReadOnlyCollection<uint> nodeEntries, int gatherSpellId,
    int targetSuccessCount = 1, int maxRouteLoops = 1)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only)..
  - `internal static IReadOnlyList<Position> OptimizeRoute(
    Position start, IReadOnlyList<Position> candidates)` —
    nearest-neighbour candidate ordering.
  - `internal static Position ComputeApproachPosition(
    Position playerPosition, Position nodePosition)` — backs off
    to `GatherRange - GatherApproachBuffer` (4.5y) along the
    player→node vector.
  - Internal `enum GatheringState { BuildRoute, MoveToCandidate,
    SearchVisibleNode, MoveToVisibleNode, AwaitGatherCast,
    AwaitGatherChannel, AwaitGatherRetry, LootNode,
    PostGatherCooldown }`.
  - Tunables: `CandidateReachDistance = 12f`,
    `VisibleNodeDistance = 80f`, `GatherRange = 5f`,
    `GatherCastDelayMs = 500`, `GatherChannelMs = 5000`,
    `MaxGatherAttempts = 4`, `PostGatherCooldownMs = 3000`.
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):*
    `player.Position`, `player.IsInCombat`,
    `player.MapId`, `IObjectManager.GameObjects` (filtered by the
    constructor's `nodeEntries` allow-set), `IObjectManager.LootFrame.IsOpen`
    / `.LootCount`. Surfaces as
    `WoWActivitySnapshot.player.Position`,
    `WoWActivitySnapshot.player.IsInCombat`,
    `WoWActivitySnapshot.nearbyGameObjects`,
    `WoWActivitySnapshot.lootFrame`.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.player.Position` (driven by
    `TryNavigateToward` / `MoveToward`),
    `WoWActivitySnapshot.movementData.MovementFlags` (start/stop
    edges through `ObjectManager.StopAllMovement` /
    `ForceStopImmediate`), `WoWActivitySnapshot.recentChatMessages`
    via `BotContext.AddDiagnosticMessage` (`[TASK]
    GatheringRouteTask route_ready|candidate_start|node_visible|
    gather_started|gather_success|...`). Inventory deltas
    (`WoWActivitySnapshot.bagItems`) fire after the loot frame
    closes via the standard `CMSG_AUTOSTORE_LOOT_ITEM` chain.
- **BG protocol footprint:**
  - `Opcode.CMSG_CAST_SPELL = 0x12E` — `ObjectManager.CastSpellOnGameObject(
    _gatherSpellId, _activeNodeGuid)` (e.g. spell 2575 Mining,
    2366 Herb Gathering — see
    `Exports/BotRunner/Combat/GatheringData.cs:100`/`:106`).
  - `Opcode.CMSG_LOOT = 0x15D` — opens the node loot frame
    (server may auto-open on cast success; the explicit fallback is
    `InventoryManager.LootTargetAsync`).
  - `Opcode.CMSG_AUTOSTORE_LOOT_ITEM = 0x108` — per-slot store
    (`ObjectManager.LootFrame.LootAll()` walks the slots).
  - `Opcode.CMSG_LOOT_RELEASE = 0x15F` —
    `ObjectManager.LootFrame.Close()`.
  - Standard movement opcode set during the
    `MoveToCandidate` / `MoveToVisibleNode` walks
    (`MSG_MOVE_HEARTBEAT`, `MSG_MOVE_START_FORWARD`,
    `MSG_MOVE_STOP`, `MSG_MOVE_SET_FACING`).
- **FG memory footprint:**
  - `IObjectManager.Player` (position, combat flag, map).
  - `IObjectManager.GameObjects` (LINQ filter by `Entry ∈
    _nodeEntries` and `DistanceTo(candidate) ≤ 80y`).
  - `IObjectManager.GetClosestGameObject` is NOT used — the task
    keeps its own candidate-anchored scan
    (`FindVisibleNodeNearCandidate`) so route ordering wins over
    raw proximity.
  - `IObjectManager.InteractWithGameObject(ulong guid)` (right-click
    the node).
  - `IObjectManager.CastSpellOnGameObject(int spellId,
    ulong guid)`, `IObjectManager.SetTarget(ulong)`.
  - `IObjectManager.MoveToward(Position)` /
    `StopAllMovement()` / `ForceStopImmediate()` /
    `Face(Position)`.
  - `IObjectManager.LootFrame` (`.IsOpen`, `.LootCount`,
    `.LootAll()`, `.Close()`).
  - `EventHandler.OnErrorMessage` for `Cast failed for spell …
    TRY_AGAIN` → schedule retry.
  - No `LuaCall`. Combat preemption pushes a `PvERotationTask`
    via `Container.ClassContainer.CreatePvERotationTask`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.GatheringProfessionTests.Mining_BG_GatherCopperVein`
  at `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs:50`.
  Sibling coverage: `Mining_FG_GatherCopperVein` (line 70),
  `Herbalism_BG_GatherHerb` (line 90),
  `Herbalism_FG_GatherHerb`, plus
  `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelectionTests.cs`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein"`
- **Catalog `TaskFamily` claim:** `Gathering`. Underlies catalog
  rows `prof.mining-route`, `prof.herbalism-route` from
  `Plan/Activities/00_INDEX.md`. Activated by
  `ActionType.StartGatheringRoute` (= `CharacterAction.StartGatheringRoute`,
  `Exports/BotRunner/BotRunnerService.ActionMapping.cs:89`).
  Dispatch metadata is the candidate position list +
  `nodeEntries` allow-set + gather spell id (mining=2575,
  herbalism=2366).

### GatherNodeTask

- **Class declaration:** `BotRunner.Tasks.GatherNodeTask` at
  `Exports/BotRunner/Tasks/GatherNodeTask.cs`. Primary-constructor
  form: `public class GatherNodeTask(IBotContext botContext,
  ulong nodeGuid) : BotTask(botContext), IBotTask`.
  **Status:** done (mining/herbalism).
  **Target surface (per R19):** post-S1.0 the body migrates from
  `Update()` to `TickAsync`; the 30-second wall-clock timeout
  becomes a `CancellationToken` deadline on the task context.
- **Public surface (current):**
  - `public GatherNodeTask(IBotContext botContext, ulong nodeGuid)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only)..
  - Constant: `private const float GATHER_RANGE = 5f`.
  - **Open:** the current shipped task only navigates to the node,
    sets target, and pops — it does **not** issue
    `CastSpellOnGameObject` or open the loot frame. That work is
    done by `GatheringRouteTask` when the action is
    `StartGatheringRoute`; an atomic
    `ActionType.GatherNode` dispatch alone is therefore a
    half-step (sets the target so the caller can finish the
    interact + loot loop). This is a known atomic-task gap.
- **Snapshot contract:**
  - *Reads:* `player.Position`, `player.IsInCombat`,
    `player.IsSwimming`, `IObjectManager.GameObjects`
    (linear search for `Guid == nodeGuid`).
    Snapshot fields: `WoWActivitySnapshot.player.{Position,
    IsInCombat, IsSwimming}`,
    `WoWActivitySnapshot.nearbyGameObjects`.
  - *Writes/mutates:* `WoWActivitySnapshot.player.Position` via
    `NavigateToward(node.Position)`,
    `WoWActivitySnapshot.player.TargetGuid` via
    `ObjectManager.SetTarget(nodeGuid)`. Pops with reason
    `interact` after target is set (no loot frame interaction
    today).
- **BG protocol footprint:**
  - Movement opcodes during the walk leg (`MSG_MOVE_HEARTBEAT`,
    `MSG_MOVE_START_FORWARD`, `MSG_MOVE_STOP`,
    `MSG_MOVE_SET_FACING`).
  - `Opcode.CMSG_SET_SELECTION = 0x13D` — `ObjectManager.SetTarget(
    nodeGuid)` (target sync for the next action).
  - **Open:** to actually loot the node the caller currently has
    to follow up with the `GatheringRouteTask` chain
    (`CMSG_CAST_SPELL → CMSG_LOOT → CMSG_AUTOSTORE_LOOT_ITEM →
    CMSG_LOOT_RELEASE`). Closing that gap is what promotes the
    family-row status from "atomic-only" to "stand-alone".
- **FG memory footprint:**
  - `IObjectManager.Player` (`.Position`, `.IsInCombat`,
    `.IsSwimming`).
  - `IObjectManager.GameObjects.FirstOrDefault(go => go.Guid ==
    nodeGuid)`.
  - `IObjectManager.MoveToward(Position)` /
    `StopAllMovement()` (`NavigateToward` in `BotTask` base).
  - `IObjectManager.SetTarget(ulong)`.
  - No `LuaCall`. No `InteractWithGameObject` today.
- **Test anchor:** **Planned anchor:**
  `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs::GatherNode_AtomicDispatch_AtRangeAndTargets`.
  No existing test exercises `ActionType.GatherNode` in
  isolation; the end-to-end flow is covered by
  `GatheringProfessionTests.Mining_*` /
  `Herbalism_*` which dispatch `StartGatheringRoute`. Mark
  `tests` row `not-started` for the atomic case.
- **Catalog `TaskFamily` claim:** `Gathering`. Atomic substep used
  by `prof.mining-route`, `prof.herbalism-route`. Activated by
  `ActionType.GatherNode` (= `CharacterAction.GatherNode`,
  `BotRunnerService.ActionMapping.cs:82`).

### FishingTask

- **Class declaration:** `BotRunner.Tasks.FishingTask` at
  `Exports/BotRunner/Tasks/FishingTask.cs`. Inherits `BotTask`
  and implements `IBotTask`. **Status:** done — Ratchet pier
  proven green.
  **Target surface (per R19):** post-S1.0 the 17-state
  `FishingState` machine becomes a private field driven from
  `TickAsync`; outfit/lure subflows are candidates for
  child-task extraction (`EquipFishingPoleTask`,
  `ApplyLureTask`) once S1.0 standardises composition.
- **Public surface (current):**
  - `public FishingTask(IBotContext botContext,
    IReadOnlyList<Position>? searchWaypoints = null,
    string? location = null, bool useGmCommands = false,
    uint? masterPoolId = null)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only)..
  - Internal `enum FishingState { EnsureGearAndSpells,
    AwaitGearAndSpells, TravelToLocation, AwaitTravelArrival,
    EnsurePoleEquipped, AwaitPoleEquip, EnsureLureApplied,
    AwaitLureApply, AcquireFishingPool, SearchForPool,
    MoveToFishingPool, ResolveAndCast, AwaitCastConfirmation,
    AwaitCatchResolution, AwaitLootWindow, LootCatch,
    AwaitLootCompletion }`.
  - Constants (`Exports/BotRunner/Combat/FishingData.cs`):
    `FishingRank1..5 = 7620 / 7731 / 7732 / 18248 / 33095`,
    `FishingPoleProficiency = 7738`,
    `FishingPoleItemId = 6256`, `NightcrawlerBaitItemId`,
    plus the rank table in `ResolveCastableFishingSpellId`.
  - Cast/approach tunables:
    `DesiredPoolDistance = 24f`, `MinCastingDistance = 10f`,
    `MaxCastingDistance = 38f`, `IdealCastingDistanceFromPool = 18f`,
    `CastConfirmationTimeoutMs = 5000`,
    `CatchResolutionTimeoutMs = 28000`,
    `LootWindowTimeoutMs = 5000`,
    `MaxPoolLockDistance = 80f`.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.{Position, MapId, IsMoving,
    IsCasting, IsChanneling, ChannelingId, IsInCombat}`,
    `IObjectManager.GameObjects` (fishing pool entries +
    bobber lookup), `IObjectManager.KnownSpellIds` (for
    `FishingData.ResolveCastableFishingSpellId`),
    `IObjectManager.Items` (pole + lure inventory),
    `IObjectManager.LootFrame.{IsOpen, LootCount}`.
    Snapshot surface:
    `WoWActivitySnapshot.player.{Position, MapId, IsCasting,
    IsChanneling}`, `WoWActivitySnapshot.nearbyGameObjects`,
    `WoWActivitySnapshot.bagItems`, `WoWActivitySnapshot.lootFrame`.
  - *Writes/mutates:* `WoWActivitySnapshot.player.Position` (the
    pre-cast approach walks),
    `WoWActivitySnapshot.player.IsCasting` (rises on cast, clears
    on bobber spawn or interrupt),
    `WoWActivitySnapshot.bagItems` deltas after the catch loot,
    `WoWActivitySnapshot.recentChatMessages` via
    `BotContext.AddDiagnosticMessage` (`[TASK] FishingTask
    activity_start|pool_acquired|cast_started|
    fishing_loot_success|...`).
- **BG protocol footprint:**
  - `Opcode.CMSG_CAST_SPELL = 0x12E` — fishing-rank spell cast
    (`ObjectManager.CastSpell((int)_fishingSpellId)` at
    `Exports/BotRunner/Tasks/FishingTask.cs:1011`). Pole equip
    + lure application also flow through this opcode via
    `IObjectManager.CastSpell`.
  - `Opcode.CMSG_USE_ITEM = 0x0AB` — alternate path for applying
    lure / using the pole when the spell-by-name route is not
    available (via `ItemUseNetworkClientComponent`,
    `Exports/WoWSharpClient/Networking/ClientComponents/ItemUseNetworkClientComponent.cs`).
  - `Opcode.CMSG_LOOT = 0x15D` — server typically auto-opens the
    bobber loot frame; the fallback path is the standard
    `InventoryManager.LootTargetAsync` chain.
  - `Opcode.CMSG_AUTOSTORE_LOOT_ITEM = 0x108` —
    `LootFrame.LootAll()` walks each slot.
  - `Opcode.CMSG_LOOT_RELEASE = 0x15F` — `LootFrame.Close()` on
    completion.
  - Standard movement opcodes during pier approach
    + facing alignment (`MSG_MOVE_SET_FACING`,
    `MSG_MOVE_HEARTBEAT`).
- **FG memory footprint:**
  - `IObjectManager.Player.{Position, MapId, IsMoving, IsCasting,
    IsChanneling, ChannelingId, IsInCombat}`.
  - `IObjectManager.GameObjects` LINQ-filtered by
    fishing-pool entry ids + bobber display ids.
  - `IObjectManager.KnownSpellIds` →
    `FishingData.ResolveCastableFishingSpellId`.
  - `IObjectManager.Items` (`FishingData.FindFishingPoleInBags`,
    `HasFishingPoleEquipped`).
  - `IObjectManager.CastSpell(int)`,
    `IObjectManager.CanCastSpell(int, ulong)`,
    `IObjectManager.Face(Position)`,
    `IObjectManager.ForceStopImmediate()`,
    `IObjectManager.MoveToward(Position)`.
  - `IObjectManager.LootFrame.{IsOpen, LootCount, LootAll,
    Close}`.
  - `EventHandler.OnErrorMessage` (inventory-full handler;
    fishing also subscribes to bobber events through the
    object-manager scan, not Lua).
  - No `LuaCall`. GM-mode test paths route through
    `ObjectManager.SendChatMessage(".additem|.learn|.setskill ...")`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool`
  at `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs:48`.
  Companion helpers / analysers:
  `RatchetFishingStageAttributionTests`,
  `FishingPoolStagePlannerTests`,
  `FishingPoolActivationAnalyzerTests`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool"`
- **Catalog `TaskFamily` claim:** `Gathering`. Activated by
  `ActionType.StartFishing` (= `CharacterAction.StartFishing`,
  `BotRunnerService.ActionMapping.cs:88`); metadata shape is
  `[location, useGmCommands, masterPoolId, waypoint floats...]`
  (`Exports/BotRunner/ActionDispatcher.cs:543`). Backs the
  `event.stv-fishing-extravaganza` world-event row in
  `Plan/Activities/00_INDEX.md` and any future
  `prof.fishing-route` (not in catalog today). Activity-resolver
  string form: `"Fishing[Ratchet]"` (per
  `Spec/03_BOTRUNNER.md#activity-resolver`).

### SkinningTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/SkinningTask.cs`. **Status:**
  `not-started` as a stand-alone task; the corpse-skin atomic step
  ships today as `BotRunner.Tasks.SkinCorpseTask` at
  `Exports/BotRunner/Tasks/SkinCorpseTask.cs`. A full
  `SkinningTask` (find-skinnable-corpse → walk → cast Skinning →
  loot) does NOT exist yet — the closest end-to-end loop is
  `GatheringRouteTask` (mining/herb) plus the atomic
  `SkinCorpseTask` invoked per kill by other behaviour trees.
  The family table's `SkinningTask` row tracks this gap.
  **Target surface (per R19):** post-S1.0 the planned task is a
  routed variant of `GatheringRouteTask` keyed on `SkinnableUnits`
  (corpses with `UNIT_DYNFLAG_LOOTABLE` and the skinning loot
  template) instead of `GameObjects`. The shipped
  `SkinCorpseTask` migrates from `Update()` to `TickAsync` with
  the existing `LootTargetAsync` call awaited directly.
- **Public surface (current — `SkinCorpseTask`):**
  - `public class SkinCorpseTask(IBotContext botContext,
    ulong corpseGuid) : BotTask(botContext), IBotTask` (primary
    constructor).
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); body calls `ObjectManager.LootTargetAsync(corpseGuid, CancellationToken.None).GetAwaiter().GetResult()` and pops on completion (success or exception). Per-family async refactor lands under S1.9 Gathering (S1.0/R25, shim-only).
  - **Open:** no walk-to-corpse leg, no `Skinning` cast (spell
    8613 Apprentice ... 10768 Master per vanilla
    `mangos.spell_template`), no per-skill node-filter. The
    pre-skin spell cast is gated by the server when the player
    interacts with a skinnable corpse via the loot opcode chain.
- **Public surface (planned — `SkinningTask`):**
  - `public SkinningTask(IBotContext botContext,
    IReadOnlyList<Position> routeCandidates,
    int skinningSpellId, int targetSuccessCount = 1,
    int maxRouteLoops = 1)` — mirror `GatheringRouteTask`'s
    constructor but key the candidate scan on
    `IObjectManager.Units` filtered by
    `IsDead && IsLootable && IsSkinnable`.
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under S1.9 Gathering (S1.0/R25, shim-only).
  - Reuse `GatheringRouteTask.OptimizeRoute` /
    `ComputeApproachPosition`.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.{Position, IsInCombat}`,
    `IObjectManager.Units` (planned: filtered by
    `IsDead && IsLootable && IsSkinnable`; the shipped atomic
    just dereferences `corpseGuid`),
    `IObjectManager.LootFrame.{IsOpen, LootCount}`,
    `IObjectManager.KnownSpellIds` (planned: highest known
    Skinning rank).
    Surfaces as `WoWActivitySnapshot.player.{Position,
    IsInCombat}`, `WoWActivitySnapshot.nearbyUnits`,
    `WoWActivitySnapshot.lootFrame`.
  - *Writes/mutates:* `WoWActivitySnapshot.bagItems` deltas after
    the skin loot completes; `WoWActivitySnapshot.player.Position`
    on the walk leg (planned).
- **BG protocol footprint:**
  - `Opcode.CMSG_LOOT = 0x15D` — `InventoryManager.LootTargetAsync`
    at `Exports/WoWSharpClient/InventoryManager.cs:406`
    (current shipped path; sends `CMSG_LOOT` directly when no
    `LootingAgent` is wired, else delegates to
    `LootingAgent.QuickLootAsync`).
  - `Opcode.CMSG_AUTOSTORE_LOOT_ITEM = 0x108` — per-slot store
    via `LootFrame.LootAll()` or the agent's auto-store path.
  - `Opcode.CMSG_LOOT_RELEASE = 0x15F` —
    `InventoryManager.ReleaseLoot(ulong)` /
    `LootFrame.Close()`.
  - `Opcode.CMSG_CAST_SPELL = 0x12E` — **planned** explicit
    Skinning-spell cast (8613 rank 1, 8617 rank 2, 8618 rank 3,
    10768 rank 4 — pending verification in
    `mangos.spell_template`). The shipped atomic relies on the
    server resolving the skin action through the loot interaction
    itself.
- **FG memory footprint:**
  - `IObjectManager.Player`,
    `IObjectManager.Units` (planned filter on `IsDead`,
    `IsLootable`, `IsSkinnable` — needs verification that all
    three are surfaced on `IWoWUnit`),
    `IObjectManager.GetClosestGameObject` is **not** used —
    skinning targets are units, not game objects.
  - `IObjectManager.LootTargetAsync(ulong, CancellationToken)`
    (shipped at `Exports/GameData.Core/Interfaces/IObjectManager.cs:252`).
  - Planned: `IObjectManager.CastSpell(int skinningSpellId)`,
    `IObjectManager.SetTarget(ulong corpseGuid)`,
    `IObjectManager.MoveToward(Position)`.
  - No `LuaCall`.
- **Test anchor:** **Planned anchor:**
  `Tests/BotRunner.Tests/LiveValidation/SkinningProfessionTests.cs::Skinning_BG_SkinCorpseOnRoute`.
  No existing `Tests/BotRunner.Tests/LiveValidation/*Skin*.cs`
  file exists today; the closest exercised path is the atomic
  `ActionType.SkinCorpse` dispatch covered indirectly by combat /
  loot tests that route through `LootTargetAsync`. Mark `tests`
  row `not-started`.
- **Catalog `TaskFamily` claim:** `Gathering`. Backs catalog row
  `prof.skinning-route` from `Plan/Activities/00_INDEX.md`.
  Activation today: `ActionType.SkinCorpse` (=
  `CharacterAction.SkinCorpse`,
  `BotRunnerService.ActionMapping.cs:81`) for the atomic step.
  Planned activation for the routed variant:
  `ActionType.StartGatheringRoute` with a
  `SkinnableUnits` metadata flag, or a new
  `ActionType.StartSkinningRoute` (the latter requires a proto
  change per the action-extension recipe in
  `Spec/03_BOTRUNNER.md#actionmessage-dispatch`).

## Slots

### SG.1 — Per-zone gather route graphs

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Bot/gathering/<profession>/<zone>.json`
- **Goal:** One route graph per profession × zone. Nodes are spawn
  cluster centers from `mangos.gameobject` (mining) or
  `gameobject_template` filtered by Type=Herb/Vein.

### SG.2 — Profession-leveling activity

- **Owner:** `monorepo-worker`
- **Status:** open
- **Catalog:** `prof.mining-route`, `prof.herbalism-route`,
  `prof.skinning-route`
- **Goal:** Level the profession to 300 by walking the appropriate
  zone graph for the bot's current skill bracket.

### SG.3 — Fishing pool LiveValidation

- **Owner:** `monorepo-test-runner`
- **Status:** done (Ratchet proven green; needs catalog wiring)
- **Goal:** Validate fishing via `RequestActivity` path.

### SG.4 — Skill-based zone selection

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Bot at skill 75 mines tin in Wetlands, not copper in
  Elwynn. Per-profession skill bracket table.

## Failure recovery

- **Node tapped by other player** → continue route, find next node.
- **Mob spawn during gather** → CombatTask preempts; resume on
  combat end.
- **Skill cap reached** → request training (visit profession trainer).
