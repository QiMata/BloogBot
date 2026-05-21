# 02 — WoW game loops decomposed into A/O/T/A

> Prerequisite: [`01_LAYERS.md`](01_LAYERS.md) for the layer definitions
> and recursive composition rules.
>
> **Goal of this file.** Take the 9 major game loops the bot has to
> drive in WoW 1.12.1 and decompose each into the four AOTA layers.
> Each loop section names: the catalog Activity rows it covers, the
> typical Objective sequence, the Tasks the Objectives unfold into,
> and the wire Actions emitted. The same shape carries over to other
> games — see [`07_PORTABILITY.md`](07_PORTABILITY.md).

## Inventory — the 9 loops

| Loop | Catalog families | Catalog rows |
|---|---|---|
| 1. Questing (Starter 1-10) | `StarterQuesting` | 6 |
| 2. Questing (Zone 10-60) | `ZoneQuesting` | 26 |
| 3. Dungeons | `Dungeon` | 21 |
| 4. Raids | `Raid` | 7 |
| 5. Battlegrounds | `Battleground` | 3 |
| 6. Professions — gathering | `ProfessionGathering` | 3 (Mining, Herb, Skinning routes) |
| 7. Professions — crafting + leveling | `ProfessionCrafting` + `ProfessionLeveling` | 1 (city-trainer loop) |
| 8. Economy | `Economy` | 2 (AH restock, vendor/repair/bank/mail) |
| 9. Reputation / Attunement / World event / World boss | `Reputation` + `Attunement` + `WorldEvent` + `WorldBoss` | 5 + 5 + 1 + 3 |

Plus two **always-on housekeeping loops** that are not Activities but
are first-class Tasks the DecisionEngine pushes when needed:

- **Recovery** (corpse run, spirit healer, stuck recovery, reconnect)
- **Loadout** (level / spells / talents / gear / professions / rep — runs at OnDemand Outfitting stage and on autonomous progression milestones)

Anchor: [`Plan/Activities/00_INDEX.md`](../../Plan/Activities/00_INDEX.md)
for the live catalog board.

---

## Loop 1+2 — Questing

### Activity

`quest.starter.<zone>` (6 rows) or `quest.zone.<zone>` (26 rows). The
Activity row carries a `Location` (zone name), `LevelRange`,
`FactionPolicy`, and `TaskFamily = "Questing"`.

### Objective sequence (per zone)

The Objective sequence is **synthesized at runtime** by walking the
quest-chain DAG (see [`04_QUEST_CHAINS.md`](04_QUEST_CHAINS.md)) — it
is not hand-authored. Conceptually each zone's Objective list is:

```
1. travel-to-hub               (Travel)
2. accept-batch-at-hub         (Interact — one Interact per available quest)
3. for each accepted quest:
     3a. kill / collect / escort / use-gameobject objectives
4. turn-in-batch-at-hub        (Interact + Reward)
5. travel-to-next-hub          (Travel)
6. … repeat until level cap of zone or chain exhaustion
7. travel-to-next-zone         (Travel)        (transition signal)
```

Per-quest sub-Objectives map 1:1 to `quest_template`'s objective
columns:

| Quest column | Objective type | Task family |
|---|---|---|
| `ReqCreatureOrGOId1..4 > 0` (creature) | `Kill` | `KillObjectiveTask` |
| `ReqCreatureOrGOId1..4 < 0` (gameobject) | `Interact` | `UseGameObjectTask` |
| `ReqItemId1..4 > 0` | `Collect` | `CollectObjectiveTask` |
| `SpecialFlags & QUEST_SPECIAL_FLAG_REPEATABLE` | `Repeatable` | (same as base type; the chain doesn't auto-advance) |
| `SpecialFlags & QUEST_SPECIAL_FLAG_ESCORT` (server-tagged) | `Escort` | `EscortObjectiveTask` |
| `SrcSpell > 0` | `CastSpell` | currently embedded in `KillObjectiveTask` |
| `RewSpell > 0` at turn-in | (no objective — reward fires on turn-in) | (handled by `TurnInQuestTask`) |

### Task fan-out

The most common Task tree per Objective:

```
KillObjectiveTask(quest=N, creature=K, count=10)
  ├─ GoToTask(hotspot[0])
  │    └─ pushes path-leg actions
  ├─ PullStrategyTask(unit)
  │    ├─ SetRaidTarget(skull)        ← Action via IObjectManager
  │    ├─ CastSpellTask("Shoot")      ← child task
  │    └─ pops when target hostile
  ├─ PvERotationTask(class-spec)
  │    └─ class-specific child casts
  ├─ LootCorpseTask(corpseGuid)
  │    └─ pushes CMSG_LOOT, CMSG_AUTOSTORE_LOOT_ITEM, CMSG_LOOT_RELEASE
  └─ (repeats for next hotspot until counter met)
```

### Wire actions

`ObjectiveType` values touched per quest cycle:
`TravelTo`, `Interact`, `AcceptQuest`, `StartMeleeAttack` /
`CastSpell`, `Loot`, `TurnInQuest`, `StartMovement` / `StopMovement`.

### Reference

- Implementation plan: [`Plan/Activities/quests.md`](../../Plan/Activities/quests.md).
- Leveling-guide bracket walkthroughs: [`leveling-guide/sections/`](../../leveling-guide/sections/).
- Per-zone leveling guide (example): [`leveling-guide/zones/elwynn-forest.md`](../../leveling-guide/zones/elwynn-forest.md).

---

## Loop 3 — Dungeons

### Activity

`dungeon.<name>` (21 rows from Ragefire Chasm through Stratholme).
`MinPlayers=5`, `MaxPlayers=5`, `RoleTemplate{Tanks=1, Healers=1, Dps=3}`
(LBRS/UBRS go 10-man).

### Objective sequence (typical clear)

```
1. group-form                  (Social — synthesized by DungeoneeringCoordinator quorum)
2. travel-to-portal            (Travel)
3. enter-instance              (Interact gameobject)
4. clear-trash-to-named-N      (Encounter)  ← N Objectives, one per named boss
5. kill-named-N                (Encounter)
6. loot-named-N                (Collect)
7. … repeat 4-6 per boss
8. (optional) pickup-dungeon-quest-objectives  (Kill / Collect / Interact)
9. exit-instance               (Travel)
```

Per-dungeon Objective[4..6] sequences are authored per encounter in
`Bot/dungeons/<id>.json` (Phase 2 deliverable; see SD.* slots in
[`Plan/Activities/dungeons.md`](../../Plan/Activities/dungeons.md#dungeon-slots)).

### Task fan-out

```
DungeoneeringTask(map, route)
  ├─ TravelToTask(instance-portal)
  ├─ UseGameObjectTask(portal)   ← teleport-in
  ├─ (loop) PullStrategyTask(packet of trash)
  │     └─ PvERotationTask
  │     └─ LootCorpseTask
  └─ BossEncounterTask(plan)
        ├─ pre-engage: position by role (tank-spot, melee-spot, ranged-spot)
        ├─ Tick: PvERotationTask + interrupt + dispel sub-tasks
        ├─ OnPhase: re-position; OnAddSpawn: re-target
        └─ post-engage: LootCorpseTask
```

### Wire actions

Adds (over questing): `StartDungeoneering` (Activity-start dispatch),
`SetRaidTarget` (skull / X / square marks), `LootRoll`,
`SelectGossipOption` (for dungeon-quest NPCs inside the instance),
`UseGameObject` (door switches / lever pulls).

### Reference

- [`Plan/Activities/dungeons.md`](../../Plan/Activities/dungeons.md) — full task contracts.
- [`leveling-guide/dungeons/`](../../leveling-guide/dungeons/) — per-dungeon reference walkthroughs.

---

## Loop 4 — Raids

### Activity

`raid.<name>` (7 rows: ZG, AQ20, MC, Onyxia, BWL, AQ40, Naxx).
`MinPlayers=20` (ZG/AQ20) or `40` (others). `RoleTemplate{2-5T 5-10H 13-27D}`.

### Objective sequence (typical clear)

```
1. group-form                       (Social — RaidCompositionService quorum)
2. attunement-gate-check            (Loadout — fixup if OnDemand)
3. travel-to-portal                 (Travel)
4. enter-instance                   (Travel — group teleport)
5. ready-check                      (Social)
6. per-encounter:
     6a. clear-trash-to-boss        (Encounter)
     6b. boss-encounter             (Encounter)
     6c. loot                       (Collect — MasterLoot policy)
7. (sub-loop for trash-only farming runs) ← Activity.TaskFamily=Raid still
8. exit-instance                    (Travel)
```

### Task fan-out

Same shape as Dungeons but with `RaidPositioningTask` (10 marks worth
of positioning, multi-tank assignment), `ReadyCheckTask`,
`MasterLootTask` instead of `GroupLootTask`. Per-encounter
`BossEncounterTask` subclasses live under
`Exports/BotRunner/Tasks/Raid/Encounters/<raid-id>/<BossName>.cs`.

### Wire actions

Adds: `MasterLoot`, `ReadyCheck`, `AcceptReadyCheck`, `RaidConvert`,
`PromoteAssistant`.

### Reference

- [`Plan/Activities/raids.md`](../../Plan/Activities/raids.md).
- [`leveling-guide/raids/`](../../leveling-guide/raids/) — MC, BWL, ZG, AQ40, Naxx, etc.

---

## Loop 5 — Battlegrounds

### Activity

`bg.wsg` / `bg.ab` / `bg.av`. `MinPlayers = MaxPlayers = 10/15/40`.

### Objective sequence (per BG type)

**WSG:**
```
1. queue                       (Queue)
2. enter-instance              (Travel — teleport)
3. role-assignment             (Social)
4. flag-capture-loop:
     4a. travel-to-enemy-base  (Travel)
     4b. pickup-enemy-flag     (Interact gameobject)
     4c. travel-to-own-base    (Travel)
     4d. cap-flag              (Interact gameobject)
5. (loop until 3 caps or time elapses)
6. exit-and-collect-rep        (Travel + Turnin)
```

**AB:** 5 nodes (Stables, Farm, Lumber Mill, Blacksmith, Gold Mine).
Sequence is `Cap` (Interact w/ flag, hold for 60s) iterated per node.

**AV:** flag-cap variant + tower-burn + boss-engagement at the end
(Drek'Thar / Vandaar Stormpike).

### Task fan-out

```
BattlegroundQueueTask(bgType)
  ├─ GoToTask(battlemaster)
  ├─ InteractWithNpcTask
  ├─ ObjectiveType.JoinBattlegroundQueue  ← single Action
  └─ wait for invite → ObjectiveType.AcceptBattlegroundInvite

(post-teleport)
BgObjectiveTask  (per BG type)
  ├─ TravelToTask(objective)
  ├─ InteractWithGameObjectTask(flag/node)
  ├─ HoldPositionTask(60s)             (AB only)
  └─ PvPRotationTask (interrupt cap, defend, escort flag carrier)
```

### Reference

- [`Plan/Activities/battlegrounds.md`](../../Plan/Activities/battlegrounds.md).
- [`leveling-guide/pvp/`](../../leveling-guide/pvp/).

---

## Loop 6 — Professions / gathering

### Activity

`prof.mining-route` / `prof.herbalism-route` / `prof.skinning-route`.
The `Location` is a route name, not a zone — route definitions live in
`Bot/gathering-routes/<id>.json`.

### Objective sequence

```
1. travel-to-route-start       (Travel)
2. route-circuit-loop:
     2a. travel-to-next-waypoint  (Travel)
     2b. scan-for-nodes           (Detect)
     2c. on detection: gather-node  (Interact + Loot)
     2d. (Skinning only) pre-step: kill-skinnable-corpse  (Kill)
3. (loop until skill cap reached for current band, then push next route)
4. travel-to-trainer            (Travel)
5. interact-trainer             (Train)
6. travel-back-to-route         (Travel)
```

### Task fan-out

```
GatheringRouteTask(route, nodeEntries)
  ├─ TravelToTask(waypoint[i])
  ├─ (each tick) scan ObjectManager.GameObjects for entry in nodeEntries within scan-radius
  └─ on detection: push GatherNodeTask(nodeGuid)
        ├─ GoToTask(nodeCoord)
        ├─ InteractWithGameObjectTask(nodeGuid)    ← triggers cast
        ├─ wait for SPELL_GO + LOOT_RELEASE
        └─ LootCorpseTask (gather-window loot)
```

Skinning prepends a `PullStrategyTask` + `PvERotationTask` so the
skinnable target is dead first; the actual interact is on the corpse,
not a node.

### Reference

- [`Plan/Activities/professions-gathering.md`](../../Plan/Activities/professions-gathering.md).
- [`leveling-guide/professions/mining.md`](../../leveling-guide/professions/mining.md), `herbalism.md`, `skinning.md`.

---

## Loop 7 — Professions / crafting + leveling

### Activity

`prof.city-trainer-loop` (single catalog row covers all 6 primary +
3 secondary crafts at the activity layer). Per-profession recipe
progression tables live in `BotRunner.Combat.CraftingData` (today
covers First Aid + Cooking; Phase 1 slot SC.2 extends to primaries).

### Objective sequence

```
1. travel-to-trainer           (Travel)
2. train-available-recipes     (Interact + Train)
3. compute-skill-up-recipe     (Decision — picks highest-XP affordable recipe)
4. material-sourcing-loop:
     4a. inventory-check       (Decision)
     4b. (missing mats) vendor-buy / AH-buy / gather-route   (Economy or Profession-gathering)
5. craft-batch                 (Craft — N casts)
6. (loop until skill cap of current rank)
7. travel-to-trainer-for-next-rank  (Travel)
8. (Engineering 200 / BS 200 / LW 225 / Tailoring 250+) specialization-decision
```

### Task fan-out

```
CraftRecipeTask(recipe, targetCount)
  ├─ MaterialSourcingTask(missing reagents)      (if needed)
  │     ├─ VendorBuyTask                         (cheap reagents)
  │     ├─ AuctionHouseBuyTask                   (expensive reagents)
  │     └─ (push GatheringRouteTask if scarce on AH — Activity hand-off)
  ├─ LearnRecipeTask(recipe)                     (if not yet known)
  ├─ TrainerVisitTask(profession)                (if at skill cap)
  └─ CastCraftSpellTask(spellId) × N
```

### Wire actions

`UseItem` (recipe scroll), `Craft` (spell-cast variant; FG uses Lua
`CastSpellByName`, BG uses `CMSG_CRAFT_ITEM` per slot SC.1),
`PurchaseFromVendor`, `BuyAuctionItem`.

### Reference

- [`Plan/Activities/professions-crafting.md`](../../Plan/Activities/professions-crafting.md).
- [`leveling-guide/professions/`](../../leveling-guide/professions/).

---

## Loop 8 — Economy

### Activity

`econ.ah-restock` (auction-house posting cycle) + `econ.vendor-loop`
(vendor + repair + bank + mail clean-up).

### Objective sequence — `econ.vendor-loop`

```
1. travel-to-city              (Travel)
2. travel-to-mailbox           (Travel)
3. mailbox-retrieve            (Mail)
4. travel-to-vendor            (Travel)
5. sell-junk                   (Economy — VendorSell)
6. repair-all                  (Economy — RepairAll)
7. travel-to-bank              (Travel)
8. bank-deposit-rotated-items  (Economy)
9. travel-to-mailbox           (Travel)
10. mailbox-send-overflow      (Mail)
```

### Objective sequence — `econ.ah-restock`

```
1. travel-to-AH               (Travel)
2. mailbox-retrieve-payouts   (Mail)
3. AH-cancel-expired          (AH — multi-Action)
4. AH-post-new-batch          (AH — multi-Action)
5. mailbox-send-postage-overflow (Mail)
```

### Task fan-out

Each Objective is one or two Tasks composed from the Economy task
family: `AuctionHousePostTask`, `AuctionHouseBuyTask`,
`BankDepositTask`, `BankWithdrawTask`, `MailSendTask`,
`MailRetrieveTask`, `VendorSellTask`, `VendorBuyTask`,
`RepairAllTask`.

### Wire actions

`OpenMail`, `SendMail`, `OpenBank`, `BankDeposit`, `OpenAuction`,
`PostAuction`, `CancelAuction`, `OpenVendor`, `VendorSell`,
`RepairAll`.

### Reference

- [`Plan/Activities/economy.md`](../../Plan/Activities/economy.md).

---

## Loop 9 — Reputation, Attunement, World event, World boss

These four families share a shape: a multi-step **chain** of Objectives
that may span multiple zones, dungeons, raids, and gold/item gates.
They are the most likely to **reference other Activities** via
preconditions (R-AC2).

### Reputation grinds

`rep.timbermaw-hold` / `rep.argent-dawn` / `rep.cenarion-circle` /
`rep.thorium-brotherhood` / `rep.zandalar-tribe`.

Objective sequence pattern:
```
1. (gate) faction-current-standing-< target
2. for each repeatable turn-in available:
     2a. farm-required-item   (Kill / Collect / via embedded Quest)
     2b. travel-to-turnin     (Travel)
     2c. interact-turnin-npc  (Quest TurnIn — repeatable)
3. (re-evaluate gate; loop until standing reaches target tier)
4. (band-promotion gate fires) reputation-target-unlock-graph-edge satisfied
```

### Attunements

`attune.mc` / `attune.ony-horde` / `attune.ony-alliance` /
`attune.bwl` / `attune.naxx`.

Each is a **fixed chain** of quests + item gates. The chain edges live
in [`05_ITEM_REQUIREMENTS.md`](05_ITEM_REQUIREMENTS.md). The
Objective sequence is the linearized topological-sort of the chain.

Example: `attune.mc` is approximately:
```
1. dungeon.ubrs (drakkisath-head)        ← cross-Activity precondition
2. travel-to-MC-portal                   (Travel)
3. interact-Lothos-Riftwaker             (Quest TurnIn)
4. accept-Attunement-quest               (Quest Accept)
5. travel-into-BRM                       (Travel)
6. interact-MC-attunement-orb            (Interact gameobject)
7. travel-back-to-Lothos                 (Travel)
8. turnin-attunement                     (Quest TurnIn)
```

### World events

`event.stv-fishing-extravaganza` — Sunday-only repeatable Activity.
The Objective sequence wraps a `FishingTask` loop with a turn-in step
at the host NPC.

### World bosses

`boss.azuregos` / `boss.kazzak` / `boss.emerald-dragons`. Same
structure as a raid but with `MinPlayers=20`-ish, no instance, and
the boss respawn timer driving when the Coordinator quorums.

### Reference

- [`Plan/Activities/reputations.md`](../../Plan/Activities/reputations.md), `attunements.md`, `world-events.md`, `world-bosses.md`.
- [`leveling-guide/attunements/`](../../leveling-guide/attunements/), `reputations/`.

---

## Cross-cutting loops (no Activity row)

### Recovery

Pushed by the BotRunner reactively, not assigned as an Activity. Tasks:
`ReleaseCorpseTask`, `RetrieveCorpseTask`, `SpiritHealerTask`,
`StuckRecoveryTask`, `ReconnectTask`. They preempt the current Task
stack on the trigger and pop back to the prior top when the recovery
predicate clears.

### Loadout

Pushed at OnDemand Outfitting stage and at autonomous progression
milestones. Tasks: `LoadoutTask` orchestrator, `LearnSpellsTask`,
`LearnTalentsTask`, `SetSkillTask`, `EquipItemTask`. The Activity
layer for Loadout is the OnDemand Activity itself (the `LoadoutTask`
runs during the `Outfitting` stage of the OnDemand pipeline; see
[`Spec/04_ACTIVITIES.md#ondemand-activity-instance-lifecycle`](../../Spec/04_ACTIVITIES.md#ondemand-activity-instance-lifecycle)).

---

## What this lets the DecisionEngine do

For every loop above, the engine asks the same two questions:

1. **Which Activity row is the bot eligible for right now?**
   Answered by the legality validator (see
   [`Spec/04_ACTIVITIES.md#legality-validation`](../../Spec/04_ACTIVITIES.md#legality-validation))
   reading the bot's snapshot, the catalog's `EntryRequirements`, and
   the unlock-graph parents.

2. **What is the next Objective inside that Activity?**
   Answered by `IActivity.NextObjective(snapshot)` (Phase 2 contract) —
   which **does not enumerate Objectives statically**. It composes
   them from the MaNGOS database at runtime per
   [`03_DYNAMIC_COMPOSITION.md`](03_DYNAMIC_COMPOSITION.md).
