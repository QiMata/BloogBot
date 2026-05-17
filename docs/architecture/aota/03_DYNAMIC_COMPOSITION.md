# 03 — Dynamic composition from the MaNGOS database

> Prerequisite: [`02_GAME_LOOPS.md`](02_GAME_LOOPS.md).
>
> **The headline.** The `ActivityCatalog` is **86 hand-authored rows**.
> For each row, the *Objective sequence* the bot follows is **generated
> at runtime** from the MaNGOS database, the bot's snapshot, the
> unlock graph, and the priority bands. This file describes the
> algorithm, the DB tables consulted, and the snapshot fields that
> gate each layer.

## 1. The three inputs

```
                  ┌──────────────────────────────────────────┐
                  │  catalog row (data-only, ~88 lines)     │
                  │   ActivityDefinition                     │
                  │     - Id, Family, Location, LevelRange   │
                  │     - EntryRequirements (items, quests)  │
                  │     - TaskFamily, RewardDefinitions      │
                  └──────────────────┬───────────────────────┘
                                     │
   ┌─────────────────────────────────┼─────────────────────────────────┐
   │                                 │                                 │
   ▼                                 ▼                                 ▼
┌────────────┐               ┌──────────────────┐              ┌───────────────┐
│ MaNGOS DB  │               │  Bot snapshot    │              │  Unlock graph │
│            │               │  (this tick)     │              │  (compiled)   │
│ quest_*    │               │  Level, Class    │              │  parent edges │
│ creature_* │               │  Reputation      │              │  per node     │
│ go_*       │               │  QuestsCompleted │              └───────────────┘
│ item_*     │               │  KeysInBags      │
│ areatrig_* │               │  Attunements     │
└────────────┘               │  Position        │
                             │  Inventory       │
                             └──────────────────┘
                                     │
                                     ▼
                ┌──────────────────────────────────────────┐
                │  DecisionEngine.ComposeObjectives        │
                │   - reads catalog row                    │
                │   - walks quest-chain DAG (DB-backed)    │
                │   - walks item-requirement DAG           │
                │   - filters by snapshot                  │
                │   - sorts by priority + unlock-fanout    │
                └──────────────────────────────────────────┘
                                     │
                                     ▼
                  IActivity instance carrying a generated
                  Objective list for THIS bot at THIS snapshot.
```

## 2. The MaNGOS tables — one column per layer responsibility

The DecisionEngine reads MaNGOS through a read-only `IMangosCatalog`
service (Phase 2 — `Services/WoWStateManager/Catalog/MangosCatalog.cs`).
**No writes**; CLAUDE.md rule R-MaNGOS forbids them outside SOAP, and
this service is on the read side only.

| MaNGOS table | Columns the engine reads | Used by layer |
|---|---|---|
| `quest_template` | `entry`, `Title`, `MinLevel`, `QuestLevel`, `Type`, `RequiredRaces`, `RequiredClasses`, `RequiredSkill`, `RequiredSkillValue`, `LimitTime`, `PrevQuestId`, `NextQuestId`, `ExclusiveGroup`, `NextQuestInChain`, `ReqCreatureOrGOId1..4`, `ReqCreatureOrGOCount1..4`, `ReqItemId1..6`, `ReqItemCount1..6`, `ReqSpellCast1..4`, `RewItemId1..4` + `RewChoiceItemId1..6`, `RewSpellCast`, `RewMoneyMaxLevel`, `SrcItemId`, `SpecialFlags` | Objective generation, Activity legality |
| `quest_relations` (= `creature_questrelation` ∪ `gameobject_questrelation` ∪ `_involvedrelation`) | `quest` (id), `id` (NPC or GO id), and the implicit start/end role | Pickup/turn-in NPC resolution; Travel-Objective coord |
| `creature_template` | `entry`, `name`, `subname`, `MinLevel`, `MaxLevel`, `Faction_A`, `Faction_H`, `NpcFlags`, `MovementType`, `ScriptName`, `Type`, `Family`, `Rank` (elite tier) | Kill-Objective hotspot resolution; pull strategy (elite gate) |
| `creature` (spawns) | `guid`, `id` (entry), `map`, `position_x/y/z`, `spawntimesecsmin/max`, `MovementType`, `event` | Hotspot coord list for Kill / Collect / Interact objectives |
| `gameobject_template` | `entry`, `name`, `type`, `displayId`, `data0..23`, `ScriptName`, `flags` | Interact-Objective resolution (chest, door, lever, herb, ore, fishing pool) |
| `gameobject` (spawns) | `guid`, `id`, `map`, `position_x/y/z`, `spawntimesecs`, `event` | Coord list for Interact / Use objectives |
| `item_template` | `entry`, `name`, `class`, `subclass`, `Quality`, `BuyPrice`, `SellPrice`, `RequiredLevel`, `RequiredSkill`, `RequiredSkillRank`, `RequiredSpell`, `RequiredRaces`, `RequiredClasses`, `bonding`, `startquest`, `spellid_1..5` + `spelltrigger_1..5` (use spells), `bagfamily`, `socketColor_1..3`, `subclass` (key vs. quest-item) | Activity legality (item gates), Reward selection, Collect-Objective itemId match |
| `npc_vendor` | `entry`, `item`, `maxcount`, `incrtime`, `ExtendedCost` | Crafting-material sourcing, vendor-Activity composition |
| `npc_trainer` (1.12) | `entry`, `spell`, `spellcost`, `reqskill`, `reqlevel` | Spell-learning task, profession-train task |
| `areatrigger_teleport` | `id`, `target_map`, `target_position_x/y/z`, `required_level`, `required_item`, `required_quest_done` | Activity legality for instance entries; Travel-Objective target |
| `mangos.reference_loot_template` ∪ `creature_loot_template` ∪ `gameobject_loot_template` ∪ `disenchant_loot_template` ∪ `fishing_loot_template` | `entry`, `item`, `ChanceOrQuestChance`, `mincount`, `maxcount`, `groupid` | Reward selection, Collect-Objective drop-table-aware retry budget |
| `npc_text` / `gossip_menu_option` | `id`, `text0_0..7`, gossip option entry | Gossip-driven legality (BG join, trainer pre-talk, etc.) |
| `character_instance` (player DB, runtime) | `id` (charguid), `instance`, `permanent` | Lockout checks (Activity legality step 5) |

Source-of-truth grounding: tested against the LandSandBoat-MaNGOS
fork running locally at `D:\MaNGOS\` with the 1.12.1 client schema.
Reference: [`docs/Spec/16_REALMS_AND_ACCOUNTS.md`](../../Spec/16_REALMS_AND_ACCOUNTS.md).

## 3. The compose-objectives algorithm

```text
function ComposeObjectives(activity : ActivityDefinition,
                            snapshot  : WoWActivitySnapshot,
                            db        : IMangosCatalog,
                            unlocks   : UnlockGraph) -> IReadOnlyList<IObjective>:

    1. ACTIVITY-PREFLIGHT
       a. validate legality (Spec/04_ACTIVITIES.md 7-step gate)
       b. on fail: return empty list — caller picks alternative

    2. SEED OBJECTIVES FROM THE CATALOG SHAPE
       switch activity.Family:
         case Dungeon | Raid:
           seed = [
             Objective("group-form",    Social),
             Objective("travel-to-portal", Travel, target = activity.TravelTarget),
             Objective("enter-instance",  Interact),
           ]
           seed += per-encounter Objectives loaded from Bot/dungeons/<id>.json
                   (or Bot/raids/<id>.json) — the catalog row points at the file
           seed += [Objective("exit-instance", Travel)]

         case StarterQuesting | ZoneQuesting:
           seed = ComposeQuestingObjectives(activity, snapshot, db)
                  // detailed in section 4 below

         case Battleground:
           seed = ComposeBattlegroundObjectives(activity, snapshot, db)

         case ProfessionGathering:
           seed = [
             Objective("travel-to-route-start", Travel, target = route.start),
             Objective("gather-loop",           Loop, until = skill-cap),
           ]

         case ProfessionCrafting | ProfessionLeveling:
           seed = ComposeCraftingObjectives(activity, snapshot, db)

         case Economy:
           seed = ComposeEconomyObjectives(activity, snapshot, db)
                  // see Plan/Activities/economy.md

         case Reputation:
           seed = ComposeReputationObjectives(activity, snapshot, db, unlocks)

         case Attunement:
           seed = ComposeAttunementObjectives(activity, snapshot, db, unlocks)
                  // walks the attunement chain DAG, see 05_ITEM_REQUIREMENTS.md

         case WorldEvent | WorldBoss:
           seed = catalog-row-specific objective list

    3. PREPEND PRECONDITION OBJECTIVES (cross-Activity)
       for each unmet EntryRequirement of `activity`:
         if RequiredItem is missing:
             prepend the Objective sequence that obtains the item
             (lookup loot source via creature_loot_template / gameobject_loot_template
              or vendor/AH via npc_vendor / observed market data)
         if RequiredQuest is missing:
             prepend the upstream quest-chain Objective sequence
         if RequiredReputation is missing:
             schedule the corresponding rep Activity as a separate
             assignment (NOT inlined — too long to embed)

    4. FILTER BY SNAPSHOT
       for each candidate Objective:
         drop if CheckCompletion(snapshot) is already true
         (no-op; already done; e.g. Kill-Hogger when quest log shows complete)

    5. TOPOLOGICAL SORT BY OBJECTIVE-LEVEL DEPENDENCIES
       (Each Objective declares its own preconditions; e.g.
        loot-rend depends on kill-rend; turn-in-attunement depends on
        accept-attunement. The sort is local-stable so repeat queries
        with identical snapshots produce identical orderings.)

    6. ATTACH PRIORITY AND COST METADATA
       priority = unlocks.Priority(Objective.UnlockNode)  // Survival 1000+ down to Background 100
       costEstimate = Travel-cost from current Position + expected XP/gold/rep gain

    7. RETURN [Objective] list  (length typically 6-40 for a single Activity)
```

## 4. Worked: `ComposeQuestingObjectives`

The most complex sub-algorithm — questing is the longest-tail loop.

```text
function ComposeQuestingObjectives(activity, snapshot, db):
    zone = activity.Location                          // "Westfall"
    zoneId = db.ZoneIdByName(zone)                    // 40
    lvlMin = activity.LevelRange.Min
    lvlMax = activity.LevelRange.Max
    bot = snapshot.Player

    // 4a. Candidate quest set: all quests whose pickup NPC spawns in `zone`
    //     AND whose level fits the bracket AND whose faction/race/class match
    //     AND that the bot has NOT yet completed/abandoned.
    candidates = db.QuestsBy(
        zoneId         = zoneId,
        levelMin       = lvlMin,
        levelMax       = lvlMax,
        races          = race-mask(bot.Race),
        classes        = class-mask(bot.Class),
        excludeIds     = bot.QuestsCompleted ∪ bot.QuestsAbandonedRecently
    )

    // 4b. Build the quest-chain DAG over candidates.
    //     Edges: q.PrevQuestId ── q  AND  q ── q.NextQuestInChain
    //     Group nodes: ExclusiveGroup (pick-one-of-N quests)
    dag = BuildQuestDag(candidates, db.QuestRelations(zoneId))

    // 4c. Compute the topological order with priorities:
    //       * class-quest seeds get priority +200 (per leveling-priority bands)
    //       * group quests (Type=Group, MinPartySize>1) deferred unless party available
    //       * chain heads (PrevQuestId=0) precede chain bodies precede chain tails
    order = TopologicalOrderWithPriority(dag, bot, snapshot)

    // 4d. For each quest in `order`, synthesize Objectives:
    objectives = []
    for quest in order:
        questGiverSpawns = db.QuestStartSpawns(quest.entry, zoneId)
        objectives += [
            Objective($"travel-to-{quest.entry}-pickup", Travel,
                       target = nearest(questGiverSpawns, bot.Position)),
            Objective($"accept-{quest.entry}", Interact,
                       npc = questGiverSpawns[0]),
        ]
        for (idx, objCol) in enumerate(quest.ObjectiveColumns):
            if objCol.IsCreatureKill:
                hotspots = db.CreatureSpawns(objCol.entry, zoneId)
                objectives += [Objective($"kill-{objCol.entry}-q{quest.entry}-o{idx}",
                                          Kill,
                                          creatureEntry = objCol.entry,
                                          requiredCount = objCol.count,
                                          hotspots      = hotspots)]
            elif objCol.IsGameObjectInteract:
                spawns = db.GameObjectSpawns(objCol.entry, zoneId)
                objectives += [Objective(..., Interact, ...)]
            elif objCol.IsItemCollect:
                source = db.LootSourcesForItem(objCol.itemId, zoneId)
                objectives += [Objective(..., Collect,
                                          itemId = objCol.itemId,
                                          sourceCreatureIds  = source.creatures,
                                          sourceGameObjectIds = source.gameObjects)]
            elif objCol.IsSpellCast:
                objectives += [Objective(..., CastSpell, ...)]
            elif objCol.IsEscort:
                objectives += [Objective(..., Escort, npcEntry = objCol.entry)]
        // Turn-in
        turninSpawns = db.QuestTurninSpawns(quest.entry, zoneId)
        objectives += [
            Objective($"travel-to-{quest.entry}-turnin", Travel,
                       target = nearest(turninSpawns, bot.Position)),
            Objective($"turnin-{quest.entry}", TurnIn,
                       npc = turninSpawns[0],
                       rewardIndex = ResolveReward(quest, bot)),
        ]

    // 4e. Append a hub-transition Objective when the zone bracket is
    //     exhausted — points at the next-zone hearthstone-rebind hub.
    if bot.Level >= activity.LevelRange.Max - 1:
        objectives += [Objective("transition-to-next-zone", Travel,
                                  target = NextZoneHubForFaction(bot.Faction))]

    return objectives
```

A typical Westfall (Alliance, level 10-18) bot starting cold returns
**80-140 Objectives** — about 25-35 unique quests × 3-5 Objectives each
+ travel transitions. As the bot completes work, the snapshot
mutates, re-runs of `ComposeObjectives` drop the now-`CheckCompletion`-true
entries, and the sequence shrinks until the Activity is itself
considered complete (zone bracket exhausted).

## 5. How the priority bands ride along

Per [`leveling-guide/decision-engine/leveling-priority.md`](../../leveling-guide/decision-engine/leveling-priority.md):

| Band | Range | Practical effect on dynamic composition |
|---|---|---|
| Survival | 1000–1999 | Preempts the current Objective. Engine pops the stack and pushes a Recovery task (corpse run, eat, repair). The current Activity is *not* unassigned — it resumes when survival predicate clears. |
| Class identity | 800–999 | At the *Activity selection* step, class-quest Activities get +200 priority for their bracket. Visible as Activity-level not Objective-level. |
| Critical-path progression | 600–799 | When evaluating which Activity to assign, attunement / key Activities at this band beat questing for same-bracket-eligible bots. |
| Optimal questing | 400–599 | Default questing Activities. |
| Background / opportunistic | 100–399 | Fallback only — gathered when no higher-band Activity is eligible. |

Within an Activity, Objectives **inherit the Activity's band** but
the engine still **stable-orders** by:

1. Soonest expiring (buff window, lockout, group-formation timer).
2. Lowest travel cost (use current zone before crossing zone borders).
3. Highest fan-out in the unlock graph (pick the Objective whose
   completion unblocks the most downstream nodes).
4. Deterministic Objective id (final tie-break).

## 6. Inputs the engine needs added to the snapshot

Per [`leveling-guide/decision-engine/state-flags.md`](../../leveling-guide/decision-engine/state-flags.md),
the engine's dynamic-composition pass requires these snapshot fields
**beyond** today's shape (status `planned`):

- `TalentTreePoints` — for spec inference (drives `BotSelectionPolicy.ClassUtilityWeight`).
- `ActiveSpec` enum — derived from `TalentTreePoints`.
- `Hearthstone.BoundZoneId` — for hearth-vs-fly decisions in Travel Objectives.
- `BankItems` — bank scan is required when at a city; drives Equipment / Loadout Objectives.
- `EffectiveItemLevel` / `ResistancePool` — used for raid-readiness gates.
- `MountTier` — derived from `RidingSkill` + class-epic spellbook scan.
- `Attunements` enum set — replaces today's free-form `progression_status.current_objective`.
- `OnyxiaQuestProgress` — per-step progress through Marshal Windsor / Eitrigg chain.
- `WorldBuffWindowOpen` — raid-relevant buff is active AND raid scheduled within decay window.
- `Spells` — known-spell set; needed for class-identity quest predicates.
- `HasPet` (Hunter), `RestedXP` — bracket-action gates.

Each of these is also surfaced in [`unlock-graph.md`](../../leveling-guide/decision-engine/unlock-graph.md)
so adding a snapshot field has one ratchet rather than two.

## 7. Per-family composition references

| Family | Composer | Pseudocode location |
|---|---|---|
| Questing | `ComposeQuestingObjectives` | section 4 above + [`04_QUEST_CHAINS.md`](04_QUEST_CHAINS.md) |
| Dungeon | `ComposeDungeonObjectives` | section 3 step 2 + per-dungeon Bot/dungeons/<id>.json |
| Raid | `ComposeRaidObjectives` | section 3 step 2 + per-raid Bot/raids/<id>.json |
| Battleground | `ComposeBattlegroundObjectives` | per-BG hard-coded objective shape (3 BGs only) |
| Profession-Gathering | `ComposeGatheringObjectives` | route file at Bot/gathering-routes/<id>.json |
| Profession-Crafting | `ComposeCraftingObjectives` | recipe progression via `CraftingData` + `npc_trainer` |
| Economy | `ComposeEconomyObjectives` | [`Plan/Activities/economy.md`](../../Plan/Activities/economy.md) |
| Reputation | `ComposeReputationObjectives` | per-faction turn-in chain via `npc_text` / `quest_template` |
| Attunement | `ComposeAttunementObjectives` | [`05_ITEM_REQUIREMENTS.md`](05_ITEM_REQUIREMENTS.md) |
| WorldEvent | `ComposeWorldEventObjectives` | catalog-row-specific (STV Extravaganza only today) |
| WorldBoss | `ComposeWorldBossObjectives` | catalog-row-specific |

## 8. Test surface

For Phase-2 slot S2.0, three contract-shaped tests:

1. **`IActivityContractTests.NextObjective_ReturnsTopologicalNext`** —
   given a snapshot at the start of an Activity, the *first*
   Objective in the composed list is the one with no unmet
   predecessors and the highest priority.
2. **`IActivityContractTests.NextObjective_SkipsCompletedObjectives`** —
   advancing the snapshot to reflect Objective[0] completion causes
   Objective[1] to be returned next.
3. **`IActivityContractTests.ComposeObjectives_HonorsEntryRequirements`** —
   missing required item / quest / rep causes the composed list to
   **prepend** the precondition Objectives, not fail outright.

Live-validation tests sit one layer up — they assert that running the
Activity end-to-end against a real bot produces a snapshot trajectory
whose `(current_activity_id, current_objective_id)` pairs match the
composed sequence in order. See
[`Plan/Activities/00_INDEX.md`](../../Plan/Activities/00_INDEX.md)
per-row "tests" column.
