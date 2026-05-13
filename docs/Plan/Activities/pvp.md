# Activities — World PvP

> **No catalog row.** World PvP is **opportunistic**, not
> catalog-scheduled — it triggers organically whenever a bot detects a
> hostile-faction player in proximity while running some *other*
> activity (questing, gathering, travel, world-boss approach, etc.).
> Therefore there is **no row in
> [`00_INDEX.md`](00_INDEX.md)** for world PvP. The Battleground PvP
> activities `bg.wsg`, `bg.ab`, `bg.av` are catalog-scheduled and live
> in the Battleground family — see
> [`battlegrounds.md`](battlegrounds.md). The combat rotation used
> against hostile players (`PvPRotationTask`) lives in the Combat
> family — see [`combat.md#pvprotationtask`](combat.md#pvprotationtask).
>
> This file is **descriptive**: it documents the world-PvP-specific
> orchestrator tasks. It does not introduce a new family head per R16
> — every task here claims `Combat` as its catalog `TaskFamily`.

Not a catalog row of its own — PvP happens organically when bots
detect hostile players. World PvP is tracked because the activity
scheduler must decide whether to engage, flee, or call for help.

## Task families

| Task | Status |
|---|---|
| `HostilePlayerDetectionTask` | not-started |
| `PvPEngagementTask` | not-started — uses class `PvPRotationTask` |
| `PvPFleeTask` | not-started — known retreat positions, mounted run |
| `PvPCallForHelpTask` | not-started — whisper guild, post in zone channel |

## Slots

### SPvP.1 — Hostile player detection

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/PvP/HostilePlayerDetectionTask.cs`
- **Goal:** Bot snapshot tracks nearby hostile players. Trigger
  conditions: hostile within 50y, hostile attacking, hostile higher
  level by N.

### SPvP.2 — Engagement decision

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Decide engage/flee based on level diff, HP, cooldowns,
  group size, current activity priority. Engagement defaults:
  - In open world questing: flee if `level_diff > 5` or
    `hp_pct < 0.6`.
  - In contested zones (BRM, Hillsbrad, EPL): more aggressive.

### SPvP.3 — Honor accumulation outside BGs

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Track honorable kills from world PvP per the
  `HonorRequired` for PvP rank progression in `CharacterBuildConfig.PvPGoals`.

## Failure recovery

- **Death from PvP** → standard corpse run; no retaliation (different
  bot) until cool-down.

## Task specifications

> Per R19/R25, each task lists both the **current shipped surface**
> (post-S1.0 — inherits the `BotTask` async shim: `TickAsync` →
> `OnTick` → legacy `Update()` body; per-task private state machine)
> and the **target Phase 1 surface** (native `TickAsync` +
> `OnPushedAsync` + `OnPoppedAsync` + `OnChildFailedAsync` override
> against `BotTaskContext`, per [`Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10)).
> Phase 1 slot S1.0 shipped the shim; per-family async refactor lands
> under each S1.4..S1.13 family slot.
>
> The combat-rotation half of world PvP (`PvPRotationTask`) is
> documented in the Combat family file — see
> [`combat.md#pvprotationtask`](combat.md#pvprotationtask). It is
> **not** duplicated here.

### WorldPvpDetectionTask

1. **Class declaration** — `not-started`. No
   `WorldPvpDetectionTask` class exists today. The detection
   *primitive* used by the would-be task already ships at
   `Exports/BotRunner/Combat/HostilePlayerDetector.cs` (static
   `HostilePlayerDetector.Scan(IObjectManager, scanRange)` returning
   `IReadOnlyList<HostilePlayer>` with per-hostile
   `ThreatLevel { Low, Medium, High, Overwhelming }`). Unit-tested at
   `Tests/BotRunner.Tests/Combat/HostilePlayerDetectorTests.cs`. The
   missing piece is the orchestrator that polls this primitive on a
   cadence, interrupts the current activity when a threat is found,
   and pushes `PvPEngagementTask` on the stack. Target file:
   `Exports/BotRunner/Tasks/PvP/WorldPvpDetectionTask.cs`. Slot
   SPvP.1 above owns this work.
2. **Public surface** —
   - **Current shipped:** none.
   - **Target (Phase 1):**
     ```csharp
     public class WorldPvpDetectionTask : IBotTask
     {
         public string Name { get; }                    // "WorldPvpDetectionTask"
         public BotTaskStatus Status { get; }
         public Task TickAsync(BotTaskContext context, CancellationToken ct);
         public Task OnPushedAsync(BotTaskContext context, CancellationToken ct);
         public Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal);
         public Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason);
     }
     ```
     Tick contract: every N seconds (suggested 1s) invoke
     `HostilePlayerDetector.Scan(ctx.ObjectManager, scanRange)`. On a
     non-empty result, push `PvPEngagementTask` and return; otherwise
     keep running silently behind the current activity. The task is a
     **background orchestrator** — it lives at or near the bottom of
     the task stack while the bot's primary catalog activity (a
     quest, a gathering route, a travel sequence) runs above it. Pop
     conditions: bot dead, bot zoned into an instance (BG / dungeon
     / raid take over PvP responsibility there), or activity ends.
3. **Snapshot contract** — reads `IObjectManager.Player`,
   `IObjectManager.Players` (filtered to hostile-faction PvP-flagged
   units within `scanRange`, via
   `Combat/HostilePlayerDetector.cs`), `Player.Position`,
   `Player.FactionTemplate`, `Player.Level`, `Player.HealthPercent`,
   and per-hostile `Player.UnitFlags`, `Player.Health`,
   `Player.HealthPercent`, `Player.Level`, `Player.FactionTemplate`,
   `Player.Guid`, `Player.Position` (→
   `WoWActivitySnapshot.player.unit.*` for the bot itself,
   `nearbyUnits[*]` for the detected hostiles). Writes: pushes
   `PvPEngagementTask` on the BotRunner task stack (→ next snapshot's
   `currentTaskStack` gains `PvPEngagementTask`). No direct game-state
   mutation; all engagement actions are issued from the child task.
4. **BG protocol footprint** — read-only. Detection uses
   `WoWSharpObjectManager`'s already-tracked object-update stream
   (`SMSG_UPDATE_OBJECT` / `SMSG_COMPRESSED_UPDATE_OBJECT` handled in
   `Exports/WoWSharpClient/Handlers/`) — no new CMSGs are emitted by
   this task. Pushing `PvPEngagementTask` is in-process.
5. **FG memory footprint** — reads
   `Services/ForegroundBotRunner/Statics/ObjectManager.Players.cs`
   for the enumeration of nearby players, `Objects/LocalPlayer.cs`
   for self position / level / HP / faction template via offsets in
   `Services/ForegroundBotRunner/Mem/Offsets.cs`, and per-player
   `UnitFlags` via the same `Offsets.cs` `UnitFlags` offset. No Lua
   calls and no FastCall writes — the task is pure read + task-stack
   push.
6. **Test anchor** — no LiveValidation test exists today; only the
   unit-test layer at
   `Tests/BotRunner.Tests/Combat/HostilePlayerDetectorTests.cs`
   exercises the detection primitive directly.
   **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/WorldPvpDetectionTests.cs::WorldPvpDetection_PushesEngagementTask_WhenHostilePlayerEntersScanRange`
   (`not-started`). The test will stage two opposing-faction bots
   (e.g. TESTBOT1 Horde + TESTBOT2 Alliance) within `scanRange`, both
   PvP-flagged, then assert that TESTBOT1's snapshot's
   `currentTaskStack` gains `PvPEngagementTask` within a bounded
   poll window.
7. **Catalog `TaskFamily` claim** — `Combat`. World PvP is not a
   family head (per R16 the claim must be in the fixed family-head
   list in
   [`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../Spec/03_BOTRUNNER.md#catalog-of-task-families)
   and `PvP` is not one of them). The orchestrator is filed under
   `Combat` because it pushes `PvPEngagementTask` → `PvPRotationTask`
   (the latter documented in
   [`combat.md#pvprotationtask`](combat.md#pvprotationtask)).
8. **Catalog rows driven** — **none directly.** There is no PvP row
   in [`00_INDEX.md`](00_INDEX.md); world PvP is opportunistic and
   runs in parallel to whatever catalog activity the bot is
   currently executing. Indirect coverage: any row whose
   `RoleTemplate` puts the bot in a contested or PvP-flagged zone
   may transitively trigger this task — open-world quest rows
   (e.g. `quest.zone.stranglethorn-vale`,
   `quest.zone.hillsbrad-foothills`,
   `quest.zone.eastern-plaguelands`), reputation grind rows in
   contested zones, and world-boss approach rows
   (`boss.azuregos`, `boss.kazzak`, `boss.emerald-dragons`).
   Battleground rows (`bg.wsg`, `bg.ab`, `bg.av`) do **not**
   trigger this task — BG PvP detection and engagement is owned by
   the BG family (`BgObjectiveTask`).

### PvPEngagementTask

1. **Class declaration** — partial. The orchestrator class exists at
   `Exports/BotRunner/Tasks/PvP/PvPEngagementTask.cs` defining
   `BotRunner.Tasks.PvP.PvPEngagementTask : BotTask, IBotTask` with a
   private `PvPState { Evaluate, Engage, Flee, Complete }` state
   machine and hard-coded guard positions for Orgrimmar
   (`1629, -4373, 31`) and Stormwind (`-8913, 554, 94`). Status: file
   compiles, but the task is not wired into any catalog activity, has
   no LiveValidation coverage, and does not yet push
   `PvPRotationTask` from the `Engage` state (it only calls
   `ObjectManager.MoveToward(target)` — combat-rotation execution is
   the missing child push). Slot SPvP.2 above owns the wiring work.
2. **Public surface** —
   - **Current shipped:**
     ```csharp
     // Inherits BotTask async shim: TickAsync -> OnTick -> legacy Update() body.
     // Per-family async refactor lands under S1.4 / S1.8 (S1.0/R25, shim-only).
     public class PvPEngagementTask : BotTask, IBotTask
     {
         public PvPEngagementTask(IBotContext context, float scanRange = 40f);
         public void Update() { /* tick body */ }
     }
     ```
     Tick: rescans hostiles via
     `HostilePlayerDetector.Scan(ObjectManager, _scanRange)`,
     transitions `Evaluate → Engage|Flee → Complete`. Engage advances
     toward the primary target with `ObjectManager.MoveToward(...)`
     when `Distance > EngageRange (30y)`. Flee runs toward the
     nearest faction guard until `Distance ≤ FleeSuccessRange (60y)`.
     Complete pops the task via `BotContext.BotTasks.Pop()`.
   - **Target (Phase 1):** same async four-method surface as
     `WorldPvpDetectionTask` above. The `Engage` branch must push
     `PvPRotationTask` (already shipped per spec in 27 profiles) as
     a child task instead of inlining the chase. The `Flee` branch
     should push `TravelTask` / `MountAndGoToTask` instead of
     calling `MoveToward` directly so that pathfinding + transport
     handoff are reused.
3. **Snapshot contract** — reads `IObjectManager.Player` (HP,
   position, faction, level), `IObjectManager.Players` filtered via
   `HostilePlayerDetector.Scan` (hostile count, primary target's
   level / HP / threat), and target `Position` for the chase
   distance check (→ `WoWActivitySnapshot.player.unit.*` for self,
   `nearbyUnits[*]` for the targeted hostile, `movementData` for the
   chase / flee movement). Writes: `ObjectManager.MoveToward(...)`
   issues a movement command (→ next snapshot's `movementData.flags`
   gains `FORWARD`, `movementData.destination` updates) and, once
   wired, pushes `PvPRotationTask` as a child (→
   `currentTaskStack` gains `PvPRotationTask`). On `Complete` pops
   itself from the stack.
4. **BG protocol footprint** — inherits the MSG_MOVE family emitted
   by
   `Exports/WoWSharpClient/Movement/MovementController.cs` during
   `MoveToward` (chase + flee). Once wired to push
   `PvPRotationTask`, also inherits that task's superset:
   `CMSG_SET_SELECTION` (`SpellcastingManager.cs:327`),
   `CMSG_CAST_SPELL` (`SpellcastingManager.cs:229,256,279`),
   `CMSG_ATTACKSWING` (`SpellcastingManager.cs:386`),
   `CMSG_ATTACKSTOP` (`SpellcastingManager.cs:339`),
   `CMSG_USE_ITEM` for PvP trinket (`InventoryManager.cs:95`).
5. **FG memory footprint** — reads
   `Services/ForegroundBotRunner/Objects/LocalPlayer.cs` for self
   state via offsets in `Mem/Offsets.cs`,
   `Statics/ObjectManager.Players.cs` for hostile enumeration, and
   target `UnitFlags` / `Health` / `Level` via the same offsets.
   Writes via `Statics/ObjectManager.Movement.cs` for `MoveToward`
   and (once wired) the `PvPRotationTask` FG path (Lua
   `LuaCall("CastSpellByName(\"<Spell>\")")` + FastCall spell-cast
   in `Statics/ObjectManager.Combat.cs`).
6. **Test anchor** — none today.
   **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/PvpEngagementTests.cs::PvpEngagement_EngagesHostile_WhenWinnable_AndFleesToGuards_WhenOverwhelming`
   (`not-started`). Test will stage two TESTBOTs at equal level
   (engage branch) and at a 6-level differential (flee branch),
   both PvP-flagged, and assert that the engage branch produces
   `nearbyUnits[hostile].health` decreasing in successive snapshots,
   and the flee branch produces `Player.Position` converging on the
   appropriate faction guard coordinate within
   `FleeSuccessRange (60y)`.
7. **Catalog `TaskFamily` claim** — `Combat`. Same reasoning as
   `WorldPvpDetectionTask`: PvP is not a family head per R16, and
   `PvPEngagementTask` is in the same `Tasks/PvP/` directory as the
   detection task but composes `Combat`-family children
   (`PvPRotationTask`) and `Travel`-family children (`TravelTask` /
   `MountAndGoToTask` for the flee branch in the target shape).
8. **Catalog rows driven** — none directly (no PvP row in
   [`00_INDEX.md`](00_INDEX.md)). Indirect via `WorldPvpDetectionTask`
   push: same set of open-world / contested-zone rows enumerated in
   `WorldPvpDetectionTask` bullet 8.

### PvPRotationTask (cross-reference)

The combat rotation a bot executes once `PvPEngagementTask` has
acquired a hostile player target is the per-spec `PvPRotationTask`,
shipped in 27 profile files under
`BotProfiles/<Spec>/Tasks/PvPRotationTask.cs`. It is documented in
the Combat family —
[`combat.md#pvprotationtask`](combat.md#pvprotationtask) — and its
catalog `TaskFamily` claim is `Combat`. This file deliberately does
not duplicate that eight-bullet block; the cross-reference exists
so a worker reading `pvp.md` end-to-end finds the rotation half of
the world-PvP pipeline.

### Not-yet-existing tasks

The remaining tasks listed in the family table above
(`PvPFleeTask`, `PvPCallForHelpTask`) have neither a shipped class
nor a slot owner beyond the high-level goals captured in SPvP.2 /
SPvP.3. They will earn their own eight-bullet specifications once a
Phase-1 or Phase-2 slot picks them up. For now they are `not-started`
and tracked only in the family table.
