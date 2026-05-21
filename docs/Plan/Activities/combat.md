# Activities — Combat (27 Class/Spec Profiles)

27 profiles already exist in `BotProfiles/`. Each implements
`RestTask`, `PullTargetTask`, `BuffTask`, `PvERotationTask`,
`PvPRotationTask`, plus extras (`HealTask`, `SummonPetTask`,
`ConjureItemsTask`).

## Task families (existing — all done)

| Task | Notes |
|---|---|
| `PullTargetTask` | ranged pull, line-of-sight management |
| `PvERotationTask` | per spec |
| `PvPRotationTask` | per spec |
| `RestTask` | drink/eat thresholds, mana/HP |
| `BuffTask` | self-buffs, party-buffs |
| `HealTask` | healers + hybrids |
| `SummonPetTask` | Hunter, Warlock |
| `ConjureItemsTask` | Mage water/food |

## Task specifications

These specs describe the **per-profile task contract** every Combat task
must satisfy. Combat tasks live under
`BotProfiles/<ClassSpec>/Tasks/<TaskName>.cs` — there is no single class
named `PullTargetTask` etc.; instead each of the 27 profiles ships its
own implementation deriving from `BotRunner.Tasks.BotTask` (placeholders)
or `BotRunner.Tasks.CombatRotationTask` (rotations). The base classes
live in `Exports/BotRunner/Tasks/`. Per-spec rotation tuning belongs in
per-spec rotation files and is out of scope for S0.8.

Shared contract:

- All tasks implement `BotRunner.Interfaces.IBotTask`
  (`Exports/BotRunner/Interfaces/IBotTask.cs`): the Phase 1 target
  contract (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
  `OnChildFailedAsync` + `Name` + `Status`). They inherit from
  `BotRunner.Tasks.BotTask(IBotContext botContext)`
  (`Exports/BotRunner/Tasks/BotTask.cs`) or its subclass
  `BotRunner.Tasks.CombatRotationTask`
  (`Exports/BotRunner/Tasks/CombatRotationTask.cs`), and pick up the
  S1.0 async shim per R25: `TickAsync` → `OnTick` → legacy `Update()`
  body. Per-family async refactor lands under S1.5 Combat.
- Snapshot reads/writes are mediated by `IObjectManager` (the FG/BG
  swap point); tasks never touch `WoWActivitySnapshot`
  (`Exports/BotCommLayer/Models/ProtoDef/communication.proto:210`)
  directly. The snapshot fields below are the ones BotRunnerService's
  snapshot builder populates from the same `IObjectManager` reads the
  task performs, and the fields a LiveValidation test must poll to
  observe task progress.
- BG opcode footprints below are reached via `IObjectManager` calls in
  `Exports/WoWSharpClient/WoWSharpObjectManager*.cs` +
  `SpellcastingManager.cs` + `InventoryManager.cs` (the BG
  implementation of `IObjectManager`).
- FG memory/Lua footprints below are reached via the FG implementation
  of `IObjectManager` in
  `Services/ForegroundBotRunner/Statics/ObjectManager.*.cs` +
  `Services/ForegroundBotRunner/Objects/LocalPlayer.cs` (which uses
  FastCall + memory offsets in
  `Services/ForegroundBotRunner/Mem/Offsets.cs`). Lua calls are issued
  via the `LuaCall(...)` helper in the FG `ObjectManager`.

### PullTargetTask

1. **Class declaration** — per-profile class. Existing for melee/caster
   specs; ranged-pull form is canonical for Hunters:
   `HunterMarksmanship.Tasks.PullTargetTask` at
   `BotProfiles/HunterMarksmanship/Tasks/PullTargetTask.cs` (status
   `done`). Sibling profiles ship the same shape:
   `<Spec>.Tasks.PullTargetTask` at
   `BotProfiles/<Spec>/Tasks/PullTargetTask.cs` for
   `DruidBalance`, `DruidRestoration`, `HunterSurvival`,
   `HunterMarksmanship`, `MageArcane`, `MageFire`, `MageFrost`,
   `PaladinHoly`, `PaladinProtection`, `PaladinRetribution`,
   `PriestDiscipline`, `PriestHoly`, `PriestShadow`, `RogueCombat`,
   `RogueSubtlety`, `ShamanRestoration`, `WarlockAffliction`,
   `WarlockDemonology`, `WarlockDestruction`. **Planned anchors**
   (`not-started`) — siblings still missing: `RogueAssassin`,
   `ShamanElemental`, `ShamanEnhancement`, `WarriorArms`,
   `WarriorFury`, `WarriorProtection`, `DruidFeral`,
   `HunterBeastMastery` (HunterBM pulls via existing
   `Tasks/PullTargetTask.cs`-shaped file pending).
2. **Public surface — current shipped** —
   ```csharp
   public class PullTargetTask(IBotContext botContext) : BotTask(botContext), IBotTask
   {
       // Inherits BotTask async shim: TickAsync -> OnTick -> legacy Update() body.
       // Per-family async refactor lands under S1.5 Combat (S1.0/R25, shim-only).
       public void Update() { /* class-specific body */ }
   }
   ```
   Tick contract: pop self if no target / target dead / target tapped
   by other / bot in combat; otherwise navigate to pull range and emit
   the pull spell (e.g. `HuntersMark` then `StartRangedAttack`), then
   pop and push `PvERotationTask`.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
3. **Snapshot contract** — reads
   `IObjectManager.Player` (→ `WoWActivitySnapshot.player.unit`
   `.health/.maxHealth/.movementData`), `GetTarget(Player)`
   (→ `WoWActivitySnapshot.player.unit.targetGuid`),
   `Aggressors` / `Player.IsInCombat` (→ recent unit aura/combat
   bits surfaced through `nearbyUnits`). Writes by side-effect:
   triggers `MoveToward` (→ `movementData` deltas), `CastSpell` /
   `StartRangedAttack` (→ next snapshot's `player.unit.targetGuid`,
   `nearbyUnits[*].auras` for debuffs like Hunter's Mark).
4. **BG protocol footprint** — `CMSG_SET_SELECTION`
   (`Exports/WoWSharpClient/SpellcastingManager.cs:327`),
   `CMSG_CAST_SPELL` (`SpellcastingManager.cs:231,256,279`) for the
   pull spell, `CMSG_ATTACKSWING` (auto-shoot enable;
   `SpellcastingManager.cs:386`), and the MSG_MOVE family emitted by
   `MovementController` (`Exports/WoWSharpClient/Movement/MovementController.cs`)
   during `NavigateToward`. Hunter pet pull adds `CMSG_PET_ACTION`
   (`Exports/WoWSharpClient/WoWSharpObjectManager.cs:743`,
   `Networking/ClientComponents/CombatSpellNetworkClientComponent.cs:357`
   `PetAttackAsync`).
5. **FG memory footprint** — reads `LocalPlayer` (HP/mana/combat
   flags via memory offsets in
   `Services/ForegroundBotRunner/Mem/Offsets.cs`),
   `ObjectManager.GetTarget` / `ObjectManager.Aggressors`
   (`Statics/ObjectManager.Combat.cs`), and writes via FastCall
   spell-cast + Lua `LuaCall("CastSpellByName(\"Hunter's Mark\")")`
   exposed by `Services/ForegroundBotRunner/Objects/LocalPlayer.cs`.
   Pet attack issued via the same `ObjectManager.Combat.cs` pet
   helper.
6. **Test anchor** — closest existing harness is the arena pull-style
   integration in `BotRunner.Tests.LiveValidation.CombatLoopTests`
   at `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs` (it
   exercises target acquisition + `StartMeleeAttack` chase, the BG
   counterpart of the pull path).
   Command:
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~CombatLoopTests"`.
   **Planned anchor test** for the dedicated ranged-pull scenario:
   `Tests/BotRunner.Tests/LiveValidation/PullTargetTests.cs::Pull_HuntersMarkLandsBeforeMelee`
   (`not-started`).
7. **Catalog `TaskFamily` claim** — `Combat`. Cross-references
   `docs/Plan/Activities/00_INDEX.md`: every row whose activity drives
   hostile engagement consumes this task — all 6 starter quest rows,
   all 29 zone quest rows, all 21 dungeon rows, all 7 raid rows, all
   3 BG rows, all 5 reputation grinds, all 5 attunement chains, both
   world events with kill objectives, and all 3 world-boss rows
   (88 rows total minus the pure-economy/social rows
   `econ.ah-restock` / `econ.vendor-loop` / `prof.city-trainer-loop`).

### PvERotationTask

1. **Class declaration** — per-profile class deriving from
   `BotRunner.Tasks.CombatRotationTask`. 27/27 profiles ship a file
   `BotProfiles/<Spec>/Tasks/PvERotationTask.cs` defining a
   `<Spec>.Tasks.PvERotationTask` class. Representative implementation:
   `WarriorFury.Tasks.PvERotationTask` at
   `BotProfiles/WarriorFury/Tasks/PvERotationTask.cs`. Status: 27
   profile files exist and compile; per-spec rotation content quality
   varies (some are stubs — e.g. `PriestHoly.Tasks.PvERotationTask`
   pops immediately on `Update`). Stub remediation is per-spec work,
   tracked under the rotation files, not this slot.
2. **Public surface — current shipped** —
   ```csharp
   public class PvERotationTask : CombatRotationTask, IBotTask
   {
       internal PvERotationTask(IBotContext botContext);
       public void Update();                       // BotTask async shim invokes this
       public override void PerformCombatRotation(); // CombatRotationTask
   }
   ```
   `Update` calls `EnsureTarget()` (defined on `CombatRotationTask`),
   then `Update(GetMeleeRange(target))` to chase/face, then
   `ExecuteRotation()` for the per-spec ability sequence using
   `TryUseAbility`, `TryCastSpell`, `TryUseAbilityById`,
   `TryCastHeal`, and `StartKite` helpers.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
3. **Snapshot contract** — reads
   `Player.HealthPercent/ManaPercent/Energy/Rage/Level/CurrentStance`
   (→ `WoWActivitySnapshot.player.unit.health/.mana/.power/.level`),
   `Player.HasBuff(...)/HasDebuff(...)` (→ `player.unit.auras`),
   `Player.IsInCombat/IsCasting/IsChanneling/IsAutoAttacking`
   (→ unit flag bits in `player.unit`), `Aggressors` /
   `GetTarget(Player)` / `PartyMembers` / `PartyLeader` /
   `SkullTargetGuid` / `CrossTargetGuid`
   (→ `nearbyUnits`, `partyLeaderGuid`, raid-marker fields).
   Writes: triggers `CastSpell`, `SetTarget`, `StartMeleeAttack`,
   `StopAttack`, `StopAllMovement`, `MoveToward`, `SetFacing`,
   `UseItem` (potions via `CombatRotationTask.TryUseHealthPotion` /
   `TryUseManaPotion`). Resulting snapshot deltas: `currentAction`
   (ObjectiveType.CastSpell etc.), `player.unit.targetGuid`,
   `movementData`, `player.unit.auras`.
4. **BG protocol footprint** — `CMSG_CAST_SPELL`
   (`SpellcastingManager.cs:229,256,279`), `CMSG_SET_SELECTION`
   (`SpellcastingManager.cs:327`), `CMSG_ATTACKSWING`
   (`SpellcastingManager.cs:386`), `CMSG_ATTACKSTOP`
   (`SpellcastingManager.cs:339`), `CMSG_CANCEL_CAST`
   (`SpellcastingManager.cs:187`), `CMSG_CANCEL_AURA`
   (`SpellcastingManager.cs:296`), `CMSG_USE_ITEM`
   (`InventoryManager.cs:95`, for potions), `CMSG_PET_ACTION`
   (`WoWSharpObjectManager.cs:743`,
   `CombatSpellNetworkClientComponent.cs:357,395,422,449`) when the
   spec has a pet, plus MSG_MOVE traffic from movement
   chase/kite/face.
5. **FG memory footprint** — `Statics/ObjectManager.Combat.cs`
   (`Aggressors`, `GetTarget`, `StartMeleeAttack`, `StopAttack`,
   `SetTarget`, `Face`, `StartRangedAttack`), `Objects/LocalPlayer.cs`
   reads HP/mana/power/auras via offsets in
   `Mem/Offsets.cs`, `Statics/ObjectManager.Inventory.cs` for
   `UseItem`. Lua calls: `LuaCall("CastSpellByName(\"<Spell>\"[,1])")`
   (cast-on-self uses arg `1`) and `LuaCall("UseAction(...)")` for
   action-bar abilities; all funnelled through
   `LocalPlayer.LuaCall(...)`.
6. **Test anchor** — `BotRunner.Tests.LiveValidation.CombatLoopTests`
   at `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`
   (`Combat_AutoAttackLevel1Boar_KillsMobFromRealMeleeCombat`),
   `CombatBgTests`
   (`Combat_BG_AutoAttacksSharedBoar_FromFreshBackgroundRoster`),
   `CombatFgTests`
   (`Combat_FG_AutoAttacksSharedBoar_FromFreshArenaRoster`).
   Command:
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~CombatBgTests|FullyQualifiedName~CombatFgTests"`.
7. **Catalog `TaskFamily` claim** — `Combat`. Every catalog row in
   `docs/Plan/Activities/00_INDEX.md` whose activity drives sustained
   hostile combat depends on this task: 6 starter quest rows + 29
   zone quest rows + 21 dungeons + 7 raids + 3 BGs + 5 reputation
   grinds + 5 attunements + 1 world event (`event.stv-fishing-extravaganza`
   incidentally) + 3 world bosses, plus dungeon-route + raid-encounter
   rotations dispatched from `DungeoneeringTask` / `RaidEncounterTask`.

### PvPRotationTask

1. **Class declaration** — 27/27 profiles ship
   `BotProfiles/<Spec>/Tasks/PvPRotationTask.cs` defining
   `<Spec>.Tasks.PvPRotationTask`. Most current files delegate to
   the spec's `PvERotationTask`. Representative:
   `MageFire.Tasks.PvPRotationTask` at
   `BotProfiles/MageFire/Tasks/PvPRotationTask.cs`. Status: 27 files
   exist; per-spec PvP differentiation is still mostly placeholder
   delegation and is per-spec work, not in scope for S0.8.
2. **Public surface — current shipped** —
   ```csharp
   public class PvPRotationTask : CombatRotationTask, IBotTask
   {
       public PvPRotationTask(IBotContext botContext);
       public void Update();                                // BotTask async shim invokes this
       public override void PerformCombatRotation();
   }
   ```
   Current implementations typically hold a private
   `PvERotationTask pveRotation` and forward both methods to it.
   Future per-spec versions must add PvP-specific gating (trinket,
   PvP cooldowns, target prioritisation against players).

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
3. **Snapshot contract** — same superset as `PvERotationTask` (the
   delegate target) plus reads of `IObjectManager.GetHostilePlayers`
   / `Combat/HostilePlayerDetector.cs` and writes to PvP-spec aura
   sets. Snapshot fields are identical to `PvERotationTask`.
4. **BG protocol footprint** — same opcode superset as
   `PvERotationTask`. Future per-spec work adds trinket via
   `CMSG_USE_ITEM` for PvP trinket and any class-specific PvP
   abilities (already covered by `CMSG_CAST_SPELL`).
5. **FG memory footprint** — same as `PvERotationTask`. Future
   per-spec work adds reads from
   `Exports/BotRunner/Combat/HostilePlayerDetector.cs` to gate
   PvP-only abilities.
6. **Test anchor** — no dedicated PvP-rotation LiveValidation test
   exists today. Closest existing fixture is the BG-objective suite
   in `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/`, which
   exercises PvP rotations transitively.
   **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/PvpRotationTests.cs::PvpRotation_DealsDamageToHostilePlayer_InWsg`
   (`not-started`).
7. **Catalog `TaskFamily` claim** — `Combat`. Drives all 3 BG rows
   (`bg.wsg`, `bg.ab`, `bg.av`) and any world-PvP engagement that
   the `pvp` family hands off (see
   `docs/Plan/Activities/pvp.md`).

### RestTask

1. **Class declaration** — 27/27 profiles ship
   `BotProfiles/<Spec>/Tasks/RestTask.cs` defining
   `<Spec>.Tasks.RestTask`. Representative:
   `WarriorFury.Tasks.RestTask` at
   `BotProfiles/WarriorFury/Tasks/RestTask.cs`. Status: 27 files
   exist and compile; thresholds and consumable selection are tuned
   per-spec but the lifecycle is uniform.
2. **Public surface — current shipped** —
   ```csharp
   public class RestTask(IBotContext botContext) : BotTask(botContext), IBotTask
   {
       // Inherits BotTask async shim: TickAsync -> OnTick -> legacy Update() body.
       // Per-family async refactor lands under S1.5 Combat (S1.0/R25, shim-only).
       public void Update() { /* class-specific body */ }
   }
   ```
   Tick contract: pop when HP ≥ 95 (or ≥ 80 if not currently
   eating), or any aggressor present. Otherwise emit a bandage
   if HP < 60 and `Recently Bandaged` debuff absent, else eat best
   food from inventory. Mana-capable specs add a drink branch.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
3. **Snapshot contract** — reads `Player.HealthPercent /
   ManaPercent / IsEating / IsChanneling / IsInCombat`
   (→ `WoWActivitySnapshot.player.unit.health/.mana/.auras`),
   `Player.HasDebuff("Recently Bandaged")` (→ `player.unit.auras`),
   `IObjectManager.Units.Any(u => u.TargetGuid == Player.Guid)`
   (→ `nearbyUnits[*].targetGuid`),
   `ObjectManager.GetContainedItems()` via
   `BotRunner.Combat.ConsumableData.FindBestFood/Bandage`
   (→ `player.inventory.bagItems`). Writes: triggers
   `DoEmote(EMOTE_STATE_STAND)`, `StopAllMovement()`,
   `IWoWItem.Use()` (→ next snapshot's `player.unit.auras`
   gains Food/Drink/Bandage aura, `player.inventory.bagItems`
   stack count decrements).
4. **BG protocol footprint** — `CMSG_USE_ITEM`
   (`InventoryManager.cs:95`) for food / drink / bandage application,
   `CMSG_EMOTE` (`WoWSharpObjectManager.cs:1426`) for the stand
   emote on pop, `CMSG_ATTACKSTOP` and MSG_MOVE
   stop-packet for `StopAllMovement()`.
5. **FG memory footprint** — reads `Player.HealthPercent`,
   `Player.IsEating`, `Player.HasDebuff` via
   `Services/ForegroundBotRunner/Objects/LocalPlayer.cs` (memory
   offsets), `Statics/ObjectManager.Inventory.cs` for
   `GetContainedItems`. Writes via Lua
   `LuaCall("UseContainerItem(<bag>, <slot>)")` or the equivalent
   `UseItem` FastCall path in `Statics/ObjectManager.Inventory.cs`.
6. **Test anchor** — no dedicated `RestTask` LiveValidation test
   exists today.
   **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/RestTaskTests.cs::Rest_BotEatsFoodToFullHp_ThenPopsTask`
   (`not-started`). Adjacent coverage:
   `BotRunner.Tests.LiveValidation.BuffAndConsumableTests` at
   `Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs`
   already exercises the same `CMSG_USE_ITEM` + aura assertion
   pattern. Command:
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~BuffAndConsumableTests"`.
7. **Catalog `TaskFamily` claim** — `Combat`. Pushed by virtually
   every combat-bearing row whenever HP/mana falls below threshold —
   starter quests, zone quests, dungeons, raids, BGs, reputation
   grinds, attunement chains, world bosses (88 rows minus pure
   economy / city-trainer-loop / vendor-loop rows).

### HealTask

1. **Class declaration** — partial coverage. Files present:
   `BotProfiles/PaladinHoly/Tasks/HealTask.cs` (planned anchor — see
   note below), `BotProfiles/PaladinProtection/Tasks/HealTask.cs`,
   `BotProfiles/PaladinRetribution/Tasks/HealTask.cs`,
   `BotProfiles/PriestShadow/Tasks/HealTask.cs`,
   `BotProfiles/DruidBalance/Tasks/HealTask.cs`,
   `BotProfiles/DruidFeral/Tasks/HealTask.cs`,
   `BotProfiles/HunterBeastMastery/Tasks/HealTask.cs`,
   `BotProfiles/ShamanElemental/Tasks/HealTask.cs`,
   `BotProfiles/ShamanEnhancement/Tasks/HealTask.cs`.
   Healer specs that should ship `HealTask` but do not yet:
   **Planned anchors** (`not-started`) —
   `BotProfiles/PaladinHoly/Tasks/HealTask.cs` (verify presence;
   `Read` returned not-found for the expected path),
   `BotProfiles/PriestHoly/Tasks/HealTask.cs`,
   `BotProfiles/PriestDiscipline/Tasks/HealTask.cs`,
   `BotProfiles/DruidRestoration/Tasks/HealTask.cs`,
   `BotProfiles/ShamanRestoration/Tasks/HealTask.cs`.
   Representative implementation:
   `PaladinProtection.Tasks.HealTask` at
   `BotProfiles/PaladinProtection/Tasks/HealTask.cs` (self-heal at
   HP < 70% with Holy Light + Divine Protection).
2. **Public surface — current shipped** —
   ```csharp
   public class HealTask(IBotContext botContext) : BotTask(botContext), IBotTask
   {
       // Inherits BotTask async shim: TickAsync -> OnTick -> legacy Update() body.
       // Per-family async refactor lands under S1.5 Combat (S1.0/R25, shim-only).
       public void Update() { /* class-specific body */ }
   }
   ```
   Tick contract: pop if HP > threshold (typ. 70) or mana < heal
   cost; otherwise cast Divine Protection / Power Word: Shield (if
   off cooldown and not on cooldown debuff) then the main heal
   (Holy Light, Heal, Greater Heal, Healing Wave). Group-aware
   variants (planned for dedicated healer specs) iterate
   `IObjectManager.PartyMembers` and pick lowest-HP member via
   `CombatRotationTask.GetHealTarget`.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
3. **Snapshot contract** — reads `Player.HealthPercent / Mana / IsCasting`
   (→ `player.unit.health/.mana`), `Player.HasBuff/HasDebuff` for
   `WeakenedSoul` / `PowerWordShield` etc.
   (→ `player.unit.auras`), `IObjectManager.PartyMembers` /
   `PartyLeader` (→ `nearbyUnits` filtered by `partyLeaderGuid`),
   `ObjectManager.GetManaCost(spellName)`,
   `ObjectManager.IsSpellReady(spellName)`. Writes via
   `CastSpell(spellName, castOnSelfArg)` or `SetTarget` + `CastSpell`
   for party heal (→ `currentAction = CastSpell`,
   `player.unit.targetGuid` blips, target's
   `nearbyUnits[*].auras` gains heal HoT, target's `health` rises).
4. **BG protocol footprint** — `CMSG_CAST_SPELL`
   (`SpellcastingManager.cs:231,256,279`), `CMSG_SET_SELECTION`
   (`SpellcastingManager.cs:327`) when re-targeting party member,
   `CMSG_CANCEL_CAST` (`SpellcastingManager.cs:187`) if interrupted
   mid-heal.
5. **FG memory footprint** — reads `LocalPlayer.HealthPercent` /
   `.Mana` / `.IsCasting` via memory offsets in
   `Services/ForegroundBotRunner/Mem/Offsets.cs`, party reads via
   `Statics/ObjectManager.cs` party APIs. Writes via Lua
   `LuaCall("CastSpellByName(\"Holy Light\", 1)")` (self) or
   `LuaCall("TargetUnit(\"<name>\")")` + `LuaCall("CastSpellByName(\"<spell>\")")`
   for party heal, both routed through
   `Services/ForegroundBotRunner/Objects/LocalPlayer.cs`.
6. **Test anchor** — no dedicated LiveValidation `HealTask` test
   exists today. Closest adjacent coverage:
   `Tests/BotRunner.Tests/LiveValidation/SpiritHealerTests.cs`
   (spirit-healer interaction, not in-combat healing).
   **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/HealTaskTests.cs::Heal_PaladinSelfHealsHolyLightToFullHp`
   and
   `Tests/BotRunner.Tests/LiveValidation/HealTaskTests.cs::Heal_PriestHealsPartyTankLowestHpFirst`
   (both `not-started`).
7. **Catalog `TaskFamily` claim** — `Combat`. Pushed by every
   grouped combat-bearing row that includes a healer in the
   `RoleTemplate`: all 21 dungeons, all 7 raids, all 3 BGs (under
   PvP triage), and any zone-quest row done by a hybrid healer
   spec. Self-heal usage applies to all combat rows for hybrid
   specs (Paladin, Druid, Shaman, Priest, Warlock).

### BuffTask

1. **Class declaration** — 27/27 profiles ship
   `BotProfiles/<Spec>/Tasks/BuffTask.cs` defining
   `<Spec>.Tasks.BuffTask`. Representative:
   `WarriorFury.Tasks.BuffTask` at
   `BotProfiles/WarriorFury/Tasks/BuffTask.cs`. Current state: most
   files immediately pop (stub); functional buff application happens
   in `PvERotationTask` (`BattleShout`, `InnerFire`, `BlessingOf*`,
   etc.). Promoting to dedicated pre-pull and party-buff cycles is
   per-spec work tracked under the bot-profile skill, not S0.8.
2. **Public surface — current shipped** —
   ```csharp
   public class BuffTask(IBotContext botContext) : BotTask(botContext), IBotTask
   {
       // Inherits BotTask async shim: TickAsync -> OnTick -> legacy Update() body.
       // Per-family async refactor lands under S1.5 Combat (S1.0/R25, shim-only).
       public void Update() { /* class-specific body */ }
   }
   ```
   Tick contract (target shape): iterate the spec's buff list; for
   each missing buff that `IsSpellReady` and within mana budget,
   `CastSpell(name, castOnSelf: <true if self>)` once per tick;
   pop when all buffs present.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
3. **Snapshot contract** — reads `Player.HasBuff(...)` /
   `Player.Mana` / `Player.IsCasting`
   (→ `player.unit.auras/.mana`),
   `IObjectManager.PartyMembers[*].HasBuff(...)`
   (→ `nearbyUnits[*].auras` filtered by party-leader GUID),
   `IObjectManager.IsSpellReady` and `GetManaCost`. Writes:
   `CastSpell` triggers `currentAction`, `player.unit.auras` gains
   buff aura; party-buff variants also flip target GUID briefly
   (→ snapshot blip in `player.unit.targetGuid`).
4. **BG protocol footprint** — `CMSG_CAST_SPELL`
   (`SpellcastingManager.cs:231,256,279`), `CMSG_SET_SELECTION`
   (`SpellcastingManager.cs:327`) for party-buff target shuffles,
   `CMSG_CANCEL_AURA` (`SpellcastingManager.cs:296`) when a buff
   refresh races a stale aura.
5. **FG memory footprint** — reads aura state via
   `Services/ForegroundBotRunner/Objects/LocalPlayer.cs` aura table
   (offsets in `Mem/Offsets.cs`), party reads via
   `Statics/ObjectManager.cs`. Writes via Lua
   `LuaCall("CastSpellByName(\"<Buff>\", 1)")` for self-cast or
   `LuaCall("TargetUnit(\"<name>\")")` + `LuaCall("CastSpellByName(\"<Buff>\")")`
   for party targets.
6. **Test anchor** — no dedicated `BuffTask` LiveValidation test;
   `Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs`
   covers item-applied buffs (Elixir of Lion's Strength) using the
   same `CMSG_USE_ITEM` + aura assertion pattern.
   Command:
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~BuffAndConsumableTests"`.
   **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/BuffTaskTests.cs::Buff_AppliesAllSelfBuffs_BeforeFirstPull`
   (`not-started`).
7. **Catalog `TaskFamily` claim** — `Combat`. Pushed by every
   combat-bearing row at session start (pre-pull) and after rez
   in dungeons/raids/BGs/world-boss attempts (88 rows minus the
   pure economy / city-trainer-loop / vendor-loop / AH-restock rows).

### SummonPetTask

1. **Class declaration** — files present for the seven pet specs:
   `HunterBeastMastery.Tasks.SummonPetTask` at
   `BotProfiles/HunterBeastMastery/Tasks/SummonPetTask.cs`,
   `HunterMarksmanship.Tasks.SummonPetTask` at
   `BotProfiles/HunterMarksmanship/Tasks/SummonPetTask.cs`,
   `HunterSurvival.Tasks.SummonPetTask` at
   `BotProfiles/HunterSurvival/Tasks/SummonPetTask.cs`,
   `WarlockAffliction.Tasks.SummonPetTask` at
   `BotProfiles/WarlockAffliction/Tasks/SummonPetTask.cs`,
   `WarlockDemonology.Tasks.SummonPetTask` at
   `BotProfiles/WarlockDemonology/Tasks/SummonPetTask.cs`,
   `WarlockDestruction.Tasks.SummonPetTask` at
   `BotProfiles/WarlockDestruction/Tasks/SummonPetTask.cs`.
   Representative implementation: `WarlockDestruction.Tasks.SummonPetTask`
   (full pet-selection logic — Voidwalker if tanking needed,
   Succubus, Felhunter, Imp fallback). Hunter specs ship stubs
   that pop immediately; Hunter pet flow is driven instead by
   `Tasks/Pet/PetManagementTask.cs` (existing) and `CallPet`
   spell cast.
2. **Public surface — current shipped** —
   ```csharp
   public class SummonPetTask(IBotContext botContext) : BotTask(botContext), IBotTask
   {
       // Inherits BotTask async shim: TickAsync -> OnTick -> legacy Update() body.
       // Per-family async refactor lands under S1.5 Combat (S1.0/R25, shim-only).
       public void Update() { /* class-specific body */ }
   }
   ```
   Tick contract: pop if pet already alive (`ObjectManager.Pet != null`)
   or no summon spell ready; otherwise pick the appropriate summon
   (Warlock chooses by tank-need; Hunter uses `CallPet`), cast it,
   push `BuffTask` next on pop.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
3. **Snapshot contract** — reads `IObjectManager.Pet`,
   `IObjectManager.Player.IsCasting`,
   `IObjectManager.Aggressors.Count`,
   `IObjectManager.Player.HealthPercent`,
   `IObjectManager.IsSpellReady(Summon*)`. Writes by side-effect:
   `CastSpell(<summon spell>)` →
   `WoWActivitySnapshot.player.unit.auras` (channeling bit while
   summon casts), `currentAction.objectiveType = CastSpell`,
   followup snapshot's `nearbyUnits` gains the pet
   (Warlock pet GUID owned by player; Hunter pet from stable),
   `player.pet_guid` (where modelled).
4. **BG protocol footprint** — `CMSG_CAST_SPELL`
   (`SpellcastingManager.cs:231,256,279`) for `SummonImp` /
   `SummonVoidwalker` / `SummonSuccubus` / `SummonFelhunter` /
   `SummonFelguard` / `FelDomination` / `CallPet`.
   `CMSG_EMOTE` (`WoWSharpObjectManager.cs:1426`) for the
   stand-up emote prior to summon. Pet command opcodes
   (`CMSG_PET_ACTION` —
   `Networking/ClientComponents/CombatSpellNetworkClientComponent.cs:357,395,422,449`)
   fire on follow-up ticks once the pet is alive (stance set, follow,
   attack).
5. **FG memory footprint** — reads pet presence via
   `Services/ForegroundBotRunner/Statics/ObjectManager.cs` /
   `Objects/LocalPlayer.cs` `Pet` property (offsets in
   `Mem/Offsets.cs`). Writes via Lua
   `LuaCall("CastSpellByName(\"Summon Imp\")")` /
   `LuaCall("CallPet()")` /
   `LuaCall("PetFollow()")` /
   `LuaCall("PetAttack()")` /
   `LuaCall("CastPetAction(<slot>)")`.
6. **Test anchor** — `BotRunner.Tests.LiveValidation.PetManagementTests`
   at `Tests/BotRunner.Tests/LiveValidation/PetManagementTests.cs`
   (`Pet_SummonAndManage_StanceFeedAbility`), which dispatches
   `ObjectiveType.CastSpell` with `CallPetSpellId = 883` then
   `DismissPetSpellId = 2641`.
   Command:
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~PetManagementTests"`.
   **Planned anchor test** for Warlock summon-by-tank-need branch:
   `Tests/BotRunner.Tests/LiveValidation/SummonPetTaskTests.cs::SummonPet_WarlockChoosesVoidwalker_WhenTankPetNeeded`
   (`not-started`).
7. **Catalog `TaskFamily` claim** — `Combat`. Pushed at session
   start for any Hunter/Warlock-spec bot on every combat-bearing
   row in `docs/Plan/Activities/00_INDEX.md` (no per-row gating —
   pet specs always summon before combat).

### ConjureItemsTask

1. **Class declaration** — 3/3 Mage specs ship the task:
   `MageArcane.Tasks.ConjureItemsTask` at
   `BotProfiles/MageArcane/Tasks/ConjureItemsTask.cs`,
   `MageFire.Tasks.ConjureItemsTask` at
   `BotProfiles/MageFire/Tasks/ConjureItemsTask.cs`,
   `MageFrost.Tasks.ConjureItemsTask` at
   `BotProfiles/MageFrost/Tasks/ConjureItemsTask.cs`. Status:
   files exist and compile; `foodItem` / `drinkItem` fields are
   currently unassigned (`CS0649` suppressed) — wiring conjured
   items back into the inventory tracker is a small follow-up tracked
   in the per-spec rotation files, not in S0.8.
2. **Public surface — current shipped** —
   ```csharp
   public class ConjureItemsTask(IBotContext botContext) : BotTask(botContext), IBotTask
   {
       // Inherits BotTask async shim: TickAsync -> OnTick -> legacy Update() body.
       // Per-family async refactor lands under S1.5 Combat (S1.0/R25, shim-only).
       public void Update() { /* class-specific body */ }
   }
   ```
   Tick contract: pop if `Player.IsCasting`; pop and push `RestTask`
   if mana < 20%; pop (optionally push `RestTask` if mana ≤ 80) when
   no free bag slots remain or both `ConjureFood` and `ConjureWater`
   are on cooldown / already stocked. Otherwise cast `ConjureFood`
   (when count ≤ 2) and `ConjureWater` (when count ≤ 2) with a
   3 s `Wait` between casts.

   **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
3. **Snapshot contract** — reads `Player.IsCasting / ManaPercent`
   (→ `player.unit.mana/.auras`),
   `IObjectManager.CountFreeSlots(false)`
   (→ `player.inventory.freeSlots`),
   `IObjectManager.IsSpellReady(ConjureFood/ConjureWater)`,
   `IObjectManager.GetItemCount(itemId)`
   (→ `player.inventory.bagItems[*].stackCount`). Writes:
   `CastSpell` produces `player.unit.auras` channeling aura then,
   on success, `player.inventory.bagItems` gains a conjured food /
   drink stack, `currentAction = CastSpell`.
4. **BG protocol footprint** — `CMSG_CAST_SPELL`
   (`SpellcastingManager.cs:231,256,279`) for `ConjureFood` /
   `ConjureWater`. No item-use opcode; the conjured item is
   server-pushed into a bag slot via `SMSG_ITEM_PUSH_RESULT`
   (handler in `Exports/WoWSharpClient/Handlers/`).
5. **FG memory footprint** — reads casting state and inventory via
   `Objects/LocalPlayer.cs` / `Statics/ObjectManager.Inventory.cs`;
   writes via Lua `LuaCall("CastSpellByName(\"Conjure Food\")")` and
   `LuaCall("CastSpellByName(\"Conjure Water\")")`.
6. **Test anchor** — no dedicated LiveValidation test exists today.
   **Planned anchor test:**
   `Tests/BotRunner.Tests/LiveValidation/ConjureItemsTaskTests.cs::ConjureItems_MageStocksFoodAndWater_BelowThreshold`
   (`not-started`). Adjacent: `BuffAndConsumableTests` (above) is
   the closest test of consumable-flow snapshot assertions.
7. **Catalog `TaskFamily` claim** — `Combat`. Pushed by Mage-spec
   bots before any `RestTask` / `BuffTask` on any combat-bearing
   row in `docs/Plan/Activities/00_INDEX.md`. Mage-specific —
   non-Mage rows do not consume this task.

## Slots

### SCo.1 — Profile parity audit

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Run all 27 profiles through LiveValidation against level-
  appropriate mobs. Surface FG/BG behavior differences via
  `Tests/BotRunner.Tests/LiveValidation/Combat*Tests.cs`.

### SCo.2 — Class-utility table

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Per-activity-family class-utility scores (Priest +
  for Strat UD; Hunter + for LBRS) feed `BotSelector` from
  [`Spec/04_ACTIVITIES.md#bot-selection-scoring`](../Spec/04_ACTIVITIES.md#bot-selection-scoring).

### SCo.3 — Threat tracking parity

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** BG threat model matches FG within snapshot-tolerance for
  group encounters.

### SCo.4 — Decision Engine integration

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** `DecisionEngineService` advice (which cooldown to pop,
  threat-based target shift) consumed by `PvERotationTask` as
  *advisory only* (existing principle).

## Skill

- `bot-profile` skill already exists at
  `.claude/skills/bot-profile/SKILL.md`. Keep current; revise if a
  new pattern requires it.
